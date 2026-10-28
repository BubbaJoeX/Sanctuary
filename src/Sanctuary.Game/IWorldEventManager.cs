using System.Collections.Generic;

namespace Sanctuary.Game;

/// <summary>
/// GM-defined events (e.g. "halloween"), and seasons (Resources/Seasons.json), which are on between their days of the
/// year as well. World NPCs flagged with an event only spawn while it is active.
/// </summary>
public interface IWorldEventManager
{
    /// <summary>
    /// On while !event has it on, or (a season) between its days of the year.
    /// </summary>
    bool IsActive(string key);

    /// <summary>
    /// Every known event key and whether it is active.
    /// </summary>
    IReadOnlyDictionary<string, bool> GetAll();

    /// <summary>
    /// Persists the event's state and spawns or despawns its NPCs in every loaded world zone.
    /// </summary>
    void SetActive(string key, bool active);

    /// <summary>
    /// Events staff opened for players to !join, and who joined.
    /// </summary>
    EventJoinBook Joins { get; }
}
