using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

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
/// The admin panel's map: the running zones, their ground, a live feed of who and what is where, and editing of the
/// props and NPCs referees placed. Edits go through the same zone code as the in-game tools, so they're saved and
/// players in view see them at once.
/// </summary>
public static partial class AdminApiEndpoints
{
    /// <summary>
    /// The "Placed model" NPC definition in Resources/Npcs.json that placed models are built on.
    /// </summary>
    private const int PlacedModelNpcId = 45000;

    private const int DefaultCatalogLimit = 60;
    private const int MaxCatalogLimit = 200;
    private const float MaxScale = 20f;

    private static readonly TimeSpan LiveInterval = TimeSpan.FromMilliseconds(250);

    private static readonly JsonSerializerOptions LiveJsonOptions = new(JsonSerializerDefaults.Web);

    private static void MapZoneEndpoints(RouteGroupBuilder referee)
    {
        referee.MapGet("/zones", ZonesHandler);
        referee.MapGet("/zones/{key}/heightmap", HeightMapHandler);
        referee.MapGet("/zones/{key}/live", LiveHandlerAsync);
        referee.MapPost("/zones/{key}/undo", UndoHandler);

        referee.MapPost("/zones/{key}/props", PlacePropHandler);
        referee.MapPost("/zones/{key}/npcs", PlaceNpcHandler);
        referee.MapPost("/zones/{key}/models", PlaceModelHandler);
        referee.MapGet("/zones/{key}/objects/{id}", ObjectHandler);
        referee.MapPatch("/zones/{key}/objects/{id}", PatchObjectHandler);
        referee.MapPost("/zones/{key}/objects/{id}/copy", CopyObjectHandler);
        referee.MapDelete("/zones/{key}/objects/{id}", DeleteObjectHandler);

        referee.MapGet("/catalog/props", PropCatalogHandler);
        referee.MapGet("/catalog/npcs", NpcCatalogHandler);

        // Every actor that can be placed as a model (it has a Models.txt row), lower case.
        referee.MapGet("/models", (IResourceManager resourceManager) => resourceManager.Models.Values
            .Select(x => x.ModelFileName.ToLowerInvariant())
            .Distinct()
            .Order());
    }

    #region Zones

    private static ZoneModel[] ZonesHandler(IZoneManager zoneManager, IResourceManager resourceManager, ZoneHeightMaps heightMaps) => zoneManager.Zones
        .OrderBy(zone => zone is not WorldZone)
        .ThenBy(zone => zone.Name)
        .Select(zone =>
        {
            BoundsModel? bounds = null;

            // Zone bounds are in tiles: latitude along X, longitude along Z.
            if (resourceManager.Zones.TryGetValue(zone.DefinitionId, out var definition) && definition.TileSize > 0f)
            {
                bounds = new BoundsModel(
                    definition.StartLatitude * definition.TileSize,
                    definition.StartLongitude * definition.TileSize,
                    definition.EndLatitude * definition.TileSize,
                    definition.EndLongitude * definition.TileSize);
            }

            return new ZoneModel(
                ZoneKey(zone),
                zone.DefinitionId,
                zone.OwnerId?.ToString(),
                zone.Name,
                zone switch
                {
                    WorldZone => "World",
                    HousingZone => "Housing",
                    CombatZone => "Combat",
                    _ => zone.GetType().Name
                },
                zone.Players.Count(),
                zone.Npcs.Count(),
                zone is WorldZone,
                heightMaps.Has(zone.Name),
                bounds,
                new PointModel(zone.SpawnPosition.X, zone.SpawnPosition.Y, zone.SpawnPosition.Z, NpcProperties.GetHeading(zone.SpawnRotation)));
        })
        .ToArray();

    private static IResult HeightMapHandler(HttpContext context, string key, IZoneManager zoneManager, ZoneHeightMaps heightMaps)
    {
        if (!TryGetZone(zoneManager, key, out var zone))
            return Results.NotFound("That zone isn't running.");

        if (heightMaps.Get(zone.Name) is not { } data)
            return Results.NotFound($"{zone.Name} has no terrain.");

        // Only changes when the server restarts with new terrain.
        context.Response.Headers.CacheControl = "private, max-age=3600";

        return Results.Bytes(data, "application/octet-stream");
    }

    /// <summary>
    /// Server-sent events: <c>players</c> four times a second and <c>objects</c> (placed props and world NPCs) twice a
    /// second, each only when it changed, and with <c>?npcs=true</c> what changed among the zone's other NPCs every
    /// second (<see cref="LiveNpcChanges"/>). Each refers to its look by key; <c>looks</c> brings the ones the feed
    /// hasn't sent yet, just before them. <c>closed</c> ends the feed when the zone shuts down.
    /// </summary>
    private static async Task LiveHandlerAsync(HttpContext context, string key, bool? npcs, IZoneManager zoneManager,
        IResourceManager resourceManager)
    {
        if (!TryGetZone(zoneManager, key, out var zone))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        var response = context.Response;
        var cancellationToken = context.RequestAborted;

        response.ContentType = "text/event-stream";
        response.Headers.CacheControl = "no-cache";
        response.Headers["X-Accel-Buffering"] = "no";

        string? lastPlayers = null;
        string? lastObjects = null;
        Dictionary<string, LiveNpc>? lastNpcs = null;
        var looks = new FeedLooks();

        using var timer = new PeriodicTimer(LiveInterval);

        try
        {
            for (var tick = 0; ; tick++)
            {
                if (!zoneManager.Zones.Contains(zone))
                {
                    await WriteEventAsync(response, "closed", "{}", cancellationToken);
                    break;
                }

                var players = JsonSerializer.Serialize(zone.Players.Select(player => new LivePlayer(
                    Id(player.Guid),
                    player.Name.FullName,
                    Round(player.Position.X),
                    Round(player.Position.Y),
                    Round(player.Position.Z),
                    Round(NpcProperties.GetHeading(player.Rotation)),
                    looks.Add(LiveLooks.Of(player, resourceManager)))).ToList(), LiveJsonOptions);

                if (players != lastPlayers)
                {
                    await looks.FlushAsync(response, cancellationToken);
                    await WriteEventAsync(response, "players", lastPlayers = players, cancellationToken);
                }

                if (tick % 2 == 0 && zone is WorldZone worldZone)
                {
                    var objects = JsonSerializer.Serialize(GetLiveObjects(worldZone, resourceManager, looks).ToList(), LiveJsonOptions);

                    if (objects != lastObjects)
                    {
                        await looks.FlushAsync(response, cancellationToken);
                        await WriteEventAsync(response, "objects", lastObjects = objects, cancellationToken);
                    }
                }

                // A big zone has thousands (FabledRealms ~5,500 with its collection nodes), so only changes go out.
                if (npcs == true && tick % 4 == 0)
                {
                    var current = GetOtherNpcs(zone, resourceManager, looks).ToDictionary(x => x.Id);
                    await looks.FlushAsync(response, cancellationToken);

                    var changes = new LiveNpcChanges(
                        lastNpcs is null,
                        [.. current.Values.Where(npc => lastNpcs is null || !lastNpcs.TryGetValue(npc.Id, out var last) || last != npc)],
                        lastNpcs is null ? [] : [.. lastNpcs.Keys.Where(id => !current.ContainsKey(id))]);

                    if (changes.Reset || changes.Upsert.Count > 0 || changes.Remove.Count > 0)
                        await WriteEventAsync(response, "npcs", JsonSerializer.Serialize(changes, LiveJsonOptions), cancellationToken);

                    lastNpcs = current;
                }

                // Keeps idle connections open through proxies.
                if (tick % 60 == 0)
                    await response.WriteAsync(": ping\n\n", cancellationToken);

                await response.Body.FlushAsync(cancellationToken);

                await timer.WaitForNextTickAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    /// <summary>
    /// The looks a feed has sent, and those its next event refers to that it hasn't.
    /// </summary>
    private sealed class FeedLooks
    {
        private readonly HashSet<string> _sent = [];
        private readonly Dictionary<string, string> _pending = [];

        /// <summary>
        /// The look's key, queueing it to send if it's new to this feed; null for an empty look.
        /// </summary>
        public string? Add(IReadOnlyList<LookPart> look)
        {
            if (look.Count == 0)
                return null;

            var (key, json) = LiveLooks.Key(look);

            if (!_sent.Contains(key))
                _pending[key] = json;

            return key;
        }

        public async Task FlushAsync(HttpResponse response, CancellationToken cancellationToken)
        {
            if (_pending.Count == 0)
                return;

            await WriteEventAsync(response, "looks", "{" + string.Join(",", _pending.Select(x => $"\"{x.Key}\":{x.Value}")) + "}", cancellationToken);

            _sent.UnionWith(_pending.Keys);
            _pending.Clear();
        }
    }

    private static IEnumerable<LiveObject> GetLiveObjects(WorldZone worldZone, IResourceManager resourceManager, FeedLooks looks)
    {
        foreach (var entity in worldZone.GetTileEntities())
        {
            if (!worldZone.TryGetNpc(entity.Guid, out var npc))
                continue;

            yield return new LiveObject(
                Id(entity.Guid),
                entity.IsProp ? "prop" : npc.IsWorldProp ? "model" : "npc",
                entity.DefinitionId,
                entity.Label,
                resourceManager.Models.TryGetValue(npc.ModelId, out var model) ? model.ModelFileName : null,
                Round(entity.Position.X),
                Round(entity.Position.Y),
                Round(entity.Position.Z),
                Round(entity.Heading),
                Round(npc.Tilt?.Y ?? 0f),
                Round(npc.Tilt?.Z ?? 0f),
                Round(npc.Scale),
                looks.Add(LiveLooks.Of(npc, resourceManager)));
        }
    }

    private static Task WriteEventAsync(HttpResponse response, string name, string data, CancellationToken cancellationToken) =>
        response.WriteAsync($"event: {name}\ndata: {data}\n\n", cancellationToken);

    /// <summary>
    /// The zone's NPCs other than placed props and world NPCs, mounts and pets: stock NPCs, mobs and script spawns.
    /// </summary>
    private static IEnumerable<LiveNpc> GetOtherNpcs(IZone zone, IResourceManager resourceManager, FeedLooks looks)
    {
        var worldZone = zone as WorldZone;
        var pets = zone.Players.Select(player => player.Pet?.Guid).ToHashSet();

        foreach (var npc in zone.Npcs)
        {
            if (!npc.Visible || npc is Mount or PetTreasureChest || pets.Contains(npc.Guid))
                continue;

            if (worldZone is not null && (worldZone.IsWorldFixture(npc.Guid) || worldZone.IsWorldNpc(npc.Guid)))
                continue;

            var label = npc.Name
                ?? (resourceManager.Npcs.TryGetValue(npc.DefinitionId, out var definition) ? definition.Name : null)
                ?? $"NPC {npc.DefinitionId}";

            var kind = npc switch
            {
                CollectionNode => "node",
                CollectionCoin => "coin",
                _ when npc.Disposition == Npc.HostileDisposition => "mob",
                _ => "npc"
            };

            // Coarser than players, so idling mobs don't send a change every second.
            yield return new LiveNpc(
                Id(npc.Guid),
                label,
                kind,
                npc.DefinitionId,
                MathF.Round(npc.Position.X, 1),
                MathF.Round(npc.Position.Y, 1),
                MathF.Round(npc.Position.Z, 1),
                MathF.Round(NpcProperties.GetHeading(npc.Rotation), 2),
                Round(npc.Tilt?.Y ?? 0f),
                Round(npc.Tilt?.Z ?? 0f),
                MathF.Round(npc.Scale, 2),
                looks.Add(LiveLooks.Of(npc, resourceManager)));
        }
    }

    private static IResult UndoHandler(HttpContext context, string key, IZoneManager zoneManager, AdminUndoBook undoBook)
    {
        if (!TryGetWorldZone(zoneManager, key, out var worldZone))
            return Results.NotFound("That world zone isn't running.");

        var referee = GetReferee(context);

        if (undoBook.Pop(referee, worldZone) is not { } undo)
            return Results.Ok(new UndoResult(null, 0));

        undo.Revert(worldZone);

        Audit(referee, "Undo", null, $"Zone: {worldZone.Name}, Edit: {undo.Label}");

        return Results.Ok(new UndoResult(undo.Label, undoBook.Count(referee, worldZone)));
    }

    #endregion

    #region Objects

    private static IResult ObjectHandler(string key, string id, IZoneManager zoneManager)
    {
        if (!TryGetObject(zoneManager, key, id, out var worldZone, out var entity, out var npc))
            return Results.NotFound("That object isn't there any more.");

        if (entity.IsProp && worldZone.TryGetWorldFixture(entity.Guid, out var fixture))
        {
            return Results.Ok(new ObjectDetail(id, "prop", entity.DefinitionId, entity.Label,
                entity.Position.X, entity.Position.Y, entity.Position.Z, entity.Heading,
                fixture.Scale, fixture.Tint, fixture.Collidable, null, npc.ModelId, null, !string.IsNullOrWhiteSpace(fixture.Script)));
        }

        return Results.Ok(new ObjectDetail(id, "npc", entity.DefinitionId, entity.Label,
            entity.Position.X, entity.Position.Y, entity.Position.Z, entity.Heading,
            npc.Scale, null, npc.Collidable, npc.Name, npc.ModelId, worldZone.GetWorldNpcEvent(entity.Guid),
            !string.IsNullOrWhiteSpace(worldZone.GetSavedScript(entity.Guid))));
    }

    private static IResult PlacePropHandler(HttpContext context, string key, PlaceRequest request, IZoneManager zoneManager,
        IHousingManager housingManager, IResourceManager resourceManager, RefereeDirectory directory, AdminUndoBook undoBook)
    {
        if (!TryGetWorldZone(zoneManager, key, out var worldZone))
            return Results.NotFound("Props go in a world zone that's running.");

        if (!housingManager.TryGetFixtureDefinition(request.Def, out _))
            return Results.BadRequest($"Item {request.Def} isn't furniture that can be placed.");

        if (request.Scale is { } scale && scale is <= 0f or > MaxScale || request.Tint < 0)
            return Results.BadRequest($"Scale must be above 0 and at most {MaxScale}, and tint 0 or more.");

        var referee = GetReferee(context);
        var position = GetPlacement(worldZone, referee, directory, request.X, request.Y, request.Z);

        // Props are stored in the client's placement format: yaw in X.
        if (!worldZone.TryPlaceWorldFixture(request.Def, request.Tint ?? 0, position, new Quaternion(request.H, 0f, 0f, 0f),
                request.Scale ?? 1f, directory.FindOnlineCharacter(referee)?.CharacterId, out var npc))
            return Results.Problem($"Item {request.Def} couldn't be placed.");

        var placed = undoBook.Ref(npc.Guid);
        var label = resourceManager.ClientItemDefinitions.TryGetValue(request.Def, out var item) ? item.Comment : null;

        undoBook.Push(referee, new TileUndo($"place {label ?? $"item {request.Def}"}", worldZone, zone => zone.TryDeleteTileEntity(placed.Guid)));

        Audit(referee, "PlaceProp", null, $"Zone: {worldZone.Name}, Item: {request.Def}, Position: {position.X:F1}, {position.Y:F1}, {position.Z:F1}");

        return Results.Ok(new EditResult(Id(npc.Guid), undoBook.Count(referee, worldZone)));
    }

    private static IResult PlaceNpcHandler(HttpContext context, string key, PlaceRequest request, IZoneManager zoneManager,
        IResourceManager resourceManager, RefereeDirectory directory, AdminUndoBook undoBook)
    {
        if (!TryGetWorldZone(zoneManager, key, out var worldZone))
            return Results.NotFound("NPCs go in a world zone that's running.");

        if (!resourceManager.Npcs.TryGetValue(request.Def, out var definition))
            return Results.BadRequest($"There's no NPC {request.Def}.");

        if (request.Scale is { } scale && scale is <= 0f or > MaxScale)
            return Results.BadRequest($"Scale must be above 0 and at most {MaxScale}.");

        var referee = GetReferee(context);
        var position = GetPlacement(worldZone, referee, directory, request.X, request.Y, request.Z);

        if (!worldZone.TryCreateWorldNpc(definition, position, QuaternionExtensions.FromHeading(request.H),
                directory.FindOnlineCharacter(referee)?.CharacterId, out var npc))
            return Results.Problem($"NPC {request.Def} couldn't be spawned.");

        if (request.Scale is { } newScale)
        {
            npc.Scale = newScale;
            npc.RefreshForVisiblePlayers();
            worldZone.TrySaveWorldNpc(npc);
        }

        var spawned = undoBook.Ref(npc.Guid);

        undoBook.Push(referee, new TileUndo($"spawn {definition.Name ?? $"NPC {request.Def}"}", worldZone, zone => zone.TryDeleteTileEntity(spawned.Guid)));

        Audit(referee, "SpawnNpc", null, $"Zone: {worldZone.Name}, Npc: {request.Def}, Position: {position.X:F1}, {position.Y:F1}, {position.Z:F1}");

        return Results.Ok(new EditResult(Id(npc.Guid), undoBook.Count(referee, worldZone)));
    }

    /// <summary>
    /// Places any actor with a Models.txt row as a still, solid world NPC (on the "Placed model" definition), which
    /// is how the zone editor drops its content browser's models into the live world.
    /// </summary>
    private static IResult PlaceModelHandler(HttpContext context, string key, ModelPlaceRequest request, IZoneManager zoneManager,
        IResourceManager resourceManager, RefereeDirectory directory, AdminUndoBook undoBook)
    {
        if (!TryGetWorldZone(zoneManager, key, out var worldZone))
            return Results.NotFound("Models go in a world zone that's running.");

        if (!resourceManager.Npcs.TryGetValue(PlacedModelNpcId, out var definition))
            return Results.Problem($"Resources/Npcs.json has no \"Placed model\" NPC ({PlacedModelNpcId}).");

        var model = resourceManager.Models.Values.FirstOrDefault(x => x.ModelFileName.Equals(request.Model, StringComparison.OrdinalIgnoreCase));

        if (model is null)
            return Results.BadRequest($"{request.Model} has no Models.txt row, so the game can't spawn it; bake it into the zone instead.");

        if (request.Scale is { } scale && scale is <= 0f or > MaxScale)
            return Results.BadRequest($"Scale must be above 0 and at most {MaxScale}.");

        var referee = GetReferee(context);
        var position = GetPlacement(worldZone, referee, directory, request.X, request.Y, request.Z);

        if (!worldZone.TryCreateWorldNpc(definition, position, QuaternionExtensions.FromHeading(request.H),
                directory.FindOnlineCharacter(referee)?.CharacterId, out var npc, model))
            return Results.Problem($"{request.Model} couldn't be placed.");

        if (request.Scale is { } newScale && newScale != npc.Scale)
        {
            npc.Scale = newScale;
            npc.RefreshForVisiblePlayers();
            worldZone.TrySaveWorldNpc(npc);
        }

        if (request.Pitch is { } pitch && pitch != 0f || request.Roll is { } roll && roll != 0f)
            SetTilt(worldZone, npc, request.Pitch ?? 0f, request.Roll ?? 0f);

        var placed = undoBook.Ref(npc.Guid);

        undoBook.Push(referee, new TileUndo($"place {npc.Name}", worldZone, zone => zone.TryDeleteTileEntity(placed.Guid)));

        Audit(referee, "PlaceModel", null, $"Zone: {worldZone.Name}, Model: {model.ModelFileName}, Position: {position.X:F1}, {position.Y:F1}, {position.Z:F1}");

        return Results.Ok(new EditResult(Id(npc.Guid), undoBook.Count(referee, worldZone)));
    }

    /// <summary>
    /// Moves, turns or changes a placed prop or world NPC, as one undo step.
    /// </summary>
    private static IResult PatchObjectHandler(HttpContext context, string key, string id, ObjectPatch request, IZoneManager zoneManager,
        AdminUndoBook undoBook)
    {
        if (!TryGetObject(zoneManager, key, id, out var worldZone, out var entity, out var npc))
            return Results.NotFound("That object isn't there any more.");

        if (request.Scale is { } scale && scale is <= 0f or > MaxScale || request.Tint < 0)
            return Results.BadRequest($"Scale must be above 0 and at most {MaxScale}, and tint 0 or more.");

        if (request.Name is { Length: > 64 })
            return Results.BadRequest("Names are at most 64 characters.");

        var guid = entity.Guid;
        var target = undoBook.Ref(guid);
        var reverts = new List<Action<WorldZone>>();
        var changes = new List<string>();

        if (request.X is not null || request.Y is not null || request.Z is not null || request.H is not null)
        {
            var from = entity.Position;
            var heading = entity.Heading;

            var x = request.X ?? from.X;
            var z = request.Z ?? from.Z;
            var y = request.Y ?? (request.Snap ? FollowGround(worldZone, from, x, z) : from.Y);

            if (!worldZone.TryMoveTileEntity(guid, new Vector4(x, y, z, 1f), request.H ?? heading))
                return Results.Problem("That object couldn't be moved.");

            reverts.Add(zone => zone.TryMoveTileEntity(target.Guid, from, heading));
            changes.Add($"Position: {x:F1}, {y:F1}, {z:F1}, Heading: {request.H ?? heading:F2}");
        }

        if (request.Pitch is not null || request.Roll is not null)
        {
            var (oldPitch, oldRoll) = (npc.Tilt?.Y ?? 0f, npc.Tilt?.Z ?? 0f);

            SetTilt(worldZone, npc, request.Pitch ?? oldPitch, request.Roll ?? oldRoll);

            reverts.Add(zone =>
            {
                if (zone.TryGetNpc(target.Guid, out var tilted))
                    SetTilt(zone, tilted, oldPitch, oldRoll);
            });
            changes.Add($"Pitch: {request.Pitch ?? oldPitch:F2}, Roll: {request.Roll ?? oldRoll:F2}");
        }

        if (entity.IsProp && worldZone.TryGetWorldFixture(guid, out var fixture))
        {
            if (request.Scale is not null || request.Tint is not null)
            {
                var (oldScale, oldTint) = (fixture.Scale, fixture.Tint);

                worldZone.TrySetWorldFixtureAppearance(guid, request.Scale ?? oldScale, request.Tint ?? oldTint);

                reverts.Add(zone => zone.TrySetWorldFixtureAppearance(target.Guid, oldScale, oldTint));
                changes.Add($"Scale: {request.Scale ?? oldScale}, Tint: {request.Tint ?? oldTint}");
            }

            if (request.Collidable is { } collidable && collidable != fixture.Collidable)
            {
                worldZone.TrySetWorldFixtureCollidable(guid, collidable);

                reverts.Add(zone => zone.TrySetWorldFixtureCollidable(target.Guid, !collidable));
                changes.Add($"Collidable: {collidable}");
            }
        }
        else if (request.Scale is not null || request.Collidable is not null || request.Name is not null)
        {
            var (oldScale, oldCollidable, oldName, oldNameId) = (npc.Scale, npc.Collidable, npc.Name, npc.NameId);

            ApplyNpc(worldZone, npc, request.Scale ?? oldScale, request.Collidable ?? oldCollidable,
                request.Name is null ? oldName : request.Name.Trim() is { Length: > 0 } name ? name : null,
                request.Name is null ? oldNameId : 0);

            reverts.Add(zone =>
            {
                if (zone.TryGetNpc(target.Guid, out var restored))
                    ApplyNpc(zone, restored, oldScale, oldCollidable, oldName, oldNameId);
            });
            changes.Add($"Scale: {npc.Scale}, Collidable: {npc.Collidable}, Name: \"{npc.Name}\"");
        }

        var referee = GetReferee(context);

        if (reverts.Count > 0)
        {
            undoBook.Push(referee, new TileUndo($"edit {entity.Label}", worldZone, zone =>
            {
                for (var i = reverts.Count - 1; i >= 0; i--)
                    reverts[i](zone);
            }));

            Audit(referee, "EditObject", entity.Label, $"Zone: {worldZone.Name}, Guid: {guid}, {string.Join(", ", changes)}");
        }

        return Results.Ok(new EditResult(id, undoBook.Count(referee, worldZone)));
    }

    private static void SetTilt(WorldZone worldZone, Npc npc, float pitch, float roll)
    {
        NpcProperties.SetTilt(npc, pitch, roll);

        if (worldZone.IsWorldFixture(npc.Guid))
            worldZone.TrySaveWorldFixtureProperties(npc);
        else
            worldZone.TrySaveWorldNpc(npc);
    }

    private static void ApplyNpc(WorldZone worldZone, Npc npc, float scale, bool collidable, string? name, int nameId)
    {
        npc.Scale = scale;
        npc.Name = name;
        npc.NameId = nameId;

        if (npc.Collidable != collidable)
            npc.SetCollidable(collidable);

        npc.RefreshForVisiblePlayers();

        worldZone.TrySaveWorldNpc(npc);
    }

    private static IResult CopyObjectHandler(HttpContext context, string key, string id, CopyRequest request, IZoneManager zoneManager,
        RefereeDirectory directory, AdminUndoBook undoBook)
    {
        if (!TryGetObject(zoneManager, key, id, out var worldZone, out var entity, out _)
            || !worldZone.TrySnapshotTileEntity(entity.Guid, out var snapshot))
            return Results.NotFound("That object isn't there any more.");

        var referee = GetReferee(context);
        var position = new Vector4(request.X, FollowGround(worldZone, entity.Position, request.X, request.Z), request.Z, 1f);

        var guid = worldZone.TryCreateFromSnapshot(snapshot, position, request.H ?? entity.Heading,
            directory.FindOnlineCharacter(referee)?.CharacterId, out var spawned);

        if (guid is not { } copy || !spawned)
            return Results.Problem($"{entity.Label} couldn't be copied.");

        var copied = undoBook.Ref(copy);

        undoBook.Push(referee, new TileUndo($"copy {entity.Label}", worldZone, zone => zone.TryDeleteTileEntity(copied.Guid)));

        Audit(referee, "CopyObject", entity.Label, $"Zone: {worldZone.Name}, Guid: {entity.Guid}, Position: {position.X:F1}, {position.Y:F1}, {position.Z:F1}");

        return Results.Ok(new EditResult(Id(copy), undoBook.Count(referee, worldZone)));
    }

    private static IResult DeleteObjectHandler(HttpContext context, string key, string id, IZoneManager zoneManager, AdminUndoBook undoBook)
    {
        if (!TryGetObject(zoneManager, key, id, out var worldZone, out var entity, out _)
            || !worldZone.TrySnapshotTileEntity(entity.Guid, out var snapshot))
            return Results.NotFound("That object isn't there any more.");

        if (!worldZone.TryDeleteTileEntity(entity.Guid))
            return Results.Problem($"{entity.Label} couldn't be deleted.");

        var referee = GetReferee(context);
        var deleted = undoBook.Ref(entity.Guid);
        var (position, heading) = (entity.Position, entity.Heading);

        // It comes back under a new guid, which the earlier steps that name it then follow.
        undoBook.Push(referee, new TileUndo($"delete {entity.Label}", worldZone, zone =>
        {
            if (zone.TryCreateFromSnapshot(snapshot, position, heading, snapshot.Fixture?.CreatedBy ?? snapshot.WorldNpc?.CreatedBy, out _) is { } restored)
                undoBook.Follow(deleted, restored);
        }));

        Audit(referee, "DeleteObject", entity.Label, $"Zone: {worldZone.Name}, Guid: {entity.Guid}, Kind: {(entity.IsProp ? "prop" : "npc")}, Def: {entity.DefinitionId}");

        return Results.Ok(new EditResult(null, undoBook.Count(referee, worldZone)));
    }

    /// <summary>
    /// Where a new object goes: on the ground at X/Z unless a height is given. Without ground data, at the
    /// referee's height when they're in the zone, or else the spawn point's.
    /// </summary>
    private static Vector4 GetPlacement(WorldZone worldZone, Referee referee, RefereeDirectory directory, float x, float? y, float z)
    {
        if (y is { } height)
            return new Vector4(x, height, z, 1f);

        if (worldZone.TryGetGroundHeight(x, z, 10_000f, 20_000f, out var ground))
            return new Vector4(x, ground, z, 1f);

        var character = directory.FindOnlineCharacter(referee);

        return new Vector4(x, ReferenceEquals(character?.Zone, worldZone) ? character!.Position.Y : worldZone.SpawnPosition.Y, z, 1f);
    }

    /// <summary>
    /// The height for something at <paramref name="from"/> moved to X/Z that keeps it as far above (or below) the
    /// ground as it was, so a prop on a hillside or sunk into the grass stays that way.
    /// </summary>
    private static float FollowGround(IZone zone, Vector4 from, float x, float z)
    {
        if (zone.TryGetGroundHeight(from.X, from.Z, from.Y + 2f, 50f, out var before)
            && zone.TryGetGroundHeight(x, z, 10_000f, 20_000f, out var after))
            return after + (from.Y - before);

        return from.Y;
    }

    private static bool TryGetWorldZone(IZoneManager zoneManager, string key, out WorldZone worldZone)
    {
        worldZone = null!;

        if (!TryGetZone(zoneManager, key, out var zone) || zone is not WorldZone found)
            return false;

        worldZone = found;
        return true;
    }

    private static bool TryGetObject(IZoneManager zoneManager, string key, string id, out WorldZone worldZone, out TileEntity entity, out Npc npc)
    {
        entity = null!;
        npc = null!;

        return TryGetWorldZone(zoneManager, key, out worldZone)
            && ulong.TryParse(id, out var guid)
            && worldZone.TryGetTileEntity(guid, out entity!)
            && worldZone.TryGetNpc(guid, out npc!);
    }

    #endregion

    #region Catalog

    /// <summary>
    /// Furniture items that can be placed as props, matching every word of <paramref name="q"/> (name, model or id).
    /// </summary>
    private static CatalogEntry[] PropCatalogHandler(string? q, int? limit, IResourceManager resourceManager, IHousingManager housingManager) =>
        Search(resourceManager.ClientItemDefinitions.Values
                .Where(item => housingManager.TryGetFixtureDefinition(item.Id, out _))
                .Select(item => new CatalogEntry(item.Id, string.IsNullOrWhiteSpace(item.Comment) ? $"Item {item.Id}" : item.Comment, item.ModelName,
                    item.Icon.Id, item.Icon.TintId)),
            q, limit);

    private static CatalogEntry[] NpcCatalogHandler(string? q, int? limit, IResourceManager resourceManager) =>
        Search(resourceManager.Npcs.Values
                .Select(npc => new CatalogEntry(npc.Id, npc.Name ?? npc.ModelFileName ?? $"NPC {npc.Id}", npc.ModelFileName)),
            q, limit);

    private static CatalogEntry[] Search(IEnumerable<CatalogEntry> entries, string? q, int? limit)
    {
        var words = (q ?? string.Empty).ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);

        return entries
            .Where(entry =>
            {
                var text = $"{entry.Id} {entry.Name} {entry.Model}".ToLowerInvariant();
                return words.All(text.Contains);
            })
            .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .Take(Math.Clamp(limit ?? DefaultCatalogLimit, 1, MaxCatalogLimit))
            .ToArray();
    }

    #endregion
}
