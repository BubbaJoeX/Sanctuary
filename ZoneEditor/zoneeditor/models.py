"""Prop previews: an actor (.adr) to its mesh (.dme) and diffuse texture, for the editor's viewport.

A .dme is "DMOD", version, an embedded .dma (its textures and materials), bounds, then per material a
vertex buffer and u16/u32 indices. Vertex layouts come from the client's materials_*.xml, looked up by
the material's definition hash (Jenkins one-at-a-time of its name). Only what the viewport needs is
read: positions, the first UV set and which texture to draw with.
"""

from __future__ import annotations

import io
import re
import struct
import xml.etree.ElementTree as ET
from pathlib import Path

import numpy as np

from .assets import jenkins
from .formats import Reader, decode_dxt

TYPE_SIZE = {"Float1": 4, "Float2": 8, "Float3": 12, "Float4": 16, "D3dcolor": 4, "Short2": 4, "Short4": 8,
             "UByte4": 4, "Color": 4, "Float16_2": 4, "Dec3n": 4}

# Texture name hints, best first, for picking a material's colour texture without its parameter names.
NOT_DIFFUSE = ("_n.", "_s.", "_spec", "_norm", "normal", "bump", "_tint", "tintmask", "_glow", "glowmap",
               "_detail", "_mask", "_ao.", "_e.", "_em.", "_r.", "_h.", "skintone")


class MaterialLibrary:
    def __init__(self, client_dir: Path):
        self.layouts = {}                    # name -> [(stride, [(offset, type, usage, usage index)])]
        self.definitions = {}                # definition hash -> preferred input layout name
        self.names = {}                      # definition hash -> name (SpecRigidAlphaTest, Water, ...)
        for path in sorted(client_dir.glob("materials*.xml")):
            try:
                root = ET.parse(path).getroot()
            except ET.ParseError:
                continue
            for node in root.iter("InputLayout"):
                name = node.attrib.get("Name", "").strip()
                entries = [(int(e.attrib.get("Offset", 0)), e.attrib.get("Type", ""), e.attrib.get("Usage", ""),
                            int(e.attrib.get("UsageIndex", 0))) for e in node.findall("Entry")
                           if e.attrib.get("Stream", "0") == "0"]
                stride = max((offset + TYPE_SIZE.get(kind, 0) for offset, kind, _, _ in entries), default=0)
                variants = self.layouts.setdefault(name, [])
                if all(existing != stride for existing, _ in variants):
                    variants.append((stride, entries))
            for node in root.iter("MaterialDefinition"):
                styles = node.findall("DrawStyle")
                if not styles:
                    continue
                style = next((s for s in styles if s.attrib.get("Name", "").lower() == "normal"), styles[0])
                definition = jenkins(node.attrib.get("Name", ""))
                self.definitions.setdefault(definition, style.attrib.get("InputLayout", ""))
                self.names.setdefault(definition, node.attrib.get("Name", ""))

    def alpha(self, definition_hash: int) -> str | None:
        """How a material uses alpha: "test" (cutout leaves and fences), "blend" (glass), "water" (the client's
        water shader, whose textures are wave normal maps) or None."""
        name = self.names.get(definition_hash, "").lower()
        if "alphatest" in name:
            return "test"
        if "water" in name:
            return "water"
        if any(word in name for word in ("alphablend", "alphascreen", "glass", "ghost", "hologram", "dualtexturerigidalpha")):
            return "blend"
        return None

    def layout(self, stride: int, definition_hash: int):
        variants = self.layouts.get(self.definitions.get(definition_hash, ""), [])
        for variant_stride, entries in variants:
            if variant_stride == stride:
                return entries
        for variant_stride, entries in variants:
            if variant_stride == stride + 4:
                return [e for e in entries if e[0] < stride]
        candidates = [entries for layouts in self.layouts.values() for s, entries in layouts if s == stride]
        return next((e for e in candidates if any(u == "Texcoord" for _, _, u, _ in e)), candidates[0] if candidates else [])


MESH_NAME = re.compile(rb"[\x02-\x7e]+?\.dme\x00", re.IGNORECASE)


def adr_mesh_name(blob: bytes) -> str | None:
    """The first .dme an actor definition names (its LOD 0): a cstring after its one-byte length."""
    for match in MESH_NAME.finditer(blob):
        run = match.group(0)
        # The length byte counts the name and its terminator; the name starts right after the byte that agrees.
        for start in range(1, len(run) - 4):
            if run[start - 1] == len(run) - start:
                return run[start:-1].decode("latin-1")
    return None


def _adr_fields(blob: bytes) -> list:
    """An actor definition's tag, length, value fields: [(tag, bytes)]. A length byte below 0x80 is the length;
    0xFF is followed by a big-endian u32 length; any other with its top bit set is the high byte of a two-byte one."""
    fields, i = [], 0
    while i + 1 < len(blob):
        tag, length = blob[i], blob[i + 1]
        i += 2
        if length == 0xFF:
            length = struct.unpack_from(">I", blob, i)[0]
            i += 4
        elif length & 0x80:
            length = (length & 0x7F) << 8 | blob[i]
            i += 1
        if i + length > len(blob):
            raise ValueError("an actor field runs past the end")
        fields.append((tag, blob[i : i + length]))
        i += length
    return fields


def _cstring(value: bytes) -> str:
    return value.split(b"\0", 1)[0].decode("latin-1")


def actor_skeleton(blob: bytes) -> str | None:
    """The .dsk an actor's meshes are skinned to (record 1, its field 1); None for a rigid prop."""
    try:
        for tag, record in _adr_fields(blob):
            if tag == 1:
                return next((_cstring(v) for t, v in _adr_fields(record) if t == 1), None) or None
    except ValueError:
        pass
    return None


def actor_attach_bones(blob: bytes) -> dict:
    """Where an actor fastens on to a character, by equipment slot (record 15: field 1 the slot, 2 the bone),
    e.g. a sword's grip on R_WEAPON."""
    bones = {}
    try:
        for tag, record in _adr_fields(blob):
            if tag != 15:
                continue
            fields = dict(_adr_fields(record))
            if 2 in fields:
                bones[fields.get(1, b"\0")[0]] = _cstring(fields[2])
    except ValueError:
        pass
    return bones


def read_skeleton(blob: bytes) -> dict:
    """A DSKE skeleton's bones in their bind pose: {NAME: 16 floats}, each the bone's model-space matrix as
    Direct3D lays it out (row vectors, translation in the last row), which is three.js's column-major order.

    "DSKE", u32 version, i32 names length, the NUL-separated names, i32 bone count, per bone (u32 name offset,
    i16 parent, i16 first child), then per bone 12 floats: its rotation rows and translation, relative to its parent.
    """
    if blob[:4] != b"DSKE":
        raise ValueError("not a DSKE skeleton")
    names_length = struct.unpack_from("<i", blob, 8)[0]
    names = blob[12 : 12 + names_length]
    offset = 12 + names_length
    count = struct.unpack_from("<i", blob, offset)[0]
    offset += 4
    links = [struct.unpack_from("<Ihh", blob, offset + 8 * i) for i in range(count)]
    offset += 8 * count
    local = np.zeros((count, 4, 4), np.float64)
    for i in range(count):
        local[i, :, :3] = np.array(struct.unpack_from("<12f", blob, offset + 48 * i)).reshape(4, 3)
        local[i, 3, 3] = 1.0
    world = [None] * count

    def model_space(i):
        if world[i] is None:
            parent = links[i][1]
            world[i] = local[i] if parent < 0 or parent >= count or parent == i else local[i] @ model_space(parent)
        return world[i]

    bones = {}
    for i, (name_offset, _, _) in enumerate(links):
        end = names.find(b"\0", name_offset)
        name = names[name_offset : end if end >= 0 else None].decode("latin-1").upper()
        bones[name] = [round(float(v), 6) for v in model_space(i).ravel()]
    return {"bones": bones}


def actor_set(blob: bytes) -> dict:
    """An .agr actor set: a character from parts (body, head, hair, gear), each an actor with the texture aliases
    it wears, one per material slot, and its tint alias; and its customization tints (HairTint, EyeTint: Tints.xml ids)."""
    try:
        root = ET.fromstring(blob.decode("latin-1"))
    except ET.ParseError:
        return {"actors": [{"file": name, "textures": [], "tint": None, "slot": None} for name in actor_group_members(blob)], "tints": {}}
    actors = [{
        "file": actor.attrib.get("name", ""),
        "textures": [t.attrib.get("name", "") for t in actor.findall("TextureAlias")],
        "tint": actor.attrib.get("tintAlias") or None,
        "slot": actor.attrib.get("EquippedSlotName") or None,
    } for actor in root.iter("Actor") if actor.attrib.get("name")]
    tints = {}
    for tint in root.iter("CustomizationTint"):
        try:
            tints[tint.attrib.get("TagName", "")] = int(tint.attrib.get("TintId", ""))
        except ValueError:
            continue
    return {"actors": actors, "tints": tints}


def read_tints(blob: bytes) -> dict:
    """The client's Tints.xml: {tint id: [r, g, b]} (0..1), what an item's dye or a hair colour's id names."""
    root = ET.fromstring(blob.decode("utf-8-sig", errors="replace"))
    tints = {}
    for value in root.iter("TintVal"):
        try:
            tints[int(value.attrib["id"])] = [round(float(value.attrib.get(c, 0)), 4) for c in "rgb"]
        except (KeyError, ValueError):
            continue
    return tints


def actor_aliases(blob: bytes) -> dict:
    """What an actor definition lets each placement choose: texture aliases (section 5: named texture
    variants per material slot, one the default) and tint aliases (section 6: named colours, one the
    default). An object picks one of each by name (its material and tint alias) and gives its own colour.

    Entries are fields 3 (slot hash), 4 (name); textures 5 (texture) and 7 (default), tints 5, 6, 7 (red,
    green, blue as big-endian floats) and 8 (default).
    """
    result = {"textures": [], "tints": []}
    try:
        sections = _adr_fields(blob)
    except ValueError:
        return result
    text = lambda value: value.rstrip(b"\0").decode("latin-1")
    for tag, section in sections:
        if tag not in (5, 6):
            continue
        try:
            entries = [dict(_adr_fields(entry)) for kind, entry in _adr_fields(section) if kind == 1]
        except ValueError:
            continue
        for entry in entries:
            if 4 not in entry:
                continue
            slot = struct.unpack("<I", entry[3])[0] if len(entry.get(3, b"")) == 4 else 0
            if tag == 5 and 5 in entry:
                result["textures"].append({"slot": slot, "name": text(entry[4]), "texture": text(entry[5]),
                                           "default": entry.get(7, b"\0")[:1] == b"\1"})
            elif tag == 6 and all(len(entry.get(k, b"")) == 4 for k in (5, 6, 7)):
                colour = [round(struct.unpack(">f", entry[k])[0], 4) for k in (5, 6, 7)]
                result["tints"].append({"slot": slot, "name": text(entry[4]), "colour": colour,
                                        "default": entry.get(8, b"\0")[:1] == b"\1"})
    return result


def actor_group_members(blob: bytes) -> list:
    """The actors an .agr actor set lists (<Actor name="..."/>), e.g. bw_pots_01.adr for sg_pots_01.agr."""
    return [name.decode("latin-1") for name in re.findall(rb'<Actor\s+name="([^"]+)"', blob)]


# Material parameters name what each of a material's textures is for, by the Jenkins hash of the name: the colour
# texture, the second colour layer of DualTexture materials (a head's face paint, drawn over the skin by its alpha),
# and the mask of TintMask materials (greyscale: where the dye or tint alias colours the diffuse).
TEXTURE_ROLES = {jenkins("Diffuse"): "diffuse", jenkins("Diffuse2"): "overlay", jenkins("TintMask"): "mask"}


def _dma_materials(blob: bytes):
    """A DMAT's textures and, per material, its definition hash, the textures its parameters use and which of
    them is its diffuse, overlay and mask."""
    r = Reader(blob)
    if r.take(4) != b"DMAT":
        raise ValueError("not a DMAT")
    r.unpack("I")
    textures = [name.decode("latin-1") for name in r.take(r.unpack("i")).split(b"\0") if name]
    by_hash = {jenkins(t.upper()): t for t in textures} | {jenkins(t): t for t in textures}
    materials = []
    for _ in range(r.unpack("i")):
        r.unpack("I")
        body = Reader(r.take(r.unpack("i")))
        definition = body.unpack("I")
        picked = []
        roles = {}
        for _ in range(body.unpack("i")):
            parameter = body.unpack("I")
            parameter_class = body.unpack("I")
            body.unpack("I")
            payload = body.take(body.unpack("i"))
            if parameter_class == 4 and len(payload) >= 4:
                value = struct.unpack_from("<I", payload)[0]
                texture = textures[value] if value < len(textures) else by_hash.get(value)
                picked.append(texture)
                if texture and parameter in TEXTURE_ROLES:
                    roles[TEXTURE_ROLES[parameter]] = texture
        materials.append((definition, [t for t in picked if t], roles))
    return textures, materials


def _diffuse(candidates: list, textures: list) -> str | None:
    for pool in (candidates, textures):
        colour = [t for t in pool if t and not any(hint in t.lower() for hint in NOT_DIFFUSE)]
        for preferred in ("_c.", "diffuse", "_d."):
            for texture in colour:
                if preferred in texture.lower():
                    return texture
        if colour:
            return colour[0]
    return None


def parse_dme(blob: bytes, library: MaterialLibrary) -> dict:
    r = Reader(blob)
    if r.take(4) != b"DMOD":
        raise ValueError("not a DMOD mesh")
    version = r.unpack("i")
    textures, materials = _dma_materials(r.take(r.unpack("i")))
    bounds_min, bounds_max = r.unpack("3f"), r.unpack("3f")
    count = r.unpack("i") if version >= 3 else len(materials)
    meshes = []
    for index in range(count):
        r.take(16)
        stride, vertex_count, index_size, index_count = r.unpack("4i")
        vertex_data = r.take(vertex_count * stride)
        index_data = r.take(index_count * index_size)
        definition, candidates, roles = materials[index] if index < len(materials) else (0, [], {})
        layout = library.layout(stride, definition)

        raw = np.frombuffer(vertex_data, np.uint8).reshape(vertex_count, stride)
        positions = raw[:, :12].copy().view("<f4").reshape(-1, 3)
        normals = None
        uvs = None
        for offset, kind, usage, usage_index in layout:
            if usage == "Position" and kind == "Float3":
                positions = raw[:, offset : offset + 12].copy().view("<f4").reshape(-1, 3)
            elif usage == "Normal" and kind == "Float3" and normals is None:
                normals = raw[:, offset : offset + 12].copy().view("<f4").reshape(-1, 3)
            elif usage == "Texcoord" and usage_index == 0 and uvs is None:
                if kind == "Float2":
                    uvs = raw[:, offset : offset + 8].copy().view("<f4").reshape(-1, 2)
                elif kind == "Float16_2":
                    uvs = raw[:, offset : offset + 4].copy().view("<f2").reshape(-1, 2).astype(np.float32)
                elif kind == "Short2":
                    uvs = raw[:, offset : offset + 4].copy().view("<i2").reshape(-1, 2).astype(np.float32) / 2048
        indices = np.frombuffer(index_data, "<u2" if index_size == 2 else "<u4").astype(np.uint32)
        # Front faces wind clockwise (Direct3D); the viewport's are counter-clockwise.
        indices = indices[: len(indices) // 3 * 3].reshape(-1, 3)[:, [0, 2, 1]].ravel()
        meshes.append({
            "positions": positions.astype(np.float32),
            "normals": normals.astype(np.float32) if normals is not None else None,
            "uvs": uvs if uvs is not None else np.zeros((vertex_count, 2), np.float32),
            "indices": indices,
            # The material's own Diffuse parameter when it names one; a DualTexture head's first texture is its face paint.
            "texture": roles.get("diffuse") or _diffuse(candidates, textures),
            "overlay": roles.get("overlay"),
            "mask": roles.get("mask"),
            "shader": library.names.get(definition, ""),
            "alpha": library.alpha(definition),
        })
    return {"meshes": meshes, "min": bounds_min, "max": bounds_max}


def open_texture(blob: bytes):
    """A DDS (or any image Pillow reads) as an RGBA Pillow image."""
    from PIL import Image

    try:
        image = Image.open(io.BytesIO(blob))
        image.load()
    except Exception:
        if blob[:4] != b"DDS ":
            raise
        height, width = struct.unpack_from("<II", blob, 12)
        fourcc = blob[84:88]
        if fourcc not in (b"DXT1", b"DXT3", b"DXT5"):
            raise
        image = Image.fromarray(decode_dxt(blob[128:], width, height, fourcc), "RGBA")
    return image.convert("RGBA")


def texture_png(blob: bytes, max_size: int = 512, opaque: bool = False) -> bytes:
    """A texture as PNG, scaled down to max_size for the viewport. opaque drops alpha, for textures that keep
    a mask there (eco detail maps) shown as swatches."""
    image = open_texture(blob)
    if opaque:
        image = image.convert("RGB")
    if max(image.size) > max_size:
        image.thumbnail((max_size, max_size))
    out = io.BytesIO()
    image.save(out, "PNG", compress_level=1)
    return out.getvalue()


def mean_colour(blob: bytes) -> list:
    """The average RGB of a texture: what a flat colour map tint of it looks like.

    Eco detail textures keep a blend mask in alpha (often near 0), so alpha is dropped first.
    """
    rgb = np.asarray(open_texture(blob), np.float32)[..., :3]
    return [int(v) for v in rgb.reshape(-1, 3).mean(0)]

