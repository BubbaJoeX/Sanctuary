using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

using Sanctuary.Core.Helpers;
using Sanctuary.Game;
using Sanctuary.Game.Authoring;
using Sanctuary.Game.ChatCommands;
using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;
using Sanctuary.Game.Zones;
using Sanctuary.Packet;

namespace Sanctuary.Gateway.Admin;

/// <summary>
/// The in-game referee tools that make sense without being in the world: what the GM panel's player card does to an
/// online player (their quests, pets, titles, jobs, collections, tutorials, health, flying, packet log, minigames,
/// coins), freezing them, a zone's countdown, freeze, gather, client areas and hosted events, granting houses, and
/// redeem codes. Commands run as the in-game ones do (TryHandleFor) with the referee's role, their answers come back
/// here instead of to the player, and the audit log names the referee.
/// </summary>
public static partial class AdminApiEndpoints
{
    private const float GatherSpread = 4f;

    // The player card's commands, each with what may follow it: a list of first words, or anything (null).
    private static readonly Dictionary<string, string[]?> PlayerCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        ["quest"] = ["list", "info", "find", "start", "skip", "finish", "reset"],
        ["pet"] = ["list", "find", "give"],
        ["title"] = ["list", "grant", "revoke"],
        ["profile"] = ["list", "grant", "level"],
        ["collection"] = ["status", "grant", "reset"],
        ["tutorial"] = null,
        ["fte"] = null,
        ["kill"] = null,
        ["fly"] = ["on", "off"],
        ["stat"] = null,
        ["packetlog"] = ["on", "off"],
        ["minigame"] = null,
        ["coins"] = null,
        ["give"] = null,
        ["reward"] = null,
    };

    // A zone's tools: what may run with someone in it standing in for the referee.
    private static readonly Dictionary<string, string[]?> ZoneCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        ["countdown"] = null,
        ["freeze"] = ["zone"],
        ["area"] = ["list", "client"],
        ["host"] = ["list", "who", "close", "end", "prize", "coins"],
    };

    private static void MapRefereeEndpoints(RouteGroupBuilder referee)
    {
        referee.MapPost("/players/{id}/command", PlayerCommandHandlerAsync);
        referee.MapPut("/players/{id}/frozen", FreezeHandler);
        referee.MapPost("/players/{id}/houses", GrantHouseHandler);

        referee.MapPost("/zones/{key}/command", ZoneCommandHandlerAsync);
        referee.MapPost("/zones/{key}/gather", GatherHandler);

        referee.MapGet("/houses", HousesHandler);

        referee.MapGet("/codes", CodesHandler);
        referee.MapPost("/codes", CreateCodeHandler);
        referee.MapPut("/codes/{code}", UpdateCodeHandler);
        referee.MapDelete("/codes/{code}", DeleteCodeHandler);
    }

    #region Commands

    /// <summary>
    /// Runs one of the player card's commands on an online player, as if they typed it with the referee's role: a
    /// command's [player] left out means them.
    /// </summary>
    private static Task<IResult> PlayerCommandHandlerAsync(HttpContext context, string id, CommandRequest request, IZoneManager zoneManager,
        IChatCommandManager chatCommandManager)
    {
        if (!TryGetOnlinePlayer(zoneManager, id, out var player))
            return Task.FromResult(Results.NotFound("That player isn't online."));

        return RunForAsync(context, player, request.Command, PlayerCommands, chatCommandManager);
    }

    /// <summary>
    /// Runs one of a zone's tools there, with a player in it standing in for the referee (the commands work on the zone
    /// they're typed in); nobody in it, nothing to do.
    /// </summary>
    private static Task<IResult> ZoneCommandHandlerAsync(HttpContext context, string key, CommandRequest request, IZoneManager zoneManager,
        IChatCommandManager chatCommandManager)
    {
        if (!TryGetZone(zoneManager, key, out var zone))
            return Task.FromResult(Results.NotFound("That zone isn't running."));

        // A player rather than a referee, so freezing the zone doesn't pass over the one it runs for.
        var player = zone.Players.OrderBy(x => x.IsMod || x.IsAdmin).FirstOrDefault();

        if (player is null)
            return Task.FromResult(Results.Conflict($"Nobody is in {zone.Name}."));

        return RunForAsync(context, player, request.Command, ZoneCommands, chatCommandManager);
    }

    private static async Task<IResult> RunForAsync(HttpContext context, Player player, string? command, Dictionary<string, string[]?> allowed,
        IChatCommandManager chatCommandManager)
    {
        var referee = GetReferee(context);
        var text = command?.Trim().TrimStart('!') ?? string.Empty;
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (words.Length == 0 || !allowed.TryGetValue(words[0], out var subcommands)
            || subcommands is not null && (words.Length < 2 || !subcommands.Contains(words[1], StringComparer.OrdinalIgnoreCase)))
            return Results.BadRequest($"The panel runs {string.Join(", ", allowed.Keys.Select(x => chatCommandManager.Prefix + x))} here.");

        var replies = new ConcurrentQueue<string>();

        await CommandLock.WaitAsync(context.RequestAborted);

        try
        {
            chatCommandManager.TryHandleFor(player, referee.Role, referee.Name, text, replies);

            // Some commands answer a moment later (database work, effects); give them a beat.
            await Task.Delay(250, CancellationToken.None);
        }
        finally
        {
            CommandLock.Release();
        }

        _logger.LogInformation("{Username} ran \"{Command}\" for {Character} from the admin panel.", referee.Name, text, player.Name.FullName);

        var lines = replies.Where(x => !x.StartsWith(ChatHelper.GmPanelToken, StringComparison.Ordinal)).ToList();

        return Results.Ok(new CommandResult(player.Name.FullName, true, lines));
    }

    #endregion

    #region Players

    /// <summary>
    /// Freezes a player where they stand, or lets them go, as !freeze does; they're told either way.
    /// </summary>
    private static IResult FreezeHandler(HttpContext context, string id, FrozenRequest request, IZoneManager zoneManager)
    {
        if (!TryGetOnlinePlayer(zoneManager, id, out var player))
            return Results.NotFound("That player isn't online.");

        if (request.Frozen == player.FrozenMovementSpeed is not null)
            return Results.NoContent();

        if (request.Frozen)
            FreezeChatCommand.Freeze(player);
        else
            FreezeChatCommand.Unfreeze(player);

        Audit(GetReferee(context), request.Frozen ? "Freeze" : "Unfreeze", player.Name.FullName, null);

        return Results.NoContent();
    }

    private static IResult GrantHouseHandler(HttpContext context, string id, HouseGrantRequest request, IZoneManager zoneManager,
        IResourceManager resourceManager, IHousingManager housingManager)
    {
        var referee = GetReferee(context);

        if (!referee.IsAdmin)
            return Results.Problem("Only admins can grant houses.", statusCode: StatusCodes.Status403Forbidden);

        if (!TryGetOnlinePlayer(zoneManager, id, out var player))
            return Results.NotFound("That player isn't online.");

        if (!resourceManager.Houses.TryGetValue(request.House, out var house))
            return Results.NotFound($"There's no house {request.House}.");

        if (!housingManager.TryGrantHouse(player, house, out _))
            return Results.Problem($"{player.Name.FullName} couldn't be given house {request.House}; they may have it already.",
                statusCode: StatusCodes.Status409Conflict);

        ChatHelper.SendSystemMessage(player, $"A referee gave you a house: {GameNameHelper.Get(house.NameId, $"house {house.Id}")}.");

        Audit(referee, "GrantHouse", player.Name.FullName, $"House: {house.Id}");

        return Results.NoContent();
    }

    /// <summary>
    /// Sends everyone in a zone (referees aside) to a spot, a few steps apart, on the ground there.
    /// </summary>
    private static IResult GatherHandler(HttpContext context, string key, GatherRequest request, IZoneManager zoneManager)
    {
        if (!TryGetZone(zoneManager, key, out var zone))
            return Results.NotFound("That zone isn't running.");

        var players = zone.Players.Where(player => !player.IsMod && !player.IsAdmin)
            .Where(player => request.Radius is not { } radius
                || Vector2.Distance(new Vector2(player.Position.X, player.Position.Z), new Vector2(request.X, request.Z)) <= radius)
            .ToList();

        for (var i = 0; i < players.Count; i++)
        {
            // A ring around the spot, so they don't stand in each other.
            var angle = i * MathF.Tau / Math.Max(players.Count, 1);
            var reach = players.Count > 1 ? GatherSpread : 0f;
            var x = request.X + MathF.Cos(angle) * reach;
            var z = request.Z + MathF.Sin(angle) * reach;
            var y = zone.TryGetGroundHeight(x, z, 10_000f, 20_000f, out var ground) ? ground : players[i].Position.Y;
            var position = new Vector4(x, y, z, 1f);

            players[i].UpdatePosition(position, players[i].Rotation);
            players[i].SendTunneled(new ClientUpdatePacketUpdateLocation
            {
                Position = position,
                Rotation = players[i].Rotation,
                Teleport = true
            });
        }

        Audit(GetReferee(context), "Gather", null, $"Zone: {zone.Name}, Spot: {request.X:F1}, {request.Z:F1}, Players: {players.Count}");

        return Results.Ok(new GatherResult(players.Count));
    }

    private static HouseModel[] HousesHandler(IResourceManager resourceManager) =>
        [.. resourceManager.Houses.Values
            .OrderBy(house => house.Id)
            .Select(house => new HouseModel(house.Id, GameNameHelper.Get(house.NameId, $"house {house.Id}"), house.Icon, house.ZoneId))];

    #endregion

    #region Redeem codes

    private static IResult CodesAdminsOnly() =>
        Results.Problem("Only admins can make and change redeem codes.", statusCode: StatusCodes.Status403Forbidden);

    private static IResult CodesHandler(HttpContext context, IRedeemCodeManager redeemCodeManager) =>
        GetReferee(context).IsAdmin
            ? Results.Ok(redeemCodeManager.GetAll().Select(ToCodeModel).ToArray())
            : CodesAdminsOnly();

    private static IResult CreateCodeHandler(HttpContext context, CodeCreateRequest request, IRedeemCodeManager redeemCodeManager,
        RefereeDirectory directory)
    {
        var referee = GetReferee(context);

        if (!referee.IsAdmin)
            return CodesAdminsOnly();

        var code = redeemCodeManager.Create(string.IsNullOrWhiteSpace(request.Code) ? null : request.Code, directory.FindOnlineCharacter(referee)?.CharacterId,
            out var error);

        if (code is null)
            return Results.BadRequest(error);

        Audit(referee, "CreateCode", null, $"Code: {code.Code}");

        return Results.Ok(ToCodeModel(code));
    }

    /// <summary>
    /// Sets a code's rewards and limits, as !code set and !code item do.
    /// </summary>
    private static IResult UpdateCodeHandler(HttpContext context, string code, CodeModel request, IRedeemCodeManager redeemCodeManager,
        IResourceManager resourceManager)
    {
        var referee = GetReferee(context);

        if (!referee.IsAdmin)
            return CodesAdminsOnly();

        if (request.Items.Length > RedeemCodeManager.MaxItems)
            return Results.BadRequest($"Codes give at most {RedeemCodeManager.MaxItems} different items.");

        if (request.Items.FirstOrDefault(x => !resourceManager.ClientItemDefinitions.ContainsKey(x.Id)) is { } unknown)
            return Results.BadRequest($"There's no item {unknown.Id}.");

        if (request.Items.Any(x => x.Quantity is < 1 or > 999) || request.Coins < 0 || request.MaxUses < 0 || request.PerCharacter < 0)
            return Results.BadRequest("Quantities are 1 to 999; coins and limits 0 or more.");

        if (request.Note is { Length: > 200 })
            return Results.BadRequest("Notes are at most 200 characters.");

        var error = redeemCodeManager.Update(code, redeemCode =>
        {
            redeemCode.Coins = request.Coins;
            redeemCode.MaxUses = request.MaxUses;
            redeemCode.PerCharacterLimit = request.PerCharacter;
            redeemCode.StartsAt = request.Starts;
            redeemCode.ExpiresAt = request.Expires;
            redeemCode.Enabled = request.Enabled;
            redeemCode.MembersOnly = request.MembersOnly;
            redeemCode.Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
            redeemCode.Items = CodeItem.Write([.. request.Items.Select(x => new CodeItem { ItemId = x.Id, Quantity = x.Quantity })]);
            return null;
        });

        if (error is not null)
            return Results.NotFound(error);

        Audit(referee, "EditCode", null, $"Code: {RedeemCodeManager.Normalize(code)}, Coins: {request.Coins}, Items: {request.Items.Length}, Enabled: {request.Enabled}");

        return redeemCodeManager.TryGet(code, out var saved) ? Results.Ok(ToCodeModel(saved)) : Results.NoContent();
    }

    private static IResult DeleteCodeHandler(HttpContext context, string code, IRedeemCodeManager redeemCodeManager)
    {
        var referee = GetReferee(context);

        if (!referee.IsAdmin)
            return CodesAdminsOnly();

        if (!redeemCodeManager.Delete(code))
            return Results.NotFound($"There's no code {RedeemCodeManager.Normalize(code)}.");

        Audit(referee, "DeleteCode", null, $"Code: {RedeemCodeManager.Normalize(code)}");

        return Results.NoContent();
    }

    private static CodeModel ToCodeModel(Sanctuary.Database.Entities.DbRedeemCode code) => new(
        code.Code,
        code.Coins,
        [.. CodeItem.Read(code.Items).Select(x => new CodeItemModel(x.ItemId, x.Quantity))],
        code.MaxUses,
        code.Uses,
        code.PerCharacterLimit,
        code.StartsAt,
        code.ExpiresAt,
        code.Enabled,
        code.MembersOnly,
        code.Note,
        code.Created);

    #endregion
}
