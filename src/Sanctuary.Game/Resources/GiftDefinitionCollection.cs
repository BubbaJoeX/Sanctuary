using System.Collections.Generic;
using System.Linq;

using Microsoft.Extensions.Logging;

using Sanctuary.Core.Collections;
using Sanctuary.Game.Resources.Definitions;

namespace Sanctuary.Game.Resources;

/// <summary>
/// The gifts by the item that opens into each.
/// </summary>
public class GiftDefinitionCollection : ObservableConcurrentDictionary<int, GiftDefinition>
{
    /// <summary>
    /// The item type the client gives a Use button that asks before opening (live's mystery chests). Gifts go out
    /// as it.
    /// </summary>
    public const int OpenableItemType = 28;

    private readonly ILogger _logger;

    public GiftDefinitionCollection(ILogger logger)
    {
        _logger = logger;
    }

    public bool Load(string filePath, RewardTableDefinitionCollection rewardTables)
    {
        if (!RecipeDefinitionCollection.TryRead<GiftDefinition>(_logger, filePath, out var entries))
            return false;

        Clear();

        foreach (var entry in entries)
        {
            if (entry.ItemIds.Count == 0 || entry.Fixed.Count == 0 && entry.Rolls.Count == 0)
            {
                _logger.LogError("Invalid gift {comment}: it needs items and something to give. \"{file}\"", entry.Comment, filePath);
                return false;
            }

            var tables = entry.Rolls.SelectMany(roll => roll.Choose.Count > 0 ? roll.Choose.Select(choice => choice.Table) : [roll.Table])
                .Where(table => !string.IsNullOrWhiteSpace(table));

            if (entry.Rolls.Any(roll => roll.Count <= 0 || roll.Choose.Count == 0 && string.IsNullOrWhiteSpace(roll.Table)) ||
                tables.FirstOrDefault(table => !rewardTables.ContainsKey(table!.Trim().ToLowerInvariant())) is { } missing)
            {
                _logger.LogError("Gift {comment} rolls on a table that isn't in Rewards.json, or rolls none. \"{file}\"", entry.Comment, filePath);
                return false;
            }

            foreach (var itemId in entry.ItemIds)
            {
                if (!TryAdd(itemId, entry))
                {
                    _logger.LogError("Item {item} is in two gifts. \"{file}\"", itemId, filePath);
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    /// Gives the gifts' items the type that lets players open them from the inventory.
    /// </summary>
    public void MarkOpenable(ClientItemDefinitionCollection items)
    {
        foreach (var itemId in Keys)
        {
            if (items.TryGetValue(itemId, out var item))
                item.Type = OpenableItemType;
        }
    }
}
