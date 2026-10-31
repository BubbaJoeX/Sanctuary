using Sanctuary.Core.Collections;

namespace Sanctuary.Game.Resources.Definitions;

public sealed class CollectionNodeDropDefinition : IWeighted
{
    public int ItemDefinitionId { get; set; }
    public int Weight { get; set; } = 1;

    // How many of the item one pickup gives, as a mining node gives a handful of ore.
    public int Quantity { get; set; } = 1;
}
