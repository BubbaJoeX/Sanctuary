using System;

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
}
