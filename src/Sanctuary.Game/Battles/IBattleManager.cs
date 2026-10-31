using System.Collections.Generic;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Interactions;
using Sanctuary.Game.Resources.Definitions;
using Sanctuary.Game.Zones;
using Sanctuary.Packet;

namespace Sanctuary.Game.Battles;

public interface IBattleManager
{
    /// <summary>
    /// The NPC (an Npcs.json id) offers a battle when clicked.
    /// </summary>
    bool IsStarter(int npcDefinitionId);

    IReadOnlyList<NpcInteractionOption> GetInteractionOptions(Player player, Npc npc);

    /// <summary>
    /// Opens the battle's start panel for the player, and asks their group along.
    /// </summary>
    bool Offer(Player player, BattleDefinition battle);

    /// <summary>
    /// The Games window's Battles tiles.
    /// </summary>
    IEnumerable<ActivityDefinition> GetActivities();

    void RequestEntrance(Player player, int encounterId);
    void CancelPending(Player player);
    void RequestExit(Player player);
    void AnswerInvitation(Player player, bool accept);

    /// <summary>
    /// The knocked out window's answer: respawn now, or (paid, outside a battle) revive where they lie.
    /// </summary>
    void Resume(Player player, bool paid);

    /// <summary>
    /// A player has loaded into a zone: a battle's own zone opens its HUD (and starts it), any other zone gets the
    /// normal combat rules back.
    /// </summary>
    void OnArrived(Player player, WorldZone zone);

    void OnPlayerLeft(Player player, WorldZone zone);

    /// <summary>
    /// A player logging out (before they're saved) is out of any battle they're in, with the job they had before it.
    /// </summary>
    void OnLogout(Player player);

    void OnKnockedOut(Player player);
}
