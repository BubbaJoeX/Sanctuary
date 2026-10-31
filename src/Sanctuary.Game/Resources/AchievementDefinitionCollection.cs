using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

using Microsoft.Extensions.Logging;

using Sanctuary.Core.Collections;
using Sanctuary.Game.Achievements;
using Sanctuary.Game.Resources.Definitions;

namespace Sanctuary.Game.Resources;

/// <summary>
/// The achievements by their Achievements.json id, with the trophies they're worth and, per game event, the
/// objectives that event counts toward.
/// </summary>
public class AchievementDefinitionCollection : ObservableConcurrentDictionary<int, AchievementDefinition>
{
    private readonly ILogger _logger;

    private Dictionary<int, AchievementTrophyDefinition> _trophies = [];

    private Dictionary<AchievementEvent, List<(AchievementDefinition Achievement, AchievementObjectiveDefinition Objective)>> _byEvent = [];

    private Dictionary<AchievementObjectiveDefinition, Regex[]> _killNames = [];

    public AchievementDefinitionCollection(ILogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// The objectives an event counts toward, in catalogue order.
    /// </summary>
    public IReadOnlyList<(AchievementDefinition Achievement, AchievementObjectiveDefinition Objective)> CountedBy(AchievementEvent achievementEvent) =>
        _byEvent.TryGetValue(achievementEvent, out var objectives) ? objectives : [];

    /// <summary>
    /// The points an achievement is worth when done: its trophy's, plus its bonus.
    /// </summary>
    public int PointsOf(AchievementDefinition achievement) =>
        (_trophies.TryGetValue(achievement.TrophyId, out var trophy) ? trophy.Points : 0) + achievement.BonusPoints;

    /// <summary>
    /// The trophy's effect played on whoever earns it, or 0.
    /// </summary>
    public int GainEffectOf(AchievementDefinition achievement) =>
        _trophies.TryGetValue(achievement.TrophyId, out var trophy) ? trophy.GainEffectId : 0;

    /// <summary>
    /// Whether a kill objective counts a mob with this name.
    /// </summary>
    public bool CountsKill(AchievementObjectiveDefinition objective, string? name, int nameId)
    {
        if (objective.Trigger?.NameIds is { } nameIds && nameId != 0 && nameIds.Contains(nameId))
            return true;

        return !string.IsNullOrEmpty(name) && _killNames.TryGetValue(objective, out var patterns) &&
            patterns.Any(pattern => pattern.IsMatch(name));
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

            var file = JsonSerializer.Deserialize<AchievementsFile>(fileStream, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                Converters = { new JsonStringEnumConverter() }
            });

            if (file is null || file.Achievements.Count == 0)
            {
                _logger.LogError("No entries found in file \"{file}\".", filePath);
                return false;
            }

            var trophies = file.Trophies.ToDictionary(trophy => trophy.Id);
            var byEvent = new Dictionary<AchievementEvent, List<(AchievementDefinition, AchievementObjectiveDefinition)>>();
            var killNames = new Dictionary<AchievementObjectiveDefinition, Regex[]>();

            Clear();

            foreach (var entry in file.Achievements)
            {
                if (!IsValid(entry, trophies, out var error))
                {
                    _logger.LogError("Invalid achievement {id} ({comment}): {error} \"{file}\"", entry.Id, entry.Comment, error, filePath);
                    return false;
                }

                if (!TryAdd(entry.Id, entry))
                {
                    _logger.LogError("Achievement {id} is listed twice. \"{file}\"", entry.Id, filePath);
                    return false;
                }

                foreach (var objective in entry.Objectives)
                {
                    if (objective.Trigger is not { Event: not AchievementEvent.None } trigger)
                        continue;

                    if (!byEvent.TryGetValue(trigger.Event, out var list))
                        byEvent[trigger.Event] = list = [];

                    list.Add((entry, objective));

                    if (trigger.Event == AchievementEvent.Kill && trigger.Names is { Count: > 0 } names)
                        killNames[objective] = names.Select(name => new Regex(name, RegexOptions.IgnoreCase | RegexOptions.Compiled)).ToArray();
                }
            }

            _trophies = trophies;
            _byEvent = byEvent;
            _killNames = killNames;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse file \"{file}\".", filePath);
            return false;
        }

        return true;
    }

    public static bool IsValid(AchievementDefinition definition, IReadOnlyDictionary<int, AchievementTrophyDefinition> trophies, out string error)
    {
        error = definition switch
        {
            { Id: <= 0 } => "its id must be above 0",
            { NameId: <= 0 } => "it needs a name",
            _ when !trophies.ContainsKey(definition.TrophyId) => $"trophy {definition.TrophyId} isn't in Trophies",
            { Objectives.Count: 0 } => "it needs an objective",
            _ when definition.Objectives.Select(objective => objective.Id).Distinct().Count() != definition.Objectives.Count =>
                "two of its objectives share an id",
            _ when definition.Objectives.Any(objective => objective.Required <= 0) => "every objective needs a count above 0",
            _ when definition.Objectives.Any(objective => objective.Trigger is { Event: AchievementEvent.Kill, Names: null or { Count: 0 }, NameIds: null or { Count: 0 } }) =>
                "a kill objective needs Names or NameIds",
            { Reward: { ItemId: <= 0 } or { Quantity: <= 0 } } => "its reward needs an item and a quantity",
            _ => string.Empty
        };

        return error.Length == 0;
    }

    private sealed class AchievementsFile
    {
        public List<AchievementTrophyDefinition> Trophies { get; set; } = [];

        public List<AchievementDefinition> Achievements { get; set; } = [];
    }
}
