"""Sanctuary's NPC definitions (src/Resources/Npcs.json) for the zone editor.

A definition is what an NPC spawned from it starts as: its model (a Models.txt row's id, and the actor file),
name, scale, texture and tint aliases, named scripts (src/Scripts/Npc), overhead icons, interaction and merchant
lists, and what it says when clicked (a conversation, which a placed NPC's own replaces). The editor lists them, changes any of them and adds new ones, for example for an actor spawned as an NPC.
Writes keep the file's layout (write_json) and go to the running Gateway's copy too; the browser then has the
Gateway reload them (POST /api/resources/npcs/reload: its file watcher only picks up new ids) and respawn the
zone's NPCs built on a changed one.
"""

from __future__ import annotations

import json
import re
import sqlite3
import threading

from .workspace import Workspace, write_json

# New definitions' ids: stock ones stop at 45000 (the "Placed model" NPC); fr_maya_mcp's start at 900000.
EDITOR_FLOOR = 50000
PLACED_MODEL = 45000
MAX_NAME = 64
# The game's conversation limits (Sanctuary.Game.Conversations.Conversation).
MAX_LINES = 64
MAX_ANSWERS = 8
MAX_TEXT = 400
# Fields the browser's form edits; anything else on an entry (interaction and merchant lists, abilities...)
# is edited as JSON and kept as it is.
SUMMARY_KEYS = ("Id", "Name", "ModelFileName")


class NpcDefinitions:
    def __init__(self, workspace: Workspace):
        self.ws = workspace
        self.path = workspace.resources / "Npcs.json"
        self._lock = threading.Lock()

    # --- reads -----------------------------------------------------------------------------------------

    def _load(self) -> list:
        return json.loads(self.path.read_text(encoding="utf-8-sig"))

    def _models(self) -> tuple[dict, dict]:
        """Models.txt: lowercase actor file -> its first row's id, and id -> actor file."""
        by_file, by_id = {}, {}
        for line in (self.ws.resources / "Models.txt").read_text(encoding="latin-1").splitlines():
            row = line.split("^")
            if len(row) < 2 or not row[0].isdigit():
                continue
            by_file.setdefault(row[1].strip().lower(), int(row[0]))
            by_id[int(row[0])] = row[1].strip()
        return by_file, by_id

    def _placed(self) -> dict:
        """How many NPCs placed in the world (the database's WorldNpcs) each definition id has."""
        database = self.ws.root / "run" / "sanctuary.db"
        if not database.exists():
            return {}
        connection = sqlite3.connect(f"file:{database}?mode=ro", uri=True, timeout=5)
        try:
            return dict(connection.execute("SELECT Definition, COUNT(*) FROM WorldNpcs GROUP BY Definition").fetchall())
        finally:
            connection.close()

    def scripts(self) -> list:
        """The named scripts a definition can run (src/Scripts/Npc/<name>.lua)."""
        folder = self.ws.root / "src" / "Scripts" / "Npc"
        return sorted(path.stem for path in folder.glob("*.lua")) if folder.is_dir() else []

    @staticmethod
    def summary(entry: dict, placed: int = 0) -> dict:
        return {
            "id": entry["Id"], "name": entry.get("Name"), "model": entry.get("ModelFileName") or None,
            "modelId": entry.get("ModelId"), "scripts": entry.get("Scripts") or [],
            "placed": bool(entry.get("IsPlacedObject")), "merchant": "MerchantList" in entry,
            "interactions": "InteractionList" in entry, "editorMade": entry["Id"] >= EDITOR_FLOOR, "placedInWorld": placed,
        }

    def search(self, query: str = "", limit: int = 200, model: str | None = None) -> dict:
        """Definitions matching every word of query (id, name or actor), or every one for an actor."""
        words = query.lower().split()
        placed = self._placed()
        found = []
        for entry in self._load():
            if model is not None and (entry.get("ModelFileName") or "").lower() != model.lower():
                continue
            text = f"{entry['Id']} {entry.get('Name') or ''} {entry.get('ModelFileName') or ''}".lower()
            if all(word in text for word in words):
                found.append(self.summary(entry, placed.get(entry["Id"], 0)))
        # Named ones first, then by id.
        found.sort(key=lambda e: (e["name"] is None, (e["name"] or "").lower(), e["id"]))
        return {"total": len(found), "definitions": found[:limit]}

    def get(self, definition_id: int) -> dict:
        for entry in self._load():
            if entry["Id"] == definition_id:
                return entry
        raise KeyError(f"NPC {definition_id}")

    # --- writes ----------------------------------------------------------------------------------------

    def _write(self, entries: list) -> list:
        write_json(self.path, entries)
        return self.ws.mirror([self.path])

    def _check(self, entry: dict) -> dict:
        """The entry made consistent, or ValueError: its model resolved from Models.txt, empty fields dropped."""
        if not isinstance(entry, dict) or not isinstance(entry.get("Id"), int):
            raise ValueError("A definition is an object with an Id.")
        by_file, by_id = self._models()
        model = (entry.get("ModelFileName") or "").strip()
        if entry["Id"] != PLACED_MODEL:
            if model:
                # The actor decides: its Models.txt row is what the client draws for the id.
                model_id = by_file.get(model.lower())
                if model_id is None:
                    raise ValueError(f"{model} has no Models.txt row (ZoneEditor/add_model_rows.py adds them).")
                if by_id.get(entry.get("ModelId"), "").lower() != model.lower():
                    entry["ModelId"] = model_id
            elif entry.get("ModelId") not in by_id:
                raise ValueError("Give the definition an actor (ModelFileName).")
            else:
                entry["ModelFileName"] = by_id[entry["ModelId"]]
        name = entry.get("Name")
        if name is not None and (not isinstance(name, str) or len(name.strip()) > MAX_NAME):
            raise ValueError(f"Names are text of up to {MAX_NAME} characters.")
        scale = entry.get("Scale")
        if scale is not None and not (isinstance(scale, (int, float)) and 0 < scale <= 20):
            raise ValueError("The scale is above 0 and at most 20.")
        scripts = entry.get("Scripts")
        if scripts is not None:
            known = set(self.scripts())
            if not isinstance(scripts, list) or any(script not in known for script in scripts):
                raise ValueError(f"Scripts are names from src/Scripts/Npc: {', '.join(sorted(known))}.")
        # An emptied name, alias or script list goes; other empty values (the placed model's actor) stay.
        for key in ("Name", "TextureAlias", "TintAlias"):
            if isinstance(entry.get(key), str):
                entry[key] = entry[key].strip() or None
        if entry.get("Scripts") == []:
            entry["Scripts"] = None
        if entry.get("Conversation") is not None:
            entry["Conversation"] = self._check_conversation(entry["Conversation"])
        return {key: value for key, value in entry.items() if value is not None}

    def _check_conversation(self, conversation) -> dict:
        """A definition's conversation as the game checks it (Conversation.Validate), and the items its answers give,
        in Npcs.json's shape; or ValueError."""
        if not isinstance(conversation, dict) or not isinstance(conversation.get("Nodes"), list):
            raise ValueError("A conversation has a Start and a list of Nodes (lines).")
        nodes = conversation["Nodes"]
        if not 1 <= len(nodes) <= MAX_LINES:
            raise ValueError(f"A conversation has 1 to {MAX_LINES} lines.")
        ids = [node.get("Id") if isinstance(node, dict) else None for node in nodes]
        if len(set(ids)) != len(ids) or any(not isinstance(i, int) or isinstance(i, bool) or i <= 0 for i in ids):
            raise ValueError("Each line of the conversation needs its own id above 0.")
        start = conversation.get("Start", 1)
        if start not in ids:
            raise ValueError(f"The conversation starts at line {start}, which isn't there.")
        items = self.ws.item_ids()
        checked = []
        for node in nodes:
            line = node["Id"]
            text = node.get("Text")
            if not isinstance(text, str) or not text.strip() or len(text) > MAX_TEXT:
                raise ValueError(f"Line {line} needs text of up to {MAX_TEXT} characters.")
            choices = node.get("Choices") or []
            if not isinstance(choices, list) or len(choices) > MAX_ANSWERS:
                raise ValueError(f"Line {line} has at most {MAX_ANSWERS} answers.")
            answers = []
            for choice in choices:
                answer = choice.get("Text") if isinstance(choice, dict) else None
                if not isinstance(answer, str) or not answer.strip() or len(answer) > MAX_TEXT:
                    raise ValueError(f"Each answer to line {line} needs text of up to {MAX_TEXT} characters.")
                following = choice.get("Next") or 0
                if following != 0 and following not in ids:
                    raise ValueError(f"An answer to line {line} leads to line {following}, which isn't there.")
                checked_choice = {"Text": answer.strip(), "Next": following}
                if item := choice.get("GiveItemId") or 0:
                    quantity = choice.get("GiveQuantity") or 1
                    if item not in items:
                        raise ValueError(f"An answer to line {line} gives item {item}, which the game doesn't have.")
                    if not isinstance(quantity, int) or not 1 <= quantity <= 100:
                        raise ValueError(f"An answer to line {line} gives 1 to 100 of its item.")
                    checked_choice.update(GiveItemId=item, GiveQuantity=quantity)
                if isinstance(choice.get("Hook"), str) and choice["Hook"].strip():
                    checked_choice["Hook"] = choice["Hook"].strip()
                answers.append(checked_choice)
            checked.append({"Id": line, "Text": text.strip(), "Choices": answers})
        return {"Start": start, "Nodes": checked}

    def save(self, entry: dict) -> dict:
        """Replaces the definition with entry's Id."""
        with self._lock:
            entries = self._load()
            index = next((i for i, e in enumerate(entries) if e["Id"] == entry.get("Id")), None)
            if index is None:
                raise KeyError(f"NPC {entry.get('Id')}")
            checked = self._check(dict(entry))
            # Keep the stock key order, with any new keys after.
            ordered = {key: checked[key] for key in entries[index] if key in checked}
            ordered.update(checked)
            entries[index] = ordered
            mirrored = self._write(entries)
        return {"definition": ordered, "mirrored": mirrored}

    def create(self, model: str, name: str | None = None, copy_of: int | None = None) -> dict:
        """A new definition for an actor (or a copy of another's, on another actor), with the next editor id."""
        with self._lock:
            entries = self._load()
            # Never an id a placed NPC still names, even if its definition was deleted.
            ids = [e["Id"] for e in entries] + list(self._placed())
            new_id = max([EDITOR_FLOOR - 1] + [i for i in ids if EDITOR_FLOOR <= i < 900000]) + 1
            base = dict(next(e for e in entries if e["Id"] == copy_of)) if copy_of is not None else {}
            if copy_of is not None and not base:
                raise KeyError(f"NPC {copy_of}")
            stem = re.sub(r"\.a[dg]r$", "", model, flags=re.IGNORECASE)
            entry = {"Id": new_id, "Name": (name or base.get("Name") or stem).strip(), "ModelFileName": model}
            entry.update({k: v for k, v in base.items() if k not in ("Id", "Name", "NameId", "ModelId", "ModelFileName")})
            checked = self._check(entry)
            ordered = {"Id": new_id, "Name": checked["Name"], "ModelId": checked["ModelId"], "ModelFileName": checked["ModelFileName"]}
            ordered.update(checked)
            entries.append(ordered)
            mirrored = self._write(entries)
        return {"definition": ordered, "mirrored": mirrored}

    def delete(self, definition_id: int) -> dict:
        """Removes one of the editor's own definitions; stock ones stay (quests and zones name them)."""
        if definition_id < EDITOR_FLOOR:
            raise PermissionError("Only definitions the editor made (ids from 50000) can be deleted.")
        if placed := self._placed().get(definition_id):
            raise ValueError(f"{placed} NPC(s) placed in the world are built on it; delete them first.")
        with self._lock:
            entries = self._load()
            kept = [e for e in entries if e["Id"] != definition_id]
            if len(kept) == len(entries):
                raise KeyError(f"NPC {definition_id}")
            mirrored = self._write(kept)
        return {"deleted": definition_id, "mirrored": mirrored}
