using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using Sanctuary.Game.Resources.Definitions;

namespace Sanctuary.Game.VehicleGames.Derby;

/// <summary>
/// The derby once it's under way, with no zone or packets: the AI cars driving on the arena floor, crashes between
/// cars, wrecks and respawns, the power-up boxes and the lead. Players' cars move by their clients' states; what
/// happens is told through the callbacks, which <see cref="DerbyMatch"/> turns into packets.
/// </summary>
public sealed class DerbySimulation
{
    public delegate bool GroundHeight(float x, float z, float fromY, float maxDrop, out float height);

    // Cars this close (centre to centre, m) are touching.
    public const float ContactDistance = 3.6f;

    public const float TopSpeed = 17f;
    private const float ReverseSpeed = 7f;
    private const float TurnRate = 1.9f;

    // The most the floor can rise under a car in one step (a ramp); more is a wall.
    private const float MaxClimb = 1.5f;

    private const int PickupRespawnMs = 12_000;

    private readonly DerbyArenaDefinition _arena;
    private readonly GroundHeight _groundHeight;
    private readonly Random _random;

    private readonly List<DerbyCar> _cars = [];
    private readonly Dictionary<ulong, DerbyCar> _carsByGuid = [];
    private readonly Dictionary<(ulong, ulong), long> _lastCrash = [];
    private readonly List<DerbyPickup> _pickups = [];

    private ulong _leaderGuid;

    public DerbySimulation(DerbyArenaDefinition arena, GroundHeight groundHeight, Random random)
    {
        _arena = arena;
        _groundHeight = groundHeight;
        _random = random;
    }

    public IReadOnlyList<DerbyCar> Cars => _cars;

    public IReadOnlyList<DerbyPickup> Pickups => _pickups;

    /// <summary>
    /// Two cars crashed: (a, b, how hard, in m/s of closing speed).
    /// </summary>
    public Action<DerbyCar, DerbyCar, float>? Crashed { get; set; }

    /// <summary>
    /// A car was wrecked: (the car, who wrecked it).
    /// </summary>
    public Action<DerbyCar, DerbyCar?>? Wrecked { get; set; }

    /// <summary>
    /// A wrecked car is back at its spawn, repaired.
    /// </summary>
    public Action<DerbyCar>? Respawned { get; set; }

    /// <summary>
    /// A car took a power-up box: (the car, the box, what it got, the boost).
    /// </summary>
    public Action<DerbyCar, DerbyPickup, DerbyItem, float>? PickupTaken { get; set; }

    public Action<DerbyPickup>? PickupSpawned { get; set; }

    /// <summary>
    /// A timed power-up ran out on a car.
    /// </summary>
    public Action<DerbyCar, DerbyItem>? ItemExpired { get; set; }

    /// <summary>
    /// A car took the lead on its own.
    /// </summary>
    public Action<DerbyCar>? NewLeader { get; set; }

    public bool TryGetCar(ulong guid, out DerbyCar car) => _carsByGuid.TryGetValue(guid, out car!);

    public void Add(DerbyCar car)
    {
        _cars.Add(car);
        _carsByGuid[car.Guid] = car;
    }

    public void Remove(DerbyCar car)
    {
        _cars.Remove(car);
        _carsByGuid.Remove(car.Guid);
    }

    /// <summary>
    /// The power-up boxes, on the floor, at GO.
    /// </summary>
    public void PlacePickups()
    {
        foreach (var definition in _arena.Pickups)
        {
            var position = definition.Point;

            if (_groundHeight(position.X, position.Z, position.Y + 5f, 30f, out var ground))
                position.Y = ground + 0.8f;

            var pickup = new DerbyPickup(definition.Id, position);

            _pickups.Add(pickup);
            Spawn(pickup);
        }
    }

    public void Update(long now, float deltaSeconds)
    {
        foreach (var car in _cars)
        {
            if (car.IsWrecked)
            {
                if (car.WreckedAt != 0 && now - car.WreckedAt >= DerbyRules.ResetDelayMs)
                    Respawn(car);
                else if (!car.IsPlayer)
                    Coast(car, deltaSeconds, now);

                continue;
            }

            if (!car.IsPlayer)
                Drive(car, now, deltaSeconds);

            ExpirePowerUps(car, now);

            if (car.IsPlayer)
                continue;

            foreach (var pickup in _pickups)
            {
                if (pickup.Active && Flat(car.Position - pickup.Position).Length() < 2.5f)
                    Take(car, pickup, now);
            }
        }

        CrashAiCars(now);

        foreach (var pickup in _pickups)
        {
            if (!pickup.Active && now >= pickup.RespawnAt)
                Spawn(pickup);
        }

        var leader = DerbyRules.Leader(_cars);

        // Live named the leader again after a tie broke the same way.
        if (leader is null)
        {
            _leaderGuid = 0;
        }
        else if (leader.Guid != _leaderGuid)
        {
            _leaderGuid = leader.Guid;
            NewLeader?.Invoke(leader);
        }
    }

    #region Driving

    /// <summary>
    /// An AI car: now and then picks a car to ram (the nearest, a player more often) or roams (to a box nearby, or
    /// anywhere), heads for it, backs off and cools down after a crash, and keeps to the arena floor.
    /// </summary>
    private void Drive(DerbyCar car, long now, float deltaSeconds)
    {
        DerbyCar? target = null;

        var lostTarget = car.TargetGuid != 0 && (!_carsByGuid.TryGetValue(car.TargetGuid, out target) || target.IsWrecked);
        var arrived = car.TargetGuid == 0 && Flat(car.RoamTo - car.Position).Length() < 6f;

        if (now >= car.RetargetAt || lostTarget || arrived)
            Choose(car, now, out target);

        Vector3 aim;

        if (ArenaRadius(car.Position) > 0.85f)
            aim = Centre;
        else if (target is not null)
            aim = target.Position + target.Velocity * Math.Min(1f, Flat(target.Position - car.Position).Length() / TopSpeed);
        else
            aim = car.RoamTo;

        var angle = ArenaRules.AngleBetween(car.Heading, ArenaRules.Heading(Flat(aim - car.Position)));

        float targetSpeed;

        if (now < car.ReverseUntil)
        {
            targetSpeed = -ReverseSpeed;
            car.Steering = car.ReverseSteering;
        }
        else
        {
            car.Steering = Math.Clamp(angle / 0.5f, -1f, 1f);
            targetSpeed = TopSpeed * car.Pace * (MathF.Abs(angle) > 1.4f ? 0.45f : 1f);
        }

        var acceleration = targetSpeed > car.Speed ? 8f : 12f;

        car.Speed = MoveTowards(car.Speed, targetSpeed, acceleration * deltaSeconds);
        car.Throttle = targetSpeed > 0f ? 1f : 0f;
        car.Brake = targetSpeed < 0f ? 1f : 0f;

        car.YawRate = car.Steering * TurnRate * Math.Clamp(MathF.Abs(car.Speed) / 5f, 0f, 1f) * MathF.Sign(car.Speed);
        car.Heading += car.YawRate * deltaSeconds;

        Move(car, car.Forward * car.Speed + car.Knock, deltaSeconds, now);

        car.Knock *= MathF.Exp(-3f * deltaSeconds);
    }

    private void Coast(DerbyCar car, float deltaSeconds, long now)
    {
        car.Speed = MoveTowards(car.Speed, 0f, 10f * deltaSeconds);
        car.Throttle = 0f;
        car.Steering = 0f;
        car.YawRate = 0f;

        Move(car, car.Forward * car.Speed + car.Knock, deltaSeconds, now);

        car.Knock *= MathF.Exp(-3f * deltaSeconds);
    }

    /// <summary>
    /// Moves an AI car, kept inside the arena and on its floor (tilted with it).
    /// </summary>
    private void Move(DerbyCar car, Vector3 velocity, float deltaSeconds, long now)
    {
        var position = car.Position + Flat(velocity) * deltaSeconds;

        // The arena's walls: back inside, losing the speed that went into them.
        var radius = ArenaRadius(position);

        if (radius > 1f)
        {
            var centre = new Vector3(_arena.Centre[0], position.Y, _arena.Centre[2]);
            position = centre + (position - centre) / radius;
            car.Speed *= 0.5f;
            car.Knock = Vector3.Zero;
        }

        var previousY = car.Position.Y;

        // Anything rising more than a step ahead is a wall (a ramp's side, a pillar): the car stops against it and
        // backs off.
        if (_groundHeight(position.X, position.Z, previousY + MaxClimb + 2f, 2f, out var obstacle) && obstacle > previousY + MaxClimb)
        {
            car.Speed *= -0.3f;
            car.Knock = Vector3.Zero;
            car.Velocity = Vector3.Zero;
            car.ReverseUntil = Math.Max(car.ReverseUntil, now + 700);
            car.ReverseSteering = _random.Next(2) == 0 ? -1f : 1f;
            car.TargetGuid = 0;
            car.RoamTo = RandomPoint();
            car.RetargetAt = now + 3000;
            return;
        }

        if (_groundHeight(position.X, position.Z, previousY + MaxClimb, 30f, out var ground))
        {
            position.Y = ground + DerbyRules.RideHeight;
            car.Up = GroundNormal(position, car.Forward, ground);
        }

        car.Velocity = new Vector3(velocity.X, deltaSeconds > 0f ? (position.Y - previousY) / deltaSeconds : 0f, velocity.Z);
        car.Position = position;
    }

    private Vector3 Settle(Vector3 position)
    {
        if (_groundHeight(position.X, position.Z, position.Y + MaxClimb, 30f, out var ground))
            position.Y = ground + DerbyRules.RideHeight;

        return position;
    }

    private Vector3 GroundNormal(Vector3 position, Vector3 forward, float ground)
    {
        var right = new Vector3(forward.Z, 0f, -forward.X);

        float Height(Vector3 offset) =>
            _groundHeight(position.X + offset.X, position.Z + offset.Z, ground + 3f, 6f, out var height) ? height : ground;

        var alongForward = forward * 3.6f + new Vector3(0f, Height(forward * 1.8f) - Height(-forward * 1.8f), 0f);
        var alongRight = right * 2.4f + new Vector3(0f, Height(right * 1.2f) - Height(-right * 1.2f), 0f);

        var up = Vector3.Cross(alongForward, alongRight);

        return up.Y > 0.5f ? Vector3.Normalize(up) : Vector3.UnitY;
    }

    /// <summary>
    /// What an AI car goes for next: mostly a car to ram, otherwise a box nearby (more so when it's hurt) or
    /// somewhere else on the floor.
    /// </summary>
    private void Choose(DerbyCar car, long now, out DerbyCar? target)
    {
        target = null;
        car.TargetGuid = 0;

        var box = _pickups.Where(pickup => pickup.Active && Flat(pickup.Position - car.Position).Length() < 70f)
            .MinBy(pickup => Flat(pickup.Position - car.Position).Length());

        var roll = _random.NextDouble();

        if (box is not null && roll < (car.Health < 50 ? 0.6 : 0.3))
        {
            car.RoamTo = box.Position;
            car.RetargetAt = now + 6000;
            return;
        }

        if (roll < 0.75)
        {
            target = PickTarget(car);

            if (target is not null)
            {
                car.TargetGuid = target.Guid;
                car.RetargetAt = now + 4000 + _random.Next(4000);
                return;
            }
        }

        car.RoamTo = RandomPoint();
        car.RetargetAt = now + 3000 + _random.Next(3000);
    }

    /// <summary>
    /// Somewhere on the arena floor, clear of the walls.
    /// </summary>
    private Vector3 RandomPoint()
    {
        var angle = (float)_random.NextDouble() * MathF.Tau;
        var radius = 0.2f + (float)_random.NextDouble() * 0.5f;

        return Centre + new Vector3(MathF.Sin(angle) * _arena.Radii[0] * radius, 0f, MathF.Cos(angle) * _arena.Radii[1] * radius);
    }

    private DerbyCar? PickTarget(DerbyCar car)
    {
        DerbyCar? best = null;
        var bestScore = float.MaxValue;

        foreach (var other in _cars)
        {
            if (other == car || other.IsWrecked)
                continue;

            // Players draw a little more attention; a bit of chance keeps the AI from all piling onto one car.
            var score = Flat(other.Position - car.Position).Length() * (other.IsPlayer ? 0.7f : 1f) * (0.75f + (float)_random.NextDouble() * 0.5f);

            if (score < bestScore)
            {
                best = other;
                bestScore = score;
            }
        }

        return best;
    }

    private Vector3 Centre => new(_arena.Centre[0], _arena.Centre[1], _arena.Centre[2]);

    /// <summary>
    /// How far out a point is: 0 at the arena's middle, 1 at its walls. The arena is a rounded rectangle (a
    /// superellipse of power 4), which holds everywhere the live cars drove.
    /// </summary>
    public float ArenaRadius(Vector3 position)
    {
        var dx = (position.X - _arena.Centre[0]) / _arena.Radii[0];
        var dz = (position.Z - _arena.Centre[2]) / _arena.Radii[1];

        return MathF.Pow(dx * dx * dx * dx + dz * dz * dz * dz, 0.25f);
    }

    #endregion

    #region Crashes

    /// <summary>
    /// AI cars crash into each other here; a player's car's crashes come from its client.
    /// </summary>
    private void CrashAiCars(long now)
    {
        for (var i = 0; i < _cars.Count; i++)
        {
            var a = _cars[i];

            if (a.IsPlayer || a.IsWrecked)
                continue;

            for (var j = i + 1; j < _cars.Count; j++)
            {
                var b = _cars[j];

                if (b.IsPlayer || b.IsWrecked)
                    continue;

                var between = Flat(b.Position - a.Position);
                var distance = between.Length();

                if (distance >= ContactDistance || distance < 1e-3f)
                    continue;

                TryCrash(a, b, now);

                // Apart again, half each, and back on the floor.
                var push = between / distance * ((ContactDistance - distance) / 2f);
                a.Position = Settle(a.Position - push);
                b.Position = Settle(b.Position + push);
            }
        }
    }

    /// <summary>
    /// A crash between two cars, if they were closing fast enough and haven't just crashed. Both players' clients
    /// report a crash between them, so a pair counts once.
    /// </summary>
    public bool TryCrash(DerbyCar a, DerbyCar b, long now)
    {
        if (a == b || a.IsWrecked || b.IsWrecked)
            return false;

        var key = a.Guid < b.Guid ? (a.Guid, b.Guid) : (b.Guid, a.Guid);

        if (_lastCrash.TryGetValue(key, out var last) && now - last < DerbyRules.CrashCooldownMs)
            return false;

        if (DerbyRules.Closing(a.Position, a.Velocity, b.Position, b.Velocity) is not { } closing)
            return false;

        _lastCrash[key] = now;

        var leader = DerbyRules.Leader(_cars);
        var (byA, byB) = DerbyRules.CrashDamage(closing.SpeedA, closing.SpeedB);

        DerbyRules.Hit(a, b, byA, now);
        DerbyRules.Hit(b, a, byB, now);

        // The AI cars bounce off and back away before going again.
        var normal = Vector3.Normalize(Flat(b.Position - a.Position));
        var impact = closing.SpeedA + closing.SpeedB;

        Bounce(a, -normal, impact, attacked: byA > 0, now);
        Bounce(b, normal, impact, attacked: byB > 0, now);

        Crashed?.Invoke(a, b, impact);

        if (b.IsWrecked)
            OnWrecked(b, leader, now);

        if (a.IsWrecked)
            OnWrecked(a, leader, now);

        return true;
    }

    private void Bounce(DerbyCar car, Vector3 away, float impact, bool attacked, long now)
    {
        if (car.IsPlayer)
            return;

        car.Knock = away * impact * 0.5f;
        car.Speed *= -0.25f;

        // Either side backs away a moment; the attacker then roams off a while before going again.
        car.ReverseUntil = now + 400 + _random.Next(600);
        car.ReverseSteering = _random.Next(2) == 0 ? -1f : 1f;

        if (attacked)
        {
            car.TargetGuid = 0;
            car.RoamTo = RandomPoint();
            car.RetargetAt = now + 3500 + _random.Next(3000);
        }
    }

    private void OnWrecked(DerbyCar victim, DerbyCar? leader, long now)
    {
        if (victim.WreckedAt != 0)
            return;

        _carsByGuid.TryGetValue(victim.LastAttacker, out var attacker);

        DerbyRules.Wreck(attacker, victim, leader == victim, now);

        Wrecked?.Invoke(victim, attacker);
    }

    private void Respawn(DerbyCar car)
    {
        DerbyRules.Respawn(car);

        car.TargetGuid = 0;
        car.LastState = null;

        Respawned?.Invoke(car);
    }

    #endregion

    #region Power-ups

    private void ExpirePowerUps(DerbyCar car, long now)
    {
        if (car.ShieldUntil != 0 && now >= car.ShieldUntil)
        {
            car.ShieldUntil = 0;
            ItemExpired?.Invoke(car, DerbyItem.Shield);
        }

        if (car.AttackUpUntil != 0 && now >= car.AttackUpUntil)
        {
            car.AttackUpUntil = 0;
            ItemExpired?.Invoke(car, DerbyItem.AttackUp);
        }

        if (car.UltimateUntil != 0 && now >= car.UltimateUntil)
        {
            car.UltimateUntil = 0;
            ItemExpired?.Invoke(car, DerbyItem.Ultimate);
        }
    }

    /// <summary>
    /// A player's car asks for the box it touched: the request carries no box, so it's the nearest one in reach.
    /// </summary>
    public bool TryTakePickup(DerbyCar car, Vector3 position, float reach, long now)
    {
        if (car.IsWrecked)
            return false;

        var pickup = _pickups.Where(pickup => pickup.Active && Flat(pickup.Position - position).Length() <= reach)
            .MinBy(pickup => Flat(pickup.Position - position).Length());

        if (pickup is null)
            return false;

        Take(car, pickup, now);

        return true;
    }

    private void Take(DerbyCar car, DerbyPickup pickup, long now)
    {
        pickup.Active = false;
        pickup.RespawnAt = now + PickupRespawnMs + _random.Next(6000);

        // A hurt car is more likely to find a repair.
        var item = car.Health < 50 && _random.Next(3) == 0 ? DerbyItem.Health : (DerbyItem)(1 + _random.Next(5));
        var boost = 0f;

        switch (item)
        {
            case DerbyItem.Health:
                car.Health = Math.Min(DerbyRules.MaxHealth, car.Health + DerbyRules.HealthPickup);
                car.Dirty = true;
                break;

            case DerbyItem.Boost:
                boost = 100f;

                if (!car.IsPlayer)
                    car.Speed = TopSpeed * 1.6f;
                break;

            case DerbyItem.Shield:
                car.ShieldUntil = now + (long)(DerbyRules.ShieldSeconds * 1000f);
                break;

            case DerbyItem.AttackUp:
                car.AttackUpUntil = now + (long)(DerbyRules.AttackUpSeconds * 1000f);
                break;

            case DerbyItem.Ultimate:
                car.UltimateUntil = now + (long)(DerbyRules.UltimateSeconds * 1000f);
                break;
        }

        PickupTaken?.Invoke(car, pickup, item, boost);
    }

    private void Spawn(DerbyPickup pickup)
    {
        pickup.Active = true;

        PickupSpawned?.Invoke(pickup);
    }

    #endregion

    public static Vector3 Flat(Vector3 vector) => new(vector.X, 0f, vector.Z);

    private static float MoveTowards(float value, float target, float step) =>
        value < target ? Math.Min(value + step, target) : Math.Max(value - step, target);
}

/// <summary>
/// What a derby power-up box gives (PickupHitResponse's item).
/// </summary>
public enum DerbyItem
{
    Health = 1,
    Boost = 2,
    Shield = 3,
    AttackUp = 4,
    Ultimate = 5
}

public sealed class DerbyPickup(int id, Vector3 position)
{
    public int Id { get; } = id;

    public Vector3 Position { get; } = position;

    public bool Active { get; set; }

    public long RespawnAt { get; set; }
}
