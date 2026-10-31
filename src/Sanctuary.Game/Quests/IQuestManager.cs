using System.Collections.Generic;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Interactions;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game.Quests;

public interface IQuestManager
{
    bool IsQuestNpc(int npcId);

    List<NpcInteractionOption> GetInteractionOptions(Player player, Npc npc);

    void OnEvent(Player player, QuestEvent questEvent);

    /// <summary>
    /// Lets the player's quest log take events from systems that don't hold a quest manager
    /// (<see cref="QuestLog.Record"/>). Call once the log is loaded.
    /// </summary>
    void Attach(Player player);

    void AcceptQuest(Player player, int questId);

    bool CompleteQuest(Player player, int questId);

    void AbandonQuest(Player player, int questId);

    /// <summary>
    /// Completes the player's current goal of an in-progress quest as if it had been done (referee tool).
    /// Skipping the last goal leads to the quest's normal turn-in.
    /// </summary>
    bool SkipGoal(Player player, int questId);

    bool ResetQuest(Player player, int questId);

    void SetActiveQuest(Player player, int questId);

    ClientQuestData GetClientQuestData(Player player);

    void RestoreJournal(Player player);

    NotificationInfo? GetQuestBadge(Player player, Npc npc);

    void RefreshQuestNotification(Player player, ulong npcGuid);

    void RefreshObjectiveTarget(Player player);
}
