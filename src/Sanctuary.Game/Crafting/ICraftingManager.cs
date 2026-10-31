using System.Collections.Generic;

using Sanctuary.Game.Entities;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game.Crafting;

public interface ICraftingManager
{
    /// <summary>
    /// Takes a logging-in player's known recipes, and learns (quietly) the recipes of any recipe cards they hold.
    /// </summary>
    void Load(Player player, IEnumerable<int>? saved);

    /// <summary>
    /// The known recipes for SendSelf, each pointing at the stacks its ingredients are counted from.
    /// </summary>
    List<RecipeData> GetRecipes(Player player);

    /// <summary>
    /// A crafting station clicked: learns the recipes of any new cards, refreshes the list and opens the recipe
    /// window on the station's recipes.
    /// </summary>
    void OpenStation(Player player, int filterType);

    /// <summary>
    /// The recipe window's Make It: checks the recipe, the job rank and the ingredients, then starts its minigame (or
    /// makes it at once if it has none).
    /// </summary>
    void Start(Player player, int recipeId);

    /// <summary>
    /// A minigame won. A recipe's game makes its item only when it was started from the recipe: that takes the
    /// ingredients. True if the game should pay what it makes; otherwise <paramref name="hint"/> says why not.
    /// </summary>
    bool TryCompleteCraft(Player player, int minigameId, out string? hint);

    /// <summary>
    /// Whether winning this minigame would make its item: always for a game that isn't a recipe's, and for a
    /// recipe's game only when it was started from the recipe.
    /// </summary>
    bool WillMakeItem(Player player, int minigameId);

    /// <summary>
    /// Teaches a recipe, as its recipe card does. False if there's no such recipe or the player knows it.
    /// </summary>
    bool Learn(Player player, int recipeId);
}
