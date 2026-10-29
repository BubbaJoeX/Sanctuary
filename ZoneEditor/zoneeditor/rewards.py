"""Sanctuary's reward tables (src/Resources/Rewards.json) for the admin panel's GameData editor.

A table is a key and a weighted list of drops: an item (with a quantity and, optionally, weighted tints) or coins.
Rolling it picks one drop by weight. Mobs (Mobs.json RewardTable), minigames, consumables and the server's own
code roll tables by key, so a table says what uses it and one in use can't be renamed or deleted. Writes keep the
file's layout (write_json) and go to the running servers' copies too; the browser then has the Gateway reload them
(POST /api/resources/rewards/reload).
"""

from __future__ import annotations

import json
import os
import re
import threading
import time
from pathlib import Path

from .workspace import Workspace, write_json

KEY = re.compile(r"[a-z0-9][a-z0-9_.-]{0,63}")
MAX_DROPS = 64
MAX_TINTS = 32
CURRENCIES = ("Coins",)
# Where the game names a reward table: resource files (their "RewardTable"/"RewardTableKey" fields) and code.
USAGE_FILES = ("Mobs.json", "Consumables.json", "ItemAbilities.json")
CODE_SUFFIXES = (".cs", ".lua")
# Not walked for code: build output, resources (data, not code) and tests.
SKIP_FOLDERS = {"bin", "obj", "Resources", "node_modules", ".git"}
CODE_STRING = re.compile(r'"([a-z0-9][a-z0-9_.-]{0,63})"')
CODE_SCAN_SECONDS = 60


class RewardTables:
    def __init__(self, workspace: Workspace):
        self.ws = workspace
        self.path = workspace.resources / "Rewards.json"
        self._lock = threading.Lock()
        self._items: tuple[float, set] | None = None
        self._code: tuple[float, dict] | None = None

    # --- reads -----------------------------------------------------------------------------------------

    def _load(self) -> list:
        return json.loads(self.path.read_text(encoding="utf-8-sig")) if self.path.exists() else []

    def _item_ids(self) -> set:
        """Item definition ids the game has (ClientItemDefinitions.json), read again when the file changes."""
        path = self.ws.resources / "ClientItemDefinitions.json"
        stamp = path.stat().st_mtime
        if self._items is None or self._items[0] != stamp:
            data = json.loads(path.read_text(encoding="utf-8-sig"))
            entries = data.values() if isinstance(data, dict) else data
            self._items = (stamp, {entry["Id"] for entry in entries if isinstance(entry, dict) and "Id" in entry})
        return self._items[1]

    def usage(self) -> dict:
        """key -> what rolls it: "Mobs.json: forest_wolf", "Minigames", "code: PlayerPet.cs"..."""
        keys = {table["Key"] for table in self._load()}
        used: dict[str, list] = {key: [] for key in keys}

        def note(key, where):
            if key in used and where not in used[key]:
                used[key].append(where)

        for name in USAGE_FILES:
            path = self.ws.resources / name
            if path.exists():
                for entry in _entries(json.loads(path.read_text(encoding="utf-8-sig"))):
                    for field in ("RewardTable", "RewardTableKey", "CoinRewardTableKey"):
                        if isinstance(entry.get(field), str):
                            note(entry[field], f"{name}: {_label(entry)}")
        minigames = self.ws.resources / "Minigames"
        for path in minigames.rglob("*.json") if minigames.is_dir() else []:
            text = path.read_text(encoding="utf-8-sig", errors="replace")
            for key in keys:
                if f'"{key}"' in text:
                    note(key, f"Minigames/{path.relative_to(minigames).as_posix()}")
        # Keys written in code as strings ("pet_chest_common").
        for key in keys:
            for name in self._code_strings().get(key, []):
                note(key, f"code: {name}")
        return used

    def _code_strings(self) -> dict:
        """Key-like string literals in the server's code and scripts -> the files they're in (cached a minute)."""
        if self._code is None or time.monotonic() - self._code[0] > CODE_SCAN_SECONDS:
            found: dict[str, list] = {}
            for folder, dirs, files in os.walk(self.ws.root / "src"):
                dirs[:] = [d for d in dirs if d not in SKIP_FOLDERS and not d.endswith(".Tests")]
                for file in files:
                    if file.endswith(CODE_SUFFIXES):
                        text = Path(folder, file).read_text(encoding="utf-8", errors="replace")
                        for literal in set(CODE_STRING.findall(text)):
                            found.setdefault(literal, []).append(file)
            self._code = (time.monotonic(), found)
        return self._code[1]

    def overview(self) -> dict:
        used = self.usage()
        return {"tables": [dict(table, UsedBy=sorted(used.get(table["Key"], []))) for table in self._load()]}

    # --- writes ----------------------------------------------------------------------------------------

    def _check(self, table: dict) -> dict:
        """The table in Rewards.json's own shape, or ValueError."""
        if not isinstance(table, dict):
            raise ValueError("A reward table is an object with a Key and a DropTable.")
        key = str(table.get("Key", "")).strip()
        if not KEY.fullmatch(key):
            raise ValueError("A key is 1-64 lower-case letters, digits, _, - and ., starting with a letter or digit.")
        drops = table.get("DropTable")
        if not isinstance(drops, list) or not drops:
            raise ValueError(f"{key} needs at least one drop.")
        if len(drops) > MAX_DROPS:
            raise ValueError(f"A table has at most {MAX_DROPS} drops.")
        items = self._item_ids()
        checked = []
        for number, drop in enumerate(drops, 1):
            if not isinstance(drop, dict):
                raise ValueError(f"Drop {number} isn't an object.")
            weight = _positive(drop.get("Weight", 1), f"Drop {number}'s weight")
            kind = drop.get("RewardType")
            if kind == "Item":
                item = drop.get("ItemDefinitionId")
                if not isinstance(item, int) or item not in items:
                    raise ValueError(f"Drop {number}: there's no item {item}.")
                entry = {"RewardType": "Item", "ItemDefinitionId": item, "Weight": weight}
                tints = drop.get("Tints") or []
                if not isinstance(tints, list) or len(tints) > MAX_TINTS:
                    raise ValueError(f"Drop {number} has at most {MAX_TINTS} tints.")
                if any(not isinstance(tint, dict) for tint in tints):
                    raise ValueError(f"Drop {number}'s tints are objects with a TintId and a Weight.")
                if tints:
                    entry["Tints"] = [{"TintId": _whole(tint.get("TintId"), f"Drop {number}'s tint"),
                                       "Weight": _positive(tint.get("Weight", 1), f"Drop {number}'s tint weight")}
                                      for tint in tints]
                entry["Quantity"] = _positive(drop.get("Quantity", 1), f"Drop {number}'s quantity", 999)
            elif kind == "Currency":
                currency = drop.get("CurrencyType", "Coins")
                if currency not in CURRENCIES:
                    raise ValueError(f"Drop {number}: the currency is one of {', '.join(CURRENCIES)}.")
                entry = {"RewardType": "Currency", "CurrencyType": currency,
                         "Amount": _positive(drop.get("Amount"), f"Drop {number}'s amount", 10_000_000), "Weight": weight}
            else:
                raise ValueError(f"Drop {number} is an Item or Currency drop.")
            checked.append(entry)
        return {"Key": key, "DropTable": checked}

    def save(self, table: dict, original_key: str | None = None) -> dict:
        """Adds the table, or replaces the one called original_key (renaming it, if it isn't in use)."""
        checked = self._check(table)
        with self._lock:
            tables = self._load()
            index = next((i for i, t in enumerate(tables) if t["Key"] == (original_key or checked["Key"])), None)
            if original_key and index is None:
                raise KeyError(f"reward table {original_key}")
            if (original_key or None) != checked["Key"] and any(t["Key"] == checked["Key"] for t in tables):
                raise ValueError(f"There's already a table called {checked['Key']}.")
            if original_key and original_key != checked["Key"] and (used := self.usage().get(original_key)):
                raise ValueError(f"{original_key} is used by {', '.join(used)}; it can't be renamed.")
            if index is None:
                tables.append(checked)
            else:
                tables[index] = checked
            write_json(self.path, tables)
            mirrored = self.ws.mirror([self.path])
        return {"table": checked, "mirrored": mirrored}

    def delete(self, key: str) -> dict:
        with self._lock:
            tables = self._load()
            kept = [t for t in tables if t["Key"] != key]
            if len(kept) == len(tables):
                raise KeyError(f"reward table {key}")
            if used := self.usage().get(key):
                raise ValueError(f"{key} is used by {', '.join(used)}; it can't be deleted.")
            write_json(self.path, kept)
            mirrored = self.ws.mirror([self.path])
        return {"deleted": key, "mirrored": mirrored}


def _label(entry: dict) -> str:
    """What a resource entry is called: its key, name or comment, else its id."""
    for field in ("Key", "Name", "Comment"):
        if isinstance(entry.get(field), str) and entry[field]:
            return entry[field].split(":")[0][:48]
    return str(entry.get("Id") or entry.get("AbilityId") or "?")


def _entries(data) -> list:
    """Every object in a resource file, however it nests them (Consumables.json has lists by kind)."""
    found = []
    stack = [data]
    while stack:
        value = stack.pop()
        if isinstance(value, dict):
            found.append(value)
            stack.extend(v for v in value.values() if isinstance(v, (dict, list)))
        elif isinstance(value, list):
            stack.extend(v for v in value if isinstance(v, (dict, list)))
    return found


def _positive(value, what: str, top: int = 1_000_000) -> int:
    if not isinstance(value, int) or isinstance(value, bool) or not 1 <= value <= top:
        raise ValueError(f"{what} is a whole number from 1 to {top:,}.")
    return value


def _whole(value, what: str) -> int:
    if not isinstance(value, int) or isinstance(value, bool) or value < 0:
        raise ValueError(f"{what} is a whole number.")
    return value
