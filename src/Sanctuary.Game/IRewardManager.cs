using Sanctuary.Game.Entities;
using Sanctuary.Game.Resources.Definitions.Rewards;

namespace Sanctuary.Game;

public interface IRewardManager
{
    bool TryRollReward(string rewardTableKey, out RewardDropDefinition? drop);

    bool TryGrantReward(Player player, RewardDropDefinition drop, ulong sourceGuid = 0);

    bool TryGrantItem(Player player, int itemDefinitionId, int tint, int quantity = 1, ulong sourceGuid = 0);

    bool TryGrantCurrency(Player player, CurrencyType currencyType, int amount);

    /// <summary>
    /// Removes that many of an item (any tint, never an equipped one). False, taking nothing, if there are fewer.
    /// </summary>
    bool TryTakeItem(Player player, int itemDefinitionId, int quantity);

    /// <summary>
    /// Removes coins. False, taking nothing, if the player has fewer.
    /// </summary>
    bool TryTakeCoins(Player player, int amount);

    /// <summary>
    /// Adds stars to the active job, ranking it up as its bar fills. False at the top rank.
    /// </summary>
    bool TryGrantExperience(Player player, int experience);
}
