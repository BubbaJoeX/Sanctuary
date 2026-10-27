using System.Threading.Tasks;

using Sanctuary.Game.Entities;

namespace Sanctuary.Game.Conversations;

/// <summary>
/// NPCs answering what players say near them: the nearest NPC in range hears a player's /say. A script with an
/// onHear(player, message) hook handles it; otherwise the Studio Umbra chat bot answers in the NPC's voice.
/// </summary>
public interface INpcChatManager
{
    /// <summary>
    /// Whether the chat bot answers (it also needs an API key). Scripts' onHear hooks run either way.
    /// </summary>
    bool BotEnabled { get; set; }

    bool BotConfigured { get; }

    void OnPlayerSay(Player player, string message);

    /// <summary>
    /// Forgets every NPC's conversation history, so the next line starts fresh.
    /// </summary>
    void Reset();

    /// <summary>
    /// The bot's state and the NPCs around the player, saying which one hears them and why the others don't.
    /// </summary>
    string[] Describe(Player player);

    /// <summary>
    /// Has the NPC that hears the player answer the message now (no cooldown) and returns what happened.
    /// </summary>
    Task<string> TestAsync(Player player, string message);
}
