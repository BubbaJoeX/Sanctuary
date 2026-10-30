using System.Collections.Generic;

namespace Sanctuary.Game.Resources.Definitions;

/// <summary>
/// A coin store bundle (Resources/CoinStoreBundles.json): the store item it's sold as, by its id, and what buying one
/// gives instead of that item: inventory items and coins. The item's own price, name, icon and category are the
/// card the store shows.
/// </summary>
public sealed class CoinStoreBundleDefinition
{
    public int Id { get; set; }

    public string? Comment { get; set; }

    public List<CoinStoreBundleItem> Items { get; set; } = [];

    public int Coins { get; set; }
}

public sealed class CoinStoreBundleItem
{
    public int Id { get; set; }

    public int Quantity { get; set; } = 1;

    public int Tint { get; set; }
}
