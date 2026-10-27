"""Client assets by name, from wherever Sanctuary would serve them.

Lookups go, in order, through:
  1. src/Resources/ClientMods: what the WebAPI publishes on top of the stock manifest,
  2. a local copy of the asset server (<bucket>/<name>, large files as <name>.z), e.g. Sanctuary's assets junction,
  3. the client's Assets_*.pack files (later packs win, like the client),
  4. the upstream asset server, cached on disk.

The client's own manifest (Assets_manifest.txt) says which names are stock. Stock assets the client keeps
in its read-only Assets_NNN packs can't be replaced from the server: the client downloads the new crc and
then refuses to write over the pack ("Attempt to update data that already exists in a non-writeable pack
file"). Only new names stream, so the editor saves zones under names the client doesn't have.
"""

from __future__ import annotations

import os
import struct
import threading
import urllib.error
import urllib.request
import zlib
from pathlib import Path

COMPRESSED_MAGIC = 0xA1B2C3D4


def decompress(data: bytes) -> bytes:
    if len(data) >= 8 and struct.unpack_from(">I", data)[0] == COMPRESSED_MAGIC:
        return zlib.decompress(data[8:])
    return data


def compress(data: bytes) -> bytes:
    return struct.pack(">II", COMPRESSED_MAGIC, len(data)) + zlib.compress(data, 9)


def jenkins(text: str) -> int:
    """Jenkins one-at-a-time with the client's signed 32-bit arithmetic."""
    def signed(value):
        value &= 0xFFFFFFFF
        return value - 0x100000000 if value & 0x80000000 else value

    h = 0
    for char in text:
        h = signed(h + ord(char))
        h = signed(h + signed(h << 10))
        h = signed(h ^ (h >> 6))
    h = signed(h + signed(h << 3))
    h = signed(h ^ (h >> 11))
    h = signed(h + signed(h << 15))
    return h & 0xFFFFFFFF


def read_pack_index(client_dir: Path) -> dict:
    """Every asset in the client's Assets*.pack files by lowercase name: (pack, offset, size). Later packs win."""
    entries = {}
    for pack in sorted(client_dir.glob("Assets*.pack")):
        with pack.open("rb") as f:
            offset = 0
            while True:
                f.seek(offset)
                next_offset, count = struct.unpack(">II", f.read(8))
                for _ in range(count):
                    name_length, = struct.unpack(">I", f.read(4))
                    name = f.read(name_length).decode("latin-1")
                    data_offset, size, _ = struct.unpack(">III", f.read(12))
                    entries[name.lower()] = (pack, data_offset, size)
                if next_offset == 0:
                    break
                offset = next_offset
    return entries


class AssetSource:
    def __init__(self, client_dir: Path, mods_dir: Path, mirror_dir: Path | None = None,
                 upstream: str | None = None, cache_dir: Path | None = None):
        self.client_dir = client_dir
        self.mods_dir = mods_dir
        self.mirror_dir = mirror_dir if mirror_dir and mirror_dir.is_dir() else None
        self.upstream = upstream.rstrip("/") if upstream else None
        self.cache_dir = cache_dir
        self.unavailable = set()
        self._lock = threading.Lock()

        # The client's manifest: lowercase plain name -> (listed name, crc of the uncompressed asset). It's the
        # copy the server last served, so once the client has met Sanctuary it lists the client mods too.
        self.manifest = {}
        manifest = client_dir / "Assets_manifest.txt"
        if manifest.exists():
            for line in manifest.read_text(encoding="latin-1").splitlines():
                if line.count(",") != 2:
                    continue
                listed, crc, _ = line.split(",")
                self.manifest[listed.removesuffix(".z").lower()] = (listed, int(crc))

        self.packs = read_pack_index(client_dir)

        self.mirror = {}
        if self.mirror_dir:
            for bucket in os.scandir(self.mirror_dir):
                if bucket.is_dir():
                    for entry in os.scandir(bucket.path):
                        self.mirror[entry.name.removesuffix(".z").lower()] = Path(entry.path)

    def _mods(self) -> dict:
        """ClientMods by lowercase plain name, re-listed whenever the folder changes."""
        stamp = self.mods_dir.stat().st_mtime_ns if self.mods_dir.is_dir() else None
        if getattr(self, "_mods_stamp", object()) != stamp:
            self._mods_index = {path.name.lower().removesuffix(".z"): path for path in self.mods_dir.iterdir()} if stamp else {}
            self._mods_stamp = stamp
        return self._mods_index

    def mod_path(self, name: str) -> Path | None:
        path = self._mods().get(name.lower())
        return path if path and path.exists() else None

    def is_stock(self, name: str) -> bool:
        """A name the client had before Sanctuary's mods (listed, and not published from ClientMods), or one
        it ships in a read-only pack: neither can be replaced from the server."""
        return self.in_readonly_pack(name) or (name.lower() in self.manifest and self.mod_path(name) is None)

    def in_readonly_pack(self, name: str) -> bool:
        """True when the client ships this name in a read-only pack, so a server copy can never replace it."""
        entry = self.packs.get(name.lower())
        return entry is not None and not entry[0].name.lower().startswith("assetsw")

    def exists(self, name: str) -> bool:
        lower = name.lower()
        return (self.mod_path(name) is not None or lower in self.mirror or lower in self.packs
                or lower in self.manifest)

    def names(self, suffix: str = "") -> set:
        """Lowercase names of every known asset, optionally only those ending with suffix."""
        names = set(self.manifest) | set(self.packs) | set(self.mirror) | set(self._mods())
        return {name for name in names if name.endswith(suffix)} if suffix else names

    def read(self, name: str, stock: bool = False) -> bytes:
        """The uncompressed asset. stock=True skips ClientMods, for the original of a modded asset."""
        if not stock:
            path = self.mod_path(name)
            if path:
                return decompress(path.read_bytes())
        lower = name.lower()
        if lower in self.mirror:
            return decompress(self.mirror[lower].read_bytes())
        if lower in self.packs:
            pack, offset, size = self.packs[lower]
            with pack.open("rb") as f:
                f.seek(offset)
                return decompress(f.read(size))
        if lower in self.manifest and self.upstream and lower not in self.unavailable:
            try:
                return self._download(*self.manifest[lower])
            except urllib.error.HTTPError as error:
                # Some manifest entries were never on the asset server; the client goes without them too.
                if error.code != 404:
                    raise
                self.unavailable.add(lower)
        raise KeyError(name)

    def _download(self, listed: str, crc: int) -> bytes:
        cached = self.cache_dir / listed if self.cache_dir else None
        if cached and cached.exists():
            return decompress(cached.read_bytes())
        # The bucket is the 2014 client's: jenkins of the upper-case listed name, mod 1000.
        url = f"{self.upstream}/{jenkins(listed.upper()) % 1000:03d}/{urllib.request.quote(listed)}?{crc}"
        request = urllib.request.Request(url, headers={"User-Agent": "FreeRealms"})
        with urllib.request.urlopen(request, timeout=60) as response:
            blob = response.read()
        data = decompress(blob)
        if zlib.crc32(data) != crc:
            raise ValueError(f"{listed} from {url} doesn't match the manifest's crc")
        if cached:
            with self._lock:
                cached.parent.mkdir(parents=True, exist_ok=True)
                cached.write_bytes(blob)
        return data
