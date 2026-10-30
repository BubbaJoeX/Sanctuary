"""The zone's own NPCs as its Lua scripts spawn them, for the zone editor.

A zone's definition (src/Resources/Zones/<zone>.json) lists its scripts, src/Scripts/Zone/<name>.lua, and they
spawn its NPCs at startup with lines like

    zone:spawnNpcWithGuid(1186, 100000001186, -1295.67, -56.80742, 830.2055, 2.336771)
    zone:spawnNpc(36313, -1469.737671, -17.296091, 441.169098, -1.411822, -1.545092, -2.515851)

(definition, [guid,] x, y, z, heading[, pitch, roll]). The editor finds an NPC's line (by guid, or by definition and
where it spawned), rewrites its numbers when the NPC is moved or turned, and takes the line out when it's deleted or
made a world NPC; the Gateway reads these files directly, so they take effect the next time the zone starts. Lines
whose numbers are worked out in code (variables, tables) aren't touched.
"""

from __future__ import annotations

import re
import threading
from pathlib import Path

from .workspace import Workspace

SPAWN = re.compile(r"^(?P<before>.*?)\b(?P<zone>[A-Za-z_]\w*):(?P<fn>spawnNpcWithGuid|spawnNpc)\((?P<args>[^()]*)\)(?P<after>.*)$")
NUMBER = re.compile(r"^-?\d+(?:\.\d+)?(?:[eE][-+]?\d+)?$")
# A spawn line's position, as floats survive the trip through the game (float32) and its JSON.
CLOSE = 0.02


def _number(token: str) -> float | None:
    token = token.strip().strip("\"'")
    return float(token) if NUMBER.match(token) else None


def _format(value: float, places: int) -> str:
    text = f"{value:.{places}f}".rstrip("0").rstrip(".")
    return "0" if text in ("-0", "") else text


class ZoneScripts:
    def __init__(self, workspace: Workspace):
        self.ws = workspace
        self._lock = threading.Lock()

    def files(self, zone: str) -> list[Path]:
        """The zone's scripts, in the order it runs them."""
        definition = self.ws.definitions().get(zone.lower())
        if not definition:
            raise KeyError(f"zone {zone}")
        folder = self.ws.root / "src" / "Scripts" / "Zone"
        return [folder / f"{name}.lua" for name in definition[1].get("Scripts") or [] if (folder / f"{name}.lua").is_file()]

    @staticmethod
    def _read(path: Path) -> tuple[list[str], str, bool]:
        raw = path.read_bytes()
        bom = raw.startswith(b"\xef\xbb\xbf")
        text = raw.decode("utf-8-sig")
        newline = "\r\n" if "\r\n" in text else "\n"
        return text.split(newline), newline, bom

    @staticmethod
    def _write(path: Path, lines: list[str], newline: str, bom: bool):
        path.write_bytes((b"\xef\xbb\xbf" if bom else b"") + newline.join(lines).encode("utf-8"))

    @staticmethod
    def _parse(line: str) -> dict | None:
        match = SPAWN.match(line)
        if not match:
            return None
        tokens = [token.strip() for token in match["args"].split(",")]
        guided = match["fn"] == "spawnNpcWithGuid"
        numbers = [_number(token) for token in tokens]
        if len(tokens) < (6 if guided else 5) or any(value is None for value in numbers):
            return None
        rest = numbers[2:] if guided else numbers[1:]
        return {
            "match": match, "tokens": tokens, "def": int(numbers[0]),
            "guid": tokens[1].strip("\"'") if guided else None,
            "x": rest[0], "y": rest[1], "z": rest[2], "h": rest[3],
            "pitch": rest[4] if len(rest) > 5 else 0.0, "roll": rest[5] if len(rest) > 5 else 0.0,
        }

    def find(self, zone: str, guid: str, definition: int, spawn: dict) -> dict:
        """The NPC's spawn line: {found, file, line (1-based), text, deletable} or {found: False, scripts}."""
        files = self.files(zone)
        best = None
        for path in files:
            lines, _, _ = self._read(path)
            for index, line in enumerate(lines):
                if "spawnNpc" not in line or str(definition) not in line:
                    continue
                parsed = self._parse(line)
                if not parsed or parsed["def"] != definition:
                    continue
                if parsed["guid"] is not None:
                    if parsed["guid"] != str(guid):
                        continue
                    score = -1.0
                elif all(abs(parsed[k] - float(spawn[k])) <= CLOSE for k in "xyz"):
                    score = abs(parsed["h"] - float(spawn.get("h", parsed["h"])))
                else:
                    continue
                if best is None or score < best[0]:
                    best = (score, path, index, line, parsed)
        if not best:
            return {"found": False, "scripts": [path.name for path in files]}
        _, path, index, line, parsed = best
        match = parsed["match"]
        # Only a line that's just the call can go; one that keeps the NPC in a variable has code using it.
        after = match["after"].strip().removeprefix(";").strip()
        deletable = not match["before"].strip() and (not after or after.startswith("--"))
        return {"found": True, "file": path.name, "line": index + 1, "text": line.strip(), "deletable": deletable,
                "path": path, "index": index, "parsed": parsed}

    @staticmethod
    def _public(found: dict) -> dict:
        return {key: value for key, value in found.items() if key not in ("path", "index", "parsed")}

    def locate(self, zone: str, guid: str, definition: int, spawn: dict) -> dict:
        return self._public(self.find(zone, guid, definition, spawn))

    def _require(self, zone: str, guid: str, definition: int, spawn: dict) -> dict:
        found = self.find(zone, guid, definition, spawn)
        if not found["found"]:
            scripts = ", ".join(found["scripts"]) or "none"
            raise ValueError(f"Couldn't find the line that spawns NPC {definition} there in the zone's scripts ({scripts}).")
        return found

    def move(self, zone: str, guid: str, definition: int, spawn: dict, to: dict) -> dict:
        """Rewrites the NPC's spawn line to start it at to (x, y, z, h, pitch, roll)."""
        with self._lock:
            found = self._require(zone, guid, definition, spawn)
            lines, newline, bom = self._read(found["path"])
            parsed = found["parsed"]
            match = parsed["match"]
            tokens = parsed["tokens"]
            head = tokens[:2] if parsed["guid"] is not None else tokens[:1]
            old = tokens[len(head):]

            # A number that didn't change keeps how the script wrote it (the game holds them as float32s).
            def number(i: int, key: str, places: int) -> str:
                value = float(to.get(key, 0))
                if i < len(old) and abs(parsed[key] - value) <= 0.6 * 10 ** -places + abs(value) * 2.5e-7:
                    return old[i]
                return _format(value, places)

            numbers = [number(0, "x", 4), number(1, "y", 4), number(2, "z", 4), number(3, "h", 6)]
            if float(to.get("pitch", 0)) or float(to.get("roll", 0)) or len(old) > 4:
                numbers += [number(4, "pitch", 6), number(5, "roll", 6)]
            text = f"{match['before']}{match['zone']}:{match['fn']}({', '.join(head + numbers)}){match['after']}"
            was = lines[found["index"]]
            lines[found["index"]] = text
            self._write(found["path"], lines, newline, bom)
            return {"file": found["file"], "line": found["line"], "text": text.strip(), "now": text, "was": was}

    def revert(self, zone: str, file: str, line: int, now: str, was: str) -> dict:
        """Undoes a move: puts back the line as it was, if it's still as the move left it."""
        with self._lock:
            path = next((path for path in self.files(zone) if path.name == file), None)
            if path is None:
                raise KeyError(f"script {file}")
            lines, newline, bom = self._read(path)
            index = int(line) - 1
            if not 0 <= index < len(lines) or lines[index] != now:
                raise ValueError(f"{file} line {line} changed since; it wasn't put back.")
            lines[index] = was
            self._write(path, lines, newline, bom)
            return {"file": file, "line": line, "text": was.strip()}

    def delete(self, zone: str, guid: str, definition: int, spawn: dict) -> dict:
        """Takes the NPC's spawn line out; the answer is what restore puts back."""
        with self._lock:
            found = self._require(zone, guid, definition, spawn)
            if not found["deletable"]:
                raise ValueError(f"{found['file']} line {found['line']} keeps this NPC for more code, so it isn't taken out here: "
                                 "edit the script to remove it.")
            lines, newline, bom = self._read(found["path"])
            removed = lines.pop(found["index"])
            self._write(found["path"], lines, newline, bom)
            return {"file": found["file"], "line": found["line"], "text": removed}

    def restore(self, zone: str, file: str, line: int, text: str) -> dict:
        """Puts a deleted spawn line back where it was."""
        with self._lock:
            path = next((path for path in self.files(zone) if path.name == file), None)
            if path is None:
                raise KeyError(f"script {file}")
            if not self._parse(text):
                raise ValueError("That isn't a spawn line.")
            lines, newline, bom = self._read(path)
            index = min(max(int(line) - 1, 0), len(lines))
            lines.insert(index, text)
            self._write(path, lines, newline, bom)
            return {"file": file, "line": index + 1, "text": text.strip()}
