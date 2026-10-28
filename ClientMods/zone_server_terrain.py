#!/usr/bin/env python3
"""Writes src/Resources/Terrain/<zone>.terrain, the server's copy of a zone's ground, from its terrain chunks.

The server uses it to stand NPCs on the ground and, when the zone has no navmesh, to build one at startup
(Resources/NavMeshes/<zone>.navmesh), so mobs, pets and quest breadcrumbs can find their way.

Input is the zone's <zone>_<row>_<col>.gcnk(.z) chunks (version 5 or 6, read with world_chunks.py): the render mesh
of each tile, in world space. Output is "STRN", version 2, the tile size (float), then a zlib block: the tile
count; per tile its key (int x, int z = floor(world / tile size)), the vertex count, vertices as u16 x and z
across the tile and f32 y, the index count and u16 indices; then the placed colliders, none here (two zero
counts). Only the ground is kept, not the tiles' objects.

Example:
  python zone_server_terrain.py --zone lavendercoast --chunks D:/Sanctuary/src/Resources/ClientMods
"""

import re
import struct
import zlib
from argparse import ArgumentParser
from collections import defaultdict
from pathlib import Path

from world_chunks import Chunk

script_dir = Path(__file__).resolve().parent
terrain_dir = script_dir.parent / "src" / "Resources" / "Terrain"

SIGNATURE = b"STRN"
VERSION = 2
TILE_SIZE = 64.0
QUANTIZED_MAX = 65535


def chunk_triangles(path):
    """World-space triangles of every tile in a chunk, as (a, b, c) vertex triples."""
    chunk = Chunk.read(path.read_bytes())
    positions = [tuple(map(float, position)) for position in chunk.vertices["position"]]
    indices = chunk.indices.tolist()

    for index_offset, batch_indices, vertex_offset, _ in chunk.batches.tolist():
        for first in range(index_offset, index_offset + batch_indices - 2, 3):
            yield tuple(positions[vertex_offset + indices[first + corner]] for corner in range(3))


def tile_key(triangle):
    # A triangle belongs to the tile its centre is in.
    x = sum(corner[0] for corner in triangle) / 3
    z = sum(corner[2] for corner in triangle) / 3
    return int(x // TILE_SIZE), int(z // TILE_SIZE)


def quantize(value, origin):
    return max(0, min(QUANTIZED_MAX, round((value - origin) / TILE_SIZE * QUANTIZED_MAX)))


def write_terrain(output, tiles):
    body = bytearray(struct.pack("<i", len(tiles)))

    for (tile_x, tile_z), triangles in sorted(tiles.items()):
        origin_x, origin_z = tile_x * TILE_SIZE, tile_z * TILE_SIZE
        vertex_ids, vertices, indices = {}, [], []

        for triangle in triangles:
            for x, y, z in triangle:
                key = (quantize(x, origin_x), y, quantize(z, origin_z))
                if key not in vertex_ids:
                    vertex_ids[key] = len(vertices)
                    vertices.append(key)
                indices.append(vertex_ids[key])

        if len(vertices) > QUANTIZED_MAX + 1:
            raise SystemExit(f"Tile {tile_x},{tile_z} has {len(vertices)} vertices; a tile holds at most {QUANTIZED_MAX + 1}")

        body += struct.pack("<iii", tile_x, tile_z, len(vertices))
        for qx, y, qz in vertices:
            body += struct.pack("<HfH", qx, y, qz)
        body += struct.pack("<i", len(indices))
        body += struct.pack(f"<{len(indices)}H", *indices)

    # No placed colliders: no meshes, no placements.
    body += struct.pack("<ii", 0, 0)

    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_bytes(SIGNATURE + struct.pack("<If", VERSION, TILE_SIZE) + zlib.compress(bytes(body), 9))


def main():
    parser = ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--zone", required=True, help="Zone name, as in Resources/Zones (e.g. lavendercoast)")
    parser.add_argument("--chunks", type=Path, required=True, help="Folder holding the zone's .gcnk / .gcnk.z chunks")
    parser.add_argument("--output", type=Path, help="Defaults to src/Resources/Terrain/<zone>.terrain")
    args = parser.parse_args()

    pattern = re.compile(re.escape(args.zone) + r"_-?\d+_-?\d+\.gcnk(\.z)?")
    chunks = sorted(path for path in args.chunks.iterdir() if pattern.fullmatch(path.name))
    if not chunks:
        raise SystemExit(f"No {args.zone} chunks in {args.chunks}")

    tiles = defaultdict(list)
    for path in chunks:
        for triangle in chunk_triangles(path):
            tiles[tile_key(triangle)].append(triangle)

    output = args.output or terrain_dir / f"{args.zone}.terrain"
    write_terrain(output, tiles)

    triangles = sum(len(triangle_list) for triangle_list in tiles.values())
    print(f"Wrote {len(tiles)} tiles ({triangles} triangles) from {len(chunks)} chunks to {output}")


if __name__ == "__main__":
    main()
