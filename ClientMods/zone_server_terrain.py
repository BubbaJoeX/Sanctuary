#!/usr/bin/env python3
"""Writes src/Resources/Terrain/<zone>.terrain, the server's copy of a zone's ground, from its terrain chunks.

The server uses it to stand NPCs on the ground and, when the zone has no navmesh, to build one at startup
(Resources/NavMeshes/<zone>.navmesh), so mobs, pets and quest breadcrumbs can find their way.

Input is the zone's <zone>_<row>_<col>.gcnk(.z) chunks (version 5 or 6, read with world_chunks.py): the render mesh
of each tile, in world space. Output is "STRN", version 2, the tile size (float), then a zlib block: the tile
count; per tile its key (int x, int z = floor(world / tile size)), the vertex count, vertices as u16 x and z
across the tile and f32 y, the index count and u16 indices; then the placed colliders, none here (two zero
counts, unless --objects): with --objects, the mesh count, per mesh its vertices (f32 x, y, z) and u16 indices, then the
placement count, per placement its mesh, its right, up and forward axes and position (f32 x, y, z each) and scale.

--client reads the chunks from the client's packs and the server's asset mirror (--assets). --objects (which needs
--client, for the actors) adds the solid objects the chunks place and the zone's invisible
walls. A cave's walls and floors are objects over a flat placeholder ground, which is then left out.

Examples:
  python zone_server_terrain.py --zone lavendercoast --chunks D:/FreeRealms/Sanctuary/src/Resources/ClientMods
  python zone_server_terrain.py --zone sg_bandit_hideout --objects --client "%LOCALAPPDATA%/OSFRLauncher/Servers/OSFR Public Server/Client" --assets D:/Sanctuary/assets
"""

import math
import re
import struct
import sys
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

# Not solid: see-through effects, webs, cloth and light.
NOT_SOLID = ("spiderweb", "cobweb", "flag", "banner", "light", "pfx", "fx_", "beam", "glow", "fog", "dust", "smoke",
             "invisible", "waterfall", "water_", "cloud", "godray", "shaft", "sparkle")


def chunk_triangles(chunk):
    """World-space triangles of every tile in a chunk, as (a, b, c) vertex triples."""
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


class ClientActors:
    """The client's packs, through the zone editor's reader: chunks, zones and the meshes of the objects they place."""

    def __init__(self, client_dir, assets_dir):
        sys.path.insert(0, str(script_dir.parent / "ZoneEditor"))
        from zoneeditor import models
        from zoneeditor.assets import AssetSource
        from zoneeditor.formats import Zone

        self.models, self.zone_format = models, Zone
        self.assets = AssetSource(client_dir, script_dir.parent / "src" / "Resources" / "ClientMods", assets_dir)
        self.library = models.MaterialLibrary(client_dir)
        self.meshes = {}

    def chunks(self, zone):
        pattern = re.compile(re.escape(zone.lower()) + r"_-?\d+_-?\d+\.gcnk")
        for name in sorted(name for name in self.assets.names(".gcnk") if pattern.fullmatch(name.lower())):
            try:
                yield Chunk.read(self.assets.read(name))
            except KeyError:
                continue

    def walls(self, zone):
        """The zone's invisible walls as triangles; each wall is a run of (bottom, top) points."""
        try:
            walls = self.zone_format.read(self.assets.read(zone + ".gzne")).walls or []
        except KeyError:
            return []
        triangles = []
        for wall in walls:
            for i in range(0, len(wall) - 3, 2):
                bottom, top, next_bottom, next_top = wall[i], wall[i + 1], wall[i + 2], wall[i + 3]
                triangles += [(bottom, top, next_bottom), (next_bottom, top, next_top)]
        return triangles

    def mesh(self, actor):
        """An actor's solid mesh as (vertices, triangles in the client's order), or None."""
        key = actor.lower()
        if key not in self.meshes:
            self.meshes[key] = self._mesh(actor)
        return self.meshes[key]

    def _mesh(self, actor):
        if any(word in actor.lower() for word in NOT_SOLID):
            return None
        try:
            if actor.lower().endswith(".agr"):
                members = self.models.actor_group_members(self.assets.read(actor))
                return self.mesh(members[0]) if members else None
            mesh_name = self.models.adr_mesh_name(self.assets.read(actor))
            model = self.models.parse_dme(self.assets.read(mesh_name), self.library) if mesh_name else None
        except (KeyError, ValueError, IndexError, struct.error):
            return None
        if model is None:
            return None
        vertices, triangles = [], []
        for part in model["meshes"]:
            if part["alpha"] in ("water", "blend") or "water" in (part["shader"] or "").lower():
                continue
            base = len(vertices)
            vertices += [tuple(map(float, vertex)) for vertex in part["positions"]]
            # parse_dme turns faces round for the viewport; the terrain keeps the client's order, as the chunks do.
            triangles += [(base + a, base + c, base + b) for a, b, c in part["indices"].reshape(-1, 3).tolist()]
        return (vertices, triangles) if triangles else None


def rotation_axes(yaw, pitch, roll):
    """Right, up and forward of an object turned as the zone editor turns it (R = Ry * Rx * Rz), by columns."""
    cy, sy = math.cos(yaw), math.sin(yaw)
    cx, sx = math.cos(pitch), math.sin(pitch)
    cz, sz = math.cos(roll), math.sin(roll)
    right = (cy * cz + sy * sx * sz, cx * sz, -sy * cz + cy * sx * sz)
    up = (-cy * sz + sy * sx * cz, cx * cz, sy * sz + cy * sx * cz)
    forward = (sy * cx, -sx, cy * cx)
    return right, up, forward


def split_mesh(vertices, triangles):
    """Pieces of a mesh small enough for u16 indices."""
    piece_vertices, piece_triangles, remap = [], [], {}
    for triangle in triangles:
        if len(piece_vertices) + 3 > QUANTIZED_MAX + 1:
            yield piece_vertices, piece_triangles
            piece_vertices, piece_triangles, remap = [], [], {}
        corners = []
        for index in triangle:
            if index not in remap:
                remap[index] = len(piece_vertices)
                piece_vertices.append(vertices[index])
            corners.append(remap[index])
        piece_triangles.append(tuple(corners))
    if piece_triangles:
        yield piece_vertices, piece_triangles


def colliders(client, zone):
    """The zone's solid placed objects and its walls, as (meshes, placements)."""
    meshes, mesh_ids, placements = [], {}, []
    for chunk in client.chunks(zone):
        for tile in chunk.tiles:
            for placed in tile.objects:
                mesh = client.mesh(placed.file_name)
                if mesh is None:
                    continue
                key = placed.file_name.lower()
                if key not in mesh_ids:
                    mesh_ids[key] = []
                    for piece in split_mesh(*mesh):
                        mesh_ids[key].append(len(meshes))
                        meshes.append(piece)
                axes = rotation_axes(*placed.rotation[:3])
                for mesh_id in mesh_ids[key]:
                    placements.append((mesh_id, *axes, tuple(placed.position[:3]), float(placed.scale)))
    walls = client.walls(zone)
    if walls:
        corners = [corner for triangle in walls for corner in triangle]
        for piece in split_mesh(corners, [(i, i + 1, i + 2) for i in range(0, len(corners), 3)]):
            placements.append((len(meshes), (1.0, 0.0, 0.0), (0.0, 1.0, 0.0), (0.0, 0.0, 1.0), (0.0, 0.0, 0.0), 1.0))
            meshes.append(piece)
    return meshes, placements


def write_terrain(output, tiles, meshes=(), placements=()):
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

    body += struct.pack("<i", len(meshes))
    for vertices, triangles in meshes:
        body += struct.pack("<i", len(vertices))
        for vertex in vertices:
            body += struct.pack("<3f", *vertex)
        indices = [index for triangle in triangles for index in triangle]
        body += struct.pack("<i", len(indices))
        body += struct.pack(f"<{len(indices)}H", *indices)
    body += struct.pack("<i", len(placements))
    for mesh_id, right, up, forward, position, scale in placements:
        body += struct.pack("<i", mesh_id)
        body += struct.pack("<12f", *right, *up, *forward, *position)
        body += struct.pack("<f", scale)

    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_bytes(SIGNATURE + struct.pack("<If", VERSION, TILE_SIZE) + zlib.compress(bytes(body), 9))


def main():
    parser = ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--zone", required=True, help="Zone name, as in Resources/Zones (e.g. lavendercoast)")
    parser.add_argument("--chunks", type=Path, help="Folder holding the zone's .gcnk / .gcnk.z chunks")
    parser.add_argument("--client", type=Path, help="The client's folder, to read the chunks (and actors) from its packs")
    parser.add_argument("--objects", action="store_true", help="Add the chunks' solid objects and the zone's walls (needs --client)")
    parser.add_argument("--assets", type=Path, default=script_dir.parent / "assets",
                        help="The server's asset mirror, which holds the zones the client streams (default: the checkout's assets)")
    parser.add_argument("--output", type=Path, help="Defaults to src/Resources/Terrain/<zone>.terrain")
    args = parser.parse_args()

    if args.objects and not args.client:
        raise SystemExit("--objects needs --client")

    client = ClientActors(args.client, args.assets) if args.client else None

    if client:
        chunks = list(client.chunks(args.zone))
    elif args.chunks:
        pattern = re.compile(re.escape(args.zone) + r"_-?\d+_-?\d+\.gcnk(\.z)?")
        chunks = [Chunk.read(path.read_bytes()) for path in sorted(args.chunks.iterdir()) if pattern.fullmatch(path.name)]
    else:
        raise SystemExit("Give --chunks or --client")

    if not chunks:
        raise SystemExit(f"No {args.zone} chunks found")

    tiles = defaultdict(list)
    for chunk in chunks:
        for triangle in chunk_triangles(chunk):
            tiles[tile_key(triangle)].append(triangle)

    meshes, placements = colliders(client, args.zone) if args.objects else ([], [])

    # A cave's terrain is a flat placeholder under its floor.
    heights = [corner[1] for triangle_list in tiles.values() for triangle in triangle_list for corner in triangle]
    if placements and heights and max(heights) - min(heights) < 0.01:
        tiles = {}

    output = args.output or terrain_dir / f"{args.zone}.terrain"
    write_terrain(output, tiles, meshes, placements)

    triangles = sum(len(triangle_list) for triangle_list in tiles.values())
    print(f"Wrote {len(tiles)} tiles ({triangles} triangles) and {len(placements)} placed colliders ({len(meshes)} meshes) "
          f"from {len(chunks)} chunks to {output}")


if __name__ == "__main__":
    main()
