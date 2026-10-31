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

namespace Sanctuary.Game.VehicleGames.Race;

/// <summary>
/// A kart race on one of the tracks (the live Wildwood race's setup): the player's kart against seven AI karts over
/// three laps. The client drives only its own kart and reports the gates and boxes it goes through; the server
/// (<see cref="RaceSimulation"/>) drives the AI karts along their drivers' live lines and works out every kart's
/// place, gap, distance to the finish, laps and lap times, sent for every kart 20 times a second as live did. The
/// client ends its race when the server's lap count for its kart reaches the total.
/// <para>
/// The client's race processor (switched on by character state 0x400) registers, then acts on each game state in
/// turn and acknowledges it: 3 waits for the players' karts, 4 for the AI karts, 5 loads them, 6 starts its kart's
/// state stream, 8 runs the countdown (each number echoed back), 9 is GO, 10 karts are finishing, 11 is over (anyone
/// still racing is finished there) and the Exit comes up.
/// </para>
/// </summary>
public sealed class RaceMatch : ArenaMatch
{
    public const int RaceCharacterState = 0x400;

    private const int SetupWaitMs = 10_000;
    private const int PositionIntervalMs = 200;

    // Once the players are home the AI get this long to finish; a player still out this long after the winner is
    // finished where they are.
    private const int AiFinishWaitMs = 20_000;
    private const int StragglerWaitMs = 90_000;

    private const float PickupReach = 4f;

    private static readonly Dictionary<RaceItem, int> EffectMs = new()
    {
        [RaceItem.TimedEffect] = 6000,
        [RaceItem.Shield] = 8000
    };

    private readonly RaceTrackDefinition _definition;
    private readonly VehicleConfig _playerVehicle;
    private readonly Random _random = new();
    private readonly RaceSimulation _simulation;

    private readonly Dictionary<ulong, ulong> _registered = [];
    private readonly HashSet<ulong> _acks = [];
    private readonly HashSet<ulong> _echoes = [];

    private int _gameState;
    private long _gameStateAt;
    private long _firstRegisteredAt;

    private int _countdown = -1;
    private long _countdownAt;
    private long _countdownEchoedAt;

    private long _firstFinishAt;
    private long _playersHomeAt;

    public RaceMatch(int id, VehicleGameStageDefinition stage, WorldZone zone, VehicleGameManager manager, ILogger logger)
        : base(id, stage, zone, manager, logger)
    {
        var games = manager.ResourceManager.VehicleGames;

        if (stage.Track is null || !games.TryGetTrack(stage.Track, out var definition))
            throw new InvalidOperationException($"No track for stage {stage.Id}.");

        if (!games.TryGetVehicle(definition.PlayerVehicle, out var playerVehicle))
            throw new InvalidOperationException($"No vehicle {definition.PlayerVehicle} for the race.");

        _definition = definition;
        _playerVehicle = playerVehicle;

        _simulation = new RaceSimulation(new RaceTrack(definition), definition.Laps, _random)
        {
            Bumped = (kart, player, impulse) => SendToPlaying(new VehicleRacePacketVehicleHit { Guid = kart.Guid, OtherGuid = player.Guid, Impulse = impulse }),
            PickupTaken = OnPickupTaken,
            PickupSpawned = pickup => SendToPlaying(new VehicleRacePacketCreateProxiedVehicleRacePickup
            {
                PickupId = (uint)pickup.Id,
                ModelId = (uint)_definition.PickupModelId,
                Position = new Vector4(pickup.Position, 1f),
                Radius = _definition.PickupRadius,
                Unknown = (uint)_definition.PickupValue
            }),
            Finished = OnFinished,
            Reset = (kart, transform) => SendToPlaying(new VehicleRacePacketResetProxiedVehicleRace { Guid = kart.Guid, Transform = transform })
        };
    }

    public override int CharacterStateBit => RaceCharacterState;

    private static long Now => Environment.TickCount64;

    private IReadOnlyList<RaceKart> Karts => _simulation.Karts;

    #region Packets in

    public override void OnPacket(ArenaParticipant participant, short opCode, ReadOnlySpan<byte> data)
    {
        if (opCode != BaseVehicleRacePacket.OpCode || data.Length < 4)
            return;

        var now = Now;

        switch (BinaryPrimitives.ReadUInt16LittleEndian(data[2..]))
        {
            case VehicleRacePacketRegisterVehicleRacePlayer.OpCode when VehicleRacePacketRegisterVehicleRacePlayer.TryDeserialize(data, out var register):
                if (_gameState == 0 && _registered.TryAdd(participant.Guid, register.Clock) && _firstRegisteredAt == 0)
                    _firstRegisteredAt = now;
                break;

            case VehicleRacePacketUpdateVehicleGamePlayerState.OpCode when VehicleRacePacketUpdateVehicleGamePlayerState.TryDeserialize(data, out var ack):
                if (ack.State == _gameState)
                    _acks.Add(participant.Guid);
                break;

            case VehicleRacePacketVehicleRaceCountdownStatus.OpCode when VehicleRacePacketVehicleRaceCountdownStatus.TryDeserialize(data, out var echo):
                if (echo.Count == _countdown && _echoes.Add(participant.Guid) && AllIn(_echoes))
                    _countdownEchoedAt = now;
                break;

            case VehicleRacePacketUpdateVehicleRaceServerData.OpCode when VehicleRacePacketUpdateVehicleRaceServerData.TryDeserialize(data, out var state):
                OnKartState(participant, state);
                break;

            case VehicleRacePacketGateHit.OpCode when VehicleRacePacketGateHit.TryDeserialize(data, out var gate):
                if (_simulation.TryGetKart(participant.Guid, out var kart))
                    _simulation.OnReportedGate(kart, new Vector3(gate.Position.X, gate.Position.Y, gate.Position.Z), now);
                break;

            case VehicleRacePacketVehicleHit.OpCode when VehicleRacePacketVehicleHit.TryDeserialize(data, out var hit):
                if (hit.Guid == participant.Guid && _simulation.TryGetKart(hit.Guid, out var bumper) && _simulation.TryGetKart(hit.OtherGuid, out var bumped))
                    _simulation.OnReportedBump(bumper, bumped, now);
                break;

            // Asked every frame until answered; only a box in reach of the kart answers.
            case VehicleRacePacketPickupHitRequest.OpCode when VehicleRacePacketPickupHitRequest.TryDeserialize(data, out var pickupHit):
                if (_gameState is 9 or 10 && _simulation.TryGetKart(participant.Guid, out var touching))
                    _simulation.TryTakePickup(touching, new Vector3(pickupHit.Position.X, pickupHit.Position.Y, pickupHit.Position.Z), PickupReach, now);
                break;

            case VehicleRacePacketUseItemRequest.OpCode when VehicleRacePacketUseItemRequest.TryDeserialize(data, out _):
                UseItem(participant, now);
                break;

            // The boost meter filled: a boost for the slots (the client only asks with room for it).
            case VehicleRacePacketBoostAwardRequest.OpCode when VehicleRacePacketBoostAwardRequest.TryDeserialize(data, out _):
                if (_simulation.TryGetKart(participant.Guid, out var boosting) && boosting.Items.Count < 3)
                {
                    boosting.Items.Enqueue(RaceItem.Boost);
                    participant.Player.SendTunneled(new VehicleRacePacketBoostAwardResponse());
                }
                break;
        }
    }

    /// <summary>
    /// The player's own kart, every frame: where it is (for its progress, the boxes and the AI), and on to any
    /// other players as their view of it, by its grid slot.
    /// </summary>
    private void OnKartState(ArenaParticipant participant, VehicleRacePacketUpdateVehicleRaceServerData state)
    {
        if (!_simulation.TryGetKart(participant.Guid, out var kart))
            return;

        if (kart.LastState is { } last && state.Clock <= last.Clock)
            return;

        kart.LastState = state;
        kart.Position = state.Transform.Translation;
        kart.Velocity = state.LinearVelocity;
        kart.Heading = ArenaRules.Heading(new Vector3(state.Transform.M31, 0f, state.Transform.M33));

        SendToPlayingUnreliable(new VehicleRacePacketUpdateVehicleRaceClientData
        {
            Transform = state.Transform,
            LinearVelocity = state.LinearVelocity,
            AngularVelocity = state.AngularVelocity,
            Steering = state.Steering,
            Flags = (byte)((state.Flags & ~0x38) | (kart.Slot << 3)),
            Clock = state.Clock
        }, except: participant.Guid);
    }

    private void UseItem(ArenaParticipant participant, long now)
    {
        if (!_simulation.TryGetKart(participant.Guid, out var kart) || !kart.Items.TryDequeue(out var item))
            return;

        participant.Player.SendTunneled(new VehicleRacePacketUseItemResponse { Item = (uint)item });

        if (EffectMs.TryGetValue(item, out var ms))
            kart.Effects[item] = now + ms;
    }

    #endregion

    #region The game states

    public override void Update(long now, float deltaSeconds)
    {
        switch (_gameState)
        {
            case 0:
                if (_registered.Count > 0 && (Playing.All(participant => _registered.ContainsKey(participant.Guid)) || now - _firstRegisteredAt > SetupWaitMs))
                    SetUp(now);
                return;

            case 3:
                if (Acknowledged(now))
                {
                    SpawnAiKarts(now);
                    SendToPlaying(new VehicleRacePacketUpdateNpcCount { Count = (uint)Karts.Count(kart => !kart.IsPlayer) });
                    SetGameState(4, now);
                }
                break;

            case 4:
            case 5:
            case 6:
                if (Acknowledged(now))
                    SetGameState(_gameState + 1, now);
                break;

            case 7:
                if (Acknowledged(now))
                {
                    SetGameState(8, now);
                    SendCountdown(4, now);
                }
                break;

            case 8:
                UpdateCountdown(now);
                break;

            case 9:
            case 10:
                _simulation.Update(now, deltaSeconds);

                ExpireEffects(now);
                SendProgress(now);
                SendAiPositions(now);

                if (IsOver(now))
                    End(now);
                break;

            case 11:
                SendAiPositions(now);
                break;
        }
    }

    /// <summary>
    /// Everyone registered: the track's config and the players' karts, then game state 3.
    /// </summary>
    private void SetUp(long now)
    {
        SendToPlaying(new VehicleRacePacketSetRaceConfig
        {
            Laps = (uint)_definition.Laps,
            TrackId = (uint)_definition.TrackId,
            FinishCameraDelay = _definition.FinishCameraDelay,
            FinishCameraTransform = Matrix(_definition.FinishCamera),
            MissileExplosionEffectId = (uint)_definition.MissileExplosionEffectId,
            MineExplosionEffectId = (uint)_definition.MineExplosionEffectId
        });

        // The first player takes the live player's slot; any others take AI drivers' slots.
        var slots = _definition.Grid.OrderBy(slot => slot.Slot == _definition.PlayerSlot ? 0 : 1).ThenBy(slot => slot.Slot).ToList();
        var index = 0;

        foreach (var participant in Playing.Where(participant => _registered.ContainsKey(participant.Guid)).ToList())
        {
            if (index >= slots.Count)
                break;

            var slot = slots[index++];

            _simulation.Add(new RaceKart(participant.Guid, slot.Slot, _playerVehicle, slot.Point, slot.Heading)
            {
                Participant = participant,
                RegisterClock = _registered[participant.Guid]
            });

            // The server's idea of where they are, so the AI drivers spawn in their view.
            participant.Player.UpdatePosition(new Vector4(slot.Point, 1f), QuaternionExtensions.FromHeading(slot.Heading));
        }

        foreach (var kart in Karts)
            SendKart(kart, now);

        SendToPlaying(new VehicleRacePacketUpdatePlayerCount { Count = (uint)Karts.Count });
        SetGameState(3, now);

        Logger.LogInformation("Race {encounter:X}: {players} player(s) registered.", Id, Karts.Count);
    }

    /// <summary>
    /// The AI karts on the slots the players left: each driver (an NPC with the kart's guid) first, then the kart.
    /// </summary>
    private void SpawnAiKarts(long now)
    {
        var taken = Karts.Select(kart => kart.Slot).ToHashSet();

        foreach (var driver in _definition.Drivers)
        {
            if (taken.Contains(driver.Slot))
                continue;

            var slot = _definition.Grid.First(slot => slot.Slot == driver.Slot);

            if (!Manager.ResourceManager.Npcs.TryGetValue(driver.NpcId, out var npcDefinition)
                || !Manager.ResourceManager.VehicleGames.TryGetVehicle(driver.Vehicle, out var vehicle)
                || !_simulation.Track.TryGetLine(driver.Slot, out var line))
            {
                Logger.LogError("Race {encounter:X}: no driver {npc}, vehicle {vehicle} or line; slot {slot} is left empty.", Id, driver.NpcId, driver.Vehicle, driver.Slot);
                continue;
            }

            if (!Zone.TrySpawnArenaNpc(npcDefinition, new Vector4(slot.Point, 1f), slot.Heading, RaceCharacterState | 1, out var npc))
                continue;

            var kart = new RaceKart(npc.Guid, driver.Slot, vehicle, slot.Point, slot.Heading)
            {
                Driver = npc,
                Line = line,
                Pace = 0.95f + (float)_random.NextDouble() * 0.06f
            };

            _simulation.Add(kart);

            SendKart(kart, now);
        }
    }

    private void SendKart(RaceKart kart, long now)
    {
        var spawn = ArenaRules.Transform(kart.SpawnPosition, ArenaRules.Forward(kart.SpawnHeading), Vector3.UnitY);

        foreach (var participant in Playing)
        {
            participant.Player.SendTunneled(new VehicleRacePacketCreateProxiedVehicleRace
            {
                Config = kart.Config,
                Guid = kart.Guid,
                Spawn = spawn,
                IsLocalPlayer = kart.Guid == participant.Guid,
                GridSlot = (byte)kart.Slot
            });

            // Its state at the spawn: a player's own kart carries its client's clock, the rest the server's.
            participant.Player.SendTunneled(new VehicleRacePacketUpdateVehicleRaceClientData
            {
                Transform = spawn,
                Flags = (byte)(kart.Slot << 3),
                Clock = kart.IsPlayer ? kart.RegisterClock : (ulong)now
            });

            participant.Player.SendTunneled(GetProgress(kart, 0, 0f, [kart.Guid], now));
        }
    }

    private void SetGameState(int state, long now)
    {
        _gameState = state;
        _gameStateAt = now;
        _acks.Clear();

        SendToPlaying(new VehicleRacePacketUpdateVehicleGameState { State = (uint)state });
    }

    private bool Acknowledged(long now) => AllIn(_acks) || now - _gameStateAt > AckTimeoutMs;

    private bool AllIn(HashSet<ulong> answered) =>
        Playing.Where(participant => _simulation.TryGetKart(participant.Guid, out _)).All(participant => answered.Contains(participant.Guid));

    private void SendCountdown(int count, long now)
    {
        _countdown = count;
        _countdownAt = now;
        _countdownEchoedAt = 0;
        _echoes.Clear();

        SendToPlaying(new VehicleRacePacketVehicleRaceCountdownStatus { Count = (uint)count });
    }

    /// <summary>
    /// Each number goes a moment after the clients echoed the last (live never saw an echo of the 4).
    /// </summary>
    private void UpdateCountdown(long now)
    {
        var due = _countdown == 4
            ? now - _countdownAt >= 750
            : (_countdownEchoedAt != 0 && now - _countdownEchoedAt >= 300) || now - _countdownAt >= 1500;

        if (!due)
            return;

        if (_countdown > 0)
        {
            SendCountdown(_countdown - 1, now);
            return;
        }

        _countdown = -1;

        SetGameState(9, now);

        _simulation.Start(now);

        Logger.LogInformation("Race {encounter:X}: GO with {karts} karts.", Id, Karts.Count);
    }

    /// <summary>
    /// The race is over once every player is home and the AI had a while to follow (or have), or a player is still
    /// out long after the winner.
    /// </summary>
    private bool IsOver(long now)
    {
        var players = Karts.Where(kart => kart.IsPlayer).ToList();

        if (players.Count == 0)
            return true;

        if (players.All(kart => kart.Progress.Finished))
        {
            if (_playersHomeAt == 0)
                _playersHomeAt = now;

            return Karts.All(kart => kart.Progress.Finished) || now - _playersHomeAt > AiFinishWaitMs;
        }

        return _firstFinishAt != 0 && now - _firstFinishAt > StragglerWaitMs;
    }

    private void OnFinished(RaceKart kart)
    {
        var now = Now;

        if (_firstFinishAt == 0)
        {
            _firstFinishAt = now;

            // Karts are finishing: the client's finish test carries on, its HUD stays up.
            SetGameState(10, now);
        }

        Logger.LogInformation("Race {encounter:X}: {kart} finished {place}.", Id,
            kart.IsPlayer ? kart.Participant!.Player.Name.FullName : kart.Driver?.Name ?? kart.Guid.ToString("X"), kart.Progress.FinishPlace + 1);
    }

    /// <summary>
    /// Over: the final order, the payout, and game state 11 (anyone still racing is finished there, and the Exit
    /// comes up).
    /// </summary>
    private void End(long now)
    {
        var standings = _simulation.Standings();

        for (var place = 0; place < standings.Count; place++)
        {
            if (standings[place].Participant is not { } participant)
                continue;

            participant.Place = place;
            participant.Won = place == 0;
        }

        SendProgress(now);
        Finish();
        SetGameState(11, now);

        Logger.LogInformation("Race {encounter:X} over: {standings}.", Id,
            string.Join(", ", standings.Select(kart => kart.IsPlayer ? kart.Participant!.Player.Name.FullName : kart.Driver?.Name ?? kart.Guid.ToString("X"))));
    }

    #endregion

    #region What happens

    private void OnPickupTaken(RaceKart kart, RacePickup pickup, RaceItem? item)
    {
        SendToPlaying(new VehicleRacePacketDestroyProxiedVehicleRacePickup { PickupId = (uint)pickup.Id });

        if (item is { } given)
            kart.Participant?.Player.SendTunneled(new VehicleRacePacketPickupHitResponse { Item = (uint)given });
    }

    private void ExpireEffects(long now)
    {
        foreach (var kart in Karts)
        {
            if (kart.Effects.Count == 0)
                continue;

            foreach (var (item, until) in kart.Effects.ToList())
            {
                if (now < until)
                    continue;

                kart.Effects.Remove(item);
                kart.Participant?.Player.SendTunneled(new VehicleRacePacketItemExpired { Item = (uint)item });
            }
        }
    }

    #endregion

    #region Packets out

    /// <summary>
    /// Every kart's progress, to everyone, every tick (live: all of them every 50 ms).
    /// </summary>
    private void SendProgress(long now)
    {
        var standings = _simulation.Standings();
        var top = standings.Take(4).Select(kart => kart.Guid).ToArray();

        for (var place = 0; place < standings.Count; place++)
        {
            var kart = standings[place];
            var gap = place == 0 ? 0f : MathF.Max(0f, _simulation.Track.RaceDistance(standings[place - 1].Progress) - _simulation.Track.RaceDistance(kart.Progress));

            SendToPlaying(GetProgress(kart, place, gap, top, now));
        }
    }

    private VehicleRacePacketUpdateVehicleRaceProgressData GetProgress(RaceKart kart, int place, float gap, ulong[] top, long now)
    {
        var progress = kart.Progress;
        var track = _simulation.Track;

        var distanceToFinish = !_simulation.Started ? float.MaxValue
            : progress.Finished ? 0f
            : MathF.Max(0f, _definition.Laps * track.LapLength - track.RaceDistance(progress));

        var lapTime = !_simulation.Started ? 0f
            : ((progress.Finished ? progress.FinishedAt : now) - progress.LapStartedAt) / 1000f;

        return new VehicleRacePacketUpdateVehicleRaceProgressData
        {
            Guid = kart.Guid,
            Place = (uint)place,
            Gap = gap,
            DistanceToFinish = distanceToFinish,
            Laps = (uint)Math.Min(progress.Laps, _definition.Laps),
            Finished = progress.Finished,
            TotalLaps = (uint)_definition.Laps,
            LapTime = lapTime,
            LastLapTime = progress.LastLap,
            BestLapTime = progress.BestLap,
            First = top.ElementAtOrDefault(0),
            Second = top.ElementAtOrDefault(1),
            Third = top.ElementAtOrDefault(2),
            Fourth = top.ElementAtOrDefault(3)
        };
    }

    /// <summary>
    /// The AI karts move as live moved them: by their drivers' positions.
    /// </summary>
    private void SendAiPositions(long now)
    {
        foreach (var kart in Karts)
        {
            if (kart.Driver is not { } driver || now - kart.PositionSentAt < PositionIntervalMs)
                continue;

            kart.PositionSentAt = now;
            driver.UpdatePosition(new Vector4(kart.Position, 1f), QuaternionExtensions.FromHeading(kart.Heading), moving: kart.Speed > 0.5f);
        }
    }

    private static Matrix4x4 Matrix(float[] values) => new(
        values[0], values[1], values[2], values[3],
        values[4], values[5], values[6], values[7],
        values[8], values[9], values[10], values[11],
        values[12], values[13], values[14], values[15]);

    #endregion

    public override void OnLeft(ArenaParticipant participant)
    {
        if (!_simulation.TryGetKart(participant.Guid, out var kart))
            return;

        _simulation.Remove(kart);
        _registered.Remove(participant.Guid);

        // The others' race order shrinks.
        foreach (var other in Playing.Where(other => other.Guid != participant.Guid))
            other.Player.SendTunneled(new VehicleRacePacketKickProxiedVehicleRace { Guid = kart.Guid });
    }

    public override void OnClosed()
    {
        foreach (var kart in Karts.ToList())
        {
            kart.Driver?.Dispose();
            _simulation.Remove(kart);
        }
    }
}
