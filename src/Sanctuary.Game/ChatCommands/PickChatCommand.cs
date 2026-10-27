using System;
using System.Collections.Generic;
using System.Linq;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;

namespace Sanctuary.Game.ChatCommands;

/// <summary>
/// Edits the Referee Picks cards on the welcome screen (see <see cref="RefereePicks"/>).
/// </summary>
public class PickChatCommand : IChatCommand
{
    private const int MaxTitleLength = 40;
    private const int MaxTextLength = 120;

    // !pick add <kind>: (welcome screen event, icon).
    private static readonly Dictionary<string, (string Action, string Icon)> Kinds = new(StringComparer.OrdinalIgnoreCase)
    {
        ["teleport"] = ("Teleport", "tree"),
        ["shop"] = ("Marketplace", "coin"),
        ["houses"] = ("HouseBrowser", "house"),
        ["friends"] = ("Communicator", "friends"),
        ["journal"] = ("OpenHerosJournal", "journal"),
        ["mystuff"] = ("gotoMyStuff", "bag"),
        ["achievements"] = ("Achievements", "trophy")
    };

    private readonly IZoneManager _zoneManager;
    private readonly IResourceManager _resourceManager;
    private readonly IChatCommandManager _chatCommandManager;

    public string KeyWord => "pick";
    public string Usage => "| add <teleport <poi id>|shop|houses|friends|journal|mystuff|achievements> <title> | <text> | remove <n> | clear | reset";
    public string Description => "Lists or edits the Referee Picks on the welcome screen. Changes reach everyone online the next time they open it, and last until a restart.";
    public ChatCommandRole RequiredRole => ChatCommandRole.Mod;

    public PickChatCommand(IZoneManager zoneManager, IResourceManager resourceManager, IChatCommandManager chatCommandManager)
    {
        _zoneManager = zoneManager;
        _resourceManager = resourceManager;
        _chatCommandManager = chatCommandManager;
    }

    public bool Handle(Player invoker, string[] args)
    {
        if (args.Length == 0)
        {
            var picks = RefereePicks.Current;

            ChatHelper.SendSystemMessage(invoker, picks.Count == 0
                ? "No Referee Picks are set."
                : string.Join('\n', picks.Select((pick, i) => $"{i + 1}. {pick.Title} ({pick.Action}{(pick.Param1 != 0 ? $" {pick.Param1}" : "")})")));
            return true;
        }

        switch (args[0].ToLowerInvariant())
        {
            case "add" when args.Length > 2:
                return Add(invoker, args[1..]);

            case "remove" when args.Length == 2 && int.TryParse(args[1], out var number):
                if (!RefereePicks.TryRemoveAt(number - 1))
                {
                    ChatHelper.SendSystemMessage(invoker, $"There is no pick {number}.");
                    return true;
                }

                Changed(invoker, "Remove", $"Pick: {number}");
                return true;

            case "clear":
                RefereePicks.Clear();
                Changed(invoker, "Clear", null);
                return true;

            case "reset":
                RefereePicks.Reset();
                Changed(invoker, "Reset", null);
                return true;
        }

        return false;
    }

    private bool Add(Player invoker, string[] args)
    {
        if (!Kinds.TryGetValue(args[0], out var kind))
            return false;

        var param1 = 0;
        var rest = args[1..];

        if (kind.Action == "Teleport")
        {
            if (rest.Length < 2 || !int.TryParse(rest[0], out param1))
                return false;

            if (!_resourceManager.PointOfInterests.ContainsKey(param1))
            {
                ChatHelper.SendSystemMessage(invoker, $"There is no point of interest {param1}.");
                return true;
            }

            rest = rest[1..];
        }

        var parts = string.Join(' ', rest).Split('|', 2, StringSplitOptions.TrimEntries);

        if (parts.Length != 2 || parts[0].Length == 0 || parts[1].Length == 0)
            return false;

        if (parts[0].Length > MaxTitleLength || parts[1].Length > MaxTextLength)
        {
            ChatHelper.SendSystemMessage(invoker, $"Keep the title to {MaxTitleLength} characters and the text to {MaxTextLength}.");
            return true;
        }

        if (!RefereePicks.TryAdd(new WelcomeCard(parts[0], parts[1], kind.Action, param1, kind.Icon)))
        {
            ChatHelper.SendSystemMessage(invoker, $"The welcome screen holds {RefereePicks.MaxPicks} picks; remove one first.");
            return true;
        }

        Changed(invoker, "Add", $"Title: \"{parts[0]}\"");
        return true;
    }

    private void Changed(Player invoker, string action, string? detail)
    {
        // The client keeps the announcements it was sent and fills the welcome screen from them when it opens.
        var packet = WelcomeScreenHelper.GetAnnouncementsPacket();

        foreach (var player in _zoneManager.Zones.SelectMany(zone => zone.Players))
            player.SendTunneled(packet);

        _chatCommandManager.LogAction(this, invoker, action, null, detail);
        ChatHelper.SendSystemMessage(invoker, $"Referee Picks updated ({RefereePicks.Current.Count} of {RefereePicks.MaxPicks}).");
    }
}
