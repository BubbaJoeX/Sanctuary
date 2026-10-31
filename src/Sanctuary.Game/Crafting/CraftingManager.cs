using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Sanctuary.Database;
using Sanctuary.Game.Achievements;
using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;
using Sanctuary.Game.Minigames;
using Sanctuary.Game.Resources.Definitions;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game.Crafting;

/// <summary>
/// Crafting with recipes (Recipes.json). A player learns a recipe by getting its card; a crafting station opens the
/// recipe window on its recipes; Make It starts the recipe's minigame, and winning it takes the ingredients and pays
/// what the game makes.
/// </summary>
public class CraftingManager : ICraftingManager
{
    /// <summary>
    /// A recipe card: an item of this type whose Param1 is the recipe's id.
    /// </summary>
    public const int RecipeCardItemType = 5;

    private readonly ILogger _logger;
    private readonly IResourceManager _resourceManager;
    private readonly IRewardManager _rewardManager;
    private readonly IDbContextFactory<DatabaseContext> _dbContextFactory;

    /// <summary>
    /// The recipe each player started with Make It, until its game is won.
    /// </summary>
    private readonly ConcurrentDictionary<ulong, RecipeDefinition> _pending = new();

    public CraftingManager(ILogger<CraftingManager> logger, IResourceManager resourceManager, IRewardManager rewardManager,
        IDbContextFactory<DatabaseContext> dbContextFactory)
    {
        _logger = logger;
        _resourceManager = resourceManager;
        _rewardManager = rewardManager;
        _dbContextFactory = dbContextFactory;
    }

    public void Load(Player player, IEnumerable<int>? saved)
    {
        lock (player.KnownRecipes)
        {
            player.KnownRecipes.Clear();

            foreach (var recipeId in saved ?? [])
                player.KnownRecipes.Add(recipeId);
        }

        LearnHeldCards(player, announce: false);

        lock (player.KnownRecipes)
        {
            foreach (var recipeId in player.KnownRecipes)
                player.Achievements.Record(AchievementEvent.Recipe, recipeId);
        }
    }

    public List<RecipeData> GetRecipes(Player player)
    {
        int[] known;

        lock (player.KnownRecipes)
            known = [.. player.KnownRecipes.Order()];

        var list = new List<RecipeData>();

        foreach (var recipeId in known)
        {
            if (_resourceManager.Recipes.TryGetValue(recipeId, out var recipe) && TryBuild(player, recipe, out var data))
                list.Add(data);
        }

        return list;
    }

    public void OpenStation(Player player, int filterType)
    {
        LearnHeldCards(player, announce: true);

        // The window counts ingredients from the stacks the list names, so it gets the stacks as they are now.
        var update = new RecipeUpdatePacket();
        update.Recipes.AddRange(GetRecipes(player));
        player.SendTunneled(update);

        if (!update.Recipes.Any(recipe => recipe.FilterType == filterType))
        {
            ChatHelper.SendSystemMessage(player, "You don't know any recipes for this yet. Recipe cards teach them: find them at the shops.");
            return;
        }

        player.SendTunneled(new CommandPacketShowRecipeWindow { FilterType = filterType });
    }

    public void Start(Player player, int recipeId)
    {
        bool known;

        lock (player.KnownRecipes)
            known = player.KnownRecipes.Contains(recipeId);

        if (!known || !_resourceManager.Recipes.TryGetValue(recipeId, out var recipe))
        {
            ChatHelper.SendSystemMessage(player, "You don't know that recipe.");
            return;
        }

        if (!player.IsAdmin && RankFor(player, recipe) < recipe.MinProfileRank)
        {
            ChatHelper.SendSystemMessage(player, $"That recipe needs rank {recipe.MinProfileRank} as a {JobName(recipe)}.");
            return;
        }

        if (Missing(player, recipe) is { Count: > 0 } missing)
        {
            ChatHelper.SendSystemMessage(player, $"You still need {string.Join(", ", missing)}.");
            return;
        }

        if (recipe.MinigameId is { } minigameId)
        {
            _pending[player.Guid] = recipe;

            if (!MinigameManager.Join(player, minigameId))
            {
                _pending.TryRemove(player.Guid, out _);
                ChatHelper.SendSystemMessage(player, "That recipe can't be made right now.");
            }

            return;
        }

        // A recipe with no game (a farm order) is made at once.
        if (!TryTakeIngredients(player, recipe))
            return;

        if (_rewardManager.TryGrantItem(player, recipe.ResultItemId, 0))
            ChatHelper.SendSystemMessage(player, $"You made {ItemName(recipe.ResultItemId)}.");
    }

    public bool TryCompleteCraft(Player player, int minigameId, out string? hint)
    {
        hint = null;

        if (!_resourceManager.Recipes.TryGetByMinigame(minigameId, out var recipe))
            return true;

        if (!_pending.TryGetValue(player.Guid, out var pending) || pending.Id != recipe.Id)
        {
            hint = $"To keep what you make, make it from its recipe at a {StationName(recipe.FilterType)}.";
            return false;
        }

        _pending.TryRemove(player.Guid, out _);

        if (!TryTakeIngredients(player, recipe))
        {
            hint = "You no longer have the ingredients, so nothing was made.";
            return false;
        }

        _logger.LogInformation("{player} crafted recipe {id} ({comment}).", player.Name.FullName, recipe.Id, recipe.Comment);
        return true;
    }

    /// <summary>
    /// Learns the recipes of the recipe cards the player holds that they don't know yet. The cards stay: live players
    /// kept theirs.
    /// </summary>
    private void LearnHeldCards(Player player, bool announce)
    {
        var learned = new List<RecipeDefinition>();

        lock (player.KnownRecipes)
        {
            foreach (var item in player.Items)
            {
                if (!_resourceManager.ClientItemDefinitions.TryGetValue(item.Definition, out var definition) ||
                    definition.Type != RecipeCardItemType ||
                    !_resourceManager.Recipes.TryGetValue(definition.Param1, out var recipe) ||
                    !player.KnownRecipes.Add(recipe.Id))
                    continue;

                learned.Add(recipe);
            }
        }

        if (learned.Count == 0)
            return;

        Save(player);

        foreach (var recipe in learned)
        {
            _logger.LogInformation("{player} learned recipe {id} ({comment}).", player.Name.FullName, recipe.Id, recipe.Comment);

            // The client prints '"X" recipe learned' itself.
            if (announce && TryBuild(player, recipe, out var data))
                player.SendTunneled(new RecipeAddPacket { Recipe = data });

            player.Achievements.Record(AchievementEvent.Recipe, recipe.Id);
        }
    }

    private bool TryTakeIngredients(Player player, RecipeDefinition recipe)
    {
        var taken = new List<RecipeComponentDefinition>();

        foreach (var component in recipe.Components)
        {
            if (_rewardManager.TryTakeItem(player, component.ItemId, component.Required))
            {
                taken.Add(component);
                continue;
            }

            // Give back what was already taken.
            foreach (var giveBack in taken)
                _rewardManager.TryGrantItem(player, giveBack.ItemId, 0, giveBack.Required);

            ChatHelper.SendSystemMessage(player, $"You need {component.Required} {ItemName(component.ItemId)}.");
            return false;
        }

        return true;
    }

    /// <summary>
    /// The ingredients the player is short of, as "3 Copper Ore".
    /// </summary>
    private List<string> Missing(Player player, RecipeDefinition recipe)
    {
        var missing = new List<string>();

        foreach (var component in recipe.Components)
        {
            var have = player.Items.Where(item => item.Definition == component.ItemId).Sum(Available);

            if (have < component.Required)
                missing.Add($"{component.Required - have} {ItemName(component.ItemId)}");
        }

        return missing;
    }

    private static int Available(ClientItem item) => Math.Max(0, item.Count - item.ConsumedCount);

    private int RankFor(Player player, RecipeDefinition recipe) =>
        player.Profiles.FirstOrDefault(profile => profile.Id == recipe.ProfileId)?.Rank ?? 0;

    /// <summary>
    /// The window's row for a recipe. False when an item it names is missing from the item definitions (it would show
    /// blank, and couldn't be made).
    /// </summary>
    private bool TryBuild(Player player, RecipeDefinition recipe, out RecipeData data)
    {
        data = null!;

        if (!_resourceManager.ClientItemDefinitions.TryGetValue(recipe.ResultItemId, out var result))
            return false;

        data = new RecipeData
        {
            Id = recipe.Id,
            NameId = result.NameId,
            IconId = result.Icon.Id,
            IconTint = result.Icon.TintId,
            DescriptionId = result.DescriptionId,
            FilterType = recipe.FilterType,
            Group = recipe.Group,
            ResultItemId = recipe.ResultItemId
        };

        foreach (var component in recipe.Components)
        {
            if (!_resourceManager.ClientItemDefinitions.TryGetValue(component.ItemId, out var item))
                return false;

            // The client counts one stack: the fullest.
            var stack = player.Items
                .Where(owned => owned.Definition == component.ItemId)
                .OrderByDescending(Available)
                .FirstOrDefault();

            data.Components.Add(new RecipeComponentData
            {
                NameId = item.NameId,
                IconId = item.Icon.Id,
                IconTint = item.Icon.TintId,
                DescriptionId = item.DescriptionId,
                Required = component.Required,
                ItemInstanceId = stack?.Id ?? 0,
                ItemId = component.ItemId
            });
        }

        return true;
    }

    private void Save(Player player)
    {
        List<int> recipes;

        lock (player.KnownRecipes)
            recipes = [.. player.KnownRecipes.Order()];

        try
        {
            using var db = _dbContextFactory.CreateDbContext();

            var dbCharacter = db.Characters.SingleOrDefault(character => character.Id == player.CharacterId);

            if (dbCharacter is null)
                return;

            dbCharacter.Recipes = recipes;
            db.SaveChanges();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save {player}'s recipes.", player.Name.FullName);
        }
    }

    private string ItemName(int itemDefinitionId) =>
        _resourceManager.ClientItemDefinitions.TryGetValue(itemDefinitionId, out var definition) && !string.IsNullOrEmpty(definition.Comment)
            ? definition.Comment
            : $"item {itemDefinitionId}";

    private static string JobName(RecipeDefinition recipe) => recipe.FilterType switch
    {
        9 => "Miner",
        10 => "Chef",
        12 => "Blacksmith",
        _ => "crafter"
    };

    private static string StationName(int filterType) => filterType switch
    {
        9 => "Furnace",
        10 => "Cooking Table",
        12 => "Anvil",
        _ => "crafting station"
    };
}
