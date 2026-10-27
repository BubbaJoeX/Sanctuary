"""Chunk terrain as the editor holds it, and back.

The editor keeps each chunk as a regular heightfield of 129 x 129 samples, one every 2 units (the stock
meshes' own grid: decimated, but every vertex sits on it), indexed [x, z] from the chunk's corner. Holes
(no triangle over a sample) are NaN. The colour map is held as (1024, 1024) RGB indexed the same way,
[x, z], so it lines up with the heights and the eco blend map; the .gck2 itself runs its rows along -x.

Saving rebuilds only the tiles whose heights changed: a regular 2-unit grid like zone_terrain.py's,
with its collision tree rebuilt by zone_terrain.QuantizedBvh. Untouched tiles keep the stock mesh and
collision, and their shared edges stay crack free because the heightfield is sampled from those
same edges.
"""

from __future__ import annotations

import numpy as np

from .formats import (CHUNK_SIZE, CHUNK_TILES, COLOUR_MAP_SIZE, TILE_SIZE, VERTEX, Chunk, ColourMap, Tile,
                      dds_header)

STEP = 2
SAMPLES = CHUNK_SIZE // STEP + 1            # 129
TILE_CELLS = TILE_SIZE // STEP              # 32
DEFAULT_COLORS = bytes([0, 0, 0, 0, 128, 128, 128, 0])


def chunk_origin(row: int, col: int) -> tuple[int, int]:
    """World x, z of a chunk's corner. Its name is <zone>_<row>_<col>, row along z, col along x."""
    return col * TILE_SIZE, row * TILE_SIZE


def rasterize(chunk: Chunk, row: int, col: int) -> np.ndarray:
    """The chunk's (129, 129) heightfield [x, z] sampled from its render mesh; NaN where it has none."""
    x0, z0 = chunk_origin(row, col)
    heights = np.full((SAMPLES, SAMPLES), np.nan, np.float32)
    positions = chunk.vertices["position"].astype(np.float64)

    triangles = []
    for tile in range(len(chunk.batches)):
        index_offset, index_count, vertex_offset, _ = chunk.batches[tile]
        if index_count > 0:
            triangles.append(chunk.indices[index_offset : index_offset + index_count].astype(np.int64).reshape(-1, 3) + vertex_offset)
    if not triangles:
        return heights
    corners = positions[np.concatenate(triangles)]                     # (n, 3, xyz)

    gx = (corners[:, :, 0] - x0) / STEP
    gz = (corners[:, :, 2] - z0) / STEP
    i0 = np.clip(np.ceil(gx.min(1) - 1e-4), 0, SAMPLES - 1).astype(np.int64)
    i1 = np.clip(np.floor(gx.max(1) + 1e-4), 0, SAMPLES - 1).astype(np.int64)
    j0 = np.clip(np.ceil(gz.min(1) - 1e-4), 0, SAMPLES - 1).astype(np.int64)
    j1 = np.clip(np.floor(gz.max(1) + 1e-4), 0, SAMPLES - 1).astype(np.int64)
    width = np.maximum(i1 - i0 + 1, 0)
    depth = np.maximum(j1 - j0 + 1, 0)
    counts = width * depth
    keep = counts > 0
    corners, i0, j0, width, counts = corners[keep], i0[keep], j0[keep], width[keep], counts[keep]

    # Every grid sample in every triangle's bounds, then barycentric coordinates for all of them at once.
    owner = np.repeat(np.arange(len(counts)), counts)
    local = np.arange(counts.sum()) - np.repeat(np.cumsum(counts) - counts, counts)
    si = i0[owner] + local % width[owner]
    sj = j0[owner] + local // width[owner]
    px, pz = x0 + si * STEP, z0 + sj * STEP

    a, b, c = corners[owner, 0], corners[owner, 1], corners[owner, 2]
    det = (b[:, 2] - c[:, 2]) * (a[:, 0] - c[:, 0]) + (c[:, 0] - b[:, 0]) * (a[:, 2] - c[:, 2])
    valid = np.abs(det) > 1e-9
    det = np.where(valid, det, 1)
    l1 = ((b[:, 2] - c[:, 2]) * (px - c[:, 0]) + (c[:, 0] - b[:, 0]) * (pz - c[:, 2])) / det
    l2 = ((c[:, 2] - a[:, 2]) * (px - c[:, 0]) + (a[:, 0] - c[:, 0]) * (pz - c[:, 2])) / det
    l3 = 1 - l1 - l2
    inside = valid & (l1 >= -1e-5) & (l2 >= -1e-5) & (l3 >= -1e-5)
    y = l1 * a[:, 1] + l2 * b[:, 1] + l3 * c[:, 1]
    heights[si[inside], sj[inside]] = y[inside]
    return heights


def colour_to_xz(colour: ColourMap) -> np.ndarray:
    return np.ascontiguousarray(colour.rgb[::-1])


def colour_from_xz(rgb: np.ndarray, template: ColourMap | None) -> ColourMap:
    version = template.version if template else 1
    header = template.header if template else dds_header(COLOUR_MAP_SIZE)
    return ColourMap(version, COLOUR_MAP_SIZE, header, np.ascontiguousarray(rgb[::-1]))


def normals(heights: np.ndarray) -> np.ndarray:
    """Unit normals (..., 3) from a heightfield [x, z] padded by one sample on every side."""
    dx = (heights[2:, 1:-1] - heights[:-2, 1:-1]) / (2 * STEP)
    dz = (heights[1:-1, 2:] - heights[1:-1, :-2]) / (2 * STEP)
    dx, dz = np.nan_to_num(dx), np.nan_to_num(dz)
    length = np.sqrt(dx * dx + 1 + dz * dz)
    return np.stack([-dx / length, 1 / length, -dz / length], -1)


def tile_mesh(row: int, col: int, tile: int, heights: np.ndarray, padded: np.ndarray, colors: bytes):
    """Vertices (VERTEX) and batch-local indices of one tile's regular grid, like zone_terrain.tile_mesh.

    heights is the chunk's (129, 129) field; padded the same with a one-sample border from its neighbours.
    Cells with a NaN corner are left out, so holes stay holes.
    """
    x0, z0 = chunk_origin(row, col)
    tile_z, tile_x = divmod(tile, CHUNK_TILES)
    cells = TILE_CELLS
    si = tile_x * cells + np.arange(cells + 1)
    sj = tile_z * cells + np.arange(cells + 1)

    # zone_terrain's order: vertices row-major over (z, x).
    jj, ii = np.meshgrid(sj, si, indexing="ij")
    y = heights[ii, jj]
    n = normals(padded)[ii, jj]
    lx = (ii - tile_x * cells) * STEP
    lz = (jj - tile_z * cells) * STEP

    vertices = np.zeros(ii.size, VERTEX)
    vertices["position"] = np.stack([x0 + ii * STEP, np.nan_to_num(y), z0 + jj * STEP], -1).reshape(-1, 3)
    normal_bytes = np.rint(n * 127 + 127).astype(np.uint8).reshape(-1, 3)
    vertices["normal"][:, 0] = normal_bytes[:, 2]
    vertices["normal"][:, 1] = normal_bytes[:, 1]
    vertices["normal"][:, 2] = normal_bytes[:, 0]
    vertices["weights"] = np.frombuffer(colors, np.uint8)
    vertices["uv"][:, 0] = (tile_z * 256 + np.clip(np.rint(lz * 4), 1, 255)).ravel()
    vertices["uv"][:, 1] = (tile_x * 256 + np.clip(np.rint(lx * 4), 1, 255)).ravel()
    vertices["detail_uv"][:, 0] = (lz * 4).ravel()
    vertices["detail_uv"][:, 1] = (lx * 4).ravel()

    width = cells + 1
    r, c = np.meshgrid(np.arange(cells), np.arange(cells), indexing="ij")
    a = r * width + c
    b, cc, d = a + 1, a + width, a + width + 1
    # Alternating diagonals; winding matches the client's own terrain.
    odd = ((r + c) % 2 == 1)[..., None]
    quads = np.where(odd, np.stack([a, b, cc, b, d, cc], -1), np.stack([a, b, d, a, d, cc], -1))
    hole = np.isnan(y)
    solid = ~(hole[r, c] | hole[r, c + 1] | hole[r + 1, c] | hole[r + 1, c + 1])
    indices = quads[solid].reshape(-1).astype(np.uint16)
    return vertices, indices


def rebuild_tiles(chunk: Chunk, row: int, col: int, heights: np.ndarray, padded: np.ndarray,
                  dirty: list, collision_template: bytes):
    """Replaces the meshes (and collision trees) of the dirty tiles with grids over the heightfield."""
    from zone_terrain import QuantizedBvh

    colors = chunk.vertices["weights"][0].tobytes() if len(chunk.vertices) else DEFAULT_COLORS
    meshes = []
    for tile in range(len(chunk.tiles)):
        index_offset, index_count, vertex_offset, vertex_count = chunk.batches[tile]
        if dirty[tile]:
            vertices, indices = tile_mesh(row, col, tile, heights, padded, colors)
            template = chunk.collision[tile] if tile < len(chunk.collision) and chunk.collision[tile] else collision_template
            if len(indices):
                record = QuantizedBvh(vertices["position"], indices.astype(np.int64).reshape(-1, 3)).serialize(template)[16:]
            else:
                record = chunk.collision[tile] if tile < len(chunk.collision) else template
            meshes.append((vertices, indices, record))
        else:
            meshes.append((chunk.vertices[vertex_offset : vertex_offset + vertex_count],
                           chunk.indices[index_offset : index_offset + index_count], chunk.collision[tile]))

    batches = []
    all_vertices = []
    all_indices = []
    vertex_total = index_total = 0
    for vertices, indices, _ in meshes:
        batches.append((index_total, len(indices), vertex_total, len(vertices)))
        all_vertices.append(vertices)
        all_indices.append(indices)
        vertex_total += len(vertices)
        index_total += len(indices)
    chunk.batches = np.array(batches, np.int32).reshape(-1, 4)
    chunk.vertices = np.concatenate(all_vertices) if all_vertices else np.zeros(0, VERTEX)
    chunk.indices = np.concatenate(all_indices).astype(np.uint16) if all_indices else np.zeros(0, np.uint16)
    chunk.collision = [record for _, _, record in meshes]


def new_chunk(row: int, col: int, version: int, base_eco: int, environment: int, tile_ids: set) -> Chunk:
    """An empty chunk of 16 tiles (meshes come from rebuild_tiles), with ids unused elsewhere in the zone."""
    rng = np.random.default_rng()
    tiles = []
    for index in range(CHUNK_TILES * CHUNK_TILES):
        tile_z, tile_x = divmod(index, CHUNK_TILES)
        tile_id = int(rng.integers(1, 32768))
        while tile_id in tile_ids:
            tile_id = int(rng.integers(1, 32768))
        tile_ids.add(tile_id)
        tiles.append(Tile(row + tile_z, col + tile_x, tile_id, environment, None, [base_eco], index=index))
    count = len(tiles)
    return Chunk(version, tiles, np.zeros((count, 4), np.int32), np.zeros(0, np.uint16), np.zeros(0, VERTEX),
                 [b""] * count, None)


def collision_template(chunk: Chunk) -> bytes | None:
    """A tile record whose Bullet header fields a rebuilt tree can copy."""
    return next((record for record in chunk.collision if len(record) >= 192), None)
