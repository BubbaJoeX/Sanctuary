using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

using Microsoft.Extensions.Logging;

using Sanctuary.Core.Collections;
using Sanctuary.Game.Resources.Definitions;

namespace Sanctuary.Game.Resources;

/// <summary>
/// The recipes by their Recipes.json id.
/// </summary>
public class RecipeDefinitionCollection : ObservableConcurrentDictionary<int, RecipeDefinition>
{
    /// <summary>
    /// The recipe window shows no more ingredients than this.
    /// </summary>
    public const int MaxComponents = 8;

    private readonly ILogger _logger;

    private Dictionary<int, RecipeDefinition> _byMinigame = [];

    public RecipeDefinitionCollection(ILogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Lets every recipe ingredient stack: the client's definitions cap some parts and ores at one each, which the
    /// live recipes (several straps and planks at once) show they weren't.
    /// </summary>
    public int UnlockIngredientStacks(ClientItemDefinitionCollection items)
    {
        var unlocked = 0;

        foreach (var itemId in Values.SelectMany(recipe => recipe.Components).Select(component => component.ItemId).Distinct())
        {
            if (items.TryGetValue(itemId, out var item) && item.MaxStackSize == 1)
            {
                item.MaxStackSize = -1;
                unlocked++;
            }
        }

        return unlocked;
    }

    /// <summary>
    /// The recipe a minigame makes, if any.
    /// </summary>
    public bool TryGetByMinigame(int minigameId, out RecipeDefinition recipe) =>
        _byMinigame.TryGetValue(minigameId, out recipe!);

    public bool Load(string filePath)
    {
        if (!TryRead<RecipeDefinition>(_logger, filePath, out var entries))
            return false;

        var byMinigame = new Dictionary<int, RecipeDefinition>();

        Clear();

        foreach (var entry in entries)
        {
            if (!IsValid(entry, out var error))
            {
                _logger.LogError("Invalid recipe {id} ({comment}): {error} \"{file}\"", entry.Id, entry.Comment, error, filePath);
                return false;
            }

            if (!TryAdd(entry.Id, entry))
            {
                _logger.LogError("Recipe {id} is listed twice. \"{file}\"", entry.Id, filePath);
                return false;
            }

            if (entry.MinigameId is { } minigameId && !byMinigame.TryAdd(minigameId, entry))
            {
                _logger.LogError("Recipes {first} and {second} are both made by minigame {game}. \"{file}\"",
                    byMinigame[minigameId].Id, entry.Id, minigameId, filePath);
                return false;
            }
        }

        _byMinigame = byMinigame;

        return true;
    }

    public static bool IsValid(RecipeDefinition definition, out string error)
    {
        error = definition switch
        {
            { Id: <= 0 } => "its id must be above 0",
            { ResultItemId: <= 0 } => "it needs a result item",
            { Components.Count: 0 } => "it needs an ingredient",
            { Components.Count: > MaxComponents } => $"the recipe window shows at most {MaxComponents} ingredients",
            _ when definition.Components.Any(component => component.ItemId <= 0 || component.Required <= 0) =>
                "every ingredient needs an item and a count above 0",
            _ when definition.Components.Select(component => component.ItemId).Distinct().Count() != definition.Components.Count =>
                "an ingredient is listed twice",
            { MinProfileRank: < 1 } => "its rank must be 1 or more",
            _ => string.Empty
        };

        return error.Length == 0;
    }

    internal static bool TryRead<T>(ILogger logger, string filePath, out List<T> entries)
    {
        entries = [];

        if (!File.Exists(filePath))
        {
            logger.LogError("Failed to find file \"{file}\"", filePath);
            return false;
        }

        try
        {
            using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);

            var read = JsonSerializer.Deserialize<List<T>>(fileStream, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip
            });

            if (read is null)
            {
                logger.LogError("No entries found in file \"{file}\".", filePath);
                return false;
            }

            entries = read;
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to parse file \"{file}\".", filePath);
            return false;
        }
    }
}

/// <summary>
/// The crafting and minigame stations by their Npcs.json id.
/// </summary>
public class StationDefinitionCollection : ObservableConcurrentDictionary<int, StationDefinition>
{
    private readonly ILogger _logger;

    public StationDefinitionCollection(ILogger logger)
    {
        _logger = logger;
    }

    public bool Load(string filePath)
    {
        if (!RecipeDefinitionCollection.TryRead<StationDefinition>(_logger, filePath, out var entries))
            return false;

        Clear();

        foreach (var entry in entries)
        {
            if (entry.NpcId <= 0 || entry.RecipeFilters.Count == 0 && entry.MinigameIds.Count == 0)
            {
                _logger.LogError("Invalid station {id} ({comment}): it needs an NPC and a recipe filter or minigames. \"{file}\"",
                    entry.NpcId, entry.Comment, filePath);
                return false;
            }

            if (!TryAdd(entry.NpcId, entry))
            {
                _logger.LogError("Station {id} is listed twice. \"{file}\"", entry.NpcId, filePath);
                return false;
            }
        }

        return true;
    }
}
