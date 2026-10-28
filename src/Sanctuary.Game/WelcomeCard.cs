using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

using Sanctuary.Core.Helpers;

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
    public static readonly IReadOnlyDictionary<string, string> Actions = RefereePickHelper.Kinds.Values
        .ToDictionary(kind => kind.Action, kind => kind.Button, StringComparer.OrdinalIgnoreCase);

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
/// The welcome screen's What's New items, read from Resources/WhatsNew.json whenever the announcements are sent,
/// so editing the file updates the welcome screen without a restart.
/// </summary>
public static class WhatsNew
{
    public static readonly string File = Path.Combine(ResourceManager.BaseDirectory, "WhatsNew.json");

    public static IReadOnlyList<WelcomeCard> Current => WelcomeCard.Load(File);
}
