using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Sanctuary.Game;

/// <summary>
/// A card on the welcome screen: a Referee Pick or a What's New item.
/// </summary>
/// <param name="Action">Welcome screen event the card's button sends, e.g. Teleport (Param1 is a point of interest).
/// Empty for a card without a button (What's New only).</param>
/// <param name="Icon">An icon the welcome screen mod embeds (house, coin, tree, journal, trophy, bag, paw, friends, book,
/// wheel, referee), or a client image id.</param>
public sealed record WelcomeCard(string Title, string Text, string Action = "", int Param1 = 0, string Icon = "", string Button = "")
{
    /// <summary>
    /// The welcome screen events a card's button can send, with the button label used when a card doesn't set one.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> Actions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Teleport"] = "Teleport",
        ["Marketplace"] = "Shop",
        ["HouseBrowser"] = "Houses",
        ["Communicator"] = "Friends",
        ["OpenHerosJournal"] = "Journal",
        ["gotoMyStuff"] = "My Stuff",
        ["Achievements"] = "Achievements"
    };

    public string ButtonLabel => string.IsNullOrEmpty(Button) && Actions.TryGetValue(Action, out var label) ? label : Button;

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public static List<WelcomeCard> Load(string file)
    {
        if (!File.Exists(file))
            return [];

        using var stream = File.OpenRead(file);

        return (JsonSerializer.Deserialize<List<WelcomeCard>>(stream, JsonOptions) ?? [])
            .Where(card => string.IsNullOrEmpty(card.Action) || Actions.ContainsKey(card.Action))
            .ToList();
    }
}

/// <summary>
/// The welcome screen's Referee Picks. Seeded from Resources/RefereePicks.json; referees change them with !pick,
/// which isn't persisted (a restart restores the file's picks), like the message of the day.
/// </summary>
public static class RefereePicks
{
    public const int MaxPicks = 4;

    public static readonly string File = Path.Combine(ResourceManager.BaseDirectory, "RefereePicks.json");

    private static readonly object Lock = new();
    private static List<WelcomeCard>? _picks;

    public static IReadOnlyList<WelcomeCard> Current
    {
        get
        {
            lock (Lock)
                return (_picks ??= Load()).ToList();
        }
    }

    public static bool TryAdd(WelcomeCard pick)
    {
        lock (Lock)
        {
            _picks ??= Load();

            if (_picks.Count >= MaxPicks)
                return false;

            _picks.Add(pick);
            return true;
        }
    }

    public static bool TryRemoveAt(int index)
    {
        lock (Lock)
        {
            _picks ??= Load();

            if (index < 0 || index >= _picks.Count)
                return false;

            _picks.RemoveAt(index);
            return true;
        }
    }

    public static void Clear()
    {
        lock (Lock)
            _picks = [];
    }

    public static void Reset()
    {
        lock (Lock)
            _picks = Load();
    }

    private static List<WelcomeCard> Load() => WelcomeCard.Load(File).Where(pick => pick.Action.Length > 0).Take(MaxPicks).ToList();
}

/// <summary>
/// The welcome screen's What's New items, read from Resources/WhatsNew.json whenever the announcements are sent,
/// so editing the file updates the welcome screen without a restart.
/// </summary>
public static class WhatsNew
{
    public static readonly string File = Path.Combine(ResourceManager.BaseDirectory, "WhatsNew.json");

    public static IReadOnlyList<WelcomeCard> Current => WelcomeCard.Load(File);
}
