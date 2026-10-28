#!/usr/bin/env python3
"""Sanctuary's own locale strings, and the locale client patch that carries them.

The client shows a point of interest's, an area's or a warpstone's name only by string id (a NameId), out of
its locale: Locale/en_us_data.dir lists each entry's key (lookup2 of "Global.Text.<id>"), offset and length in
en_us_data.dat, whose entries are "<key>\\tucdt\\t<text>", sorted by key, one CRLF after each. New names are
Sanctuary's: src/Resources/LocaleStrings.json ({"<id>": "text"}, ids from 9500000; fr_maya_mcp's start at
9100000). They reach clients as the locale client patch (src/Resources/ClientPatches/locale), which launchers
download before Play and launch.ps1 puts in a local client; the client reads the locale when it starts.

Rebuilding takes the patch as it is (the stock locale, and fr_maya_mcp's strings when it wrote it) and sets every
Sanctuary string in it, laid out as the stock files are: the client walks the two files together, so the
offsets, Count and MD5Checksum have to agree and the stock entries stay byte for byte.

    python locale_strings.py --client "%LOCALAPPDATA%/OSFRLauncher/Servers/OSFR Public Server/Client"
"""

from __future__ import annotations

import hashlib
import json
from argparse import ArgumentParser
from pathlib import Path

from extract_names import lookup2

script_dir = Path(__file__).resolve().parent
resources_dir = script_dir.parent / "src" / "Resources"

LOCALE = "en_us"
FIRST_ID = 9500000
STRINGS_FILE = "LocaleStrings.json"
PATCH_DIR = Path("ClientPatches") / "locale"
# What the client was before anything patched it (fr_maya_mcp's backup, launch.ps1's), else what it has.
BACKUPS = (".fr_maya_mcp-backup", ".sanctuary-backup")


def string_key(string_id: int) -> int:
    return lookup2(f"Global.Text.{string_id}".encode())


def _parse(dat: bytes, dir_: bytes):
    """(.dir header lines, rows (key, offset, length, flag) in file order, where the first entry starts)."""
    lines = dir_.decode("utf-8").split("\r\n")
    header = [line for line in lines if line.startswith("##")]
    rows = []
    for line in lines:
        if line and not line.startswith("#"):
            key, offset, length, flag = line.split("\t")
            rows.append((int(key), int(offset), int(length), flag))
    return header, rows, rows[0][1] if rows else 0


def _layout(dat: bytes, dir_: bytes, strings: dict[int, bytes]) -> tuple[bytes, bytes]:
    """The locale with strings ({key: whole entry}) set, as the stock files are laid out: every entry sorted by key
    after the .dat's BOM, the .dir listing the same entries in the same order, Count, MD5Checksum and TextLength
    right."""
    header, rows, start = _parse(dat, dir_)
    entries = {key: (dat[offset:offset + length], flag) for key, offset, length, flag in rows}
    for key, line in strings.items():
        entries[key] = (line, entries.get(key, (None, "d"))[1])
    out = bytearray(dat[:start])
    out_rows = []
    for key in sorted(entries):
        line, flag = entries[key]
        out_rows.append(f"{key}\t{len(out)}\t{len(line)}\t{flag}")
        out += line + b"\r\n"
    longest = max((len(line.split(b"\t", 2)[2]) for line, _ in entries.values() if line.count(b"\t") >= 2), default=0)
    new_header = []
    for line in header:
        name = line[3:].split(":", 1)[0]
        if name == "Count":
            line = f"## Count:\t{len(out_rows)}"
        elif name == "MD5Checksum":
            line = f"## MD5Checksum:\t{hashlib.md5(bytes(out)).hexdigest().upper()}"
        elif name == "TextLength":
            line = f"## TextLength:\t{max(int(line.split(chr(9))[-1]), longest)}"
        new_header.append(line)
    return bytes(out), ("\r\n".join(new_header + out_rows) + "\r\n").encode("utf-8")


def _check(stock: tuple[bytes, bytes], new: tuple[bytes, bytes]):
    """Refuses a locale the client would misread, or one that changed a stock string."""
    (dat, dir_), (new_dat, new_dir) = stock, new
    header, rows, start = _parse(new_dat, new_dir)
    fields = {line[3:].split(":", 1)[0]: line.split("\t")[-1] for line in header}
    problems = []
    if int(fields.get("Count", -1)) != len(rows):
        problems.append("Count doesn't match the rows")
    if fields.get("MD5Checksum", "").upper() != hashlib.md5(new_dat).hexdigest().upper():
        problems.append("MD5Checksum isn't the .dat's")
    expected = start
    for i, (key, offset, length, _) in enumerate(rows):
        if i and key <= rows[i - 1][0] or offset != expected or not new_dat.startswith(f"{key}\t".encode(), offset):
            problems.append(f"row {i} (key {key}) is out of order or doesn't point at its entry")
            break
        expected = offset + length + 2
    if expected != len(new_dat):
        problems.append("the .dat has bytes after its last entry")
    entries = {key: new_dat[offset:offset + length] for key, offset, length, _ in rows}
    lost = [key for key, offset, length, _ in _parse(dat, dir_)[1] if entries.get(key) != dat[offset:offset + length]]
    if lost:
        problems.append(f"{len(lost)} stock strings would change or go missing")
    if problems:
        raise ValueError("locale patch refused: " + "; ".join(problems))


class LocaleStrings:
    """The client's locale as Sanctuary's players get it (its patch, else the client's own), and Sanctuary's strings."""

    def __init__(self, client_dir: Path, resources: Path = resources_dir):
        self.client_dir = client_dir
        self.resources = resources
        self.registry_path = resources / STRINGS_FILE
        self._texts = None
        self._stamp = None

    # --- the files -------------------------------------------------------------------------------------

    def _client_file(self, ext: str) -> Path:
        folder = next((p for p in self.client_dir.iterdir() if p.name.lower() == "locale"), self.client_dir / "Locale")
        return folder / f"{LOCALE}_data.{ext}"

    def stock(self) -> tuple[bytes, bytes]:
        def read(ext):
            path = self._client_file(ext)
            backup = next((path.with_name(path.name + suffix) for suffix in BACKUPS if path.with_name(path.name + suffix).exists()), path)
            return backup.read_bytes()
        return read("dat"), read("dir")

    def current(self) -> tuple[bytes, bytes]:
        """The patch players get, else the stock locale."""
        patch = self.resources / PATCH_DIR
        dat, dir_ = patch / f"{LOCALE}_data.dat", patch / f"{LOCALE}_data.dir"
        return (dat.read_bytes(), dir_.read_bytes()) if dat.exists() and dir_.exists() else self.stock()

    def registry(self) -> dict[int, str]:
        if not self.registry_path.exists():
            return {}
        return {int(k): v for k, v in json.loads(self.registry_path.read_text(encoding="utf-8-sig")).items()}

    # --- reading ---------------------------------------------------------------------------------------

    def _load(self) -> dict[int, str]:
        """Every string by key, as players get them (read again when the patch on disk changes)."""
        patch = self.resources / PATCH_DIR
        stamp = tuple(p.stat().st_mtime_ns if p.exists() else None for p in (patch / f"{LOCALE}_data.dat", patch / f"{LOCALE}_data.dir"))
        if self._texts is None or self._stamp != stamp:
            dat, dir_ = self.current()
            _, rows, _ = _parse(dat, dir_)
            self._texts = {key: dat[offset:offset + length].decode("utf-8", "replace").split("\t", 2)[-1]
                           for key, offset, length, _ in rows}
            self._stamp = stamp
        return self._texts

    def text(self, string_id: int) -> str | None:
        """A string id's text; Sanctuary's own even before the patch is rebuilt."""
        if string_id >= FIRST_ID and string_id in (registry := self.registry()):
            return registry[string_id]
        return self._load().get(string_key(string_id)) if string_id > 0 else None

    # --- writing ---------------------------------------------------------------------------------------

    def add(self, text: str) -> int:
        """A new string (or the one that already says exactly this); returns its id."""
        text = self._clean(text)
        registry = self.registry()
        for string_id, existing in registry.items():
            if existing == text:
                return string_id
        string_id = max([FIRST_ID - 1, *registry]) + 1
        # Clear of anything already in the locale under that key.
        while string_key(string_id) in self._load():
            string_id += 1
        registry[string_id] = text
        self._save(registry)
        return string_id

    def set(self, string_id: int, text: str):
        """Changes one of Sanctuary's strings (stock ones stay as they are)."""
        registry = self.registry()
        if string_id not in registry:
            raise ValueError(f"String {string_id} isn't one of Sanctuary's (ids from {FIRST_ID} in {STRINGS_FILE}).")
        registry[string_id] = self._clean(text)
        self._save(registry)

    @staticmethod
    def _clean(text: str) -> str:
        text = " ".join(str(text).split())
        if not text or len(text) > 400:
            raise ValueError("A name is one line of 1 to 400 characters.")
        return text

    def _save(self, registry: dict[int, str]):
        self.registry_path.write_text(json.dumps({str(k): v for k, v in sorted(registry.items())}, indent=2, ensure_ascii=False) + "\n",
                                      encoding="utf-8")

    def build_patch(self) -> list[Path]:
        """Rebuilds the locale client patch with every Sanctuary string set; returns the files written."""
        stock = self.stock()
        strings = {string_key(i): f"{string_key(i)}\tucdt\t{text}".encode("utf-8") for i, text in self.registry().items()}
        new = _layout(*self.current(), strings)
        _check(stock, new)
        patch = self.resources / PATCH_DIR
        patch.mkdir(parents=True, exist_ok=True)
        written = []
        for ext, data in zip(("dat", "dir"), new):
            path = patch / f"{LOCALE}_data.{ext}"
            if not path.exists() or path.read_bytes() != data:
                path.write_bytes(data)
                written.append(path)
        self._texts = None
        return written


def main():
    parser = ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--client", type=Path, required=True, help="Free Realms client folder")
    args = parser.parse_args()
    strings = LocaleStrings(args.client)
    written = strings.build_patch()
    print(f"{len(strings.registry())} Sanctuary string(s); {'wrote ' + ', '.join(map(str, written)) if written else 'the patch already has them'}.")


if __name__ == "__main__":
    main()
