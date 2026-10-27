using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text.Json;

using Microsoft.Extensions.Logging;

using Sanctuary.Core.Collections;
using Sanctuary.Game.Resources.Definitions.Combat;

namespace Sanctuary.Game.Resources;

public sealed class MobDefinitionCollection : ObservableConcurrentDictionary<string, MobDefinition>
{
    private readonly ILogger _logger;
    private readonly object _writeLock = new();

    public MobDefinitionCollection(ILogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// The named entry, falling back to the default one for an unknown or empty key.
    /// </summary>
    public bool TryGetMob(string? key, [MaybeNullWhen(false)] out MobDefinition mob)
    {
        if (!string.IsNullOrWhiteSpace(key) && TryGetValue(key.Trim().ToLowerInvariant(), out mob))
            return true;

        return TryGetValue(MobDefinition.DefaultKey, out mob);
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
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);

            return Load(JsonSerializer.Deserialize<List<MobDefinition>>(stream, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }), filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse file \"{file}\".", filePath);
            return false;
        }
    }

    public bool Load(List<MobDefinition>? entries, string source)
    {
        if (entries is null || entries.Count == 0)
        {
            _logger.LogError("No mobs found in \"{file}\".", source);
            return false;
        }

        var loaded = new Dictionary<string, MobDefinition>();

        foreach (var entry in entries)
        {
            entry.Key = entry.Key.Trim().ToLowerInvariant();

            if (entry.Key.Length == 0 || entry.MaxHitpoints <= 0 || entry.Damage < 0 || entry.AttackIntervalMs <= 0 ||
                entry.AttackRange <= 0f || entry.AggroRadius < 0f || entry.LeashRadius <= entry.AggroRadius || entry.RespawnSeconds < 0)
            {
                _logger.LogError("Invalid mob \"{key}\" in \"{file}\".", entry.Key, source);
                return false;
            }

            if (!loaded.TryAdd(entry.Key, entry))
            {
                _logger.LogError("Duplicate mob \"{key}\" in \"{file}\".", entry.Key, source);
                return false;
            }
        }

        if (!loaded.ContainsKey(MobDefinition.DefaultKey))
        {
            _logger.LogError("No \"{key}\" mob in \"{file}\".", MobDefinition.DefaultKey, source);
            return false;
        }

        lock (_writeLock)
        {
            foreach (var entry in loaded)
                this[entry.Key] = entry.Value;

            foreach (var key in Keys.Where(key => !loaded.ContainsKey(key)).ToArray())
                Remove(key);
        }

        return true;
    }
}
