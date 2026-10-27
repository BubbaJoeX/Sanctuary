using System.Threading.Tasks;

using Sanctuary.Game.Entities;
using Sanctuary.Scripting;

namespace Sanctuary.Game.Conversations;

/// <summary>
/// Runs GM-authored content on NPCs and props: Lua scripts (see Sanctuary.Scripting.ScriptSandbox) and
/// conversation trees, shown to players as the chat window mod's dialog box.
/// </summary>
public interface INpcContentManager
{
    /// <summary>
    /// Compiles and attaches a script (null or empty removes it), then runs onSpawn.
    /// Returns the error to show the GM, or null. Does not persist anything.
    /// </summary>
    Task<string?> SetScriptAsync(Npc npc, string? source);

    /// <summary>
    /// Runs onSpawn and onInteract with the given player, returning the first error.
    /// </summary>
    Task<string?> TestScriptAsync(Npc npc, Player player);

    /// <summary>
    /// Attaches a conversation (null removes it). Does not persist anything.
    /// </summary>
    void SetConversation(Npc npc, Conversation? conversation);

    /// <summary>
    /// A player clicked an NPC that has a script or conversation.
    /// </summary>
    void OnInteract(Npc npc, Player player);

    /// <summary>
    /// A player said something near the NPC. Runs its script's onHear(player, message) hook and returns
    /// whether it has one.
    /// </summary>
    bool OnHear(Npc npc, Player player, string message);

    /// <summary>
    /// Called every second for NPCs with a script or conversation. Runs onSecond and due timers, and
    /// onProximity, onLeave and onArrive as they happen.
    /// </summary>
    void OnSecond(Npc npc);

    /// <summary>
    /// Runs onSignal on the other scripted NPCs in the sender's zone within the radius (0 for all).
    /// Returns false if the sender is over its per-second signal limit.
    /// </summary>
    bool Signal(Npc from, string name, string? data, float radius);

    /// <summary>
    /// The script-facing view of a player.
    /// </summary>
    IScriptablePlayer WrapPlayer(Player player);

    bool StartConversation(Player player, Npc npc, int? nodeId = null);

    void Choose(Player player, ulong npcGuid, int nodeId, int index);

    void EndConversation(Player player);
}
