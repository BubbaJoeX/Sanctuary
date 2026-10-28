using System;

namespace Sanctuary.Database.Entities;

/// <summary>
/// A Referee Pick card on the welcome screen. Referees edit them with !pick in game or on the WebAPI's admin site.
/// </summary>
public class DbRefereePick
{
    public int Id { get; set; }

    /// <summary>
    /// Order on the welcome screen, lowest first.
    /// </summary>
    public int Position { get; set; }

    public string Title { get; set; } = null!;
    public string Text { get; set; } = null!;

    /// <summary>
    /// Welcome screen event the card's button sends, e.g. Teleport (Param1 is a point of interest).
    /// </summary>
    public string Action { get; set; } = null!;

    public int Param1 { get; set; }

    public string Icon { get; set; } = "";

    /// <summary>
    /// Button label, or empty for the action's default label.
    /// </summary>
    public string Button { get; set; } = "";

    /// <summary>
    /// Character that last changed it with !pick; null for the stock picks and admin site edits (the WebAPI logs who made those).
    /// </summary>
    public ulong? UpdatedBy { get; set; }

    public DateTimeOffset Updated { get; set; } = DateTimeOffset.UtcNow;
}
