using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Sanctuary.Game.VehicleGames.Race;

/// <summary>
/// A kart race once it's under way, with no zone or packets: the AI karts driving their drivers' live lines at their
/// own pace (kept in touch with the players), everybody's laps, places and times, the item boxes, AI karts bumping
/// players, and karts that fell off the track. What happens is told through the callbacks, which
/// <see cref="RaceMatch"/> turns into packets.
/// </summary>
public sealed class RaceSimulation
{
    private const float Acceleration = 6f;
    private const float Braking = 10f;

    // An AI kart this close to a player's, and closing, bumps it.
    private const float BumpDistance = 2.4f;
    private const int BumpCooldownMs = 1000;

    private const int PickupRespawnMinMs = 5000;
    private const int PickupRespawnMaxMs = 8000;

    private readonly RaceTrack _track;
    private readonly Random _random;

    private readonly List<RaceKart> _karts = [];
    private readonly Dictionary<ulong, RaceKart> _kartsByGuid = [];
    private readonly List<RacePickup> _pickups;

    private int _finished;

    public RaceSimulation(RaceTrack track, int totalLaps, Random random)
    {
        _track = track;
        _random = random;

        TotalLaps = totalLaps;

        _pickups = track.Definition.Pickups.Select(pickup => new RacePickup(pickup.Id, pickup.Point)).ToList();
    }

    public RaceTrack Track => _track;

    public int TotalLaps { get; }

    public IReadOnlyList<RaceKart> Karts => _karts;

    public IReadOnlyList<RacePickup> Pickups => _pickups;

    public long StartedAt { get; private set; }

    public bool Started => StartedAt != 0;

    /// <summary>
    /// An AI kart bumped a player's: (the AI kart, the player's, the push).
    /// </summary>
    public Action<RaceKart, RaceKart, float>? Bumped { get; set; }

    /// <summary>
    /// A kart took an item box: (the kart, the box, the item a player's got).
    /// </summary>
    public Action<RaceKart, RacePickup, RaceItem?>? PickupTaken { get; set; }

    public Action<RacePickup>? PickupSpawned { get; set; }

    /// <summary>
    /// A kart crossed the finish for the last time.
    /// </summary>
    public Action<RaceKart>? Finished { get; set; }

    /// <summary>
    /// A player's kart fell off the track: it goes back on at its last gate (a transform).
    /// </summary>
    public Action<RaceKart, Matrix4x4>? Reset { get; set; }

    public bool TryGetKart(ulong guid, out RaceKart kart) => _kartsByGuid.TryGetValue(guid, out kart!);

    public void Add(RaceKart kart)
    {
        _karts.Add(kart);
        _kartsByGuid[kart.Guid] = kart;

        _track.Advance(kart.Progress, kart.Position, 0);
    }

    public void Remove(RaceKart kart)
    {
        _karts.Remove(kart);
        _kartsByGuid.Remove(kart.Guid);
    }

    /// <summary>
    /// GO: the clocks start and the item boxes go out.
    /// </summary>
    public void Start(long now)
    {
        StartedAt = now;

        foreach (var kart in _karts)
            kart.Progress.LapStartedAt = now;

        foreach (var pickup in _pickups)
            Spawn(pickup);
    }

    public void Update(long now, float deltaSeconds)
    {
        var leadingPlayer = _karts.Where(kart => kart.IsPlayer).Select(kart => (float?)_track.RaceDistance(kart.Progress)).Max();

        foreach (var kart in _karts)
        {
            if (!kart.IsPlayer)
                Drive(kart, now, deltaSeconds, leadingPlayer);

            if (kart.Progress.Finished)
                continue;

            _track.Advance(kart.Progress, kart.Position, now);

            if (kart.Progress.Laps >= TotalLaps)
                Finish(kart, now);
            else if (kart.IsPlayer)
                CheckOnTrack(kart, now);
        }

        AvoidAndBump(now);
        TakeAiPickups(now);

        foreach (var pickup in _pickups)
        {
            if (!pickup.Active && now >= pickup.RespawnAt)
                Spawn(pickup);
        }
    }

    /// <summary>
    /// The race order: finishers as they finished, then the rest by how far round they are.
    /// </summary>
    public List<RaceKart> Standings() =>
        _karts.OrderBy(kart => kart.Progress.Finished ? kart.Progress.FinishPlace : int.MaxValue)
            .ThenByDescending(kart => _track.RaceDistance(kart.Progress))
            .ToList();

    private void Finish(RaceKart kart, long now)
    {
        kart.Progress.Laps = TotalLaps;
        kart.Progress.Finished = true;
        kart.Progress.FinishedAt = now;
        kart.Progress.FinishPlace = _finished++;

        Finished?.Invoke(kart);
    }

    #region Driving

    /// <summary>
    /// An AI kart: along its driver's line at the speed live drove it, at its own pace, a little quicker when well
    /// behind the leading player and a little slower when well ahead, and slowed down after a bump.
    /// </summary>
    private void Drive(RaceKart kart, long now, float deltaSeconds, float? leadingPlayer)
    {
        if (kart.Line is not { } line || !Started)
            return;

        var path = kart.OnStartLane ? line.Start : line.Loop;

        var catchUp = 1f;

        if (leadingPlayer is { } player && !kart.Progress.Finished)
            catchUp = Math.Clamp(1f + (player - _track.RaceDistance(kart.Progress)) / 2500f, 0.85f, 1.08f);

        var target = path.SpeedAt(kart.LineDistance) * kart.Pace * catchUp;

        if (kart.Progress.Finished)
            target *= 0.6f;

        if (now < kart.BoostedUntil)
            target *= 1.3f;

        if (now < kart.SlowedUntil)
            target *= 0.7f;

        kart.Speed = kart.Speed < target
            ? Math.Min(target, kart.Speed + Acceleration * deltaSeconds)
            : Math.Max(target, kart.Speed - Braking * deltaSeconds);

        var distance = kart.LineDistance + kart.Speed * deltaSeconds;

        if (kart.OnStartLane && distance >= line.Start.Length)
        {
            kart.OnStartLane = false;
            distance = line.Entry + (distance - line.Start.Length);
            path = line.Loop;
        }

        kart.LineDistance = path.Wrap(distance);

        kart.Offset += Math.Clamp(kart.TargetOffset - kart.Offset, -1.5f * deltaSeconds, 1.5f * deltaSeconds);

        var direction = path.DirectionAt(kart.LineDistance);
        var right = Vector3.Normalize(new Vector3(direction.Z, 0f, -direction.X));

        kart.Position = path.PositionAt(kart.LineDistance) + right * kart.Offset;
        kart.Heading = ArenaRules.Heading(direction);
        kart.Velocity = direction * kart.Speed;
    }

    /// <summary>
    /// AI karts edge round a kart close in front of them, and one that runs into a player's kart bumps it.
    /// </summary>
    private void AvoidAndBump(long now)
    {
        foreach (var kart in _karts)
        {
            if (kart.IsPlayer || !Started)
                continue;

            var forward = ArenaRules.Forward(kart.Heading);
            var right = new Vector3(forward.Z, 0f, -forward.X);
            var avoid = 0f;

            foreach (var other in _karts)
            {
                if (other == kart)
                    continue;

                var between = other.Position - kart.Position;
                var ahead = Vector3.Dot(between, forward);
                var side = Vector3.Dot(between, right);

                if (ahead > 0f && ahead < 7f && MathF.Abs(side) < 2.5f && MathF.Abs(between.Y) < 3f)
                    avoid = side > 0f ? -2.5f : 2.5f;

                if (!other.IsPlayer || between.Length() > BumpDistance || now - other.LastBumpAt < BumpCooldownMs)
                    continue;

                var closing = Vector3.Dot(kart.Velocity - other.Velocity, Vector3.Normalize(between));

                if (closing < 2f)
                    continue;

                other.LastBumpAt = now;
                kart.SlowedUntil = now + 500;

                // Through a shield nothing pushes.
                if (other.Effects.TryGetValue(RaceItem.Shield, out var shieldUntil) && now < shieldUntil)
                    continue;

                Bumped?.Invoke(kart, other, 300f + 120f * closing);
            }

            kart.TargetOffset = avoid;
        }
    }

    /// <summary>
    /// A player's client says its kart ran into another: an AI kart is knocked back a little.
    /// </summary>
    public void OnReportedBump(RaceKart kart, RaceKart other, long now)
    {
        if (other.IsPlayer || now - kart.LastBumpAt < 300)
            return;

        kart.LastBumpAt = now;
        other.SlowedUntil = now + 700;
        other.TargetOffset = Vector3.Dot(other.Position - kart.Position, new Vector3(MathF.Cos(other.Heading), 0f, -MathF.Sin(other.Heading))) > 0f ? 2.5f : -2.5f;
    }

    /// <summary>
    /// A player's client says its kart went through a gate.
    /// </summary>
    public void OnReportedGate(RaceKart kart, Vector3 position, long now)
    {
        if (!Started || _track.GateAt(position) is not { } gate)
            return;

        _track.PassGate(kart.Progress, gate, now);
    }

    /// <summary>
    /// A player's kart well below the track where it's found (fallen off) for two seconds goes back on at its last
    /// gate.
    /// </summary>
    private void CheckOnTrack(RaceKart kart, long now)
    {
        var below = _track.Loop.PositionAt(kart.Progress.Distance).Y - kart.Position.Y;

        if (below < 12f)
        {
            kart.OffTrackSince = 0;
            return;
        }

        if (kart.OffTrackSince == 0)
        {
            kart.OffTrackSince = now;
            return;
        }

        if (now - kart.OffTrackSince < 2000)
            return;

        kart.OffTrackSince = 0;

        var gate = Math.Max(0, kart.Progress.NextGate - 1);
        var distance = _track.GateDistances[gate];
        var direction = _track.Loop.DirectionAt(distance);

        Reset?.Invoke(kart, ArenaRules.Transform(_track.Loop.PositionAt(distance) + new Vector3(0f, 1f, 0f), new Vector3(direction.X, 0f, direction.Z), Vector3.UnitY));
    }

    #endregion

    #region Items

    /// <summary>
    /// A player's kart asks for the box it touched: the request carries no box, so it's the nearest one in reach.
    /// </summary>
    public bool TryTakePickup(RaceKart kart, Vector3 position, float reach, long now)
    {
        var pickup = _pickups.Where(pickup => pickup.Active && Vector3.Distance(pickup.Position, position) <= reach)
            .MinBy(pickup => Vector3.Distance(pickup.Position, position));

        if (pickup is null)
            return false;

        Take(kart, pickup, now);

        return true;
    }

    private void Take(RaceKart kart, RacePickup pickup, long now)
    {
        pickup.Active = false;
        pickup.RespawnAt = now + _random.Next(PickupRespawnMinMs, PickupRespawnMaxMs);

        RaceItem? item = null;

        if (kart.IsPlayer)
        {
            // The weapons (missiles and mines) need projectiles live never showed; boosts, shields and the timed
            // effect are all the client's own.
            var roll = _random.Next(10);
            item = roll < 5 ? RaceItem.Boost : roll < 8 ? RaceItem.Shield : RaceItem.TimedEffect;

            if (kart.Items.Count < 3)
                kart.Items.Enqueue(item.Value);
        }
        else if (_random.Next(3) == 0)
        {
            kart.BoostedUntil = now + 2000;
        }

        PickupTaken?.Invoke(kart, pickup, item);
    }

    private void Spawn(RacePickup pickup)
    {
        pickup.Active = true;

        PickupSpawned?.Invoke(pickup);
    }

    /// <summary>
    /// AI karts take the boxes on their line.
    /// </summary>
    private void TakeAiPickups(long now)
    {
        foreach (var kart in _karts)
        {
            if (kart.IsPlayer || !Started)
                continue;

            foreach (var pickup in _pickups)
            {
                if (pickup.Active && Vector3.Distance(pickup.Position, kart.Position) < 3f)
                    Take(kart, pickup, now);
            }
        }
    }

    #endregion
}

public sealed class RacePickup(int id, Vector3 position)
{
    public int Id { get; } = id;

    public Vector3 Position { get; } = position;

    public bool Active { get; set; }

    public long RespawnAt { get; set; }
}
