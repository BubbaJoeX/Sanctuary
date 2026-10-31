using Sanctuary.Game.Quests;

namespace Sanctuary.Game.Resources.Definitions.QuestGoals;

// Win a Battles.json battle (a dungeon or an encounter). The tracker points at its starter NPC.
public sealed class BattleGoal : CountedGoal
{
    public int BattleId { get; set; }

    public override bool Matches(QuestDefinition quest, QuestEvent questEvent)
        => BattleId != 0 && questEvent is BattleWonEvent { BattleId: var battleId } && battleId == BattleId;
}
