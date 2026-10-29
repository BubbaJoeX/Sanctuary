using System.Collections.Generic;

namespace Sanctuary.Game.Housing;

/// <summary>
/// Who is standing on one teleporter pad. Stepping on starts a charge-up; staying on until it's done sends
/// the player. Someone who arrives on a pad has to step off it before it will send them anywhere.
/// </summary>
public sealed class TeleporterPad
{
    /// <summary>
    /// How long the charge-up takes: the teleporter flash lands about a second and a quarter in.
    /// </summary>
    public const long ChargeMilliseconds = 1250;

    private readonly Dictionary<ulong, long> _charging = [];
    private readonly HashSet<ulong> _arrived = [];

    /// <param name="onPad">Players standing on the pad now.</param>
    /// <returns>Who just started charging, and who finished charging and should be sent.</returns>
    public (List<ulong> Started, List<ulong> Ready) Update(IReadOnlyCollection<ulong> onPad, long now)
    {
        var started = new List<ulong>();
        var ready = new List<ulong>();

        var standing = new HashSet<ulong>(onPad);

        foreach (var guid in new List<ulong>(_charging.Keys))
        {
            if (!standing.Contains(guid))
                _charging.Remove(guid);
        }

        _arrived.IntersectWith(standing);

        foreach (var guid in standing)
        {
            if (_arrived.Contains(guid))
                continue;

            if (!_charging.TryGetValue(guid, out var startedAt))
            {
                _charging[guid] = now;
                started.Add(guid);
            }
            else if (now - startedAt >= ChargeMilliseconds)
            {
                _charging.Remove(guid);
                ready.Add(guid);
            }
        }

        return (started, ready);
    }

    /// <summary>
    /// The player was just sent here: they won't be sent on until they step off.
    /// </summary>
    public void MarkArrived(ulong guid)
    {
        _charging.Remove(guid);
        _arrived.Add(guid);
    }

    public bool IsCharging(ulong guid) => _charging.ContainsKey(guid);
}
