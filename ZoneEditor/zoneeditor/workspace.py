"""The zones the editor opens and everything a save writes.

A save writes client assets to src/Resources/ClientMods (the WebAPI patches them into the manifest it
serves) and Sanctuary's own data to src/Resources: Zones/<zone>.json, Zones/Environments/<zone>.json and
Houses.json. When the servers have been built, the same files are copied into their output folders
(src/Sanctuary.WebAPI/bin/*/*/ClientMods, src/Sanctuary.Gateway/bin/*/*/Resources) so a restart
picks them up without a rebuild.

Every chunk is editable. Saving one of a stock zone's (FabledRealms) also hides the client's own copy in
its read-only packs, so it downloads the edited one (unlock_client). A zone opened from a .gzne file
reads its zone files only from that file's folder, never stock, and saves write back there as well.
"""

from __future__ import annotations

import copy
import hashlib
import json
import math
import os
import re
import shutil
import struct
import threading
import time
import zlib
from collections import OrderedDict
from pathlib import Path

import numpy as np

from . import formats, models, terrain
from .assets import AssetSource, compress, decompress, jenkins
from .formats import CHUNK_TILES, ECO_MAP_SIZE, TILE_SIZE, Chunk, ColourMap, Light, Object, Zone
from .world import World

NAME = re.compile(r"^[a-z][a-z0-9_]{2,47}$")
# Downloaded assets, the actor usage count and prop thumbnails; beside the editor, not in the checkout it saves to.
CACHE_DIR = Path(__file__).resolve().parent.parent / ".cache"
CHUNK_STEP = CHUNK_TILES                       # chunk names step 4 tiles in row and col
# What the editor changes in a zone definition; the rest of the file is left as it is.
DEFINITION_KEYS = ["StartLatitude", "StartLongitude", "EndLatitude", "EndLongitude", "SpawnPosition", "SpawnRotation", "Sky",
                   "AreaDefinitions", "Portals", "SeasonalClientAreas"]
PAYLOAD_CACHE = 32                             # chunk payloads kept per zone, about 5 MB each
# Loose files in this folder of the client beat its packs and the manifest it downloaded: it lists their names when
# it starts and reads a file each time it loads it. A client the editor starts gets the zone's files there.
LIVE_FOLDER = "zone-editor-live"
LIVE_KINDS = (".gzne", ".gcnk", ".gck2")
LIVE_CHECK = 5                                 # seconds between checks that the client still runs
LIVE_GRACE = 90                                # seconds a launched client has to start before its live files go
LAUNCH_SERVER = "localhost:20042"              # the login server launch.ps1 points its clients at ($LoginServer)
LAUNCH_TIMEOUT = 300                           # seconds launch.ps1 has to start the servers and client


INLINE_NUMBERS = re.compile(r"\[\s*(-?[\d.eE+-]+(?:,\s*-?[\d.eE+-]+)*)\s*\]")


def actor_drawable(assets: AssetSource, actor: str) -> bool:
    """Whether the client has a mesh to draw an actor with (an actor set: each of its actors). Some stock actors
    name a mesh the manifest lists but the asset server never had, and draw as nothing."""
    try:
        if actor.lower().endswith(".agr"):
            members = models.actor_group_members(assets.read(actor))
            return bool(members) and all(actor_drawable(assets, member) for member in members)
        mesh = models.adr_mesh_name(assets.read(actor))
    except (KeyError, ValueError):
        return False
    return bool(mesh) and assets.available(mesh)


def _json_styles(data) -> list:
    """The ways Sanctuary's resource files lay JSON out: expanded (by 2 spaces, or 4 as Rewards.json is), short
    number lists inline, and an environment grid with one row per line."""
    styles = []
    for indent in (2, 4):
        expanded = json.dumps(data, indent=indent, ensure_ascii=False)
        styles += [expanded, INLINE_NUMBERS.sub(lambda m: "[" + ", ".join(v.strip() for v in m.group(1).split(",")) + "]", expanded)]
    if isinstance(data, dict) and isinstance(data.get("Rows"), list):
        head = {k: v for k, v in data.items() if k != "Rows"}
        rows = ",\n".join("    [" + ", ".join(map(str, row)) + "]" for row in data["Rows"])
        styles.append(json.dumps(head, indent=2, ensure_ascii=False)[:-2] + ',\n  "Rows": [\n' + rows + "\n  ]\n}")
    return styles


def _render(value, multiline: bool, indent: int, compact: bool = False) -> str:
    """value as JSON at indent; compact writes number lists as [1,2,3], as some files do."""
    text = _json_styles(value)[0 if multiline else 1].replace("\n", "\n" + " " * indent)
    return INLINE_NUMBERS.sub(lambda m: m.group(0).replace(", ", ","), text) if compact else text


def _compact(text: str) -> bool:
    """Whether a file's number lists are written [1,2,3] rather than [1, 2, 3]."""
    lists = [m.group(0) for m in INLINE_NUMBERS.finditer(text)]
    return any("," in found for found in lists) and not any(", " in found for found in lists)


def _patch_keys(text: str, old: dict, new: dict, keys: list) -> str:
    """Replaces, adds or removes only the top-level keys whose values changed, each in its old layout."""
    decoder = json.JSONDecoder()
    for key in keys:
        if old.get(key) == new.get(key):
            continue
        match = re.search(r'\n  "' + re.escape(key) + r'":\s*', text)
        if match:
            _, end = decoder.raw_decode(text, match.end())
            if key in new:
                span = text[match.end() : end]
                multiline = "\n" in span
                # A list of objects (a zone's areas and portals) keeps its unchanged entries as they're laid out.
                lists = isinstance(old.get(key), list) and isinstance(new[key], list)
                patched = _patch_list(span, old[key], new[key], 4) if lists else None
                text = text[: match.end()] + (patched if patched is not None else _render(new[key], multiline, 2)) + text[end:]
            else:
                text = text[: match.start()].rstrip(",") + text[end:]
        elif key in new:
            close = text.rstrip().rfind("}")
            text = text[:close].rstrip() + f',\n  "{key}": {_render(new[key], False, 2)}\n' + text[close:]
    return text


def _patch_list(text: str, old: list, new: list, indent: int) -> str | None:
    """A list of objects laid out one entry after another at indent (Houses.json, PointOfInterests.json, a zone's
    areas and portals): unchanged entries keep their text, changed and new ones are written in the file's number
    style. Entries are matched by Id when all of them have one, else by place. None if it isn't laid out so."""
    decoder = json.JSONDecoder()
    spans = []
    position = text.index("[") + 1
    while True:
        position = len(text) - len(text[position:].lstrip(" \n,"))
        if text[position] == "]":
            break
        value, end = decoder.raw_decode(text, position)
        spans.append(text[position:end])
        position = end
    pad, closing = " " * indent, " " * (indent - 2)
    rebuilt = lambda items: "[\n" + ",\n".join(pad + item for item in items) + "\n" + closing + "]"
    if not spans or rebuilt(spans) != text or any(not isinstance(item, dict) for item in old + new):
        return None
    # Each entry written as it was laid out (expanded or with inline number lists), a new one as the first was.
    styles = [(False, _compact(text)), (True, False), (False, False), (False, True)]
    style_of = lambda item, span: next((s for s in styles if _render(item, s[0], indent, s[1]) == span), styles[0])
    default = style_of(old[0], spans[0]) if old else styles[0]
    by_id = all("Id" in item for item in old + new)
    originals = {item["Id"]: (item, span) for item, span in zip(old, spans)} if by_id else dict(enumerate(zip(old, spans)))
    items = []
    for i, item in enumerate(new):
        before = originals.get(item["Id"] if by_id else i)
        if before and before[0] == item:
            items.append(before[1])
        else:
            multiline, compact = style_of(*before) if before else default
            items.append(_render(item, multiline, indent, compact))
    return rebuilt(items)


def _patch_items(text: str, old: list, new: list) -> str | None:
    """A top-level list of objects (Houses.json): see _patch_list."""
    return _patch_list(text, old, new, 2)


def write_json(path: Path, data, keys: list | None = None) -> bool:
    """Writes data in the layout the file already has, keeping its line endings; True if anything changed.

    Hand-laid-out files are patched rather than rewritten, so a save doesn't reflow them: a dict gets only
    its changed top-level `keys` replaced (FabledRealms.json's tile lists stay as they are), and a list of
    entries with ids (Houses.json) only its changed entries.
    """
    original = None
    if path.exists():
        with path.open(encoding="utf-8-sig", newline="") as f:
            original = f.read()
    old = json.loads(original) if original is not None else None
    if original is not None and old == data:
        return False
    crlf = original is not None and "\r\n" in original
    ending = "\n" if original is None or original.endswith("\n") else ""
    plain = original.replace("\r\n", "\n").rstrip("\n") if original is not None else None
    styles = [i for i, text in enumerate(_json_styles(old)) if text == plain] if plain is not None else [0]
    text = None
    if styles:
        text = _json_styles(data)[styles[0]]
    elif isinstance(old, dict) and isinstance(data, dict) and keys:
        text = _patch_keys(plain, old, data, keys)
    elif isinstance(old, list) and isinstance(data, list):
        text = _patch_items(plain, old, data)
    if text is None:
        text = _json_styles(data)[1]
    text += ending
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="utf-8", newline="") as f:
        f.write(text.replace("\n", "\r\n") if crlf else text)
    return True


def _draw_distance(raw: bytes) -> float | None:
    """An object's draw distance (0: the client's default); None for the stock records whose bytes aren't
    a sensible distance."""
    value = struct.unpack("<f", raw)[0]
    return round(value, 3) if math.isfinite(value) and 0 <= value <= 1e6 else None


def _set_environments(env: dict, cells: dict):
    """Sets tiles of a zone's environment grid from {(x tile, z tile): environment}. The grid is
    Rows[z tile - StartLongitude][x tile - StartLatitude] (longitude is z); it grows, with out-of-bounds
    (-1), to fit."""
    rows = env["Rows"]
    xs = [x for x, _ in cells]
    zs = [z for _, z in cells]
    while min(zs) < env["StartLongitude"]:
        rows.insert(0, [-1] * len(rows[0]))
        env["StartLongitude"] -= 1
    while min(xs) < env["StartLatitude"]:
        for r in rows:
            r.insert(0, -1)
        env["StartLatitude"] -= 1
    while env["StartLongitude"] + len(rows) <= max(zs):
        rows.append([-1] * len(rows[0]))
    while env["StartLatitude"] + len(rows[0]) <= max(xs):
        for r in rows:
            r.append(-1)
    for (x, z), environment in cells.items():
        rows[z - env["StartLongitude"]][x - env["StartLatitude"]] = environment




class Workspace:
    def __init__(self, sanctuary_root: Path, client_dir: Path, mirror_dir: Path | None, upstream: str | None):
        self.root = sanctuary_root
        self.resources = sanctuary_root / "src" / "Resources"
        self.mods_dir = self.resources / "ClientMods"
        self.zones_dir = self.resources / "Zones"
        self.client_dir = client_dir
        self.cache_dir = CACHE_DIR
        self.assets = AssetSource(client_dir, self.mods_dir, mirror_dir, upstream, self.cache_dir / "assets")
        self.library = models.MaterialLibrary(client_dir)
        self.world = World(self)
        self.projects = {}
        self.lock = threading.RLock()
        self._model_cache = {}
        self._skeletons = {}
        self._tints = None
        self._actor_names = None
        self._palette = None
        self._colours = {}
        self._item_ids: tuple[float, set] | None = None
        self._usage = None
        self._usage_thread = None
        self._missing = None
        self._missing_thread = None
        self._live_lock = threading.Lock()
        self._live_thread = None
        self._launching = False                # a client is being started: its live files stay
        self._grace_until = 0.0
        self._live_record = self.cache_dir / "live.json"
        self._seed_record = self.cache_dir / "live-seed.json"   # the zone last opened or tested, for launch.ps1
        self.live = json.loads(self._live_record.read_text(encoding="utf-8")) if self._live_record.exists() else None
        if self.live_dir.exists():
            # Left by an editor stopped before its client: kept while the client runs, then cleared.
            self._watch_live()
        else:
            self._drop_live()

    # --- listings ------------------------------------------------------------------------------------

    def definitions(self) -> dict:
        """Sanctuary zone definitions by lowercase zone name."""
        result = {}
        for path in sorted(self.zones_dir.glob("*.json")):
            try:
                data = json.loads(path.read_text(encoding="utf-8-sig"))
            except (OSError, json.JSONDecodeError):
                continue
            if isinstance(data, dict) and "Name" in data:
                result[data["Name"].lower()] = (path, data)
        return result

    def zones(self) -> list:
        definitions = self.definitions()
        zones = []
        for lower in sorted(self.assets.names(".gzne")):
            name = lower[: -len(".gzne")]
            listed = self.assets.manifest.get(lower, (None,))[0]
            mod = self.assets.mod_path(name + ".gzne")
            display = (mod.name.removesuffix(".z")[: -len(".gzne")] if mod
                       else listed.removesuffix(".z")[: -len(".gzne")] if listed else name)
            stock = self.assets.is_stock(name + ".gzne")
            definition = definitions.get(name, (None, {}))[1]
            zones.append({"name": display, "stock": stock, "custom": not stock,
                          "type": definition.get("Type"), "id": definition.get("Id")})
        return zones

    def actor_names(self) -> list:
        if self._actor_names is None:
            # As the manifest lists them, the casing stock chunks use.
            names = self.assets.names(".adr") | self.assets.names(".agr")
            self._actor_names = sorted((self.assets.manifest.get(n, (n,))[0].removesuffix(".z") for n in names), key=str.lower)
        return self._actor_names

    def _assets_stamp(self) -> str:
        """Changes when the actors or where assets come from do (a pack, the asset server's folder, a mod)."""
        return f"{len(self.actor_names())}:{len(self.assets.packs)}:{len(self.assets.mirror)}:{len(self.assets._mods())}"

    def missing_actors(self) -> list | None:
        """The actors the client has nothing to draw with (actor_drawable), which can't be placed. Checked once in
        the background and cached on disk, again when the assets change; None until it's ready."""
        path = self.cache_dir / "missing_actors.json"
        with self.lock:
            if self._missing is None and path.exists():
                cached = json.loads(path.read_text(encoding="utf-8"))
                if cached.get("stamp") == self._assets_stamp():
                    self._missing = cached["missing"]
            if self._missing is not None:
                return self._missing
            if self._missing_thread is None:
                self._missing_thread = threading.Thread(target=self._find_missing_actors, args=(path,), daemon=True)
                self._missing_thread.start()
        return None

    def _find_missing_actors(self, path: Path):
        stamp = self._assets_stamp()
        missing = [name for name in self.actor_names() if not actor_drawable(self.assets, name)]
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(json.dumps({"stamp": stamp, "missing": missing}), encoding="utf-8")
        with self.lock:
            self._missing = missing

    def actor_usage(self) -> dict | None:
        """How often each actor is placed across every zone's chunks, and in how many zones:
        {name: [placements, zones]}. Counted once in the background (a few thousand chunks) and cached on
        disk; None until it's ready."""
        path = self.cache_dir / "actor_usage.json"
        with self.lock:
            if self._usage is not None:
                return self._usage
            if path.exists():
                self._usage = json.loads(path.read_text(encoding="utf-8"))
                return self._usage
            if self._usage_thread is None:
                self._usage_thread = threading.Thread(target=self._count_actor_usage, args=(path,), daemon=True)
                self._usage_thread.start()
        return None

    def _count_actor_usage(self, path: Path):
        placements, zones = {}, {}
        pattern = re.compile(r"^(.+)_-?\d+_-?\d+\.gcnk$")
        for lower in sorted(self.assets.names(".gcnk")):
            match = pattern.match(lower)
            try:
                for name in formats.chunk_object_names(self.assets.read(lower)):
                    name = name.lower()
                    placements[name] = placements.get(name, 0) + 1
                    zones.setdefault(name, set()).add(match.group(1) if match else lower)
            except Exception:
                continue
        usage = {name: [count, len(zones[name])] for name, count in sorted(placements.items())}
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(json.dumps(usage), encoding="utf-8")
        with self.lock:
            self._usage = usage

    def thumbnail(self, actor: str) -> bytes:
        path = self.cache_dir / "thumbs" / (actor.lower() + ".png")
        if not path.exists():
            raise KeyError(actor)
        return path.read_bytes()

    def save_thumbnail(self, actor: str, png: bytes):
        if not re.fullmatch(r"[\w.\-]+\.a[dg]r", actor, re.IGNORECASE) or png[:8] != b"\x89PNG\r\n\x1a\n":
            raise ValueError("not an actor thumbnail")
        path = self.cache_dir / "thumbs" / (actor.lower() + ".png")
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(png)

    # --- drafts --------------------------------------------------------------------------------------
    # Unsaved work the editor autosaves, so a crash loses at most a few seconds of it. Drafts are kept
    # beside the editor, never published; saving a zone clears its draft.

    def _draft_dir(self, zone: str) -> Path:
        """By zone name, or for a zone opened from a file (zone is its path) by a hash of the path."""
        if "/" in zone or "\\" in zone:
            folder = "file_" + hashlib.sha1(str(Path(zone).resolve()).lower().encode()).hexdigest()[:12]
        elif re.fullmatch(r"[A-Za-z0-9_\-]+", zone):
            folder = zone.lower()
        else:
            raise ValueError(f"{zone} is not a zone name")
        return self.cache_dir / "drafts" / folder

    def save_draft_chunk(self, zone: str, row: int, col: int, body: bytes | None):
        """Autosaves a chunk; None drops it from the draft (its edits were undone)."""
        path = self._draft_dir(zone) / f"{int(row)}_{int(col)}.bin"
        if body is None:
            path.unlink(missing_ok=True)
            return
        path.parent.mkdir(parents=True, exist_ok=True)
        temporary = path.with_suffix(".tmp")
        temporary.write_bytes(zlib.compress(body, 1))
        temporary.replace(path)

    def save_draft_zone(self, zone: str, settings: dict):
        path = self._draft_dir(zone) / "zone.json"
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(json.dumps(settings), encoding="utf-8")

    def draft(self, zone: str) -> dict | None:
        folder = self._draft_dir(zone)
        if not folder.is_dir():
            return None
        chunks = []
        for path in sorted(folder.glob("*.bin")):
            row, col = map(int, path.stem.split("_"))
            chunks.append({"row": row, "col": col, "time": path.stat().st_mtime})
        zone_file = folder / "zone.json"
        settings = json.loads(zone_file.read_text(encoding="utf-8")) if zone_file.exists() else None
        if not chunks and settings is None:
            return None
        times = [c["time"] for c in chunks] + ([zone_file.stat().st_mtime] if settings is not None else [])
        return {"time": max(times), "chunks": chunks, "zone": settings}

    def draft_chunk(self, zone: str, row: int, col: int) -> bytes:
        path = self._draft_dir(zone) / f"{int(row)}_{int(col)}.bin"
        if not path.exists():
            raise KeyError(f"no draft of {row}_{col}")
        return zlib.decompress(path.read_bytes())

    def clear_draft(self, zone: str):
        shutil.rmtree(self._draft_dir(zone), ignore_errors=True)

    def eco_definitions(self) -> list:
        return sorted(name[:-4] for name in self.assets.names(".eco"))

    def palette(self) -> dict:
        """Every eco the client knows, by "name|texture": those in any zone's eco list (the first zone's detail
        repeat and flora win) and then the client's .eco definitions, for what no zone uses."""
        with self.lock:
            if self._palette is not None:
                return self._palette
        entries = {}
        flora_textures = {}
        for lower in sorted(self.assets.names(".gzne")):
            listed = self.assets.manifest.get(lower, (lower,))[0]
            zone_name = listed.removesuffix(".z")[: -len(".gzne")]
            try:
                zone = Zone.read(self.assets.read(lower))
            except (KeyError, ValueError, OSError):
                continue
            floras = {f.name.lower(): f for f in zone.floras}
            flora_textures.update({name: f.texture for name, f in floras.items()})
            for eco in zone.ecos:
                key = f"{eco.name.lower()}|{eco.texture.lower()}"
                if key not in entries:
                    entries[key] = {"eco": copy.deepcopy(eco), "zones": [],
                                    "floras": [copy.deepcopy(floras[f.name.lower()]) for f in eco.floras if f.name.lower() in floras]}
                entries[key]["zones"].append(zone_name)
        for name in self.eco_definitions():
            try:
                eco, floras = formats.eco_from_definition(0, name, self.assets.read(name + ".eco").decode("latin-1"), flora_textures)
            except (KeyError, ValueError, OSError, IndexError):
                continue
            key = f"{eco.name.lower()}|{eco.texture.lower()}"
            entries.setdefault(key, {"eco": eco, "zones": [], "floras": floras}).setdefault("definition", name)
        with self.lock:
            self._palette = dict(sorted(entries.items()))
        return self._palette

    def palette_list(self) -> list:
        return [{"key": key, "name": e["eco"].name, "texture": e["eco"].texture, "repeat": e["eco"].repeat,
                 "floras": [f.name for f in e["eco"].floras], "zones": e["zones"][:6], "zoneCount": len(e["zones"]),
                 "definition": e.get("definition")} for key, e in self.palette().items()]

    def item_ids(self) -> set:
        """Item definition ids the game has (ClientItemDefinitions.json), read again when the file changes."""
        path = self.resources / "ClientItemDefinitions.json"
        stamp = path.stat().st_mtime
        if self._item_ids is None or self._item_ids[0] != stamp:
            data = json.loads(path.read_text(encoding="utf-8-sig"))
            entries = data.values() if isinstance(data, dict) else data
            self._item_ids = (stamp, {entry["Id"] for entry in entries if isinstance(entry, dict) and "Id" in entry})
        return self._item_ids[1]

    def skies(self) -> list:
        sky_dir = self.client_dir / "Resources" / "SKY"
        skies = {path.name for path in sky_dir.glob("*.xml")} if sky_dir.is_dir() else set()
        skies |= {path.name for path in (self.root / "ClientMods" / "sky").glob("*.xml")}
        return sorted(skies, key=str.lower)

    def sky(self, name: str) -> str:
        if "/" in name or "\\" in name or not name.lower().endswith(".xml"):
            raise ValueError(f"{name} is not a sky file name")
        for folder in (self.root / "ClientMods" / "sky", self.client_dir / "Resources" / "SKY"):
            path = folder / name
            if path.exists():
                return path.read_text(encoding="utf-8-sig", errors="replace")
        raise KeyError(name)

    def terrain_environments(self) -> list:
        """ObjectTerrainData.xml rows, as players get them (World.environments): a tile's environment id picks its
        sky, atlas and minimap."""
        return self.world.environments()

    # --- assets for the viewport ---------------------------------------------------------------------

    def model(self, actor: str) -> dict:
        with self.lock:
            if actor.lower() in self._model_cache:
                return self._model_cache[actor.lower()]
        if actor.lower().endswith(".agr"):
            # An actor set: its first actor stands in for it (they differ by tint or texture alias).
            members = models.actor_group_members(self.assets.read(actor))
            if not members:
                raise KeyError(f"{actor} lists no actors")
            return self.model(members[0])
        definition = self.assets.read(actor) if actor.lower().endswith(".adr") else None
        mesh_name = models.adr_mesh_name(definition) if definition else actor
        if not mesh_name:
            raise KeyError(f"{actor} names no mesh")
        model = models.parse_dme(self.assets.read(mesh_name), self.library)
        model["aliases"] = models.actor_aliases(definition) if definition else {"textures": [], "tints": []}
        # A character part's skeleton, and where gear (a weapon) fastens to it.
        model["skeleton"] = models.actor_skeleton(definition) if definition else None
        model["attach"] = models.actor_attach_bones(definition) if definition else {}
        # What it plays (loc_stand standing about), and the skeleton's names of the bones its meshes are skinned to.
        model["animations"] = models.actor_animations(definition) if definition else {}
        model["skin_bones"] = []
        if model["skeleton"] and any(m["joints"] is not None for m in model["meshes"]):
            try:
                names = {jenkins(name): name for name in self.skeleton(model["skeleton"])["names"]}
                model["skin_bones"] = [names.get(h) for h in model["bone_hashes"]]
            except (KeyError, ValueError):
                pass
        with self.lock:
            self._model_cache[actor.lower()] = model
        return model

    def animation(self, file: str) -> bytes:
        """A .gr2 clip baked for the viewport (granny.bake) as JSON, kept in .cache/anims once baked."""
        from . import granny

        path = self.cache_dir / "anims" / (file.lower() + ".json")
        if path.is_file():
            return path.read_bytes()
        data = json.dumps(granny.bake(self.assets.read(file))).encode()
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(data)
        return data

    def actor_set(self, name: str) -> dict:
        """An .agr character's parts and customization tints (models.actor_set)."""
        return models.actor_set(self.assets.read(name))

    def skeleton(self, name: str) -> dict:
        with self.lock:
            if name.lower() in self._skeletons:
                return self._skeletons[name.lower()]
        skeleton = models.read_skeleton(self.assets.read(name))
        with self.lock:
            self._skeletons[name.lower()] = skeleton
        return skeleton

    def tints(self) -> dict:
        """The client's tint colours by id (Resources/Tints.xml), for hair colours and item dyes."""
        with self.lock:
            if self._tints is None:
                path = self.client_dir / "Resources" / "Tints.xml"
                self._tints = models.read_tints(path.read_bytes()) if path.is_file() else {}
            return self._tints

    def texture(self, name: str, size: int = 512, opaque: bool = False) -> bytes:
        return models.texture_png(self.assets.read(name), size, opaque)

    def texture_colour(self, name: str) -> list:
        with self.lock:
            if name.lower() in self._colours:
                return self._colours[name.lower()]
        try:
            colour = models.mean_colour(self.assets.read(name))
        except (KeyError, ValueError, OSError):
            colour = [128, 128, 128]
        with self.lock:
            self._colours[name.lower()] = colour
        return colour

    def project(self, name: str) -> "ZoneProject":
        """A zone by name, or by the path of a .gzne / .gzne.z file to open that copy of it (see ZoneProject)."""
        file = None
        if "/" in name or "\\" in name:
            file = Path(name).expanduser().resolve()
            if not file.name.lower().removesuffix(".z").endswith(".gzne"):
                raise ValueError(f"{name} is not a zone file (.gzne or .gzne.z)")
            if not file.is_file():
                raise KeyError(f"No file {file}")
            name = str(file)
        with self.lock:
            key = name.lower()
            if key not in self.projects:
                self.projects[key] = ZoneProject(self, name, file)
            return self.projects[key]

    def pick_zone_file(self, start: str | None = None) -> str | None:
        """Asks for a zone file with the desktop's own file dialog; None if it was cancelled."""
        import subprocess
        import sys

        folder = Path(start).parent if start and Path(start).parent.is_dir() else self.mods_dir
        script = ("import sys, tkinter, tkinter.filedialog as d\n"
                  "root = tkinter.Tk(); root.withdraw(); root.attributes('-topmost', True)\n"
                  "print(d.askopenfilename(parent=root, title='Open a zone file', initialdir=sys.argv[1],"
                  " filetypes=[('Zone files', '*.gzne *.gzne.z'), ('All files', '*.*')]))")
        result = subprocess.run([sys.executable, "-c", script, str(folder)], capture_output=True, text=True, timeout=3600)
        if result.returncode:
            raise RuntimeError(result.stderr.strip().splitlines()[-1] if result.stderr.strip() else "the file dialog failed")
        return str(Path(result.stdout.strip())) if result.stdout.strip() else None

    # --- new zones -----------------------------------------------------------------------------------

    def create_zone(self, name: str, kind: str, source: str | None, width: int, depth: int, height: float,
                    eco_source: str | None, eco_index: int | None, eco_key: str | None = None) -> "ZoneProject":
        """A new zone: a copy of an existing one (all its files under the new name), or blank terrain.

        Blank zones get width x depth chunks of flat ground at the given height, painted with one eco from
        the palette (eco_key) or another zone's list (eco_source / eco_index), e.g. FabledRealms' SG_Grass.
        """
        name = name.strip()
        if not NAME.match(name):
            raise ValueError("Zone names are 3-48 lowercase letters, digits and '_', starting with a letter")
        if self.assets.exists(name + ".gzne") or self.definitions().get(name):
            raise ValueError(f"{name} already exists")
        if self.assets.names() and any(n.startswith(name + "_") for n in self.assets.manifest):
            raise ValueError(f"The client already has assets named {name}_*")

        self.mods_dir.mkdir(parents=True, exist_ok=True)
        if source:
            source_project = ZoneProject(self, source)
            zone = copy.deepcopy(source_project.zone)
            # Chunks, colour maps, and minimap tiles with their index (like fork_zone.py), read before
            # anything is written so a file the asset sources can't supply doesn't leave half a zone.
            copies = {}
            for (row, col) in source_project.chunk_cells:
                for ext in ("gcnk", "gck2"):
                    asset = f"{source_project.name}_{row}_{col}.{ext}"
                    if self.assets.exists(asset):
                        copies[f"{name}_{row}_{col}.{ext}"] = asset
            prefix = source_project.name.lower() + "_tile"
            for lower, (listed, _) in sorted(self.assets.manifest.items()):
                if lower.startswith(prefix) and (lower.endswith(".dds") or lower.endswith("_tileinfo.txt")):
                    copies[name + listed.removesuffix(".z")[len(source_project.name):]] = lower
            data = {}
            for target, asset in copies.items():
                try:
                    data[target] = self.assets.read(asset)
                except (KeyError, OSError) as error:
                    # Some stock chunks have no colour map anywhere; the copy goes without, like the original.
                    if asset.lower().endswith(".gck2"):
                        continue
                    raise ValueError(f"Can't copy {source_project.name}: {asset} isn't in the client, the local "
                                     f"asset copy or (if set) the upstream server ({error})") from None
            for target, blob in data.items():
                if target.lower().endswith(".gcnk"):
                    version = int.from_bytes(blob[4:8], "little")
                    if version not in (5, 6):
                        raise ValueError(f"{source_project.name}'s chunks are version {version}; the editor reads and writes 5 and 6")
            for target, blob in data.items():
                if target.lower().endswith("_tileinfo.txt"):
                    blob = blob.decode("latin-1").replace(source_project.name, name).encode("latin-1")
                (self.mods_dir / target).write_bytes(blob)
            definition = dict(source_project.definition or {})
        else:
            zone = Zone(start_z=0, start_x=0, size_z=depth * CHUNK_TILES, size_x=width * CHUNK_TILES)
            source_zone = self.project(eco_source).zone if eco_source else None
            if eco_key:
                entry = self.palette()[eco_key]
                eco = entry["eco"]
                zone.ecos = [formats.Eco(0, eco.name, eco.texture, eco.repeat, copy.deepcopy(eco.floras))]
                zone.floras = copy.deepcopy(entry["floras"])
            elif source_zone and eco_index is not None:
                eco = next(e for e in source_zone.ecos if e.index == eco_index)
                zone.ecos = [formats.Eco(0, eco.name, eco.texture, eco.repeat, copy.deepcopy(eco.floras))]
                needed = {f.name.lower() for f in eco.floras}
                zone.floras = [copy.deepcopy(f) for f in source_zone.floras if f.name.lower() in needed]
            else:
                zone.ecos = [formats.Eco(0, "sg_grass", "sg_grass.dds", 10)]
            definition = {}

        (self.mods_dir / f"{name}.gzne.z").write_bytes(compress(zone.write()))

        definitions = [data for _, data in self.definitions().values()]
        houses = self._houses()
        zone_id = max([d.get("Id", 0) for d in definitions] + [1099]) + 1
        spawn = definition.get("SpawnPosition") or [width * CHUNK_TILES * TILE_SIZE / 2, height + 2, depth * CHUNK_TILES * TILE_SIZE / 2]
        definition.update({
            "$type": kind, "Id": zone_id, "Name": name, "Type": kind, "TileSize": TILE_SIZE,
            # Sanctuary's latitude is x and longitude z (BaseZone.GetTileFromPosition); End is exclusive.
            "StartLatitude": zone.start_x, "StartLongitude": zone.start_z,
            "EndLatitude": zone.end_x, "EndLongitude": zone.end_z,
            "SpawnPosition": spawn, "SpawnRotation": definition.get("SpawnRotation", [-1, 0, 0, 0]),
        })
        for key in ("AreaDefinitions", "EnabledClientAreaIds", "SeasonalClientAreas", "Scripts"):
            definition.pop(key, None)
        ordered = {k: definition[k] for k in ("$type", "Id", "Name", "Type", "TileSize", "StartLatitude",
                                              "StartLongitude", "EndLatitude", "EndLongitude", "SpawnPosition",
                                              "SpawnRotation") if k in definition}
        ordered.update({k: v for k, v in definition.items() if k not in ordered})
        write_json(self.zones_dir / f"{name}.json", ordered)

        if kind == "Housing":
            house_id = max([h.get("Id", 0) for h in houses] + [0]) + 1
            x, y, z = spawn[:3]
            house = {"Id": house_id, "NameId": 0, "ZoneId": zone_id, "Icon": 0, "ItemDefinitionIds": [],
                     "ExitObjectId": 0, "ExitPosition": [x, y, z, 0], "SpawnPosition": [x, y, z, 0],
                     "SpawnRotation": [-1, 0, 0, 0],
                     "BuildAreas": [{"Min": [x - 48, y - 20, z - 48], "Max": [x + 48, y + 60, z + 48]}]}
            # A copied lot keeps its props' ids, so its house's exit door, spawn and build areas still fit.
            # Deed items stay with the original: grant the new one with !house grant <id>.
            source_id = source and source_project.definition and source_project.definition.get("Id")
            original = next((h for h in houses if source_id and h.get("ZoneId") == source_id), None)
            if original:
                house.update({k: copy.deepcopy(v) for k, v in original.items() if k not in ("Id", "ZoneId", "ItemDefinitionIds")})
            houses.append(house)
            write_json(self.resources / "Houses.json", houses)

        written = [self.mods_dir / f"{name}.gzne.z", self.zones_dir / f"{name}.json"]
        if kind == "Housing":
            written.append(self.resources / "Houses.json")
        with self.lock:
            self.projects.pop(name, None)
        project = self.project(name)
        if not source:
            for row in range(0, depth * CHUNK_TILES, CHUNK_STEP):
                for col in range(0, width * CHUNK_TILES, CHUNK_STEP):
                    project.create_flat_chunk(row, col, height, 0, 0)
        else:
            written += [self.mods_dir / f"{name}_{row}_{col}.{ext}" for row, col in project.chunk_cells for ext in ("gcnk", "gck2")]
        written += project.written
        project.written = []
        self.mirror([path for path in written if path.exists()])
        return project

    def _houses(self) -> list:
        path = self.resources / "Houses.json"
        return json.loads(path.read_text(encoding="utf-8-sig")) if path.exists() else []

    # --- the game ------------------------------------------------------------------------------------

    def unlock_client(self) -> dict:
        """Hides the stock assets the client mods replace from the client's read-only packs, so it downloads
        the edited ones (install_client_mods.sync_stock_overrides). Needs the client closed."""
        import install_client_mods

        try:
            hidden, restored = install_client_mods.sync_stock_overrides(self.client_dir, self.mods_dir)
            return {"applied": True, "hidden": hidden, "restored": restored}
        except RuntimeError as error:
            return {"applied": False, "message": str(error)}

    def play(self, servers: bool, zone: str | None = None) -> dict:
        """Closes the client, hides replaced stock assets and starts it again with launch.ps1: the servers
        too when servers is set, since zone definitions and houses load at startup. The zone's files go to the
        client as live files (seed_live), so later saves reach it without another restart."""
        import subprocess

        script = self.root / "launch.ps1"
        if not script.exists():
            raise FileNotFoundError(f"{script} not found; start the client yourself")
        self._launching = True
        try:
            with self._live_lock:
                self._stop_clients()
                for _ in range(40):
                    if not self._clients():
                        break
                    time.sleep(0.25)
                unlock = self.unlock_client()
                live = self.seed_live(zone)
            # Its output goes to a file, not a pipe: the servers it starts inherit its handles and would hold a pipe
            # open long after it's done, and the editor would wait on it for good.
            log = self.root / "run" / "editor-launch.log"
            log.parent.mkdir(parents=True, exist_ok=True)
            arguments = ["powershell", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", str(script),
                         "-NoBuild" if servers else "-ClientOnly"]
            # The live files are seeded already; launch.ps1 seeds them itself for clients it starts on its own.
            environment = {**os.environ, "ZONE_EDITOR_SEEDED": "1"}
            with log.open("w", encoding="utf-8", errors="replace") as output:
                result = subprocess.run(arguments, stdin=subprocess.DEVNULL, stdout=output, stderr=subprocess.STDOUT,
                                        timeout=LAUNCH_TIMEOUT, cwd=self.root, env=environment)
        finally:
            self._launching = False
            self._grace_until = time.monotonic() + LIVE_GRACE
        return {"ok": result.returncode == 0, "servers": servers, "unlock": unlock, "live": live,
                "output": log.read_text(encoding="utf-8", errors="replace").strip()[-1500:]}

    def _clients(self) -> list:
        """The game clients launch.ps1 points at this checkout's servers (other sessions' stay out of it)."""
        import subprocess

        command = ("Get-CimInstance Win32_Process -Filter \"Name='FreeRealms.exe'\" | "
                   f"Where-Object {{ $_.CommandLine -match 'Server={re.escape(LAUNCH_SERVER)}(\\s|$)' }} | "
                   "ForEach-Object { $_.ProcessId }")
        result = subprocess.run(["powershell", "-NoProfile", "-Command", command], capture_output=True, text=True,
                                stdin=subprocess.DEVNULL, timeout=30)
        return [int(line) for line in result.stdout.split() if line.isdigit()]

    def _stop_clients(self):
        import subprocess

        for pid in self._clients():
            subprocess.run(["taskkill", "/PID", str(pid), "/F"], capture_output=True, stdin=subprocess.DEVNULL)

    def remember_zone(self, zone: str):
        """The zone opened in the editor, which a client launch.ps1 starts on its own gets live files for."""
        if not self._seed_record.exists() or json.loads(self._seed_record.read_text(encoding="utf-8")).get("zone") != zone:
            self._seed_record.parent.mkdir(parents=True, exist_ok=True)
            self._seed_record.write_text(json.dumps({"zone": zone}), encoding="utf-8")

    def seed_last(self) -> dict:
        """Live files for a client launch.ps1 starts on its own: the zone last opened in the editor or tested."""
        if not self._seed_record.exists():
            return {"zone": None, "files": 0, "chunks": 0}
        last = json.loads(self._seed_record.read_text(encoding="utf-8"))
        with self._live_lock:
            try:
                live = self.seed_live(last["zone"])
            except (KeyError, ValueError, OSError) as error:
                # Half a zone would shadow nothing useful and hide the failure; the client starts without it.
                self._drop_live()
                return {"zone": None, "files": 0, "chunks": 0, "error": str(error)}
            self._grace_until = time.monotonic() + LIVE_GRACE
        return live

    # --- live files ----------------------------------------------------------------------------------

    @property
    def live_dir(self) -> Path:
        return self.client_dir / LIVE_FOLDER

    def seed_live(self, zone: str | None) -> dict:
        """Puts a zone's files in the client's live folder before it starts: the zone file and every chunk and
        colour map (FabledRealms: about 200 MB, a few seconds). The client lists those names once, at startup,
        so a save that rewrites them (publish_live) shows in game when the ground round them loads again; a
        chunk added since, or another zone, waits for the next start."""
        self._drop_live()
        if not zone:
            return {"zone": None, "files": 0, "chunks": 0}
        project = self.project(zone)
        self.live_dir.mkdir(parents=True, exist_ok=True)
        names = []
        cells = sorted(project.chunk_cells)
        for name in [project.name + ".gzne"] + [f"{project.name}_{row}_{col}.{kind}" for row, col in cells
                                                for kind in ("gcnk", "gck2")]:
            try:
                data = project._read(name)
            except KeyError:
                # Listed in the manifest but on no asset server (a few stock chunks): the client has none either.
                continue
            (self.live_dir / name).write_bytes(data)
            names.append(name)
        self.live = {"zone": project.name, "files": names}
        self._live_record.parent.mkdir(parents=True, exist_ok=True)
        self._live_record.write_text(json.dumps(self.live), encoding="utf-8")
        self._seed_record.write_text(json.dumps({"zone": zone}), encoding="utf-8")
        self._watch_live()
        return {"zone": project.name, "files": len(names), "chunks": sum(name.endswith(".gcnk") for name in names)}

    def publish_live(self, paths: list) -> dict | None:
        """Copies a save's client files over the live files the running client listed (seed_live): each one is
        written beside its place and renamed into it, so the client never reads half a file. updated names what
        went, missed what the client didn't list. None while no client started from the editor runs."""
        import install_client_mods

        with self._live_lock:
            if not self.live or not self.live_dir.is_dir() or not install_client_mods.client_running():
                return None
            listed = {name.lower(): name for name in self.live["files"]}
            updated, missed = {}, {}
            for path in map(Path, paths):
                name = path.name.removesuffix(".z")
                if not name.lower().endswith(LIVE_KINDS):
                    continue
                target = listed.get(name.lower())
                if target is None:
                    missed[name.lower()] = name
                elif target not in updated.values():
                    part = self.live_dir / (target + ".part")
                    part.write_bytes(decompress(path.read_bytes()))
                    os.replace(part, self.live_dir / target)
                    updated[target.lower()] = target
            return {"zone": self.live["zone"], "updated": list(updated.values()), "missed": list(missed.values())}

    def clear_live(self) -> bool:
        """Removes the live files once no client runs, so they can't shadow later updates. True when gone."""
        import install_client_mods

        with self._live_lock:
            starting = self._launching or time.monotonic() < self._grace_until
            if self.live_dir.exists() and (starting or install_client_mods.client_running()):
                return False
            self._drop_live()
            return True

    def _drop_live(self):
        shutil.rmtree(self.live_dir, ignore_errors=True)
        self.live = None
        self._live_record.unlink(missing_ok=True)

    def _watch_live(self):
        """Clears the live files soon after the client closes."""
        if self._live_thread and self._live_thread.is_alive():
            return

        def watch():
            while True:
                time.sleep(LIVE_CHECK)
                if self.clear_live():
                    return

        self._live_thread = threading.Thread(target=watch, name="live-files", daemon=True)
        self._live_thread.start()

    # --- output folders ------------------------------------------------------------------------------

    def mirror(self, paths: list) -> list:
        """Copies written files into built server output folders, so a restart serves them."""
        copies = []
        src = self.root / "src"
        targets = []
        patches = self.resources / "ClientPatches"
        for build in src.glob("Sanctuary.WebAPI/bin/*/*"):
            targets.append((self.mods_dir, build / "ClientMods"))
            targets.append((patches, build / "ClientPatches"))
        for build in src.glob("Sanctuary.Gateway/bin/*/*"):
            targets.append((self.resources, build / "Resources"))
        for path in paths:
            for source_root, target_root in targets:
                try:
                    relative = path.relative_to(source_root)
                except ValueError:
                    continue
                # Client patches are the WebAPI's to serve, not the Gateway's to read.
                if source_root == self.resources and patches in path.parents:
                    continue
                if not target_root.parent.is_dir():
                    continue
                target = target_root / relative
                target.parent.mkdir(parents=True, exist_ok=True)
                _copy_when_free(path, target)
                copies.append(str(target))
        return copies


def _copy_when_free(source: Path, target: Path, seconds: float = 3.0):
    """Copies a file over one a server may be reading: the Gateway reloads a resource file as soon as it changes,
    and while it reads it Windows refuses to overwrite it (WinError 32), so a second save straight after one waits."""
    deadline = time.monotonic() + seconds
    while True:
        try:
            shutil.copy2(source, target)
            return
        except PermissionError:
            if time.monotonic() > deadline:
                raise
            time.sleep(0.05)


class ZoneProject:
    """One zone being edited. By name it reads the zone's files wherever the client would (ClientMods, then
    stock); opened from a file it reads them only from that file's folder, so nothing comes from stock, and
    saves write back there as well as publishing to ClientMods."""

    def __init__(self, workspace: Workspace, name: str, file: Path | None = None):
        self.ws = workspace
        assets = workspace.assets
        self.id = name                         # what the browser asks for it by: the name, or the file's path
        self.file = file
        self.folder = file.parent if file else None
        if file:
            self.name = file.name.removesuffix(".z")[: -len(".gzne")]
        else:
            listed = assets.manifest.get(name.lower() + ".gzne")
            mod = assets.mod_path(name + ".gzne")
            if mod:
                self.name = mod.name.removesuffix(".z")[: -len(".gzne")]
            elif listed:
                self.name = listed[0].removesuffix(".z")[: -len(".gzne")]
            else:
                raise KeyError(f"No zone named {name}")
            self.id = self.name
        self.stock = not file and assets.is_stock(self.name + ".gzne")
        self.zone = Zone.read(self._read(self.name + ".gzne"))
        self.zone_dirty = False
        self.environment_cells = {}            # (x tile, z tile) -> environment, for the server's grid
        self.lock = threading.RLock()
        self.written = []

        pattern = re.compile(re.escape(self.name.lower()) + r"_(-?\d+)_(-?\d+)\.gcnk$")
        self.chunk_cells = {}
        if file:
            for path in self.folder.iterdir():
                match = pattern.match(path.name.lower().removesuffix(".z"))
                if match:
                    self.chunk_cells[(int(match.group(1)), int(match.group(2)))] = "file"
        else:
            for asset in assets.names(".gcnk"):
                match = pattern.match(asset)
                if match:
                    row, col = int(match.group(1)), int(match.group(2))
                    self.chunk_cells[(row, col)] = "stock" if assets.is_stock(asset) else "mod"
        self.chunks = {}
        self.originals = {}
        self.snapshot = {}
        self.payloads = OrderedDict()          # (row, col) -> chunk_payload, the most recently sent last

        definitions = workspace.definitions()
        self.definition_path, self.definition = definitions.get(self.name.lower(), (None, None))
        environment_path = workspace.zones_dir / "Environments" / f"{self.name}.json"
        self.environments = json.loads(environment_path.read_text(encoding="utf-8-sig")) if environment_path.exists() else None

    # --- reading -------------------------------------------------------------------------------------

    def _local(self, name: str) -> Path | None:
        """A zone file in the opened file's folder, plain or compressed."""
        for path in (self.folder / name, self.folder / (name + ".z")):
            if path.is_file():
                return path
        return None

    def _has(self, name: str) -> bool:
        return self._local(name) is not None if self.folder else self.ws.assets.exists(name)

    def _read(self, name: str) -> bytes:
        if not self.folder:
            return self.ws.assets.read(name)
        path = self._local(name)
        if path is None:
            raise KeyError(f"No {name} in {self.folder}")
        return decompress(path.read_bytes())

    def editable(self, row: int, col: int) -> bool:
        # Every chunk: a saved stock chunk is hidden from the client's read-only packs (Workspace.unlock_client).
        return True

    def in_bounds(self, row: int, col: int) -> bool:
        z = self.zone
        return z.start_x <= col and col + CHUNK_TILES <= z.end_x and z.start_z <= row and row + CHUNK_TILES <= z.end_z

    def chunk(self, row: int, col: int) -> Chunk | None:
        with self.lock:
            if (row, col) not in self.chunks:
                if (row, col) not in self.chunk_cells:
                    return None
                blob = self._read(f"{self.name}_{row}_{col}.gcnk")
                self.chunks[(row, col)] = Chunk.read(blob)
                self.originals[(row, col)] = self.chunks[(row, col)]
            return self.chunks[(row, col)]

    def colour(self, row: int, col: int) -> ColourMap | None:
        name = f"{self.name}_{row}_{col}.gck2"
        return ColourMap.read(self._read(name)) if self._has(name) else None

    def eco_colour(self, index: int) -> list:
        eco = next((e for e in self.zone.ecos if e.index == index), None)
        if not eco:
            raise KeyError(index)
        return self.ws.texture_colour(eco.texture)

    def meta(self) -> dict:
        z = self.zone
        houses = []
        if self.definition:
            houses = [h for h in self.ws._houses() if h.get("ZoneId") == self.definition.get("Id")]
        pois = self.ws.world.pois(self.definition.get("Id") if self.definition else None)
        return {
            "id": self.id, "name": self.name, "file": str(self.file) if self.file else None,
            "stock": self.stock, "version": z.version,
            # A Sanctuary zone that install_client_mods.py put in the client's read-only pack: locked like stock.
            "installed": self.stock and self.ws.assets.mod_path(self.name + ".gzne") is not None,
            "bounds": {"startX": z.start_x, "startZ": z.start_z, "endX": z.end_x, "endZ": z.end_z},
            "ecos": [{"index": e.index, "name": e.name, "texture": e.texture, "repeat": e.repeat,
                      "floras": [f.name for f in e.floras]} for e in z.ecos],
            "floras": [{"name": f.name, "texture": f.texture} for f in z.floras],
            "walls": z.walls or [],
            "chunks": [{"row": r, "col": c, "editable": self.editable(r, c), "source": s}
                       for (r, c), s in sorted(self.chunk_cells.items())],
            "definition": self.definition, "houses": houses,
            "environments": self.ws.terrain_environments(),
            "skies": self.ws.skies(),
            "pois": pois,
            # What the NameIds of its points of interest and areas say.
            "names": self.ws.world.texts({row.get(key) for row in pois for key in ("NameId", "SubNameId") if row.get(key)}
                                         | {area.get("NameId") for area in (self.definition or {}).get("AreaDefinitions", []) if area.get("NameId")}),
        }

    def chunk_payload(self, row: int, col: int):
        """(meta, heights f32 [129 x 129], eco weights u8 [512 x 512 x n], colour RGBA u8 [1024 x 1024, x by z]).

        The colour map goes raw rather than as an image: decoding it is most of the work, and the browser
        paints on the raw pixels anyway. The last few payloads are kept, as the viewport drops chunks
        behind the camera and asks for them again.
        """
        with self.lock:
            if (row, col) in self.payloads:
                self.payloads.move_to_end((row, col))
                return self.payloads[(row, col)]
        chunk = self.chunk(row, col)
        if chunk is None:
            raise KeyError(f"No chunk {row}_{col}")
        heights = terrain.rasterize(chunk, row, col)
        ecos, weights = formats.chunk_eco_weights(chunk)
        colour = self.colour(row, col)
        rgba = np.full((1024, 1024, 4), 255, np.uint8)
        rgba[..., :3] = terrain.colour_to_xz(colour) if colour else 128

        objects = []
        lights = []
        for t, tile in enumerate(chunk.tiles):
            for i, o in enumerate(tile.objects):
                objects.append({"uid": f"{row}_{col}_{t}_{i}", "tile": t, "file": o.file_name,
                                "position": list(o.position[:3]), "rotation": list(o.rotation[:3]),
                                "scale": o.scale, "id": o.object_id, "flags": o.flags,
                                "material": o.material, "tintAlias": o.tint_alias,
                                "tint": list(o.tint) if o.tint_alias else None,
                                "drawDistance": _draw_distance(o.draw_distance)})
            for i, light in enumerate(formats.read_lights(tile.lights)):
                a, r, g, b = light.color
                lights.append({"uid": f"{row}_{col}_{t}_{i}", "tile": t, "name": light.name,
                               "colorName": light.color_name, "type": light.type,
                               "position": list(light.position[:3]), "range": light.range,
                               "intensity": light.intensity, "color": [r, g, b, a]})
        meta = {
            "row": row, "col": col, "version": chunk.version, "editable": self.editable(row, col),
            "tiles": [{"row": t.row, "col": t.col, "environment": t.environment, "ecos": t.ecos} for t in chunk.tiles],
            "ecos": ecos, "objects": objects, "lights": lights, "colourSize": rgba.shape[0],
        }
        payload = meta, heights.astype("<f4").tobytes(), np.ascontiguousarray(weights).tobytes(), rgba.tobytes()
        with self.lock:
            self.payloads[(row, col)] = payload
            while len(self.payloads) > PAYLOAD_CACHE:
                self.payloads.popitem(last=False)
        return payload

    # --- writing -------------------------------------------------------------------------------------

    def _original(self, uid: str, kind: str):
        """The object or light a uid from chunk_payload named, as the chunk was when the editor loaded it."""
        try:
            row, col, tile, index = map(int, uid.split("_"))
            chunk = self.snapshot.get((row, col)) or self.chunk(row, col)
            if kind == "lights":
                return formats.read_lights(chunk.tiles[tile].lights)[index]
            return chunk.tiles[tile].objects[index]
        except (AttributeError, IndexError, ValueError):
            return None

    def save_chunks(self, saves: list) -> list:
        """Saves several chunks against one snapshot, so props moved between them resolve in any order."""
        with self.lock:
            self.snapshot = dict(self.originals)
            try:
                return [self.save_chunk(*save) for save in saves]
            finally:
                self.snapshot = {}

    def _used_ids(self) -> set:
        ids = set()
        for key in list(self.chunk_cells):
            chunk = self.chunks.get(key)
            if chunk:
                ids |= {o.object_id for tile in chunk.tiles for o in tile.objects}
        return ids

    def _tile_ids(self) -> set:
        return {tile.id for chunk in self.chunks.values() for tile in chunk.tiles}

    def _template(self) -> tuple[bytes, int]:
        """A collision record and chunk version to build new chunks from: this zone's, else FabledRealms'."""
        for chunk in self.chunks.values():
            record = terrain.collision_template(chunk)
            if record:
                return record, chunk.version
        for key in sorted(self.chunk_cells):
            record = terrain.collision_template(self.chunk(*key))
            if record:
                return record, self.chunk(*key).version
        fabled = self.ws.project("FabledRealms")
        key = sorted(fabled.chunk_cells)[0]
        return terrain.collision_template(fabled.chunk(*key)), 6

    def create_flat_chunk(self, row: int, col: int, height: float, eco: int, environment: int):
        heights = np.full((terrain.SAMPLES, terrain.SAMPLES), height, np.float32)
        weights = np.full((ECO_MAP_SIZE, ECO_MAP_SIZE, 1), 255, np.uint8)
        colour = np.empty((1024, 1024, 3), np.uint8)
        colour[:] = self.eco_colour(eco) if self.zone.ecos else [96, 128, 64]
        self.save_chunk(row, col, {"ecos": [eco], "tileDirty": [True] * 16, "objects": [], "lights": [],
                                   "environments": [environment] * 16},
                        np.pad(heights, 1, mode="edge"), weights, colour)

    def save_chunk(self, row: int, col: int, meta: dict, padded: np.ndarray, weights: np.ndarray | None,
                   colour: np.ndarray | None) -> dict:
        """Rebuilds and writes one chunk from the editor's state.

        meta: ecos (zone eco indices of the weight channels), tileDirty (16 bools: heights changed),
        environments (16 ids), objects and lights (the chunk's full lists; existing ones carry their uid).
        padded is the (131, 131) heightfield with a one-sample border from the neighbours.
        """
        if row % CHUNK_STEP or col % CHUNK_STEP:
            raise ValueError("Chunk rows and columns are multiples of 4")
        new = (row, col) not in self.chunk_cells

        with self.lock:
            record, version = self._template()
            if new:
                base = int(meta["ecos"][0]) if meta.get("ecos") else 0
                chunk = terrain.new_chunk(row, col, version, base, 0, self._tile_ids())
                dirty = [True] * len(chunk.tiles)
            else:
                chunk = copy.deepcopy(self.chunk(row, col))
                dirty = [bool(d) for d in meta.get("tileDirty", [False] * len(chunk.tiles))]

            heights = padded[1:-1, 1:-1]
            if any(dirty):
                terrain.rebuild_tiles(chunk, row, col, heights, padded, dirty, record)

            for t, environment in enumerate(meta.get("environments") or []):
                if t < len(chunk.tiles) and environment is not None and chunk.tiles[t].environment != int(environment):
                    chunk.tiles[t].environment = int(environment)
                    if self.environments is not None:
                        self.environment_cells[(chunk.tiles[t].col, chunk.tiles[t].row)] = int(environment)

            if weights is not None:
                formats.encode_eco_weights(chunk, [int(e) for e in meta["ecos"]], weights)

            self._apply_objects(chunk, row, col, meta.get("objects", []))
            self._apply_lights(chunk, row, col, meta.get("lights", []))

            name = f"{self.name}_{row}_{col}"
            written = self._write(name + ".gcnk", chunk.write())
            if colour is not None or new:
                if colour is None:
                    colour = np.full((1024, 1024, 3), 128, np.uint8)
                written += self._write(name + ".gck2", terrain.colour_from_xz(colour, self.colour(row, col) if not new else None).write())

            self.chunks[(row, col)] = chunk
            self.originals[(row, col)] = chunk
            self.payloads.pop((row, col), None)
            self.chunk_cells[(row, col)] = "file" if self.file else "mod"
            self.written += written
            if new:
                self._grow(row, col, int((meta.get("environments") or [0])[0] or 0))
            return {"files": [str(p) for p in written], "vertices": int(len(chunk.vertices)),
                    "objects": sum(len(t.objects) for t in chunk.tiles)}

    def _write(self, name: str, data: bytes) -> list:
        """Writes one of the zone's files into ClientMods (the zone file compressed, like the client's) and, for
        a zone opened from a file, into that folder too, in the form the file already has there. The other
        form of the same file goes, so it can't shadow this one."""
        targets = [(self.ws.mods_dir, name.endswith(".gzne"))]
        if self.folder and self.folder.resolve() != self.ws.mods_dir.resolve():
            existing = self._local(name)
            targets.insert(0, (self.folder, existing is not None and existing.name.endswith(".z")))
        written = []
        for folder, compressed in targets:
            path = folder / (name + ".z" if compressed else name)
            other = folder / (name if compressed else name + ".z")
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(compress(data) if compressed else data)
            if other.exists():
                other.unlink()
            written.append(path)
        return written

    def _home_tile(self, chunk: Chunk, row: int, col: int, position) -> int:
        x0, z0 = terrain.chunk_origin(row, col)
        tile_x = int(np.clip((position[0] - x0) // TILE_SIZE, 0, CHUNK_TILES - 1))
        tile_z = int(np.clip((position[2] - z0) // TILE_SIZE, 0, CHUNK_TILES - 1))
        return tile_z * CHUNK_TILES + tile_x

    def _apply_objects(self, chunk: Chunk, row: int, col: int, objects: list):
        used = self._used_ids()
        rng = np.random.default_rng()
        for tile in chunk.tiles:
            tile.objects = []
        for item in objects:
            original = self._original(item["uid"], "objects") if item.get("uid") else None
            template = self._original(item["copyOf"], "objects") if item.get("copyOf") else None
            if original:
                o = copy.deepcopy(original)
            elif template:
                o = copy.deepcopy(template)
                o.object_id = 0
            else:
                o = Object(item["file"])
            o.file_name = item["file"]
            o.position = (*map(float, item["position"]), 1.0)
            o.rotation = (*map(float, item["rotation"]), 0.0)
            o.scale = float(item.get("scale", 1))
            # Texture variant, tint and draw distance: only what the editor sends, so stock bytes it can't
            # show (a draw distance that isn't a sensible float) stay as they are.
            if "material" in item:
                o.material = str(item["material"] or "")
            if "tintAlias" in item:
                o.tint_alias = str(item["tintAlias"] or "")
                tint = list(item.get("tint") or [1, 1, 1, 0])[:4]
                o.tint = tuple(float(v) for v in tint + [0.0] * (4 - len(tint))) if o.tint_alias else None
            distance = item.get("drawDistance")
            if distance is not None and distance != _draw_distance(o.draw_distance):
                o.draw_distance = struct.pack("<f", float(distance)) if distance else b"\0\0\0\0"
            if (o.material or o.tint_alias) and chunk.version < 6:
                chunk.version = 6           # version 5 objects have no texture or tint alias; nothing else differs
            if not original:
                while o.object_id == 0 or o.object_id in used:
                    o.object_id = int(rng.integers(1 << 24, 1 << 31))
                used.add(o.object_id)
                o.flags = int(rng.integers(1 << 24, 1 << 31)) if not template else o.flags
            # An object stays in the tile it came from unless it moved into this chunk from elsewhere.
            tile = item.get("tile")
            same_chunk = item.get("uid", "").startswith(f"{row}_{col}_")
            if tile is None or not same_chunk:
                tile = self._home_tile(chunk, row, col, o.position)
            chunk.tiles[int(tile)].objects.append(o)

    def _apply_lights(self, chunk: Chunk, row: int, col: int, lights: list):
        by_tile = [[] for _ in chunk.tiles]
        for item in lights:
            original = self._original(item["uid"], "lights") if item.get("uid") else None
            light = copy.deepcopy(original) if original else Light()
            light.name = item.get("name") or light.name
            light.color_name = item.get("colorName", light.color_name)
            light.position = (*map(float, item["position"]), 1.0)
            light.range = float(item.get("range", light.range))
            light.intensity = float(item.get("intensity", light.intensity))
            r, g, b, a = (list(item.get("color", [255, 255, 255, 255])) + [255])[:4]
            light.color = bytes([int(a), int(r), int(g), int(b)])
            tile = item.get("tile")
            if tile is None or not item.get("uid", "").startswith(f"{row}_{col}_"):
                tile = self._home_tile(chunk, row, col, light.position)
            by_tile[int(tile)].append(light)
        for tile, tile_lights in zip(chunk.tiles, by_tile):
            # A tile that had none and still has none keeps its empty record as it was.
            if tile_lights or tile.lights:
                tile.lights = formats.write_lights(tile_lights)

    def _grow(self, row: int, col: int, environment: int):
        """A new chunk widens the zone's bounds and fills its cells in the server's environment grid."""
        z = self.zone
        if z.cover(row, col, CHUNK_TILES):
            self.zone_dirty = True
        if self.environments is not None:
            for x in range(col, col + CHUNK_TILES):
                for zt in range(row, row + CHUNK_TILES):
                    self.environment_cells[(x, zt)] = environment

    def restore_ecos(self, ecos: list):
        """Re-adds ecos the editor added to the zone (from the palette) that this server doesn't hold, after a
        restart before they were saved, so the saved chunks' eco indices still point at them."""
        for item in sorted(ecos, key=lambda e: e["index"]):
            if any(e.index == item["index"] for e in self.zone.ecos):
                continue
            key = f"{item['name'].lower()}|{item['texture'].lower()}"
            if key not in self.ws.palette():
                raise ValueError(f"eco {item['name']} ({item['texture']}) isn't in the client's palette")
            added = self.add_eco(palette=key)
            if added["index"] != item["index"]:
                raise ValueError(f"{self.name}'s eco list changed since the editor loaded it; reload the zone")

    def add_eco(self, source_zone: str | None = None, source_index: int | None = None, definition: str | None = None,
                palette: str | None = None) -> dict:
        """Adds an eco to the zone's list: from the palette (Workspace.palette), another zone's list or a client
        .eco file. One the zone already has (same name and texture) is returned as it is."""
        index = max([e.index for e in self.zone.ecos] + [-1]) + 1
        if palette:
            entry = self.ws.palette()[palette]
            original = entry["eco"]
            eco = formats.Eco(index, original.name, original.texture, original.repeat, copy.deepcopy(original.floras))
            floras = copy.deepcopy(entry["floras"])
        elif definition:
            flora_textures = {}
            for zone_name in (source_zone, "FabledRealms"):
                if zone_name:
                    flora_textures |= {f.name.lower(): f.texture for f in self.ws.project(zone_name).zone.floras}
            eco, floras = formats.eco_from_definition(index, definition, self.ws.assets.read(definition + ".eco").decode("latin-1"), flora_textures)
        else:
            source = self.ws.project(source_zone).zone
            original = next(e for e in source.ecos if e.index == source_index)
            eco = formats.Eco(index, original.name, original.texture, original.repeat, copy.deepcopy(original.floras))
            needed = {f.name.lower() for f in original.floras}
            floras = [copy.deepcopy(f) for f in source.floras if f.name.lower() in needed]
        existing = next((e for e in self.zone.ecos if e.name.lower() == eco.name.lower() and e.texture.lower() == eco.texture.lower()), None)
        if existing:
            return {"index": existing.index, "name": existing.name, "texture": existing.texture, "repeat": existing.repeat,
                    "floras": [f.name for f in existing.floras]}
        self.zone.ecos.append(eco)
        known = {f.name.lower() for f in self.zone.floras}
        self.zone.floras += [f for f in floras if f.name.lower() not in known]
        self.zone_dirty = True
        return {"index": eco.index, "name": eco.name, "texture": eco.texture, "repeat": eco.repeat,
                "floras": [f.name for f in eco.floras]}

    def save_zone(self, settings: dict) -> dict:
        """Writes the zone file, the Sanctuary zone definition, environments and house.

        Sanctuary's JSON is read again here and only the editor's changes applied to it, so whatever else
        changed it since the zone was opened (another tool, another session) stays.
        """
        written = []
        with self.lock:
            # Invisible walls are the zone file's: each a list of posts, a bottom and a top point each.
            if settings.get("walls") is not None:
                self.zone.walls = [[tuple(float(v) for v in point) for point in wall] for wall in settings["walls"] if len(wall) >= 4]
                self.zone_dirty = True
            # A zone opened from a file publishes its zone file with its first chunks, or ClientMods' chunks
            # would go out with whatever .gzne the client already has.
            published = self.ws.assets.mod_path(self.name + ".gzne")
            unpublished = self.file is not None and self.written and (
                published is None or decompress(published.read_bytes()) != self.zone.write())
            if self.zone_dirty or unpublished:
                written += self._write(f"{self.name}.gzne", self.zone.write())

            if self.definition is not None:
                self.definition_path, definition = self.ws.definitions().get(self.name.lower(), (self.definition_path, self.definition))
                self.definition = definition
                if "spawnPosition" in settings:
                    x, y, z = settings["spawnPosition"]
                    definition["SpawnPosition"] = [round(x, 3), round(y, 3), round(z, 3)]
                if "spawnHeading" in settings:
                    angle = np.radians(settings["spawnHeading"])
                    definition["SpawnRotation"] = [round(float(np.cos(angle)), 6), round(float(np.sin(angle)), 6)]
                if "sky" in settings:
                    if settings["sky"]:
                        definition["Sky"] = settings["sky"]
                    else:
                        definition.pop("Sky", None)
                # Named areas and portals, as the World tool left them.
                for key, field in (("areas", "AreaDefinitions"), ("portals", "Portals"), ("seasons", "SeasonalClientAreas")):
                    if settings.get(key) is not None:
                        definition[field] = settings[key]
                if self.zone_dirty or not self.stock:
                    z = self.zone
                    # Latitude is x and longitude z; bounds only ever grow (FabledRealms' run past its chunks).
                    definition.update({
                        "StartLatitude": min(z.start_x, definition.get("StartLatitude", z.start_x)),
                        "StartLongitude": min(z.start_z, definition.get("StartLongitude", z.start_z)),
                        "EndLatitude": max(z.end_x, definition.get("EndLatitude", z.end_x)),
                        "EndLongitude": max(z.end_z, definition.get("EndLongitude", z.end_z)),
                    })
                path = self.definition_path or self.ws.zones_dir / f"{self.name}.json"
                if write_json(path, definition, DEFINITION_KEYS):
                    written.append(path)
                self.definition_path = path

            if settings.get("pois") is not None and self.definition is not None:
                path = self.ws.world.save_pois(self.definition["Id"], settings["pois"], settings.get("poisDeleted") or [])
                if path:
                    written.append(path)

            path = self.ws.zones_dir / "Environments" / f"{self.name}.json"
            if self.environment_cells and path.exists():
                environments = json.loads(path.read_text(encoding="utf-8-sig"))
                _set_environments(environments, self.environment_cells)
                # The server names the environments it has tiles of (!area here), so new ones aren't "unknown".
                known = self.ws.world.environment_names()
                names = environments.setdefault("Environments", {})
                for env_id in {str(e) for e in self.environment_cells.values()}:
                    if env_id not in names and env_id in known:
                        names[env_id] = known[env_id]
                if write_json(path, environments):
                    written.append(path)
                self.environments = environments
                self.environment_cells = {}

            if settings.get("houses") is not None and self.definition is not None:
                houses = self.ws._houses()
                ours = {h["Id"]: h for h in settings["houses"]}
                houses = [ours.pop(h.get("Id"), h) if h.get("ZoneId") == self.definition.get("Id") else h for h in houses]
                houses += ours.values()
                path = self.ws.resources / "Houses.json"
                if write_json(path, houses):
                    written.append(path)

            if settings.get("customSky"):
                path = self.ws.root / "ClientMods" / "sky" / settings["customSky"]["name"]
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_text(settings["customSky"]["xml"], encoding="utf-8")
                written.append(path)
                client_sky = self.ws.client_dir / "Resources" / "SKY" / path.name
                shutil.copyfile(path, client_sky)
                written.append(client_sky)

            self.zone_dirty = False
            written += self.written
            self.written = []
        copies = self.ws.mirror(written)
        # Stock assets this save replaced: the client has to stop reading them from its read-only packs.
        overrides = sorted({p.name.removesuffix(".z") for p in written if self.ws.assets.in_readonly_pack(p.name.removesuffix(".z"))})
        unlock = self.ws.unlock_client() if overrides else None
        server_data = any(self.ws.resources in p.parents and self.ws.mods_dir not in p.parents for p in written)
        return {"files": [str(p) for p in written], "mirrored": copies, "overrides": overrides, "unlock": unlock,
                "serverData": server_data}
