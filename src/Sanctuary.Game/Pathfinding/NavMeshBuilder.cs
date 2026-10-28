using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using DotRecast.Core;
using DotRecast.Detour;
using DotRecast.Detour.Io;
using DotRecast.Recast;
using DotRecast.Recast.Geom;

using Sanctuary.Game.Terrain;

namespace Sanctuary.Game.Pathfinding;

/// <summary>
/// Builds a tiled navmesh from a zone's ground (its .terrain) for zones that don't ship one, sized for a
/// player: 2 m tall, 0.6 m wide, stepping up 0.9 m and walking slopes up to 45 degrees.
/// </summary>
public static class NavMeshBuilder
{
    private const float CellSize = 0.6f;
    private const float CellHeight = 0.3f;
    private const float AgentHeight = 2f;
    private const float AgentRadius = 0.6f;
    private const float AgentMaxClimb = 0.9f;
    private const float AgentMaxSlope = 45f;

    private const int TileCells = 64;
    private const int VertsPerPoly = 6;

    private const int WalkableArea = 1;
    private const int WalkableFlag = 1;

    // The same header NavMeshLoader reads.
    private const string Signature = "SNAV";
    private const uint Version = 1;

    /// <summary>
    /// A navmesh over a zone's ground. The client winds floors clockwise seen from above, the other way round
    /// from Recast, so every triangle is flipped first.
    /// </summary>
    public static DtNavMesh? Build(TerrainMesh terrain, CancellationToken cancellationToken = default)
    {
        var (vertices, faces) = terrain.GetTriangles();

        for (var first = 0; first + 2 < faces.Length; first += 3)
            (faces[first + 1], faces[first + 2]) = (faces[first + 2], faces[first + 1]);

        return Build(vertices, faces, cancellationToken);
    }

    /// <summary>
    /// A navmesh over <paramref name="vertices"/> (x, y, z each) and <paramref name="faces"/> (three vertex numbers
    /// each, counter-clockwise seen from above for a floor), or null when nothing on them is walkable.
    /// </summary>
    public static DtNavMesh? Build(float[] vertices, int[] faces, CancellationToken cancellationToken = default)
    {
        if (faces.Length < 3)
            return null;

        var geometry = new RcSampleInputGeomProvider(vertices, faces);

        var config = new RcConfig(
            useTiles: true, tileSizeX: TileCells, tileSizeZ: TileCells, borderSize: RcConfig.CalcBorder(AgentRadius, CellSize),
            partition: RcPartition.WATERSHED, cellSize: CellSize, cellHeight: CellHeight,
            agentMaxSlope: AgentMaxSlope, agentHeight: AgentHeight, agentRadius: AgentRadius, agentMaxClimb: AgentMaxClimb,
            minRegionArea: 8, mergeRegionArea: 20, edgeMaxLen: 12f, edgeMaxError: 1.3f, vertsPerPoly: VertsPerPoly,
            detailSampleDist: 6f, detailSampleMaxError: 1f,
            filterLowHangingObstacles: true, filterLedgeSpans: true, filterWalkableLowHeightSpans: true,
            walkableAreaMod: new RcAreaModification(WalkableArea), buildMeshDetail: true);

        var results = new RcBuilder().BuildTiles(geometry, config, keepInterResults: false, buildAll: true,
            Environment.ProcessorCount, Task.Factory, cancellationToken);

        var tiles = results
            .Where(result => result.Mesh is { npolys: > 0 })
            .Select(result => CreateTile(result))
            .Where(tile => tile is not null)
            .ToList();

        if (tiles.Count == 0)
            return null;

        var navMeshParams = new DtNavMeshParams
        {
            orig = geometry.GetMeshBoundsMin(),
            tileWidth = TileCells * CellSize,
            tileHeight = TileCells * CellSize,
            maxTiles = (int)BitOperations.RoundUpToPowerOf2((uint)results.Count),
            maxPolys = (int)BitOperations.RoundUpToPowerOf2((uint)tiles.Max(tile => tile!.header.polyCount))
        };

        var navMesh = new DtNavMesh();

        if (navMesh.Init(ref navMeshParams, VertsPerPoly).Failed())
            return null;

        foreach (var tile in tiles)
            navMesh.AddTile(tile!, 0, 0, out _);

        return navMesh;
    }

    /// <summary>
    /// Writes <paramref name="navMesh"/> where NavMeshLoader can read it back: "SNAV", version 1, then the zlib'd
    /// Detour mesh set.
    /// </summary>
    public static void Save(string filePath, DtNavMesh navMesh)
    {
        var temporaryPath = filePath + ".tmp";

        using (var stream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write))
        {
            using var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true);

            writer.Write(Encoding.ASCII.GetBytes(Signature));
            writer.Write(Version);
            writer.Flush();

            using var zLibStream = new ZLibStream(stream, CompressionLevel.Optimal, leaveOpen: true);
            using var meshWriter = new BinaryWriter(zLibStream);

            new DtMeshSetWriter().Write(meshWriter, navMesh, RcByteOrder.LITTLE_ENDIAN, false);
        }

        File.Move(temporaryPath, filePath, overwrite: true);
    }

    private static DtMeshData? CreateTile(RcBuilderResult result)
    {
        var mesh = result.Mesh;
        var detail = result.MeshDetail;

        // Detour's default filter only walks polygons with a flag set.
        for (var poly = 0; poly < mesh.npolys; poly++)
            mesh.flags[poly] = mesh.areas[poly] == WalkableArea ? WalkableFlag : 0;

        var createParams = new DtNavMeshCreateParams
        {
            verts = mesh.verts,
            vertCount = mesh.nverts,
            polys = mesh.polys,
            polyAreas = mesh.areas,
            polyFlags = mesh.flags,
            polyCount = mesh.npolys,
            nvp = mesh.nvp,
            detailMeshes = detail?.meshes,
            detailVerts = detail?.verts,
            detailVertsCount = detail?.nverts ?? 0,
            detailTris = detail?.tris,
            detailTriCount = detail?.ntris ?? 0,
            walkableHeight = AgentHeight,
            walkableRadius = AgentRadius,
            walkableClimb = AgentMaxClimb,
            bmin = mesh.bmin,
            bmax = mesh.bmax,
            cs = CellSize,
            ch = CellHeight,
            buildBvTree = true,
            tileX = result.TileX,
            tileZ = result.TileZ
        };

        return DtNavMeshBuilder.CreateNavMeshData(createParams);
    }
}
