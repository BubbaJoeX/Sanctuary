using System.Collections.Generic;

using Sanctuary.Game.Quests;

namespace Sanctuary.Game.Resources.Definitions.QuestGoals;

public sealed class DefeatGoal : CountedGoal
{
    // The mobs that count, by their name's locale id, so every spawn of "Bandit Patrolman" counts whichever
    // definition it uses. NpcIds adds single definitions, such as one named boss.
    public List<int> NpcNameIds { get; set; } = [];
    public List<int> NpcIds { get; set; } = [];

    public bool Counts(int npcId, int nameId) => NpcIds.Contains(npcId) || nameId != 0 && NpcNameIds.Contains(nameId);

    public override bool Matches(QuestDefinition quest, QuestEvent questEvent)
        => questEvent is MobDefeatedEvent { Mob: var mob } && Counts(mob.DefinitionId, mob.NameId);
}
