using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using Sanctuary.Database;
using Sanctuary.Database.Entities;

namespace Sanctuary.Core.Helpers;

/// <summary>
/// The welcome screen's Referee Picks (the RefereePicks table), shared by !pick in the Gateway and the WebAPI's admin site.
/// </summary>
public static class RefereePickHelper
{
    public const int MaxPicks = 4;
    public const int MaxTitleLength = 40;
    public const int MaxTextLength = 120;
    public const int MaxIconLength = 32;
    public const int MaxButtonLength = 24;

    /// <summary>
    /// How often the Gateway reads the table, so picks saved on the admin site reach players online.
    /// </summary>
    public static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(10);

    /// <summary>
    /// The kinds of pick: the welcome screen event its button sends, the icon it gets by default and the button
    /// label used when a pick doesn't set one. A teleport pick's Param1 is a point of interest.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, (string Action, string Icon, string Button)> Kinds = new Dictionary<string, (string Action, string Icon, string Button)>(StringComparer.OrdinalIgnoreCase)
    {
        ["teleport"] = ("Teleport", "tree", "Teleport"),
        ["shop"] = ("Marketplace", "coin", "Shop"),
        ["houses"] = ("HouseBrowser", "house", "Houses"),
        ["friends"] = ("Communicator", "friends", "Friends"),
        ["journal"] = ("OpenHerosJournal", "journal", "Journal"),
        ["mystuff"] = ("gotoMyStuff", "bag", "My Stuff"),
        ["achievements"] = ("Achievements", "trophy", "Achievements")
    };

    /// <summary>
    /// Icons the welcome screen mod embeds. A pick can also use a client image id.
    /// </summary>
    public static readonly IReadOnlyList<string> Icons = ["house", "coin", "tree", "journal", "trophy", "bag", "paw", "friends", "book", "wheel", "referee"];

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    /// <summary>
    /// Why a pick can't go on the welcome screen, or null when it can.
    /// </summary>
    public static string? Validate(DbRefereePick pick, Func<int, bool> isPointOfInterest)
    {
        if (!Kinds.Values.Any(kind => kind.Action == pick.Action))
            return $"\"{pick.Action}\" isn't a welcome screen event.";

        if (string.IsNullOrWhiteSpace(pick.Title) || string.IsNullOrWhiteSpace(pick.Text))
            return "A pick needs a title and text.";

        if (pick.Title.Length > MaxTitleLength || pick.Text.Length > MaxTextLength)
            return $"Keep the title to {MaxTitleLength} characters and the text to {MaxTextLength}.";

        if ((pick.Icon?.Length ?? 0) > MaxIconLength || (pick.Button?.Length ?? 0) > MaxButtonLength)
            return $"Keep the icon to {MaxIconLength} characters and the button to {MaxButtonLength}.";

        if (pick.Action == Kinds["teleport"].Action && !isPointOfInterest(pick.Param1))
            return $"There is no point of interest {pick.Param1}.";

        return null;
    }

    /// <summary>
    /// The picks in welcome screen order.
    /// </summary>
    public static List<DbRefereePick> Load(DatabaseContext dbContext)
    {
        return dbContext.RefereePicks
            .AsNoTracking()
            .OrderBy(x => x.Position)
            .ThenBy(x => x.Id)
            .ToList();
    }

    /// <summary>
    /// Replaces every pick with these, in this order.
    /// </summary>
    public static void Replace(DatabaseContext dbContext, IEnumerable<DbRefereePick> picks, ulong? updatedBy)
    {
        var updated = DateTimeOffset.UtcNow;
        var rows = picks.Take(MaxPicks).Select((pick, i) => new DbRefereePick
        {
            Position = i,
            Title = pick.Title,
            Text = pick.Text,
            Action = pick.Action,
            Param1 = pick.Param1,
            Icon = pick.Icon ?? "",
            Button = pick.Button ?? "",
            UpdatedBy = updatedBy,
            Updated = updated
        }).ToList();

        var strategy = dbContext.Database.CreateExecutionStrategy();

        strategy.Execute(() =>
        {
            dbContext.ChangeTracker.Clear();

            using var transaction = dbContext.Database.BeginTransaction();

            dbContext.RefereePicks.ExecuteDelete();
            dbContext.RefereePicks.AddRange(rows);
            dbContext.SaveChanges();

            transaction.Commit();
        });
    }

    /// <summary>
    /// The stock picks in Resources/RefereePicks.json, which !pick reset and the admin site restore.
    /// Picks that fail <see cref="Validate"/> are left out.
    /// </summary>
    public static List<DbRefereePick> LoadDefaults(string file, Func<int, bool> isPointOfInterest)
    {
        if (!File.Exists(file))
            return [];

        using var stream = File.OpenRead(file);

        return (JsonSerializer.Deserialize<List<DbRefereePick>>(stream, JsonOptions) ?? [])
            .Where(pick => Validate(pick, isPointOfInterest) is null)
            .Take(MaxPicks)
            .ToList();
    }
}
