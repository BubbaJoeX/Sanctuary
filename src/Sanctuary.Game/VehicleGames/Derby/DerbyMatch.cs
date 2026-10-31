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

namespace Sanctuary.Game.VehicleGames.Derby;

/// <summary>
/// A Wildwood demolition derby (the live match's setup): the player's car against seven AI cars for four minutes,
/// most points wins. The client simulates only its own car; the server (<see cref="DerbySimulation"/>) drives the AI
/// cars on the arena floor, works out every crash, and owns health, score, wrecks and respawns, the power-up boxes
/// and the announcer.
/// <para>
/// The client's derby processor (switched on by character state 0x800) registers, then acts on each game state in
/// turn and acknowledges it: 3 waits for the players' cars, 4 for the AI cars, 5 loads them, 6 starts its car's
/// state stream, 8 runs the countdown (each number echoed back), 9 is GO, 10 shows the results and 11 the Exit.
/// </para>
/// </summary>
public sealed class DerbyMatch : ArenaMatch
{
    public const int DerbyCharacterState = 0x800;

    private const int SetupWaitMs = 10_000;
    private const int StateRefreshMs = 1000;
    private const int PositionIntervalMs = 250;
    private const int ResultsWaitMs = 5000;

    private readonly DerbyArenaDefinition _arena;
    private readonly Random _random = new();
    private readonly DerbySimulation _simulation;

    private readonly Dictionary<ulong, ulong> _registered = [];
    private readonly HashSet<ulong> _acks = [];
    private readonly HashSet<ulong> _echoes = [];

    private int _gameState;
    private long _gameStateAt;
    private long _firstRegisteredAt;

    private int _countdown = -1;
    private long _countdownAt;
    private long _countdownEchoedAt;

    private long _endsAt;

    public DerbyMatch(int id, VehicleGameStageDefinition stage, WorldZone zone, VehicleGameManager manager, ILogger logger)
        : base(id, stage, zone, manager, logger)
    {
        _arena = manager.ResourceManager.VehicleGames.Definition.Derby;

        if (!manager.ResourceManager.VehicleGames.TryGetVehicle(_arena.PlayerVehicle, out _))
            throw new InvalidOperationException($"No vehicle {_arena.PlayerVehicle} for the derby.");


        _simulation = new DerbySimulation(_arena, zone.TryGetGroundHeight, _random)
        {
            Crashed = OnCrashed,
            Wrecked = OnWrecked,
            Respawned = OnRespawned,
            PickupTaken = OnPickupTaken,
            PickupSpawned = OnPickupSpawned,
            ItemExpired = OnItemExpired,
            NewLeader = leader => SendToPlaying(new VehicleDemolitionDerbyPacketAnnouncerMessage { Message = 1, Guid = leader.Guid })
        };
    }

    public override int CharacterStateBit => DerbyCharacterState;

    private static long Now => Environment.TickCount64;

    private IReadOnlyList<DerbyCar> Cars => _simulation.Cars;

    #region Packets in

    public override void OnPacket(ArenaParticipant participant, short opCode, ReadOnlySpan<byte> data)
    {
        if (opCode != BaseVehicleDemolitionDerbyPacket.OpCode || data.Length < 4)
            return;

        switch (BinaryPrimitives.ReadUInt16LittleEndian(data[2..]))
        {
            case VehicleDemolitionDerbyPacketRegisterVehicleDemolitionDerbyPlayer.OpCode
                when VehicleDemolitionDerbyPacketRegisterVehicleDemolitionDerbyPlayer.TryDeserialize(data, out var register):
                if (_gameState == 0 && _registered.TryAdd(participant.Guid, register.Clock) && _firstRegisteredAt == 0)
                    _firstRegisteredAt = Now;
                break;

            case VehicleDemolitionDerbyPacketUpdateVehicleGamePlayerState.OpCode
                when VehicleDemolitionDerbyPacketUpdateVehicleGamePlayerState.TryDeserialize(data, out var ack):
                if (ack.State == _gameState)
                    _acks.Add(participant.Guid);
                break;

            case VehicleDemolitionDerbyPacketVehicleDemolitionDerbyCountdownStatus.OpCode
                when VehicleDemolitionDerbyPacketVehicleDemolitionDerbyCountdownStatus.TryDeserialize(data, out var echo):
                if (echo.Count == _countdown && _echoes.Add(participant.Guid) && AllIn(_echoes))
                    _countdownEchoedAt = Now;
                break;

            case VehicleDemolitionDerbyPacketUpdateVehicleDemolitionDerbyData.OpCode
                when VehicleDemolitionDerbyPacketUpdateVehicleDemolitionDerbyData.TryDeserialize(data, out var state):
                OnCarState(participant, state);
                break;

            // The player's client felt its car touch another: a crash, if they were closing fast enough.
            case VehicleDemolitionDerbyPacketVehicleHit.OpCode when VehicleDemolitionDerbyPacketVehicleHit.TryDeserialize(data, out var hit):
                if (_gameState == 9 && hit.Guid == participant.Guid
                    && _simulation.TryGetCar(hit.Guid, out var car) && _simulation.TryGetCar(hit.OtherGuid, out var other))
                    _simulation.TryCrash(car, other, Now);
                break;

            // Asked every frame until answered; only a box in reach of the car answers.
            case VehicleDemolitionDerbyPacketPickupHitRequest.OpCode
                when VehicleDemolitionDerbyPacketPickupHitRequest.TryDeserialize(data, out var pickupHit):
                if (_gameState == 9 && _simulation.TryGetCar(participant.Guid, out var touching))
                    _simulation.TryTakePickup(touching, new Vector3(pickupHit.Position.X, pickupHit.Position.Y, pickupHit.Position.Z), 4f, Now);
                break;
        }
    }

    /// <summary>
    /// The player's own car, every frame: where it is (for crashes, the AI and the power-ups), and on to any other
    /// players.
    /// </summary>
    private void OnCarState(ArenaParticipant participant, VehicleDemolitionDerbyPacketUpdateVehicleDemolitionDerbyData state)
    {
        if (state.Guid != participant.Guid || !_simulation.TryGetCar(participant.Guid, out var car))
            return;

        if (car.LastState is { } last && state.Clock <= last.Clock)
            return;

        car.LastState = state;
        car.Position = state.Transform.Translation;
        car.Velocity = new Vector3(state.LinearVelocity.X, state.LinearVelocity.Y, state.LinearVelocity.Z);
        car.Heading = ArenaRules.Heading(new Vector3(state.Transform.M31, 0f, state.Transform.M33));

        SendToPlayingUnreliable(state, except: participant.Guid);
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
                    SpawnAiCars(now);
                    SendToPlaying(new VehicleDemolitionDerbyPacketUpdateNpcCount { Count = (uint)Cars.Count(car => !car.IsPlayer) });
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
                _simulation.Update(now, deltaSeconds);
                SendAiStates(now);

                if (now >= _endsAt)
                    End(now);
                break;

            case 10:
                SendAiStates(now);

                if (Acknowledged(now, ResultsWaitMs))
                {
                    Finish();
                    SetGameState(11, now);
                }
                break;
        }

        SendStateData(now);
    }

    /// <summary>
    /// Everyone registered: the derby's config and the players' cars, then game state 3.
    /// </summary>
    private void SetUp(long now)
    {
        SendToPlaying(new VehicleDemolitionDerbyPacketSetDerbyConfig { TimeLimitSeconds = _arena.TimeLimitSeconds, CrownModelId = (uint)_arena.CrownModelId });

        // The first player takes the live player's spot; any others take AI cars' spots.
        var spawns = new List<VehicleSpawnDefinition> { _arena.PlayerSpawn };
        spawns.AddRange(_arena.Drivers.Select(driver => driver.Spawn));

        var index = 0;

        foreach (var participant in Playing.Where(participant => _registered.ContainsKey(participant.Guid)).ToList())
        {
            if (index >= spawns.Count)
                break;

            var spawn = spawns[index++];

            var vehicle = Manager.PlayerVehicle(participant.Player, Garage.VehicleLoadouts.DemolitionDerby);

            _simulation.Add(new DerbyCar(participant.Guid, vehicle, spawn.Point, spawn.Heading) { Participant = participant });

            // The server's idea of where they are, so the AI drivers spawn in their view.
            participant.Player.UpdatePosition(new Vector4(spawn.Point, 1f), QuaternionExtensions.FromHeading(spawn.Heading));
        }

        foreach (var car in Cars)
            SendCar(car, now);

        SendToPlaying(new VehicleDemolitionDerbyPacketUpdatePlayerCount { Count = (uint)Cars.Count });
        SetGameState(3, now);

        Logger.LogInformation("Derby {encounter:X}: {players} player(s) registered.", Id, Cars.Count);
    }

    /// <summary>
    /// The AI cars: each driver (an NPC with the car's guid) first, then the car, as live.
    /// </summary>
    private void SpawnAiCars(long now)
    {
        // Spots taken by players' cars past the first.
        var taken = Cars.Count - 1;

        foreach (var driver in _arena.Drivers.Skip(taken))
        {
            if (!Manager.ResourceManager.Npcs.TryGetValue(driver.NpcId, out var npcDefinition)
                || !Manager.ResourceManager.VehicleGames.TryGetVehicle(driver.Vehicle, out var vehicle))
            {
                Logger.LogError("Derby {encounter:X}: no driver {npc} or vehicle {vehicle}; that car is left out.", Id, driver.NpcId, driver.Vehicle);
                continue;
            }

            var spawn = driver.Spawn;

            if (!Zone.TrySpawnArenaNpc(npcDefinition, new Vector4(spawn.Point, 1f), spawn.Heading, DerbyCharacterState | 1, out var npc))
                continue;

            var car = new DerbyCar(npc.Guid, vehicle, spawn.Point, spawn.Heading)
            {
                Driver = npc,
                Pace = 0.85f + (float)_random.NextDouble() * 0.2f
            };

            _simulation.Add(car);

            SendCar(car, now);
        }
    }

    private void SendCar(DerbyCar car, long now)
    {
        foreach (var participant in Playing)
        {
            participant.Player.SendTunneled(new VehicleDemolitionDerbyPacketCreateProxiedVehicleDemolitionDerby
            {
                Guid = car.Guid,
                Name = car.IsPlayer ? " " : string.Empty,
                Config = car.Config,
                Spawn = ArenaRules.Transform(car.SpawnPosition, ArenaRules.Forward(car.SpawnHeading), Vector3.UnitY),
                IsPlayer = car.IsPlayer,
                Unknown = _arena.TimeLimitSeconds
            });

            // Its state at the spawn: a player's own car carries its client's clock, the rest the server's.
            var clock = car.Participant is { } owner && _registered.TryGetValue(owner.Guid, out var registered) ? registered : (ulong)now;

            participant.Player.SendTunneled(GetCarState(car, clock));
            participant.Player.SendTunneled(car.GetStateData());
        }

        car.StateSentAt = now;
        car.Dirty = false;
    }

    private void SetGameState(int state, long now)
    {
        _gameState = state;
        _gameStateAt = now;
        _acks.Clear();

        SendToPlaying(new VehicleDemolitionDerbyPacketUpdateVehicleGameState { State = (uint)state });
    }

    /// <summary>
    /// Every player with a car has acknowledged the game state (or it's been too long).
    /// </summary>
    private bool Acknowledged(long now, int timeoutMs = AckTimeoutMs) => AllIn(_acks) || now - _gameStateAt > timeoutMs;

    private bool AllIn(HashSet<ulong> answered) =>
        Playing.Where(participant => _simulation.TryGetCar(participant.Guid, out _)).All(participant => answered.Contains(participant.Guid));

    private void SendCountdown(int count, long now)
    {
        _countdown = count;
        _countdownAt = now;
        _countdownEchoedAt = 0;
        _echoes.Clear();

        SendToPlaying(new VehicleDemolitionDerbyPacketVehicleDemolitionDerbyCountdownStatus { Count = count });
    }

    /// <summary>
    /// Each number goes a moment after the clients echoed the last (live never saw an echo of the 4).
    /// </summary>
    private void UpdateCountdown(long now)
    {
        var due = _countdown == 4
            ? now - _countdownAt >= 350
            : (_countdownEchoedAt != 0 && now - _countdownEchoedAt >= 300) || now - _countdownAt >= 1500;

        if (!due)
            return;

        if (_countdown > 0)
        {
            SendCountdown(_countdown - 1, now);
            return;
        }

        _countdown = -1;
        _endsAt = now + (long)(_arena.TimeLimitSeconds * 1000f);

        SetGameState(9, now);

        _simulation.PlacePickups();

        foreach (var car in Cars)
            car.Dirty = true;

        Logger.LogInformation("Derby {encounter:X}: GO with {cars} cars.", Id, Cars.Count);
    }

    /// <summary>
    /// Time's up: the results (GameState 10), then once they're up the payout and the Exit (11).
    /// </summary>
    private void End(long now)
    {
        var standings = DerbyRules.Standings(Cars);

        for (var place = 0; place < standings.Count; place++)
        {
            if (standings[place].Participant is not { } participant)
                continue;

            participant.Place = place;
            participant.Won = place == 0;
        }

        SetGameState(10, now);

        Logger.LogInformation("Derby {encounter:X} over: {standings}.", Id,
            string.Join(", ", standings.Select(car => $"{(car.IsPlayer ? car.Participant!.Player.Name.FullName : car.Driver?.Name ?? car.Guid.ToString("X"))} {car.Score}")));
    }

    #endregion

    #region What happens

    private void OnCrashed(DerbyCar a, DerbyCar b, float impact)
    {
        // The client plays the impact on both cars. A player's client feels its own crashes already.
        if (!a.IsPlayer && !b.IsPlayer)
            SendToPlaying(new VehicleDemolitionDerbyPacketVehicleHit { Guid = a.Guid, OtherGuid = b.Guid, Impulse = impact * 400f });
    }

    private void OnWrecked(DerbyCar victim, DerbyCar? attacker)
    {
        SendToPlaying(new VehicleDemolitionDerbyPacketAnnouncerMessage { Message = 2, Guid = victim.Guid, OtherGuid = attacker?.Guid ?? 0 });

        // The minigame HUD's knock outs, as live.
        victim.Participant?.Player.SendTunneled(new MiniGameKnockOutPacket { Count = victim.TimesWrecked + 1 });
    }

    private void OnRespawned(DerbyCar car)
    {
        SendToPlaying(new VehicleDemolitionDerbyPacketResetProxiedVehicleDemolitionDerby { Guid = car.Guid });

        car.Driver?.UpdatePosition(new Vector4(car.Position, 1f), QuaternionExtensions.FromHeading(car.Heading));
    }

    private void OnPickupTaken(DerbyCar car, DerbyPickup pickup, DerbyItem item, float boost)
    {
        SendToPlaying(new VehicleDemolitionDerbyPacketDestroyProxiedVehicleDemolitionDerbyPickup { PickupId = (uint)pickup.Id });

        car.Participant?.Player.SendTunneled(new VehicleDemolitionDerbyPacketPickupHitResponse { Guid = car.Guid, Item = (uint)item, A = boost });

        if (item >= DerbyItem.Shield)
            SendToPlaying(new VehicleDemolitionDerbyPacketAnnouncerMessage { Message = 0, Item = (uint)item, Guid = car.Guid });
    }

    private void OnPickupSpawned(DerbyPickup pickup) => SendToPlaying(new VehicleDemolitionDerbyPacketCreateProxiedVehicleDemolitionDerbyPickup
    {
        PickupId = (uint)pickup.Id,
        ModelId = (uint)_arena.PickupModelId,
        Position = new Vector4(pickup.Position, 1f),
        Unknown = 1f
    });

    private static void OnItemExpired(DerbyCar car, DerbyItem item) =>
        car.Participant?.Player.SendTunneled(new VehicleDemolitionDerbyPacketItemExpired { Item = (uint)item });

    #endregion

    #region Packets out

    /// <summary>
    /// The AI cars' physics state for the clients, every tick (live 20 Hz), and their drivers' positions.
    /// </summary>
    private void SendAiStates(long now)
    {
        foreach (var car in Cars)
        {
            if (car.IsPlayer)
                continue;

            SendToPlayingUnreliable(GetCarState(car, (ulong)now));

            if (car.Driver is { } driver && now - car.PositionSentAt >= PositionIntervalMs)
            {
                car.PositionSentAt = now;
                driver.UpdatePosition(new Vector4(car.Position, 1f), QuaternionExtensions.FromHeading(car.Heading), moving: MathF.Abs(car.Speed) > 0.5f);
            }
        }
    }

    private void SendStateData(long now)
    {
        if (_gameState < 3)
            return;

        foreach (var car in Cars)
        {
            if (!car.Dirty && now - car.StateSentAt < StateRefreshMs)
                continue;

            car.Dirty = false;
            car.StateSentAt = now;

            SendToPlaying(car.GetStateData());
        }
    }

    private static VehicleDemolitionDerbyPacketUpdateVehicleDemolitionDerbyData GetCarState(DerbyCar car, ulong clock) => new()
    {
        Guid = car.Guid,
        Transform = ArenaRules.Transform(car.Position, car.Forward, car.Up),
        LinearVelocity = new Vector4(car.Velocity, 1f),
        AngularVelocity = new Vector4(0f, car.YawRate, 0f, 1f),
        Throttle = car.Throttle,
        Brake = car.Brake,
        Steering = car.Steering,
        Unknown4 = 1,
        Clock = clock
    };

    #endregion

    public override void OnLeft(ArenaParticipant participant)
    {
        if (!_simulation.TryGetCar(participant.Guid, out var car))
            return;

        _simulation.Remove(car);
        _registered.Remove(participant.Guid);

        // The others' race order shrinks.
        foreach (var other in Playing.Where(other => other.Guid != participant.Guid))
            other.Player.SendTunneled(new VehicleDemolitionDerbyPacketKickProxiedVehicleDemolitionDerby { Guid = car.Guid });
    }

    public override void OnClosed()
    {
        foreach (var car in Cars.ToList())
        {
            car.Driver?.Dispose();
            _simulation.Remove(car);
        }
    }
}
