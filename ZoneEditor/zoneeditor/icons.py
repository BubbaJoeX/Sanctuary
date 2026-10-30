"""Game icons as PNG, for the admin panel: an item's, a mount's or a collection's icon (an image set id), a title's,
a pet's, an ability's, a house's or a point of interest's (an image id), or an NPC's overhead icon (a notification id).

The client's image tables say which .dds each is: ImageSets.txt (ID^DESCRIPTION), ImageSetMappings.txt
(IMAGE_SET_ID^IMAGE_TYPE^IMAGE_ID) and Images.txt (ID^FILE_NAME^CONTENT_ID), read from Sanctuary's client patch
(src/Resources/ClientPatches/Resources/Images: the stock tables with Sanctuary's own icons added), else the client's.
A set's picture is its largest the assets have: type 7 (128 px), 6 (64), 5 (32), else a button's up state (1) (some
sets list a 128 px file that was never shipped). Types 8-10 are its
tint masks: a tint (a Tints.xml id) colours the icon where the mask of its size says, as dyes do in game. A notification
(the client's Resources/NotificationImages.txt: ID^IMAGE_ID^LAYER_ORDER^SIZE_X^SIZE_Y^...) is layers of images, a
bubble and the symbol on it, each a share of the icon's size, drawn bottom layer first.
"""

from __future__ import annotations

import io
import threading
from pathlib import Path

from . import models
from .workspace import Workspace

PICTURE_TYPES = ("7", "6", "5", "1")
# A picture type's tint mask: Large -> LargeMask and so on.
MASK_TYPES = {"7": "10", "6": "9", "5": "8"}
TABLES = ("ImageSets.txt", "ImageSetMappings.txt", "Images.txt")


def _table(path: Path) -> list:
    return [line.split("^") for line in path.read_text(encoding="latin-1").splitlines() if line and not line.startswith("#")]


class Icons:
    def __init__(self, workspace: Workspace):
        self.ws = workspace
        self._lock = threading.Lock()
        self._tables: tuple | None = None
        self._stamp = None

    def _folder(self) -> Path:
        patched = self.ws.resources / "ClientPatches" / "Resources" / "Images"
        return patched if all((patched / name).exists() for name in TABLES) else self.ws.client_dir / "Resources" / "Images"

    def _load(self) -> tuple[dict, dict]:
        """set id -> {type: image id}, and image id -> file name; read again when a table changes."""
        folder = self._folder()
        stamp = tuple((folder / name).stat().st_mtime for name in TABLES if (folder / name).exists())
        with self._lock:
            if self._tables is None or stamp != self._stamp:
                sets: dict[str, dict] = {}
                for row in _table(folder / "ImageSetMappings.txt"):
                    if len(row) >= 3:
                        sets.setdefault(row[0], {})[row[1]] = row[2]
                files = {row[0]: row[1] for row in _table(folder / "Images.txt") if len(row) >= 2 and row[1]}
                self._tables, self._stamp = (sets, files), stamp
            return self._tables

    def files(self, set_id: str | None = None, image_id: str | None = None) -> list[tuple[str, str | None]]:
        """The .dds files an icon can be drawn from, largest first, each with its tint mask's (a set's), or KeyError."""
        sets, files = self._load()
        if image_id is not None:
            return [(files[str(int(image_id))], None)]
        mapping = sets[str(int(set_id))]
        found = []
        for kind in PICTURE_TYPES:
            if mapping.get(kind) in files:
                mask = mapping.get(MASK_TYPES.get(kind, ""))
                found.append((files[mapping[kind]], files.get(mask) if mask else None))
        if not found:
            raise KeyError(f"icon set {set_id}")
        return found

    def _notifications(self) -> dict:
        """notification id -> [(layer order, image id, width share, height share)]"""
        path = self.ws.client_dir / "Resources" / "NotificationImages.txt"
        layers: dict[str, list] = {}
        for row in _table(path) if path.exists() else []:
            if len(row) >= 5:
                layers.setdefault(row[0], []).append((int(row[2]), row[1], float(row[3]), float(row[4])))
        return layers

    def notification_png(self, notification_id: str, size: int = 64) -> bytes:
        from PIL import Image

        layers = sorted(self._notifications()[str(int(notification_id))])
        _, files = self._load()
        canvas = Image.new("RGBA", (size, size))
        for _, image_id, width, height in layers:
            if image_id not in files:
                continue
            try:
                image = models.open_texture(self.ws.assets.read(files[image_id]))
            except KeyError:
                continue
            w, h = max(1, round(size * min(width, 1.0))), max(1, round(size * min(height, 1.0)))
            image = image.resize((w, h))
            canvas.alpha_composite(image, ((size - w) // 2, (size - h) // 2))
        out = io.BytesIO()
        canvas.save(out, "PNG", compress_level=1)
        return out.getvalue()

    def png(self, set_id: str | None = None, image_id: str | None = None, size: int = 64, tint: int = 0) -> bytes:
        image = None
        for name, mask_name in self.files(set_id, image_id):
            try:
                image = models.open_texture(self.ws.assets.read(name))
                break
            except KeyError:
                continue
        if image is None:
            raise KeyError(name)
        colour = self.ws.tints().get(int(tint)) if tint else None
        if colour and mask_name:
            try:
                mask = models.open_texture(self.ws.assets.read(mask_name)).resize(image.size)
            except KeyError:
                mask = None
            if mask is not None:
                from PIL import Image, ImageChops

                tinted = ImageChops.multiply(image, Image.new("RGBA", image.size, tuple(int(c * 255) for c in colour) + (255,)))
                image = Image.composite(tinted, image, mask.getchannel("A"))
        if max(image.size) > size:
            image.thumbnail((size, size))
        out = io.BytesIO()
        image.save(out, "PNG", compress_level=1)
        return out.getvalue()
