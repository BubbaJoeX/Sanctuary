using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.Extensions.Logging;

using Sanctuary.Game.Fishing;
using Sanctuary.Game.Resources.Definitions;

namespace Sanctuary.Game.Resources;

/// <summary>
/// Fishing.json. A reload swaps the whole file in at once, and a file that fails to load or check keeps the old one.
/// </summary>
public sealed class FishingCollection
{
    private readonly ILogger _logger;

    private Tables _tables = new(new FishingDefinition());

    public FishingCollection(ILogger logger)
    {
        _logger = logger;
    }

    public FishingDefinition Definition => _tables.Definition;

    public IReadOnlyDictionary<int, FishDefinition> Fish => _tables.Fish;

    public IReadOnlyList<FishingPondDefinition> Ponds => _tables.Definition.Ponds;

    public bool TryGetPond(string zoneName, [MaybeNullWhen(false)] out FishingPondDefinition pond) =>
        _tables.PondsByZone.TryGetValue(zoneName, out pond);

    /// <summary>
    /// The pond a Fabled Realms fishing hole (an Npcs.json id) leads to.
    /// </summary>
    public bool TryGetPondByHole(int npcDefinitionId, [MaybeNullWhen(false)] out FishingPondDefinition pond) =>
        _tables.PondsByHole.TryGetValue(npcDefinitionId, out pond);

    /// <summary>
    /// The rod whose item names this bobber, or null for anything else in the weapon slot.
    /// </summary>
    public FishingRodDefinition? GetRod(int bobberModelId) => _tables.Rods.GetValueOrDefault(bobberModelId);

    public bool Load(string filePath)
    {
        if (!File.Exists(filePath))
        {
            _logger.LogError("Failed to find file \"{file}\"", filePath);
            return false;
        }

        FishingDefinition? definition;

        try
        {
            using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);

            definition = JsonSerializer.Deserialize<FishingDefinition>(fileStream, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                Converters = { new JsonStringEnumConverter() }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse file \"{file}\"", filePath);
            return false;
        }

        var error = definition is null ? "the file is empty" : Validate(definition);

        if (error is not null)
        {
            _logger.LogError("Invalid fishing data in \"{file}\": {error}", filePath, error);
            return false;
        }

        _tables = new Tables(definition!);

        return true;
    }

    /// <summary>
    /// Null when the file is usable, else what's wrong with it.
    /// </summary>
    public static string? Validate(FishingDefinition definition)
    {
        var fish = new HashSet<int>();

        foreach (var entry in definition.Fish)
        {
            if (!fish.Add(entry.Id))
                return $"fish {entry.Id} is listed twice";

            if (entry.Rarity is < 1 or > 4 || entry.Items.Count == 0 || entry.ModelId <= 0 || entry.NameId <= 0)
                return $"fish {entry.Id} ({entry.Comment}) needs a name, a model, a rarity of 1 to 4 and at least one size";
        }

        if (definition.Rods.Count == 0 || definition.Rods.GroupBy(rod => rod.BobberModelId).Any(group => group.Count() > 1))
            return "every rod needs its own bobber model";

        if (definition.Rods.Any(rod => rod.MaxCastDistance <= FishingRules.MinCastDistance || rod.ItemId <= 0))
            return $"every rod needs an item and a cast longer than {FishingRules.MinCastDistance} m";

        if (definition.Treasure.Any(treasure => treasure.Weight <= 0 || string.IsNullOrEmpty(treasure.RewardTable)))
            return "every treasure needs a reward table and a weight";

        var zones = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var holes = new HashSet<int>();

        foreach (var pond in definition.Ponds)
        {
            if (!zones.Add(pond.Zone) || !holes.Add(pond.HoleNpcId))
                return $"pond {pond.Zone} shares its zone or fishing hole with another";

            if (new[] { pond.Arrival, pond.Guide, pond.Return }.Any(spot => spot.Position.Length != 3))
                return $"pond {pond.Zone} needs x, y and z for its arrival, guide and return";

            if (pond.Fish.Count == 0 || pond.Fish.Any(id => !fish.Contains(id)))
                return $"pond {pond.Zone} lists no fish or an unknown one";

            if (pond.Schools.GroupBy(school => school.Id).Any(group => group.Count() > 1))
                return $"pond {pond.Zone} has two schools with the same id";

            foreach (var school in pond.Schools)
            {
                if (school.Position.Length != 3 || school.ModelId <= 0)
                    return $"pond {pond.Zone} school {school.Id} needs x, y and z and a model";

                if (school.Fish.Any(id => !pond.Fish.Contains(id)))
                    return $"pond {pond.Zone} school {school.Id} has a fish the pond doesn't";
            }
        }

        return null;
    }

    private sealed class Tables(FishingDefinition definition)
    {
        public FishingDefinition Definition { get; } = definition;

        public Dictionary<int, FishDefinition> Fish { get; } = definition.Fish.ToDictionary(fish => fish.Id);

        public Dictionary<string, FishingPondDefinition> PondsByZone { get; } =
            definition.Ponds.ToDictionary(pond => pond.Zone, StringComparer.OrdinalIgnoreCase);

        public Dictionary<int, FishingPondDefinition> PondsByHole { get; } = definition.Ponds.ToDictionary(pond => pond.HoleNpcId);

        public Dictionary<int, FishingRodDefinition> Rods { get; } = definition.Rods.ToDictionary(rod => rod.BobberModelId);
    }
}
