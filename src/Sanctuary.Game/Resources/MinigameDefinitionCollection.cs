using System;
using System.IO;
using System.Linq;
using System.Text.Json;

using Microsoft.Extensions.Logging;

using Sanctuary.Core.Collections;
using Sanctuary.Game.Helpers;
using Sanctuary.Game.Resources.Definitions;

namespace Sanctuary.Game.Resources;

public class MinigameDefinitionCollection : ObservableConcurrentDictionary<int, MinigameDefinition>
{
    private readonly ILogger _logger;

    public MinigameDefinitionCollection(ILogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Loads every .json in the folder: arrays of games, one file per game family.
    /// </summary>
    public bool Load(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
        {
            _logger.LogError("Failed to find directory \"{directory}\"", directoryPath);
            return false;
        }

        var jsonSerializerOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        };

        foreach (var filePath in Directory.GetFiles(directoryPath, "*.json", SearchOption.TopDirectoryOnly).Order())
        {
            try
            {
                using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);

                using var jsonDocument = JsonDocument.Parse(fileStream, new JsonDocumentOptions
                {
                    CommentHandling = JsonCommentHandling.Skip
                });

                ResourceHelper.LoadDefinitions<MinigameDefinition>(jsonDocument.RootElement, jsonSerializerOptions, _logger, filePath,
                    entry => entry.Id,
                    entry => entry.Id > 0 && !string.IsNullOrEmpty(entry.Game) && !string.IsNullOrEmpty(entry.Swf)
                        && entry.Comment is not null && entry.Coins >= 0 && entry.Stars >= 0 && entry.LossShare is >= 0 and <= 1
                        && entry.Items is not null && entry.Items.All(item => item is not null && item.ItemId > 0 && item.Quantity > 0)
                        && entry.RewardTable is not null && entry.DetailImage is not null && entry.ThumbnailImage is not null,
                    TryAdd);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse file \"{file}\".", filePath);
                return false;
            }
        }

        if (Count == 0)
        {
            _logger.LogError("No minigames were loaded. \"{directory}\"", directoryPath);
            return false;
        }

        return true;
    }
}
