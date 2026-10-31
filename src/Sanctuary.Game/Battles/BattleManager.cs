using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;

using Microsoft.Extensions.Logging;

using Sanctuary.Core.Extensions;
using Sanctuary.Core.IO;
using Sanctuary.Game.Achievements;
using Sanctuary.Game.Entities;
using Sanctuary.Game.Groups;
using Sanctuary.Game.Helpers;
using Sanctuary.Game.Interactions;
using Sanctuary.Game.Minigames;
using Sanctuary.Game.Quests;
using Sanctuary.Game.Resources;
using Sanctuary.Game.Resources.Definitions;
using Sanctuary.Game.Resources.Definitions.Rewards;
using Sanctuary.Game.Zones;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game.Battles;

/// <summary>
/// Runs the battles, as the live game did: a start panel offers the battle, Play takes the player (and whoever of
/// their group came along) into a private copy of its zone, enemies come in waves (the boss's health bar up top
/// with the last), and clearing them puts an Exit in the zone. Going through it shows the end screen and pays; closing
/// that takes everyone back to where they were. Knocked out players come back at the entry; too many knock outs, or
/// running out of time, loses the battle.
/// </summary>
public sealed class BattleManager : IBattleManager, IDisposable
{
    private const int KnockoutObjectiveId = 1;
    private const int DefeatObjectiveId = 2;
    private const int BossObjectiveId = 3;

    // "Don't get knocked out 5 times!" / "Don't get knocked out too many times!"
    private const int KnockoutObjectiveNameId = 2286;
    private const int KnockoutObjectiveDescriptionId = 100712;

    // "Your job has been switched because you are entering a combat area."
    private const int JobSwitchedMessageId = 634;

    // The live battles' Exit (sg_exit_door_01).
    private const int ExitNpcId = 34061;

    // The Games window's Battles category, and the live tiles' icon set.
    private const int ActivityCategory = 10;
    private const int ActivityImageSetId = 1345;

    // The start panel's "Star" reward line.
    private const int StarIconId = 3901;
    private const int StarNameId = 2416;

    private const int InvitationTimeoutSeconds = 60;

    private const int UpdateIntervalMs = 250;
    private const int OfferTimeoutMs = 3 * 60 * 1000;
    private const int EndScreenTimeoutMs = 2 * 60 * 1000;

    // Points for a goal done, on the end screen.
    private const int ObjectivePoints = 5000;

    private readonly ILogger _logger;
    private readonly IResourceManager _resourceManager;
    private readonly IRewardManager _rewardManager;
    private readonly IZoneManager _zoneManager;

    private readonly ConcurrentDictionary<int, Battle> _battles = new();
    private readonly ConcurrentDictionary<ulong, Battle> _playerBattles = new();

    // Players who left a battle's zone. They're told of from inside the zone manager's move, so they're dealt with on
    // the next update rather than under the battle's lock there.
    private readonly ConcurrentQueue<(Battle Battle, ulong PlayerGuid)> _departures = new();
    private readonly Timer _timer;

    private int _instance;

    public BattleManager(ILogger<BattleManager> logger, IResourceManager resourceManager, IRewardManager rewardManager, IZoneManager zoneManager)
    {
        _logger = logger;
        _resourceManager = resourceManager;
        _rewardManager = rewardManager;
        _zoneManager = zoneManager;

        _timer = new Timer(_ => Update(), null, UpdateIntervalMs, UpdateIntervalMs);
    }

    public void Dispose() => _timer.Dispose();

    public bool IsStarter(int npcDefinitionId) => _resourceManager.Battles.IsStarter(npcDefinitionId);

    public IReadOnlyList<NpcInteractionOption> GetInteractionOptions(Player player, Npc npc) =>
        _resourceManager.Battles.OfferedBy(npc.DefinitionId).Select(battle => new NpcInteractionOption
        {
            IconId = ContextIcons.QuestOffer,
            ButtonTextId = battle.NameId,
            Invoke = interactingPlayer => Offer(interactingPlayer, battle)
        }).ToList();

    public IEnumerable<ActivityDefinition> GetActivities() =>
        _resourceManager.Battles.Values.Where(battle => battle.Image is not null).OrderBy(battle => battle.Difficulty).ThenBy(battle => battle.Id)
            .Select(battle => new ActivityDefinition
            {
                Id = BattleDefinitionCollection.ActivityIdBase + battle.Id,
                Type = BaseActivityServicePacket.MinigameActivityType,
                Category = ActivityCategory,
                ImageSetId = ActivityImageSetId,
                DisplayNameId = battle.NameId,
                DisplayDescriptionId = battle.DescriptionId,
                NameId = battle.NameId,
                DescriptionId = battle.DescriptionId,
                CanPlayerJoin = true,
                DetailImageFilename = $"{battle.Image}_detail.dds",
                ThumbnailImageFilename = $"{battle.Image}_thumb.dds",
                Difficulty = battle.Difficulty
            });

    #region Offer and entry

    public bool Offer(Player player, BattleDefinition definition)
    {
        if (_playerBattles.TryGetValue(player.Guid, out var current) && current.State != BattleState.Closed)
        {
            ChatHelper.SendSystemMessage(player, "Finish the battle you're in first.");
            return false;
        }

        if (MinigameManager.GetSession(player) is not null)
        {
            ChatHelper.SendSystemMessage(player, "Finish the game you're playing first.");
            return false;
        }

        if (!player.CanFight)
        {
            ChatHelper.SendSystemMessage(player, "You need a combat job to fight here.");
            return false;
        }

        if (!TryCreate(definition, out var battle))
        {
            ChatHelper.SendSystemMessage(player, "That battle failed to start. Try again in a moment.");
            return false;
        }

        lock (battle)
        {
            AddParticipant(battle, player);

            // Group members are asked along; each one who accepts gets the start panel too.
            foreach (var memberGuid in player.Group?.Members ?? [])
            {
                if (memberGuid == player.Guid || battle.Invited.Count + 1 >= definition.MaxPlayers || !_zoneManager.TryGetPlayer(memberGuid, out var member))
                    continue;

                if (_playerBattles.TryGetValue(memberGuid, out var theirs) && theirs.State != BattleState.Closed)
                    continue;

                battle.Invited.Add(memberGuid);

                member.SendTunneled(new EncounterInvitationPacket
                {
                    EncounterId = battle.Id,
                    ReplyId = BattleRules.ServerId,
                    Guid = player.Guid,
                    NameId = definition.NameId,
                    Timeout = InvitationTimeoutSeconds
                });
            }
        }

        _logger.LogInformation("{player} was offered battle {id} ({name}) as {encounter:X}.", player.Name.FullName, definition.Id, definition.Comment, battle.Id);

        return true;
    }

    private bool TryCreate(BattleDefinition definition, out Battle battle)
    {
        battle = null!;

        var instance = Interlocked.Increment(ref _instance);
        var id = BattleRules.EncounterId(instance, definition.Id);

        if (!_zoneManager.TryGetOrCreateZoneInstance(definition.ZoneId, (ulong)(uint)id, out var zone) || zone is not WorldZone worldZone)
        {
            _logger.LogError("Battle {id} ({name}): its zone {zone} failed to start.", definition.Id, definition.Comment, definition.ZoneId);
            return false;
        }

        battle = new Battle(id, definition, worldZone)
        {
            OfferedAt = Environment.TickCount64,
            Total = BattleRules.EnemyCount(definition)
        };

        worldZone.Battle = battle;
        _battles[id] = battle;

        return true;
    }

    /// <summary>
    /// Puts the start panel up: the server's states, the offer, then (the zone is already up) Play.
    /// </summary>
    private void AddParticipant(Battle battle, Player player)
    {
        battle.Participants[player.Guid] = new BattleParticipant(player);
        _playerBattles[player.Guid] = battle;

        // Live switched the player to a combat job as the battle was offered.
        if (player.TrySwitchToCombatJob())
            player.SendTunneled(new EncounterParticipantMessagePacket { MessageId = JobSwitchedMessageId, Guid = player.Guid, Name = player.Name.FullName });

        foreach (var state in (ReadOnlySpan<int>)[2, 3, 4])
            player.SendTunneled(new EncounterStatePacket { EncounterId = battle.Id, ReplyId = BattleRules.ServerId, State = state });

        player.SendTunneled(GetDetails(battle, isUpdate: false));

        player.SendTunneled(new EncounterZoneIsReadyPacket { EncounterId = battle.Id, ReplyId = BattleRules.ServerId });
        player.SendTunneled(new EncounterStatePacket { EncounterId = battle.Id, ReplyId = BattleRules.ServerId, State = 5 });
    }

    public void AnswerInvitation(Player player, bool accept)
    {
        var battle = _battles.Values.FirstOrDefault(battle => battle.Invited.Contains(player.Guid));

        if (battle is null)
            return;

        lock (battle)
        {
            if (!battle.Invited.Remove(player.Guid))
                return;

            foreach (var participant in battle.Participants.Values)
            {
                participant.Player.SendTunneled(new GroupPacketAnnounceEncounterReply { MemberGuid = player.Guid, Accepted = accept });
                participant.Player.SendTunneled(new EncounterInvitationPacket { EncounterId = battle.Id, ReplyId = BattleRules.ServerId, NameId = battle.Definition.NameId });
            }

            player.SendTunneled(new EncounterInvitationPacket { EncounterId = battle.Id, ReplyId = BattleRules.ServerId, NameId = battle.Definition.NameId });

            if (!accept || battle.State is BattleState.Over or BattleState.Closed)
                return;

            if (_playerBattles.TryGetValue(player.Guid, out var theirs) && theirs.State != BattleState.Closed)
                return;

            if (!player.CanFight)
            {
                ChatHelper.SendSystemMessage(player, "You need a combat job to fight here.");
                return;
            }

            AddParticipant(battle, player);
        }
    }

    public void RequestEntrance(Player player, int encounterId)
    {
        if (!_battles.TryGetValue(encounterId, out var battle))
            return;

        lock (battle)
        {
            if (!battle.Participants.TryGetValue(player.Guid, out var participant) || participant.Entered
                || battle.State is BattleState.Over or BattleState.Closed)
                return;

            participant.Entered = true;
            participant.ReturnZoneId = player.Zone.DefinitionId;
            participant.ReturnOwnerId = player.Zone.OwnerId;
            participant.ReturnPosition = player.Position;
            participant.ReturnRotation = player.Rotation;

            player.SendTunneled(new EncounterStatePacket { EncounterId = battle.Id, ReplyId = BattleRules.ServerId, State = 6 });

            if (!player.TeleportToZone(battle.Zone, battle.EntryPosition, QuaternionExtensions.FromHeading(battle.Definition.EntryHeading)))
            {
                participant.Entered = false;
                ChatHelper.SendSystemMessage(player, "You couldn't get into the battle.");
                _logger.LogError("{player} couldn't be moved into battle {encounter:X}.", player.Name.FullName, battle.Id);
            }
        }
    }

    public void CancelPending(Player player)
    {
        if (!_playerBattles.TryGetValue(player.Guid, out var battle))
            return;

        lock (battle)
        {
            if (!battle.Participants.TryGetValue(player.Guid, out var participant) || participant.Entered)
                return;

            battle.Participants.Remove(player.Guid);
            _playerBattles.TryRemove(new KeyValuePair<ulong, Battle>(player.Guid, battle));

            if (battle.Participants.Count == 0)
                Close(battle);
        }
    }

    #endregion

    #region In the battle

    public void OnArrived(Player player, WorldZone zone)
    {
        if (zone.Battle is not { } battle)
        {
            // Out of any battle the client's combat rules are the overworld's again.
            player.SendTunneled(EncounterDataCommonPacket.Default);
            return;
        }

        lock (battle)
        {
            if (!battle.Participants.TryGetValue(player.Guid, out var participant) || battle.State == BattleState.Closed)
            {
                // Not one of its players (a referee teleporting in, say): back out they go.
                SendBack(player, null);
                return;
            }

            participant.Arrived = true;
            participant.Left = false;

            player.SendTunneled(EncounterDataCommonPacket.Battle);

            player.SendTunneled(new ObjectiveActivatePacket { ObjectiveId = KnockoutObjectiveId, Total = battle.Definition.MaxKnockouts });
            player.SendTunneled(new ObjectiveActivatePacket { ObjectiveId = DefeatObjectiveId, Total = battle.Total });

            if (HasBossObjective(battle))
                player.SendTunneled(new ObjectiveActivatePacket { ObjectiveId = BossObjectiveId, Total = 1 });

            player.SendTunneled(new TaskAddPacket { TaskId = DefeatObjectiveId, TextId = ObjectiveNameId(battle), Total = battle.Total });

            if (HasBossObjective(battle))
                player.SendTunneled(new TaskAddPacket { TaskId = BossObjectiveId, TextId = battle.Definition.BossObjectiveNameId, Total = 1 });

            player.SendTunneled(GetDetails(battle, isUpdate: true));

            player.SendTunneled(new EncounterPacketPlayerEnter { EncounterId = battle.Id, ReplyId = BattleRules.ServerId });
            player.SendTunneled(new EncounterPacketPlayerEnter { EncounterId = battle.Id, ReplyId = BattleRules.ServerId, Guid = player.Guid });

            // Someone joining a fight already going sees how far it's got.
            if (battle.Defeated > 0)
            {
                player.SendTunneled(new ObjectiveUpdatePacket { ObjectiveId = DefeatObjectiveId, Count = battle.Defeated });
                player.SendTunneled(new TaskUpdatePacket { TaskId = DefeatObjectiveId, Count = battle.Defeated });
            }

            if (battle.Knockouts > 0)
                player.SendTunneled(new ObjectiveUpdatePacket { ObjectiveId = KnockoutObjectiveId, Count = battle.Knockouts });

            if (battle.State == BattleState.Offered)
                Start(battle);
        }
    }

    private void Start(Battle battle)
    {
        var now = Environment.TickCount64;

        battle.State = BattleState.Running;
        battle.StartedAt = now;
        battle.EndsAt = now + battle.Definition.TimeLimitSeconds * 1000L;

        // Posted guards are there from the start; the first wave of the rest comes a moment later.
        for (var i = 0; i < battle.Definition.Waves.Count; i++)
        {
            if (battle.Definition.Waves[i].AtStart)
                SpawnWave(battle, i);
        }

        battle.NextWave = NextSequentialWave(battle, 0);
        battle.NextWaveAt = battle.NextWave < battle.Definition.Waves.Count ? now + battle.Definition.Waves[battle.NextWave].DelayMs : long.MaxValue;

        _logger.LogInformation("Battle {encounter:X} ({name}) started with {players} player(s), {enemies} enemies.",
            battle.Id, battle.Definition.Comment, battle.Participants.Count, battle.Total);
    }

    private static int NextSequentialWave(Battle battle, int from)
    {
        while (from < battle.Definition.Waves.Count && battle.Definition.Waves[from].AtStart)
            from++;

        return from;
    }

    private void SpawnWave(Battle battle, int index)
    {
        foreach (var spawn in battle.Definition.Waves[index].Spawns)
        {
            if (!_resourceManager.Npcs.TryGetValue(spawn.NpcId, out var npcDefinition))
            {
                _logger.LogError("Battle {id} ({name}): no NPC {npc}; that enemy is skipped.", battle.Definition.Id, battle.Definition.Comment, spawn.NpcId);
                battle.Total--;
                continue;
            }

            var position = new Vector4(spawn.Position[0], spawn.Position[1], spawn.Position[2], 1f);

            if (!battle.Zone.TrySpawnBattleEnemy(npcDefinition, spawn.Mob, position, spawn.Heading, out var enemy))
            {
                battle.Total--;
                continue;
            }

            battle.Alive.Add(enemy);

            if (spawn.Boss)
                battle.Boss = enemy;
        }
    }

    private void Update()
    {
        var now = Environment.TickCount64;

        while (_departures.TryDequeue(out var departure))
        {
            lock (departure.Battle)
                OnDeparted(departure.Battle, departure.PlayerGuid);
        }

        foreach (var battle in _battles.Values)
        {
            try
            {
                lock (battle)
                    Update(battle, now);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Battle {encounter:X} ({name}) failed to update.", battle.Id, battle.Definition.Comment);
            }
        }
    }

    private void Update(Battle battle, long now)
    {
        switch (battle.State)
        {
            case BattleState.Offered:
                if (now - battle.OfferedAt > OfferTimeoutMs)
                    Close(battle);
                return;

            case BattleState.Running:
                UpdateEnemies(battle);

                if (battle.State != BattleState.Running)
                    return;

                if (now >= battle.EndsAt)
                {
                    End(battle, won: false, tookTooLong: true);
                    return;
                }

                if (battle.NextWave < battle.Definition.Waves.Count && battle.Alive.Count == 0 && now >= battle.NextWaveAt)
                {
                    SpawnWave(battle, battle.NextWave);

                    battle.NextWave = NextSequentialWave(battle, battle.NextWave + 1);
                    battle.NextWaveAt = long.MaxValue;
                }
                else if (battle.NextWave < battle.Definition.Waves.Count && battle.Alive.Count == 0 && battle.NextWaveAt == long.MaxValue)
                {
                    battle.NextWaveAt = now + battle.Definition.Waves[battle.NextWave].DelayMs;
                }
                else if (battle.NextWave >= battle.Definition.Waves.Count && battle.Alive.Count == 0)
                {
                    Clear(battle);
                }

                break;

            case BattleState.Cleared:
                // Out of time with the Exit up still counts as a win.
                if (now >= battle.EndsAt)
                    End(battle, won: true, tookTooLong: false);
                break;

            case BattleState.Over:
                if (now - battle.OverAt > EndScreenTimeoutMs)
                {
                    foreach (var participant in battle.Participants.Values.Where(participant => participant.Entered && !participant.Left).ToList())
                        Leave(battle, participant);
                }
                break;
        }

        if (battle.State is BattleState.Running or BattleState.Cleared or BattleState.Over
            && battle.Participants.Values.All(participant => !participant.Entered || participant.Left))
            Close(battle);
    }

    private void UpdateEnemies(Battle battle)
    {
        for (var i = battle.Alive.Count - 1; i >= 0; i--)
        {
            var enemy = battle.Alive[i];

            if (enemy.Combat is { IsDead: false })
                continue;

            battle.Alive.RemoveAt(i);
            battle.Defeated++;
            battle.LastKillPosition = enemy.Position;

            SendToPlayers(battle, new ObjectiveUpdatePacket { ObjectiveId = DefeatObjectiveId, Count = battle.Defeated });
            SendToPlayers(battle, new TaskUpdatePacket { TaskId = DefeatObjectiveId, Count = battle.Defeated });

            if (enemy != battle.Boss)
                continue;

            battle.BossDefeated = true;

            SendToPlayers(battle, new CombatPacketEnableBossDisplay { BossGuid = enemy.Guid, Enable = false });

            if (HasBossObjective(battle))
            {
                SendToPlayers(battle, new ObjectiveCompletePacket { ObjectiveId = BossObjectiveId, Points = ObjectivePoints });
                SendToPlayers(battle, new TaskCompletePacket { TaskId = BossObjectiveId });
            }
        }

        // The boss's bar goes up for each player once the boss is in their view.
        if (battle.Boss is { } boss && !battle.BossDefeated)
        {
            foreach (var participant in battle.Participants.Values)
            {
                if (participant.BossBarShown || !participant.Arrived || participant.Left || !boss.VisiblePlayers.ContainsKey(participant.Player.Guid))
                    continue;

                participant.BossBarShown = true;
                participant.Player.SendTunneled(new CombatPacketEnableBossDisplay { BossGuid = boss.Guid, Enable = true });
            }
        }
    }

    /// <summary>
    /// The last enemy fell: the goal is done and the Exit appears where it fell.
    /// </summary>
    private void Clear(Battle battle)
    {
        battle.State = BattleState.Cleared;

        SendToPlayers(battle, new ObjectiveCompletePacket { ObjectiveId = DefeatObjectiveId, Points = ObjectivePoints });
        SendToPlayers(battle, new TaskCompletePacket { TaskId = DefeatObjectiveId });

        var exitAt = battle.Definition.Exit is { } exit ? new Vector4(exit[0], exit[1], exit[2], 1f) : battle.LastKillPosition;

        if (_resourceManager.Npcs.TryGetValue(ExitNpcId, out var exitDefinition)
            && battle.Zone.TrySpawnBattleProp(exitDefinition, exitAt, battle.Definition.EntryHeading, out var door))
        {
            battle.Exit = door;
            door.InteractAction = player => OnExitClicked(battle, player);
        }
        else
        {
            // Without a door the battle just ends.
            End(battle, won: true, tookTooLong: false);
        }
    }

    private void OnExitClicked(Battle battle, Player player)
    {
        lock (battle)
        {
            if (battle.State != BattleState.Cleared || !battle.Participants.ContainsKey(player.Guid))
                return;

            End(battle, won: true, tookTooLong: false);
        }
    }

    /// <summary>
    /// The end screen for everyone in the battle, and their rewards.
    /// </summary>
    private void End(Battle battle, bool won, bool tookTooLong)
    {
        if (battle.State is BattleState.Over or BattleState.Closed)
            return;

        battle.State = BattleState.Over;
        battle.OverAt = Environment.TickCount64;
        battle.Won = won;

        // A lost battle's enemies go, so nobody's hit while the end screen is up.
        if (!won)
        {
            foreach (var enemy in battle.Alive)
                enemy.Dispose();

            battle.Alive.Clear();

            if (battle.Boss is { } boss && !battle.BossDefeated)
                SendToPlayers(battle, new CombatPacketEnableBossDisplay { BossGuid = boss.Guid, Enable = false });

            SendToPlayers(battle, new ObjectiveFailPacket { ObjectiveId = DefeatObjectiveId });
            SendToPlayers(battle, new TaskFailPacket { TaskId = DefeatObjectiveId });
        }

        var elapsedSeconds = (int)((Environment.TickCount64 - battle.StartedAt) / 1000);
        var (stars, coins) = BattleRules.Payout(battle.Definition, won, battle.Defeated, battle.Total);
        var fullGroup = battle.Participants.Values.Count(participant => participant.Entered && !participant.Left) >= PlayerGroup.MaxMembers;

        foreach (var participant in battle.Participants.Values)
        {
            if (!participant.Entered || participant.Left)
                continue;

            var player = participant.Player;
            var boostedStars = player.ApplyExperienceBoost(stars);
            var boostedCoins = player.ApplyCoinBoost(coins);

            player.SendTunneled(new EncounterPacketEncounterComplete
            {
                Won = won,
                Stars = boostedStars,
                ElapsedSeconds = elapsedSeconds,
                Reward = new RewardBundleBase { Success = false, Coins = boostedCoins, Experience = boostedStars, Unknown3 = 0, IconId = -1, NameId = -1 }
            });

            if (boostedCoins > 0)
                _rewardManager.TryGrantCurrency(player, CurrencyType.Coins, boostedCoins);

            if (boostedStars > 0)
                _rewardManager.TryGrantExperience(player, boostedStars);

            if (won && !string.IsNullOrWhiteSpace(battle.Definition.RewardTable)
                && _rewardManager.TryRollReward(battle.Definition.RewardTable, out var drop) && drop is not null)
                _rewardManager.TryGrantReward(player, drop, battle.Exit?.Guid ?? 0);

            if (won)
            {
                player.Achievements.Record(AchievementEvent.Battle, battle.Definition.Id);

                if (fullGroup)
                    player.Achievements.Record(AchievementEvent.BattleFullGroup, battle.Definition.Id);

                player.Quests.Record(new BattleWonEvent(battle.Definition.Id));
            }

            if (tookTooLong)
                ChatHelper.SendSystemMessage(player, "You ran out of time.");
        }

        _logger.LogInformation("Battle {encounter:X} ({name}) {result} after {seconds}s: {defeated}/{total} defeated, {knockouts} knock out(s), {stars} stars, {coins} coins each.",
            battle.Id, battle.Definition.Comment, won ? "won" : "lost", elapsedSeconds, battle.Defeated, battle.Total, battle.Knockouts, stars, coins);
    }

    public void OnKnockedOut(Player player)
    {
        if (player.Zone is not WorldZone { Battle: { } battle })
        {
            // Out in the world: respawn at the zone's start, or pay to get up where they lie.
            player.SendTunneled(new EncounterShowRespawnWindowPacket
            {
                Milliseconds = CombatRules.PlayerRespawnSeconds * 1000,
                ReviveCost = BattleRules.OverworldReviveCost
            });
            return;
        }

        lock (battle)
        {
            if (battle.State != BattleState.Running || !battle.Participants.ContainsKey(player.Guid))
                return;

            battle.Knockouts++;

            SendToPlayers(battle, new ObjectiveUpdatePacket { ObjectiveId = KnockoutObjectiveId, Count = battle.Knockouts });

            player.SendTunneled(new EncounterShowRespawnWindowPacket { Milliseconds = CombatRules.PlayerRespawnSeconds * 1000 });

            if (battle.Knockouts >= battle.Definition.MaxKnockouts)
            {
                SendToPlayers(battle, new ObjectiveFailPacket { ObjectiveId = KnockoutObjectiveId });
                End(battle, won: false, tookTooLong: false);
            }
        }
    }

    public void Resume(Player player, bool paid)
    {
        if (!player.IsKnockedOut)
            return;

        var inBattle = player.Zone is WorldZone { Battle: not null };

        if (paid && !inBattle && _rewardManager.TryTakeCoins(player, BattleRules.OverworldReviveCost))
        {
            player.Revive(player.MaxHitpoints);
            return;
        }

        if (paid && !inBattle)
            ChatHelper.SendSystemMessage(player, $"You need {BattleRules.OverworldReviveCost} coins to revive here.");

        player.RecoverNow();
    }

    #endregion

    #region Leaving

    public void RequestExit(Player player)
    {
        if (!_playerBattles.TryGetValue(player.Guid, out var battle))
        {
            // Stuck in a battle's zone with no battle: out they go.
            if (player.Zone is WorldZone { Battle: not null })
                SendBack(player, null);
            return;
        }

        lock (battle)
        {
            if (!battle.Participants.TryGetValue(player.Guid, out var participant))
                return;

            if (!participant.Entered)
            {
                CancelPending(player);
                return;
            }

            // Quitting a fight is losing it, for them.
            if (battle.State is BattleState.Running or BattleState.Cleared && participant.Arrived)
                player.SendTunneled(new EncounterPacketEncounterComplete { Won = false, ElapsedSeconds = (int)((Environment.TickCount64 - battle.StartedAt) / 1000) });

            Leave(battle, participant);
        }
    }

    private void Leave(Battle battle, BattleParticipant participant)
    {
        var player = participant.Player;

        participant.Left = true;
        battle.Participants.Remove(player.Guid);
        _playerBattles.TryRemove(new KeyValuePair<ulong, Battle>(player.Guid, battle));

        player.SendTunneled(new EncounterParticipantTerminateEncounterPacket { EncounterId = battle.Id, ReplyId = BattleRules.ServerId });

        if (battle.Boss is { } boss && !battle.BossDefeated && participant.BossBarShown)
            player.SendTunneled(new CombatPacketEnableBossDisplay { BossGuid = boss.Guid, Enable = false });

        if (ReferenceEquals(player.Zone, battle.Zone))
            SendBack(player, participant);
    }

    /// <summary>
    /// Back to where they went in from (or the world's start), on their feet.
    /// </summary>
    private void SendBack(Player player, BattleParticipant? participant)
    {
        if (player.IsKnockedOut)
            player.RecoverNow();

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
        if (zone.Battle is { } battle)
            _departures.Enqueue((battle, player.Guid));
    }

    /// <summary>
    /// A player who logged off or teleported away mid-battle is out of it. Those who left through the Exit or the end
    /// screen are already gone from it.
    /// </summary>
    private void OnDeparted(Battle battle, ulong playerGuid)
    {
        if (!battle.Participants.TryGetValue(playerGuid, out var participant) || !participant.Arrived
            || battle.Zone.TryGetPlayer(playerGuid, out _))
            return;

        participant.Left = true;
        battle.Participants.Remove(playerGuid);
        _playerBattles.TryRemove(new KeyValuePair<ulong, Battle>(playerGuid, battle));
    }

    /// <summary>
    /// Ends it for good: anyone still on the start panel has it taken down, and the zone goes.
    /// </summary>
    private void Close(Battle battle)
    {
        if (battle.State == BattleState.Closed)
            return;

        battle.State = BattleState.Closed;
        _battles.TryRemove(new KeyValuePair<int, Battle>(battle.Id, battle));

        // Leaving takes each one off the list, so it goes through a copy.
        foreach (var participant in battle.Participants.Values.ToList())
        {
            _playerBattles.TryRemove(new KeyValuePair<ulong, Battle>(participant.Player.Guid, battle));

            if (!participant.Entered)
                participant.Player.SendTunneled(new EncounterPacketEncounterShutdown { EncounterId = battle.Id, ReplyId = BattleRules.ServerId });
            else if (!participant.Left && ReferenceEquals(participant.Player.Zone, battle.Zone))
                Leave(battle, participant);
        }

        battle.Participants.Clear();

        foreach (var invitedGuid in battle.Invited)
        {
            if (_zoneManager.TryGetPlayer(invitedGuid, out var invited))
                invited.SendTunneled(new EncounterInvitationPacket { EncounterId = battle.Id, ReplyId = BattleRules.ServerId, NameId = battle.Definition.NameId });
        }

        battle.Invited.Clear();
        battle.Zone.Battle = null;
        battle.Alive.Clear();

        _zoneManager.EvictIfEmpty(battle.Zone);
    }

    #endregion

    #region Packets

    private EncounterDetailsResponsePacket GetDetails(Battle battle, bool isUpdate)
    {
        var definition = battle.Definition;
        var secondsLeft = battle.State == BattleState.Offered ? definition.TimeLimitSeconds
            : (int)Math.Max(1, (battle.EndsAt - Environment.TickCount64) / 1000);
        var objectiveState = isUpdate ? 1 : 0;

        var objectives = new List<QuestObjectiveInfo>
        {
            new()
            {
                ObjectiveId = KnockoutObjectiveId,
                NameId = KnockoutObjectiveNameId,
                DescriptionId = KnockoutObjectiveDescriptionId,
                RewardBundle = EmptyBundle(),
                State = objectiveState,
                CurrentCount = battle.Knockouts,
                RequiredCount = isUpdate ? definition.MaxKnockouts : 0,
                Kind = 1
            },
            new()
            {
                ObjectiveId = DefeatObjectiveId,
                NameId = ObjectiveNameId(battle),
                DescriptionId = definition.ObjectiveDescriptionId,
                RewardBundle = new RewardBundleBase
                {
                    Success = false,
                    Experience = definition.Stars,
                    Unknown3 = 15,
                    IconId = StarIconId,
                    NameId = StarNameId
                },
                State = objectiveState,
                CurrentCount = battle.Defeated,
                RequiredCount = isUpdate ? battle.Total : 0
            }
        };

        if (HasBossObjective(battle))
        {
            objectives.Add(new QuestObjectiveInfo
            {
                ObjectiveId = BossObjectiveId,
                NameId = definition.BossObjectiveNameId,
                DescriptionId = definition.BossObjectiveDescriptionId,
                RewardBundle = EmptyBundle(),
                State = objectiveState,
                CurrentCount = battle.BossDefeated ? 1 : 0,
                RequiredCount = isUpdate ? 1 : 0
            });
        }

        var participants = battle.Participants.Values.Select(participant => new EncounterParticipant
        {
            Key = participant.Player.Guid.GetHashCode(),
            Guid = participant.Player.Guid,
            Entered = participant.Entered,
            Position = participant.Entered ? participant.ReturnPosition : participant.Player.Position,
            Name = participant.Player.Name.FullName,
            ServerId = BattleRules.ServerId
        }).ToList();

        return new EncounterDetailsResponsePacket
        {
            EncounterId = battle.Id,
            ReplyId = BattleRules.ServerId,
            IsUpdate = isUpdate,
            MarketplaceId = isUpdate ? 0 : battle.Id & 0xFFFF,
            Details = new EncounterDetails
            {
                Id = battle.Id,
                Unknown = isUpdate ? 4 : 2,
                Teams = [participants],
                TimeLimitSeconds = secondsLeft,
                RespawnMilliseconds = CombatRules.PlayerRespawnSeconds * 1000,
                Info = new MiniGameInfoPacket
                {
                    NameId = definition.NameId,
                    IconId = definition.IconId,
                    DescriptionId = definition.DescriptionId,
                    Difficulty = definition.Difficulty,
                    ProfileType = 2,
                    Type = 4,
                    Reward = EmptyBundle(),
                    MemberReward = EmptyBundle(),
                    RewardPreview = new RewardBundleBase
                    {
                        Success = false,
                        Coins = definition.Coins,
                        Experience = definition.Stars,
                        Unknown3 = 15,
                        IconId = StarIconId,
                        NameId = StarNameId
                    },
                    Objectives = objectives,
                    ShowStarCounter = true,
                    ShowStatusIcon = true,
                    ShowActionBar = true,
                    ShowStartScreen = true,
                    ShowEndScreen = true,
                    Unknown = 1,
                    Unknown2 = true,
                    Unknown6 = isUpdate ? 0 : battle.Id
                }
            }
        };
    }

    private static RewardBundleBase EmptyBundle() => new() { Success = false, Unknown3 = 0, IconId = -1, NameId = -1 };

    private static int ObjectiveNameId(Battle battle) =>
        battle.Definition.ObjectiveNameId != 0 ? battle.Definition.ObjectiveNameId : battle.Definition.NameId;

    private static bool HasBossObjective(Battle battle) => battle.Definition.BossObjectiveNameId != 0;

    private static void SendToPlayers(Battle battle, ISerializablePacket packet)
    {
        foreach (var participant in battle.Participants.Values)
        {
            if (participant.Arrived && !participant.Left)
                participant.Player.SendTunneled(packet);
        }
    }

    #endregion
}
