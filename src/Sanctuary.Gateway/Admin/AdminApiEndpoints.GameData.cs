using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

using Sanctuary.Game;
using Sanctuary.Game.Authoring;
using Sanctuary.Game.Conversations;
using Sanctuary.Game.Helpers;
using Sanctuary.Game.Zones;

namespace Sanctuary.Gateway.Admin;

/// <summary>
/// The admin panel's GameData editor (admins, as !questedit and !conv are): GM-authored quests, made and published
/// as !questedit does, and the world NPCs whose conversations it edits through the object routes. NPC definitions
/// and reward tables are files, which the zone editor writes; the panel has the game reload them.
/// </summary>
public static partial class AdminApiEndpoints
{
    private const int MaxQuestTitle = 64;
    private const int MaxQuestText = 400;
    private const int MaxWorldNpcs = 500;

    private static void MapGameDataEndpoints(RouteGroupBuilder referee)
    {
        referee.MapGet("/quests", QuestsHandler);
        referee.MapGet("/quests/options", QuestOptionsHandler);
        referee.MapPost("/quests/custom", CreateQuestHandler);
        referee.MapGet("/quests/custom/{id:int}", CustomQuestHandler);
        referee.MapPut("/quests/custom/{id:int}", SaveQuestHandler);
        referee.MapPost("/quests/custom/{id:int}/publish", PublishQuestHandler);
        referee.MapPost("/quests/custom/{id:int}/unpublish", UnpublishQuestHandler);
        referee.MapDelete("/quests/custom/{id:int}", DeleteQuestHandler);

        referee.MapGet("/conversations", ConversationsHandler);
    }

    #region Quests

    /// <summary>
    /// The GM-authored quests, and the game's own (which they can follow on from or lead to).
    /// </summary>
    private static IResult QuestsHandler(HttpContext context, ICustomQuestManager customQuestManager, IResourceManager resourceManager)
    {
        if (!GetReferee(context).IsAdmin)
            return AdminsOnly();

        var custom = customQuestManager.GetAll()
            .OrderBy(quest => quest.Id)
            .Select(quest => new CustomQuestSummaryModel(quest.Id, CustomQuestManager.QuestIdBase + quest.Id, quest.Title, quest.IsPublished,
                quest.GiverNpcId, NpcName(resourceManager, quest.GiverNpcId), CustomQuestJson.ReadList<CustomQuestGoal>(quest.Goals).Count, quest.Updated))
            .ToArray();

        var stock = resourceManager.Quests.Quests.Values
            .Where(quest => !CustomQuestManager.IsCustomQuestId(quest.QuestId))
            .OrderBy(quest => quest.QuestId)
            .Select(quest => new StockQuestModel(quest.QuestId, GameNameHelper.Get(quest.TitleId) ?? $"Quest {quest.QuestId}", quest.GiverNpcId,
                NpcName(resourceManager, quest.GiverNpcId), quest.Goals.Count, quest.NextQuestId))
            .ToArray();

        return Results.Ok(new QuestListModel(custom, stock));
    }

    /// <summary>
    /// What a quest's goals can name: collection node types to gather from and zones to reach.
    /// </summary>
    private static QuestOptionsModel QuestOptionsHandler(IResourceManager resourceManager) => new(
        [.. resourceManager.CollectionNodeTypes.Values.OrderBy(x => x.Name).Select(x => new NodeTypeModel(x.Key, x.Name))],
        [.. resourceManager.Zones.Values.OrderBy(x => x.Name).Select(x => new ZoneOptionModel(x.Id, x.Name))],
        CustomQuestManager.MaxGoals,
        CustomQuestManager.MaxRewards);

    private static IResult CreateQuestHandler(HttpContext context, QuestCreateRequest request, ICustomQuestManager customQuestManager)
    {
        var referee = GetReferee(context);

        if (!referee.IsAdmin)
            return AdminsOnly();

        var title = request.Title?.Trim() ?? string.Empty;

        if (title.Length is 0 or > MaxQuestTitle)
            return Results.BadRequest($"A quest's title is 1-{MaxQuestTitle} characters.");

        var quest = customQuestManager.Create(title, referee.Id);

        Audit(referee, "Quest Create", title, $"Quest: {CustomQuestManager.QuestIdBase + quest.Id}");

        return Results.Ok(ToCustomQuestModel(customQuestManager, quest));
    }

    private static IResult CustomQuestHandler(HttpContext context, int id, ICustomQuestManager customQuestManager)
    {
        if (!GetReferee(context).IsAdmin)
            return AdminsOnly();

        return customQuestManager.TryGet(id, out var quest)
            ? Results.Ok(ToCustomQuestModel(customQuestManager, quest))
            : Results.NotFound("There's no such quest.");
    }

    /// <summary>
    /// Saves every field of a quest. A published quest stays live, so an edit that would break it is refused until
    /// it's unpublished.
    /// </summary>
    private static IResult SaveQuestHandler(HttpContext context, int id, CustomQuestRequest request, ICustomQuestManager customQuestManager,
        IResourceManager resourceManager)
    {
        var referee = GetReferee(context);

        if (!referee.IsAdmin)
            return AdminsOnly();

        if (CheckQuest(id, request, customQuestManager, resourceManager) is { } problem)
            return Results.BadRequest(problem);

        var goals = request.Goals ?? [];
        var rewards = request.Rewards ?? [];

        var error = customQuestManager.Update(id, referee.Id, quest =>
        {
            quest.Title = request.Title.Trim();
            quest.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
            quest.TitleId = request.TitleId;
            quest.DescriptionId = request.DescriptionId;
            quest.GiverNpcId = request.GiverNpcId;
            quest.TurnInNpcId = request.TurnInNpcId;
            quest.RewardCoins = request.RewardCoins;
            quest.PrerequisiteQuestId = request.PrerequisiteQuestId;
            quest.NextQuestId = request.NextQuestId;
            quest.Goals = CustomQuestJson.Write(goals.Select(TrimGoal).ToList());
            quest.Rewards = CustomQuestJson.Write(rewards);
            return null;
        });

        if (error is not null)
            return Results.Problem(error, statusCode: error.StartsWith("No custom quest") ? StatusCodes.Status404NotFound : StatusCodes.Status409Conflict);

        Audit(referee, "Quest Save", request.Title.Trim(), $"Quest: {CustomQuestManager.QuestIdBase + id}, Goals: {goals.Count}, Rewards: {rewards.Count}");

        customQuestManager.TryGet(id, out var saved);

        return Results.Ok(ToCustomQuestModel(customQuestManager, saved));
    }

    private static IResult PublishQuestHandler(HttpContext context, int id, ICustomQuestManager customQuestManager)
    {
        var referee = GetReferee(context);

        if (!referee.IsAdmin)
            return AdminsOnly();

        if (customQuestManager.Publish(id, referee.Id) is { } error)
            return Results.Problem(error, statusCode: StatusCodes.Status409Conflict);

        customQuestManager.TryGet(id, out var quest);

        Audit(referee, "Quest Publish", quest.Title, $"Quest: {CustomQuestManager.QuestIdBase + id}");

        return Results.Ok(ToCustomQuestModel(customQuestManager, quest));
    }

    private static IResult UnpublishQuestHandler(HttpContext context, int id, ICustomQuestManager customQuestManager)
    {
        var referee = GetReferee(context);

        if (!referee.IsAdmin)
            return AdminsOnly();

        if (!customQuestManager.Unpublish(id, referee.Id) || !customQuestManager.TryGet(id, out var quest))
            return Results.NotFound("There's no such quest.");

        Audit(referee, "Quest Unpublish", quest.Title, $"Quest: {CustomQuestManager.QuestIdBase + id}");

        return Results.Ok(ToCustomQuestModel(customQuestManager, quest));
    }

    private static IResult DeleteQuestHandler(HttpContext context, int id, ICustomQuestManager customQuestManager)
    {
        var referee = GetReferee(context);

        if (!referee.IsAdmin)
            return AdminsOnly();

        if (!customQuestManager.TryGet(id, out var quest) || !customQuestManager.Delete(id))
            return Results.NotFound("There's no such quest.");

        Audit(referee, "Quest Delete", quest.Title, $"Quest: {CustomQuestManager.QuestIdBase + id}");

        return Results.NoContent();
    }

    /// <summary>
    /// What's wrong with a quest's fields, or null. A draft may leave goals unfinished (no NPC, node type or spot
    /// yet); publishing checks those.
    /// </summary>
    private static string? CheckQuest(int id, CustomQuestRequest request, ICustomQuestManager customQuestManager, IResourceManager resourceManager)
    {
        var title = request.Title?.Trim() ?? string.Empty;

        if (title.Length is 0 or > MaxQuestTitle)
            return $"A quest's title is 1-{MaxQuestTitle} characters.";

        if (request.Description?.Trim().Length > MaxQuestText)
            return $"The description is at most {MaxQuestText} characters.";

        if (request.TitleId < 0 || request.DescriptionId < 0)
            return "Locale string ids are 0 (the generic ones) or more.";

        foreach (var (npcId, role) in new[] { (request.GiverNpcId, "giver"), (request.TurnInNpcId, "turn-in NPC") })
        {
            if (npcId != 0 && !resourceManager.Npcs.ContainsKey(npcId))
                return $"There's no NPC {npcId} for the {role}.";
        }

        if (request.RewardCoins is < 0 or > 10_000_000)
            return "Reward coins are from 0 to 10,000,000.";

        foreach (var (questId, role) in new[] { (request.PrerequisiteQuestId, "prerequisite"), (request.NextQuestId, "next quest") })
        {
            if (questId is not { } other)
                continue;

            if (other == CustomQuestManager.QuestIdBase + id)
                return $"A quest can't be its own {role}.";

            var exists = CustomQuestManager.IsCustomQuestId(other)
                ? customQuestManager.TryGet(other - CustomQuestManager.QuestIdBase, out _)
                : resourceManager.Quests.Quests.ContainsKey(other);

            if (!exists)
                return $"There's no quest {other} for the {role}.";
        }

        var goals = request.Goals ?? [];

        if (goals.Count > CustomQuestManager.MaxGoals)
            return $"A quest has at most {CustomQuestManager.MaxGoals} goals.";

        for (var i = 0; i < goals.Count; i++)
        {
            var goal = goals[i];
            var name = $"Goal {i + 1}";

            switch (goal.Type)
            {
                case CustomQuestGoal.Talk when goal.NpcId != 0 && !resourceManager.Npcs.ContainsKey(goal.NpcId):
                    return $"{name}: there's no NPC {goal.NpcId}.";

                case CustomQuestGoal.Collect when !string.IsNullOrEmpty(goal.NodeType) && !resourceManager.CollectionNodeTypes.ContainsKey(goal.NodeType):
                    return $"{name}: there's no node type {goal.NodeType}.";

                case CustomQuestGoal.Collect when goal.Count is < 0 or > 999:
                    return $"{name}: gather from 1 to 999.";

                case CustomQuestGoal.Reach when goal.ZoneId != 0 && !resourceManager.Zones.ContainsKey(goal.ZoneId):
                    return $"{name}: there's no zone {goal.ZoneId}.";

                case CustomQuestGoal.Reach when goal.Radius is < 1 or > 500:
                    return $"{name}: the radius is from 1 to 500.";

                case not (CustomQuestGoal.Talk or CustomQuestGoal.Collect or CustomQuestGoal.Reach):
                    return $"{name} is a talk, collect or reach goal.";
            }

            if (goal.Text?.Trim().Length > MaxQuestText)
                return $"{name}'s text is at most {MaxQuestText} characters.";
        }

        var rewards = request.Rewards ?? [];

        if (rewards.Count > CustomQuestManager.MaxRewards)
            return $"A quest gives at most {CustomQuestManager.MaxRewards} kinds of item.";

        foreach (var reward in rewards)
        {
            if (!resourceManager.ClientItemDefinitions.ContainsKey(reward.ItemId))
                return $"There's no item {reward.ItemId} to reward.";

            if (reward.Quantity is < 1 or > 99)
                return "Reward items come 1 to 99 at a time.";
        }

        return null;
    }

    private static CustomQuestGoal TrimGoal(CustomQuestGoal goal)
    {
        goal.Text = string.IsNullOrWhiteSpace(goal.Text) ? null : goal.Text.Trim();
        goal.NodeType = string.IsNullOrWhiteSpace(goal.NodeType) ? null : goal.NodeType.Trim();
        return goal;
    }

    private static CustomQuestModel ToCustomQuestModel(ICustomQuestManager customQuestManager, Database.Entities.DbCustomQuest quest) => new(
        quest.Id,
        CustomQuestManager.QuestIdBase + quest.Id,
        quest.Title,
        quest.Description,
        quest.TitleId,
        quest.DescriptionId,
        quest.GiverNpcId,
        quest.TurnInNpcId,
        quest.RewardCoins,
        quest.PrerequisiteQuestId,
        quest.NextQuestId,
        CustomQuestJson.ReadList<CustomQuestGoal>(quest.Goals),
        CustomQuestJson.ReadList<CustomQuestReward>(quest.Rewards),
        quest.IsPublished,
        customQuestManager.Validate(quest),
        quest.Updated);

    private static string? NpcName(IResourceManager resourceManager, int npcId) =>
        npcId != 0 && resourceManager.Npcs.TryGetValue(npcId, out var npc)
            ? npc.Name ?? GameNameHelper.Get(npc.NameId) ?? npc.ModelFileName
            : null;

    #endregion

    #region Conversations

    /// <summary>
    /// The world NPCs in running zones (those that talk first, with their own conversation or their definition's),
    /// whose name, definition, zone or greeting has <paramref name="search"/> in it. Their own conversations are read
    /// and saved through /zones/{key}/objects/{id}/conversation.
    /// </summary>
    private static IResult ConversationsHandler(HttpContext context, string? search, IZoneManager zoneManager, IResourceManager resourceManager)
    {
        if (!GetReferee(context).IsAdmin)
            return ConversationsAdminsOnly();

        var npcs = zoneManager.Zones
            .OfType<WorldZone>()
            .SelectMany(zone => zone.GetWorldNpcs().Select(x => (Zone: zone, x.Guid, x.WorldNpc)))
            .Select(x =>
            {
                var definitionConversation = resourceManager.Npcs.TryGetValue(x.WorldNpc.Definition, out var definition) ? definition.Conversation : null;
                var conversation = Conversation.FromJson(x.WorldNpc.Conversation);
                var spoken = conversation ?? definitionConversation;
                var greeting = spoken?.GetNode(spoken.Start)?.Text;
                var definitionName = NpcName(resourceManager, x.WorldNpc.Definition);

                return new WorldNpcModel(
                    ZoneKey(x.Zone),
                    x.Zone.Name,
                    Id(x.Guid),
                    x.WorldNpc.Definition,
                    x.WorldNpc.Name ?? definitionName ?? $"NPC {x.WorldNpc.Definition}",
                    definitionName,
                    conversation?.Nodes.Count ?? 0,
                    greeting is { Length: > 120 } ? $"{greeting[..120]}…" : greeting,
                    Round(x.WorldNpc.PositionX),
                    Round(x.WorldNpc.PositionZ),
                    definitionConversation?.Nodes.Count ?? 0);
            })
            .Where(npc => string.IsNullOrWhiteSpace(search)
                || $"{npc.Name} {npc.DefinitionName} {npc.Definition} {npc.ZoneName} {npc.Greeting}".Contains(search.Trim(), StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(npc => npc.Nodes > 0 || npc.DefinitionNodes > 0)
            .ThenBy(npc => npc.Name, StringComparer.OrdinalIgnoreCase)
            .Take(MaxWorldNpcs)
            .ToArray();

        return Results.Ok(npcs);
    }

    #endregion
}
