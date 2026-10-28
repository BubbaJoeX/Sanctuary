using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

using Sanctuary.Game;
using Sanctuary.Game.Authoring;
using Sanctuary.Game.Conversations;
using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;
using Sanctuary.Game.Resources.Definitions.Combat;
using Sanctuary.Game.Zones;

namespace Sanctuary.Gateway.Admin;

/// <summary>
/// What a placed NPC, model or prop does beyond where it stands, for the zone editor: its name, look, behaviour,
/// event and conditions (what !npc and !prop set in game), and a world NPC's conversation (!conv, admins only).
/// Each change shows for the players in view at once, is saved, and is one undo step. NPC definitions (Npcs.json)
/// are the editor's to write; once it has reloaded them, respawn brings the zone's NPCs built on one up to date.
/// </summary>
public static partial class AdminApiEndpoints
{
    private const float MaxSpeed = 50f;
    private const float MaxWanderRadius = 200f;
    private const float MaxOffset = 50f;
    private const int MaxIcons = 4;

    private static readonly string[] Dispositions = ["hostile", "neutral", "ally"];

    private static void MapNpcEndpoints(RouteGroupBuilder referee)
    {
        referee.MapGet("/npc-options", NpcOptionsHandler);
        referee.MapGet("/zones/{key}/objects/{id}/settings", ObjectSettingsHandler);
        referee.MapPut("/zones/{key}/objects/{id}/settings", SaveObjectSettingsHandler);
        referee.MapGet("/zones/{key}/objects/{id}/conversation", ConversationHandler);
        referee.MapPut("/zones/{key}/objects/{id}/conversation", SaveConversationHandler);
        referee.MapDelete("/zones/{key}/objects/{id}/conversation", ClearConversationHandler);
        referee.MapPost("/zones/{key}/definitions/{def}/respawn", RespawnDefinitionHandler);
    }

    private static NpcOptions NpcOptionsHandler(IResourceManager resourceManager, IWorldEventManager worldEventManager) => new(
        [.. RefereeEditorHelper.Animations.Select(x => new AnimationOption(x.Id, x.Label, x.Loop))],
        [MobDefinition.DefaultKey, .. resourceManager.Mobs.Keys.Where(x => x != MobDefinition.DefaultKey).Order()],
        [.. worldEventManager.GetAll().OrderBy(x => x.Key).Select(x => new EventModel(x.Key, x.Value))],
        [.. resourceManager.Seasons.Values.OrderBy(x => x.StartMonth).ThenBy(x => x.StartDay)
            .Select(x => new SeasonModel(x.Key, x.Name, x.StartMonth, x.StartDay, x.EndMonth, x.EndDay, worldEventManager.IsActive(x.Key)))],
        NpcConditions.QuestStates,
        NpcIconCatalog.Entries(resourceManager),
        MathF.Round(GameClock.Hour, 2));

    private static IResult ObjectSettingsHandler(string key, string id, IZoneManager zoneManager, IResourceManager resourceManager)
    {
        if (!TryGetScriptTarget(zoneManager, key, id, out var worldZone, out var npc, out var kind)
            || !worldZone.TryGetTileEntity(npc.Guid, out var entity))
            return Results.NotFound("Only NPCs, models and props placed in a world zone have settings.");

        return Results.Ok(GetSettings(worldZone, npc, entity, kind, resourceManager));
    }

    private static ObjectSettings GetSettings(WorldZone worldZone, Npc npc, TileEntity entity, string kind, IResourceManager resourceManager)
    {
        var defName = kind == "prop"
            ? resourceManager.ClientItemDefinitions.TryGetValue(entity.DefinitionId, out var item) ? item.Comment : null
            : resourceManager.Npcs.TryGetValue(entity.DefinitionId, out var definition) ? definition.Name : null;

        return new ObjectSettings(kind, entity.DefinitionId, defName, npc.Name, npc.NameId, !npc.HideNamePlate, npc.ModelId,
            npc.Animation, Dispositions.ElementAtOrDefault(npc.Disposition) ?? "neutral", npc.MobKey,
            npc.Behavior.ToString().ToLowerInvariant(), npc.WanderRadius, npc.Speed, npc.Collidable,
            worldZone.GetWorldNpcEvent(npc.Guid), npc.TextureAlias, npc.TintAlias, npc.TintId, npc.CompositeEffectId,
            npc.SubTextNameId, npc.VerticalOffset, NpcProperties.GetIcons(npc), npc.Conditions, npc.Conversation is not null);
    }

    private static IResult SaveObjectSettingsHandler(HttpContext context, string key, string id, ObjectSettingsPatch request,
        IZoneManager zoneManager, IResourceManager resourceManager, RefereeDirectory directory, AdminUndoBook undoBook)
    {
        if (!TryGetScriptTarget(zoneManager, key, id, out var worldZone, out var npc, out var kind)
            || !worldZone.TryGetTileEntity(npc.Guid, out var entity))
            return Results.NotFound("Only NPCs, models and props placed in a world zone have settings.");

        var referee = GetReferee(context);
        var follow = directory.FindOnlineCharacter(referee) is { } character && ReferenceEquals(character.Zone, worldZone)
            ? character.Guid
            : 0ul;

        if (ValidateSettings(request, kind, follow, resourceManager) is { } error)
            return Results.BadRequest(error);

        var before = GetSettings(worldZone, npc, entity, kind, resourceManager);
        var target = undoBook.Ref(npc.Guid);

        var gone = ApplySettings(worldZone, npc, kind, request, follow, resourceManager);

        // Undoing puts every field back as it was; an NPC its event despawned has nothing left to undo.
        undoBook.Push(referee, new TileUndo($"edit {entity.Label}", worldZone, zone =>
        {
            if (zone.TryGetNpc(target.Guid, out var restored))
                ApplySettings(zone, restored, kind, ToPatch(before), follow, resourceManager);
        }));

        Audit(referee, "EditSettings", entity.Label, $"Zone: {worldZone.Name}, Guid: {npc.Guid}, Kind: {kind}");

        return Results.Ok(new EditResult(gone ? null : id, undoBook.Count(referee, worldZone)));
    }

    private static ObjectSettingsPatch ToPatch(ObjectSettings settings) => new(
        settings.Name ?? string.Empty, settings.NameId, settings.Nameplate, settings.ModelId, settings.Animation, settings.Disposition,
        settings.Mob ?? string.Empty, settings.Behavior, settings.WanderRadius, settings.Speed, settings.Event ?? string.Empty,
        settings.TextureAlias ?? string.Empty, settings.TintAlias ?? string.Empty, settings.TintId, settings.Effect, settings.Subtitle,
        settings.Offset, settings.Icons, settings.Conditions ?? new NpcConditions());

    private static string? ValidateSettings(ObjectSettingsPatch request, string kind, ulong follow, IResourceManager resourceManager)
    {
        if (request.Name is { Length: > 64 })
            return "Names are at most 64 characters.";

        if (request.NameId < 0 || request.Animation < 0 || request.TintId < 0 || request.Effect < 0 || request.Subtitle < 0)
            return "Ids are 0 or more.";

        if (request.Offset is { } offset && MathF.Abs(offset) > MaxOffset)
            return $"The height offset is between -{MaxOffset} and {MaxOffset}.";

        if (request.Icons is { Count: > MaxIcons })
            return $"At most {MaxIcons} icons.";

        if (request.Conditions?.Validate() is { } conditionsError)
            return conditionsError;

        if (request.Conditions?.QuestId is { } questId && !resourceManager.Quests.TryGet(questId, out _))
            return $"There's no quest {questId}.";

        if (request.Conditions?.ItemId is { } itemId && !resourceManager.ClientItemDefinitions.ContainsKey(itemId))
            return $"There's no item {itemId}.";

        // A prop is furniture: its model, and whether it moves or fights, are its item's.
        if (kind == "prop")
            return null;

        if (request.ModelId is { } modelId && !resourceManager.Models.ContainsKey(modelId))
            return $"Models.txt has no model {modelId}.";

        if (request.Disposition is { } disposition && !Dispositions.Contains(disposition))
            return "The disposition is hostile, neutral or ally.";

        if (request.Mob is { Length: > 0 } mob && mob != MobDefinition.DefaultKey && !resourceManager.Mobs.ContainsKey(mob))
            return $"Mobs.json has no mob \"{mob}\".";

        if (request.Behavior is { } behavior && (!Enum.TryParse<NpcBehavior>(behavior, true, out var parsed) || !Enum.IsDefined(parsed)))
            return "The behaviour is stay, follow or wander.";

        if (request.Behavior is { } follows && follows.Equals("follow", StringComparison.OrdinalIgnoreCase) && follow == 0)
            return "To follow, your character has to be in this zone.";

        if (request.WanderRadius is { } radius && radius is <= 0f or > MaxWanderRadius)
            return $"The wander radius is above 0 and at most {MaxWanderRadius}.";

        if (request.Speed is { } speed && speed is <= 0f or > MaxSpeed)
            return $"The speed is above 0 and at most {MaxSpeed}.";

        if (request.Event is { Length: > 32 })
            return "Event keys are at most 32 characters.";

        return null;
    }

    /// <summary>
    /// Applies the settings and saves them; true when an event the NPC now waits for despawned it.
    /// </summary>
    private static bool ApplySettings(WorldZone worldZone, Npc npc, string kind, ObjectSettingsPatch request, ulong follow,
        IResourceManager resourceManager)
    {
        var npcOnly = kind == "npc";

        // An empty name goes back to the NPC definition's (a prop has none of its own).
        var definition = npcOnly && worldZone.TryGetTileEntity(npc.Guid, out var entity)
            && resourceManager.Npcs.TryGetValue(entity.DefinitionId, out var found) ? found : null;

        NpcProperties.Change(npc, () =>
        {
            if (request.Name is not null)
            {
                var name = request.Name.Trim();

                if (name.Length > 0)
                {
                    npc.Name = name;
                    npc.NameId = request.NameId ?? 0;
                }
                else if (request.NameId is { } nameId)
                {
                    npc.Name = null;
                    npc.NameId = nameId;
                }
                else
                {
                    npc.Name = definition?.Name;
                    npc.NameId = definition?.NameId ?? 0;
                }
            }

            if (request.Nameplate is { } nameplate)
                npc.HideNamePlate = !nameplate;

            if (request.Animation is { } animation)
                npc.Animation = animation;

            if (request.TextureAlias is not null)
                npc.TextureAlias = request.TextureAlias.Length > 0 ? request.TextureAlias : null;

            if (request.TintAlias is not null)
                npc.TintAlias = request.TintAlias.Length > 0 ? request.TintAlias : null;

            if (request.TintId is { } tintId)
                npc.TintId = tintId;

            if (request.Effect is { } effect)
                npc.CompositeEffectId = effect;

            if (request.Subtitle is { } subtitle)
                npc.SubTextNameId = subtitle;

            if (request.Offset is { } offset)
                npc.VerticalOffset = offset;

            if (request.Icons is not null)
                NpcProperties.SetIcons(npc, request.Icons);

            if (!npcOnly)
                return;

            if (request.ModelId is { } modelId)
                npc.ModelId = modelId;

            if (request.Disposition is { } disposition)
                npc.Disposition = Array.IndexOf(Dispositions, disposition);

            if (request.Mob is not null)
                npc.MobKey = request.Mob.Length == 0 || request.Mob == MobDefinition.DefaultKey ? null : request.Mob;

            if (request.Speed is { } speed)
                npc.Speed = speed;
        });

        if (npcOnly && (request.Behavior is not null || request.WanderRadius is not null))
        {
            var behavior = request.Behavior is { } named ? Enum.Parse<NpcBehavior>(named, true) : npc.Behavior;

            npc.SetBehavior(behavior, behavior == NpcBehavior.Follow ? follow : 0ul, request.WanderRadius ?? npc.WanderRadius);
        }

        if (request.Conditions is { } conditions)
        {
            npc.Conditions = conditions.IsEmpty ? null : conditions;
            npc.RefreshViewers();
        }

        WorldNpcHelper.Save(npc);

        if (!npcOnly || request.Event is null)
            return false;

        var eventKey = request.Event.Trim().ToLowerInvariant();

        if ((eventKey.Length > 0 ? eventKey : null) == worldZone.GetWorldNpcEvent(npc.Guid))
            return false;

        worldZone.TrySetWorldNpcEvent(npc, eventKey.Length > 0 ? eventKey : null);

        return !worldZone.TryGetNpc(npc.Guid, out _);
    }

    #region Conversations

    private static IResult ConversationsAdminsOnly() => Results.Problem("Only admins can edit conversations.", statusCode: StatusCodes.Status403Forbidden);

    private static IResult ConversationHandler(HttpContext context, string key, string id, IZoneManager zoneManager)
    {
        if (!GetReferee(context).IsAdmin)
            return ConversationsAdminsOnly();

        if (!TryGetScriptTarget(zoneManager, key, id, out _, out var npc, out var kind) || kind != "npc")
            return Results.NotFound("Only NPCs and models placed in a world zone have conversations.");

        return Results.Ok(new ConversationModel(npc.Conversation));
    }

    private static IResult SaveConversationHandler(HttpContext context, string key, string id, Conversation request, IZoneManager zoneManager,
        IResourceManager resourceManager, AdminUndoBook undoBook)
    {
        var referee = GetReferee(context);

        if (!referee.IsAdmin)
            return ConversationsAdminsOnly();

        if (!TryGetScriptTarget(zoneManager, key, id, out var worldZone, out var npc, out var kind) || kind != "npc")
            return Results.NotFound("Only NPCs and models placed in a world zone have conversations.");

        if (request.Validate() is { } error)
            return Results.BadRequest(error);

        var item = request.Nodes.SelectMany(x => x.Choices).FirstOrDefault(x => x.GiveItemId > 0 && !resourceManager.ClientItemDefinitions.ContainsKey(x.GiveItemId));

        if (item is not null)
            return Results.BadRequest($"There's no item {item.GiveItemId} to give.");

        return SetConversation(referee, worldZone, npc, request, undoBook);
    }

    private static IResult ClearConversationHandler(HttpContext context, string key, string id, IZoneManager zoneManager, AdminUndoBook undoBook)
    {
        var referee = GetReferee(context);

        if (!referee.IsAdmin)
            return ConversationsAdminsOnly();

        if (!TryGetScriptTarget(zoneManager, key, id, out var worldZone, out var npc, out var kind) || kind != "npc")
            return Results.NotFound("Only NPCs and models placed in a world zone have conversations.");

        return SetConversation(referee, worldZone, npc, null, undoBook);
    }

    private static IResult SetConversation(Referee referee, WorldZone worldZone, Npc npc, Conversation? conversation, AdminUndoBook undoBook)
    {
        var before = npc.Conversation?.Clone();
        var target = undoBook.Ref(npc.Guid);

        if (!worldZone.TrySaveConversation(npc, conversation))
            return Results.Problem("The conversation couldn't be saved.");

        undoBook.Push(referee, new TileUndo($"{(conversation is null ? "remove" : "edit")} {npc.Name ?? "an NPC"}'s conversation", worldZone, zone =>
        {
            if (zone.TryGetNpc(target.Guid, out var restored))
                zone.TrySaveConversation(restored, before);
        }));

        Audit(referee, conversation is null ? "ClearConversation" : "SaveConversation", npc.Name,
            $"Zone: {worldZone.Name}, Guid: {npc.Guid}, Nodes: {conversation?.Nodes.Count ?? 0}");

        return Results.Ok(new EditResult(Id(npc.Guid), undoBook.Count(referee, worldZone)));
    }

    #endregion

    private static IResult RespawnDefinitionHandler(HttpContext context, string key, int def, IZoneManager zoneManager)
    {
        if (!TryGetWorldZone(zoneManager, key, out var worldZone))
            return Results.NotFound("That world zone isn't running.");

        var respawned = worldZone.RespawnWorldNpcs(def);

        if (respawned > 0)
            Audit(GetReferee(context), "RespawnDefinition", null, $"Zone: {worldZone.Name}, Npc: {def}, Respawned: {respawned}");

        return Results.Ok(new RespawnResult(respawned));
    }
}
