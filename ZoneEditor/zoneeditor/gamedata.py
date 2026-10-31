"""What the admin panel's content editors (abilities, jobs, titles, battles) share.

- Texts: a record's names are locale string ids (NameId, DescriptionId...). The editors read and write them as text:
  text that a record's string already says keeps its id, and new text becomes one of Sanctuary's own strings
  (LocaleStrings.json, ids from 9500000, through World.add_names), with the locale client patch rebuilt once per save.
  Strings are never changed in place, since one id can name several things.
- The client's ids an editor picks from: animations (AnimationGroups.xml), composite effects
  (ActorCompositeEffectDefinitions.xml, Sanctuary's patch first) and image sets (ImageSets.txt).
- Resource files: read and written whole, in their own layout (write_json), then mirrored to the servers' copies.
"""

from __future__ import annotations

import json
import re
from pathlib import Path

from .workspace import Workspace, write_json

GROUP = re.compile(r'<AnimationGroup\s+name="([^"]*)"\s+id="(\d+)"')
EFFECT = re.compile(r'<EffectDefinition\s+name="([^"]*)"\s+id="(\d+)"')
SANCTUARY_STRINGS = 9500000


class Texts:
    """Records' locale strings as text, for the content editors."""

    def __init__(self, workspace: Workspace):
        self.ws = workspace

    def read(self, records: list, fields: tuple) -> list:
        """{field: text} for each record: what its string ids say (an empty string for none or an unknown id)."""
        ids = {record.get(field) for record in records for field in fields if isinstance(record.get(field), int) and record.get(field) > 0}
        known = self.ws.world.texts(sorted(ids))
        return [{field: known.get(str(record.get(field))) or "" for field in fields} for record in records]

    def apply(self, records: list, texts: list, fields: tuple) -> list:
        """Sets each record's string ids from texts ({field: text}, None leaves the field as it is): text its id
        already says keeps it, empty text is 0, anything else a Sanctuary string. Returns the ids given out."""
        wanted = []
        current = self.read(records, fields)
        for record, given, now in zip(records, texts, current):
            for field in fields:
                text = given.get(field) if given else None
                if text is None:
                    continue
                # Compared as typed text: the game's own strings can carry stray spaces.
                text = " ".join(str(text).split())
                if text == " ".join(now[field].split()) and record.get(field):
                    continue
                if not text:
                    record[field] = 0
                    continue
                if len(text) > 400:
                    raise ValueError(f"{field[:-2] if field.endswith('Id') else field} is at most 400 characters.")
                wanted.append((record, field, text))
        if not wanted:
            return []
        ids, _ = self.ws.world.add_names([text for _, _, text in wanted])
        for (record, field, _), string_id in zip(wanted, ids):
            record[field] = string_id
        return ids


def load(path: Path):
    return json.loads(path.read_text(encoding="utf-8-sig")) if path.exists() else []


def save(ws: Workspace, changed: list) -> list:
    """Writes each (path, data) whose data changed, in the file's layout, and mirrors them: the paths written."""
    written = [path for path, data in changed if write_json(path, data)]
    ws.mirror(written)
    return written


def whole(value, what: str, low: int = 0, high: int = 2_000_000_000) -> int:
    if isinstance(value, bool) or not isinstance(value, (int, float)) or int(value) != value or not low <= value <= high:
        raise ValueError(f"{what} is a whole number from {low:,} to {high:,}.")
    return int(value)


def number(value, what: str, low: float = 0, high: float = 1e9) -> float | int:
    if isinstance(value, bool) or not isinstance(value, (int, float)) or not low <= value <= high:
        raise ValueError(f"{what} is a number from {low:g} to {high:g}.")
    return int(value) if float(value).is_integer() else round(float(value), 4)


def point(value, what: str) -> list:
    if not isinstance(value, list) or len(value) != 3 or any(isinstance(v, bool) or not isinstance(v, (int, float)) for v in value):
        raise ValueError(f"{what} is three numbers: x, y and z.")
    return [round(float(v), 3) for v in value]


def ordered(entry: dict, like: dict | None) -> dict:
    """entry with its keys in like's order (an entry it replaces), new keys after them, and empty values dropped."""
    entry = {k: v for k, v in entry.items() if v is not None}
    if not like:
        return entry
    out = {k: entry[k] for k in like if k in entry}
    out.update(entry)
    return out


class ClientIds:
    """Ids the client knows by name, to pick from: animations, composite effects and image sets."""

    def __init__(self, workspace: Workspace):
        self.ws = workspace
        self._cache: dict[str, tuple] = {}

    def _source(self, name: str) -> Path:
        patch = self.ws.resources / "ClientPatches" / "Resources" / name
        return patch if patch.exists() else self.ws.client_dir / "Resources" / name

    def _rows(self, kind: str) -> list:
        """[(id, name)] for animations, effects or imagesets, read again when its file changes."""
        path = {"animation": self._source("AnimationGroups.xml"), "effect": self._source("ActorCompositeEffectDefinitions.xml"),
                "imageset": self._source("Images/ImageSets.txt")}[kind]
        if not path.exists():
            return []
        stamp = path.stat().st_mtime
        cached = self._cache.get(kind)
        if cached and cached[0] == stamp:
            return cached[1]
        text = path.read_text(encoding="utf-8-sig", errors="replace")
        if kind == "imageset":
            rows = [(int(row[0]), row[1]) for row in (line.split("^") for line in text.splitlines()) if len(row) >= 2 and row[0].isdigit()]
        else:
            pattern = GROUP if kind == "animation" else EFFECT
            rows = [(int(found.group(2)), found.group(1)) for found in pattern.finditer(text)]
        self._cache[kind] = (stamp, rows)
        return rows

    def search(self, kind: str, text: str, limit: int = 60) -> list:
        """[{id, name}] whose name has every word of text, or whose id it is."""
        if kind not in ("animation", "effect", "imageset"):
            raise ValueError("The kind is animation, effect or imageset.")
        rows = self._rows(kind)
        words = text.lower().split()
        if len(words) == 1 and words[0].isdigit():
            found = [row for row in rows if row[0] == int(words[0])]
        else:
            found = [row for row in rows if all(word in row[1].lower() for word in words)]
        return [{"id": i, "name": name} for i, name in found[:limit]]

    def names(self, kind: str, ids: list) -> dict:
        """{id: name} for the ids the client has."""
        wanted = {int(i) for i in ids}
        return {str(i): name for i, name in self._rows(kind) if i in wanted}
