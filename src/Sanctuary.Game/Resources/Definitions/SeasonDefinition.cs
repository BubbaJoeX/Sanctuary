using System;

namespace Sanctuary.Game.Resources.Definitions;

/// <summary>
/// A seasonal holiday (Resources/Seasons.json): a world event that is on between two days of the year, every year,
/// as well as whenever !event turns it on. NPCs flagged for it spawn only then, and conditions can wait for it.
/// </summary>
public class SeasonDefinition
{
    /// <summary>
    /// The event key NPCs and conditions name it by (lower case, up to 32 characters).
    /// </summary>
    public string Key { get; set; } = null!;

    public string Name { get; set; } = null!;

    public int StartMonth { get; set; }
    public int StartDay { get; set; }

    /// <summary>
    /// The last day it's on; before the start it runs over the new year (Dec 1 to Jan 6).
    /// </summary>
    public int EndMonth { get; set; }
    public int EndDay { get; set; }

    public bool IsActive(DateTime date)
    {
        var day = (date.Month, date.Day);
        var start = (StartMonth, StartDay);
        var end = (EndMonth, EndDay);

        if (start.CompareTo(end) <= 0)
            return day.CompareTo(start) >= 0 && day.CompareTo(end) <= 0;

        return day.CompareTo(start) >= 0 || day.CompareTo(end) <= 0;
    }
}
