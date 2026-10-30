using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text.Json;

using Microsoft.Extensions.Logging;

using Sanctuary.Core.Collections;
using Sanctuary.Game.Resources.Definitions;

namespace Sanctuary.Game.Resources;

/// <summary>
/// The battles by their Battles.json id.
/// </summary>
public class BattleDefinitionCollection : ObservableConcurrentDictionary<int, BattleDefinition>
{
    /// <summary>
    /// A battle's Games window tile id, clear of the minigames' and fishing's ids.
    /// </summary>
    public const int ActivityIdBase = 9000;

    private readonly ILogger _logger;

    public BattleDefinitionCollection(ILogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// The battles an NPC (an Npcs.json id) offers when clicked.
    /// </summary>
    public IEnumerable<BattleDefinition> OfferedBy(int npcDefinitionId) =>
        Values.Where(battle => battle.StarterNpcIds.Contains(npcDefinitionId)).OrderBy(battle => battle.Id);

    public bool IsStarter(int npcDefinitionId) => Values.Any(battle => battle.StarterNpcIds.Contains(npcDefinitionId));

    public bool TryGetByActivity(int activityId, [MaybeNullWhen(false)] out BattleDefinition battle) =>
        TryGetValue(activityId - ActivityIdBase, out battle) && battle.Image is not null;

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

            var entries = JsonSerializer.Deserialize<List<BattleDefinition>>(fileStream, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip
            });

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
                    _logger.LogError("Invalid battle {id} ({comment}): {error} \"{file}\"", entry.Id, entry.Comment, error, filePath);
                    return false;
                }

                if (!TryAdd(entry.Id, entry))
                {
                    _logger.LogError("Battle {id} is listed twice. \"{file}\"", entry.Id, filePath);
                    return false;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse file \"{file}\".", filePath);
            return false;
        }

        return true;
    }

    public static bool IsValid(BattleDefinition definition, out string error)
    {
        error = definition switch
        {
            { Id: <= 0 or > 0xFFFF } => "its id must be 1 to 65535",
            { ZoneId: <= 0 } or { NameId: <= 0 } => "it needs a zone and a name",
            { Entry.Length: not 3 } => "its entry must be x, y, z",
            { Exit.Length: not 3 } => "its exit must be x, y, z",
            { Waves.Count: 0 } => "it needs at least one wave",
            _ when definition.Waves.Any(wave => wave.Spawns.Count == 0) => "every wave needs an enemy",
            _ when definition.Waves.SelectMany(wave => wave.Spawns).Any(spawn => spawn.NpcId <= 0 || spawn.Position.Length != 3) =>
                "every enemy needs an NPC and a position (x, y, z)",
            { TimeLimitSeconds: <= 0 } or { RespawnSeconds: <= 0 } or { MaxKnockouts: <= 0 } or { MaxPlayers: <= 0 } =>
                "its time limit, respawn, knockouts and players must be above 0",
            { Difficulty: < 1 or > 5 } => "its difficulty must be 1 to 5",
            { Stars: < 0 } or { Coins: < 0 } => "its rewards can't be negative",
            _ => string.Empty
        };

        return error.Length == 0;
    }
}
