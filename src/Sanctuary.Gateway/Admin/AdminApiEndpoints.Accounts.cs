using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

using Sanctuary.Core.Helpers;
using Sanctuary.Database;
using Sanctuary.Database.Entities;
using Sanctuary.Game;
using Sanctuary.Game.ChatCommands;
using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;
using Sanctuary.Game.Resources.Definitions.Rewards;
using Sanctuary.Packet.Common;

namespace Sanctuary.Gateway.Admin;

/// <summary>
/// Accounts and characters, online or not: finding them, banning, muting and roles (as !mod does), a character's
/// inventory and mounts, and giving or taking items and coins.
/// </summary>
public static partial class AdminApiEndpoints
{
    private const int MaxAccounts = 100;
    private const int MaxItemResults = 60;
    private const int MaxMuteMinutes = 60 * 24 * 365;

    private static void MapAccountEndpoints(RouteGroupBuilder referee)
    {
        referee.MapGet("/accounts", AccountsHandler);
        referee.MapGet("/accounts/{id}", AccountHandler);
        referee.MapPut("/accounts/{id}/ban", BanHandler);
        referee.MapDelete("/accounts/{id}/ban", UnbanHandler);
        referee.MapPut("/accounts/{id}/mute", MuteHandler);
        referee.MapDelete("/accounts/{id}/mute", UnmuteHandler);
        referee.MapPut("/accounts/{id}/role", RoleHandler);

        referee.MapGet("/characters/{id}", CharacterHandler);
        referee.MapPost("/characters/{id}/items", GiveItemHandler);
        referee.MapPost("/characters/{id}/items/take", TakeItemHandler);
        referee.MapPost("/characters/{id}/coins", CoinsHandler);

        referee.MapGet("/items", ItemsHandler);
    }

    #region Accounts

    /// <summary>
    /// Accounts whose username or a character's name has <paramref name="search"/> in it, last signed in first.
    /// </summary>
    private static AccountModel[] AccountsHandler(string? search, IDbContextFactory<DatabaseContext> dbContextFactory,
        IZoneManager zoneManager)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        var users = dbContext.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_")}%";

            users = users.Where(user => EF.Functions.Like(user.Username, pattern, "\\")
                || user.Characters.Any(character => character.FullName != null && EF.Functions.Like(character.FullName, pattern, "\\")));
        }

        // SQLite orders by neither a DateTimeOffset nor a ulong, so the matches are sorted here.
        var online = OnlineByCharacter(zoneManager);

        return LoadAccounts(users, online)
            .OrderByDescending(account => account.Characters.Any(character => character.Online is not null))
            .ThenByDescending(account => account.LastLogin ?? account.Created)
            .Take(MaxAccounts)
            .ToArray();
    }

    private static IResult AccountHandler(string id, IDbContextFactory<DatabaseContext> dbContextFactory, IZoneManager zoneManager) =>
        FindAccount(id, dbContextFactory, zoneManager) is { } account ? Results.Ok(account) : Results.NotFound("There's no such account.");

    /// <summary>
    /// Locks an account for <see cref="BanRequest.Minutes"/>, or for good, and kicks its characters.
    /// </summary>
    private static IResult BanHandler(HttpContext context, string id, BanRequest request, IDbContextFactory<DatabaseContext> dbContextFactory,
        IZoneManager zoneManager)
    {
        if (request.Minutes is <= 0)
            return Results.BadRequest("A ban lasts at least a minute; leave the length empty to ban for good.");

        var referee = GetReferee(context);

        if (!TryGetTarget(referee, id, "ban", dbContextFactory, out var target, out var problem))
            return problem;

        var until = request.Minutes is { } minutes ? DateTimeOffset.UtcNow.AddMinutes(minutes) : DateTimeOffset.MaxValue;

        using (var dbContext = dbContextFactory.CreateDbContext())
        {
            dbContext.Users.Where(user => user.Id == target.Id).ExecuteUpdate(user => user.SetProperty(u => u.LockedUntil, until));
        }

        foreach (var player in OnlinePlayers(zoneManager, target.CharacterIds))
        {
            ChatHelper.SendSystemMessage(player, "Your account has been banned.");
            player.Disconnect();
        }

        Audit(referee, "Ban", target.Username, (request.Minutes is null ? "Permanent" : $"Until: {until:u}") + Reason(request.Reason));

        return Results.Ok(FindAccount(id, dbContextFactory, zoneManager));
    }

    private static IResult UnbanHandler(HttpContext context, string id, IDbContextFactory<DatabaseContext> dbContextFactory, IZoneManager zoneManager)
    {
        var referee = GetReferee(context);

        if (!TryGetTarget(referee, id, "unban", dbContextFactory, out var target, out var problem))
            return problem;

        using (var dbContext = dbContextFactory.CreateDbContext())
        {
            dbContext.Users.Where(user => user.Id == target.Id).ExecuteUpdate(user => user.SetProperty(u => u.LockedUntil, (DateTimeOffset?)null));
        }

        Audit(referee, "Unban", target.Username, null);

        return Results.Ok(FindAccount(id, dbContextFactory, zoneManager));
    }

    /// <summary>
    /// Stops an account's characters chatting for <see cref="MuteRequest.Minutes"/>, at once for those online.
    /// </summary>
    private static IResult MuteHandler(HttpContext context, string id, MuteRequest request, IDbContextFactory<DatabaseContext> dbContextFactory,
        IZoneManager zoneManager)
    {
        if (request.Minutes is <= 0 or > MaxMuteMinutes)
            return Results.BadRequest("A mute lasts from a minute to a year.");

        var referee = GetReferee(context);

        if (!TryGetTarget(referee, id, "mute", dbContextFactory, out var target, out var problem))
            return problem;

        var until = DateTimeOffset.UtcNow.AddMinutes(request.Minutes);

        using (var dbContext = dbContextFactory.CreateDbContext())
        {
            dbContext.Users.Where(user => user.Id == target.Id).ExecuteUpdate(user => user.SetProperty(u => u.MutedUntil, until));
        }

        foreach (var player in OnlinePlayers(zoneManager, target.CharacterIds))
        {
            player.MutedUntil = until;
            ChatHelper.SendSystemMessage(player, $"You have been muted until {until:u}.");
        }

        Audit(referee, "Mute", target.Username, $"Until: {until:u}{Reason(request.Reason)}");

        return Results.Ok(FindAccount(id, dbContextFactory, zoneManager));
    }

    private static IResult UnmuteHandler(HttpContext context, string id, IDbContextFactory<DatabaseContext> dbContextFactory, IZoneManager zoneManager)
    {
        var referee = GetReferee(context);

        if (!TryGetTarget(referee, id, "unmute", dbContextFactory, out var target, out var problem))
            return problem;

        using (var dbContext = dbContextFactory.CreateDbContext())
        {
            dbContext.Users.Where(user => user.Id == target.Id).ExecuteUpdate(user => user.SetProperty(u => u.MutedUntil, (DateTimeOffset?)null));
        }

        foreach (var player in OnlinePlayers(zoneManager, target.CharacterIds))
        {
            player.MutedUntil = null;
            ChatHelper.SendSystemMessage(player, "You can chat again.");
        }

        Audit(referee, "Unmute", target.Username, null);

        return Results.Ok(FindAccount(id, dbContextFactory, zoneManager));
    }

    /// <summary>
    /// Makes an account a player, mod or admin (admins only, and not for another admin). Characters online get the
    /// role's commands at once.
    /// </summary>
    private static IResult RoleHandler(HttpContext context, string id, RoleRequest request, IDbContextFactory<DatabaseContext> dbContextFactory,
        IZoneManager zoneManager)
    {
        var referee = GetReferee(context);

        if (!referee.IsAdmin)
            return Results.StatusCode(StatusCodes.Status403Forbidden);

        if (!Enum.TryParse<ChatCommandRole>(request.Role, ignoreCase: true, out var role)
            || role is not (ChatCommandRole.Player or ChatCommandRole.Mod or ChatCommandRole.Admin))
            return Results.BadRequest("A role is Player, Mod or Admin.");

        if (!TryGetTarget(referee, id, "change the role of", dbContextFactory, out var target, out var problem))
            return problem;

        var isAdmin = role == ChatCommandRole.Admin;
        var isMod = role == ChatCommandRole.Mod;

        using (var dbContext = dbContextFactory.CreateDbContext())
        {
            dbContext.Users.Where(user => user.Id == target.Id)
                .ExecuteUpdate(user => user.SetProperty(u => u.IsAdmin, isAdmin).SetProperty(u => u.IsMod, isMod));
        }

        foreach (var player in OnlinePlayers(zoneManager, target.CharacterIds))
        {
            player.IsAdmin = isAdmin;
            player.IsMod = isMod;
        }

        Audit(referee, "Role", target.Username, $"Role: {role}");

        return Results.Ok(FindAccount(id, dbContextFactory, zoneManager));
    }

    private sealed record TargetAccount(ulong Id, string Username, ulong[] CharacterIds);

    /// <summary>
    /// The account a moderation acts on: not the referee's own, and of a lower role than theirs.
    /// </summary>
    private static bool TryGetTarget(Referee referee, string id, string verb, IDbContextFactory<DatabaseContext> dbContextFactory,
        out TargetAccount target, out IResult problem)
    {
        target = null!;
        problem = Results.NotFound("There's no such account.");

        if (!ulong.TryParse(id, out var userId))
            return false;

        using var dbContext = dbContextFactory.CreateDbContext();

        var user = dbContext.Users.AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => new { user.Id, user.Username, user.IsAdmin, user.IsMod, CharacterIds = user.Characters.Select(character => character.Id).ToArray() })
            .SingleOrDefault();

        if (user is null)
            return false;

        if (user.Id == referee.Id)
        {
            problem = Results.Problem($"You can't {verb} your own account.", statusCode: StatusCodes.Status403Forbidden);
            return false;
        }

        if (AccountRole(user.IsAdmin, user.IsMod) >= referee.Role)
        {
            problem = Results.Problem($"You can't {verb} {user.Username}: their role is as high as yours.", statusCode: StatusCodes.Status403Forbidden);
            return false;
        }

        target = new TargetAccount(user.Id, user.Username, user.CharacterIds);
        return true;
    }

    private static AccountModel? FindAccount(string id, IDbContextFactory<DatabaseContext> dbContextFactory, IZoneManager zoneManager)
    {
        if (!ulong.TryParse(id, out var userId))
            return null;

        using var dbContext = dbContextFactory.CreateDbContext();

        return LoadAccounts(dbContext.Users.AsNoTracking().Where(user => user.Id == userId), OnlineByCharacter(zoneManager)).SingleOrDefault();
    }

    private static List<AccountModel> LoadAccounts(IQueryable<DbUser> users, Dictionary<ulong, Player> online)
    {
        var now = DateTimeOffset.UtcNow;

        return users
            .Select(user => new
            {
                user.Id,
                user.Username,
                user.IsAdmin,
                user.IsMod,
                user.Created,
                user.LastLogin,
                user.LockedUntil,
                user.MutedUntil,
                user.MaxCharacters,
                Characters = user.Characters
                    .Select(character => new
                    {
                        character.Id,
                        character.FirstName,
                        character.FullName,
                        character.Coins,
                        character.PlayTime,
                        character.Created,
                        character.LastLogin
                    })
                    .ToList()
            })
            .AsEnumerable()
            .Select(user => new AccountModel(
                Id(user.Id),
                user.Username,
                AccountRole(user.IsAdmin, user.IsMod).ToString(),
                user.Created,
                user.LastLogin,
                user.LockedUntil > now ? user.LockedUntil : null,
                user.MutedUntil > now ? user.MutedUntil : null,
                user.MaxCharacters,
                user.Characters
                    .OrderByDescending(character => character.LastLogin ?? character.Created)
                    .Select(character => new CharacterSummaryModel(
                        Id(character.Id),
                        character.FullName ?? character.FirstName,
                        online.TryGetValue(character.Id, out var player) ? player.Coins : character.Coins,
                        character.PlayTime,
                        character.Created,
                        character.LastLogin,
                        player is null ? null : ToOnlineModel(player)))
                    .ToArray()))
            .ToList();
    }

    /// <summary>
    /// The role an account has. Not <see cref="ChatHelper.GetRoleFromFlags"/>, which makes everyone an admin in
    /// debug builds.
    /// </summary>
    private static ChatCommandRole AccountRole(bool isAdmin, bool isMod) =>
        isAdmin ? ChatCommandRole.Admin : isMod ? ChatCommandRole.Mod : ChatCommandRole.Player;

    private static Dictionary<ulong, Player> OnlineByCharacter(IZoneManager zoneManager)
    {
        var online = new Dictionary<ulong, Player>();

        foreach (var player in zoneManager.Zones.SelectMany(zone => zone.Players))
            online[GuidHelper.GetPlayerId(player.Guid)] = player;

        return online;
    }

    private static IEnumerable<Player> OnlinePlayers(IZoneManager zoneManager, ulong[] characterIds)
    {
        foreach (var characterId in characterIds)
        {
            if (zoneManager.TryGetPlayer(GuidHelper.GetPlayerGuid(characterId), out var player))
                yield return player;
        }
    }

    private static OnlineModel ToOnlineModel(Player player) =>
        new(Id(player.Guid), ZoneKey(player.Zone), player.Zone.Name, Round(player.Position.X), Round(player.Position.Y), Round(player.Position.Z));

    private static string Reason(string? reason) =>
        string.IsNullOrWhiteSpace(reason) ? string.Empty : $", Reason: \"{reason.Trim()[..Math.Min(reason.Trim().Length, MaxMessageLength)]}\"";

    #endregion

    #region Characters

    /// <summary>
    /// A character, its account, and what it owns: as the game has it for one online, as saved otherwise.
    /// </summary>
    private static IResult CharacterHandler(string id, IDbContextFactory<DatabaseContext> dbContextFactory, IZoneManager zoneManager,
        IResourceManager resourceManager) =>
        FindCharacter(id, dbContextFactory, zoneManager, resourceManager) is { } character
            ? Results.Ok(character)
            : Results.NotFound("There's no such character.");

    private static IResult GiveItemHandler(HttpContext context, string id, ItemGrantRequest request, IDbContextFactory<DatabaseContext> dbContextFactory,
        IZoneManager zoneManager, IResourceManager resourceManager, IRewardManager rewardManager)
    {
        if (request.Quantity is < 1 or > 999)
            return Results.BadRequest("Give from 1 to 999 at a time.");

        if (!resourceManager.ClientItemDefinitions.TryGetValue(request.Item, out var definition))
            return Results.NotFound($"There's no item {request.Item}.");

        if (!TryGetOnlineCharacter(id, zoneManager, out var player, out var problem))
            return problem;

        if (!rewardManager.TryGrantItem(player, request.Item, request.Tint, request.Quantity))
            return Results.Problem($"Couldn't give {player.Name.FullName} item {request.Item}; see the gateway log.");

        var name = ItemName(definition);

        ChatHelper.SendSystemMessage(player, $"A referee gave you {request.Quantity}x {name}.");

        Audit(GetReferee(context), "Give Item", player.Name.FullName, $"item={request.Item}, quantity={request.Quantity}, tint={request.Tint}");

        return Results.Ok(FindCharacter(id, dbContextFactory, zoneManager, resourceManager));
    }

    /// <summary>
    /// Takes items of a kind from an online character (not ones they're wearing).
    /// </summary>
    private static IResult TakeItemHandler(HttpContext context, string id, ItemTakeRequest request, IDbContextFactory<DatabaseContext> dbContextFactory,
        IZoneManager zoneManager, IResourceManager resourceManager, IRewardManager rewardManager)
    {
        if (request.Quantity < 1)
            return Results.BadRequest("Take at least one.");

        if (!TryGetOnlineCharacter(id, zoneManager, out var player, out var problem))
            return problem;

        if (!rewardManager.TryTakeItem(player, request.Item, request.Quantity))
            return Results.Problem($"{player.Name.FullName} doesn't have {request.Quantity} of item {request.Item} that they aren't wearing.",
                statusCode: StatusCodes.Status409Conflict);

        var name = resourceManager.ClientItemDefinitions.TryGetValue(request.Item, out var definition) ? ItemName(definition) : $"item {request.Item}";

        ChatHelper.SendSystemMessage(player, $"A referee took {request.Quantity}x {name}.");

        Audit(GetReferee(context), "Take Item", player.Name.FullName, $"item={request.Item}, quantity={request.Quantity}");

        return Results.Ok(FindCharacter(id, dbContextFactory, zoneManager, resourceManager));
    }

    /// <summary>
    /// Gives coins (a positive amount) or takes them (negative): through the game for a character online, in the
    /// database otherwise.
    /// </summary>
    private static IResult CoinsHandler(HttpContext context, string id, CoinsRequest request, IDbContextFactory<DatabaseContext> dbContextFactory,
        IZoneManager zoneManager, IResourceManager resourceManager, IRewardManager rewardManager)
    {
        if (request.Amount == 0 || Math.Abs((long)request.Amount) > 10_000_000)
            return Results.BadRequest("Give or take from 1 to 10,000,000 coins.");

        if (!ulong.TryParse(id, out var characterId))
            return Results.NotFound("There's no such character.");

        string name;

        if (zoneManager.TryGetPlayer(GuidHelper.GetPlayerGuid(characterId), out var player))
        {
            var done = request.Amount > 0
                ? rewardManager.TryGrantCurrency(player, CurrencyType.Coins, request.Amount)
                : rewardManager.TryTakeCoins(player, -request.Amount);

            if (!done)
                return Results.Problem($"{player.Name.FullName} has only {player.Coins:N0} coins.", statusCode: StatusCodes.Status409Conflict);

            ChatHelper.SendSystemMessage(player, request.Amount > 0
                ? $"A referee gave you {request.Amount:N0} coins."
                : $"A referee took {-request.Amount:N0} coins.");

            name = player.Name.FullName;
        }
        else
        {
            using var dbContext = dbContextFactory.CreateDbContext();

            var character = dbContext.Characters.SingleOrDefault(character => character.Id == characterId);

            if (character is null)
                return Results.NotFound("There's no such character.");

            if (character.Coins + (long)request.Amount < 0)
                return Results.Problem($"{character.FullName} has only {character.Coins:N0} coins.", statusCode: StatusCodes.Status409Conflict);

            character.Coins += request.Amount;
            dbContext.SaveChanges();

            name = character.FullName ?? character.FirstName;
        }

        Audit(GetReferee(context), request.Amount > 0 ? "Give Coins" : "Take Coins", name, $"Amount: {Math.Abs(request.Amount)}");

        return Results.Ok(FindCharacter(id, dbContextFactory, zoneManager, resourceManager));
    }

    private static bool TryGetOnlineCharacter(string id, IZoneManager zoneManager, out Player player, out IResult problem)
    {
        player = null!;
        problem = Results.NotFound("There's no such character.");

        if (!ulong.TryParse(id, out var characterId))
            return false;

        if (!zoneManager.TryGetPlayer(GuidHelper.GetPlayerGuid(characterId), out player!))
        {
            problem = Results.Problem("They have to be online for that.", statusCode: StatusCodes.Status409Conflict);
            return false;
        }

        return true;
    }

    private static CharacterModel? FindCharacter(string id, IDbContextFactory<DatabaseContext> dbContextFactory, IZoneManager zoneManager,
        IResourceManager resourceManager)
    {
        if (!ulong.TryParse(id, out var characterId))
            return null;

        using var dbContext = dbContextFactory.CreateDbContext();

        var character = dbContext.Characters.AsNoTracking()
            .Where(character => character.Id == characterId)
            .Select(character => new
            {
                character.Id,
                character.FirstName,
                character.FullName,
                character.Coins,
                character.PlayTime,
                character.Created,
                character.LastLogin,
                User = new { character.User.Id, character.User.Username, character.User.IsAdmin, character.User.IsMod, character.User.LockedUntil, character.User.MutedUntil },
                Items = character.Items.Select(item => new { item.Id, item.Definition, item.Count, item.Tint, Equipped = item.Profiles.Any() }).ToList(),
                Mounts = character.Mounts.Select(mount => new { mount.Id, mount.Definition, mount.IsUpgraded, mount.Tint }).ToList()
            })
            .SingleOrDefault();

        if (character is null)
            return null;

        var now = DateTimeOffset.UtcNow;

        zoneManager.TryGetPlayer(GuidHelper.GetPlayerGuid(characterId), out var player);

        // Online, the game's own copy is the one that's current.
        var items = player is null
            ? character.Items.Select(item => (item.Id, item.Definition, item.Count, item.Tint, item.Equipped)).ToList()
            : player.Items.Select(item => (item.Id, item.Definition, item.Count, item.Tint, Equipped: player.IsItemEquipped(item.Id))).ToList();

        return new CharacterModel(
            Id(character.Id),
            character.FullName ?? character.FirstName,
            new AccountRefModel(
                Id(character.User.Id),
                character.User.Username,
                AccountRole(character.User.IsAdmin, character.User.IsMod).ToString(),
                character.User.LockedUntil > now ? character.User.LockedUntil : null,
                character.User.MutedUntil > now ? character.User.MutedUntil : null),
            player?.Coins ?? character.Coins,
            player?.PlayTime ?? character.PlayTime,
            character.Created,
            character.LastLogin,
            player is null ? null : ToOnlineModel(player),
            items
                .Select(item =>
                {
                    resourceManager.ClientItemDefinitions.TryGetValue(item.Definition, out var definition);

                    return new InventoryItemModel(
                        item.Id,
                        item.Definition,
                        definition is null ? $"item {item.Definition}" : ItemName(definition),
                        item.Count,
                        item.Tint,
                        item.Equipped,
                        definition?.Icon.Id ?? 0,
                        item.Tint != 0 ? item.Tint : definition?.Icon.TintId ?? 0);
                })
                .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            character.Mounts
                .Select(mount =>
                {
                    resourceManager.Mounts.TryGetValue(mount.Definition, out var definition);

                    return new OwnedMountModel(
                        mount.Id,
                        mount.Definition,
                        definition is null ? $"mount {mount.Definition}" : GameNameHelper.Get(definition.NameId, $"mount {mount.Definition}"),
                        mount.IsUpgraded,
                        definition?.ImageSetId ?? 0,
                        mount.Tint != 0 ? mount.Tint : definition?.TintId ?? 0);
                })
                .OrderBy(mount => mount.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray());
    }

    #endregion

    #region Items

    /// <summary>
    /// Items whose name (or id) matches <paramref name="search"/>: those starting with it first.
    /// </summary>
    private static ItemSearchModel[] ItemsHandler(string? search, IResourceManager resourceManager)
    {
        if (string.IsNullOrWhiteSpace(search))
            return [];

        search = search.Trim();

        if (int.TryParse(search, out var itemId))
        {
            return resourceManager.ClientItemDefinitions.TryGetValue(itemId, out var definition)
                ? [ToItemSearchModel(definition)]
                : [];
        }

        return resourceManager.ClientItemDefinitions.Values
            .Select(definition => (Definition: definition, Name: ItemName(definition)))
            .Where(x => x.Name.Contains(search, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.Name.StartsWith(search, StringComparison.OrdinalIgnoreCase))
            .ThenBy(x => x.Name.Length)
            .ThenBy(x => x.Definition.Id)
            .Take(MaxItemResults)
            .Select(x => ToItemSearchModel(x.Definition))
            .ToArray();
    }

    private static ItemSearchModel ToItemSearchModel(ClientItemDefinition definition) =>
        new(definition.Id, ItemName(definition), definition.Slot, definition.Rarity, definition.MaxStackSize, definition.Icon.Id, definition.Icon.TintId);

    private static string ItemName(ClientItemDefinition definition) =>
        GameNameHelper.Get(definition.NameId) ?? definition.Comment ?? $"item {definition.Id}";

    #endregion
}
