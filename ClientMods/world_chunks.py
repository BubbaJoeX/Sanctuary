#!/usr/bin/env python3
"""Reads and writes Fabled Realms world chunks (<zone>_<row>_<col>.gcnk, version 5 and 6) exactly.

A chunk covers 4x4 tiles of 64 units: row is world z / 64 and col is world x / 64 of its first tile.
It is "GCNK", version, uncompressed and compressed sizes, a zlib payload, then the collision block
(uncompressed and compressed sizes and zlib data, see zone_terrain.py).

The payload is the tiles, then the render mesh:

    int32 tile_count
    tile x { int32 row, col, id, environment (ObjectTerrainData id: 3 Wilds, 6 Seaside, ...); float -1000; int32 1
             int32 has_bounds, [int32 x4]; float -1000
             int32 eco_count, eco[]                 indices into the zone .gzne's ecosystem list
             int32 object_count, object[]           placed actors (Object below)
             int32 light_count, light[]; area_count, area[]; group_count, group[]
             int32 index (tile number in the chunk), int32 0 }
    int32 batch_count, batch x { int32 index_offset, index_count, vertex_offset, vertex_count }   one per tile
    int32 layers (0, 1, 2 or 4), [int32 size (512), size * size * layers / 2 bytes]   ecosystem weights (below)
    int32 index_count, uint16 indices[]             batch-local
    int32 vertex_count, 32-byte vertices            f32 x, y, z; normal bytes (z, y, x, 0);
                                                    8 bytes of ecosystem weights; u16 u, v; u16 detail u, v

The weights image has 512x512 pixels at 0.5 units, rows along +x and columns along +z, each holding
`layers` 4-bit weights of the tile's ecos[1:] over ecos[0]. With 4 layers a pixel is two bytes and ecos[1..4] use
nibbles 2, 3, 1, 0 (low nibble of the first byte is 0); with 2 layers one byte, ecos[1], ecos[2] in nibbles 0, 1;
with 1 layer two pixels share a byte. A tile with one ecosystem needs no weights, and a chunk without the
image has none.

``python world_chunks.py --verify <folder>`` checks that every chunk in a folder (or a client's packs with
--client) re-encodes byte for byte.
"""

from __future__ import annotations

import struct
import zlib
from dataclasses import dataclass, field
from pathlib import Path

import numpy as np

from fork_zone import COMPRESSED_MAGIC

VERTEX = np.dtype([("position", "<f4", 3), ("normal", "u1", 4), ("weights", "u1", 8), ("uv", "<u2", 2), ("detail_uv", "<u2", 2)])
assert VERTEX.itemsize == 32


class Reader:
    def __init__(self, data: bytes):
        self.data = data
        self.offset = 0

    def take(self, count: int) -> bytes:
        if count < 0 or self.offset + count > len(self.data):
            raise ValueError(f"read {count} past the end at {self.offset}/{len(self.data)}")
        chunk = self.data[self.offset : self.offset + count]
        self.offset += count
        return chunk

    def unpack(self, fmt: str):
        values = struct.unpack_from("<" + fmt, self.take(struct.calcsize("<" + fmt)))
        return values[0] if len(values) == 1 else values

    def cstring(self) -> str:
        end = self.data.index(b"\0", self.offset)
        text = self.data[self.offset : end].decode("latin-1")
        self.offset = end + 1
        return text


def cstring(text: str) -> bytes:
    return text.encode("latin-1") + b"\0"


@dataclass
class Object:
    """An actor placed in a tile. Rotation is (yaw, pitch, roll, 0) in radians, yaw about +y."""

    file_name: str
    position: tuple = (0.0, 0.0, 0.0, 1.0)
    rotation: tuple = (0.0, 0.0, 0.0, 0.0)
    scale: float = 1.0
    flags: int = 0
    alias: str = ""
    material: str = ""
    tint_alias: str = ""
    tint: tuple | None = None
    unknown: int = 255
    object_id: int = 0
    draw_distance: bytes = b"\0\0\0\0"       # a float when set (150, 1000, ...)
    extra: list = field(default_factory=list)

    @classmethod
    def read(cls, r: Reader, version: int) -> "Object":
        o = cls(flags=r.unpack("i"), file_name=r.cstring(), alias=r.cstring())
        o.position, o.rotation, o.scale = r.unpack("4f"), r.unpack("4f"), r.unpack("f")
        if version >= 6:
            o.material, o.tint_alias = r.cstring(), r.cstring()
            if o.tint_alias:
                o.tint = r.unpack("4f")
        o.unknown, o.object_id = r.unpack("ii")
        o.draw_distance = r.take(4)
        o.extra = [r.take(4 * r.unpack("i")) for _ in range(r.unpack("i"))]
        return o

    def write(self, version: int) -> bytes:
        out = struct.pack("<i", self.flags) + cstring(self.file_name) + cstring(self.alias)
        out += struct.pack("<4f4ff", *self.position, *self.rotation, self.scale)
        if version >= 6:
            out += cstring(self.material) + cstring(self.tint_alias)
            if self.tint_alias:
                out += struct.pack("<4f", *self.tint)
        out += struct.pack("<ii", self.unknown, self.object_id) + self.draw_distance
        out += struct.pack("<i", len(self.extra))
        for block in self.extra:
            out += struct.pack("<i", len(block) // 4) + block
        return out


def _skip_light(r: Reader):
    r.cstring(), r.cstring(), r.take(1 + 16 + 4 + 4 + 4)


def _skip_area(r: Reader):
    r.cstring(), r.unpack("i"), r.cstring(), r.take(16 + 16 + 4 + 12)


def _skip_group(r: Reader):
    r.cstring(), r.take(16 + 16 + 4)


@dataclass
class Tile:
    row: int
    col: int
    id: int = 0
    environment: int = 3
    bounds: tuple | None = None
    ecos: list = field(default_factory=list)
    objects: list = field(default_factory=list)
    lights: bytes = b""          # count-prefixed records, kept as they are
    areas: bytes = b""
    groups: bytes = b""
    index: int = 0

    @classmethod
    def read(cls, r: Reader, version: int) -> "Tile":
        row, col, tile_id, environment = r.unpack("4i")
        if r.unpack("f") != -1000 or r.unpack("i") != 1:
            raise ValueError("unexpected tile header")
        has_bounds = r.unpack("i")
        bounds = r.unpack("4i") if has_bounds > 0 else None
        if r.unpack("f") != -1000:
            raise ValueError("unexpected tile header")
        tile = cls(row, col, tile_id, environment, bounds)
        tile.ecos = [r.unpack("i") for _ in range(r.unpack("i"))]
        tile.objects = [Object.read(r, version) for _ in range(r.unpack("i"))]
        for name, skip in (("lights", _skip_light), ("areas", _skip_area), ("groups", _skip_group)):
            start = r.offset
            for _ in range(r.unpack("i")):
                skip(r)
            setattr(tile, name, r.data[start : r.offset])
        tile.index, zero = r.unpack("ii")
        if zero != 0:
            raise ValueError("unexpected tile trailer")
        return tile

    def write(self, version: int) -> bytes:
        out = struct.pack("<4ifi", self.row, self.col, self.id, self.environment, -1000.0, 1)
        out += struct.pack("<i4i", 1, *self.bounds) if self.bounds else struct.pack("<i", 0)
        out += struct.pack("<fi", -1000.0, len(self.ecos)) + struct.pack(f"<{len(self.ecos)}i", *self.ecos)
        out += struct.pack("<i", len(self.objects)) + b"".join(o.write(version) for o in self.objects)
        for raw in (self.lights, self.areas, self.groups):
            out += raw or struct.pack("<i", 0)
        return out + struct.pack("<ii", self.index, 0)


@dataclass
class Chunk:
    version: int
    tiles: list
    batches: np.ndarray                  # (tiles, 4) int32
    indices: np.ndarray                  # uint16, batch-local
    vertices: np.ndarray                 # VERTEX records
    collision: list                      # per-tile serialized btQuantizedBvh records (without the 16-byte header)
    image: tuple | None = None           # (layers, size, bytes): ecosystem weights, see the module docstring

    @classmethod
    def read(cls, blob: bytes) -> "Chunk":
        if struct.unpack_from(">I", blob)[0] == COMPRESSED_MAGIC:
            blob = zlib.decompress(blob[8:])
        magic, version, size, length = struct.unpack_from("<4sIII", blob)
        if magic != b"GCNK":
            raise ValueError("not a GCNK chunk")
        r = Reader(zlib.decompress(blob[16 : 16 + length]))
        if len(r.data) != size:
            raise ValueError("payload size mismatch")

        tiles = [Tile.read(r, version) for _ in range(r.unpack("i"))]
        batches = np.frombuffer(r.take(16 * r.unpack("i")), "<i4").reshape(-1, 4).copy()
        image = None
        bits = r.unpack("i")
        if bits > 0:
            image_size = r.unpack("i")
            image = (bits, image_size, r.take(image_size * image_size * bits // 2))
        indices = np.frombuffer(r.take(2 * r.unpack("i")), "<u2").copy()
        vertices = np.frombuffer(r.take(32 * r.unpack("i")), VERTEX).copy()
        if r.offset != len(r.data):
            raise ValueError(f"{len(r.data) - r.offset} bytes left in the payload")

        offset = 16 + length
        size, length = struct.unpack_from("<II", blob, offset)
        block = zlib.decompress(blob[offset + 8 : offset + 8 + length])
        collision = []
        position = 0
        while position < len(block):
            record_size = struct.unpack_from("<I", block, position)[0]
            collision.append(block[position + 16 : position + 16 + record_size])
            position += 16 + record_size
        return cls(version, tiles, batches, indices, vertices, collision, image)

    def payload(self) -> bytes:
        out = struct.pack("<i", len(self.tiles)) + b"".join(t.write(self.version) for t in self.tiles)
        out += struct.pack("<i", len(self.batches)) + self.batches.astype("<i4").tobytes()
        if self.image:
            bits, image_size, data = self.image
            out += struct.pack("<ii", bits, image_size) + data
        else:
            out += struct.pack("<i", 0)
        out += struct.pack("<i", len(self.indices)) + self.indices.astype("<u2").tobytes()
        return out + struct.pack("<i", len(self.vertices)) + self.vertices.tobytes()

    def collision_block(self) -> bytes:
        return b"".join(struct.pack("<I12x", len(record)) + record for record in self.collision)

    def write(self, level: int = 9) -> bytes:
        payload = self.payload()
        collision = self.collision_block()
        packed_payload = zlib.compress(payload, level)
        packed_collision = zlib.compress(collision, level)
        blob = struct.pack("<4sIII", b"GCNK", self.version, len(payload), len(packed_payload)) + packed_payload
        return blob + struct.pack("<II", len(collision), len(packed_collision)) + packed_collision

    def tile_triangles(self, tile: int):
        """World positions and triangle indices of one tile's batch (what its collision tree covers)."""
        index_offset, index_count, vertex_offset, vertex_count = self.batches[tile]
        positions = self.vertices["position"][vertex_offset : vertex_offset + vertex_count]
        triangles = self.indices[index_offset : index_offset + index_count].astype(np.int64).reshape(-1, 3)
        return positions, triangles


WEIGHT_NIBBLES = {1: [0], 2: [0, 1], 4: [2, 3, 1, 0]}


def encode_weights(weights: np.ndarray) -> tuple:
    """(layers, 512, bytes) for a chunk's weights image from a (512, 512, n) array of 0..15 weights for ecos[1:]."""
    count = weights.shape[-1]
    layers = 1 if count <= 1 else 2 if count == 2 else 4
    nibbles = np.zeros(weights.shape[:2] + (layers,), np.uint8)
    for k in range(count):
        nibbles[..., WEIGHT_NIBBLES[layers][k]] = np.clip(weights[..., k], 0, 15)
    flat = nibbles.reshape(-1, 2)
    return layers, weights.shape[0], (flat[:, 0] | flat[:, 1] << 4).astype(np.uint8).tobytes()


def raw_payload(blob: bytes) -> tuple[bytes, bytes]:
    """The inflated payload and collision block of a stored chunk, for byte comparisons."""
    if struct.unpack_from(">I", blob)[0] == COMPRESSED_MAGIC:
        blob = zlib.decompress(blob[8:])
    length = struct.unpack_from("<I", blob, 12)[0]
    payload = zlib.decompress(blob[16 : 16 + length])
    offset = 16 + length
    collision = zlib.decompress(blob[offset + 8 : offset + 8 + struct.unpack_from("<I", blob, offset + 4)[0]])
    return payload, collision


def main():
    import re
    from argparse import ArgumentParser

    from fork_zone import read_asset, read_pack_index
    from zone_terrain import QuantizedBvh

    parser = ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--verify", type=Path, help="Folder of .gcnk / .gcnk.z files to re-encode")
    parser.add_argument("--client", type=Path, help="Client folder: verify the chunks in its packs instead")
    parser.add_argument("--zone", default="FabledRealms", help="Zone prefix to check (default FabledRealms)")
    parser.add_argument("--collision", action="store_true", help="Also rebuild every collision tree from the mesh and compare")
    args = parser.parse_args()

    pattern = re.compile(re.escape(args.zone) + r"_-?\d+_-?\d+\.gcnk(\.z)?$", re.IGNORECASE)
    if args.client:
        entries = read_pack_index(args.client)
        sources = [(name, lambda name=name: read_asset(entries, name)) for name in sorted(entries) if pattern.match(name)]
    else:
        sources = [(p.name, p.read_bytes) for p in sorted(args.verify.iterdir()) if pattern.match(p.name)]

    ok = trees = trees_ok = 0
    for name, load in sources:
        blob = load()
        chunk = Chunk.read(blob)
        payload, collision = raw_payload(blob)
        if chunk.payload() != payload or chunk.collision_block() != collision:
            print(f"MISMATCH {name}")
            continue
        ok += 1
        if args.collision:
            for tile, record in enumerate(chunk.collision):
                positions, triangles = chunk.tile_triangles(tile)
                trees += 1
                rebuilt = QuantizedBvh(positions, triangles).serialize(record)
                trees_ok += rebuilt[16:] == record
    print(f"{ok}/{len(sources)} chunks re-encode exactly")
    if args.collision:
        print(f"{trees_ok}/{trees} collision trees rebuilt exactly from the render mesh")


if __name__ == "__main__":
    main()
