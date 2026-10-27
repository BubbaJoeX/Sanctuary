using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;

using Microsoft.Extensions.Logging;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;
using Sanctuary.Game.Resources.Definitions;
using Sanctuary.Game.Zones;
using Sanctuary.Packet;

namespace Sanctuary.Game.ChatCommands;

/// <summary>
/// Backs the referee panel in the patched chat window (ClientMods/chatWindowCtrl).
/// Lookups reply with <see cref="ChatHelper.SendGmPanelData"/> lines the panel renders.
/// </summary>
public class GmChatCommand : IChatCommand
{
    private const int PageSize = 18;

    private readonly ILogger _logger;
    private readonly IChatCommandManager _chatCommandManager;
    private readonly IZoneManager _zoneManager;
    private readonly IResourceManager _resourceManager;
    private readonly IWorldEventManager _worldEventManager;
    private readonly RefereeBookmarks _bookmarks;

    public string KeyWord => "gm";
    public string Usage => "[open] | items|npcs|houses|players <page> [search] | player <name> | goto <player> | summon <player> | " +
        "gather [radius] | back | pos | marks | mark <name> | unmark <name> | go <name> | events";
    public string Description => "Referee tools: opens the GM panel and answers its lookups.";
    public ChatCommandRole RequiredRole => ChatCommandRole.Mod;

    public GmChatCommand(ILogger<GmChatCommand> logger, IChatCommandManager chatCommandManager, IZoneManager zoneManager,
        IResourceManager resourceManager, IWorldEventManager worldEventManager)
    {
        _logger = logger;
        _chatCommandManager = chatCommandManager;
        _zoneManager = zoneManager;
        _resourceManager = resourceManager;
        _worldEventManager = worldEventManager;
        _bookmarks = new RefereeBookmarks(logger);
    }

    private static string? GetName(int nameId) => GameNameHelper.Get(nameId);

    public bool Handle(Player invoker, string[] args)
    {
        var subCommand = args.Length == 0 ? "open" : args[0].ToLowerInvariant();

        switch (subCommand)
        {
            case "open":
                ChatHelper.SendGmPanelData(invoker, "open", invoker.ChatCommandRole);
                return true;

            case "items":
                return HandleItems(invoker, args[1..]);

            case "npcs":
                return HandleNpcs(invoker, args[1..]);

            case "houses":
                return HandleHouses(invoker, args[1..]);

            case "players":
                return HandlePlayers(invoker, args[1..]);

            case "player":
                return HandlePlayer(invoker, args[1..]);

            case "goto":
                return HandleGoto(invoker, args[1..]);

            case "summon":
                return HandleSummon(invoker, args[1..]);

            case "gather":
                return HandleGather(invoker, args[1..]);

            case "back":
                return HandleBack(invoker);

            case "pos":
                ChatHelper.SendGmPanelData(invoker, "pos", invoker.Zone.Name,
                    invoker.Position.X.ToString("0.0"), invoker.Position.Y.ToString("0.0"), invoker.Position.Z.ToString("0.0"));
                return true;

            case "marks":
                SendBookmarks(invoker);
                return true;

            case "mark":
                return HandleMark(invoker, args[1..]);

            case "unmark":
                return HandleUnmark(invoker, args[1..]);

            case "go":
                return HandleGo(invoker, args[1..]);

            case "events":
                SendEvents(invoker);
                return true;

            default:
                return false;
        }
    }

    private static void ParsePage(string[] args, out int page, out string search)
    {
        page = 1;

        if (args.Length > 0 && int.TryParse(args[0], out var parsedPage))
        {
            page = Math.Max(1, parsedPage);
            args = args[1..];
        }

        search = string.Join(' ', args).Trim();
    }

    private static bool Matches(string search, int id, params string?[] values)
    {
        if (search.Length == 0 || id.ToString() == search)
            return true;

        return values.Any(value => value?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false);
    }

    private static void SendPage<T>(Player invoker, string kind, string search, int page, List<T> results, Action<T> sendRow)
    {
        var pages = Math.Max(1, (results.Count + PageSize - 1) / PageSize);
        page = Math.Min(page, pages);

        // Header first so the panel can clear the previous results.
        ChatHelper.SendGmPanelData(invoker, kind, search, page, pages, results.Count);

        foreach (var result in results.Skip((page - 1) * PageSize).Take(PageSize))
            sendRow(result);

        ChatHelper.SendGmPanelData(invoker, "end", kind);
    }

    private bool HandleItems(Player invoker, string[] args)
    {
        ParsePage(args, out var page, out var search);

        // Unnamed items are internal (test/unused), keep them out of the browser.
        var results = _resourceManager.ClientItemDefinitions.Values
            .Select(item => (Item: item, Name: GetName(item.NameId) ?? item.Comment))
            .Where(x => x.Name is not null && Matches(search, x.Item.Id, x.Name, x.Item.ModelName))
            .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Item.Id)
            .ToList();

        SendPage(invoker, "items", search, page, results, x =>
            ChatHelper.SendGmPanelData(invoker, "item", x.Item.Id, x.Item.Icon.Id, x.Item.Icon.TintId, x.Item.Type, x.Name!));

        return true;
    }

    private bool HandleNpcs(Player invoker, string[] args)
    {
        ParsePage(args, out var page, out var search);

        var results = _resourceManager.Npcs.Values
            .Select(npc => (Npc: npc, Name: GetName(npc.NameId) ?? npc.Name ?? Path.GetFileNameWithoutExtension(npc.ModelFileName)))
            .Where(x => Matches(search, x.Npc.Id, x.Name, x.Npc.ModelFileName))
            .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Npc.Id)
            .ToList();

        SendPage(invoker, "npcs", search, page, results, x =>
            ChatHelper.SendGmPanelData(invoker, "npc", x.Npc.Id, x.Npc.ModelId, x.Name));

        return true;
    }

    private bool HandleHouses(Player invoker, string[] args)
    {
        ParsePage(args, out var page, out var search);

        var results = _resourceManager.Houses.Values
            .Select(house => (House: house, Name: GetName(house.NameId) ?? $"House {house.Id}"))
            .Where(x => Matches(search, x.House.Id, x.Name))
            .OrderBy(x => x.House.Id)
            .ToList();

        SendPage(invoker, "houses", search, page, results, x =>
            ChatHelper.SendGmPanelData(invoker, "house", x.House.Id, GetHouseImageSetId(x.House), x.Name));

        return true;
    }

    /// <summary>
    /// The panel draws image sets; a house's own icon isn't one, so use its marketplace item's.
    /// </summary>
    private int GetHouseImageSetId(HouseDefinition house)
    {
        foreach (var itemDefinitionId in house.ItemDefinitionIds)
        {
            if (_resourceManager.ClientItemDefinitions.TryGetValue(itemDefinitionId, out var itemDefinition) && itemDefinition.Icon.Id > 0)
                return itemDefinition.Icon.Id;
        }

        return 0;
    }

    private bool HandlePlayers(Player invoker, string[] args)
    {
        ParsePage(args, out var page, out var search);

        var results = _zoneManager.Zones
            .SelectMany(zone => zone.Players)
            .Where(player => search.Length == 0 || player.Name.FullName.Contains(search, StringComparison.OrdinalIgnoreCase))
            .OrderBy(player => player.Name.FullName)
            .ToList();

        SendPage(invoker, "players", search, page, results, player =>
            ChatHelper.SendGmPanelData(invoker, "player", player.Guid, player.Name.FullName, player.Zone.Name));

        return true;
    }

    private bool TryFindPlayer(Player invoker, string[] args, out Player target)
    {
        target = null!;

        var name = string.Join(' ', args).Trim();

        if (name.Length == 0)
            return false;

        if (!_zoneManager.TryGetPlayer(name, out var found))
        {
            ChatHelper.SendSystemMessage(invoker, $"Player \"{name}\" is not online.");
            return false;
        }

        target = found;
        return true;
    }

    private bool HandleGoto(Player invoker, string[] args)
    {
        if (args.Length == 0)
            return false;

        if (!TryFindPlayer(invoker, args, out var target))
            return true;

        RememberReturnPoint(invoker);
        MovePlayer(invoker, target.Zone, target.Position, target.Rotation);

        _chatCommandManager.LogAction(this, invoker, "Goto", target.Name.FullName, null);
        ChatHelper.SendSystemMessage(invoker, $"Teleported to {target.Name.FullName}.");
        return true;
    }

    private bool HandleSummon(Player invoker, string[] args)
    {
        if (args.Length == 0)
            return false;

        if (!TryFindPlayer(invoker, args, out var target))
            return true;

        MovePlayer(target, invoker.Zone, invoker.Position, invoker.Rotation);

        _chatCommandManager.LogAction(this, invoker, "Summon", target.Name.FullName, null);
        ChatHelper.SendSystemMessage(invoker, $"Summoned {target.Name.FullName}.");
        ChatHelper.SendSystemMessage(target, $"You were summoned by {invoker.Name.FullName}.");
        return true;
    }

    /// <summary>
    /// The player card in the panel's Players tab:
    /// <c>pinfo|guid|name|zone|x|y|z|role|coins|member|mutedUntil|frozen|godMode|snooped|packetLog</c>.
    /// </summary>
    private bool HandlePlayer(Player invoker, string[] args)
    {
        if (args.Length == 0)
            return false;

        if (!TryFindPlayer(invoker, args, out var target))
            return true;

        ChatHelper.SendGmPanelData(invoker, "pinfo",
            target.Guid, target.Name.FullName, target.Zone.Name,
            target.Position.X.ToString("0.0"), target.Position.Y.ToString("0.0"), target.Position.Z.ToString("0.0"),
            target.ChatCommandRole, target.Coins, target.MembershipStatus != 0 ? 1 : 0,
            target.IsMuted() ? $"{target.MutedUntil:u}" : string.Empty,
            target.FrozenMovementSpeed is null ? 0 : 1,
            target.GodMode ? 1 : 0,
            target.Snoopers.Contains(invoker.Guid) ? 1 : 0,
            target.PacketLogger is null ? 0 : 1);

        return true;
    }

    /// <summary>
    /// Pulls every player in your zone (referees excluded) to you, or only those within a radius; for starting events.
    /// </summary>
    private bool HandleGather(Player invoker, string[] args)
    {
        var radius = float.MaxValue;

        if (args.Length > 1 || args.Length == 1 && (!float.TryParse(args[0], NumberStyles.Float, CultureInfo.InvariantCulture, out radius) || radius <= 0))
            return false;

        var center = new Vector3(invoker.Position.X, invoker.Position.Y, invoker.Position.Z);

        var gathered = invoker.Zone.Players
            .Where(player => player != invoker && !player.IsMod && !player.IsAdmin)
            .Where(player => Vector3.Distance(new Vector3(player.Position.X, player.Position.Y, player.Position.Z), center) <= radius)
            .ToList();

        foreach (var player in gathered)
        {
            MovePlayer(player, invoker.Zone, invoker.Position, invoker.Rotation);
            ChatHelper.SendSystemMessage(player, $"You were gathered by {invoker.Name.FullName}.");
        }

        _chatCommandManager.LogAction(this, invoker, "Gather", null, $"Players: {gathered.Count}, Radius: {(args.Length == 0 ? "zone" : radius)}");
        ChatHelper.SendSystemMessage(invoker, $"Gathered {gathered.Count} player(s).");
        return true;
    }

    private static void RememberReturnPoint(Player invoker)
    {
        invoker.ReturnPoint = (invoker.Zone.DefinitionId, invoker.Zone.OwnerId, invoker.Position, invoker.Rotation);
    }

    private bool HandleBack(Player invoker)
    {
        if (invoker.ReturnPoint is not { } point)
        {
            ChatHelper.SendSystemMessage(invoker, "Nowhere to go back to yet. !gm goto and !gm go remember where you were.");
            return true;
        }

        RememberReturnPoint(invoker);

        if (point.ZoneDefinitionId == invoker.Zone.DefinitionId && point.OwnerId == invoker.Zone.OwnerId)
            MovePlayer(invoker, invoker.Zone, point.Position, point.Rotation);
        else if (!invoker.TeleportToZone(point.ZoneDefinitionId, point.OwnerId, point.Position, point.Rotation))
            ChatHelper.SendSystemMessage(invoker, "That zone isn't available any more.");

        return true;
    }

    // bookmarks|name:zoneId:x:y:z|...
    private void SendBookmarks(Player invoker)
    {
        ChatHelper.SendGmPanelData(invoker, "bookmarks", [.. _bookmarks.GetAll().Select(x => string.Join(':', x.Name, x.ZoneDefinitionId,
            x.X.ToString("0", CultureInfo.InvariantCulture),
            x.Y.ToString("0", CultureInfo.InvariantCulture),
            x.Z.ToString("0", CultureInfo.InvariantCulture)))]);
    }

    /// <summary>
    /// False when no name was given; a null name back means it was invalid and the referee has been told why.
    /// </summary>
    private static bool TryParseBookmarkName(Player invoker, string[] args, out string? name)
    {
        name = null;

        if (args.Length == 0)
            return false;

        name = RefereeBookmarks.NormalizeName(args);

        if (name is null)
            ChatHelper.SendSystemMessage(invoker, $"Bookmark names are 1 to {RefereeBookmarks.MaxNameLength} characters, without ':'.");

        return true;
    }

    private bool HandleMark(Player invoker, string[] args)
    {
        if (!TryParseBookmarkName(invoker, args, out var name))
            return false;

        if (name is null)
            return true;

        if (invoker.Zone.OwnerId is not null)
        {
            ChatHelper.SendSystemMessage(invoker, "Bookmarks only work in world zones, not houses or instances.");
            return true;
        }

        var saved = _bookmarks.Set(new RefereeBookmark
        {
            Name = name,
            ZoneDefinitionId = invoker.Zone.DefinitionId,
            X = invoker.Position.X,
            Y = invoker.Position.Y,
            Z = invoker.Position.Z,
            RotationX = invoker.Rotation.X,
            RotationY = invoker.Rotation.Y,
            RotationZ = invoker.Rotation.Z,
            RotationW = invoker.Rotation.W,
            CreatedBy = invoker.Name.FullName
        });

        if (!saved)
        {
            ChatHelper.SendSystemMessage(invoker, "Couldn't save the bookmark; see the server log.");
            return true;
        }

        _chatCommandManager.LogAction(this, invoker, "Mark", name, $"Zone: {invoker.Zone.DefinitionId}, Position: {invoker.Position}");
        ChatHelper.SendSystemMessage(invoker, $"Bookmarked this spot as \"{name}\". !gm go {name} comes back here.");
        SendBookmarks(invoker);
        return true;
    }

    private bool HandleUnmark(Player invoker, string[] args)
    {
        if (!TryParseBookmarkName(invoker, args, out var name))
            return false;

        if (name is null)
            return true;

        if (!_bookmarks.Remove(name))
        {
            ChatHelper.SendSystemMessage(invoker, $"No bookmark named \"{name}\".");
            return true;
        }

        _chatCommandManager.LogAction(this, invoker, "Unmark", name, null);
        ChatHelper.SendSystemMessage(invoker, $"Removed bookmark \"{name}\".");
        SendBookmarks(invoker);
        return true;
    }

    private bool HandleGo(Player invoker, string[] args)
    {
        if (!TryParseBookmarkName(invoker, args, out var name))
            return false;

        if (name is null)
            return true;

        if (!_bookmarks.TryGet(name, out var bookmark))
        {
            ChatHelper.SendSystemMessage(invoker, $"No bookmark named \"{name}\". !gm marks lists them.");
            return true;
        }

        var position = new Vector4(bookmark.X, bookmark.Y, bookmark.Z, 1f);
        var rotation = new Quaternion(bookmark.RotationX, bookmark.RotationY, bookmark.RotationZ, bookmark.RotationW);

        RememberReturnPoint(invoker);

        if (bookmark.ZoneDefinitionId == invoker.Zone.DefinitionId && invoker.Zone.OwnerId is null)
            MovePlayer(invoker, invoker.Zone, position, rotation);
        else if (!invoker.TeleportToZone(bookmark.ZoneDefinitionId, null, position, rotation))
            ChatHelper.SendSystemMessage(invoker, $"Couldn't enter zone {bookmark.ZoneDefinitionId}.");

        _chatCommandManager.LogAction(this, invoker, "Go", name, null);
        return true;
    }

    // events|key:1|key:0|...
    private void SendEvents(Player invoker)
    {
        ChatHelper.SendGmPanelData(invoker, "events",
            [.. _worldEventManager.GetAll().OrderBy(x => x.Key).Select(x => $"{x.Key}:{(x.Value ? 1 : 0)}")]);
    }

    private static void MovePlayer(Player player, IZone zone, Vector4 position, Quaternion rotation)
    {
        if (player.Zone != zone)
        {
            player.TeleportToZone(zone.DefinitionId, zone.OwnerId, position, rotation);
            return;
        }

        player.UpdatePosition(position, rotation);

        player.SendTunneled(new ClientUpdatePacketUpdateLocation
        {
            Position = position,
            Rotation = rotation,
            Teleport = true
        });
    }
}
