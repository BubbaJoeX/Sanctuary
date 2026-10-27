using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using Sanctuary.Core.Extensions;
using Sanctuary.Game.Resources.Definitions.Combat;

namespace Sanctuary.Game.Entities;

public enum MobState
{
    Idle,
    Fighting,

    /// <summary>
    /// Walking home after losing its targets or straying past its leash. Ignores players until it arrives.
    /// </summary>
    Returning,
    Dead
}

public enum MobActionType
{
    None,
    Chase,
    Attack,
    Return,

    /// <summary>
    /// Got home from <see cref="MobState.Returning"/>; heals to full.
    /// </summary>
    Reset
}

/// <param name="Engaged">The target is new to this fight (the moment a player gets "attacked by" it).</param>
public readonly record struct MobAction(MobActionType Type, ulong TargetGuid = 0, Vector3 Destination = default, bool Engaged = false);

/// <param name="CanBeAttacked">Alive, not a referee in god mode, and able to fight back.</param>
public readonly record struct MobCandidate(ulong Guid, Vector3 Position, bool CanBeAttacked);

/// <summary>
/// What a hostile mob decides to do each tick: aggro on players in range, chase and hit the one that has
/// hurt it most, and give up at its leash. Knows nothing of packets, so it can be tested on its own;
/// <see cref="NpcCombat"/> carries its decisions out.
/// </summary>
public sealed class MobBrain
{
    private const float HomeTolerance = 1.5f;

    // Players above or below it by more than this (on a bridge, in a cave) are out of reach.
    private const float MaxHeightDifference = 8f;

    private readonly Dictionary<ulong, int> _threat = [];
    private readonly HashSet<ulong> _engaged = [];

    private long _nextAttackAt;

    public MobBrain(MobDefinition definition)
    {
        Definition = definition;
    }

    public MobDefinition Definition { get; set; }

    public MobState State { get; private set; }

    public ulong TargetGuid { get; private set; }

    public IReadOnlyDictionary<ulong, int> Threat => _threat;

    /// <summary>
    /// Records damage (or a click, with 0) from a player. A mob minding its own business fights back.
    /// </summary>
    public void AddThreat(ulong guid, int amount)
    {
        if (State is MobState.Dead or MobState.Returning)
            return;

        _threat[guid] = _threat.GetValueOrDefault(guid) + Math.Max(0, amount);
        State = MobState.Fighting;
    }

    public void Kill()
    {
        State = MobState.Dead;
        TargetGuid = 0;
        _threat.Clear();
        _engaged.Clear();
    }

    public void Revive()
    {
        State = MobState.Idle;
        TargetGuid = 0;
        _threat.Clear();
        _engaged.Clear();
        _nextAttackAt = 0;
    }

    public MobAction Think(long nowMs, Vector3 position, Vector3 home, IReadOnlyList<MobCandidate> candidates)
    {
        switch (State)
        {
            case MobState.Dead:
                return default;

            case MobState.Returning:
                if (position.DistanceSquaredXZ(home) > HomeTolerance * HomeTolerance)
                    return new MobAction(MobActionType.Return, Destination: home);

                State = MobState.Idle;
                return new MobAction(MobActionType.Reset);

            case MobState.Idle:
                var spotted = FindNearest(position, candidates, Definition.AggroRadius);

                if (spotted is null)
                    return default;

                AddThreat(spotted.Value.Guid, 0);
                break;
        }

        if (position.DistanceSquaredXZ(home) > Definition.LeashRadius * Definition.LeashRadius)
            return GiveUp(home);

        // Forget anyone who died, logged off or left the zone.
        foreach (var guid in _threat.Keys.ToArray())
        {
            if (!candidates.Any(candidate => candidate.Guid == guid && candidate.CanBeAttacked))
                _threat.Remove(guid);
        }

        // Pick up anyone else who wanders close mid-fight.
        foreach (var candidate in candidates)
        {
            if (candidate.CanBeAttacked && !_threat.ContainsKey(candidate.Guid) && IsWithin(position, candidate.Position, Definition.AggroRadius))
                _threat[candidate.Guid] = 0;
        }

        if (_threat.Count == 0)
            return GiveUp(home);

        // The one that hurt it most; the current target keeps a tie.
        var targetGuid = _threat
            .OrderByDescending(entry => entry.Value)
            .ThenByDescending(entry => entry.Key == TargetGuid)
            .First().Key;

        TargetGuid = targetGuid;

        var engaged = _engaged.Add(targetGuid);
        var target = candidates.First(candidate => candidate.Guid == targetGuid);

        if (!IsWithin(position, target.Position, Definition.AttackRange))
            return new MobAction(MobActionType.Chase, targetGuid, target.Position, engaged);

        if (nowMs < _nextAttackAt)
            return new MobAction(MobActionType.None, targetGuid, Engaged: engaged);

        _nextAttackAt = nowMs + Definition.AttackIntervalMs;

        return new MobAction(MobActionType.Attack, targetGuid, target.Position, engaged);
    }

    private MobAction GiveUp(Vector3 home)
    {
        State = MobState.Returning;
        TargetGuid = 0;
        _threat.Clear();
        _engaged.Clear();

        return new MobAction(MobActionType.Return, Destination: home);
    }

    private static MobCandidate? FindNearest(Vector3 position, IReadOnlyList<MobCandidate> candidates, float radius)
    {
        MobCandidate? nearest = null;
        var nearestDistance = float.MaxValue;

        foreach (var candidate in candidates)
        {
            if (!candidate.CanBeAttacked || !IsWithin(position, candidate.Position, radius))
                continue;

            var distance = position.DistanceSquaredXZ(candidate.Position);

            if (distance >= nearestDistance)
                continue;

            nearestDistance = distance;
            nearest = candidate;
        }

        return nearest;
    }

    private static bool IsWithin(Vector3 position, Vector3 other, float radius) =>
        radius > 0f &&
        MathF.Abs(position.Y - other.Y) <= MaxHeightDifference &&
        position.DistanceSquaredXZ(other) <= radius * radius;
}
