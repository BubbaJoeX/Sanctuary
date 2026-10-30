"""The coin store's files, for the admin panel's Store page.

- src/Resources/CoinStoreItems.json: what the store sells, {Id, CategoryId, TintGroupId}, with the client's flags
  BuyDisabled (shown, but the Gateway won't sell it), CoinStoreOnly and Hidden when set.
- src/Resources/ClientItemDefinitions.json: an item's Cost, MemberDiscount (percent off, what everyone pays now) and
  ResellValue (what selling one back gives).
- src/Resources/CoinStoreBundles.json: bundles. A bundle is sold as a store item (its card: name, icon, price and
  category) and buying one gives the items and coins it holds instead.

The panel reads the store from the Gateway (GET /api/store/...) and changes it here. Both big files are laid out an
entry to a block (two-space JSON, CRLF), so an edit swaps only the blocks of the entries it changes and the rest stays
byte for byte; writes go to the running Gateway's copies too, and the panel then has it reload "items" and
"coinstore". Custom items (9000000 and up) live in the database, so their prices are set through the Gateway.
"""

from __future__ import annotations

import json
import re
import threading
from pathlib import Path

from .workspace import Workspace

CUSTOM_ITEM_BASE = 9000000
# Item types sold on their own: a bundle can't hold them (pets, mounts and houses are records, not inventory).
NOT_IN_BUNDLES = {2: "a pet", 19: "a mount", 16: "a house"}
MAX_PRICE = 100_000_000
MAX_QUANTITY = 999
MAX_BUNDLE_ITEMS = 32
FLAGS = ("BuyDisabled", "CoinStoreOnly", "Hidden")


def _render(entry: dict) -> str:
    """An entry as the files lay one out: two-space JSON, one level in."""
    return "\n".join("  " + line for line in json.dumps(entry, indent=2, ensure_ascii=False).split("\n"))


class _EntryFile:
    """A top-level list of entries with ids, edited an entry at a time."""

    def __init__(self, path: Path):
        self.path = path
        self._cache: tuple | None = None

    def read(self) -> str:
        raw = self.path.read_bytes()
        return raw.decode("utf-8-sig").replace("\r\n", "\n")

    def entries(self) -> list:
        stamp = self.path.stat().st_mtime
        if self._cache is None or self._cache[0] != stamp:
            self._cache = (stamp, json.loads(self.read()))
        return self._cache[1]

    def by_id(self) -> dict:
        return {entry["Id"]: entry for entry in self.entries()}

    @staticmethod
    def span(text: str, entry_id: int) -> tuple[int, int] | None:
        """Where the entry with this id is: from its "  {" to its closing "}"."""
        for match in re.finditer(rf'^    "Id": {int(entry_id)},?$', text, re.M):
            start = text.rfind("\n  {\n", 0, match.start()) + 1
            end = text.find("\n  }", match.end()) + 4
            if start > 0 and end > 3:
                try:
                    if json.loads(text[start:end]).get("Id") == entry_id:
                        return start, end
                except json.JSONDecodeError:
                    continue
        return None

    def write(self, changes: dict):
        """Applies {id: entry (new or changed) or None (removed)} and writes the file back as it was laid out."""
        raw = self.path.read_bytes()
        bom = raw.startswith(b"\xef\xbb\xbf")
        crlf = b"\r\n" in raw[:2000]
        text = raw.decode("utf-8-sig").replace("\r\n", "\n")
        for entry_id, entry in changes.items():
            found = self.span(text, entry_id)
            if found and entry is not None:
                text = text[: found[0]] + _render(entry) + text[found[1] :]
            elif found:
                start, end = found
                if text.startswith(",\n", end):
                    text = text[:start] + text[end + 2 :]
                else:
                    before = text.rfind(",\n", 0, start)
                    text = text[:before] + text[end:]
            elif entry is not None:
                close = text.rstrip().rfind("\n]")
                if close < 0:
                    raise ValueError(f"{self.path.name} isn't a list of entries.")
                empty = text[:close].rstrip().endswith("[")
                text = text[:close] + ("\n" if empty else ",\n") + _render(entry) + text[close:]
        json.loads(text)
        if crlf:
            text = text.replace("\n", "\r\n")
        self.path.write_bytes((b"\xef\xbb\xbf" if bom else b"") + text.encode("utf-8"))
        self._cache = None


class Store:
    def __init__(self, workspace: Workspace):
        self.ws = workspace
        self.items = _EntryFile(workspace.resources / "ClientItemDefinitions.json")
        self.store = _EntryFile(workspace.resources / "CoinStoreItems.json")
        self.bundles_path = workspace.resources / "CoinStoreBundles.json"
        self._lock = threading.Lock()

    # --- reads -----------------------------------------------------------------------------------------

    def bundles(self) -> list:
        return json.loads(self.bundles_path.read_text(encoding="utf-8-sig")) if self.bundles_path.exists() else []

    def category_names(self) -> dict:
        """Category id -> what the client calls it (ItemCategories.txt's string ids, through the locale)."""
        rows = [line.split("^") for line in (self.ws.resources / "ItemCategories.txt").read_text(encoding="latin-1").splitlines()
                if line and not line.startswith("#")]
        texts = self.ws.world.texts([row[1] for row in rows if len(row) > 1])
        return {int(row[0]): texts.get(row[1]) for row in rows if len(row) > 1 and row[0].isdigit()}

    # --- writes ----------------------------------------------------------------------------------------

    def _item(self, item_id: int) -> dict:
        item = self.items.by_id().get(item_id)
        if item is None:
            raise ValueError(f"There's no item {item_id} in ClientItemDefinitions.json.")
        return item

    def save(self, entries: list) -> dict:
        """Puts items in the store or changes how it sells them: [{id, categoryId, tintGroupId, buyDisabled,
        coinStoreOnly, hidden, cost?, memberDiscount?, resellValue?}]. Prices of custom items are the Gateway's."""
        store_changes, item_changes = {}, {}
        with self._lock:
            current = self.store.by_id()
            for raw in entries:
                item_id = int(raw["id"])
                if item_id <= CUSTOM_ITEM_BASE:
                    item = dict(self._item(item_id))
                    for field, key in (("Cost", "cost"), ("MemberDiscount", "memberDiscount"), ("ResellValue", "resellValue")):
                        if raw.get(key) is None:
                            continue
                        value = int(raw[key])
                        if field == "MemberDiscount" and not 0 <= value <= 100:
                            raise ValueError("A discount is 0 to 100 percent.")
                        if not 0 <= value <= MAX_PRICE:
                            raise ValueError(f"Prices are 0 to {MAX_PRICE:,} coins.")
                        if field == "ResellValue" and value == 0 and "ResellValue" not in item:
                            continue
                        item[field] = value
                    if item != self._item(item_id):
                        item_changes[item_id] = item
                entry = dict(current.get(item_id) or {"Id": item_id})
                entry["CategoryId"] = int(raw.get("categoryId", entry.get("CategoryId", 0)))
                tint_group = int(raw.get("tintGroupId", entry.get("TintGroupId", 0)))
                if tint_group:
                    entry["TintGroupId"] = tint_group
                else:
                    entry.pop("TintGroupId", None)
                for flag in FLAGS:
                    key = flag[0].lower() + flag[1:]
                    if raw.get(key):
                        entry[flag] = True
                    elif key in raw:
                        entry.pop(flag, None)
                if entry != current.get(item_id):
                    store_changes[item_id] = entry
            written = []
            if item_changes:
                self.items.write(item_changes)
                written.append(self.items.path)
            if store_changes:
                self.store.write(store_changes)
                written.append(self.store.path)
            mirrored = self.ws.mirror(written)
        return {"store": len(store_changes), "prices": len(item_changes), "mirrored": mirrored}

    def remove(self, item_ids: list) -> dict:
        """Takes items out of the store (a bundle's card too, which keeps the bundle from being sold)."""
        with self._lock:
            current = self.store.by_id()
            gone = {int(i): None for i in item_ids if int(i) in current}
            if gone:
                self.store.write(gone)
            mirrored = self.ws.mirror([self.store.path]) if gone else []
        return {"removed": len(gone), "mirrored": mirrored}

    def save_bundle(self, bundle: dict, category_id: int | None = None) -> dict:
        """Adds or replaces a bundle ({id, comment, items: [{id, quantity, tint}], coins}) and puts its card in the
        store under category_id (keeping the card's category when it's already there)."""
        bundle_id = int(bundle["id"])
        items = [{"Id": int(i["id"]), "Quantity": int(i.get("quantity", 1)), **({"Tint": int(i["tint"])} if i.get("tint") else {})}
                 for i in bundle.get("items") or []]
        coins = int(bundle.get("coins") or 0)
        if not items and coins <= 0:
            raise ValueError("A bundle gives at least an item or some coins.")
        if len(items) > MAX_BUNDLE_ITEMS:
            raise ValueError(f"A bundle holds at most {MAX_BUNDLE_ITEMS} items.")
        if not 0 <= coins <= MAX_PRICE:
            raise ValueError(f"Coins are 0 to {MAX_PRICE:,}.")
        definitions = self.items.by_id()
        if bundle_id <= CUSTOM_ITEM_BASE and bundle_id not in definitions:
            raise ValueError(f"There's no item {bundle_id} to sell it as.")
        for item in items:
            definition = definitions.get(item["Id"])
            if definition is None and item["Id"] <= CUSTOM_ITEM_BASE:
                raise ValueError(f"There's no item {item['Id']}.")
            if item["Id"] == bundle_id:
                raise ValueError("A bundle can't hold the item it's sold as.")
            if definition and definition.get("Type") in NOT_IN_BUNDLES:
                raise ValueError(f"Item {item['Id']} is {NOT_IN_BUNDLES[definition['Type']]}, which is sold on its own.")
            if not 1 <= item["Quantity"] <= MAX_QUANTITY:
                raise ValueError(f"Quantities are 1 to {MAX_QUANTITY}.")
        entry = {"Id": bundle_id, **({"Comment": bundle["comment"].strip()} if (bundle.get("comment") or "").strip() else {}),
                 "Items": items, **({"Coins": coins} if coins else {})}
        with self._lock:
            bundles = [b for b in self.bundles() if b.get("Id") != bundle_id] + [entry]
            bundles.sort(key=lambda b: b["Id"])
            self.bundles_path.write_bytes((json.dumps(bundles, indent=2, ensure_ascii=False) + "\n").replace("\n", "\r\n").encode("utf-8"))
            written = [self.bundles_path]
            current = self.store.by_id()
            card = current.get(bundle_id)
            if card is None or (category_id is not None and card.get("CategoryId") != category_id):
                card = dict(card or {"Id": bundle_id})
                card["CategoryId"] = int(category_id if category_id is not None else card.get("CategoryId", 0))
                self.store.write({bundle_id: card})
                written.append(self.store.path)
            mirrored = self.ws.mirror(written)
        return {"bundle": entry, "mirrored": mirrored}

    def delete_bundle(self, bundle_id: int, keep_card: bool = False) -> dict:
        """Deletes a bundle and, unless keep_card, takes its card out of the store."""
        bundle_id = int(bundle_id)
        with self._lock:
            bundles = self.bundles()
            kept = [b for b in bundles if b.get("Id") != bundle_id]
            if len(kept) == len(bundles):
                raise KeyError(f"bundle {bundle_id}")
            self.bundles_path.write_bytes((json.dumps(kept, indent=2, ensure_ascii=False) + "\n").replace("\n", "\r\n").encode("utf-8"))
            written = [self.bundles_path]
            if not keep_card and bundle_id in self.store.by_id():
                self.store.write({bundle_id: None})
                written.append(self.store.path)
            mirrored = self.ws.mirror(written)
        return {"deleted": bundle_id, "mirrored": mirrored}
