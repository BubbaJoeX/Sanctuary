using System;
using System.Numerics;

using Sanctuary.Game.Resources.Definitions;

namespace Sanctuary.Game.Battles;

/// <summary>
/// The battle rules that need no zone or packets, so they can be tested on their own.
/// </summary>
public static class BattleRules
{
    /// <summary>
    /// Live sent this as every battle packet's second header int, and the client echoes it back.
    /// </summary>
    public const int ServerId = 1008;

    /// <summary>
    /// A lost battle still pays this share of its stars, for the enemies beaten.
    /// </summary>
    public const float LossStarShare = 0.5f;

    /// <summary>
    /// "Revive here" outside a battle.
    /// </summary>
    public const int OverworldReviveCost = 100;

    /// <summary>
    /// How far behind the entry the Exit that's up from the start stands.
    /// </summary>
    public const float EntryExitDistance = 4f;

    /// <summary>
    /// The encounter id: the instance number in the high 16 bits over the battle's id, as live.
    /// </summary>
    public static int EncounterId(int instance, int battleId) => ((instance & 0x7FFF) << 16) | (battleId & 0xFFFF);

    /// <summary>
    /// Every enemy in the battle, over all its waves.
    /// </summary>
    public static int EnemyCount(BattleDefinition battle)
    {
        var count = 0;

        foreach (var wave in battle.Waves)
            count += wave.Spawns.Count;

        return count;
    }

    /// <summary>
    /// What winning (or losing, having beaten <paramref name="defeated"/> of <paramref name="total"/>) pays each
    /// player.
    /// </summary>
    public static (int Stars, int Coins) Payout(BattleDefinition battle, bool won, int defeated, int total)
    {
        if (won)
            return (battle.Stars, battle.Coins);

        if (total <= 0)
            return (0, 0);

        var share = Math.Clamp((float)defeated / total, 0f, 1f) * LossStarShare;

        return ((int)MathF.Round(battle.Stars * share), 0);
    }

    /// <summary>
    /// Where the Exit that's up from the start stands: <see cref="EntryExitDistance"/> behind the entry, or (with
    /// the entry's back to a wall) behind and to one side, or to one side, wherever the ground goes furthest.
    /// <paramref name="walk"/> walks straight from the entry towards a spot and says where the ground stops it.
    /// </summary>
    public static Vector3 EntryExitPosition(Vector3 entry, float entryHeading, Func<Vector3, Vector3, Vector3> walk)
    {
        var forward = new Vector3(MathF.Sin(entryHeading), 0f, MathF.Cos(entryHeading));
        var right = new Vector3(forward.Z, 0f, -forward.X);

        var best = entry;
        var bestDistance = 0f;

        foreach (var direction in (ReadOnlySpan<Vector3>)[-forward, Vector3.Normalize(right - forward), Vector3.Normalize(-right - forward), right, -right])
        {
            var end = walk(entry, entry + direction * EntryExitDistance);
            var distance = Vector2.Distance(new Vector2(end.X, end.Z), new Vector2(entry.X, entry.Z));

            if (distance >= EntryExitDistance - 0.5f)
                return end;

            if (distance > bestDistance)
            {
                best = end;
                bestDistance = distance;
            }
        }

        return best;
    }
}
