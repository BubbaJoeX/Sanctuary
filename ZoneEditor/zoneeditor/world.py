"""World data the zone editor edits beside a zone's own files.

- Names: the locale strings a NameId shows (ClientMods/locale_strings.py). New names are Sanctuary's own
  (src/Resources/LocaleStrings.json, ids from 9500000) and reach clients in the locale client patch.
- Environments: the client's Resources/ObjectTerrainData.xml, one row per environment id a tile can have (its sky,
  minimap tile set and atlas page). New and changed rows go out as a client patch
  (src/Resources/ClientPatches/Resources/ObjectTerrainData.xml); the client reads it when it starts.
- Points of interest: src/Resources/PointOfInterests.json, the map's places, among them the warp destinations
  (hubs) a warpstone's atlas offers. The Gateway sends them to players as they enter a zone.
"""

from __future__ import annotations

import json
import re
import threading
from pathlib import Path

from locale_strings import LocaleStrings

ENVIRONMENTS_FILE = Path("Resources") / "ObjectTerrainData.xml"
ENVIRONMENTS_PATCH = Path("ClientPatches") / ENVIRONMENTS_FILE
ENVIRONMENT_FIELDS = ("Id", "DataName", "SkyFileName", "MinimapTileset", "RoomType", "AtlasName")
ROW = re.compile(r'<ObjectTerrainData\b([^>]*?)\s*/>')
# A warp destination as the game's own are: the warpstone icon, "Warpstone" under its name.
HUB = {"IconId": 1345, "NotificationType": 7, "SubNameId": 382845, "Unknown": 651, "Unknown2": 30}
NOTIFICATION_TYPES = {7: "warpstone (hub)", 6: "place", 3: "dungeon", 10: "ruins"}
STARTING_ZONE = 1   # a point of interest without a ZoneId is in the starting zone (PacketZoneTeleportRequestHandler)


class World:
    def __init__(self, workspace):
        self.ws = workspace
        self.names = LocaleStrings(workspace.client_dir, workspace.resources)
        self.lock = threading.Lock()

    # --- names -----------------------------------------------------------------------------------------

    def texts(self, ids) -> dict:
        return {str(i): self.names.text(int(i)) for i in ids if str(i).lstrip("-").isdigit()}

    def add_name(self, text: str) -> dict:
        """A Sanctuary string for text (the one that says it already, if any), in the locale patch."""
        with self.lock:
            string_id = self.names.add(text)
            written = self.names.build_patch()
        return {"id": string_id, "text": self.names.text(string_id), "mirrored": self.ws.mirror(written)}

    def add_names(self, texts: list) -> tuple[list, list]:
        """Sanctuary strings for several texts at once, the locale patch rebuilt once: their ids, and the copies
        mirrored to the servers."""
        with self.lock:
            ids = [self.names.add(text) for text in texts]
            written = self.names.build_patch() if ids else []
        return ids, self.ws.mirror(written)

    def set_name(self, string_id: int, text: str) -> dict:
        with self.lock:
            self.names.set(string_id, text)
            written = self.names.build_patch()
        return {"id": string_id, "text": self.names.text(string_id), "mirrored": self.ws.mirror(written)}

    # --- environments ----------------------------------------------------------------------------------

    def _environments_path(self) -> Path:
        patch = self.ws.resources / ENVIRONMENTS_PATCH
        return patch if patch.exists() else self.ws.client_dir / ENVIRONMENTS_FILE

    def environments(self) -> list:
        """Every environment as players get it: {id, name, sky, minimap, atlas, roomType, stock}."""
        path = self._environments_path()
        if not path.exists():
            return []
        stock = {row["Id"] for row in self._rows(self._stock_text())}
        return [{"id": int(row["Id"]), "name": row.get("DataName", ""), "sky": row.get("SkyFileName", ""),
                 "minimap": row.get("MinimapTileset", ""), "atlas": row.get("AtlasName", ""),
                 "roomType": int(row.get("RoomType") or 0), "stock": row["Id"] in stock}
                for row in self._rows(path.read_text(encoding="utf-8-sig", errors="replace")) if "Id" in row]

    def _stock_text(self) -> str:
        path = self.ws.client_dir / ENVIRONMENTS_FILE
        backup = path.with_name(path.name + ".sanctuary-backup")
        source = backup if backup.exists() else path
        return source.read_text(encoding="utf-8-sig", errors="replace") if source.exists() else ""

    @staticmethod
    def _rows(text: str) -> list:
        return [dict(re.findall(r'(\w+)="([^"]*)"', match.group(1))) for match in ROW.finditer(text)]

    def minimaps(self) -> list:
        """Minimap tile sets the client has (<name>_TileInfo.txt)."""
        return sorted({name[: -len("_tileinfo.txt")] for name in self.ws.assets.names("_tileinfo.txt")})

    def save_environment(self, entry: dict) -> dict:
        """Adds an environment (no id: the next one) or changes one, in the ObjectTerrainData client patch."""
        name = str(entry.get("name", "")).strip()
        if not re.fullmatch(r"[A-Za-z][A-Za-z0-9_]{1,47}", name):
            raise ValueError("An environment's name is 2-48 letters, digits and _, starting with a letter.")
        sky = str(entry.get("sky", "")).strip()
        if sky and sky.lower() not in {s.lower() for s in self.ws.skies()}:
            raise ValueError(f"There's no sky {sky}.")
        with self.lock:
            path = self._environments_path()
            # Its own CRLF line ends, which the client reads its tables by.
            with path.open(encoding="utf-8-sig", errors="replace", newline="") as f:
                text = f.read()
            rows = self._rows(text)
            ids = [int(row["Id"]) for row in rows if row.get("Id", "").isdigit()]
            env_id = int(entry["id"]) if entry.get("id") not in (None, "") else max(ids, default=0) + 1
            values = {"Id": str(env_id), "DataName": name, "SkyFileName": sky,
                      "MinimapTileset": str(entry.get("minimap", "")).strip(), "RoomType": "0",
                      "AtlasName": str(entry.get("atlas", "")).strip() or "sacredgrove"}
            line = "<ObjectTerrainData " + " ".join(f'{key}="{values[key]}"' for key in ENVIRONMENT_FIELDS
                                                    if values[key] or key in ("Id", "DataName", "RoomType")) + " />"
            existing = next((m for m in ROW.finditer(text) if re.search(rf'\bId="{env_id}"', m.group(1))), None)
            if existing:
                text = text[: existing.start()] + line + text[existing.end():]
            else:
                text = text.rstrip("\r\n") + "\r\n" + line
            target = self.ws.resources / ENVIRONMENTS_PATCH
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_text(text, encoding="utf-8", newline="")
        return {"id": env_id, "environments": self.environments(), "mirrored": self.ws.mirror([target])}

    def environment_names(self) -> dict:
        return {str(e["id"]): e["name"] for e in self.environments()}

    # --- seasons ---------------------------------------------------------------------------------------

    def _seasons_path(self) -> Path:
        return self.ws.resources / "Seasons.json"

    def seasons(self) -> list:
        """Seasonal holidays (Resources/Seasons.json): event keys that are on between two days of the year."""
        path = self._seasons_path()
        return json.loads(path.read_text(encoding="utf-8-sig")) if path.exists() else []

    def save_seasons(self, seasons: list) -> dict:
        from .workspace import write_json

        keys = set()
        for season in seasons:
            key = str(season.get("Key", "")).strip().lower()
            if not re.fullmatch(r"[a-z0-9_-]{1,32}", key) or key in keys:
                raise ValueError("Each season has its own key of 1-32 letters, digits, _ and -.")
            keys.add(key)
            season["Key"] = key
            season["Name"] = str(season.get("Name") or key).strip()
            for field, top in (("StartMonth", 12), ("StartDay", 31), ("EndMonth", 12), ("EndDay", 31)):
                if not isinstance(season.get(field), int) or not 1 <= season[field] <= top:
                    raise ValueError(f"{season['Name']}'s {field} is 1 to {top}.")
        path = self._seasons_path()
        with self.lock:
            write_json(path, seasons)
        return {"seasons": seasons, "mirrored": self.ws.mirror([path])}

    # --- points of interest ----------------------------------------------------------------------------

    def _poi_path(self) -> Path:
        return self.ws.resources / "PointOfInterests.json"

    def pois(self, zone_id: int | None) -> list:
        """The zone's points of interest (those without a ZoneId are the starting zone's)."""
        path = self._poi_path()
        if zone_id is None or not path.exists():
            return []
        rows = json.loads(path.read_text(encoding="utf-8-sig"))
        return [row for row in rows if "Position" in row and (row.get("ZoneId") or STARTING_ZONE) == zone_id]

    def save_pois(self, zone_id: int, rows: list, deleted: list) -> Path | None:
        """Writes the zone's points of interest: rows replace theirs (by Id) or are added, deleted ones go."""
        from .workspace import write_json

        path = self._poi_path()
        with self.lock:
            existing = json.loads(path.read_text(encoding="utf-8-sig"))
            # New ones (no Id yet) get the next ids.
            next_id = max([0] + [row["Id"] for row in existing] + [row["Id"] for row in rows if isinstance(row.get("Id"), int)]) + 1
            for row in rows:
                if row.get("Id") is None:
                    row["Id"], next_id = next_id, next_id + 1
            # Rows as they were stay byte for byte; only new and changed ones are checked.
            before = {row["Id"]: row for row in existing}
            ours = {int(row["Id"]): row if before.get(row["Id"]) == row else self._check_poi(row, zone_id) for row in rows}
            gone = {int(i) for i in deleted}
            updated = [ours.pop(row["Id"], row) for row in existing if row["Id"] not in gone]
            updated += [ours[i] for i in sorted(ours)]
            return path if write_json(path, updated) else None

    def new_poi_id(self) -> int:
        path = self._poi_path()
        return max([0] + [row["Id"] for row in json.loads(path.read_text(encoding="utf-8-sig"))]) + 1

    @staticmethod
    def _check_poi(row: dict, zone_id: int) -> dict:
        if not isinstance(row.get("Id"), int) or not isinstance(row.get("NameId"), int):
            raise ValueError("A point of interest needs an Id and a NameId.")
        for key in ("Position", "SpawnPosition"):
            if not (isinstance(row.get(key), list) and len(row[key]) == 3 and all(isinstance(v, (int, float)) for v in row[key])):
                raise ValueError(f"A point of interest's {key} is [x, y, z].")
        if not isinstance(row.get("Heading", 0), (int, float)):
            raise ValueError("A point of interest's Heading is a number (radians).")
        row = {key: value for key, value in row.items() if value is not None}
        if zone_id != STARTING_ZONE:
            row["ZoneId"] = zone_id
        return row
