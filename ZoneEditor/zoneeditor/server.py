"""The editor's local web server: the browser UI in web/ and a small JSON/binary API over a Workspace.

Binary bodies (a chunk going to the browser, a save coming back) are a u32 length, that much JSON, then
the blobs the JSON's "sizes" list, back to back.
"""

from __future__ import annotations

import base64
import json
import mimetypes
import struct
import traceback
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import parse_qs, urlparse

import numpy as np

from .battles import Battles
from .combat import Combat
from .gamedata import ClientIds
from .npcs import NpcDefinitions
from .icons import Icons
from .rewards import RewardTables
from .store import Store
from .titles import Titles
from .workspace import Workspace
from .zone_scripts import ZoneScripts

WEB_DIR = Path(__file__).resolve().parent.parent / "web"


def frame(meta: dict, blobs: list) -> bytes:
    meta = dict(meta, sizes=[len(blob) for blob in blobs])
    header = json.dumps(meta).encode()
    return struct.pack("<I", len(header)) + header + b"".join(blobs)


def unframe(body: bytes):
    length, = struct.unpack_from("<I", body)
    meta = json.loads(body[4 : 4 + length])
    offset = 4 + length
    blobs = []
    for size in meta.get("sizes", []):
        blobs.append(body[offset : offset + size])
        offset += size
    return meta, blobs


def b64(array: np.ndarray) -> str:
    return base64.b64encode(np.ascontiguousarray(array).tobytes()).decode()


class Handler(BaseHTTPRequestHandler):
    workspace: Workspace = None
    npcs: NpcDefinitions = None
    rewards: RewardTables = None
    zone_scripts: ZoneScripts = None
    icons: Icons = None
    store: Store = None
    combat: Combat = None
    titles: Titles = None
    battles: Battles = None
    client_ids: ClientIds = None
    protocol_version = "HTTP/1.1"

    def log_message(self, fmt, *args):
        if args and str(args[1])[0] in "45":
            super().log_message(fmt, *args)

    def send(self, status: int, body: bytes, content_type: str, cache: bool = False):
        self.send_response(status)
        self.send_header("Content-Type", content_type)
        self.send_header("Content-Length", str(len(body)))
        self.send_header("Cache-Control", "max-age=86400" if cache else "no-store")
        self.end_headers()
        self.wfile.write(body)

    def send_json(self, data, status: int = 200):
        self.send(status, json.dumps(data).encode(), "application/json")

    def do_GET(self):
        self.handle_request("GET")

    def do_POST(self):
        self.handle_request("POST")

    def handle_request(self, method: str):
        url = urlparse(self.path)
        query = {key: values[0] for key, values in parse_qs(url.query).items()}
        try:
            if url.path.startswith("/api/"):
                route = getattr(self, f"{method.lower()}_{url.path[5:].replace('/', '_').replace('-', '_')}", None)
                if not route:
                    return self.send_json({"error": f"No route {method} {url.path}"}, 404)
                body = self.rfile.read(int(self.headers.get("Content-Length", 0))) if method == "POST" else b""
                return route(query, body)
            path = (WEB_DIR / (url.path.lstrip("/") or "index.html")).resolve()
            if WEB_DIR not in path.parents and path != WEB_DIR / "index.html" or not path.is_file():
                return self.send(404, b"Not found", "text/plain")
            self.send(200, path.read_bytes(), mimetypes.guess_type(path.name)[0] or "application/octet-stream")
        except KeyError as error:
            self.send_json({"error": f"Not found: {error}"}, 404)
        except (ValueError, PermissionError) as error:
            self.send_json({"error": str(error)}, 400)
        except Exception as error:
            traceback.print_exc()
            self.send_json({"error": f"{type(error).__name__}: {error}"}, 500)

    # --- reads -----------------------------------------------------------------------------------------

    def get_zones(self, query, body):
        ws = self.workspace
        self.send_json({"zones": ws.zones(), "sanctuary": str(ws.root), "client": str(ws.client_dir),
                        "mods": str(ws.mods_dir)})

    def get_zone(self, query, body):
        meta = self.workspace.project(query["name"]).meta()
        self.workspace.remember_zone(query["name"])
        self.send_json(meta)

    def get_chunk(self, query, body):
        project = self.workspace.project(query["zone"])
        meta, heights, weights, colour = project.chunk_payload(int(query["row"]), int(query["col"]))
        self.send(200, frame(meta, [heights, weights, colour]), "application/octet-stream")

    def get_model(self, query, body):
        model = self.workspace.model(query["name"])
        skinned = bool(model["skin_bones"])
        self.send(200, json.dumps({
            "min": model["min"], "max": model["max"], "aliases": model["aliases"],
            "skeleton": model["skeleton"], "attach": model["attach"],
            "animations": model["animations"], "skinBones": model["skin_bones"],
            "meshes": [{"texture": m["texture"], "overlay": m["overlay"], "mask": m["mask"], "shader": m["shader"],
                        "alpha": m["alpha"], "maps": m["maps"], "params": m["params"],
                        "positions": b64(m["positions"]), "uvs": b64(m["uvs"]),
                        "uvs2": b64(m["uvs2"]) if m["uvs2"] is not None else None,
                        "normals": b64(m["normals"]) if m["normals"] is not None else None,
                        "joints": b64(m["joints"]) if skinned and m["joints"] is not None else None,
                        "weights": b64(m["weights"]) if skinned and m["joints"] is not None else None,
                        "indices": b64(m["indices"])} for m in model["meshes"]],
        }).encode(), "application/json", cache=True)

    def get_animation(self, query, body):
        """A .gr2 clip sampled for the viewport (granny.bake): each bone's rotation, position and scale."""
        from . import granny

        if not granny.available():
            return self.send_json({"error": f"Animations need granny2.dll at {granny.DLL_PATH} (set FR_GRANNY_DLL)."}, 404)
        self.send(200, self.workspace.animation(query["name"]), "application/json", cache=True)

    def get_actor_set(self, query, body):
        """A character's parts (.agr): each part's actor, texture aliases, tint alias and slot, and its tints."""
        self.send(200, json.dumps(self.workspace.actor_set(query["name"])).encode(), "application/json", cache=True)

    def get_skeleton(self, query, body):
        """A skeleton's bones in their bind pose, for fastening gear (a weapon) to a character's bones."""
        self.send(200, json.dumps(self.workspace.skeleton(query["name"])).encode(), "application/json", cache=True)

    def get_tints(self, query, body):
        self.send_json({str(k): v for k, v in self.workspace.tints().items()})

    def get_texture(self, query, body):
        png = self.workspace.texture(query["name"], int(query.get("size", 512)), query.get("opaque") == "1")
        self.send(200, png, "image/png", cache=True)

    def get_texture_colour(self, query, body):
        self.send_json(self.workspace.texture_colour(query["name"]))

    def get_ecos(self, query, body):
        """The full eco palette: every zone's ecos and the client's .eco definitions."""
        self.send_json(self.workspace.palette_list())

    def get_dds(self, query, body):
        """A texture as the client has it, for the viewport to upload compressed with its own mipmaps."""
        data = self.workspace.assets.read(query["name"])
        if data[:4] != b"DDS ":
            raise ValueError(f"{query['name']} is not a DDS texture")
        self.send(200, data, "application/octet-stream", cache=True)

    def get_eco_colour(self, query, body):
        self.send_json(self.workspace.project(query["zone"]).eco_colour(int(query["index"])))

    def get_sky(self, query, body):
        self.send(200, self.workspace.sky(query["name"]).encode("utf-8"), "text/plain; charset=utf-8")

    def get_actors(self, query, body):
        """Every actor and actor set the client can draw, and (once counted) how often zones place each:
        {name: [placements, zones]}. checked is false until the ones with nothing to draw are known and left out."""
        words = query.get("q", "").lower().split()
        missing = self.workspace.missing_actors()
        left_out = set(missing or ())
        names = [n for n in self.workspace.actor_names() if n not in left_out and all(w in n for w in words)]
        limit = int(query.get("limit", len(names) or 1))
        self.send_json({"total": len(names), "names": names[:limit], "usage": self.workspace.actor_usage(),
                        "checked": missing is not None})

    def get_thumb(self, query, body):
        self.send(200, self.workspace.thumbnail(query["name"]), "image/png")

    def post_thumb(self, query, body):
        self.workspace.save_thumbnail(query["name"], body)
        self.send_json({"ok": True})

    # --- writes ----------------------------------------------------------------------------------------

    def post_zone_new(self, query, body):
        data = json.loads(body)
        project = self.workspace.create_zone(
            data["name"], data.get("type", "World"), data.get("source") or None, int(data.get("width", 2)),
            int(data.get("depth", 2)), float(data.get("height", 0)), data.get("ecoZone"), data.get("ecoIndex"),
            data.get("ecoKey"))
        self.send_json(project.meta())

    def get_draft(self, query, body):
        self.send_json(self.workspace.draft(query["zone"]))

    def get_draft_chunk(self, query, body):
        data = self.workspace.draft_chunk(query["zone"], int(query["row"]), int(query["col"]))
        self.send(200, data, "application/octet-stream")

    def post_draft_chunk(self, query, body):
        """An autosaved chunk: the same framing as a chunk going to the browser (meta, then its blobs). One with
        nothing dirty leaves the draft."""
        meta, _ = unframe(body)
        keep = any(meta.get("dirty", {}).values())
        self.workspace.save_draft_chunk(query["zone"], meta["row"], meta["col"], body if keep else None)
        self.send_json({"ok": True})

    def post_draft_zone(self, query, body):
        self.workspace.save_draft_zone(query["zone"], json.loads(body))
        self.send_json({"ok": True})

    def post_draft_clear(self, query, body):
        self.workspace.clear_draft(query["zone"])
        self.send_json({"ok": True})

    def post_pick_zone(self, query, body):
        """Shows the desktop's file dialog for a zone file: {"path": ...}, or {"path": null} if cancelled."""
        self.send_json({"path": self.workspace.pick_zone_file(json.loads(body or b"{}").get("start"))})

    # --- NPC definitions (Npcs.json) -----------------------------------------------------------------------

    def get_npcs(self, query, body):
        """Definitions matching q (id, name or actor), or every one for an actor (model)."""
        result = self.npcs.search(query.get("q", ""), int(query.get("limit", 200)), query.get("model"))
        self.send_json(dict(result, scripts=self.npcs.scripts()))

    def get_npcs_entry(self, query, body):
        self.send_json(self.npcs.get(int(query["id"])))

    def post_npcs_save(self, query, body):
        self.send_json(self.npcs.save(json.loads(body)))

    def post_npcs_new(self, query, body):
        data = json.loads(body)
        self.send_json(self.npcs.create(data["model"], data.get("name"), data.get("copyOf")))

    def post_npcs_delete(self, query, body):
        self.send_json(self.npcs.delete(int(json.loads(body)["id"])))

    # --- reward tables (Rewards.json), for the admin panel's GameData editor ---------------------------------

    def get_rewards(self, query, body):
        """Every reward table, each with what rolls it (UsedBy)."""
        self.send_json(self.rewards.overview())

    def post_rewards_save(self, query, body):
        """{table, originalKey?}: adds a table, or replaces (and may rename) originalKey."""
        data = json.loads(body)
        self.send_json(self.rewards.save(data["table"], data.get("originalKey")))

    def post_rewards_delete(self, query, body):
        self.send_json(self.rewards.delete(json.loads(body)["key"]))

    # --- jobs and combat abilities (Profiles.json, CombatJobs.json, CombatAbilities.json), for the GameData editor ---

    def get_jobs(self, query, body):
        """Every job: its profile, texts and combat kit."""
        self.send_json(self.combat.jobs())

    def get_jobs_entry(self, query, body):
        """One job with its kit and the abilities it fights with."""
        self.send_json(self.combat.job(int(query["id"])))

    def post_jobs_save(self, query, body):
        """{profile, texts, kit, abilities: [{ability, texts}]}: a job and everything it fights with, in one save."""
        self.send_json(self.combat.save_job(json.loads(body)))

    def post_jobs_delete(self, query, body):
        self.send_json(self.combat.delete_job(int(json.loads(body)["id"])))

    def get_abilities(self, query, body):
        """Every combat ability, with its texts and the jobs that fight with it."""
        self.send_json(self.combat.abilities())

    def post_abilities_save(self, query, body):
        """{ability (Id null: a new one), texts}"""
        data = json.loads(body)
        self.send_json(self.combat.save_ability(data["ability"], data.get("texts")))

    def post_abilities_delete(self, query, body):
        self.send_json(self.combat.delete_ability(int(json.loads(body)["id"])))

    # --- player titles (PlayerTitles.json) and battles (Battles.json, Mobs.json), for the GameData editor -------

    def get_titles(self, query, body):
        self.send_json(self.titles.overview())

    def post_titles_save(self, query, body):
        """{title (Id null: a new one), texts}"""
        data = json.loads(body)
        self.send_json(self.titles.save(data["title"], data.get("texts")))

    def post_titles_delete(self, query, body):
        self.send_json(self.titles.delete(int(json.loads(body)["id"])))

    def get_battles(self, query, body):
        self.send_json(self.battles.overview())

    def get_battles_options(self, query, body):
        """World zones, mobs and reward tables a battle can name."""
        self.send_json(self.battles.options())

    def get_battles_entry(self, query, body):
        self.send_json(self.battles.entry(int(query["id"])))

    def post_battles_save(self, query, body):
        """{battle (Id null: a new one), texts, mobs: changed Mobs.json entries}"""
        data = json.loads(body)
        self.send_json(self.battles.save(data["battle"], data.get("texts"), data.get("mobs")))

    def post_battles_delete(self, query, body):
        self.send_json(self.battles.delete(int(json.loads(body)["id"])))

    def get_client_ids(self, query, body):
        """The client's animations, composite effects or image sets (kind) by name or id (q): [{id, name}]; with ids
        (1,2,3) instead, {id: name} for those."""
        if query.get("ids") is not None:
            self.send_json(self.client_ids.names(query.get("kind", ""), [i for i in query["ids"].split(",") if i.isdigit()]))
        else:
            self.send_json(self.client_ids.search(query.get("kind", ""), query.get("q", ""), min(200, int(query.get("limit", 60)))))

    # --- the coin store (CoinStoreItems.json, prices, CoinStoreBundles.json), for the admin panel ---------------

    def get_store_categories(self, query, body):
        """What the client calls each store category: {id: name or null}."""
        self.send_json({str(k): v for k, v in self.store.category_names().items()})

    def post_store_save(self, query, body):
        """{entries: [{id, categoryId, tintGroupId, buyDisabled, coinStoreOnly, hidden, cost, memberDiscount,
        resellValue}]}: puts items in the store or changes how it sells them."""
        self.send_json(self.store.save(json.loads(body)["entries"]))

    def post_store_remove(self, query, body):
        self.send_json(self.store.remove(json.loads(body)["ids"]))

    def post_store_bundle_save(self, query, body):
        """{bundle: {id, comment, items: [{id, quantity, tint}], coins}, categoryId}"""
        data = json.loads(body)
        self.send_json(self.store.save_bundle(data["bundle"], data.get("categoryId")))

    def post_store_bundle_delete(self, query, body):
        data = json.loads(body)
        self.send_json(self.store.delete_bundle(int(data["id"]), bool(data.get("keepCard"))))

    # --- game icons ------------------------------------------------------------------------------------------

    def get_icon(self, query, body):
        """An icon as PNG: set (an image set id: items, mounts, collections) or image (an image id: titles, pets,
        abilities, houses) or notification (an NPC's overhead icon), at most size pixels, tinted by tint (a Tints.xml id)
        where the icon's mask says."""
        if query.get("notification"):
            png = self.icons.notification_png(query["notification"], min(128, int(query.get("size", 64))))
        else:
            png = self.icons.png(query.get("set"), query.get("image"), int(query.get("size", 64)), int(query.get("tint", 0) or 0))
        self.send(200, png, "image/png", cache=True)

    def get_icons(self, query, body):
        """Images by file name or id (q), for picking an icon by image id: [{id, name}]."""
        self.send_json(self.icons.search(query.get("q", ""), min(200, int(query.get("limit", 60)))))

    # --- the zone's own NPCs (its Lua scripts' spawn lines) ------------------------------------------------------

    def post_zone_npcs_find(self, query, body):
        """{zone, guid, def, spawn: {x, y, z, h}}: the line that spawns that NPC, if it can be edited here."""
        data = json.loads(body)
        self.send_json(self.zone_scripts.locate(data["zone"], data["guid"], int(data["def"]), data["spawn"]))

    def post_zone_npcs_edit(self, query, body):
        """{zone, op, ...}: move (guid, def, spawn, to: {x, y, z, h, pitch, roll}), delete (guid, def, spawn) or
        restore (file, line, text: what delete answered) or revert (file, line, now, was: what move answered)."""
        data = json.loads(body)
        scripts, zone, op = self.zone_scripts, data["zone"], data["op"]
        if op == "move":
            result = scripts.move(zone, data["guid"], int(data["def"]), data["spawn"], data["to"])
        elif op == "delete":
            result = scripts.delete(zone, data["guid"], int(data["def"]), data["spawn"])
        elif op == "restore":
            result = scripts.restore(zone, data["file"], int(data["line"]), data["text"])
        elif op == "revert":
            result = scripts.revert(zone, data["file"], int(data["line"]), data["now"], data["was"])
        else:
            raise ValueError(f"Unknown edit {op}.")
        self.send_json(result)

    # --- names (locale strings), environments and seasons ---------------------------------------------------

    def get_names(self, query, body):
        """What string ids say: {id: text or null}."""
        self.send_json(self.workspace.world.texts(query.get("ids", "").split(",")))

    def post_names_new(self, query, body):
        """A Sanctuary string for text (ids from 9500000), in the locale client patch."""
        self.send_json(self.workspace.world.add_name(json.loads(body)["text"]))

    def post_names_set(self, query, body):
        data = json.loads(body)
        self.send_json(self.workspace.world.set_name(int(data["id"]), data["text"]))

    def get_environments(self, query, body):
        """Every tile environment (ObjectTerrainData.xml as players get it), and what a new one can use."""
        world = self.workspace.world
        environments = world.environments()
        self.send_json({"environments": environments, "skies": self.workspace.skies(), "minimaps": world.minimaps(),
                        "atlases": sorted({e["atlas"] for e in environments if e["atlas"]})})

    def post_environments_save(self, query, body):
        self.send_json(self.workspace.world.save_environment(json.loads(body)))

    def get_seasons(self, query, body):
        self.send_json(self.workspace.world.seasons())

    def post_seasons_save(self, query, body):
        self.send_json(self.workspace.world.save_seasons(json.loads(body)))

    def post_live_seed(self, query, body):
        """Live files for a client launch.ps1 is about to start on its own (Workspace.seed_last)."""
        self.send_json(self.workspace.seed_last())

    def post_play(self, query, body):
        data = json.loads(body or b"{}")
        self.send_json(self.workspace.play(bool(data.get("servers")), data.get("zone")))

    def post_zone_eco(self, query, body):
        data = json.loads(body)
        project = self.workspace.project(data["zone"])
        self.send_json(project.add_eco(data.get("sourceZone"), data.get("sourceIndex"), data.get("definition"), data.get("palette")))

    def post_save(self, query, body):
        meta, blobs = unframe(body)
        project = self.workspace.project(meta["zone"])
        saves = []
        blob_index = 0
        for chunk in meta.get("chunks", []):
            parts = {}
            for key in ("heights", "weights", "colour"):
                if chunk.get(key):
                    parts[key] = blobs[blob_index]
                    blob_index += 1
            heights = np.frombuffer(parts["heights"], "<f4").reshape(131, 131) if "heights" in parts else None
            if heights is None:
                raise ValueError(f"chunk {chunk['row']}_{chunk['col']} has no heights")
            weights = None
            if "weights" in parts:
                weights = np.frombuffer(parts["weights"], np.uint8).reshape(512, 512, len(chunk["meta"]["ecos"]))
            colour = None
            if "colour" in parts:
                colour = np.frombuffer(parts["colour"], np.uint8).reshape(1024, 1024, 4)[..., :3]
            saves.append((chunk["row"], chunk["col"], chunk["meta"], heights, weights, colour))
        project.restore_ecos(meta.get("ecos") or [])
        results = project.save_chunks(saves)
        zone = project.save_zone(meta.get("settings") or {})
        live = self.workspace.publish_live(zone["files"])
        self.send_json({"chunks": results, "zone": zone, "live": live})


def serve(workspace: Workspace, host: str, port: int) -> ThreadingHTTPServer:
    Handler.workspace = workspace
    Handler.npcs = NpcDefinitions(workspace)
    Handler.rewards = RewardTables(workspace)
    Handler.zone_scripts = ZoneScripts(workspace)
    Handler.icons = Icons(workspace)
    Handler.store = Store(workspace)
    Handler.combat = Combat(workspace)
    Handler.titles = Titles(workspace)
    Handler.battles = Battles(workspace)
    Handler.client_ids = ClientIds(workspace)
    server = ThreadingHTTPServer((host, port), Handler)
    server.daemon_threads = True
    return server
