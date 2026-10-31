using System.Collections.Generic;

using Sanctuary.Game.Quests;

namespace Sanctuary.Game.Resources.Definitions.QuestGoals;

// Wear or wield one of these items. Already having one on when the goal starts counts.
public sealed class EquipGoal : QuestGoal
{
    public List<int> ItemDefinitionIds { get; set; } = [];

    public override bool Matches(QuestDefinition quest, QuestEvent questEvent)
        => questEvent is ItemEquippedEvent { ItemDefinitionId: var itemDefinitionId } && ItemDefinitionIds.Contains(itemDefinitionId);
}
