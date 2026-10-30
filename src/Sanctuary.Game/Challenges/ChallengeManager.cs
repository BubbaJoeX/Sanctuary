using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;

using Microsoft.Extensions.Logging;

using Sanctuary.Core.Extensions;
using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;
using Sanctuary.Game.Interactions;
using Sanctuary.Game.Minigames;
using Sanctuary.Game.Resources.Definitions;
using Sanctuary.Game.Resources.Definitions.Rewards;
using Sanctuary.Game.Zones;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game.Challenges;

/// <summary>
/// Runs the in-world challenges. The client keeps no part of the game: it shows the start screen, the HUD (the
/// countdown, the objective tracker, the star counter, the breadcrumb trail) and the end screen as it's told, so
/// what's out there, what the player touched and whether time ran out is all decided here, 5 times a second.
/// </summary>
public sealed class ChallengeManager : IChallengeManager, IDisposable
{
    /// <summary>
    /// A challenge has one goal, shown as one tracker row.
    /// </summary>
    private const int ObjectiveId = 1;

    private const int UpdateIntervalMs = 200;

    /// <summary>
    /// Players start a challenge by its NPC, and it's called off if they wander this far from its area.
    /// </summary>
    private const float StartRange = 30f;
    private const float AbandonMargin = 60f;

    private const float TouchRange = 2.2f;
    private const float FlagRange = 4f;
    private const float ReturnRange = 4.5f;
    private const float DeliverRange = 3.5f;
    private const float CatchWanderRadius = 8f;

    private const int GreenFlagModelId = 371;   // bw_flag_01_green
    private const int RedFlagModelId = 374;     // bw_flag_01_red

    private const int PickupEffectId = 5386;    // PFX_sparkles-swirl_gold_treasure-reward
    private const int DeliverEffectId = 15534;  // PFX_stars_yellow_root_exp-reward

    /// <summary>
    /// Running out of time (or a slow race) still pays this share of the stars.
    /// </summary>
    private const float LossStarShare = 0.25f;

    private readonly ILogger _logger;
    private readonly IResourceManager _resourceManager;
    private readonly IRewardManager _rewardManager;

    private readonly ConcurrentDictionary<ulong, ChallengeSession> _sessions = new();
    private readonly Random _random = new();
    private readonly Timer _timer;

    public ChallengeManager(ILogger<ChallengeManager> logger, IResourceManager resourceManager, IRewardManager rewardManager)
    {
        _logger = logger;
        _resourceManager = resourceManager;
        _rewardManager = rewardManager;

        _timer = new Timer(_ => Update(), null, UpdateIntervalMs, UpdateIntervalMs);
    }

    public void Dispose() => _timer.Dispose();

    public bool IsGiver(int npcDefinitionId) => _resourceManager.Challenges.IsGiver(npcDefinitionId);

    public IReadOnlyList<NpcInteractionOption> GetInteractionOptions(Player player, Npc npc) =>
        _resourceManager.Challenges.OfferedBy(npc.DefinitionId).Select(challenge => new NpcInteractionOption
        {
            IconId = ContextIcons.QuestOffer,
            ButtonTextId = challenge.NameId,
            Invoke = interactingPlayer => Offer(interactingPlayer, challenge, npc)
        }).ToList();

    #region Offer and start

    /// <summary>
    /// Opens the challenge's start screen: its name, description, prize and goal.
    /// </summary>
    private void Offer(Player player, ChallengeDefinition challenge, Npc giver)
    {
        if (_sessions.TryGetValue(player.Guid, out var current) && current.Running)
        {
            ChatHelper.SendSystemMessage(player, "Finish the challenge you're on first.");
            return;
        }

        if (MinigameManager.GetSession(player) is not null)
        {
            ChatHelper.SendSystemMessage(player, "Finish the game you're playing first.");
            return;
        }

        _sessions[player.Guid] = new ChallengeSession(player, challenge, giver);

        player.SendTunneled(GetInfo(challenge, won: null));
    }

    private static MiniGameInfoPacket GetInfo(ChallengeDefinition challenge, bool? won)
    {
        var prize = new RewardBundleBase { Coins = challenge.Coins, Experience = challenge.Stars };

        return new MiniGameInfoPacket
        {
            StageId = challenge.Id,
            MiniGameId = challenge.Id,
            Type = MiniGameTypeInWorld,
            NameId = challenge.NameId,
            IconId = challenge.IconId,
            DescriptionId = challenge.DescriptionId,
            Difficulty = challenge.Difficulty,
            RewardPreview = prize,
            Reward = won is null ? new RewardBundleBase { Success = false } : prize,
            Objectives =
            [
                new QuestObjectiveInfo
                {
                    ObjectiveId = ObjectiveId,
                    NameId = challenge.NameId,
                    DescriptionId = challenge.DescriptionId,
                    RequiredCount = challenge.Count,
                    Category = 1
                }
            ],
            ShowStarCounter = true,
            ShowStatusIcon = true,
            ShowActionBar = true,
            ShowStartScreen = true,
            ShowEndScreen = true
        };
    }

    /// <summary>
    /// The client's in-world minigame type (the minigame HUD, no movie).
    /// </summary>
    private const int MiniGameTypeInWorld = 1;

    public bool TryStart(Player player, int stageId)
    {
        if (!_sessions.TryGetValue(player.Guid, out var session) || session.Challenge.Id != stageId)
            return false;

        lock (session)
        {
            if (session.Running || session.Ended)
                return true;

            var challenge = session.Challenge;

            if (!ReferenceEquals(player.Zone, session.Giver.Zone) || Distance(player.Position, session.Giver.Position) > StartRange)
            {
                ChatHelper.SendSystemMessage(player, "Go back to whoever offered you the challenge to start it.");
                End(session, ChallengeOutcome.Cancelled);
                return true;
            }

            if (!SetUp(session))
            {
                ChatHelper.SendSystemMessage(player, "There's no room for that challenge here right now.");
                _logger.LogWarning("Challenge {id} ({name}) couldn't be set up for {player}.", challenge.Id, challenge.Comment, player.Name.FullName);
                End(session, ChallengeOutcome.Cancelled);
                return true;
            }

            session.Running = true;
            session.StartedAt = Environment.TickCount64;
            session.EndsAt = session.StartedAt + challenge.TimeLimitSeconds * 1000L;

            // The tracker only picks up rows that exist when the HUD opens.
            player.SendTunneled(new TaskAddPacket
            {
                TaskId = ObjectiveId,
                IconId = challenge.IconId,
                TextId = challenge.NameId,
                Category = 1,
                Total = session.Total
            });

            player.SendTunneled(new ObjectiveActivatePacket { ObjectiveId = ObjectiveId, Total = session.Total });

            player.SendTunneled(new MiniGameHeaderPacket { SubOpCode = BaseMiniGamePacket.StartGame, StageId = challenge.Id });

            player.SendTunneled(new StartTimerPacket { Seconds = challenge.TimeLimitSeconds });

            if (challenge.Kind == ChallengeKind.Race)
            {
                player.SendTunneled(new PacketClientFlashTimer { Start = true, Type = PacketClientFlashTimer.CheckpointType });
                player.SendTunneled(new PacketClientAreaTimer { Type = PacketClientAreaTimer.GoType, Guid = player.Guid });
            }

            PointToNext(session);
        }

        return true;
    }

    /// <summary>
    /// Puts out the challenge's things around its NPC: pickups, flags or animals, or picks the people to deliver to.
    /// </summary>
    private bool SetUp(ChallengeSession session)
    {
        var challenge = session.Challenge;
        var center = session.Giver.Position.ToVector3();

        if (session.Giver.Zone is not BaseZone zone)
            return false;

        Vector3? Sample(Vector3 spot, float radius) =>
            zone.TryFindRandomNavMeshPoint(spot, radius, _random, out var point) ? point : null;

        if (challenge.Kind == ChallengeKind.Deliver)
        {
            foreach (var npcId in challenge.RecipientNpcIds)
            {
                var recipient = zone.Npcs
                    .Where(npc => npc.DefinitionId == npcId && npc.Visible)
                    .MinBy(npc => npc.Position.ToVector3().DistanceSquaredXZ(center));

                if (recipient is not null)
                    session.Recipients.Add(recipient);

                if (session.Recipients.Count == challenge.Count)
                    break;
            }

            session.Total = session.Recipients.Count;
            return session.Total > 0;
        }

        var points = challenge.Kind == ChallengeKind.Race
            ? ChallengeLayout.RaceLoop(center, challenge.Radius, challenge.Count, _random.NextSingle() * MathF.Tau, Sample)
            : ChallengeLayout.Scatter(center, challenge.Radius, challenge.Count, Sample);

        if (points.Count < Math.Min(2, challenge.Count))
            return false;

        for (var i = 0; i < points.Count; i++)
        {
            var modelId = challenge.Kind != ChallengeKind.Race ? challenge.ModelId
                : i == points.Count - 1 ? RedFlagModelId : GreenFlagModelId;

            if (!zone.TryAddNpc((owner, guid) => new ChallengeObject(owner, session.Player.Guid) { Guid = guid }, out var thing))
                continue;

            var index = i;

            thing.ModelId = modelId;
            thing.Name = string.Empty;
            thing.HideNamePlate = true;
            thing.CursorId = 17;
            thing.HasCursor = true;
            thing.InteractRange = 6;
            thing.Visible = true;
            thing.InteractAction = player => Touch(session, index, clicked: true);

            // Animals wander round where they were put.
            thing.SpawnPosition = new Vector4(points[i], 1f);
            thing.UpdatePosition(thing.SpawnPosition, Quaternion.CreateFromYawPitchRoll(_random.NextSingle() * MathF.Tau, 0f, 0f));

            if (challenge.Kind == ChallengeKind.Catch)
                thing.SetBehavior(NpcBehavior.Wander, wanderRadius: CatchWanderRadius);

            session.Things.Add(thing);
        }

        session.Total = session.Things.Count;
        return session.Total > 0;
    }

    #endregion

    #region Playing

    private void Update()
    {
        var now = Environment.TickCount64;

        foreach (var session in _sessions.Values)
        {
            try
            {
                lock (session)
                {
                    if (!session.Running || session.Ended)
                        continue;

                    var player = session.Player;

                    if (!ReferenceEquals(player.Zone, session.Giver.Zone)
                        || Distance(player.Position, session.Giver.Position) > session.Challenge.Radius + AbandonMargin)
                    {
                        ChatHelper.SendSystemMessage(player, "You left the challenge behind.");
                        End(session, ChallengeOutcome.Cancelled);
                        continue;
                    }

                    if (now >= session.EndsAt)
                    {
                        End(session, ChallengeOutcome.TimedOut);
                        continue;
                    }

                    CheckTouches(session);
                }
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Challenge {id} for {player} failed.", session.Challenge.Id, session.Player.Name.FullName);
            }
        }
    }

    /// <summary>
    /// What the player is standing at: a pickup or a flag they run through, an animal they catch, a person they
    /// deliver to, the NPC they bring something back to.
    /// </summary>
    private void CheckTouches(ChallengeSession session)
    {
        var position = session.Player.Position.ToVector3();
        var challenge = session.Challenge;

        switch (challenge.Kind)
        {
            case ChallengeKind.Race:
                if (session.Done < session.Things.Count && position.DistanceSquaredXZ(session.Things[session.Done].Position.ToVector3()) <= FlagRange * FlagRange)
                    Touch(session, session.Done, clicked: false);
                break;

            case ChallengeKind.Deliver:
                for (var i = 0; i < session.Recipients.Count; i++)
                {
                    if (!session.Delivered.Contains(i) && position.DistanceSquaredXZ(session.Recipients[i].Position.ToVector3()) <= DeliverRange * DeliverRange)
                    {
                        session.Delivered.Add(i);
                        session.Recipients[i].PlayEffect(DeliverEffectId);
                        Progress(session);
                    }
                }
                break;

            case ChallengeKind.Return when session.Carrying:
                if (position.DistanceSquaredXZ(session.Giver.Position.ToVector3()) <= ReturnRange * ReturnRange)
                {
                    session.Carrying = false;
                    session.Giver.PlayEffect(DeliverEffectId);
                    Progress(session);
                }
                break;

            default:
                for (var i = 0; i < session.Things.Count; i++)
                {
                    if (!session.Taken.Contains(i) && position.DistanceSquaredXZ(session.Things[i].Position.ToVector3()) <= TouchRange * TouchRange)
                        Touch(session, i, clicked: false);
                }
                break;
        }
    }

    /// <summary>
    /// The player reached (or clicked) one of the challenge's things.
    /// </summary>
    private void Touch(ChallengeSession session, int index, bool clicked)
    {
        lock (session)
        {
            if (!session.Running || session.Ended || session.Taken.Contains(index))
                return;

            var challenge = session.Challenge;
            var thing = session.Things[index];

            // A click from afar would skip the running.
            if (clicked && Distance(session.Player.Position, thing.Position) > thing.InteractRange)
                return;

            switch (challenge.Kind)
            {
                case ChallengeKind.Race when index != session.Done:
                    return;

                case ChallengeKind.Return when session.Carrying:
                    if (clicked)
                        ChatHelper.SendSystemMessage(session.Player, "Bring back the one you have first.");
                    return;
            }

            session.Taken.Add(index);
            thing.Remove(PickupEffectId);

            if (challenge.Kind == ChallengeKind.Return)
            {
                session.Carrying = true;
                PointTo(session, session.Giver);
                return;
            }

            Progress(session);
        }
    }

    /// <summary>
    /// One more done: the tracker, the goal, the star counter and the trail to the next one; and the win at the end.
    /// </summary>
    private void Progress(ChallengeSession session)
    {
        var player = session.Player;
        var challenge = session.Challenge;

        session.Done++;

        player.SendTunneled(new TaskUpdatePacket { TaskId = ObjectiveId, Count = session.Done });
        player.SendTunneled(new ObjectiveUpdatePacket { ObjectiveId = ObjectiveId, Count = session.Done });
        player.SendTunneled(new MiniGameStarsEarnedPacket { StageId = challenge.Id, Stars = StarsFor(session, won: true) });

        if (session.Done >= session.Total)
        {
            var won = challenge.Kind != ChallengeKind.Race || challenge.RecordSeconds <= 0
                || Environment.TickCount64 - session.StartedAt <= challenge.RecordSeconds * 1000L;

            End(session, won ? ChallengeOutcome.Won : ChallengeOutcome.Slow);
            return;
        }

        PointToNext(session);
    }

    /// <summary>
    /// The stars for what's done so far: all of them for a win, a share for a loss.
    /// </summary>
    private static int StarsFor(ChallengeSession session, bool won)
    {
        var earned = session.Total == 0 ? 0 : session.Challenge.Stars * session.Done / session.Total;

        return won ? earned : (int)MathF.Round(earned * LossStarShare);
    }

    private void PointToNext(ChallengeSession session)
    {
        var position = session.Player.Position.ToVector3();

        if (session.Challenge.Kind == ChallengeKind.Deliver)
        {
            var next = ChallengeLayout.Nearest(position, session.Recipients.Select(npc => npc.Position.ToVector3()).ToList(), i => !session.Delivered.Contains(i));

            if (next >= 0)
                PointTo(session, session.Recipients[next]);
            return;
        }

        var nextThing = session.Challenge.Kind == ChallengeKind.Race
            ? (session.Done < session.Things.Count ? session.Done : -1)
            : ChallengeLayout.Nearest(position, session.Things.Select(thing => thing.Position.ToVector3()).ToList(), i => !session.Taken.Contains(i));

        if (nextThing >= 0)
            PointTo(session, session.Things[nextThing]);
    }

    /// <summary>
    /// The breadcrumb trail and target arrow lead to <paramref name="target"/>.
    /// </summary>
    private static void PointTo(ChallengeSession session, Npc target) => session.Player.SendTunneled(new ObjectiveTargetUpdatePacket
    {
        Active = true,
        TaskId = ObjectiveId,
        Unknown = ObjectiveId,
        Guid = target is ChallengeObject ? 0 : target.Guid,
        NameId = session.Challenge.NameId,
        PositionX = target.Position.X,
        PositionY = target.Position.Y,
        PositionZ = target.Position.Z
    });

    #endregion

    #region Ending

    private enum ChallengeOutcome
    {
        Won,
        /// <summary>A race finished, but slower than the record.</summary>
        Slow,
        TimedOut,
        Cancelled
    }

    public bool TryCancel(Player player)
    {
        if (!_sessions.TryGetValue(player.Guid, out var session))
            return false;

        lock (session)
            End(session, ChallengeOutcome.Cancelled);

        return true;
    }

    public void OnPlayerLeft(Player player)
    {
        if (!_sessions.TryGetValue(player.Guid, out var session))
            return;

        lock (session)
            End(session, ChallengeOutcome.Cancelled, playerLeft: true);
    }

    /// <summary>
    /// Takes the challenge's things away and closes the client's game; a finished one gets its end screen and pays.
    /// Called under the session's lock.
    /// </summary>
    private void End(ChallengeSession session, ChallengeOutcome outcome, bool playerLeft = false)
    {
        if (session.Ended)
            return;

        session.Ended = true;
        _sessions.TryRemove(new KeyValuePair<ulong, ChallengeSession>(session.Player.Guid, session));

        foreach (var (thing, index) in session.Things.Select((thing, index) => (thing, index)))
        {
            if (!session.Taken.Contains(index))
                thing.Dispose();
        }

        if (playerLeft)
            return;

        var player = session.Player;
        var challenge = session.Challenge;

        if (!session.Running)
        {
            // Only the start screen was up.
            player.SendTunneled(new MiniGameHeaderPacket { SubOpCode = BaseMiniGamePacket.LeaveGame, StageId = challenge.Id });
            return;
        }

        var won = outcome == ChallengeOutcome.Won;
        var finished = outcome is ChallengeOutcome.Won or ChallengeOutcome.Slow;
        var secondsLeft = (int)Math.Max(0, (session.EndsAt - Environment.TickCount64) / 1000);

        var stars = outcome == ChallengeOutcome.Cancelled ? 0 : StarsFor(session, won);
        var coins = won ? challenge.Coins : finished ? challenge.Coins / 2 : 0;

        player.SendTunneled(new StopTimerPacket());
        player.SendTunneled(new ObjectiveTargetUpdatePacket { Active = false });

        if (challenge.Kind == ChallengeKind.Race)
        {
            player.SendTunneled(new PacketClientFlashTimer { Start = false, Type = PacketClientFlashTimer.CheckpointType });

            if (finished)
            {
                player.SendTunneled(new PacketClientAreaTimer
                {
                    Type = won ? PacketClientAreaTimer.PersonalBestType : PacketClientAreaTimer.TimeType,
                    TimeMs = (int)(Environment.TickCount64 - session.StartedAt),
                    Guid = player.Guid
                });
            }
        }

        if (finished)
        {
            player.SendTunneled(new TaskCompletePacket { TaskId = ObjectiveId });
            player.SendTunneled(new ObjectiveCompletePacket { ObjectiveId = ObjectiveId, Points = stars });
        }
        else
        {
            player.SendTunneled(new TaskFailPacket { TaskId = ObjectiveId });
            player.SendTunneled(new ObjectiveFailPacket { ObjectiveId = ObjectiveId });
        }

        var result = GetInfo(challenge, won);
        result.Reward = new RewardBundleBase { Coins = coins, Experience = stars };

        player.SendTunneled(new MiniGameUpdateRewardPacket { StageId = challenge.Id, Info = result });

        player.SendTunneled(new MiniGameGameOverPacket
        {
            StageId = challenge.Id,
            Won = won,
            StarsEarned = stars,
            SecondsLeft = secondsLeft,
            Score = session.Done
        });

        player.SendTunneled(new MiniGameHeaderPacket { SubOpCode = BaseMiniGamePacket.LeaveGame, StageId = challenge.Id });

        if (coins > 0)
            _rewardManager.TryGrantCurrency(player, CurrencyType.Coins, coins);

        if (stars > 0)
        {
            var boosted = player.ApplyExperienceBoost(stars);

            if (challenge.ProfileId != 0)
                _rewardManager.TryGrantExperience(player, boosted, challenge.ProfileId);
            else
                _rewardManager.TryGrantExperience(player, boosted);
        }

        _logger.LogInformation("{player} {outcome} challenge {id} ({name}): {done}/{total}, {coins} coins, {stars} stars.",
            player.Name.FullName, outcome, challenge.Id, challenge.Comment, session.Done, session.Total, coins, stars);
    }

    #endregion

    private static float Distance(Vector4 a, Vector4 b) => Vector3.Distance(a.ToVector3(), b.ToVector3());

    private sealed class ChallengeSession(Player player, ChallengeDefinition challenge, Npc giver)
    {
        public Player Player { get; } = player;
        public ChallengeDefinition Challenge { get; } = challenge;
        public Npc Giver { get; } = giver;

        public bool Running { get; set; }
        public bool Ended { get; set; }

        public long StartedAt { get; set; }
        public long EndsAt { get; set; }

        /// <summary>
        /// How many it takes to finish: what was put out, or the people found to deliver to.
        /// </summary>
        public int Total { get; set; }

        public int Done { get; set; }

        public List<ChallengeObject> Things { get; } = [];
        public HashSet<int> Taken { get; } = [];

        public List<Npc> Recipients { get; } = [];
        public HashSet<int> Delivered { get; } = [];

        /// <summary>
        /// For Return: something picked up and not yet brought back.
        /// </summary>
        public bool Carrying { get; set; }
    }
}
