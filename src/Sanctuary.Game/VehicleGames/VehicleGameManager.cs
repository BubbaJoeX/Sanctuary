using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Sanctuary.Core.Extensions;
using Sanctuary.Database;
using Sanctuary.Game.Achievements;
using Sanctuary.Game.Battles;
using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;
using Sanctuary.Game.Interactions;
using Sanctuary.Game.Minigames;
using Sanctuary.Game.Quests;
using Sanctuary.Game.Resources;
using Sanctuary.Game.Resources.Definitions;
using Sanctuary.Game.Resources.Definitions.Rewards;
using Sanctuary.Game.VehicleGames.Derby;
using Sanctuary.Game.VehicleGames.Garage;
using Sanctuary.Game.VehicleGames.Race;
using Sanctuary.Game.VehicleGames.Soccer;
using Sanctuary.Game.Zones;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game.VehicleGames;

/// <summary>
/// Runs the 3D games (kart racing, the demolition derby, soccer) and the kart garage, as the live game did: the game's
/// NPC (or Games window tile) puts up its start panel, Play takes the player into a copy of the game's zone, and as it
/// loads their character state switches the game's own processor on in their client. From there the game (an
/// <see cref="ArenaMatch"/>) runs on the server at 20 Hz against server-driven AI: each client only drives its
/// player's own vehicle or footballer. The results screen's Exit takes them back to where they were.
/// <para>
/// Several play one game together two ways: the player's group members are asked along when the start panel goes up
/// (as for a group battle), and the lobby's matchmaking queues match up players who queued (see the Queues part).
/// The game holds its start for everyone coming, then fills the empty places with AI.
/// </para>
/// </summary>
public sealed partial class VehicleGameManager : IVehicleGameManager, IDisposable
{
    public const int TickMs = 50;

    // "Your job has been switched to one that matches the minigame you are about to join."
    private const int JobSwitchedMessageId = 4594;

    private const int OfferTimeoutMs = 3 * 60 * 1000;
    private const int InvitationTimeoutSeconds = 60;
    private const int ResultsTimeoutMs = 2 * 60 * 1000;

    // The 3D games' encounter ids carry this over the stage id, clear of the battles' ids.
    private const int EncounterIdBase = 0x4000;

    // The start panel's BUY area, as live sent it for every one of these games.
    private static readonly int[] MarketplaceAds = [2194, 2919, 2920, 2921, 3752];

    private readonly ILogger _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IResourceManager _resourceManager;
    private readonly IRewardManager _rewardManager;
    private readonly IZoneManager _zoneManager;
    private readonly IDbContextFactory<DatabaseContext> _dbContextFactory;

    private readonly ConcurrentDictionary<int, ArenaMatch> _matches = new();
    private readonly ConcurrentDictionary<ulong, ArenaMatch> _playerMatches = new();

    // Group members asked along to a game, by the game.
    private readonly ConcurrentDictionary<ulong, ArenaMatch> _invitations = new();

    // Players who left a game's zone. They're told of from inside the zone manager's move, so they're dealt with on
    // the next tick rather than under the match's lock there.
    private readonly ConcurrentQueue<(ArenaMatch Match, ulong PlayerGuid)> _departures = new();

    private readonly Timer _timer;

    private int _instance;
    private long _lastUpdate = Environment.TickCount64;

    public VehicleGameManager(ILoggerFactory loggerFactory, IResourceManager resourceManager, IRewardManager rewardManager, IZoneManager zoneManager,
        IDbContextFactory<DatabaseContext> dbContextFactory)
    {
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<VehicleGameManager>();
        _resourceManager = resourceManager;
        _rewardManager = rewardManager;
        _zoneManager = zoneManager;
        _dbContextFactory = dbContextFactory;

        _timer = new Timer(_ => Update(), null, TickMs, TickMs);
    }

    public void Dispose() => _timer.Dispose();

    internal IResourceManager ResourceManager => _resourceManager;

    internal IDbContextFactory<DatabaseContext> DbContextFactory => _dbContextFactory;

    public IReadOnlyList<NpcInteractionOption> GetInteractionOptions(Player player, Npc npc) =>
        _resourceManager.VehicleGames.OfferedBy(npc.DefinitionId).Select(stage => new NpcInteractionOption
        {
            IconId = stage.IconId,
            ButtonTextId = stage.NameId,
            Invoke = interactingPlayer => Offer(interactingPlayer, stage)
        }).Concat(GetLobbyOptions(npc)).ToList();

    public IEnumerable<ActivityDefinition> GetActivities() =>
        _resourceManager.VehicleGames.Stages.Where(stage => stage.Image is not null).OrderBy(stage => stage.Kind).ThenBy(stage => stage.Id)
            .Select(stage => new ActivityDefinition
            {
                Id = VehicleGameCollection.ActivityIdBase + stage.Id,
                Type = BaseActivityServicePacket.MinigameActivityType,
                Category = stage.ActivityCategory,
                ImageSetId = stage.IconId,
                DisplayNameId = stage.NameId,
                DisplayDescriptionId = stage.DescriptionId,
                NameId = stage.NameId,
                DescriptionId = stage.DescriptionId,
                CanPlayerJoin = true,
                DetailImageFilename = $"{stage.Image}_detail.dds",
                ThumbnailImageFilename = $"{stage.Image}_thumb.dds",
                Difficulty = stage.Difficulty
            });

    public bool IsPlaying(Player player) =>
        _playerMatches.TryGetValue(player.Guid, out var match) && match.State != ArenaMatchState.Closed;

    #region Offer and entry

    public bool Offer(Player player, VehicleGameStageDefinition stage)
    {
        if (!CanPlay(player, tell: true))
            return false;

        if (!TryCreate(stage, out var match))
        {
            ChatHelper.SendSystemMessage(player, "That game failed to start. Try again in a moment.");
            return false;
        }

        lock (match)
        {
            AddParticipant(match, player);

            // Group members are asked along; each one who accepts gets the start panel too.
            if (stage.Kind != VehicleGameKind.Garage)
                Invite(match, player);
        }

        _logger.LogInformation("{player} was offered {name} ({stage}) as {encounter:X}.", player.Name.FullName, stage.Comment, stage.Id, match.Id);

        return true;
    }

    /// <summary>
    /// Free to start a game: not in one (or offered one), a minigame or a battle.
    /// </summary>
    private bool CanPlay(Player player, bool tell)
    {
        if (!IsPlaying(player) && MinigameManager.GetSession(player) is null && player.Zone is not WorldZone { Battle: not null })
            return true;

        if (tell)
            ChatHelper.SendSystemMessage(player, "Finish the game you're playing first.");

        return false;
    }

    /// <summary>
    /// Asks the offerer's group members along, as many as the game has places for: the group window's timer and a
    /// "Join [game]?" notification. Members busy with a game, or asked to another, are left out.
    /// </summary>
    private void Invite(ArenaMatch match, Player offerer)
    {
        foreach (var memberGuid in offerer.Group?.Members ?? [])
        {
            if (memberGuid == offerer.Guid || match.Participants.Count + match.Invited.Count >= match.Capacity
                || !_zoneManager.TryGetPlayer(memberGuid, out var member) || !CanPlay(member, tell: false)
                || !_invitations.TryAdd(memberGuid, match))
                continue;

            match.Invited[memberGuid] = Environment.TickCount64 + InvitationTimeoutSeconds * 1000L;

            member.SendTunneled(new EncounterInvitationPacket
            {
                EncounterId = match.Id,
                ReplyId = BattleRules.ServerId,
                Guid = offerer.Guid,
                NameId = match.Stage.NameId,
                Timeout = InvitationTimeoutSeconds
            });
        }
    }

    public bool AnswerInvitation(Player player, bool accept)
    {
        if (!_invitations.TryRemove(player.Guid, out var match))
            return false;

        lock (match)
        {
            if (!match.Invited.Remove(player.Guid))
                return true;

            // Everyone's group window ticks or crosses them, and the timers close.
            foreach (var participant in match.Participants.Values)
            {
                participant.Player.SendTunneled(new GroupPacketAnnounceEncounterReply { MemberGuid = player.Guid, Accepted = accept });
                participant.Player.SendTunneled(new EncounterInvitationPacket { EncounterId = match.Id, ReplyId = BattleRules.ServerId, NameId = match.Stage.NameId });
            }

            player.SendTunneled(new EncounterInvitationPacket { EncounterId = match.Id, ReplyId = BattleRules.ServerId, NameId = match.Stage.NameId });

            if (!accept)
                return true;

            if (match.Started || match.State is ArenaMatchState.Over or ArenaMatchState.Closed)
            {
                ChatHelper.SendSystemMessage(player, "That game has already started.");
                return true;
            }

            if (CanPlay(player, tell: true))
                AddParticipant(match, player);
        }

        return true;
    }

    /// <summary>
    /// A game for players a queue matched up: each gets the start panel, on the side given.
    /// </summary>
    private bool Launch(VehicleGameStageDefinition stage, IReadOnlyList<(Player Player, int Side)> players)
    {
        if (!TryCreate(stage, out var match))
            return false;

        lock (match)
        {
            foreach (var (player, side) in players)
            {
                if (CanPlay(player, tell: true))
                    AddParticipant(match, player, side);
            }

            if (match.Participants.Count == 0)
            {
                Close(match);
                return false;
            }
        }

        _logger.LogInformation("{players} were matched for {name} ({stage}) as {encounter:X}.",
            string.Join(", ", players.Select(player => player.Player.Name.FullName)), stage.Comment, stage.Id, match.Id);

        return true;
    }

    private bool TryCreate(VehicleGameStageDefinition stage, out ArenaMatch match)
    {
        match = null!;

        var instance = Interlocked.Increment(ref _instance);
        var id = BattleRules.EncounterId(instance, EncounterIdBase + stage.Id);

        if (!_zoneManager.TryGetOrCreateZoneInstance(stage.ZoneId, (ulong)(uint)id, out var zone) || zone is not WorldZone worldZone)
        {
            _logger.LogError("{name} ({stage}): its zone {zone} failed to start.", stage.Comment, stage.Id, stage.ZoneId);
            return false;
        }

        var logger = _loggerFactory.CreateLogger($"{stage.Kind} {id:X}");

        try
        {
            match = stage.Kind switch
            {
                VehicleGameKind.Derby => new DerbyMatch(id, stage, worldZone, this, logger),
                VehicleGameKind.Race => new RaceMatch(id, stage, worldZone, this, logger),
                VehicleGameKind.Soccer => new SoccerMatch(id, stage, worldZone, this, logger),
                _ => new GarageVisit(id, stage, worldZone, this, logger)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{name} ({stage}) failed to set up.", stage.Comment, stage.Id);
            _zoneManager.EvictIfEmpty(worldZone);
            return false;
        }

        match.OfferedAt = Environment.TickCount64;

        worldZone.Arena = match;
        _matches[id] = match;

        return true;
    }

    /// <summary>
    /// Puts the start panel up: the job switch, the server's states, the offer, then (the zone is already up) Play.
    /// </summary>
    private void AddParticipant(ArenaMatch match, Player player, int side = 0)
    {
        match.Participants[player.Guid] = new ArenaParticipant(player) { Side = side };
        _playerMatches[player.Guid] = match;

        // Off to a game: out of any queue.
        LeaveQueues(player);

        var stage = match.Stage;

        // Live moved the player to the game's job as it was offered.
        if (stage.ProfileId != 0 && player.ActiveProfileId != stage.ProfileId && player.Profiles.Any(profile => profile.Id == stage.ProfileId)
            && player.ActivateProfile(stage.ProfileId))
            player.SendTunneled(new EncounterParticipantMessagePacket { MessageId = JobSwitchedMessageId, Guid = player.Guid, Name = player.Name.FullName });

        foreach (var state in (ReadOnlySpan<int>)[2, 3, 4])
            player.SendTunneled(new EncounterStatePacket { EncounterId = match.Id, ReplyId = BattleRules.ServerId, State = state });

        player.SendTunneled(GetDetails(match, isUpdate: false));

        player.SendTunneled(new EncounterZoneIsReadyPacket { EncounterId = match.Id, ReplyId = BattleRules.ServerId });
        player.SendTunneled(new EncounterStatePacket { EncounterId = match.Id, ReplyId = BattleRules.ServerId, State = 5 });
    }

    public bool RequestEntrance(Player player, int encounterId)
    {
        if (!_matches.TryGetValue(encounterId, out var match))
            return false;

        lock (match)
        {
            if (!match.Participants.TryGetValue(player.Guid, out var participant) || participant.Entered
                || match.State is ArenaMatchState.Over or ArenaMatchState.Closed)
                return true;

            if (match.Started)
            {
                TurnAway(match, participant);
                return true;
            }

            participant.Entered = true;
            participant.ReturnZoneId = player.Zone.DefinitionId;
            participant.ReturnOwnerId = player.Zone.OwnerId;
            participant.ReturnPosition = player.Position;
            participant.ReturnRotation = player.Rotation;

            if (match.State == ArenaMatchState.Offered)
            {
                match.State = ArenaMatchState.Running;
                match.StartedAt = Environment.TickCount64;
            }

            if (!player.TeleportToZone(match.Zone, match.Stage.EntryPosition, QuaternionExtensions.FromHeading(match.Stage.EntryHeading)))
            {
                participant.Entered = false;
                ChatHelper.SendSystemMessage(player, "You couldn't get into the game.");
                _logger.LogError("{player} couldn't be moved into {encounter:X}.", player.Name.FullName, match.Id);
            }
        }

        return true;
    }

    public bool CancelPending(Player player)
    {
        if (!_playerMatches.TryGetValue(player.Guid, out var match))
            return false;

        lock (match)
        {
            if (!match.Participants.TryGetValue(player.Guid, out var participant) || participant.Entered)
                return true;

            match.Participants.Remove(player.Guid);
            _playerMatches.TryRemove(new KeyValuePair<ulong, ArenaMatch>(player.Guid, match));

            if (match.Participants.Count == 0)
                Close(match);
        }

        return true;
    }

    #endregion

    #region In the game

    public void OnClientIsReady(Player player, WorldZone zone)
    {
        if (zone.Arena is not { } match)
        {
            // Back from a game: the client's game is off with the zone that held it, and the server forgets it.
            if (player.GameCharacterState != 0)
            {
                player.SetGameCharacterState(0, notify: false);
                player.SendCharacterState();
            }

            return;
        }

        lock (match)
        {
            if (!match.Participants.TryGetValue(player.Guid, out var participant) || match.State == ArenaMatchState.Closed)
            {
                // Not one of its players (a referee teleporting in, say): back out they go.
                SendBack(player, null);
                return;
            }

            participant.InZone = true;
            participant.Left = false;

            // In after the game started without them (it waited as long as it could).
            if (match.Started && !match.HasPlace(player.Guid))
            {
                TurnAway(match, participant);
                return;
            }

            player.SendTunneled(GetDetails(match, isUpdate: true));
            player.SendTunneled(new EncounterPacketPlayerEnter { EncounterId = match.Id, ReplyId = BattleRules.ServerId });

            if (match.Stage.Kind != VehicleGameKind.Garage)
                player.SendTunneled(new MiniGameKnockOutPacket());

            player.SendTunneled(EncounterDataCommonPacket.Minigame);

            // The game's bit switches its processor on in the client, which registers and waits for the setup.
            if (match.CharacterStateBit != 0)
                player.SetGameCharacterState(match.CharacterStateBit);

            player.SendTunneled(new EncounterStatePacket { EncounterId = match.Id, ReplyId = BattleRules.ServerId, State = 6 });

            match.OnZoneLoaded(participant);
        }
    }

    public void OnArrived(Player player, WorldZone zone)
    {
        if (zone.Arena is not { } match)
            return;

        lock (match)
        {
            if (!match.Participants.TryGetValue(player.Guid, out var participant) || !participant.InZone)
                return;

            participant.Arrived = true;

            // Live sent it as the loading screen went: the minigame HUD comes up.
            player.SendTunneled(new EncounterPacketPlayerEnter { EncounterId = match.Id, ReplyId = BattleRules.ServerId, Guid = player.Guid });

            match.OnArrived(participant);
        }
    }

    public void HandlePacket(Player player, short opCode, ReadOnlySpan<byte> data)
    {
        if (!_playerMatches.TryGetValue(player.Guid, out var match))
        {
            // A game's start screen picks the vehicle without the garage.
            if (opCode == BaseVehiclePartPacket.OpCode && VehiclePartPacketRequestSelectVehicle.TryDeserialize(data, out var select))
                SelectVehicle(player, (int)select.VehicleType, (int)select.VehicleId);

            return;
        }

        lock (match)
        {
            if (!match.Participants.TryGetValue(player.Guid, out var participant) || !participant.InZone || participant.Left
                || match.State == ArenaMatchState.Closed || !ReferenceEquals(player.Zone, match.Zone))
                return;

            match.OnPacket(participant, opCode, data);
        }
    }

    private void Update()
    {
        var now = Environment.TickCount64;
        var deltaSeconds = Math.Clamp((now - Interlocked.Exchange(ref _lastUpdate, now)) / 1000f, 0f, 0.25f);

        try
        {
            UpdateQueues(now);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "The matchmaking queues failed to update.");
        }

        while (_departures.TryDequeue(out var departure))
        {
            lock (departure.Match)
                OnDeparted(departure.Match, departure.PlayerGuid);
        }

        foreach (var match in _matches.Values)
        {
            try
            {
                lock (match)
                    Update(match, now, deltaSeconds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{encounter:X} ({name}) failed to update.", match.Id, match.Stage.Comment);
            }
        }
    }

    private void Update(ArenaMatch match, long now, float deltaSeconds)
    {
        UpdateInvitations(match, now);

        switch (match.State)
        {
            case ArenaMatchState.Offered:
                if (now - match.OfferedAt > OfferTimeoutMs)
                    Close(match);
                return;

            case ArenaMatchState.Running:
                match.Update(now, deltaSeconds);

                if (match.Started)
                    TurnAwayLatecomers(match);
                else
                    TellWaiting(match);
                break;

            case ArenaMatchState.Over:
                match.Update(now, deltaSeconds);

                if (now - match.OverAt > ResultsTimeoutMs)
                {
                    foreach (var participant in match.Participants.Values.Where(participant => participant.Entered && !participant.Left).ToList())
                        Leave(match, participant);
                }
                break;
        }

        if (match.State is ArenaMatchState.Running or ArenaMatchState.Over
            && match.Participants.Values.All(participant => !participant.Entered || participant.Left))
            Close(match);
    }

    /// <summary>
    /// Invitations lapse after a minute, or as their player logs off.
    /// </summary>
    private void UpdateInvitations(ArenaMatch match, long now)
    {
        if (match.Invited.Count == 0)
            return;

        foreach (var (guid, expiresAt) in match.Invited.ToList())
        {
            if (now >= expiresAt || !_zoneManager.TryGetPlayer(guid, out _))
                Withdraw(match, guid);
        }
    }

    private void Withdraw(ArenaMatch match, ulong guid)
    {
        match.Invited.Remove(guid);
        _invitations.TryRemove(new KeyValuePair<ulong, ArenaMatch>(guid, match));

        if (_zoneManager.TryGetPlayer(guid, out var player))
            player.SendTunneled(new EncounterInvitationPacket { EncounterId = match.Id, ReplyId = BattleRules.ServerId, NameId = match.Stage.NameId });
    }

    /// <summary>
    /// The first in hear, once, who the game is holding its start for.
    /// </summary>
    private void TellWaiting(ArenaMatch match)
    {
        if (match.ToldWaiting || !match.Playing.Any())
            return;

        var waiting = match.Expected
            .Where(guid => !(match.Participants.TryGetValue(guid, out var participant) && participant.InZone))
            .Select(guid => _zoneManager.TryGetPlayer(guid, out var player) ? player.Name.FullName : null)
            .OfType<string>()
            .ToList();

        if (waiting.Count == 0)
            return;

        match.ToldWaiting = true;

        foreach (var participant in match.Playing)
            ChatHelper.SendSystemMessage(participant.Player, $"Waiting up to {ArenaMatch.JoinWaitMs / 1000} seconds for {string.Join(", ", waiting)}.");
    }

    /// <summary>
    /// The game has started with those who were there: the invitations still out are withdrawn, and anyone left on
    /// the start panel, or in the zone with no place, is told and goes.
    /// </summary>
    private void TurnAwayLatecomers(ArenaMatch match)
    {
        foreach (var guid in match.Invited.Keys.ToList())
            Withdraw(match, guid);

        foreach (var participant in match.Participants.Values.ToList())
        {
            if (!participant.Left && (!participant.Entered || participant.InZone) && !match.HasPlace(participant.Guid))
                TurnAway(match, participant);
        }
    }

    private void TurnAway(ArenaMatch match, ArenaParticipant participant)
    {
        var player = participant.Player;

        ChatHelper.SendSystemMessage(player, "That game started without you.");

        if (participant.Entered)
        {
            Leave(match, participant);
            return;
        }

        match.Participants.Remove(player.Guid);
        _playerMatches.TryRemove(new KeyValuePair<ulong, ArenaMatch>(player.Guid, match));

        player.SendTunneled(new EncounterPacketEncounterShutdown { EncounterId = match.Id, ReplyId = BattleRules.ServerId });
    }

    /// <summary>
    /// Pays every player still in the match by how they placed: the end screen's reward and the stars and coins.
    /// </summary>
    internal void Reward(ArenaMatch match)
    {
        var elapsedSeconds = (int)((Environment.TickCount64 - match.StartedAt) / 1000);

        foreach (var participant in match.Participants.Values)
        {
            if (!participant.InZone || participant.Left || participant.Rewarded || participant.Place < 0)
                continue;

            participant.Rewarded = true;

            var player = participant.Player;
            var (stars, coins) = ArenaRules.Payout(match.Stage, participant.Place, participant.Won);
            var boostedStars = player.ApplyExperienceBoost(stars);
            var boostedCoins = player.ApplyCoinBoost(coins);

            player.SendTunneled(new EncounterPacketEncounterComplete
            {
                Won = participant.Won,
                Stars = boostedStars,
                ElapsedSeconds = elapsedSeconds,
                Reward = new RewardBundleBase { Success = false, Coins = boostedCoins, Experience = boostedStars, Unknown3 = 0, IconId = -1, NameId = -1 }
            });

            if (boostedCoins > 0)
                _rewardManager.TryGrantCurrency(player, CurrencyType.Coins, boostedCoins);

            if (boostedStars > 0)
                _rewardManager.TryGrantExperience(player, boostedStars);

            player.Achievements.Record(AchievementEvent.Minigame, match.Stage.Id);

            if (participant.Won)
                player.Achievements.Record(AchievementEvent.MinigameWin, match.Stage.Id);

            player.Quests.Record(new MinigameFinishedEvent(match.Stage.Id, participant.Won));

            _logger.LogInformation("{player} finished {name} ({encounter:X}) in place {place}: {stars} stars, {coins} coins.",
                player.Name.FullName, match.Stage.Comment, match.Id, participant.Place + 1, boostedStars, boostedCoins);
        }
    }

    #endregion

    #region Vehicles

    /// <summary>
    /// The vehicle the player drives in a game of <paramref name="vehicleType"/> (1 kart racing, 2 demolition
    /// derby): their active loadout's.
    /// </summary>
    internal VehicleConfig PlayerVehicle(Player player, int vehicleType)
    {
        var slots = VehicleLoadoutStore.Active(player, vehicleType)?.Slots.Select(slot => (int)slot).ToList() ?? [];

        return VehicleLoadouts.Compose(_resourceManager.VehicleGames, _resourceManager.ClientItemDefinitions, vehicleType, slots);
    }

    /// <summary>
    /// The player picks the loadout they drive in games of a type (in the garage, or on a game's start screen).
    /// </summary>
    public bool SelectVehicle(Player player, int vehicleType, int loadoutId)
    {
        if (vehicleType is not (VehicleLoadouts.KartRace or VehicleLoadouts.DemolitionDerby)
            || !player.VehicleStruct.Loadouts.Any(loadout => loadout.Id == loadoutId && loadout.Unknown == vehicleType))
            return false;

        VehicleLoadoutStore.SetActive(player, vehicleType, loadoutId, _dbContextFactory);

        return true;
    }

    #endregion

    #region Leaving

    public bool RequestExit(Player player)
    {
        if (!_playerMatches.TryGetValue(player.Guid, out var match))
        {
            // Stuck in a game's zone with no game: out they go.
            if (player.Zone is WorldZone { Arena: not null })
            {
                SendBack(player, null);
                return true;
            }

            return false;
        }

        lock (match)
        {
            if (!match.Participants.TryGetValue(player.Guid, out var participant))
                return true;

            if (!participant.Entered)
            {
                CancelPending(player);
                return true;
            }

            Leave(match, participant);
        }

        return true;
    }

    private void Leave(ArenaMatch match, ArenaParticipant participant)
    {
        var player = participant.Player;

        participant.Left = true;
        match.OnLeft(participant);

        match.Participants.Remove(player.Guid);
        _playerMatches.TryRemove(new KeyValuePair<ulong, ArenaMatch>(player.Guid, match));

        player.SendTunneled(new EncounterParticipantTerminateEncounterPacket { EncounterId = match.Id, ReplyId = BattleRules.ServerId });

        if (ReferenceEquals(player.Zone, match.Zone))
            SendBack(player, participant);
    }

    /// <summary>
    /// Back to where they went in from (or the world's start).
    /// </summary>
    private void SendBack(Player player, ArenaParticipant? participant)
    {
        // The client turns the game off as the zone unloads; the server forgets it now.
        player.SetGameCharacterState(0, notify: false);

        if (participant is not null && participant.ReturnZoneId != 0
            && player.TeleportToZone(participant.ReturnZoneId, participant.ReturnOwnerId, participant.ReturnPosition, participant.ReturnRotation))
            return;

        var startingZone = _zoneManager.StartingZone;
        var position = player.StartingZonePosition == Vector4.Zero ? startingZone.SpawnPosition : player.StartingZonePosition;
        var rotation = player.StartingZonePosition == Vector4.Zero ? startingZone.SpawnRotation : player.StartingZoneRotation;

        player.TeleportToZone(startingZone.DefinitionId, null, position, rotation);
    }

    public void OnPlayerLeft(Player player, WorldZone zone)
    {
        if (zone.Arena is { } match)
            _departures.Enqueue((match, player.Guid));
    }

    /// <summary>
    /// A player who logged off or teleported away mid-game is out of it. Those who left through the results screen
    /// are already gone from it.
    /// </summary>
    private void OnDeparted(ArenaMatch match, ulong playerGuid)
    {
        if (!match.Participants.TryGetValue(playerGuid, out var participant) || !participant.InZone
            || match.Zone.TryGetPlayer(playerGuid, out _))
            return;

        participant.Left = true;
        match.OnLeft(participant);

        match.Participants.Remove(playerGuid);
        _playerMatches.TryRemove(new KeyValuePair<ulong, ArenaMatch>(playerGuid, match));

        participant.Player.SetGameCharacterState(0, notify: false);
    }

    /// <summary>
    /// Ends it for good: anyone still on the start panel has it taken down, and the zone goes.
    /// </summary>
    private void Close(ArenaMatch match)
    {
        if (match.State == ArenaMatchState.Closed)
            return;

        match.State = ArenaMatchState.Closed;
        _matches.TryRemove(new KeyValuePair<int, ArenaMatch>(match.Id, match));

        foreach (var participant in match.Participants.Values.ToList())
        {
            _playerMatches.TryRemove(new KeyValuePair<ulong, ArenaMatch>(participant.Player.Guid, match));

            if (!participant.Entered)
                participant.Player.SendTunneled(new EncounterPacketEncounterShutdown { EncounterId = match.Id, ReplyId = BattleRules.ServerId });
            else if (!participant.Left && ReferenceEquals(participant.Player.Zone, match.Zone))
                Leave(match, participant);
        }

        match.Participants.Clear();

        foreach (var guid in match.Invited.Keys.ToList())
            Withdraw(match, guid);

        try
        {
            match.OnClosed();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{encounter:X} ({name}) failed to clean up.", match.Id, match.Stage.Comment);
        }

        match.Zone.Arena = null;

        _zoneManager.EvictIfEmpty(match.Zone);
    }

    #endregion

    #region Packets

    /// <summary>
    /// The start panel's offer (and, once in, its update), laid out as live sent them for these games.
    /// </summary>
    internal static EncounterDetailsResponsePacket GetDetails(ArenaMatch match, bool isUpdate)
    {
        var stage = match.Stage;

        var participants = match.Participants.Values.Select(participant => new EncounterParticipant
        {
            Key = participant.Player.Guid.GetHashCode(),
            Guid = participant.Player.Guid,
            Entered = isUpdate,
            Position = stage.EntryPosition,
            Name = participant.Player.Name.FullName,
            ServerId = BattleRules.ServerId
        }).ToList();

        return BuildDetails(match.Id, stage, participants, isUpdate);
    }

    /// <summary>
    /// The offer or its update for <paramref name="participants"/>, as live sent it.
    /// </summary>
    public static EncounterDetailsResponsePacket BuildDetails(int encounterId, VehicleGameStageDefinition stage, List<EncounterParticipant> participants, bool isUpdate)
    {
        var objectives = new List<QuestObjectiveInfo>();

        if (stage.ObjectiveId != 0)
        {
            objectives.Add(new QuestObjectiveInfo
            {
                ObjectiveId = stage.ObjectiveId,
                NameId = stage.ObjectiveNameId,
                DescriptionId = stage.ObjectiveDescriptionId,
                RewardBundle = EmptyBundle(),
                State = isUpdate ? 1 : 0
            });
        }

        return new EncounterDetailsResponsePacket
        {
            EncounterId = encounterId,
            ReplyId = BattleRules.ServerId,
            IsUpdate = isUpdate,
            MarketplaceId = isUpdate ? 0 : encounterId & 0xFFFF,
            MarketplaceAds = isUpdate ? [] : [.. MarketplaceAds],
            Details = new EncounterDetails
            {
                Id = encounterId,
                Unknown = isUpdate ? 4 : 2,
                Teams = [participants],
                Kind = (int)stage.Kind,
                Unknown3 = false,
                TimeLimitSeconds = 2000,
                RespawnMilliseconds = 10000,
                Info = new MiniGameInfoPacket
                {
                    NameId = stage.NameId,
                    IconId = stage.IconId,
                    DescriptionId = stage.DescriptionId,
                    Difficulty = stage.Difficulty,
                    ProfileType = stage.ProfileType,
                    Type = stage.MiniGameType,
                    Reward = EmptyBundle(),
                    MemberReward = EmptyBundle(),
                    RewardPreview = EmptyBundle(),
                    Objectives = objectives,
                    ShowStarCounter = true,
                    ShowStatusIcon = true,
                    ShowActionBar = true,
                    ShowStartScreen = true,
                    ShowEndScreen = true,
                    Unknown = 1,
                    Unknown2 = true,
                    MiniGameId = stage.Id,
                    // Live: the whole id in the offer, its low 16 bits in the update.
                    Unknown6 = isUpdate ? encounterId & 0xFFFF : encounterId
                }
            }
        };
    }

    private static RewardBundleBase EmptyBundle() => new() { Success = false, Unknown3 = 0, IconId = -1, NameId = -1 };

    #endregion
}
