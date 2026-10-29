using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.Extensions.Logging;

using Sanctuary.Core.Collections;
using Sanctuary.Game.Resources.Definitions;

namespace Sanctuary.Game.Resources;

/// <summary>
/// Furniture behaviours by the item they're placed from.
/// </summary>
public class HousingFixtureDefinitionCollection : ObservableConcurrentDictionary<int, HousingFixtureDefinition>
{
    private readonly ILogger _logger;

    public HousingFixtureDefinitionCollection(ILogger logger)
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

            var jsonSerializerOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new JsonStringEnumConverter() }
            };

            var entries = JsonSerializer.Deserialize<List<HousingFixtureDefinition>>(fileStream, jsonSerializerOptions);

            if (entries is null)
            {
                _logger.LogError("No entries found in file \"{file}\".", filePath);
                return false;
            }

            Clear();

            foreach (var entry in entries)
            {
                if (!IsValid(entry, out var error))
                {
                    _logger.LogError("Invalid fixture behaviour for items {items}: {error} \"{file}\"",
                        string.Join(", ", entry.ItemDefinitionIds), error, filePath);
                    return false;
                }

                foreach (var itemDefinitionId in entry.ItemDefinitionIds)
                {
                    if (!TryAdd(itemDefinitionId, entry))
                    {
                        _logger.LogError("Item {item} has more than one fixture behaviour. \"{file}\"", itemDefinitionId, filePath);
                        return false;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse file \"{file}\".", filePath);
            return false;
        }

        if (Count == 0)
        {
            _logger.LogError("No data was loaded. \"{file}\"", filePath);
            return false;
        }

        return true;
    }

    public static bool IsValid(HousingFixtureDefinition definition, out string error)
    {
        error = string.Empty;

        if (definition.ItemDefinitionIds.Count == 0)
            error = "it has no items";
        else if (definition.AreaDefinitionId == 0 && definition.Platform is null && definition.Teleporter is null && string.IsNullOrEmpty(definition.ModelName))
            error = "it does nothing";
        else if (definition.Platform is not null && definition.Teleporter is not null)
            error = "a teleporter can't also be a platform";
        else if (definition.Platform is { } platform)
        {
            if (platform.Path.Count < 2)
                error = "a platform path needs at least two points";
            else if (platform.Path.Any(point => point.Length != 3 || point.Any(value => !float.IsFinite(value))))
                error = "platform path points are [x, y, z]";
            else if (!float.IsFinite(platform.Speed) || platform.Speed <= 0f)
                error = "a platform's speed must be above 0";
            else if (!float.IsFinite(platform.PauseSeconds) || platform.PauseSeconds < 0f)
                error = "a platform's pause can't be negative";
        }
        else if (definition.Teleporter is { } teleporter && !Enum.IsDefined(teleporter.Mode))
            error = "unknown teleporter mode";

        return error.Length == 0;
    }
}
