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

from .workspace import Workspace

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
        self.send_json(self.workspace.project(query["name"]).meta())

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

    def post_live_seed(self, query, body):
        """Live files for a client launch.ps1 is about to start on its own (Workspace.seed_last)."""
        self.send_json(self.workspace.seed_last())

    def post_play(self, query, body):
        data = json.loads(body or b"{}")
        self.send_json(self.workspace.play(bool(data.get("servers")), data.get("zone"), data.get("cells") or []))

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
    server = ThreadingHTTPServer((host, port), Handler)
    server.daemon_threads = True
    return server
