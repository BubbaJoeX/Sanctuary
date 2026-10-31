"""Player titles (src/Resources/PlayerTitles.json) for the admin panel's GameData editor.

A title is an id (kept in the database for every player who has it, so never renumbered or reused), a Type the
client is sent as it is, a name (a locale string: Sanctuary's own for new text, in the locale client patch) and an
optional icon (an image id). The client has no title list of its own: it learns each one from the server at login.
Titles are granted by !title grant, and to new characters by login.json's DefaultTitleId and UnlockAllTitles. Writes
keep the file's layout and go to the servers' copies; the browser then reloads them (POST
/api/resources/titles/reload; the file watcher can't). Players see a change as they next log in.
"""

from __future__ import annotations

import threading

from . import gamedata
from .gamedata import Texts, ordered, whole
from .workspace import Workspace

TEXTS = ("NameId",)
# The game's titles; titles made here come after them and only they can be deleted.
STOCK = 90


class Titles:
    def __init__(self, workspace: Workspace):
        self.ws = workspace
        self.path = workspace.resources / "PlayerTitles.json"
        self.texts = Texts(workspace)
        self._lock = threading.Lock()

    def overview(self) -> dict:
        titles = gamedata.load(self.path)
        return {"titles": [dict(t, Texts=x, made=t["Id"] > STOCK) for t, x in zip(titles, self.texts.read(titles, TEXTS))]}

    def save(self, title: dict, texts: dict | None) -> dict:
        """Adds a title (Id null) or changes one."""
        if not isinstance(title, dict):
            raise ValueError("A title is an object.")
        with self._lock:
            titles = gamedata.load(self.path)
            index = next((i for i, t in enumerate(titles) if t["Id"] == title.get("Id")), None)
            if title.get("Id") and index is None:
                raise KeyError(f"title {title['Id']}")
            checked = {"Id": title.get("Id"), "Type": whole(title.get("Type", 1), "The type", 0, 255),
                       "NameId": whole(title.get("NameId") or 0, "NameId")}
            if title.get("IconId"):
                checked["IconId"] = whole(title["IconId"], "The icon")
            if index is None:
                checked["Id"] = max([t["Id"] for t in titles] + [STOCK]) + 1
            checked = ordered(checked, titles[index] if index is not None else None)
            strings = self.texts.apply([checked], [texts or {}], TEXTS)
            if not checked["NameId"]:
                raise ValueError("Give the title a name.")
            if index is None:
                titles.append(checked)
            else:
                titles[index] = checked
            gamedata.save(self.ws, [(self.path, titles)])
        return {"title": checked, "strings": len(strings)}

    def delete(self, title_id: int) -> dict:
        if title_id <= STOCK:
            raise ValueError("Only titles made here can be deleted; players have the game's.")
        with self._lock:
            titles = gamedata.load(self.path)
            kept = [t for t in titles if t["Id"] != title_id]
            if len(kept) == len(titles):
                raise KeyError(f"title {title_id}")
            gamedata.save(self.ws, [(self.path, kept)])
        return {"deleted": title_id}
