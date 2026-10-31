using System.IO;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using Sanctuary.Core.Collections;
using Sanctuary.Game.Terrain;

namespace Sanctuary.Game.Resources;


public class TerrainCollection : ObservableConcurrentDictionary<string, TerrainMesh>
{
    private readonly ILogger _logger;

    public TerrainCollection(ILogger logger)
    {
        _logger = logger;
    }

    public bool Load(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
        {
            _logger.LogInformation(
                "Terrain directory \"{Path}\" not found - no ground height data will be available.", directoryPath);
            return true;
        }

        var filePaths = Directory.GetFiles(directoryPath, "*.terrain");
        var terrainMeshes = new TerrainMesh?[filePaths.Length];

        // The big zones take seconds each to read, so the files load side by side.
        Parallel.For(0, filePaths.Length, index =>
        {
            if (!TerrainLoader.TryLoad(filePaths[index], _logger, out terrainMeshes[index]))
            {
                _logger.LogWarning(
                    "Skipping terrain file \"{Path}\" due to a load failure (see error above). " +
                    "Zone \"{ZoneName}\" will have no ground height data available.",
                    filePaths[index], Path.GetFileNameWithoutExtension(filePaths[index]));
            }
        });

        for (var index = 0; index < filePaths.Length; index++)
        {
            if (terrainMeshes[index] is { } terrainMesh)
                this[Path.GetFileNameWithoutExtension(filePaths[index])] = terrainMesh;
        }

        _logger.LogInformation("Loaded {Count} terrain mesh(es) from \"{Path}\".", Count, directoryPath);
        return true;
    }
}
