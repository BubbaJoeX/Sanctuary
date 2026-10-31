using System.Collections.Generic;

using Sanctuary.Game.Quests;

namespace Sanctuary.Game.Resources.Definitions.QuestGoals;

// Make something from a recipe: cook, smelt, forge... by Recipes.json id or by the item it makes.
public sealed class CraftGoal : CountedGoal
{
    public List<int> RecipeIds { get; set; } = [];
    public List<int> ItemDefinitionIds { get; set; } = [];

    public override bool Matches(QuestDefinition quest, QuestEvent questEvent)
        => questEvent is ItemCraftedEvent { RecipeId: var recipeId, ItemDefinitionId: var itemDefinitionId }
            && (RecipeIds.Contains(recipeId) || ItemDefinitionIds.Contains(itemDefinitionId));
}
