using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

using Sanctuary.Game;
using Sanctuary.Game.Authoring;
using Sanctuary.Game.ChatCommands;
using Sanctuary.Game.Entities;
using Sanctuary.Game.Zones;

namespace Sanctuary.Gateway.Admin;

/// <summary>
/// GM scripts on the NPCs and props referees placed, and the script library: what the in-game GM panel's Script tab
/// does with !npc script, !prop script and !scriptlib, for the zone editor. Admins only, as in game; scripts compile
/// and run before they're saved, so a broken one never replaces a working one.
/// </summary>
public static partial class AdminApiEndpoints
{
    private static void MapScriptEndpoints(RouteGroupBuilder referee)
    {
        referee.MapGet("/zones/{key}/objects/{id}/script", ScriptHandler);
        referee.MapPut("/zones/{key}/objects/{id}/script", SaveScriptHandlerAsync);
        referee.MapDelete("/zones/{key}/objects/{id}/script", ClearScriptHandlerAsync);
        referee.MapPost("/zones/{key}/objects/{id}/script/use", UseScriptHandlerAsync);
        referee.MapPost("/zones/{key}/objects/{id}/script/test", TestScriptHandlerAsync);

        referee.MapGet("/scripts/library", LibraryHandler);
        referee.MapGet("/scripts/library/{name}", LibraryEntryHandler);
        referee.MapPut("/scripts/library/{name}", SaveLibraryEntryHandlerAsync);
        referee.MapDelete("/scripts/library/{name}", DeleteLibraryEntryHandler);
    }

    private static IResult AdminsOnly() => Results.Problem("Only admins can edit scripts.", statusCode: StatusCodes.Status403Forbidden);

    /// <summary>
    /// A world NPC (placed models included) or prop, the only things GM scripts go on.
    /// </summary>
    private static bool TryGetScriptTarget(IZoneManager zoneManager, string key, string id, out WorldZone worldZone, out Npc npc, out string kind)
    {
        kind = string.Empty;

        if (!TryGetObject(zoneManager, key, id, out worldZone, out _, out npc))
            return false;

        if (worldZone.IsWorldFixture(npc.Guid))
            kind = "prop";
        else if (worldZone.IsWorldNpc(npc.Guid))
            kind = "npc";

        return kind.Length > 0;
    }

    private static ScriptResult Result(Npc npc, bool ok, string message) => new(ok, message, npc.GmScript is not null);

    private static IResult ScriptHandler(HttpContext context, string key, string id, IZoneManager zoneManager)
    {
        if (!GetReferee(context).IsAdmin)
            return AdminsOnly();

        if (!TryGetScriptTarget(zoneManager, key, id, out var worldZone, out var npc, out var kind))
            return Results.NotFound("Only NPCs and props placed in a world zone can have scripts.");

        var saved = worldZone.GetSavedScript(npc.Guid) ?? string.Empty;

        return Results.Ok(new ScriptModel(kind, saved, saved.Length > 0 && npc.GmScript?.Source == saved));
    }

    private static async Task<IResult> SaveScriptHandlerAsync(HttpContext context, string key, string id, ScriptRequest request, IZoneManager zoneManager)
    {
        var referee = GetReferee(context);

        if (!referee.IsAdmin)
            return AdminsOnly();

        if (!TryGetScriptTarget(zoneManager, key, id, out var worldZone, out var npc, out _))
            return Results.NotFound("Only NPCs and props placed in a world zone can have scripts.");

        if (string.IsNullOrWhiteSpace(request.Source))
            return Results.BadRequest("Write a script first, or remove it.");

        if (!ScriptCommandHelper.TryFillDefaults(request.Source, out var filled, out var fillError))
            return Results.Ok(Result(npc, false, fillError!));

        var (saved, error) = await ScriptCommandHelper.ApplyAsync(worldZone, npc, filled);

        if (saved)
            Audit(referee, "ScriptSave", npc.Name, $"Zone: {worldZone.Name}, Guid: {npc.Guid}, Length: {filled.Length}");

        return Results.Ok(Result(npc, saved && error is null, error ?? "Saved and running."));
    }

    private static async Task<IResult> ClearScriptHandlerAsync(HttpContext context, string key, string id, IZoneManager zoneManager)
    {
        var referee = GetReferee(context);

        if (!referee.IsAdmin)
            return AdminsOnly();

        if (!TryGetScriptTarget(zoneManager, key, id, out var worldZone, out var npc, out _))
            return Results.NotFound("Only NPCs and props placed in a world zone can have scripts.");

        var (saved, error) = await ScriptCommandHelper.ApplyAsync(worldZone, npc, null);

        if (saved)
            Audit(referee, "ScriptClear", npc.Name, $"Zone: {worldZone.Name}, Guid: {npc.Guid}");

        return Results.Ok(Result(npc, saved, error ?? "Script removed."));
    }

    private static async Task<IResult> UseScriptHandlerAsync(HttpContext context, string key, string id, UseScriptRequest request,
        IZoneManager zoneManager, IScriptLibrary scriptLibrary)
    {
        var referee = GetReferee(context);

        if (!referee.IsAdmin)
            return AdminsOnly();

        if (!TryGetScriptTarget(zoneManager, key, id, out var worldZone, out var npc, out _))
            return Results.NotFound("Only NPCs and props placed in a world zone can have scripts.");

        var values = new Dictionary<string, string>(request.Values ?? [], StringComparer.OrdinalIgnoreCase);

        if (!ScriptCommandHelper.TryRenderLibraryScript(scriptLibrary, request.Name, values, out var source, out var renderError))
            return Results.Ok(Result(npc, false, renderError!));

        var (saved, error) = await ScriptCommandHelper.ApplyAsync(worldZone, npc, source);

        if (saved)
            Audit(referee, "ScriptUse", npc.Name, $"Zone: {worldZone.Name}, Guid: {npc.Guid}, Library: {request.Name}");

        return Results.Ok(Result(npc, saved && error is null, error ?? $"{request.Name} is on it and running."));
    }

    /// <summary>
    /// Runs onSpawn and then onInteract with the referee's own character as the player, as the GM panel's Test does:
    /// what the hooks do (items given, players moved) really happens.
    /// </summary>
    private static async Task<IResult> TestScriptHandlerAsync(HttpContext context, string key, string id, IZoneManager zoneManager,
        RefereeDirectory directory)
    {
        var referee = GetReferee(context);

        if (!referee.IsAdmin)
            return AdminsOnly();

        if (!TryGetScriptTarget(zoneManager, key, id, out var worldZone, out var npc, out _))
            return Results.NotFound("Only NPCs and props placed in a world zone can have scripts.");

        if (directory.FindOnlineCharacter(referee) is not { } player)
            return Results.Conflict("A test runs onInteract on your character, and none of yours is in the world.");

        if (!ReferenceEquals(player.Zone, worldZone))
            return Results.Conflict($"A test runs onInteract on your character, which is in {player.Zone.Name}, not {worldZone.Name}.");

        var error = await worldZone.NpcContent.TestScriptAsync(npc, player);

        return Results.Ok(Result(npc, error is null, error ?? "Ran onSpawn and onInteract without errors."));
    }

    private static IResult LibraryHandler(HttpContext context, string? search, IScriptLibrary scriptLibrary)
    {
        if (!GetReferee(context).IsAdmin)
            return AdminsOnly();

        return Results.Ok(scriptLibrary.GetAll(search).Select(entry => ToModel(entry, false)));
    }

    private static IResult LibraryEntryHandler(HttpContext context, string name, IScriptLibrary scriptLibrary)
    {
        if (!GetReferee(context).IsAdmin)
            return AdminsOnly();

        return scriptLibrary.TryGet(name, out var entry)
            ? Results.Ok(ToModel(entry, true))
            : Results.NotFound($"No library script named {name}.");
    }

    /// <summary>
    /// Saves a referee's library script, if it compiles with its parameter defaults filled in. Templates are read-only.
    /// </summary>
    private static async Task<IResult> SaveLibraryEntryHandlerAsync(HttpContext context, string name, LibrarySaveRequest request,
        IZoneManager zoneManager, IScriptLibrary scriptLibrary, RefereeDirectory directory)
    {
        var referee = GetReferee(context);

        if (!referee.IsAdmin)
            return AdminsOnly();

        if (!ScriptTemplate.IsValidName(name))
            return Results.BadRequest("Library names are 1-32 letters, digits, - and _.");

        if (string.IsNullOrWhiteSpace(request.Source))
            return Results.BadRequest("Write a script first.");

        // The compile check runs on a stand-in NPC in a running zone; any will do.
        if (zoneManager.Zones.OfType<WorldZone>().FirstOrDefault() is not { } zone)
            return Results.Conflict("No world zone is running to check the script in.");

        if (await ScriptCommandHelper.CheckLibraryScriptAsync(zone, name, request.Source, request.Description) is { } checkError)
            return Results.Ok(new ScriptResult(false, checkError, false));

        if (!scriptLibrary.TrySave(name, request.Source, request.Description, directory.FindOnlineCharacter(referee)?.CharacterId, out var saveError))
            return Results.Ok(new ScriptResult(false, saveError!, false));

        Audit(referee, "ScriptLibrarySave", name, $"Length: {request.Source.Length}");

        return Results.Ok(new ScriptResult(true, $"Saved {name} to the library.", false));
    }

    private static IResult DeleteLibraryEntryHandler(HttpContext context, string name, IScriptLibrary scriptLibrary)
    {
        var referee = GetReferee(context);

        if (!referee.IsAdmin)
            return AdminsOnly();

        if (!scriptLibrary.TryDelete(name, out var error))
            return Results.BadRequest(error);

        Audit(referee, "ScriptLibraryDelete", name, null);

        return Results.NoContent();
    }

    private static LibraryEntryModel ToModel(ScriptTemplate entry, bool withSource) =>
        new(entry.Name, entry.Description, entry.IsTemplate,
            entry.Params.Select(param => new ScriptParamModel(param.Key, param.Type, param.Default)).ToList(),
            withSource ? entry.Source : null);
}
