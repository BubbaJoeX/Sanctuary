using System.Collections.Generic;

using Sanctuary.Core.Collections;

namespace Sanctuary.Game.Resources.Definitions.Rewards;

public sealed class ItemRewardDropDefinition : RewardDropDefinition
{
    public int ItemDefinitionId { get; set; }
    public List<TintDropDefinition> Tints { get; set; } = [];

    public int Quantity { get; set; } = 1;

    /// <summary>
    /// Who it can go to, as an item's GenderUsage: 0 anyone. Tables that roll for a player skip the other gender's.
    /// </summary>
    public int GenderUsage { get; set; }

    private WeightedDropTable<TintDropDefinition>? _tintTable;
    public WeightedDropTable<TintDropDefinition>? TintTable => Tints.Count == 0
        ? null
        : _tintTable ??= new WeightedDropTable<TintDropDefinition>(Tints);
}
