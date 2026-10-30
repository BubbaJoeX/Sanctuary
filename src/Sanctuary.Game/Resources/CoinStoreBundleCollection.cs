using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

using Microsoft.Extensions.Logging;

using Sanctuary.Core.Collections;
using Sanctuary.Game.Resources.Definitions;

namespace Sanctuary.Game.Resources;

/// <summary>
/// Coin store bundles by the item they're sold as. The file is optional: without it there are none.
/// </summary>
public class CoinStoreBundleCollection : ObservableConcurrentDictionary<int, CoinStoreBundleDefinition>
{
    private readonly ILogger _logger;

    public CoinStoreBundleCollection(ILogger logger)
    {
        _logger = logger;
    }

    public bool Load(string filePath)
    {
        if (!File.Exists(filePath))
            return true;

        try
        {
            using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);

            var entries = JsonSerializer.Deserialize<List<CoinStoreBundleDefinition>>(fileStream, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            foreach (var entry in entries ?? [])
            {
                if (entry.Items.Count == 0 && entry.Coins <= 0)
                {
                    _logger.LogWarning("Bundle {id} gives nothing, so it isn't sold. \"{file}\"", entry.Id, filePath);
                    continue;
                }

                if (entry.Coins < 0 || entry.Items.Exists(x => x.Quantity is < 1 or > 999))
                {
                    _logger.LogWarning("Bundle {id} has a quantity out of range, so it isn't sold. \"{file}\"", entry.Id, filePath);
                    continue;
                }

                if (!TryAdd(entry.Id, entry))
                    _logger.LogWarning("Bundle {id} is in the file twice. \"{file}\"", entry.Id, filePath);
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
