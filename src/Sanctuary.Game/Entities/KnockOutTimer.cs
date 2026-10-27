using Sanctuary.Game.Helpers;

namespace Sanctuary.Game.Entities;

/// <summary>
/// A player's knock-out: down until <see cref="CombatRules.PlayerRespawnSeconds"/> run out and they recover at
/// the zone's start, unless an ally revives them where they lie first. A revived player then has
/// <see cref="CombatRules.ReviveProtectionSeconds"/> before mobs notice them again. Knows nothing of packets,
/// so it can be tested on its own; <see cref="Player"/> carries it out.
/// </summary>
public sealed class KnockOutTimer
{
    private readonly object _lock = new();

    private long? _recoverAt;
    private long _protectedUntil = long.MinValue;

    /// <summary>
    /// Knocked out and not yet back up.
    /// </summary>
    public bool IsDown
    {
        get
        {
            lock (_lock)
                return _recoverAt is not null;
        }
    }

    public void KnockOut(long nowMs)
    {
        lock (_lock)
        {
            _recoverAt = nowMs + CombatRules.PlayerRespawnSeconds * 1000L;
            _protectedUntil = long.MinValue;
        }
    }

    /// <summary>
    /// True once, when the countdown to the zone's start runs out. False after a revive.
    /// </summary>
    public bool TryRecover(long nowMs)
    {
        lock (_lock)
        {
            if (_recoverAt is not { } recoverAt || nowMs < recoverAt)
                return false;

            _recoverAt = null;

            return true;
        }
    }

    /// <summary>
    /// Gets them up where they lie instead, with a moment to get clear. False if they aren't down,
    /// so a revive and the recovery at the zone's start never both happen.
    /// </summary>
    public bool TryRevive(long nowMs)
    {
        lock (_lock)
        {
            if (_recoverAt is null)
                return false;

            _recoverAt = null;
            _protectedUntil = nowMs + CombatRules.ReviveProtectionSeconds * 1000L;

            return true;
        }
    }

    public bool IsProtected(long nowMs)
    {
        lock (_lock)
            return nowMs < _protectedUntil;
    }

    /// <summary>
    /// Picking a fight gives the protection up.
    /// </summary>
    public void EndProtection()
    {
        lock (_lock)
            _protectedUntil = long.MinValue;
    }
}
