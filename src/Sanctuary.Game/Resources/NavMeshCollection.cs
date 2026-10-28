using System;
using System.Diagnostics;
using System.IO;

using Microsoft.Extensions.Logging;

using DotRecast.Detour;

using Sanctuary.Core.Collections;
using Sanctuary.Game.Pathfinding;

namespace Sanctuary.Game.Resources;


public class NavMeshCollection : ObservableConcurrentDictionary<string, DtNavMesh>
{
    private readonly ILogger _logger;

    public NavMeshCollection(ILogger logger)
    {
        _logger = logger;
    }

    public bool Load(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
        {
            _logger.LogInformation(
                "Navmesh directory \"{Path}\" not found - no navmesh pathfinding will be available.", directoryPath);
            return true;
        }

        foreach (var filePath in Directory.GetFiles(directoryPath, "*.navmesh"))
        {
            var zoneName = Path.GetFileNameWithoutExtension(filePath);

            if (!NavMeshLoader.TryLoad(filePath, _logger, out var navMesh))
            {
                _logger.LogWarning(
                    "Skipping navmesh file \"{Path}\" due to a load failure (see error above). " +
                    "Zone \"{ZoneName}\" will have no navmesh pathfinding available.",
                    filePath, zoneName);
                continue;
            }

            this[zoneName] = navMesh!;
        }

        _logger.LogInformation("Loaded {Count} navmesh(es) from \"{Path}\".", Count, directoryPath);
        return true;
    }

    /// <summary>
    /// Builds a navmesh for every zone with terrain but no navmesh, and saves it with the others so later starts
    /// just load it. Delete a zone's .navmesh to rebuild it after its terrain changes.
    /// </summary>
    public void BuildMissing(TerrainCollection terrains, string directoryPath)
    {
        foreach (var (zoneName, terrain) in terrains)
        {
            if (ContainsKey(zoneName))
                continue;

            var stopwatch = Stopwatch.StartNew();

            DtNavMesh? navMesh;

            try
            {
                navMesh = NavMeshBuilder.Build(terrain);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to build a navmesh for zone \"{ZoneName}\" from its terrain.", zoneName);
                continue;
            }

            if (navMesh is null)
            {
                _logger.LogWarning("Zone \"{ZoneName}\"'s terrain has nothing walkable; it gets no navmesh.", zoneName);
                continue;
            }

            this[zoneName] = navMesh;

            _logger.LogInformation("Built a navmesh for zone \"{ZoneName}\" from its terrain in {Seconds:F1}s.",
                zoneName, stopwatch.Elapsed.TotalSeconds);

            try
            {
                Directory.CreateDirectory(directoryPath);
                NavMeshBuilder.Save(Path.Combine(directoryPath, zoneName + ".navmesh"), navMesh);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                _logger.LogWarning(exception, "Couldn't save zone \"{ZoneName}\"'s navmesh; it will be built again next start.", zoneName);
            }
        }
    }
}
