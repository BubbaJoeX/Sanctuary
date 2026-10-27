"""Free Realms zone files the editor reads and writes, byte for byte.

Terrain chunks (<zone>_<row>_<col>.gcnk) are ClientMods/world_chunks.py's Chunk, Tile and Object; this adds
their point lights, parsed from the raw records a Tile keeps, and the eco blend map as per-eco weights.

<zone>.gzne (version 2 and 3):
    "GZNE", u32 version, [u32 0 in version 3], u32 512, u32 128, f32 tile size (64), u32 8,
    i32 start x, start z, end x, end z (in tiles), u32 seed, then
    u32 eco count, eco { u32 index, cstring name, cstring detail texture, u32 detail repeat,
                         u32 flora count, flora { f32 density, min scale, max scale; u8 min alpha; cstring name } }
    u32 flora count, flora { cstring name, cstring texture, u8 flag }
    u32 wall count, wall { u32 point count, vec3 points }   invisible walls: posts as bottom, top pairs
    Anything past that is kept as it is.

A tile's point light: cstring name, cstring colour label, u8 type, vec4 position, f32 range, f32 intensity,
    colour bytes a, r, g, b.

Eco blend map (layout in world_chunks.py): the client layers a tile's ecos, the base first and each later
    one over it at its weight / 15. That fits the stock colour maps far better than a weighted sum.

<zone>_<row>_<col>.gck2: the chunk's colour map. "GCK2", version, texture size, uncompressed and
    compressed sizes, zlib DDS: 1024 x 1024 DXT1, 4 texels per unit, column along +z, row along -x
    (row 0 is the chunk's far x edge).

*.eco: a text ecosystem: *TEXTUREPART layers (detail map and repeat) and *FLORAPART layers.
"""

from __future__ import annotations

import struct
import zlib
from dataclasses import dataclass, field

import numpy as np

import world_chunks
from world_chunks import VERTEX, WEIGHT_NIBBLES, Chunk, Object, Tile, cstring, encode_weights, raw_payload  # noqa: F401

TILE_SIZE = 64
CHUNK_TILES = 4
CHUNK_SIZE = TILE_SIZE * CHUNK_TILES
ECO_MAP_SIZE = 512
COLOUR_MAP_SIZE = 1024


class Reader(world_chunks.Reader):
    @property
    def remaining(self) -> int:
        return len(self.data) - self.offset


# --- .gzne -------------------------------------------------------------------------------------------

@dataclass
class EcoFlora:
    name: str
    density: float = 500.0
    min_scale: float = 1.0
    max_scale: float = 1.0
    min_alpha: int = 128


@dataclass
class Eco:
    index: int
    name: str
    texture: str
    repeat: int = 8
    floras: list = field(default_factory=list)


@dataclass
class Flora:
    name: str
    texture: str
    flag: int = 1


@dataclass
class Zone:
    version: int = 3
    lead: int = 0                    # version 3 only
    sizes: tuple = (512, 128)
    tile_size: float = 64.0
    unknown: int = 8
    start_x: int = 0
    start_z: int = 0
    end_x: int = 16
    end_z: int = 16
    seed: int = 5239
    ecos: list = field(default_factory=list)
    floras: list = field(default_factory=list)
    walls: list = field(default_factory=list)      # [[bottom xyz, top xyz, bottom xyz, ...], ...]
    tail: bytes = b""

    @classmethod
    def read(cls, blob: bytes) -> "Zone":
        r = Reader(blob)
        if r.take(4) != b"GZNE":
            raise ValueError("not a GZNE zone")
        zone = cls(version=r.unpack("I"))
        if zone.version not in (2, 3):
            raise ValueError(f"GZNE version {zone.version} isn't supported")
        if zone.version >= 3:
            zone.lead = r.unpack("I")
        zone.sizes = r.unpack("II")
        zone.tile_size, zone.unknown = r.unpack("fI")
        zone.start_x, zone.start_z, zone.end_x, zone.end_z = r.unpack("4i")
        zone.seed = r.unpack("I")
        for _ in range(r.unpack("I")):
            eco = Eco(r.unpack("I"), r.cstring(), r.cstring(), r.unpack("I"))
            for _ in range(r.unpack("I")):
                density, min_scale, max_scale = r.unpack("3f")
                alpha = r.unpack("B")
                eco.floras.append(EcoFlora(r.cstring(), density, min_scale, max_scale, alpha))
            zone.ecos.append(eco)
        if r.remaining >= 4:
            zone.floras = [Flora(r.cstring(), r.cstring(), r.unpack("B")) for _ in range(r.unpack("I"))]
        if r.remaining >= 4:
            start = r.offset
            try:
                walls = []
                for _ in range(r.unpack("I")):
                    walls.append([r.unpack("3f") for _ in range(r.unpack("I"))])
                zone.walls = walls
            except ValueError:
                r.offset = start
                zone.walls = None
        zone.tail = r.data[r.offset :]
        return zone

    def write(self) -> bytes:
        out = b"GZNE" + struct.pack("<I", self.version)
        if self.version >= 3:
            out += struct.pack("<I", self.lead)
        out += struct.pack("<IIfI4iI", *self.sizes, self.tile_size, self.unknown,
                           self.start_x, self.start_z, self.end_x, self.end_z, self.seed)
        out += struct.pack("<I", len(self.ecos))
        for eco in self.ecos:
            out += struct.pack("<I", eco.index) + cstring(eco.name) + cstring(eco.texture)
            out += struct.pack("<II", eco.repeat, len(eco.floras))
            for flora in eco.floras:
                out += struct.pack("<3fB", flora.density, flora.min_scale, flora.max_scale, flora.min_alpha)
                out += cstring(flora.name)
        out += struct.pack("<I", len(self.floras))
        for flora in self.floras:
            out += cstring(flora.name) + cstring(flora.texture) + struct.pack("<B", flora.flag)
        if self.walls is not None:
            out += struct.pack("<I", len(self.walls))
            for wall in self.walls:
                out += struct.pack("<I", len(wall))
                for point in wall:
                    out += struct.pack("<3f", *point)
        return out + self.tail


# --- point lights ------------------------------------------------------------------------------------

@dataclass
class Light:
    """A point light. Color is bytes a, r, g, b; color_name is the artist's label (warm01, mines_cyan, ...)."""

    name: str = "newLight"
    color_name: str = ""
    type: int = 0
    position: tuple = (0.0, 0.0, 0.0, 1.0)
    range: float = 20.0
    intensity: float = 1.0
    color: bytes = b"\xff\xff\xff\xff"

    @classmethod
    def read(cls, r: Reader) -> "Light":
        return cls(r.cstring(), r.cstring(), r.unpack("B"), r.unpack("4f"), *r.unpack("2f"), r.take(4))

    def write(self) -> bytes:
        return (cstring(self.name) + cstring(self.color_name)
                + struct.pack("<B4f2f", self.type, *self.position, self.range, self.intensity) + self.color)


def read_lights(raw: bytes) -> list:
    """A tile's lights from the count-prefixed records world_chunks.Tile keeps."""
    if not raw:
        return []
    r = Reader(raw)
    return [Light.read(r) for _ in range(r.unpack("i"))]


def write_lights(lights: list) -> bytes:
    return struct.pack("<i", len(lights)) + b"".join(light.write() for light in lights)


# --- eco blend map -----------------------------------------------------------------------------------

def tile_region(tile: int):
    """Slices of a chunk-wide (x, z) map that belong to tile number `tile` (row-major along +x, then +z)."""
    tile_z, tile_x = divmod(tile, CHUNK_TILES)
    step = ECO_MAP_SIZE // CHUNK_TILES
    return slice(tile_x * step, (tile_x + 1) * step), slice(tile_z * step, (tile_z + 1) * step)


def decode_nibbles(image) -> np.ndarray:
    """(size, size, channels) uint8 nibbles, 0..15, from an eco blend map."""
    channels, size, data = image
    raw = np.frombuffer(data, np.uint8)
    return np.stack([raw & 15, raw >> 4], 1).reshape(size, size, channels)


def tile_weights(alphas: np.ndarray) -> np.ndarray:
    """Layer alphas (..., n - 1) in 0..1 to the weights (..., n) the client's layering gives each eco."""
    n = alphas.shape[-1] + 1
    weights = np.zeros(alphas.shape[:-1] + (n,), np.float32)
    weights[..., 0] = 1
    for k in range(1, n):
        a = alphas[..., k - 1]
        weights[..., :k] *= (1 - a)[..., None]
        weights[..., k] = a
    return weights


def tile_alphas(weights: np.ndarray) -> np.ndarray:
    """The inverse of tile_weights: the top layer takes its weight, each one below a share of what's left."""
    n = weights.shape[-1]
    alphas = np.zeros(weights.shape[:-1] + (n - 1,), np.float32)
    remaining = np.ones(weights.shape[:-1], np.float32)
    for k in range(n - 1, 0, -1):
        a = np.where(remaining > 1e-6, weights[..., k] / np.maximum(remaining, 1e-6), 0)
        a = np.rint(np.clip(a, 0, 1) * 15) / 15
        alphas[..., k - 1] = a
        remaining = remaining * (1 - a)
    return alphas


def chunk_eco_weights(chunk: Chunk) -> tuple[list, np.ndarray]:
    """Every eco used in the chunk, and their (512, 512, len) weights in 0..255 (each texel sums to 255)."""
    ecos = []
    for tile in chunk.tiles:
        for eco in tile.ecos:
            if eco not in ecos:
                ecos.append(eco)
    weights = np.zeros((ECO_MAP_SIZE, ECO_MAP_SIZE, max(len(ecos), 1)), np.float32)
    nibbles = decode_nibbles(chunk.image) if chunk.image else None
    for t, tile in enumerate(chunk.tiles):
        if not tile.ecos:
            continue
        rows, cols = tile_region(t)
        count = len(tile.ecos)
        if count == 1 or nibbles is None:
            weights[rows, cols, ecos.index(tile.ecos[0])] = 1
            continue
        order = WEIGHT_NIBBLES.get(chunk.image[0], list(range(chunk.image[0])))
        alphas = np.stack([nibbles[rows, cols, order[k]] for k in range(count - 1)], -1).astype(np.float32) / 15
        for k, w in enumerate(np.moveaxis(tile_weights(alphas), -1, 0)):
            weights[rows, cols, ecos.index(tile.ecos[k])] += w
    return ecos, np.rint(weights * 255).astype(np.uint8)


def encode_eco_weights(chunk: Chunk, ecos: list, weights: np.ndarray, max_layers: int = 5):
    """Sets each tile's eco list and the chunk's blend map from chunk-wide (512, 512, len(ecos)) weights.

    A tile keeps the ecos it had, in their order, while they still have weight; new ones go on top.
    At most max_layers ecos per tile (the 4-channel map's limit): the lightest are dropped.
    """
    weights = weights.astype(np.float32) / 255
    layers = []
    for t, tile in enumerate(chunk.tiles):
        rows, cols = tile_region(t)
        region = weights[rows, cols]
        totals = region.reshape(-1, len(ecos)).sum(0)
        used = [i for i in np.argsort(-totals, kind="stable") if totals[i] > 0.5][:max_layers]
        if not used:
            used = [ecos.index(tile.ecos[0]) if tile.ecos and tile.ecos[0] in ecos else int(np.argmax(totals))]
        kept = [ecos.index(e) for e in tile.ecos if e in ecos and ecos.index(e) in used]
        # The base layer needs weight wherever the others don't cover: the heaviest eco is the safest.
        if not kept or totals[kept[0]] < totals.max() * 0.25:
            heaviest = int(np.argmax(totals))
            kept = [heaviest] + [i for i in kept if i != heaviest]
        order = kept + [i for i in used if i not in kept]
        tile.ecos = [ecos[i] for i in order]
        layers.append(region[..., order])

    # One layer of zeros when every tile has a single eco, rather than no map at all.
    channels = max(max(len(tile.ecos) for tile in chunk.tiles) - 1, 1)
    alpha_map = np.zeros((ECO_MAP_SIZE, ECO_MAP_SIZE, channels), np.float32)
    for t, region in enumerate(layers):
        if region.shape[-1] < 2:
            continue
        rows, cols = tile_region(t)
        total = region.sum(-1, keepdims=True)
        normalized = np.where(total > 1e-6, region / np.maximum(total, 1e-6), np.eye(region.shape[-1])[0])
        alphas = tile_alphas(normalized)
        alpha_map[rows, cols, : alphas.shape[-1]] = alphas
    chunk.image = encode_weights(np.rint(alpha_map * 15).astype(np.uint8))


# --- .gck2 colour maps -------------------------------------------------------------------------------

def _from_565(value):
    v = value.astype(np.int64)
    r, g, b = v >> 11 & 31, v >> 5 & 63, v & 31
    return np.stack([r << 3 | r >> 2, g << 2 | g >> 4, b << 3 | b >> 2], -1).astype(np.float32)


def decode_dxt(data: bytes, width: int, height: int, fourcc: bytes = b"DXT1") -> np.ndarray:
    """RGB(A) uint8 pixels of DXT1/DXT3/DXT5 data."""
    block_size = 8 if fourcc == b"DXT1" else 16
    count = (width // 4) * (height // 4)
    blocks = np.frombuffer(data[: count * block_size], np.uint8).reshape(count, block_size)
    colour = blocks[:, block_size - 8 :]
    c0 = colour[:, 0:2].copy().view("<u2")[:, 0]
    c1 = colour[:, 2:4].copy().view("<u2")[:, 0]
    bits = colour[:, 4:8].copy().view("<u4")[:, 0]
    e0, e1 = _from_565(c0), _from_565(c1)
    four = ((c0 > c1) | (fourcc != b"DXT1"))[:, None]
    p2 = np.where(four, (2 * e0 + e1) / 3, (e0 + e1) / 2)
    p3 = np.where(four, (e0 + 2 * e1) / 3, 0)
    palette = np.stack([e0, e1, p2, p3], 1)
    index = (bits[:, None] >> (2 * np.arange(16))) & 3
    rgb = palette[np.arange(count)[:, None], index]
    alpha = np.full((count, 16, 1), 255, np.float32)
    if fourcc == b"DXT1":
        alpha[..., 0] = np.where((~four[:, 0])[:, None] & (index == 3), 0, 255)
    elif fourcc == b"DXT3":
        raw = blocks[:, :8]
        alpha[..., 0] = np.stack([raw & 15, raw >> 4], -1).reshape(count, 16) * 17
    else:
        a0, a1 = blocks[:, 0].astype(np.float32), blocks[:, 1].astype(np.float32)
        code_bits = blocks[:, 2:8].astype(np.uint64)
        packed = sum(code_bits[:, i] << np.uint64(8 * i) for i in range(6))
        codes = (packed[:, None] >> (np.uint64(3) * np.arange(16, dtype=np.uint64))) & np.uint64(7)
        codes = codes.astype(np.int64)
        eight = (a0 > a1)[:, None]
        k = codes.astype(np.float32)
        interp8 = np.where(codes == 0, a0[:, None], np.where(codes == 1, a1[:, None], ((8 - k) * a0[:, None] + (k - 1) * a1[:, None]) / 7))
        interp6 = np.where(codes == 0, a0[:, None], np.where(codes == 1, a1[:, None],
                  np.where(codes == 6, 0, np.where(codes == 7, 255, ((6 - k) * a0[:, None] + (k - 1) * a1[:, None]) / 5))))
        alpha[..., 0] = np.where(eight, interp8, interp6)
    pixels = np.concatenate([rgb, alpha], -1)
    image = pixels.reshape(height // 4, width // 4, 4, 4, 4).transpose(0, 2, 1, 3, 4).reshape(height, width, 4)
    return np.clip(np.rint(image), 0, 255).astype(np.uint8)


@dataclass
class ColourMap:
    version: int
    size: int
    header: bytes                        # the DDS header, kept for writing
    rgb: np.ndarray                      # (size, size, 3) uint8, rows along -x, columns along +z

    @classmethod
    def read(cls, blob: bytes) -> "ColourMap":
        magic, version, size, length, packed_length = struct.unpack_from("<4s4i", blob)
        if magic != b"GCK2":
            raise ValueError("not a GCK2 colour map")
        dds = zlib.decompress(blob[20 : 20 + packed_length])
        fourcc = dds[84:88]
        return cls(version, size, dds[:128], decode_dxt(dds[128:], size, size, fourcc)[..., :3])

    def write(self) -> bytes:
        from zone_terrain import encode_dxt1

        dds = self.header + encode_dxt1(self.rgb.astype(np.float64))
        packed = zlib.compress(dds, 9)
        return struct.pack("<4s4i", b"GCK2", self.version, self.size, len(dds), len(packed)) + packed


def dds_header(size: int) -> bytes:
    """A DDS header for a size x size DXT1 texture without mipmaps, like the stock colour maps'."""
    header = bytearray(128)
    struct.pack_into("<4sIIIIIII", header, 0, b"DDS ", 124, 0x81007, size, size, size * size // 2, 0, 0)
    struct.pack_into("<II4s", header, 76, 32, 4, b"DXT1")
    struct.pack_into("<I", header, 108, 0x1000)
    return bytes(header)


# --- .eco --------------------------------------------------------------------------------------------

def parse_eco(text: str) -> dict:
    """An ecosystem's texture layers and flora layers as {"textures": [...], "floras": [...]} of key/value dicts."""
    result = {"textures": [], "floras": []}
    section = None
    layer = None
    for line in text.splitlines():
        parts = line.split(None, 1)
        if not parts:
            continue
        key = parts[0].upper()
        value = parts[1].strip() if len(parts) > 1 else ""
        if key == "*TEXTUREPART":
            section = "textures"
        elif key == "*FLORAPART":
            section = "floras"
        elif key in ("*OBJECTPART", "*END_TEXTUREPART", "*END_FLORAPART", "*END_OBJECTPART"):
            section = None if key.startswith("*END") or key == "*OBJECTPART" else section
        elif key == "*LAYER" and section:
            layer = {"LAYER": value}
            result[section].append(layer)
        elif key == "*END_LAYER":
            layer = None
        elif layer is not None and key.startswith("*"):
            layer[key[1:]] = value
    return result


def eco_from_definition(index: int, name: str, text: str, flora_textures: dict) -> tuple[Eco, list]:
    """A .gzne eco (and the zone floras it needs) from a .eco file, for adding it to a zone."""
    definition = parse_eco(text)
    texture = definition["textures"][0] if definition["textures"] else {}
    eco = Eco(index, texture.get("LAYER", name), texture.get("DETAILMAP", name + ".dds"),
              int(float(texture.get("DETAILREPEAT", 8))))
    floras = []
    for layer in definition["floras"]:
        flora_name = layer.get("FLORA", "")
        if not flora_name:
            continue
        eco.floras.append(EcoFlora(flora_name, float(layer.get("DENSITY", 500)), float(layer.get("MINSCALE", 1)),
                                   float(layer.get("MAXSCALE", 1)), int(float(layer.get("MINALPHA", 128)))))
        floras.append(Flora(flora_name, flora_textures.get(flora_name.lower(), flora_name + ".dds")))
    return eco, floras

