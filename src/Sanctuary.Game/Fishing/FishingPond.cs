using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Core.Extensions;
using Sanctuary.Database;
using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;
using Sanctuary.Game.Interactions;
using Sanctuary.Game.Resources.Definitions;
using Sanctuary.Game.Zones;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game.Fishing;

/// <summary>
/// One fishing zone's fishing: Fishermen get the client's fishing turned on as they arrive, cast onto the pond and
/// reel in what bites. The client only animates, so every nibble, bite, escape and catch is decided and timed here
/// (see <see cref="FishingRules"/>). Players around a fisher see their casts, bobbers and catches.
/// </summary>
public sealed class FishingPond
{
    /// <summary>
    /// The run's first fish is the one the cast will catch; the others swim round the lure.
    /// </summary>
    private const int TargetFishId = 1;

    private const int LeaveButtonTextId = 103018;   // Leave
    private const int LeaveIconId = 1345;
    private const string LureEffectGroup = "FishingLure";

    private readonly WorldZone _zone;
    private readonly ILogger _logger;
    private readonly IResourceManager _resourceManager;
    private readonly IRewardManager _rewardManager;
    private readonly IDbContextFactory<DatabaseContext> _dbContextFactory;

    private readonly object _lock = new();
    private readonly Dictionary<ulong, FishingSession> _sessions = [];
    private readonly Random _random = new();

    /// <summary>
    /// Where each player went fishing from, and the job the pond switched them out of, by player guid. Going on to
    /// another pond keeps the trip, so Leave still ends where it began.
    /// </summary>
    private static readonly ConcurrentDictionary<ulong, FishingTrip> Trips = new();

    private ulong _guideGuid;

    public FishingPond(WorldZone zone, IServiceProvider serviceProvider)
    {
        _zone = zone;
        _logger = serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(FishingPond));
        _resourceManager = serviceProvider.GetRequiredService<IResourceManager>();
        _rewardManager = serviceProvider.GetRequiredService<IRewardManager>();
        _dbContextFactory = serviceProvider.GetRequiredService<IDbContextFactory<DatabaseContext>>();
    }

    /// <summary>
    /// Read at each use, so a reload of Fishing.json takes effect at once.
    /// </summary>
    private FishingPondDefinition? Definition => _resourceManager.Fishing.TryGetPond(_zone.Name, out var pond) ? pond : null;

    /// <summary>
    /// Puts the pond's fisherman by the arrival spot: he sells rods and lures (Npcs.json) and takes players back.
    /// </summary>
    public void OnStart()
    {
        if (_guideGuid != 0 || Definition is not { } pond)
            return;

        var spot = pond.Guide.Position;

        if (_zone.TrySpawnNpc(_resourceManager.Fishing.Definition.GuideNpcId, null, spot[0], spot[1], spot[2], pond.Guide.Heading, 0f, 0f, out var guide)
            && guide is Npc npc)
        {
            _guideGuid = npc.Guid;

            // A click opens his menu (Leave, and the shop) before this runs; having it shows players he can be clicked.
            npc.InteractAction = player => InteractionMenuHelper.TryOpenMenu(player, npc, GetInteractionOptions(player, npc));
        }
        else
            _logger.LogWarning("{zone}: couldn't place the fishing guide (Npcs.json {id}).", _zone.Name, _resourceManager.Fishing.Definition.GuideNpcId);
    }

    public IReadOnlyList<NpcInteractionOption> GetInteractionOptions(Player player, Npc npc)
    {
        if (npc.Guid != _guideGuid || Definition is null)
            return [];

        return
        [
            new NpcInteractionOption
            {
                IconId = LeaveIconId,
                ButtonTextId = LeaveButtonTextId,
                Invoke = Leave
            }
        ];
    }

    /// <summary>
    /// Takes a player out of the pond they're in, as the fisherman's Leave does. False if they aren't in one.
    /// </summary>
    public static bool TryLeave(Player player)
    {
        if ((player.Zone as WorldZone)?.Fishing is not { } pond)
            return false;

        pond.Leave(player);
        return true;
    }

    /// <summary>
    /// Back to where the player went fishing from (or the pond's fishing hole, if that's gone), in the job they had
    /// before the pond made them a Fisherman. Changing jobs ends their fishing before they go.
    /// </summary>
    private void Leave(Player player)
    {
        if (!ReferenceEquals(player.Zone, _zone) || Definition is not { } pond)
            return;

        Trips.TryRemove(player.Guid, out var trip);

        if (trip is { PreviousProfileId: not 0 } && trip.PreviousProfileId != player.ActiveProfileId)
            player.ActivateProfile(trip.PreviousProfileId);

        if (trip is { ZoneId: not 0 } && player.TeleportToZone(trip.ZoneId, trip.OwnerId, trip.Position, trip.Rotation))
            return;

        var position = new Vector4(pond.Return.Position[0], pond.Return.Position[1], pond.Return.Position[2], 1f);

        player.TeleportToZone(pond.ReturnZoneId, null, position, HeadingRotation(pond.Return.Heading));
    }

    /// <summary>
    /// Sends a player from a Fabled Realms fishing hole (or the Games window) to its pond, remembering where they
    /// were for the way back.
    /// </summary>
    public static bool GoFishing(Player player, FishingPondDefinition pond)
    {
        var position = new Vector4(pond.Arrival.Position[0], pond.Arrival.Position[1], pond.Arrival.Position[2], 1f);

        var zone = player.Zone as WorldZone;

        if (zone?.Fishing is not null && Trips.TryGetValue(player.Guid, out var trip))
            trip.PondZoneId = pond.ZoneId;
        else if (zone is { Fishing: not null } or { Battle: not null } or { Arena: not null })
            // Nowhere to come back to: Leave goes to the pond's fishing hole.
            Trips[player.Guid] = new FishingTrip(0, null, default, default, pond.ZoneId);
        else
            Trips[player.Guid] = new FishingTrip(player.Zone.DefinitionId, player.Zone.OwnerId, player.Position, player.Rotation, pond.ZoneId);

        return player.TeleportToZone(pond.ZoneId, null, position, HeadingRotation(pond.Arrival.Heading));
    }

    private static Quaternion HeadingRotation(float heading) => new(MathF.Sin(heading), 0f, MathF.Cos(heading), 0f);

    private const int ActivityIconId = 9870;
    private const string ActivityImage = "Fishing_RainbowLake.dds";

    /// <summary>
    /// The Games window's Fishing tile: each pond's entries, which PLAY sends players to (see <see cref="GoFishing"/>).
    /// </summary>
    public static IEnumerable<ActivityDefinition> GetActivities(IResourceManager resourceManager) =>
        resourceManager.Fishing.Ponds.SelectMany(pond => pond.Activities).OrderBy(activity => activity.Id).Select(activity => new ActivityDefinition
        {
            Id = activity.Id,
            Type = BaseActivityServicePacket.MinigameActivityType,
            Category = FishingRules.ActivityCategory,
            ImageSetId = ActivityIconId,
            DisplayNameId = activity.NameId,
            DisplayDescriptionId = activity.DescriptionId,
            NameId = activity.NameId,
            DescriptionId = activity.DescriptionId,
            CanPlayerJoin = true,
            DetailImageFilename = ActivityImage,
            ThumbnailImageFilename = ActivityImage,
            Difficulty = 1
        });

    #region Fishers

    /// <summary>
    /// A player has loaded in: a Fisherman starts fishing; anyone else switches to the Fisherman job first, as
    /// picking a fight puts a player in their combat job.
    /// </summary>
    public void OnPlayerArrived(Player player)
    {
        if (player.ActiveProfileId == FishingRules.FishermanProfileId)
            StartFishing(player);
        else
        {
            var previousProfileId = player.ActiveProfileId;

            // Switching jobs comes back through OnProfileChanged, which starts fishing.
            if (player.ActivateProfile(FishingRules.FishermanProfileId))
            {
                if (Trips.TryGetValue(player.Guid, out var trip) && trip.PreviousProfileId == 0)
                    trip.PreviousProfileId = previousProfileId;

                ChatHelper.SendSystemMessage(player, "You switched to your Fisherman job to fish.");
            }
            else
                ChatHelper.SendSystemMessage(player, "You need the Fisherman job to fish here.");
        }

        // Nothing on the client's fishing screen says how to leave.
        ChatHelper.SendSystemMessage(player, "When you're done, talk to the Fisherman by the dock (or press X beside him) to head back.");
    }

    public void OnProfileChanged(Player player)
    {
        if (!ReferenceEquals(player.Zone, _zone))
            return;

        if (player.ActiveProfileId == FishingRules.FishermanProfileId)
            StartFishing(player);
        else
            StopFishing(player, notify: true);
    }

    public void OnPlayerLeft(Player player)
    {
        StopFishing(player, notify: false);

        // The trip ends wherever they went, unless GoFishing just pointed it at another pond.
        if (Trips.TryGetValue(player.Guid, out var trip) && trip.PondZoneId == _zone.DefinitionId)
            Trips.TryRemove(new KeyValuePair<ulong, FishingTrip>(player.Guid, trip));
    }

    private void StartFishing(Player player)
    {
        lock (_lock)
        {
            if (_sessions.ContainsKey(player.Guid))
                return;

            _sessions[player.Guid] = new FishingSession(player);
        }

        EnsureRod(player);

        player.SetFishing(true);
    }

    private void StopFishing(Player player, bool notify)
    {
        lock (_lock)
            _sessions.Remove(player.Guid);

        player.SetFishing(false, notify);
    }

    /// <summary>
    /// A changed rod casts further or less far: the client is sent its new range between casts.
    /// </summary>
    public void OnGearChanged(Player player)
    {
        lock (_lock)
        {
            if (!_sessions.TryGetValue(player.Guid, out var session) || !session.Registered || session.State != FishingState.Idle)
                return;
        }

        SendSetup(player, spawnSchools: false);
    }

    /// <summary>
    /// The client casts only with something in its hands, so a Fisherman without a rod is handed their best one,
    /// or a starter rod if they have none.
    /// </summary>
    private void EnsureRod(Player player)
    {
        if (GetRod(player) is not null)
            return;

        var rank = player.ActiveProfile.Rank;

        var rod = player.Items
            .Select(item => (Item: item, Definition: GetItemDefinition(item.Definition)))
            .Where(entry => entry.Definition is not null && entry.Definition.Slot == Player.WeaponSlot
                && entry.Definition.MinProfileRank <= rank && _resourceManager.Fishing.GetRod(entry.Definition.Param1) is not null)
            .OrderByDescending(entry => _resourceManager.Fishing.GetRod(entry.Definition!.Param1)!.Tier)
            .FirstOrDefault();

        if (rod.Item is null)
        {
            var starterRodId = _resourceManager.Fishing.Definition.StarterRodItemId;

            if (!_rewardManager.TryGrantItem(player, starterRodId, 0) || player.Items.LastOrDefault(item => item.Definition == starterRodId) is not { } starter)
            {
                _logger.LogWarning("{zone}: couldn't give {player} a fishing rod.", _zone.Name, player.Name.FullName);
                return;
            }

            rod = (starter, GetItemDefinition(starterRodId));
            ChatHelper.SendSystemMessage(player, "Here's a fishing rod to get you started. Click the water to cast!");
        }

        var definition = rod.Definition!;
        var profileId = player.ActiveProfileId;
        var oldItemId = EquipmentHelper.SetProfileItem(player.ActiveProfile, rod.Item, definition);

        using (var dbContext = _dbContextFactory.CreateDbContext())
        {
            if (!EquipmentHelper.SaveProfileItem(dbContext, player.Guid, profileId, oldItemId, rod.Item.Id))
                _logger.LogWarning("{zone}: couldn't save {player}'s fishing rod.", _zone.Name, player.Name.FullName);
        }

        EquipmentHelper.SendEquipped(player, profileId, definition.Slot, rod.Item, definition);
    }

    private ClientItemDefinition? GetItemDefinition(int id) =>
        _resourceManager.ClientItemDefinitions.TryGetValue(id, out var definition) ? definition : null;

    /// <summary>
    /// The rod the player is holding, or null if what's in their weapon slot isn't one.
    /// </summary>
    private FishingRodDefinition? GetRod(Player player) =>
        player.GetEquippedItem(Player.WeaponSlot) is { } item
        && _resourceManager.ClientItemDefinitions.TryGetValue(item.Definition, out var definition)
            ? _resourceManager.Fishing.GetRod(definition.Param1)
            : null;

    #endregion

    #region Client requests

    /// <summary>
    /// The client's fishing came on: it gets its casting range, camera and the pond's fish and schools.
    /// </summary>
    public void Register(Player player)
    {
        lock (_lock)
        {
            if (!_sessions.TryGetValue(player.Guid, out var session))
                return;

            session.Registered = true;
        }

        SendSetup(player, spawnSchools: true);
    }

    private void SendSetup(Player player, bool spawnSchools)
    {
        if (Definition is not { } pond)
            return;

        var rod = GetRod(player);
        var fish = pond.Fish.Select(id => _resourceManager.Fishing.Fish.GetValueOrDefault(id)).OfType<FishDefinition>().ToList();

        var response = new FishingPacketRegisterPlayerResponse
        {
            PlayerConfig =
            {
                BobberMinDistanceFromPlayer = 1f,
                MinCastDistance = FishingRules.MinCastDistance,
                MaxCastDistance = rod?.MaxCastDistance ?? _resourceManager.Fishing.Definition.Rods.Min(entry => entry.MaxCastDistance),
                MinTargetRadius = 0.5f,
                MaxTargetRadius = 1.5f,
                BobberSplashCompositeEffectId = FishingRules.BobberSplashEffectId,
                // ControllerFishing's own defaults: metres, radians (world heading) and the share of the gap the
                // camera closes every 30th of a second.
                CameraDistance = 6f,
                CameraPitch = 0.445f,
                CameraHeading = 1.85f,
                CameraTargetHeight = 0.2f,
                FishMaxDistanceFromHook = 10f,
                FishStartDistanceFromHook = 3f,
                MaxNibbleTime = FishingRules.BiteDelayMaxMs / 1000f
            },
            ZoneConfig =
            {
                UnderwaterSky = pond.UnderwaterSky,
                UnderwaterRunScale = FishingRules.UnderwaterRunScale,
                UnderwaterRunOffset = FishingRules.UnderwaterRunOffset,
                ReelDuration = FishingRules.ReelMs(1, rod?.Tier ?? 0) / 1000f,
                BobberGroundCheckMinY = pond.WaterHeight - 20f
            },
            PreloadModelIds =
            [
                rod?.BobberModelId ?? FishingRules.StarterBobberModelId,
                FishingRules.LureModelId,
                FishingRules.TreasureChestModelId,
                FishingRules.CaughtJunkModelId,
                .. Enum.GetValues<FishSize>().Select(FishingRules.CaughtFishModelId),
                .. fish.Select(entry => entry.ModelId).Distinct(),
                .. pond.Schools.Select(school => school.ModelId).Distinct()
            ],
            Fish = fish.Select(entry => new ClientFishEntryInfo
            {
                FishType = entry.Id,
                NameId = entry.NameId,
                IconId = entry.IconId,
                Special = entry.Rarity >= 4,
                LureNameId = GetItemDefinition(entry.LureItemId)?.NameId ?? 0,
                SchoolId = pond.Schools.FirstOrDefault(school => school.Fish.Contains(entry.Id))?.Id ?? 0,
                Catchable = true,
                FishFinderEffectId = FishingRules.FishFinderEffectId(entry.Rarity)
            }).ToList()
        };

        player.SendTunneled(response);

        if (!spawnSchools)
            return;

        foreach (var school in pond.Schools)
        {
            player.SendTunneled(new FishingPacketSpawnProxiedFishingSchool
            {
                SchoolId = school.Id,
                Position = new Vector4(school.Position[0], school.Position[1], school.Position[2], 1f),
                Models = [new FishingSchoolModel { ModelId = school.ModelId, Heading = school.Id * 1.3f % MathF.Tau, AnimationParameter = 1f }],
                CompositeEffectIds = [FishingRules.SchoolEffectId]
            });
        }
    }

    /// <summary>
    /// The players around see the fisher wind up.
    /// </summary>
    public void CastAnimation(Player player, float distance, Vector4 direction)
    {
        lock (_lock)
        {
            if (!_sessions.TryGetValue(player.Guid, out var session) || session.State != FishingState.Idle)
                return;
        }

        player.SendTunneledToVisible(new FishingPacketCastAnimRequest
        {
            Guid = player.Guid,
            Distance = distance,
            Direction = direction
        });
    }

    /// <summary>
    /// The cast reached the water: if it's in range and on the pond, the bobber goes in and what it'll bring up
    /// is rolled. The client repeats this until the bobber shows, so only the first counts.
    /// </summary>
    public void Cast(Player player, Vector4 target)
    {
        if (Definition is not { } pond)
            return;

        var rod = GetRod(player);
        var maxCastDistance = rod?.MaxCastDistance ?? _resourceManager.Fishing.Definition.Rods.Min(entry => entry.MaxCastDistance);

        var distance = Vector3.Distance(player.Position.ToVector3(), target.ToVector3());

        var valid = float.IsFinite(distance)
            && distance <= maxCastDistance + FishingRules.CastDistanceSlack
            && MathF.Abs(target.Y - pond.WaterHeight) <= FishingRules.WaterHeightTolerance;

        lock (_lock)
        {
            if (!_sessions.TryGetValue(player.Guid, out var session) || !session.Registered || session.State != FishingState.Idle)
                return;

            if (!valid)
            {
                _logger.LogDebug("{zone}: refused {player}'s cast to {target}, {distance:0.0} m out (water at {water}).",
                    _zone.Name, player.Name.FullName, target, distance, pond.WaterHeight);

                SendResult(player, new FishingPacketFishingResult { Guid = player.Guid, Type = FishingResultType.Cancel });
                return;
            }

            var school = pond.Schools.FirstOrDefault(entry =>
                Vector2.Distance(new Vector2(entry.Position[0], entry.Position[2]), new Vector2(target.X, target.Z)) <= FishingRules.SchoolRadius);

            var lureItemId = GetActiveLure(player);

            session.RodTier = rod?.Tier ?? 0;
            session.Catch = FishingRules.Roll(new FishingCastContext
            {
                PondFish = pond.Fish.Select(id => _resourceManager.Fishing.Fish.GetValueOrDefault(id)).OfType<FishDefinition>().ToList(),
                SchoolFish = school?.Fish ?? [],
                Junk = _resourceManager.Fishing.Definition.Junk,
                Treasure = _resourceManager.Fishing.Definition.Treasure,
                LureItemId = lureItemId ?? 0,
                TreasureLure = lureItemId == 0,
                RodTier = session.RodTier
            }, _random);

            session.State = FishingState.Casting;
            session.CastAt = Environment.TickCount64;
            session.NextAt = session.CastAt + FishingRules.FishRunDelayMs;
        }

        var forward = Vector3.Normalize(new Vector3(target.X - player.Position.X, 0f, target.Z - player.Position.Z));

        player.SendTunneledToVisible(new FishingPacketSpawnProxiedFishingBobber
        {
            Guid = player.Guid,
            ModelId = rod?.BobberModelId ?? FishingRules.StarterBobberModelId,
            Position = new Vector4(target.X, target.Y, target.Z, 1f),
            Forward = float.IsFinite(forward.X) ? new Vector4(forward, 0f) : Vector4.UnitZ
        }, true);
    }

    /// <summary>
    /// The lure (item id) the player has in use: 0 for the Treasure Magnet, which tempts no fish, and null for none.
    /// A lure's buff carries its item's name (see ItemAbilities.json's FishingLure group).
    /// </summary>
    private int? GetActiveLure(Player player)
    {
        if (player.Effects.FirstOrDefault(effect => effect.Group == LureEffectGroup) is not { } effect)
            return null;

        var lured = _resourceManager.Fishing.Fish.Values.FirstOrDefault(fish =>
            GetItemDefinition(fish.LureItemId)?.NameId == effect.BuffNameId);

        return lured?.LureItemId ?? 0;
    }

    /// <summary>
    /// The fisher clicked to reel in: a fish on the bite (or a chest by the lure) is hooked and landed after a
    /// moment; anything earlier scares the fish off.
    /// </summary>
    public void ReelIn(Player player)
    {
        FishingPacketFishingResult? result = null;

        lock (_lock)
        {
            if (!_sessions.TryGetValue(player.Guid, out var session))
                return;

            var now = Environment.TickCount64;

            switch (session.State)
            {
                case FishingState.Biting:
                case FishingState.ItemWaiting:
                    var fishingCatch = session.Catch!;

                    session.State = FishingState.Reeling;
                    session.NextAt = now + FishingRules.ReelMs(fishingCatch.Rarity, session.RodTier);
                    session.Escapes = fishingCatch.Kind == FishingCatchKind.Fish
                        && _random.NextDouble() < FishingRules.ReelEscapeChance(fishingCatch.Rarity, session.RodTier);

                    result = new FishingPacketFishingResult { Guid = player.Guid, Type = FishingResultType.Hooked, IsItem = fishingCatch.IsItem };
                    break;

                case FishingState.Waiting:
                case FishingState.Nibbling:
                case FishingState.Empty:
                    session.State = FishingState.Idle;
                    result = new FishingPacketFishingResult { Guid = player.Guid, Type = FishingResultType.NothingCaught };
                    break;

                default:
                    return;
            }
        }

        if (result.Type == FishingResultType.Hooked)
            player.SendTunneledToVisible(new FishingPacketReelInRequest { Guid = player.Guid, Reeling = true });

        SendResult(player, result);
    }

    #endregion

    #region Update

    /// <summary>
    /// Runs every zone tick: the fish nibble, bite, get away or come up on time.
    /// </summary>
    public void Update()
    {
        List<(FishingSession Session, FishingState State)>? due = null;

        var now = Environment.TickCount64;

        lock (_lock)
        {
            foreach (var session in _sessions.Values)
            {
                if (session.State == FishingState.Idle || now < session.NextAt)
                    continue;

                (due ??= []).Add((session, session.State));
            }

            if (due is null)
                return;

            foreach (var (session, _) in due)
                Advance(session, now);
        }

        foreach (var (session, state) in due)
        {
            if (state == FishingState.Reeling && session.State == FishingState.Idle && session.Landed is { } landed)
                Grant(session.Player, landed);
        }
    }

    /// <summary>
    /// The next step of a cast whose time has come. Called under the lock; rewards are granted outside it.
    /// </summary>
    private void Advance(FishingSession session, long now)
    {
        var player = session.Player;
        var fishingCatch = session.Catch!;

        session.Landed = null;

        switch (session.State)
        {
            case FishingState.Casting:
                SendFishRun(session);

                (session.State, session.NextAt) = fishingCatch.Kind switch
                {
                    FishingCatchKind.Fish => (FishingState.Waiting, now + _random.NextInt64(FishingRules.NibbleDelayMinMs, FishingRules.NibbleDelayMaxMs)),
                    FishingCatchKind.Nothing => (FishingState.Empty, now + FishingRules.EmptyRunMs),
                    _ => (FishingState.ItemWaiting, now + FishingRules.ItemWaitMs)
                };
                break;

            case FishingState.Waiting:
                player.SendTunneled(new FishingPacketUpdateProxiedFishingBobber { Guid = player.Guid, FishId = TargetFishId, Nibble = true });

                session.State = FishingState.Nibbling;
                session.NextAt = now + _random.NextInt64(FishingRules.BiteDelayMinMs, FishingRules.BiteDelayMaxMs);
                break;

            case FishingState.Nibbling:
                player.SendTunneled(new FishingPacketUpdateProxiedFishingBobber { Guid = player.Guid, FishId = TargetFishId, Bite = true });

                session.State = FishingState.Biting;
                session.NextAt = now + FishingRules.BiteLockMs + FishingRules.BiteWindowMs(fishingCatch.Rarity, session.RodTier);
                break;

            case FishingState.Biting:
            case FishingState.ItemWaiting:
                GetAway(session);
                break;

            case FishingState.Empty:
                session.State = FishingState.Idle;
                SendResult(player, new FishingPacketFishingResult { Guid = player.Guid, Type = FishingResultType.NothingCaught });
                break;

            case FishingState.Reeling:
                if (session.Escapes)
                {
                    GetAway(session);
                    break;
                }

                session.State = FishingState.Idle;
                session.Landed = fishingCatch;
                SendResult(player, GetCaughtResult(session, fishingCatch, now));
                break;
        }
    }

    /// <summary>
    /// The fish swims off (or the chest drifts away): "fish escaped" or "item lost", then the line comes in.
    /// </summary>
    private void GetAway(FishingSession session)
    {
        var player = session.Player;

        session.State = FishingState.Idle;

        player.SendTunneledToVisible(new FishingPacketUpdateProxiedFishingBobber { Guid = player.Guid, FishId = TargetFishId }, true);

        SendResult(player, new FishingPacketFishingResult { Guid = player.Guid, Type = FishingResultType.Lost, IsItem = session.Catch!.IsItem });
    }

    /// <summary>
    /// The lure and line go in, then the fish around it: the one the cast will catch (or the chest) first, and a
    /// few others to make it a choice.
    /// </summary>
    private void SendFishRun(FishingSession session)
    {
        var player = session.Player;
        var fishingCatch = session.Catch!;

        player.SendTunneled(new FishingPacketSpawnFishRun { LureModelId = FishingRules.LureModelId });

        var run = new List<UnderwaterFishSpawnInfo>();

        if (fishingCatch.IsItem)
            run.Add(new UnderwaterFishSpawnInfo { Id = TargetFishId, ModelId = FishingRules.TreasureChestModelId, SizeClass = 1, IsItem = true });
        else if (fishingCatch.Fish is { } fish)
            run.Add(new UnderwaterFishSpawnInfo { Id = TargetFishId, ModelId = fish.ModelId, SizeClass = (int)fishingCatch.Size });

        var others = Definition?.Fish
            .Where(id => id != fishingCatch.Fish?.Id)
            .Select(id => _resourceManager.Fishing.Fish.GetValueOrDefault(id))
            .OfType<FishDefinition>()
            .OrderBy(_ => _random.Next())
            .Take(_random.Next(fishingCatch.Kind == FishingCatchKind.Nothing ? 2 : 1, 4))
            .ToList() ?? [];

        foreach (var other in others)
        {
            var sizes = other.Items.Keys.ToList();

            run.Add(new UnderwaterFishSpawnInfo
            {
                Id = run.Count + 1,
                ModelId = other.ModelId,
                SizeClass = (int)sizes[_random.Next(sizes.Count)]
            });
        }

        player.SendTunneled(new FishingPacketSpawnFishRun { LureModelId = FishingRules.LureModelId, Fish = run });
    }

    private FishingPacketFishingResult GetCaughtResult(FishingSession session, FishingCatch fishingCatch, long now)
    {
        var result = new FishingPacketFishingResult
        {
            Guid = session.Player.Guid,
            Type = FishingResultType.Caught,
            IsItem = fishingCatch.IsItem,
            TimeToCatch = (now - session.CastAt) / 1000f,
            Difficulty = fishingCatch.Rarity,
            Rarity = fishingCatch.Rarity,
            Score = FishingRules.Score(fishingCatch),
            DisplayScale = 1
        };

        switch (fishingCatch.Kind)
        {
            case FishingCatchKind.Fish:
                result.NameId = fishingCatch.Fish!.NameId;
                result.IconId = fishingCatch.Fish.IconId;
                result.Weight = fishingCatch.Weight;
                result.Size = (int)fishingCatch.Size;
                result.CaughtModelId = FishingRules.CaughtFishModelId(fishingCatch.Size);
                result.CatchCompositeEffectId = FishingRules.CatchFishEffectId;
                result.DisplayScale = (int)fishingCatch.Size;
                break;

            case FishingCatchKind.Treasure:
                result.NameId = fishingCatch.Treasure!.NameId;
                result.IconId = fishingCatch.Treasure.IconId;
                result.CaughtModelId = FishingRules.TreasureChestModelId;
                result.CatchCompositeEffectId = FishingRules.CatchChestEffectId;
                break;

            case FishingCatchKind.Junk:
                var junk = GetItemDefinition(fishingCatch.ItemId);

                result.NameId = junk?.NameId ?? 0;
                result.IconId = junk?.Icon.Id ?? 0;
                result.CaughtModelId = FishingRules.CaughtJunkModelId;
                break;
        }

        return result;
    }

    /// <summary>
    /// The catch goes in the fisher's bags (a pouch or chest opens into its reward), and the Fisherman job earns
    /// its stars.
    /// </summary>
    private void Grant(Player player, FishingCatch fishingCatch)
    {
        switch (fishingCatch.Kind)
        {
            case FishingCatchKind.Fish:
            case FishingCatchKind.Junk:
                if (!_rewardManager.TryGrantItem(player, fishingCatch.ItemId, 0))
                    _logger.LogWarning("{zone}: couldn't give {player} their catch (item {item}).", _zone.Name, player.Name.FullName, fishingCatch.ItemId);
                break;

            case FishingCatchKind.Treasure:
                if (_rewardManager.TryRollReward(fishingCatch.Treasure!.RewardTable, out var drop) && drop is not null)
                    _rewardManager.TryGrantReward(player, drop);
                break;
        }

        _rewardManager.TryGrantExperience(player, player.ApplyExperienceBoost(FishingRules.Stars(fishingCatch)), FishingRules.FishermanProfileId);
    }

    /// <summary>
    /// Results go to the players around too: they see a catch over the fisher's head, and their view of the
    /// fisher's line comes back in.
    /// </summary>
    private static void SendResult(Player player, FishingPacketFishingResult result) => player.SendTunneledToVisible(result, true);

    #endregion

    private enum FishingState
    {
        Idle,
        /// <summary>The bobber is in the air.</summary>
        Casting,
        /// <summary>A fish is on its way to the lure.</summary>
        Waiting,
        Nibbling,
        /// <summary>The fish has bitten: reel now.</summary>
        Biting,
        /// <summary>A chest or a boot sits by the lure.</summary>
        ItemWaiting,
        /// <summary>Nothing's biting this time.</summary>
        Empty,
        Reeling
    }

    private sealed class FishingTrip(int zoneId, ulong? ownerId, Vector4 position, Quaternion rotation, int pondZoneId)
    {
        /// <summary>
        /// The zone they came from, or 0 if it was one there's no going back to.
        /// </summary>
        public int ZoneId { get; } = zoneId;

        public ulong? OwnerId { get; } = ownerId;

        public Vector4 Position { get; } = position;

        public Quaternion Rotation { get; } = rotation;

        /// <summary>
        /// The pond the player was last sent to.
        /// </summary>
        public int PondZoneId { get; set; } = pondZoneId;

        /// <summary>
        /// The job the first pond switched them out of, or 0 if they came as a Fisherman.
        /// </summary>
        public int PreviousProfileId { get; set; }
    }

    private sealed class FishingSession(Player player)
    {
        public Player Player { get; } = player;

        /// <summary>
        /// The client has asked for the pond's setup, so it's fishing.
        /// </summary>
        public bool Registered { get; set; }

        public FishingState State { get; set; }

        public FishingCatch? Catch { get; set; }

        public int RodTier { get; set; }

        public long CastAt { get; set; }

        public long NextAt { get; set; }

        /// <summary>
        /// A hooked fish breaks free before it's landed.
        /// </summary>
        public bool Escapes { get; set; }

        /// <summary>
        /// Landed this tick, to be granted outside the lock.
        /// </summary>
        public FishingCatch? Landed { get; set; }
    }
}
