using System.Collections.Generic;

namespace Sanctuary.Game.Resources.Definitions;

/// <summary>
/// A recipe (see Recipes.json): what it makes, from what, and the minigame that makes it. A player learns one by
/// getting its recipe card (an item of type 5 whose Param1 is the recipe's id).
/// </summary>
public sealed class RecipeDefinition
{
    public int Id { get; set; }

    public string Comment { get; set; } = string.Empty;

    /// <summary>
    /// The item it makes. The recipe window shows this item's name, icon and description.
    /// </summary>
    public int ResultItemId { get; set; }

    /// <summary>
    /// The recipe window's filter: the crafting job's profile type (9 smelting, 10 cooking, 12 forging, 0 farm orders).
    /// </summary>
    public int FilterType { get; set; }

    /// <summary>
    /// With FilterType 0, the farm's order group.
    /// </summary>
    public int Group { get; set; }

    /// <summary>
    /// The job it belongs to (a Profiles.json id).
    /// </summary>
    public int ProfileId { get; set; }

    /// <summary>
    /// The job rank it needs, as its recipe card says.
    /// </summary>
    public int MinProfileRank { get; set; } = 1;

    /// <summary>
    /// The minigame that makes it: winning it, started from the recipe, takes the ingredients and pays what the game
    /// makes. None makes it at once.
    /// </summary>
    public int? MinigameId { get; set; }

    /// <summary>
    /// At most 8.
    /// </summary>
    public List<RecipeComponentDefinition> Components { get; set; } = [];

    /// <summary>
    /// Whether its ingredients were written for Sanctuary (no live record of them survives).
    /// </summary>
    public bool Authored { get; set; }
}

public sealed class RecipeComponentDefinition
{
    public int ItemId { get; set; }

    public int Required { get; set; } = 1;
}

/// <summary>
/// A world prop or NPC that crafts or plays a minigame when clicked (see Stations.json): a cooking table, furnace or
/// anvil opens the recipe window on its recipes; a game table, harvest field or arcade starts its game.
/// </summary>
public sealed class StationDefinition
{
    /// <summary>
    /// The Npcs.json id.
    /// </summary>
    public int NpcId { get; set; }

    public string Comment { get; set; } = string.Empty;

    /// <summary>
    /// The kinds of recipes it makes (recipe FilterTypes); with several, clicking it asks which.
    /// </summary>
    public List<int> RecipeFilters { get; set; } = [];

    /// <summary>
    /// The minigames it starts; with several different games, clicking it asks which (several levels of one game
    /// pick one at random).
    /// </summary>
    public List<int> MinigameIds { get; set; } = [];
}
