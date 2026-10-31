using System.Collections.Generic;

using Sanctuary.Game.Resources.Definitions.Rewards;

namespace Sanctuary.Game.Resources.Definitions;

/// <summary>
/// A gift (see Gifts.json): an item that opens into others, coins or stars instead of going into the inventory, as
/// mystery gifts, grab bags, random clothes and bonus stars did on live. One definition covers every item of a kind.
/// </summary>
public sealed class GiftDefinition
{
    public string Comment { get; set; } = string.Empty;

    /// <summary>
    /// The items that open into it.
    /// </summary>
    public List<int> ItemIds { get; set; } = [];

    /// <summary>
    /// Always given, all of them (or with <see cref="PickByGender"/>, the first the player's gender can have).
    /// </summary>
    public List<RewardDropDefinition> Fixed { get; set; } = [];

    public bool PickByGender { get; set; }

    /// <summary>
    /// Random picks from reward tables (Rewards.json), each skipping what the player's gender can't have.
    /// </summary>
    public List<GiftRollDefinition> Rolls { get; set; } = [];
}

public sealed class GiftRollDefinition
{
    /// <summary>
    /// A Rewards.json table key.
    /// </summary>
    public string Table { get; set; } = string.Empty;

    /// <summary>
    /// Instead of one table: each pick first chooses among these by weight.
    /// </summary>
    public List<GiftChoiceDefinition> Choose { get; set; } = [];

    public int Count { get; set; } = 1;
}

/// <summary>
/// One of a roll's choices: a pick from a table, or coins.
/// </summary>
public sealed class GiftChoiceDefinition
{
    public string? Table { get; set; }

    public int Coins { get; set; }

    public int Weight { get; set; } = 1;
}
