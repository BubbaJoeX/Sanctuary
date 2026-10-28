using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

using Microsoft.Extensions.Logging;

using Sanctuary.Core.Collections;
using Sanctuary.Game.Resources.Definitions;

namespace Sanctuary.Game.Resources;

/// <summary>
/// Seasonal holidays (Resources/Seasons.json) by event key, lower case.
/// </summary>
public class SeasonDefinitionCollection : ObservableConcurrentDictionary<string, SeasonDefinition>
{
    private readonly ILogger _logger;

    public SeasonDefinitionCollection(ILogger logger)
    {
        _logger = logger;
    }

    public bool Load(string filePath)
    {
        if (!File.Exists(filePath))
        {
            _logger.LogError("Failed to find file \"{file}\"", filePath);
            return false;
        }

        try
        {
            using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);

            var list = JsonSerializer.Deserialize<List<SeasonDefinition>>(fileStream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (list is null)
            {
                _logger.LogError("No entries found in file \"{file}\".", filePath);
                return false;
            }

            foreach (var entry in list)
            {
                if (string.IsNullOrWhiteSpace(entry.Key) || entry.Key.Length > 32
                    || entry.StartMonth is < 1 or > 12 || entry.EndMonth is < 1 or > 12
                    || entry.StartDay is < 1 or > 31 || entry.EndDay is < 1 or > 31)
                {
                    _logger.LogWarning("Skipping season \"{key}\": a key of up to 32 characters and real dates. \"{file}\"", entry.Key, filePath);
                    continue;
                }

                entry.Key = entry.Key.ToLowerInvariant();

                if (!TryAdd(entry.Key, entry))
                    _logger.LogWarning("Failed to add entry. {key} \"{file}\"", entry.Key, filePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse file \"{file}\".", filePath);
            return false;
        }

        return true;
    }
}
