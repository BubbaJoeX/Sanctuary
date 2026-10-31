using System.Collections.Generic;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Interactions;

namespace Sanctuary.Game.Challenges;

/// <summary>
/// The in-world challenges (MiniGameData.txt type 1): an NPC offers one, the client's start screen shows it, and the
/// player plays it where they stand, against the minigame HUD's clock.
/// </summary>
public interface IChallengeManager
{
    /// <summary>
    /// The challenges an NPC offers, for its menu.
    /// </summary>
    IReadOnlyList<NpcInteractionOption> GetInteractionOptions(Player player, Npc npc);

    bool IsGiver(int npcDefinitionId);

    /// <summary>
    /// Opens a challenge's start screen (a Challenges.json id) for the player, laid out round the giver. False if
    /// there's no such challenge or the player is busy with another.
    /// </summary>
    bool Offer(Player player, int challengeId, Npc giver);

    /// <summary>
    /// The start screen's Go. False when the player has no challenge of that stage waiting, so it's another game's.
    /// </summary>
    bool TryStart(Player player, int stageId);

    /// <summary>
    /// The start screen closed or the player quit. False when they had no challenge.
    /// </summary>
    bool TryCancel(Player player);

    /// <summary>
    /// The player left the zone or logged out: their challenge ends, unpaid.
    /// </summary>
    void OnPlayerLeft(Player player);
}
