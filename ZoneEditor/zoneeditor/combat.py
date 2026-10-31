"""Jobs and combat abilities for the admin panel's GameData editor: a job and everything it is, in one save.

A job spans three files, and its words a fourth:
- src/Resources/Profiles.json: the job itself (ClientProfileData, sent to clients on every zone entry): its name,
  description and trainer hint (locale strings), category (Type), icon (an image id), badge, button and ability
  background (image sets), the items it starts with, and members-only and trial flags.
- src/Resources/CombatJobs.json: a combat job's kit (JobKitDefinition; a job is a combat job because it has one): its
  reach, recast and energy, and which ability each of its weapons fights with, basic and special.
- src/Resources/CombatAbilities.json: the abilities (AbilityDefinition): what each does (EffectType, damage, area,
  energy), how it looks (animation, cast, hit and end effects) and its name, description and icon on the toolbar.
- Names are Sanctuary strings (gamedata.Texts) in the locale client patch.

The Gateway refuses to start when a kit names an ability that doesn't exist, so a save checks every kit against
every ability before it writes anything, and an ability a kit uses can't be deleted. Writes keep each file's layout
(CombatAbilities.json and CombatJobs.json are PowerShell's) and go to the servers' copies; the browser then reloads
abilities, jobs and profiles (POST /api/resources/{name}/reload). Players see a job's changes as they next log in;
new characters get new jobs after the Login server restarts.
"""

from __future__ import annotations

import threading

from . import gamedata
from .gamedata import Texts, number, ordered, whole
from .workspace import Workspace

EFFECT_TYPES = ("SweepDamage", "AoeDamage", "SingleTargetDamage", "AoeDamageHeal", "Summon", "Buff")
# Job categories (Profiles.json Type), as the client groups them.
JOB_TYPES = {1: "Adventurer", 2: "Combat", 3: "Postman", 5: "Kart racing", 7: "Demolition derby", 8: "Soccer",
             9: "Miner and staff", 10: "Chef", 12: "Blacksmith", 17: "Card duelist", 21: "Fisherman"}
ABILITY_TEXTS = ("NameId", "DescriptionId")
JOB_TEXTS = ("NameId", "DescriptionId", "Hint")
# Fields the server never reads, kept as they are.
ABILITY_SPARE = ("TargetAnimationId", "WeaponEffectId", "WeaponEffectDurationMs", "TargetEffectDurationMs", "ContactEffectId",
                 "EnemyExtraEffectId")
# Jobs made here take ids from this one up, clear of the game's (1-137); only they can be deleted.
NEW_JOBS = 200
# Every stock kit's toolbar slots: the client's own basic and special attack slot definitions.
BASIC_SLOT, SPECIAL_SLOT = 4895, 4899
CUSTOM_ITEMS = 9_000_000


class Combat:
    def __init__(self, workspace: Workspace):
        self.ws = workspace
        self.abilities_path = workspace.resources / "CombatAbilities.json"
        self.kits_path = workspace.resources / "CombatJobs.json"
        self.profiles_path = workspace.resources / "Profiles.json"
        self.texts = Texts(workspace)
        self._lock = threading.Lock()

    def _load(self):
        return gamedata.load(self.abilities_path), gamedata.load(self.kits_path), gamedata.load(self.profiles_path)

    # --- reads -----------------------------------------------------------------------------------------

    @staticmethod
    def _uses(kits: list, profiles: list) -> dict:
        """ability id -> [{profileId, job, slot, weapon}] for every kit that names it."""
        names = {p["Id"]: p.get("Comment") or f"Job {p['Id']}" for p in profiles}
        uses: dict[int, list] = {}

        def note(ability, profile, slot, weapon=None):
            if ability:
                uses.setdefault(ability, []).append({"profileId": profile, "job": names.get(profile, f"Job {profile}"), "slot": slot, "weapon": weapon})

        for kit in kits:
            note(kit.get("FallbackBasicAbilityId"), kit["ProfileId"], "fallback")
            for weapon in kit.get("Weapons", []):
                note(weapon.get("BasicAbilityId"), kit["ProfileId"], "basic", weapon.get("Comment"))
                note(weapon.get("SpecialAbilityId"), kit["ProfileId"], "special", weapon.get("Comment"))
        return uses

    def abilities(self) -> dict:
        """Every combat ability with its texts and what uses it, and what an ability can be."""
        abilities, kits, profiles = self._load()
        uses = self._uses(kits, profiles)
        texts = self.texts.read(abilities, ABILITY_TEXTS)
        return {"abilities": [dict(a, Texts=t, UsedBy=uses.get(a["Id"], [])) for a, t in zip(abilities, texts)],
                "effectTypes": list(EFFECT_TYPES)}

    def jobs(self) -> dict:
        """Every job: its profile, texts and kit (None for a job that doesn't fight)."""
        abilities, kits, profiles = self._load()
        by_profile = {kit["ProfileId"]: kit for kit in kits}
        texts = self.texts.read(profiles, JOB_TEXTS)
        return {"jobs": [{"profile": p, "texts": t, "kit": by_profile.get(p["Id"]), "made": p["Id"] >= NEW_JOBS}
                         for p, t in zip(profiles, texts)],
                "types": {str(k): v for k, v in JOB_TYPES.items()}, "effectTypes": list(EFFECT_TYPES)}

    def job(self, profile_id: int) -> dict:
        """One job: its profile, texts, kit, and each ability its kit names (with texts and every other job using it)."""
        abilities, kits, profiles = self._load()
        profile = next((p for p in profiles if p["Id"] == profile_id), None)
        if profile is None:
            raise KeyError(f"job {profile_id}")
        kit = next((k for k in kits if k["ProfileId"] == profile_id), None)
        uses = self._uses(kits, profiles)
        named = _kit_abilities(kit) if kit else []
        found = [a for a in abilities if a["Id"] in named]
        return {"profile": profile, "texts": self.texts.read([profile], JOB_TEXTS)[0], "kit": kit, "made": profile_id >= NEW_JOBS,
                "abilities": [{"ability": a, "texts": t, "usedBy": uses.get(a["Id"], [])}
                              for a, t in zip(found, self.texts.read(found, ABILITY_TEXTS))]}

    # --- checks ----------------------------------------------------------------------------------------

    def _check_ability(self, ability: dict, like: dict | None) -> dict:
        if not isinstance(ability, dict):
            raise ValueError("An ability is an object.")
        name = f"Ability {ability.get('Id') or '(new)'}"
        effect = ability.get("EffectType")
        if effect not in EFFECT_TYPES:
            raise ValueError(f"{name}'s effect is one of {', '.join(EFFECT_TYPES)}.")
        out = {"Id": ability.get("Id"), "Comment": str(ability.get("Comment") or "").strip() or None, "EffectType": effect,
               "Damage": whole(ability.get("Damage", 0), f"{name}'s damage", 0, 1_000_000)}
        if ability.get("HitCount"):
            out["HitCount"] = whole(ability["HitCount"], f"{name}'s hits", 1, 20)
        if ability.get("AoeRadius"):
            out["AoeRadius"] = number(ability["AoeRadius"], f"{name}'s area radius", 0, 100)
        if ability.get("EnergyCost"):
            out["EnergyCost"] = whole(ability["EnergyCost"], f"{name}'s energy cost", 0, 1000)
        for field in ("AnimationId", "HitEffectId", "CastEffectId", "CasterEndEffectId", *ABILITY_SPARE):
            if ability.get(field) is not None:
                out[field] = whole(ability[field], f"{name}'s {field}")
        for field in ("NameId", "DescriptionId", "IconId"):
            if ability.get(field) is not None:
                out[field] = whole(ability[field], f"{name}'s {field}")
        if effect == "AoeDamage" and not out.get("AoeRadius"):
            raise ValueError(f"{name} hits an area: give it a radius.")
        return ordered(out, like)

    def _check_kit(self, kit: dict, profile_id: int, ability_ids: set, like: dict | None) -> dict:
        if not isinstance(kit, dict):
            raise ValueError("A combat kit is an object.")
        items = self.ws.item_ids()

        def ability(value, what, required):
            value = whole(value or 0, what)
            if not value and required:
                raise ValueError(f"{what}: pick an ability.")
            if value and value not in ability_ids:
                raise ValueError(f"{what}: there's no ability {value}.")
            return value

        weapons = []
        for number_, weapon in enumerate(kit.get("Weapons") or [], 1):
            label = f"Weapon {number_}" + (f" ({weapon.get('Comment')})" if weapon.get("Comment") else "")
            ids = weapon.get("WeaponDefIds") or []
            if not ids:
                raise ValueError(f"{label} needs at least one weapon item.")
            for item in ids:
                if not isinstance(item, int) or (item not in items and item < CUSTOM_ITEMS):
                    raise ValueError(f"{label}: there's no item {item}.")
            weapons.append(ordered({"Comment": str(weapon.get("Comment") or "").strip() or None, "WeaponDefIds": ids,
                                    "BasicAbilityId": ability(weapon.get("BasicAbilityId"), f"{label}'s basic attack", True),
                                    "SpecialAbilityId": ability(weapon.get("SpecialAbilityId"), f"{label}'s special", False) or None},
                                   (like or {}).get("Weapons", [{}])[0] if like and like.get("Weapons") else None))
        energy = kit.get("Energy") or {}
        out = {
            "ProfileId": profile_id,
            "Comment": str(kit.get("Comment") or "").strip() or None,
            "AutoTargetReach": number(kit.get("AutoTargetReach", 7), "The reach", 1, 100),
            "BasicAutoTargetReach": number(kit["BasicAutoTargetReach"], "The basic attacks' reach", 1, 100) if kit.get("BasicAutoTargetReach") else None,
            "WieldType": kit.get("WieldType"),
            "BasicSlotDefId": whole(kit.get("BasicSlotDefId", BASIC_SLOT), "The basic slot"),
            "SpecialSlotDefId": whole(kit.get("SpecialSlotDefId", SPECIAL_SLOT), "The special slot"),
            "BasicRecastMs": whole(kit.get("BasicRecastMs", 600), "The basic recast", 100, 10000),
            "FallbackBasicAbilityId": ability(kit.get("FallbackBasicAbilityId"), "The bare-handed attack", False) or None,
            "Energy": ordered({"Max": whole(energy.get("Max", 100), "Most energy", 1, 1000), "RegenPerSecond": energy.get("RegenPerSecond")},
                              (like or {}).get("Energy")),
            "Weapons": weapons,
        }
        return ordered(out, like)

    def _check_profile(self, profile: dict, like: dict | None) -> dict:
        if not isinstance(profile, dict):
            raise ValueError("A job is an object.")
        items = self.ws.item_ids()
        defaults = profile.get("DefaultItems") or []
        for item in defaults:
            if not isinstance(item, int) or (item not in items and item < CUSTOM_ITEMS):
                raise ValueError(f"It starts with item {item}, which there isn't.")
        classes = profile.get("ItemClasses") or {}
        if not isinstance(classes, dict) or any(not isinstance(v, dict) for v in classes.values()):
            raise ValueError("ItemClasses is {class id: {Class, Unknown}}.")
        out = {"Comment": str(profile.get("Comment") or "").strip() or None, "Id": profile.get("Id")}
        for field in ("NameId", "DescriptionId"):
            out[field] = whole(profile.get(field) or 0, field)
        out["Type"] = whole(profile.get("Type", 1), "The category", 0, 255)
        for field in ("Icon", "BadgeImageSet", "ButtonImageSet", "AbilityBgImageSet", "Hint"):
            out[field] = whole(profile.get(field) or 0, field)
        out["MembersOnly"] = bool(profile.get("MembersOnly"))
        out["Trial"] = bool(profile.get("Trial"))
        out["Unknown"] = 4 if out["Trial"] else whole(profile.get("Unknown") or 0, "Unknown")
        out["Unknown2"] = whole(profile.get("Unknown2") or 0, "Unknown2")
        out["ItemClasses"] = classes
        if defaults or "DefaultItems" in profile:
            out["DefaultItems"] = defaults
        return ordered(out, like)

    # --- writes ----------------------------------------------------------------------------------------

    def save_ability(self, ability: dict, texts: dict | None) -> dict:
        """Adds an ability (Id null) or changes one; its texts become locale strings."""
        with self._lock:
            abilities, kits, profiles = self._load()
            index = next((i for i, a in enumerate(abilities) if a["Id"] == ability.get("Id")), None)
            if ability.get("Id") and index is None:
                raise KeyError(f"ability {ability['Id']}")
            checked = self._check_ability(ability, abilities[index] if index is not None else None)
            if index is None:
                checked = {"Id": max([a["Id"] for a in abilities] + [0]) + 1, **checked}
            strings = self.texts.apply([checked], [texts or {}], ABILITY_TEXTS)
            if index is None:
                abilities.append(checked)
            else:
                abilities[index] = checked
            written = gamedata.save(self.ws, [(self.abilities_path, abilities)])
        return {"ability": checked, "written": [p.name for p in written], "strings": len(strings)}

    def delete_ability(self, ability_id: int) -> dict:
        with self._lock:
            abilities, kits, profiles = self._load()
            if not any(a["Id"] == ability_id for a in abilities):
                raise KeyError(f"ability {ability_id}")
            if used := self._uses(kits, profiles).get(ability_id):
                jobs = sorted({u["job"] for u in used})
                raise ValueError(f"{' and '.join(jobs)} {'fights' if len(jobs) == 1 else 'fight'} with it: give "
                                 f"{'that job' if len(jobs) == 1 else 'them'} another ability first.")
            gamedata.save(self.ws, [(self.abilities_path, [a for a in abilities if a["Id"] != ability_id])])
        return {"deleted": ability_id}

    def save_job(self, data: dict) -> dict:
        """{profile, texts, kit (None: not a combat job), abilities: [{ability, texts}]}: a job, its kit and the
        abilities it fights with, in one go. A new job's profile and new abilities have no Id; the kit names a new
        ability by its negative stand-in id (-1, -2...), which becomes its new id."""
        profile = data.get("profile")
        if not isinstance(profile, dict):
            raise ValueError("A job needs its profile.")
        with self._lock:
            abilities, kits, profiles = self._load()
            original = next((p for p in profiles if p["Id"] == profile.get("Id")), None)
            if profile.get("Id") and original is None:
                raise KeyError(f"job {profile['Id']}")
            checked = self._check_profile(profile, original)
            if original is None:
                checked["Id"] = max([p["Id"] for p in profiles] + [NEW_JOBS - 1]) + 1
                checked = ordered(checked, profiles[0] if profiles else None)
            if not (data.get("texts") or {}).get("NameId") and not checked.get("NameId"):
                raise ValueError("Give the job a name.")

            # Abilities: changed ones replace theirs, new ones (no Id, or a negative stand-in) get ids in a block of
            # their own, after the kit's other abilities.
            given = data.get("abilities") or []
            by_id = {a["Id"]: i for i, a in enumerate(abilities)}
            kit = data.get("kit")
            existing_ids = [a["ability"].get("Id") for a in given if (a["ability"].get("Id") or 0) > 0]
            existing_ids += [i for i in _kit_abilities(kit or {}) if i > 0]
            taken = set(by_id)
            block = max((i for i in existing_ids if i in taken), default=None)
            next_id = block + 1 if block else (max(taken | {0}) // 1000 + 1) * 1000 + 10
            stand_ins, changed_abilities, ability_texts = {}, [], []
            for entry in given:
                ability = dict(entry.get("ability") or {})
                given_id = ability.get("Id") or 0
                if given_id > 0 and given_id not in by_id:
                    raise KeyError(f"ability {given_id}")
                fresh = given_id <= 0
                checked_ability = self._check_ability(ability, None if fresh else abilities[by_id[given_id]])
                if fresh:
                    while next_id in taken:
                        next_id += 1
                    checked_ability = {"Id": next_id, **{k: v for k, v in checked_ability.items() if k != "Id"}}
                    taken.add(next_id)
                    if given_id < 0:
                        stand_ins[given_id] = next_id
                changed_abilities.append(checked_ability)
                ability_texts.append(entry.get("texts") or {})

            ability_ids = taken
            kit_index = next((i for i, k in enumerate(kits) if k["ProfileId"] == checked["Id"]), None)
            checked_kit = None
            if kit:
                kit = _swap_ids(kit, stand_ins)
                checked_kit = self._check_kit(kit, checked["Id"], ability_ids, kits[kit_index] if kit_index is not None else None)

            # All checked: names, then the files.
            strings = self.texts.apply([checked, *changed_abilities], [data.get("texts") or {}, *ability_texts], JOB_TEXTS)
            for ability in changed_abilities:
                if ability["Id"] in by_id:
                    abilities[by_id[ability["Id"]]] = ability
                else:
                    abilities.append(ability)
            if original is None:
                profiles.append(checked)
            else:
                profiles[profiles.index(original)] = checked
            if checked_kit is not None:
                if kit_index is None:
                    kits.append(checked_kit)
                else:
                    kits[kit_index] = checked_kit
            elif kit_index is not None:
                kits.pop(kit_index)
            _check_kits(kits, {a["Id"] for a in abilities})
            written = gamedata.save(self.ws, [(self.abilities_path, abilities), (self.kits_path, kits), (self.profiles_path, profiles)])
        return {"id": checked["Id"], "abilityIds": {str(k): v for k, v in stand_ins.items()}, "written": [p.name for p in written],
                "strings": len(strings), "new": original is None}

    def delete_job(self, profile_id: int) -> dict:
        """Deletes a job made here, its kit, and the abilities it fought with that no other job does."""
        if profile_id < NEW_JOBS:
            raise ValueError("Only jobs made here can be deleted; the game's are named by achievements, recipes and minigames.")
        with self._lock:
            abilities, kits, profiles = self._load()
            if not any(p["Id"] == profile_id for p in profiles):
                raise KeyError(f"job {profile_id}")
            kit = next((k for k in kits if k["ProfileId"] == profile_id), None)
            kits = [k for k in kits if k["ProfileId"] != profile_id]
            still = {i for k in kits for i in _kit_abilities(k)}
            gone = {i for i in (_kit_abilities(kit) if kit else []) if i not in still}
            gamedata.save(self.ws, [(self.profiles_path, [p for p in profiles if p["Id"] != profile_id]), (self.kits_path, kits),
                                    (self.abilities_path, [a for a in abilities if a["Id"] not in gone])])
        return {"deleted": profile_id, "abilities": sorted(gone)}


def _kit_abilities(kit: dict) -> list:
    ids = [kit.get("FallbackBasicAbilityId") or 0]
    for weapon in kit.get("Weapons") or []:
        ids += [weapon.get("BasicAbilityId") or 0, weapon.get("SpecialAbilityId") or 0]
    return [i for i in dict.fromkeys(ids) if i]


def _swap_ids(kit: dict, stand_ins: dict) -> dict:
    """kit with each negative stand-in ability id swapped for the new ability's id."""
    swap = lambda value: stand_ins.get(value, value) if isinstance(value, int) else value
    kit = dict(kit, FallbackBasicAbilityId=swap(kit.get("FallbackBasicAbilityId")))
    kit["Weapons"] = [dict(w, BasicAbilityId=swap(w.get("BasicAbilityId")), SpecialAbilityId=swap(w.get("SpecialAbilityId")))
                      for w in kit.get("Weapons") or []]
    for value in _kit_abilities(kit):
        if value < 0:
            raise ValueError(f"The kit names a new ability ({value}) that isn't in the save.")
    return kit


def _check_kits(kits: list, ability_ids: set):
    """What the Gateway checks as it starts (ResourceManager): every kit's abilities exist."""
    for kit in kits:
        missing = [i for i in _kit_abilities(kit) if i not in ability_ids]
        if missing:
            raise ValueError(f"Job {kit['ProfileId']}'s kit names abilities that don't exist: {', '.join(map(str, missing))}.")
