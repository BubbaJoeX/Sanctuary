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
/// The in-world challenges by their MiniGameData.txt id.
/// </summary>
public class ChallengeDefinitionCollection : ObservableConcurrentDictionary<int, ChallengeDefinition>
{
    private readonly ILogger _logger;

    public ChallengeDefinitionCollection(ILogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// The challenges an NPC (an Npcs.json id) offers.
    /// </summary>
    public IEnumerable<ChallengeDefinition> OfferedBy(int npcDefinitionId) =>
        Values.Where(challenge => challenge.GiverNpcId == npcDefinitionId).OrderBy(challenge => challenge.Id);

    public bool IsGiver(int npcDefinitionId) => Values.Any(challenge => challenge.GiverNpcId == npcDefinitionId);

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

            var entries = JsonSerializer.Deserialize<List<ChallengeDefinition>>(fileStream, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                Converters = { new JsonStringEnumConverter() }
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
                    _logger.LogError("Invalid challenge {id} ({comment}): {error} \"{file}\"", entry.Id, entry.Comment, error, filePath);
                    return false;
                }

                if (!TryAdd(entry.Id, entry))
                {
                    _logger.LogError("Challenge {id} is listed twice. \"{file}\"", entry.Id, filePath);
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

    public static bool IsValid(ChallengeDefinition definition, out string error)
    {
        error = definition switch
        {
            { Id: <= 0 } or { NameId: <= 0 } => "it needs its MiniGameData id and name",
            { GiverNpcId: <= 0 } => "it needs an NPC to offer it",
            { Count: <= 0 } or { TimeLimitSeconds: <= 0 } or { Radius: <= 0 } => "its count, time limit and radius must be above 0",
            { Kind: ChallengeKind.Collect or ChallengeKind.Return or ChallengeKind.Catch or ChallengeKind.Use, ModelId: <= 0 } => "it needs a model for what's collected, caught or used",
            { Kind: ChallengeKind.Deliver } when definition.RecipientNpcIds.Count < definition.Count => "it names fewer people than it delivers to",
            { Kind: ChallengeKind.Race, Count: < 2 } => "a race needs at least two flags",
            { Stars: < 0 } or { Coins: < 0 } => "its rewards can't be negative",
            _ => string.Empty
        };

        return error.Length == 0;
    }
}
