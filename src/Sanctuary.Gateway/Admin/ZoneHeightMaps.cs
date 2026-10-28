using System;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

using Sanctuary.Game;

namespace Sanctuary.Gateway.Admin;

/// <summary>
/// Top-down height grids of the zones that have terrain, for the admin panel's map. Built from the same terrain
/// meshes the server uses for ground height, once per zone, at up to <see cref="MaxSide"/> samples a side.
/// </summary>
public sealed class ZoneHeightMaps
{
    private const int MaxSide = 1024;
    private const float MinCellSize = 2f;

    /// <summary>
    /// Heights are stored in tenths of a metre; this marks a sample with no ground under it.
    /// </summary>
    public const short NoGround = short.MinValue;

    private readonly IResourceManager _resourceManager;
    private readonly ConcurrentDictionary<string, Lazy<byte[]?>> _maps = new();

    public ZoneHeightMaps(IResourceManager resourceManager)
    {
        _resourceManager = resourceManager;
    }

    public bool Has(string zoneName) => _resourceManager.Terrains.TryGetValue(zoneName, out var mesh) && mesh.Tiles.Count > 0;

    /// <summary>
    /// The zone's grid as a u32 header length, a JSON header (minX, minZ, cell, cols, rows) and then cols x rows
    /// little-endian int16 heights in tenths of a metre, row by row along +Z. Null when the zone has no terrain.
    /// </summary>
    public byte[]? Get(string zoneName) =>
        _maps.GetOrAdd(zoneName, name => new Lazy<byte[]?>(() => Build(name))).Value;

    private byte[]? Build(string zoneName)
    {
        if (!_resourceManager.Terrains.TryGetValue(zoneName, out var mesh) || mesh.Tiles.Count == 0)
            return null;

        var minTileX = mesh.Tiles.Keys.Min(x => x.X);
        var maxTileX = mesh.Tiles.Keys.Max(x => x.X);
        var minTileZ = mesh.Tiles.Keys.Min(x => x.Z);
        var maxTileZ = mesh.Tiles.Keys.Max(x => x.Z);

        var minX = minTileX * mesh.TileSize;
        var minZ = minTileZ * mesh.TileSize;
        var width = (maxTileX - minTileX + 1) * mesh.TileSize;
        var depth = (maxTileZ - minTileZ + 1) * mesh.TileSize;

        var cell = MathF.Max(MinCellSize, MathF.Ceiling(MathF.Max(width, depth) / MaxSide));
        var cols = (int)MathF.Ceiling(width / cell);
        var rows = (int)MathF.Ceiling(depth / cell);

        var header = JsonSerializer.SerializeToUtf8Bytes(new { minX, minZ, cell, cols, rows });
        var data = new byte[4 + header.Length + cols * rows * sizeof(short)];

        BinaryPrimitives.WriteUInt32LittleEndian(data, (uint)header.Length);
        header.CopyTo(data, 4);

        var offset = 4 + header.Length;

        Parallel.For(0, rows, row =>
        {
            var z = minZ + (row + 0.5f) * cell;

            for (var col = 0; col < cols; col++)
            {
                var x = minX + (col + 0.5f) * cell;

                // From high above, so the top surface wins where ground overhangs (cave roofs, arches).
                var value = mesh.TryGetGroundHeight(x, z, 10_000f, 20_000f, out var height)
                    ? (short)Math.Clamp(MathF.Round(height * 10f), short.MinValue + 1, short.MaxValue)
                    : NoGround;

                BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(offset + (row * cols + col) * sizeof(short)), value);
            }
        });

        return data;
    }
}
