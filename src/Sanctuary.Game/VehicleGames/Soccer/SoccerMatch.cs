using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using Microsoft.Extensions.Logging;

using Sanctuary.Core.Extensions;
using Sanctuary.Game.Resources.Definitions;
using Sanctuary.Game.Zones;
using Sanctuary.Packet;

namespace Sanctuary.Game.VehicleGames.Soccer;

/// <summary>
/// A Sacred Grove soccer match (the live match's setup): the player and three AI team mates (the Sidekicks) against
/// four AI players (the Snowspurs), two halves of three minutes. Other players take AI players' places, up to four a
/// side: a group plays on one team. Each client moves only its own player and reports their position, stick and
/// state, which go on to the other clients; the server (<see cref="SoccerSimulation"/>) runs the ball, possession
/// (whoever reaches it first) and tackles, the AI players (by their stick and buttons, as live), the power-ups, the
/// score and the clock.
/// <para>
/// As the zone loads, character state 0x1000 switches the client's soccer processor on and it waits for the other
/// players (game state 2). Once everyone coming is in, the server sets the match up as live did: the config, the
/// teams, the other players (registered as players, not AI) and the AI players, the kick-off formation, the colours
/// and every player's tuning. Each client says it's ready (RegisterPlayer with no guid); with all of them, "Ready!",
/// then the kick-off and play. A goal is celebrated and kicked off again; half time shows the half time board; level
/// at the end, the next goal wins.
/// </para>
/// </summary>
public sealed class SoccerMatch : ArenaMatch
{
    public const int SoccerCharacterState = 0x1000;

    // Game states (S.4.1).
    private const int StartGame = 7;
    private const int Halftime = 8;
    private const int HalftimeClosed = 9;
    private const int EndGame = 10;
    private const int KickOffState = 11;
    private const int PlayingSoccer = 12;
    private const int StartGoalCelebration = 16;
    private const int GoalCelebration = 17;
    private const int EndGoalCelebration = 18;
    private const int SuddenDeath = 19;

    private const int ReadyToKickOffMs = 4000;
    private const int KickOffToPlayMs = 1000;
    private const int CelebrationMs = 3000;
    private const int HalftimeWaitMs = 15_000;
    private const int ClockIntervalMs = 500;
    private const int BallIntervalMs = 150;
    private const int PositionIntervalMs = 500;
    private const float SuddenDeathSeconds = 120f;

    private static readonly int[] PickupModels = [0, 852, 853, 0, 887];

    private readonly SoccerPitchDefinition _pitch;
    private readonly Random _random = new();
    private readonly SoccerSimulation _simulation;

    private readonly HashSet<ulong> _readied = [];
    private readonly HashSet<ulong> _halftimeClosed = [];

    private bool _setUp;
    private long _firstInAt;
    private long _setUpAt;
    private long _firstReadyAt;
    private bool _playing;
    private int _state;
    private long _stateAt;
    private long _startedAt;
    private long _clockSentAt;
    private int _period;
    private float _periodLeft;
    private bool _suddenDeath;
    private bool _goalScored;
    private bool _kickedOff;

    public SoccerMatch(int id, VehicleGameStageDefinition stage, WorldZone zone, VehicleGameManager manager, ILogger logger)
        : base(id, stage, zone, manager, logger)
    {
        _pitch = manager.ResourceManager.VehicleGames.Definition.Soccer;

        var bounds = new Vector4(_pitch.PitchBounds[0], _pitch.PitchBounds[1], _pitch.PitchBounds[2], _pitch.PitchBounds[3]);
        var centre = new Vector3(_pitch.BallSpawn[0], SoccerRules.BallRadius, _pitch.BallSpawn[2]);

        _simulation = new SoccerSimulation(bounds, centre, _pitch.PickupSlots.Select(slot => new Vector2(slot[0], slot[1])).ToList(), _random)
        {
            Acquired = (holder, previous) => SendToPlaying(new SoccerPacketAcquireBall { Guid = holder.Guid, PreviousHolder = previous }),
            Freed = kicker => SendToPlaying(new SoccerPacketFreeBall { Position = new Vector4(_simulation.Ball.Position, 1f), Guid = kicker.Guid }),
            Lobbed = (target, flight) => SendToPlaying(new SoccerPacketHighArcBall { Target = new Vector4(target, 1f), FlightTime = flight }),
            Bounced = kind => SendToPlaying(new SoccerPacketBallBounce { Kind = (uint)kind }),
            Scored = OnScored,
            PickupSpawned = (slot, kind, lifetime) => SendToPlaying(new SoccerPacketSpawnPickUp
            {
                Kind = (byte)kind,
                ModelId = (uint)PickupModels[kind],
                Slot = (byte)slot,
                X = _pitch.PickupSlots[slot][0],
                Z = _pitch.PickupSlots[slot][1],
                Lifetime = lifetime,
                Unknown = 1f
            }),
            PickupRemoved = slot => SendToPlaying(new SoccerPacketSpawnPickUp { Slot = (byte)slot, X = _pitch.PickupSlots[slot][0], Z = _pitch.PickupSlots[slot][1], Unknown = 1f }),
            PickupTaken = OnPickupTaken,
            PickupEnded = player => SendToPlaying(GetVariableStats(player))
        };
    }

    public override int CharacterStateBit => SoccerCharacterState;

    /// <summary>
    /// Four a side.
    /// </summary>
    public override int Capacity => 2 * SoccerRules.PlayersPerTeam;

    public override bool HasPlace(ulong guid) => _simulation.TryGetPlayer(guid, out _);

    /// <summary>
    /// The players' footballers (the rest are AI).
    /// </summary>
    private IEnumerable<SoccerPlayer> Humans => _simulation.Players.Where(player => player.IsPlayer);

    private static long Now => Environment.TickCount64;

    #region Setting up

    public override void OnZoneLoaded(ArenaParticipant participant)
    {
        participant.Player.SendTunneled(new ObjectiveActivatePacket { ObjectiveId = Stage.ObjectiveId });

        if (_setUp)
            return;

        if (_firstInAt == 0)
            _firstInAt = Now;

        // The live setup's first two states: "waiting for players", until everyone coming is in.
        participant.Player.SendTunneled(GetGameState(1, unknown: 0));
        participant.Player.SendTunneled(GetGameState(2, unknown: 0));
    }

    /// <summary>
    /// The live setup, in its order after the first two states: the config, the teams, the players (each client's
    /// own, then the others registered as players), the AI players in the places left (each an NPC registered as an
    /// AI player and put on its team), the kick-off formation, the colours and everyone's tuning.
    /// </summary>
    private void SetUp(long now)
    {
        Started = true;

        _setUp = true;
        _setUpAt = now;

        var humans = Playing.ToList();
        var lineup = SoccerRules.Lineup(humans.Select(human => human.Side).ToList());

        for (var index = 0; index < humans.Count; index++)
        {
            if (lineup[index] is not { } place)
                continue;

            var (team, position) = place;
            var spot = _pitch.Players.FirstOrDefault(definition => definition.Team == team && definition.Position == position);

            _simulation.Add(new SoccerPlayer(humans[index].Guid, team, position, Point(spot is null || (team, position) == (0, 0) ? _pitch.PlayerKickOff : spot.KickOff))
            {
                Participant = humans[index],
                RunSpeed = 6.5f
            });
        }

        SendToPlaying(new SoccerPacketSetClientConfig
        {
            PitchBounds = new Vector4(_pitch.PitchBounds[0], _pitch.PitchBounds[1], _pitch.PitchBounds[2], _pitch.PitchBounds[3]),
            Tuning = [.. _pitch.Tuning],
            Tuning2 = [.. _pitch.Tuning2],
            BallModel = _pitch.BallModel,
            Unknown = _pitch.Unknown,
            Unknown2 = _pitch.Unknown2,
            BallSpawn = new Vector4(_pitch.BallSpawn[0], _pitch.BallSpawn[1], _pitch.BallSpawn[2], 1f),
            Tuning3 = [.. _pitch.Tuning3],
            Unknown3 = _pitch.Unknown3,
            Unknown4 = _pitch.Unknown4,
            Unknown5 = _pitch.Unknown5
        });

        SendGameState(2, unknown: 1);
        SendGameState(3, unknown: 1);
        SendGameState(4, unknown: 1);

        foreach (var human in Humans.ToList())
        {
            // A client has its own player already; the others are players to it, not AI.
            SendToPlaying(new SoccerPacketRegisterPlayer { Guid = human.Guid }, except: human.Guid);
            SendToPlaying(new SoccerPacketSetPlayerTeam { Guid = human.Guid, Team = (uint)human.Team, Position = (uint)human.Position });
        }

        foreach (var definition in _pitch.Players)
        {
            if (Humans.Any(human => human.Team == definition.Team && human.Position == definition.Position))
                continue;

            if (!Manager.ResourceManager.Npcs.TryGetValue(definition.NpcId, out var npcDefinition)
                || !Zone.TrySpawnArenaNpc(npcDefinition, new Vector4(Point(definition.KickOff), 1f), definition.Team == 0 ? -MathF.PI / 2f : MathF.PI / 2f,
                    SoccerCharacterState | 1, out var npc))
            {
                Logger.LogError("Soccer {encounter:X}: no player {npc}; their place is left empty.", Id, definition.NpcId);
                continue;
            }

            var ai = new SoccerPlayer(npc.Guid, definition.Team, definition.Position, Point(definition.KickOff))
            {
                Npc = npc,
                RunSpeed = definition.OneTimeStats.Length >= 8 ? BitConverter.ToSingle(Convert.FromHexString(definition.OneTimeStats[..8])) : 6.65f
            };

            _simulation.Add(ai);

            SendToPlaying(new SoccerPacketRegisterPlayer { Guid = ai.Guid, IsAi = true });
            SendToPlaying(new SoccerPacketSetPlayerTeam { Guid = ai.Guid, Team = (uint)ai.Team, Position = (uint)ai.Position });
        }

        _simulation.ResetFormation();

        SendFormation();
        SendFormation();

        SendGameState(5, unknown: 8);

        var teams = _pitch.Teams;

        SendToPlaying(new SoccerPacketTeamColours
        {
            Tint = teams[0].Tint,
            Tint2 = teams[1].Tint,
            Colour = teams[0].Colour,
            Colour2 = teams[1].Colour,
            NameId = (uint)teams[0].NameId,
            NameId2 = (uint)teams[1].NameId
        });

        foreach (var soccerPlayer in _simulation.Players)
        {
            var hex = soccerPlayer.IsPlayer ? _pitch.PlayerOneTimeStats : _pitch.Players.First(definition => definition.Team == soccerPlayer.Team && definition.Position == soccerPlayer.Position).OneTimeStats;

            SendToPlaying(new SoccerPacketSetSoccerPlayerOneTimeStats { Guid = soccerPlayer.Guid, Stats = Convert.FromHexString(hex) });
            SendToPlaying(GetVariableStats(soccerPlayer));
        }

        SendGameState(6, unknown: 8);

        Logger.LogInformation("Soccer {encounter:X}: set up with {players} player(s).", Id, Humans.Count());
    }

    #endregion

    #region Packets in

    public override void OnPacket(ArenaParticipant participant, short opCode, ReadOnlySpan<byte> data)
    {
        if (opCode != BaseSoccerPacket.OpCode || data.Length < 4)
            return;

        var now = Now;
        var subOpCode = BinaryPrimitives.ReadUInt16LittleEndian(data[2..]);

        // The client's "ready", with no guid: as its loading screen goes, which may be before the setup.
        if (subOpCode == SoccerPacketRegisterPlayer.OpCode)
        {
            if (SoccerPacketRegisterPlayer.TryDeserialize(data, out var register) && register.Guid == 0 && _readied.Add(participant.Guid) && _firstReadyAt == 0)
                _firstReadyAt = now;

            return;
        }

        if (!_simulation.TryGetPlayer(participant.Guid, out var player))
            return;

        switch (subOpCode)
        {
            case SoccerPacketUpdatePlayerPos.OpCode when SoccerPacketUpdatePlayerPos.TryDeserialize(data, out var position):
                player.Location = new Vector3(position.Position.X, 0f, position.Position.Z);
                SendToPlayingUnreliable(position, except: participant.Guid);
                break;

            case SoccerPacketUpdatePlayerControls.OpCode when SoccerPacketUpdatePlayerControls.TryDeserialize(data, out var controls):
                player.StickX = controls.X;
                player.StickY = controls.Y;
                player.Buttons = controls.Buttons;
                SendToPlaying(controls, except: participant.Guid);
                break;

            case SoccerPacketUpdatePlayerState.OpCode when SoccerPacketUpdatePlayerState.TryDeserialize(data, out var state):
                player.Location = new Vector3(state.Position.X, 0f, state.Position.Z);

                if (_state == PlayingSoccer)
                    _simulation.OnPlayerState(player, state.State, state.Charge, new Vector3(state.Facing.X, 0f, state.Facing.Z), now);

                SendToPlaying(state, except: participant.Guid);
                break;

            // The client lets go of the ball itself (not seen live): along its facing, or with the push it gives.
            case SoccerPacketFreeBall.OpCode when SoccerPacketFreeBall.TryDeserialize(data, out _):
                if (_state == PlayingSoccer)
                    _simulation.Release(player, player.Facing * 14f + new Vector3(0f, 1.5f, 0f));
                break;

            case SoccerPacketImpulseBall.OpCode when SoccerPacketImpulseBall.TryDeserialize(data, out var impulse):
                if (_state == PlayingSoccer)
                    _simulation.Release(player, new Vector3(impulse.Impulse.X, impulse.Impulse.Y, impulse.Impulse.Z));
                break;

            // The half time board was closed: the second half once every player has.
            case SoccerPacketUpdateGameState.OpCode when SoccerPacketUpdateGameState.TryDeserialize(data, out var closed):
                if (closed.State == HalftimeClosed && _state == Halftime && _halftimeClosed.Add(participant.Guid)
                    && Humans.All(human => _halftimeClosed.Contains(human.Guid)))
                    SecondHalf(now);
                break;
        }
    }

    #endregion

    #region The match

    public override void Update(long now, float deltaSeconds)
    {
        if (!_setUp)
        {
            // Everyone coming is in (or the others have had long enough).
            if (_firstInAt != 0 && Playing.Any()
                && (Expected.All(guid => Participants.TryGetValue(guid, out var participant) && participant.InZone) || now - _firstInAt > JoinWaitMs))
                SetUp(now);

            return;
        }

        if (!_playing)
        {
            // "Ready!" once every player's client is (or the slow ones have had long enough).
            if (_firstReadyAt != 0 && (Humans.All(human => _readied.Contains(human.Guid)) || now - Math.Max(_firstReadyAt, _setUpAt) > AckTimeoutMs))
            {
                _playing = true;
                _startedAt = now;

                SetState(StartGame, now);
            }

            return;
        }

        switch (_state)
        {
            case StartGame:
                if (now - _stateAt >= ReadyToKickOffMs)
                {
                    _periodLeft = _pitch.PeriodSeconds;
                    KickOff(now);
                }
                break;

            case KickOffState:
                if (now - _stateAt >= KickOffToPlayMs)
                    SetState(PlayingSoccer, now);
                break;

            case PlayingSoccer:
                _simulation.Update(now, deltaSeconds);
                _periodLeft -= deltaSeconds;

                if (_goalScored)
                {
                    _goalScored = false;
                    break;
                }

                if (_periodLeft <= 0f)
                    EndPeriod(now);
                break;

            case StartGoalCelebration:
                SetState(GoalCelebration, now);
                break;

            case GoalCelebration:
                _simulation.Update(now, deltaSeconds);

                if (now - _stateAt >= CelebrationMs)
                {
                    SetState(EndGoalCelebration, now);

                    if (_suddenDeath)
                        End(now);
                    else
                        KickOff(now);
                }
                break;

            case Halftime:
                if (now - _stateAt >= HalftimeWaitMs)
                    SecondHalf(now);
                break;

            // The banner up a moment, then a kick-off for the golden goal.
            case SuddenDeath:
                if (now - _stateAt >= 2000)
                    KickOff(now);
                break;
        }

        SendPlayers(now);
        SendBall(now);

        if (now - _clockSentAt >= ClockIntervalMs)
        {
            _clockSentAt = now;
            SendToPlaying(new SoccerPacketUpdateGameTime { PeriodLeft = MathF.Max(0f, _periodLeft), Clock = (now - _startedAt) / 1000f });
        }
    }

    private void KickOff(long now)
    {
        _kickedOff = true;
        _simulation.ResetFormation();

        SendFormation();
        SendBall(now, snap: true, unknown: 0, flags: 0xe3);
        SetState(KickOffState, now);
    }

    private void OnScored(int goal, int scoringTeam, SoccerPlayer? scorer)
    {
        var now = Now;

        _goalScored = true;

        SendToPlaying(new SoccerPacketGoalMade { Goal = (uint)goal, ScoringTeam = (uint)scoringTeam });

        if (scorer is not null)
            SendStats(scorer);

        _simulation.ClearPickups();

        // The scorers celebrate, the others hang their heads.
        foreach (var player in _simulation.Players.Where(player => !player.IsPlayer))
        {
            player.SetStick(0f, 0f);
            player.SetState(player.Team == scoringTeam ? (byte)(45 + _random.Next(3)) : (byte)(48 + _random.Next(3)));
            player.BusyUntil = now + CelebrationMs;
        }

        SendBall(now, snap: false, unknown: 1, flags: 0xc2);
        SetState(StartGoalCelebration, now, afterGoal: true);

        Logger.LogInformation("Soccer {encounter:X}: goal for team {team} ({score0}-{score1}).", Id, scoringTeam, _simulation.Score[0], _simulation.Score[1]);
    }

    private void EndPeriod(long now)
    {
        if (_period == 0)
        {
            _halftimeClosed.Clear();

            SetState(Halftime, now);
            return;
        }

        if (_simulation.Score[0] == _simulation.Score[1] && !_suddenDeath)
        {
            // Level: the next goal wins.
            _suddenDeath = true;
            _periodLeft = SuddenDeathSeconds;

            SetState(SuddenDeath, now);
            return;
        }

        End(now);
    }

    private void SecondHalf(long now)
    {
        _period = 1;
        _periodLeft = _pitch.PeriodSeconds;
        _simulation.NewPeriod();

        KickOff(now);
    }

    /// <summary>
    /// The final whistle: the results (won if the player's team leads), then the payout.
    /// </summary>
    private void End(long now)
    {
        var score = _simulation.Score;
        var winner = score[0] > score[1] ? 0 : score[1] > score[0] ? 1 : -1;

        foreach (var player in _simulation.Players.Where(player => player.IsPlayer))
        {
            player.Participant!.Won = winner == player.Team;
            player.Participant.Place = winner == -1 || winner == player.Team ? 0 : 1;
        }

        SetState(EndGame, now);
        Finish();

        Logger.LogInformation("Soccer {encounter:X} over: {score0}-{score1}.", Id, score[0], score[1]);
    }

    private void OnPickupTaken(SoccerPlayer player, int slot, int kind)
    {
        SendToPlaying(new SoccerPacketAcquirePickUp { Slot = (byte)slot, Seconds = 20f, Guid = player.Guid });
        SendToPlaying(new SoccerPacketSpawnPickUp { Slot = (byte)slot, X = _pitch.PickupSlots[slot][0], Z = _pitch.PickupSlots[slot][1], Unknown = 1f });
        SendToPlaying(GetVariableStats(player));
    }

    #endregion

    #region Packets out

    private void SetState(int state, long now, bool afterGoal = false)
    {
        _state = state;
        _stateAt = now;

        SendGameState(state, unknown: 8, afterGoal: afterGoal || _simulation.Score.Sum() > 0);
    }

    private void SendGameState(int state, int unknown, bool afterGoal = false) => SendToPlaying(GetGameState(state, unknown, afterGoal));

    private SoccerPacketUpdateGameState GetGameState(int state, int unknown, bool afterGoal = false)
    {
        var score = _simulation.Score;

        return new SoccerPacketUpdateGameState
        {
            LeadingTeam = score[0] > score[1] ? 0 : score[1] > score[0] ? 1 : -1,
            Period = _period,
            Score = score[0],
            Score2 = score[1],
            Shots = _simulation.Shots[0],
            Shots2 = _simulation.Shots[1],
            Knockdowns = _simulation.Knockdowns[0],
            Knockdowns2 = _simulation.Knockdowns[1],
            Pickups = _simulation.Pickups[0],
            Pickups2 = _simulation.Pickups[1],
            Steals = _simulation.StealCount[0],
            Steals2 = _simulation.StealCount[1],
            State = state,
            Unknown = unknown,
            Unknown3 = _kickedOff ? 1 : 0,
            Unknown4 = afterGoal ? 0 : -1
        };
    }

    /// <summary>
    /// Everybody on their kick-off spots, at once.
    /// </summary>
    private void SendFormation()
    {
        var clock = _startedAt == 0 ? 0f : (Now - _startedAt) / 1000f;

        foreach (var player in _simulation.Players)
        {
            SendToPlaying(new SoccerPacketUpdatePlayerPos
            {
                Guid = player.Guid,
                Position = new Vector4(player.Location, 1f),
                Clock = clock,
                Snap = true
            });
        }
    }

    /// <summary>
    /// The AI players' sticks and states as they change, their positions twice a second (the client moves them by
    /// their sticks; this keeps them where the server has them), and anyone's new tallies.
    /// </summary>
    private void SendPlayers(long now)
    {
        var clock = (now - _startedAt) / 1000f;

        foreach (var player in _simulation.Players)
        {
            if (player.StatsChanged)
                SendStats(player);

            if (player.IsPlayer)
                continue;

            if (player.ControlsChanged)
            {
                player.ControlsChanged = false;
                SendToPlaying(new SoccerPacketUpdatePlayerControls { Guid = player.Guid, X = player.StickX, Y = player.StickY, Buttons = player.Buttons });
            }

            if (player.StateChanged)
            {
                player.StateChanged = false;
                SendToPlaying(new SoccerPacketUpdatePlayerState
                {
                    Guid = player.Guid,
                    State = player.State,
                    Charge = player.Charge,
                    Sequence = player.Sequence,
                    Unknown = new Vector4(0f, 1f, 0f, 0f),
                    Facing = new Vector4(player.Facing, 0f),
                    Position = new Vector4(player.Location, 1f)
                });
            }

            if (now - player.PositionSentAt < PositionIntervalMs)
                continue;

            player.PositionSentAt = now;

            var direction = SoccerRules.StickDirection(player.StickX, player.StickY);

            SendToPlaying(new SoccerPacketUpdatePlayerPos
            {
                Guid = player.Guid,
                Position = new Vector4(player.Location, 1f),
                Direction = new Vector4(direction, 0f),
                Facing = new Vector4(player.Facing, 0f),
                Clock = clock,
                Speed = direction == Vector3.Zero ? 0f : player.RunSpeed
            });

            player.Npc?.UpdatePosition(new Vector4(player.Location, 1f), QuaternionExtensions.FromHeading(MathF.Atan2(player.Facing.X, player.Facing.Z)), moving: direction != Vector3.Zero);
        }
    }

    private void SendStats(SoccerPlayer player)
    {
        player.StatsChanged = false;

        SendToPlaying(new SoccerPacketUpdatePlayerStats
        {
            Guid = player.Guid,
            Knockdowns = player.Knockdowns,
            Steals = player.Steals,
            Dodges = player.Dodges,
            Goals = player.Goals,
            Score = player.Score
        });
    }

    /// <summary>
    /// The loose ball's flight, every 0.15 s as live; nothing while someone has it.
    /// </summary>
    private void SendBall(long now, bool snap = false, byte unknown = 255, byte flags = 0xc3)
    {
        var ball = _simulation.Ball;

        if (!snap && (!ball.IsLoose || now - ball.UpdateSentAt < BallIntervalMs))
            return;

        ball.UpdateSentAt = now;

        if (ball.Lobbed)
            flags |= 0x04;

        SendToPlaying(new SoccerPacketUpdateSoccerBall
        {
            Transform = Matrix4x4.CreateTranslation(ball.Position),
            Velocity = new Vector4(ball.Velocity, 1f),
            Unknown = unknown,
            Flags = flags,
            Unknown2 = ball.KickTimer,
            Unknown3 = ball.KickTimer > 0f ? 5f : 0f,
            Snap = snap
        });

        ball.KickTimer = MathF.Max(0f, ball.KickTimer - 0.76f);
    }

    /// <summary>
    /// A player's stats (kick power, footwork, speed, toughness, tackle power) and what their power-up changes.
    /// </summary>
    private SoccerPacketPlayerVariableStats GetVariableStats(SoccerPlayer player)
    {
        var stats = player.IsPlayer ? _pitch.PlayerStats : _pitch.Players.First(definition => definition.Team == player.Team && definition.Position == player.Position).Stats;

        return new SoccerPacketPlayerVariableStats
        {
            Guid = player.Guid,
            Stat = stats.ElementAtOrDefault(0),
            Stat2 = stats.ElementAtOrDefault(1),
            Stat3 = stats.ElementAtOrDefault(2) + (player.PickupKind == 1 ? 4 : 0),
            Stat4 = stats.ElementAtOrDefault(3),
            Stat5 = stats.ElementAtOrDefault(4),
            Unknown2 = player.PickupKind == 2 ? 100 : 0,
            Unknown3 = player.PickupKind == 4 ? -0.75f : 0f
        };
    }

    #endregion

    private static Vector3 Point(float[] values) => new(values[0], values[1], values[2]);

    public override void OnLeft(ArenaParticipant participant)
    {
        if (!_simulation.TryGetPlayer(participant.Guid, out var player))
            return;

        _simulation.Remove(player);
        _readied.Remove(participant.Guid);
        _halftimeClosed.Remove(participant.Guid);

        foreach (var other in Playing.Where(other => other.Guid != participant.Guid))
            other.Player.SendTunneled(new SoccerPacketRegisterPlayer { Guid = participant.Guid, Remove = true });
    }

    public override void OnClosed()
    {
        foreach (var player in _simulation.Players.ToList())
        {
            player.Npc?.Dispose();
            _simulation.Remove(player);
        }
    }
}
