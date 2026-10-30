using System.Linq;
using System.Numerics;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

using Sanctuary.Core.Extensions;
using Sanctuary.Game;
using Sanctuary.Game.Authoring;
using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;
using Sanctuary.Game.Zones;

namespace Sanctuary.Gateway.Admin;

/// <summary>
/// The zone's own NPCs, the ones its Lua scripts spawn (src/Scripts/Zone), for the zone editor: moving, turning or
/// removing one here changes it for players at once, and the editor rewrites or removes its spawn line so the zone
/// starts that way. One can also become a world NPC, saved in the database and edited like any placed NPC. Admins
/// only, as they're the zone's content; each change is one undo step.
/// </summary>
public static partial class AdminApiEndpoints
{
    private static void MapZoneNpcEndpoints(RouteGroupBuilder referee)
    {
        referee.MapGet("/zones/{key}/zone-npcs/{id}", ZoneNpcHandler);
        referee.MapPatch("/zones/{key}/zone-npcs/{id}", MoveZoneNpcHandler);
        referee.MapDelete("/zones/{key}/zone-npcs/{id}", RemoveZoneNpcHandler);
        referee.MapPost("/zones/{key}/zone-npcs/{id}/adopt", AdoptZoneNpcHandler);
    }

    private static IResult ZoneNpcAdminsOnly() =>
        Results.Problem("Only admins can edit the zone's own NPCs.", statusCode: StatusCodes.Status403Forbidden);

    /// <summary>
    /// Where the NPC starts when the zone does, as its spawn line has it.
    /// </summary>
    private static IResult ZoneNpcHandler(string key, string id, IZoneManager zoneManager, IResourceManager resourceManager)
    {
        if (!TryGetZoneNpc(zoneManager, key, id, out _, out var npc))
            return Results.NotFound("That NPC isn't one the zone's scripts spawned, or it's gone.");

        return Results.Ok(ToZoneNpcModel(npc, resourceManager));
    }

    private static IResult MoveZoneNpcHandler(HttpContext context, string key, string id, ZoneNpcMove request, IZoneManager zoneManager,
        IResourceManager resourceManager, AdminUndoBook undoBook)
    {
        var referee = GetReferee(context);

        if (!referee.IsAdmin)
            return ZoneNpcAdminsOnly();

        if (!TryGetZoneNpc(zoneManager, key, id, out var worldZone, out var npc))
            return Results.NotFound("That NPC isn't one the zone's scripts spawned, or it's gone.");

        if (!float.IsFinite(request.X + request.Y + request.Z + request.H + request.Pitch + request.Roll))
            return Results.BadRequest("Position and angles must be numbers.");

        var before = ToZoneNpcModel(npc, resourceManager);
        var target = undoBook.Ref(npc.Guid);

        PlaceZoneNpc(npc, new Vector4(request.X, request.Y, request.Z, 1f), request.H, request.Pitch, request.Roll);

        undoBook.Push(referee, new TileUndo($"move {before.Label}", worldZone, zone =>
        {
            if (zone.TryGetNpc(target.Guid, out var moved))
                PlaceZoneNpc(moved, new Vector4(before.X, before.Y, before.Z, 1f), before.H, before.Pitch, before.Roll);
        }));

        Audit(referee, "MoveZoneNpc", before.Label,
            $"Zone: {worldZone.Name}, Guid: {npc.Guid}, Def: {npc.DefinitionId}, Position: {request.X:F2}, {request.Y:F2}, {request.Z:F2}, Heading: {request.H:F2}, Pitch: {request.Pitch:F2}, Roll: {request.Roll:F2}");

        return Results.Ok(new EditResult(id, undoBook.Count(referee, worldZone)));
    }

    private static IResult RemoveZoneNpcHandler(HttpContext context, string key, string id, IZoneManager zoneManager,
        IResourceManager resourceManager, AdminUndoBook undoBook)
    {
        var referee = GetReferee(context);

        if (!referee.IsAdmin)
            return ZoneNpcAdminsOnly();

        if (!TryGetZoneNpc(zoneManager, key, id, out var worldZone, out var npc))
            return Results.NotFound("That NPC isn't one the zone's scripts spawned, or it's gone.");

        var removed = ToZoneNpcModel(npc, resourceManager);
        var target = undoBook.Ref(npc.Guid);

        npc.Dispose();

        undoBook.Push(referee, new TileUndo($"remove {removed.Label}", worldZone, zone => RespawnZoneNpc(zone, removed, target, undoBook)));

        Audit(referee, "RemoveZoneNpc", removed.Label,
            $"Zone: {worldZone.Name}, Guid: {removed.Id}, Def: {removed.Def}, Position: {removed.X:F2}, {removed.Y:F2}, {removed.Z:F2}");

        return Results.Ok(new EditResult(null, undoBook.Count(referee, worldZone)));
    }

    /// <summary>
    /// Makes the NPC a world NPC where it stands, with its name, size and tilt, and takes the script's one away:
    /// from then on it's saved in the database and edited like any NPC placed here (its look, behaviour, what it says).
    /// </summary>
    private static IResult AdoptZoneNpcHandler(HttpContext context, string key, string id, IZoneManager zoneManager,
        IResourceManager resourceManager, RefereeDirectory directory, AdminUndoBook undoBook)
    {
        var referee = GetReferee(context);

        if (!referee.IsAdmin)
            return ZoneNpcAdminsOnly();

        if (!TryGetZoneNpc(zoneManager, key, id, out var worldZone, out var npc))
            return Results.NotFound("That NPC isn't one the zone's scripts spawned, or it's gone.");

        if (!resourceManager.Npcs.TryGetValue(npc.DefinitionId, out var definition))
            return Results.Problem($"NPC definition {npc.DefinitionId} isn't in Npcs.json.");

        var stock = ToZoneNpcModel(npc, resourceManager);

        if (!worldZone.TryCreateWorldNpc(definition, npc.SpawnPosition, npc.SpawnRotation, directory.FindOnlineCharacter(referee)?.CharacterId,
                out var adopted))
            return Results.Problem($"{stock.Label} couldn't be made a world NPC.");

        if (stock.Pitch != 0f || stock.Roll != 0f)
            NpcProperties.SetTilt(adopted, stock.Pitch, stock.Roll);

        ApplyNpc(worldZone, adopted, npc.Scale, npc.Collidable, npc.Name, npc.NameId);

        var stockRef = undoBook.Ref(npc.Guid);
        var adoptedRef = undoBook.Ref(adopted.Guid);

        npc.Dispose();

        undoBook.Push(referee, new TileUndo($"make {stock.Label} a world NPC", worldZone, zone =>
        {
            zone.TryDeleteTileEntity(adoptedRef.Guid);
            RespawnZoneNpc(zone, stock, stockRef, undoBook);
        }));

        Audit(referee, "AdoptZoneNpc", stock.Label,
            $"Zone: {worldZone.Name}, Guid: {stock.Id}, Def: {stock.Def}, World NPC: {adopted.Guid}");

        return Results.Ok(new EditResult(Id(adopted.Guid), undoBook.Count(referee, worldZone)));
    }

    /// <summary>
    /// Brings a removed one back under its old guid (NpcMovements.json keys paths by it), as its script spawned it.
    /// </summary>
    private static void RespawnZoneNpc(WorldZone zone, ZoneNpcModel npc, ObjectRef target, AdminUndoBook undoBook)
    {
        if (!ulong.TryParse(npc.Id, out var guid)
            || !zone.TrySpawnNpc(npc.Def, guid, npc.X, npc.Y, npc.Z, npc.H, npc.Pitch, npc.Roll, out var spawned))
            return;

        if (spawned is Npc respawned && respawned.Guid != target.Guid)
            undoBook.Follow(target, respawned.Guid);
    }

    /// <summary>
    /// Sets where the NPC starts (and walks back to) and puts it there for everyone in view.
    /// </summary>
    private static void PlaceZoneNpc(Npc npc, Vector4 position, float heading, float pitch, float roll)
    {
        var facing = QuaternionExtensions.FromHeading(heading);

        npc.SpawnPosition = position;
        npc.SpawnRotation = facing;
        npc.UpdatePosition(position, facing);

        // A tilt is sent when the NPC is added, so viewers get it again when it changes. Placed objects keep theirs
        // when level, as their spawn gave them one.
        Vector4? tilt = npc.Tilt is not null || pitch != 0f || roll != 0f ? new Vector4(heading, pitch, roll, 0f) : null;

        if (tilt?.Y != npc.Tilt?.Y || tilt?.Z != npc.Tilt?.Z)
            NpcProperties.Change(npc, () => npc.Tilt = tilt);
        else
            npc.Tilt = tilt;
    }

    private static ZoneNpcModel ToZoneNpcModel(Npc npc, IResourceManager resourceManager)
    {
        var definition = resourceManager.Npcs.TryGetValue(npc.DefinitionId, out var found) ? found : null;

        return new ZoneNpcModel(
            Id(npc.Guid),
            npc.Name ?? definition?.Name ?? $"NPC {npc.DefinitionId}",
            npc.Disposition == Npc.HostileDisposition ? "mob" : "npc",
            npc.DefinitionId,
            definition?.Name,
            npc.SpawnPosition.X,
            npc.SpawnPosition.Y,
            npc.SpawnPosition.Z,
            NpcProperties.GetHeading(npc.SpawnRotation),
            npc.Tilt?.Y ?? 0f,
            npc.Tilt?.Z ?? 0f,
            npc.MovementController is not null);
    }

    /// <summary>
    /// One of the zone's NPCs that its scripts spawned: not a player's mount or pet, a collection node (they come
    /// from the pools) or anything placed in the world (props and world NPCs).
    /// </summary>
    private static bool TryGetZoneNpc(IZoneManager zoneManager, string key, string id, out WorldZone worldZone, out Npc npc)
    {
        npc = null!;

        if (!TryGetWorldZone(zoneManager, key, out worldZone)
            || !ulong.TryParse(id, out var guid)
            || !worldZone.TryGetNpc(guid, out var found))
            return false;

        if (found is Mount or PetTreasureChest or CollectionNode or CollectionCoin
            || found.IsWorldProp
            || found.SpawnPosition == default
            || worldZone.IsWorldFixture(guid)
            || worldZone.IsWorldNpc(guid)
            || worldZone.Players.Any(player => player.Pet?.Guid == guid))
            return false;

        npc = found;
        return true;
    }
}
