"""Battles (src/Resources/Battles.json) and the mobs they fight with (Mobs.json), for the admin panel's GameData editor.

A battle runs in a private copy of a World zone: its starter NPCs offer it, players arrive at its entry, its waves of
enemies (NPC definitions from Npcs.json, each fighting as a Mobs.json mob) spawn one after another, and a win pays its
stars, coins and reward table (a Rewards.json key). Its name, description and goals are locale strings: new text
becomes Sanctuary's own, in the locale client patch. A save checks what the Gateway's loader and BattleTests check
(zone, NPCs, mobs, the dungeon wave rules) before it writes anything, and changed mobs go to Mobs.json in the same save.

Writes keep each file's layout and go to the servers' copies; the browser then reloads battles and mobs (POST
/api/resources/{name}/reload). Running battles keep what they started with. A starter is clickable only when a zone
script spawns it (src/Scripts/Zone/Battles.lua, read when the Gateway starts), which the overview says.
"""

from __future__ import annotations

import json
import re
import threading

from . import gamedata
from .gamedata import Texts, number, ordered, point, whole
from .workspace import Workspace

TEXTS = ("NameId", "DescriptionId", "ObjectiveNameId", "ObjectiveDescriptionId", "BossObjectiveNameId", "BossObjectiveDescriptionId")
MOB_KEY = re.compile(r"[a-z0-9][a-z0-9_.-]{0,63}")
SPAWN_LINE = re.compile(r"spawnNpc(?:WithGuid)?\(\s*(\d+)")
# A battle with a Games window tile (Image) has the tile 9000 + its id; vehicle games' tiles start at 9500.
MAX_TILED = 499
MAX_ID = 65535


class Battles:
    def __init__(self, workspace: Workspace):
        self.ws = workspace
        self.path = workspace.resources / "Battles.json"
        self.mobs_path = workspace.resources / "Mobs.json"
        self.texts = Texts(workspace)
        self._lock = threading.Lock()

    # --- reads -----------------------------------------------------------------------------------------

    def _npcs(self) -> dict:
        """NPC definition id -> name (or its model), from Npcs.json."""
        return {npc["Id"]: npc.get("Name") or npc.get("ModelFileName") or f"NPC {npc['Id']}"
                for npc in gamedata.load(self.ws.resources / "Npcs.json") if isinstance(npc, dict) and "Id" in npc}

    def _placed(self) -> set:
        """NPC ids a zone script spawns (src/Scripts/Zone/*.lua): the starters players can click."""
        folder = self.ws.root / "src" / "Scripts" / "Zone"
        found = set()
        for path in folder.glob("*.lua"):
            found.update(int(m.group(1)) for m in SPAWN_LINE.finditer(path.read_text(encoding="utf-8", errors="replace")))
        return found

    def _zones(self) -> dict:
        """World zone id -> name (the zones a battle can copy)."""
        return {data["Id"]: data["Name"] for _, data in self.ws.definitions().values()
                if data.get("$type") == "World" and isinstance(data.get("Id"), int)}

    def overview(self) -> dict:
        battles = gamedata.load(self.path)
        texts = self.texts.read(battles, ("NameId",))
        zones = self._zones()
        return {"battles": [{"Id": b["Id"], "Comment": b.get("Comment"), "name": t["NameId"], "ZoneId": b.get("ZoneId"),
                             "zone": zones.get(b.get("ZoneId")), "Difficulty": b.get("Difficulty"), "IconId": b.get("IconId"),
                             "dungeon": bool(b.get("Image")), "waves": len(b.get("Waves", [])),
                             "enemies": sum(len(w.get("Spawns", [])) for w in b.get("Waves", []))}
                            for b, t in zip(battles, texts)]}

    def options(self) -> dict:
        """What a battle can name: World zones, mobs (with their stats) and reward tables."""
        rewards = gamedata.load(self.ws.resources / "Rewards.json")
        return {"zones": [{"id": i, "name": n} for i, n in sorted(self._zones().items())],
                "mobs": gamedata.load(self.mobs_path), "rewardTables": sorted(t["Key"] for t in rewards)}

    def entry(self, battle_id: int) -> dict:
        """One battle with its texts, its NPCs' names and whether its starters are placed in the world."""
        battle = next((b for b in gamedata.load(self.path) if b["Id"] == battle_id), None)
        if battle is None:
            raise KeyError(f"battle {battle_id}")
        npcs = self._npcs()
        ids = set(battle.get("StarterNpcIds", [])) | {s.get("NpcId") for w in battle.get("Waves", []) for s in w.get("Spawns", [])}
        placed = self._placed()
        return {"battle": battle, "texts": self.texts.read([battle], TEXTS)[0], "npcs": {str(i): npcs.get(i) for i in ids if i},
                "placedStarters": [i for i in battle.get("StarterNpcIds", []) if i in placed], "usedBy": self._uses().get(battle_id, [])}

    def _uses(self) -> dict:
        """battle id -> what names it: quests (a battle goal) and achievements."""
        uses: dict[int, list] = {}
        for quest in gamedata.load(self.ws.resources / "Quests.json"):
            for goal in _walk(quest):
                if isinstance(goal.get("BattleId"), int):
                    uses.setdefault(goal["BattleId"], []).append(f"the quest {quest.get('Name') or quest.get('QuestId')}")
        return uses

    # --- checks ----------------------------------------------------------------------------------------

    def _check_mob(self, mob: dict, like: dict | None, rewards: set) -> dict:
        if not isinstance(mob, dict):
            raise ValueError("A mob is an object.")
        key = str(mob.get("Key", "")).strip()
        if not MOB_KEY.fullmatch(key):
            raise ValueError("A mob's key is lower-case letters, digits, _, - and .")
        what = f"Mob {key}"
        out = dict(mob, Key=key)
        out["MaxHitpoints"] = whole(mob.get("MaxHitpoints"), f"{what}'s health", 1, 10_000_000)
        out["Damage"] = whole(mob.get("Damage", 0), f"{what}'s damage", 0, 1_000_000)
        out["AttackIntervalMs"] = whole(mob.get("AttackIntervalMs"), f"{what}'s attack interval", 1, 600_000)
        out["AttackRange"] = number(mob.get("AttackRange"), f"{what}'s attack range", 0.1, 200)
        out["AggroRadius"] = number(mob.get("AggroRadius", 0), f"{what}'s aggro radius", 0, 500)
        out["LeashRadius"] = number(mob.get("LeashRadius"), f"{what}'s leash radius", 0, 2000)
        if out["LeashRadius"] <= out["AggroRadius"]:
            raise ValueError(f"{what} gives up the chase (leash) further out than it picks a fight (aggro).")
        for field in ("RespawnSeconds", "Coins", "Experience"):
            if mob.get(field) is not None:
                out[field] = whole(mob[field], f"{what}'s {field}", 0, 10_000_000)
        if mob.get("RewardChance") is not None:
            out["RewardChance"] = number(mob["RewardChance"], f"{what}'s reward chance", 0, 1)
        if mob.get("RewardTable") and mob["RewardTable"] not in rewards:
            raise ValueError(f"{what}: there's no reward table {mob['RewardTable']}.")
        return ordered(out, like)

    def _check(self, battle: dict, like: dict | None, mobs: set, rewards: set, npcs: dict) -> dict:
        if not isinstance(battle, dict):
            raise ValueError("A battle is an object.")
        zones = self._zones()
        zone = battle.get("ZoneId")
        if zone not in zones:
            raise ValueError("Pick a World zone for it to run in.")
        out = {"Id": battle.get("Id"), "Comment": str(battle.get("Comment") or "").strip() or None, "ZoneId": zone}
        for field in TEXTS[:2] + ("IconId",):
            out[field] = whole(battle.get(field) or 0, field)
        out["Difficulty"] = whole(battle.get("Difficulty", 1), "The difficulty", 1, 5)
        starters = battle.get("StarterNpcIds") or []
        for npc in starters:
            if npc not in npcs:
                raise ValueError(f"There's no NPC {npc} to start it.")
        out["StarterNpcIds"] = starters
        image = str(battle.get("Image") or "").strip()
        out["Image"] = image or None
        out["TimeLimitSeconds"] = whole(battle.get("TimeLimitSeconds", 1200), "The time limit", 1, 36000)
        for field, low, high in (("RespawnSeconds", 1, 600), ("MaxKnockouts", 1, 100), ("MaxPlayers", 1, 4)):
            if battle.get(field) is not None:
                out[field] = whole(battle[field], field, low, high)
        out["Entry"] = point(battle.get("Entry"), "The entry")
        out["EntryHeading"] = number(battle.get("EntryHeading", 0), "The entry heading", -7, 7)
        out["Exit"] = point(battle["Exit"], "The exit") if battle.get("Exit") else None
        for field in TEXTS[2:]:
            if battle.get(field) is not None or field.startswith("Objective"):
                out[field] = whole(battle.get(field) or 0, field)
        waves = battle.get("Waves") or []
        if not waves:
            raise ValueError("A battle needs at least one wave.")
        out["Waves"] = []
        for w, wave in enumerate(waves, 1):
            spawns = wave.get("Spawns") or []
            if not spawns:
                raise ValueError(f"Wave {w} has no enemies.")
            checked = []
            for s, spawn in enumerate(spawns, 1):
                where = f"Wave {w}, enemy {s}"
                if spawn.get("NpcId") not in npcs:
                    raise ValueError(f"{where}: there's no NPC {spawn.get('NpcId')}.")
                if spawn.get("Mob") and spawn["Mob"] not in mobs:
                    raise ValueError(f"{where}: there's no mob {spawn['Mob']}.")
                checked.append(ordered({"NpcId": spawn["NpcId"], "Position": point(spawn.get("Position"), f"{where}'s position"),
                                        "Heading": number(spawn.get("Heading", 0), f"{where}'s heading", -7, 7),
                                        "Mob": spawn.get("Mob") or None, "Boss": True if spawn.get("Boss") else None}, None))
            out["Waves"].append(ordered({"DelayMs": whole(wave.get("DelayMs", 1000), f"Wave {w}'s delay", 0, 600_000),
                                         "AtStart": True if wave.get("AtStart") else None, "Spawns": checked}, None))
        _check_dungeon(out)
        out["Stars"] = whole(battle.get("Stars", 0), "The stars", 0, 100_000)
        out["Coins"] = whole(battle.get("Coins", 0), "The coins", 0, 10_000_000)
        if battle.get("RewardTable"):
            if battle["RewardTable"] not in rewards:
                raise ValueError(f"There's no reward table {battle['RewardTable']}.")
            out["RewardTable"] = battle["RewardTable"]
        return ordered(out, like)

    # --- writes ----------------------------------------------------------------------------------------

    def save(self, battle: dict, texts: dict | None, mobs: list | None = None) -> dict:
        """{battle (Id null: a new one), texts, mobs: changed or new Mobs.json entries}: in one go."""
        with self._lock:
            battles = gamedata.load(self.path)
            all_mobs = gamedata.load(self.mobs_path)
            rewards = {t["Key"] for t in gamedata.load(self.ws.resources / "Rewards.json")}
            mob_index = {m["Key"]: i for i, m in enumerate(all_mobs)}
            for mob in mobs or []:
                checked_mob = self._check_mob(mob, all_mobs[mob_index[mob["Key"]]] if mob.get("Key") in mob_index else None, rewards)
                if checked_mob["Key"] in mob_index:
                    all_mobs[mob_index[checked_mob["Key"]]] = checked_mob
                else:
                    mob_index[checked_mob["Key"]] = len(all_mobs)
                    all_mobs.append(checked_mob)
            index = next((i for i, b in enumerate(battles) if b["Id"] == battle.get("Id")), None)
            if battle.get("Id") and index is None:
                raise KeyError(f"battle {battle['Id']}")
            checked = self._check(battle, battles[index] if index is not None else None, set(mob_index), rewards, self._npcs())
            if index is None:
                checked["Id"] = max([b["Id"] for b in battles] + [0]) + 1
                checked = ordered(checked, battles[-1] if battles else None)
            if checked.get("Image") and checked["Id"] > MAX_TILED:
                raise ValueError(f"A battle with a Games window tile has an id up to {MAX_TILED}.")
            if checked["Id"] > MAX_ID:
                raise ValueError(f"Battle ids go up to {MAX_ID}.")
            strings = self.texts.apply([checked], [texts or {}], TEXTS)
            if not checked["NameId"]:
                raise ValueError("Give the battle a name.")
            if index is None:
                battles.append(checked)
            else:
                battles[index] = checked
            written = gamedata.save(self.ws, [(self.path, battles), (self.mobs_path, all_mobs)])
        placed = self._placed()
        return {"battle": checked, "written": [p.name for p in written], "new": index is None, "strings": len(strings),
                "unplacedStarters": [i for i in checked["StarterNpcIds"] if i not in placed]}

    def delete(self, battle_id: int) -> dict:
        with self._lock:
            battles = gamedata.load(self.path)
            if not any(b["Id"] == battle_id for b in battles):
                raise KeyError(f"battle {battle_id}")
            if used := self._uses().get(battle_id):
                raise ValueError(f"{', '.join(used)} names it; it can't be deleted.")
            gamedata.save(self.ws, [(self.path, [b for b in battles if b["Id"] != battle_id])])
        return {"deleted": battle_id}


def _check_dungeon(battle: dict):
    """A dungeon (guards there from the start: waves AtStart) as BattleTests has it: every wave but the last is
    there at the start, the last isn't and holds its one boss, and it has an Exit."""
    waves = battle["Waves"]
    if not any(w.get("AtStart") for w in waves):
        return
    bosses = [s for w in waves for s in w["Spawns"] if s.get("Boss")]
    if len(bosses) != 1:
        raise ValueError("A dungeon (with guards there from the start) has exactly one boss.")
    if waves[-1].get("AtStart") or not any(s.get("Boss") for s in waves[-1]["Spawns"]):
        raise ValueError("A dungeon's last wave comes after the guards and holds its boss.")
    if any(not w.get("AtStart") for w in waves[:-1]):
        raise ValueError("In a dungeon every wave but the last is there from the start.")
    if not battle.get("Exit"):
        raise ValueError("A dungeon needs an Exit.")


def _walk(value):
    """Every object in a resource entry, however it nests them."""
    stack = [value]
    while stack:
        item = stack.pop()
        if isinstance(item, dict):
            yield item
            stack.extend(v for v in item.values() if isinstance(v, (dict, list)))
        elif isinstance(item, list):
            stack.extend(v for v in item if isinstance(v, (dict, list)))
