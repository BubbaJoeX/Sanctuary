using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text.Json;

using Microsoft.Extensions.Logging;

using Sanctuary.Core.IO;
using Sanctuary.Game.Resources.Definitions;
using Sanctuary.Packet;

namespace Sanctuary.Game.Resources;

/// <summary>
/// VehicleGames.json and VehicleConfigs.json (the live game's karts and derby cars, by name). A reload swaps them in
/// at once, and files that fail to load or check keep the old ones.
/// </summary>
public sealed class VehicleGameCollection
{
    /// <summary>
    /// A game's Games window tile id, clear of the minigames', fishing's and the battles' ids.
    /// </summary>
    public const int ActivityIdBase = 9500;

    private readonly ILogger _logger;

    private Tables _tables = new(new VehicleGamesDefinition(), new Dictionary<string, VehicleConfig>());

    public VehicleGameCollection(ILogger logger)
    {
        _logger = logger;
    }

    public VehicleGamesDefinition Definition => _tables.Definition;

    public IReadOnlyList<VehicleGameStageDefinition> Stages => _tables.Definition.Stages;

    public IReadOnlyList<VehicleGameQueueDefinition> Queues => _tables.Definition.Queues;

    public bool TryGetStage(int stageId, [MaybeNullWhen(false)] out VehicleGameStageDefinition stage) =>
        _tables.Stages.TryGetValue(stageId, out stage);

    public bool TryGetByActivity(int activityId, [MaybeNullWhen(false)] out VehicleGameStageDefinition stage) =>
        TryGetStage(activityId - ActivityIdBase, out stage) && stage.Image is not null;

    /// <summary>
    /// The games an NPC (an Npcs.json id) offers when clicked.
    /// </summary>
    public IEnumerable<VehicleGameStageDefinition> OfferedBy(int npcDefinitionId) =>
        Stages.Where(stage => stage.StarterNpcIds.Contains(npcDefinitionId));

    public bool TryGetTrack(string zoneName, [MaybeNullWhen(false)] out RaceTrackDefinition track) =>
        _tables.Tracks.TryGetValue(zoneName, out track);

    public bool TryGetVehicle(string name, [MaybeNullWhen(false)] out VehicleConfig config) =>
        _tables.Vehicles.TryGetValue(name, out config);

    public bool Load(string gamesFilePath, string vehiclesFilePath)
    {
        VehicleGamesDefinition? definition;
        Dictionary<string, string>? vehicleHex;

        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip
            };

            using (var fileStream = new FileStream(gamesFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                definition = JsonSerializer.Deserialize<VehicleGamesDefinition>(fileStream, options);

            using (var fileStream = new FileStream(vehiclesFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                vehicleHex = JsonSerializer.Deserialize<Dictionary<string, string>>(fileStream, options);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load \"{file}\" or \"{vehicles}\".", gamesFilePath, vehiclesFilePath);
            return false;
        }

        if (definition is null || vehicleHex is null)
        {
            _logger.LogError("No entries found in \"{file}\" or \"{vehicles}\".", gamesFilePath, vehiclesFilePath);
            return false;
        }

        var vehicles = new Dictionary<string, VehicleConfig>(StringComparer.OrdinalIgnoreCase);

        foreach (var (name, hex) in vehicleHex)
        {
            // Each must read back whole, as the client reads it, or the client drops the packet carrying it.
            var data = Convert.FromHexString(hex);
            var reader = new PacketReader(data);

            if (!VehicleConfig.TryRead(ref reader, out var config) || reader.RemainingLength != 0)
            {
                _logger.LogError("Vehicle \"{name}\" doesn't read as a whole VehicleConfig. \"{file}\"", name, vehiclesFilePath);
                return false;
            }

            vehicles[name] = config;
        }

        var tables = new Tables(definition, vehicles);

        if (!IsValid(tables, out var error))
        {
            _logger.LogError("Invalid vehicle games: {error} \"{file}\"", error, gamesFilePath);
            return false;
        }

        _tables = tables;

        return true;
    }

    private static bool IsValid(Tables tables, out string error)
    {
        var definition = tables.Definition;

        foreach (var stage in definition.Stages)
        {
            if (stage.Entry.Length != 3)
            {
                error = $"stage {stage.Id} needs an entry of three numbers";
                return false;
            }

            if (stage.Kind == VehicleGameKind.Race && (stage.Track is null || !tables.Tracks.ContainsKey(stage.Track)))
            {
                error = $"stage {stage.Id} has no track";
                return false;
            }
        }

        if (tables.Stages.Count != definition.Stages.Count)
        {
            error = "a stage is listed twice";
            return false;
        }

        foreach (var queue in definition.Queues)
        {
            if (queue.Params.Length != 7 || queue.MaxPlayers < 1 || queue.MinPlayers < 1)
            {
                error = $"queue {queue.Id} needs seven params and at least one player";
                return false;
            }

            if (queue.StageIds.Count == 0
                || queue.StageIds.Any(stageId => !tables.Stages.TryGetValue(stageId, out var stage) || stage.Kind == VehicleGameKind.Garage))
            {
                error = $"queue {queue.Id} needs games to start";
                return false;
            }
        }

        if (definition.Queues.Select(queue => queue.Id).Distinct().Count() != definition.Queues.Count)
        {
            error = "a queue is listed twice";
            return false;
        }

        IEnumerable<string> vehicleNames =
        [
            definition.Derby.PlayerVehicle,
            .. definition.Derby.Drivers.Select(driver => driver.Vehicle),
            .. definition.Tracks.Select(track => track.PlayerVehicle),
            .. definition.Tracks.SelectMany(track => track.Drivers).Select(driver => driver.Vehicle)
        ];

        foreach (var name in vehicleNames)
        {
            if (!tables.Vehicles.ContainsKey(name))
            {
                error = $"no vehicle \"{name}\" in VehicleConfigs.json";
                return false;
            }
        }

        foreach (var track in definition.Tracks)
        {
            if (track.Gates.Count < 2 || track.Gates.Any(gate => gate.Length != 3))
            {
                error = $"track {track.Zone} needs its gates, three numbers each";
                return false;
            }

            if (track.FinishCamera.Length != 16)
            {
                error = $"track {track.Zone}'s finish camera needs 16 numbers";
                return false;
            }

            foreach (var driver in track.Drivers)
            {
                if (driver.Line.Count < 4 || driver.Line.Any(point => point.Length != 4))
                {
                    error = $"track {track.Zone}'s driver {driver.NpcId} needs a line of (seconds, x, y, z) points";
                    return false;
                }

                if (track.Grid.All(slot => slot.Slot != driver.Slot) || driver.Slot == track.PlayerSlot)
                {
                    error = $"track {track.Zone}'s driver {driver.NpcId} has no grid slot of their own";
                    return false;
                }
            }
        }

        error = string.Empty;
        return true;
    }

    private sealed class Tables(VehicleGamesDefinition definition, Dictionary<string, VehicleConfig> vehicles)
    {
        public VehicleGamesDefinition Definition { get; } = definition;

        public Dictionary<string, VehicleConfig> Vehicles { get; } = vehicles;

        // A stage listed twice keeps its first entry; the check above refuses the file.
        public Dictionary<int, VehicleGameStageDefinition> Stages { get; } =
            definition.Stages.GroupBy(stage => stage.Id).ToDictionary(group => group.Key, group => group.First());

        public Dictionary<string, RaceTrackDefinition> Tracks { get; } =
            definition.Tracks.ToDictionary(track => track.Zone, StringComparer.OrdinalIgnoreCase);
    }
}
