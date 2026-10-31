using System.Collections.Generic;

using Sanctuary.Game.Quests;

namespace Sanctuary.Game.Resources.Definitions.QuestGoals;

// Play one of MiniGameData's games, such as a tower defense board or the smelting game.
public sealed class MinigameGoal : CountedGoal
{
    public List<int> MinigameIds { get; set; } = [];

    // False counts a loss too.
    public bool RequireWin { get; set; } = true;

    public override bool Matches(QuestDefinition quest, QuestEvent questEvent)
        => questEvent is MinigameFinishedEvent { MinigameId: var minigameId, Won: var won }
            && (won || !RequireWin)
            && MinigameIds.Contains(minigameId);
}
