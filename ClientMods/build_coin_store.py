#!/usr/bin/env python3
"""Fills the coin store (src/Resources/CoinStoreItems.json) with every item players can own, and prices it.

The coin store started as the original game's list, plus the Station Cash Marketplace's items. Most of what
players find, win or buy from NPCs in game wasn't in it: job gear, jewelry, tools, food, potions, crafting and
farming supplies, pet supplies, recipes, kart parts, karts, style cards, houses and wallpapers. This script adds
every one of them that players can see, and sets one price list for the whole store.

What goes in:
  - Items with an English name (Locale/en_us_data.*) and an icon, whose name isn't a developer's ("Test",
    "DEPRECATED", "(PH)", snake_case, ...), in a store category that has a price below.
  - One card per item: the client has a definition for each colour of most items, which the store already sells
    as one card with a colour choice (a tint group from the client's TintGroupMappings.txt). An item already in
    the store in any colour isn't added again. Gear for each rank is its own item.
  - Pets need a model (Models.txt), mounts a mount definition (Mounts.json) and houses a house (Houses.json),
    since the store can't hand them over otherwise.

Custom items (CustomItemManager, 9000000 and up) are kept in the database, not ClientItemDefinitions.json, so their
cards (the FR MCP Hover-Car's, 9000006) are added to CoinStoreItems.json by hand. The script keeps them, and they
cost what their base item costs unless they override "cost".

What stays out, because players earn it rather than buy it, or it does nothing here: collection pieces and junk
(sell-only loot), titles, coin bundles, reward boxes and loot bags (they open into coins), quest packs, the guild
name change and bank upgrades, and character creation's own hair, skin and outfit choices.

Prices: every store item costs its category's price below, and gear (clothes, armour, weapons, tools, jewelry,
shards, potions and food) costs half as much again for each rank it needs, so rank 20 gear costs 10.5 times a
rank 1 piece. Houses' working fixtures (HousingFixtures.json: launch pads, elevators, teleporters, music blocks)
cost FIXTURE_PRICE. Prices are rounded to steps of 5, 25, 50 or 500. Everyone pays the member price, so the
member discount goes and Cost is the price; every colour of an item costs the same, wherever it's sold. An item
sells back for at most a quarter of its price.

Example:
  python build_coin_store.py --client "%LOCALAPPDATA%/OSFRLauncher/Servers/OSFR Public Server/Client"
"""

import json
import re
from argparse import ArgumentParser
from collections import Counter, defaultdict
from pathlib import Path

from extract_names import load_locale, lookup2

script_dir = Path(__file__).resolve().parent
resources_dir = script_dir.parent / "src" / "Resources"

# Store category: (price in coins, whether the price rises with the rank the item needs).
CATEGORY_PRICES = {
    # Clothes & Armor
    101: (200, True),     # Hats
    102: (150, True),     # Gloves
    103: (250, True),     # Shirts & Dresses
    104: (200, True),     # Pants
    105: (200, True),     # Shoes
    106: (150, True),     # Miscellaneous
    2053: (200, True),    # Headwear
    201: (1000, False),   # Costumes
    # Weapons and Tools
    207: (300, True),     # Weapons & Tools
    107: (250, True),     # Tools
    12: (250, False),     # Extras
    # Jewelry & Shards
    108: (250, True),     # Jewelry
    121: (200, True),     # Power Shards
    124: (200, True),     # Flair Shards
    # Supplies
    3: (100, False),      # Keys & Coin
    9: (50, True),        # Potions
    10: (50, True),       # Food & Drink
    14: (75, True),       # Battle Items
    2089: (500, False),   # Boombox
    # Crafts
    16: (25, False),      # Supplies
    17: (250, False),     # Crafting (recipes)
    # Extras
    29: (100, False),     # Extras
    # Pets
    2: (2500, False),     # Pets
    200: (5000, False),   # Pet Pals
    6: (150, False),      # Clothing
    2102: (500, False),   # Dog & Cat Costumes
    7: (50, False),       # Supplies
    # Housing
    4: (25, False),       # Building Blocks
    147: (25, False),     # Blocks
    51: (500, False),     # Floors
    55: (500, False),     # Roofs
    59: (500, False),     # Walls and Roofs
    52: (150, False),     # Wall Hangings
    53: (250, False),     # Furniture & Decorations
    54: (200, False),     # Decorations
    56: (300, False),     # Exterior Decorations
    57: (500, False),     # Outdoor Equipment
    2125: (25000, False), # Houses
    2206: (25000, False), # Lots & Farms
    # Farming
    301: (50, False),     # Soil
    302: (25, False),     # Seed
    305: (10, False),     # Samples
    306: (25, False),     # Harvested Crops
    307: (50, False),     # Supplies
    308: (100, False),    # Protection
    # Karts
    129: (250, False),    # Engine
    130: (250, False),    # Turbo
    131: (250, False),    # Steering
    132: (250, False),    # Traction
    133: (250, False),    # Mod A
    134: (250, False),    # Mod B
    136: (250, False),    # Decals
    139: (250, False),    # Chassis
    2103: (250, False),   # Race Kart Parts
    2104: (250, False),   # Derby Kart Parts
    2247: (5000, False),  # Race Karts
    2248: (5000, False),  # Derby Karts
    # Style Cards
    141: (500, False),    # Hair Styles
    142: (250, False),    # Hair Colors
    143: (150, False),    # Eye Colors
    144: (250, False),    # Facial Hair Styles
    145: (250, False),    # Face Paints
    146: (250, False),    # Wing Patterns
    # Rides and Pets
    2266: (10000, False), # Winged
    2267: (10000, False), # Mechanical
    2268: (10000, False), # Exotic
    2269: (10000, False), # Non-Winged
    # Card Games
    2083: (1000, False),  # FR Trading Card Game
    2234: (1000, False),  # Treasure War
    # Goods and Services
    2243: (250, False),
}

# The Mysterious Merchant's category holds all sorts; its items cost what they would elsewhere.
MYSTERIOUS_MERCHANT_CATEGORY = 12001
# Where a mount goes when no mount in the store shares its model.
NO_WINGS_CATEGORY = 2269

FIXTURE_PRICE = 2500
RANK_STEP = 0.5
RESELL_SHARE = 4

# Store categories for item types whose definitions carry none.
TYPE_CATEGORIES = {3: 6, 4: 7, 5: 17, 7: 2083, 9: 2103, 10: 2104}
# Items sitting in a parent category, which the store doesn't list, go to one of its children.
CATEGORY_MOVES = {8: 3}

PET_TYPE, PET_CLOTHING_TYPE, KART_TYPE, STYLE_CARD_TYPE, HOUSE_TYPE, WALLPAPER_TYPE, MOUNT_TYPE = 2, 3, 11, 12, 16, 17, 19
# Titles, coin bundles, reward boxes, the guild name change and bank upgrades.
LEFT_OUT_TYPES = {6, 13, 14, 18, 27, 30}
# Collection pieces and junk.
LEFT_OUT_CATEGORIES = {19, 43}
WEARABLE_TYPES = {1, PET_CLOTHING_TYPE}

DEVELOPER_NAME = re.compile(
    r"\btest|deprecated|do not|don't use|not used|unused|placeholder|\btbd|\btemp\b|debug|\bdev\b|\bqa\b|\bnpc\b"
    r"|\bgm\b|^pet: |\(ph\)|\bdummy\b|quest pack|mystery reward|print voucher"
    # Tiers and slots with no name yet: "Alchemist L3 Head", "Archer M1 Quiver", "Pet Trainer chest mudpie".
    r"|\b[LM]\d\b|\b(?-i:head|chest|legs|feet|hands|shoulders?) (?-i:[a-z]+)$",
    re.IGNORECASE)
# Gear whose name is a string meant for something else.
MISNAMED = {"Exit Decorate Mode", "Small Dog Pet"}
PLURAL_NAME = re.compile(r"\{n\}(.*?)\{pl=")


def read_json(path):
    return json.loads(path.read_text("utf-8-sig"))


def write_json(path, data):
    """Writes data as the resources are laid out: two-space indents, CRLF, no BOM, nothing escaped."""
    text = json.dumps(data, ensure_ascii=False, indent=2).replace("\n", "\r\n")
    path.write_bytes(text.encode("utf-8"))


def read_table(path):
    """Rows of a ^-separated client table, header and comments skipped."""
    for line in path.read_text("utf-8-sig", "replace").splitlines():
        if line and not line.startswith("#"):
            yield line.rstrip("^").split("^")


def nice_price(price):
    step = 5 if price < 100 else 25 if price < 1000 else 50 if price < 10000 else 500
    return max(step, int(price / step + 0.5) * step)


def set_after(item, key, value, before):
    """Sets item[key], putting a new key where the definition files have it: ahead of the first of before."""
    if key in item:
        item[key] = value
        return
    entries = list(item.items())
    index = next((i for i, (name, _) in enumerate(entries) if name in before), len(entries))
    entries.insert(index, (key, value))
    item.clear()
    item.update(entries)


# BaseItemDefinition's fields after Cost, in order.
AFTER_COST = ("Class", "MaxStackSize", "ProfileOverride", "Slot", "NoTrade", "SingleUse", "ModelName", "GenderUsage",
              "TextureAlias", "CategoryId", "MembersOnly", "NonMiniGame", "NoSale", "WeaponTrailEffectId",
              "CompositeEffectId", "PowerRating", "MinProfileRank", "Rarity", "TintAlias", "IsTintable",
              "ForceDisablePreview", "MemberDiscount", "RaceSetId", "VipRankRequired", "ClientEquipReqSetId",
              "ResellValue", "Param1", "Param2", "Unknown5", "Stats", "Abilities")


def main():
    parser = ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--client", type=Path, required=True, help="Free Realms client folder (contains Locale)")
    args = parser.parse_args()

    locale = load_locale(args.client)

    def item_name(item):
        text = locale.get(lookup2(f"Global.Text.{item.get('NameId', 0)}".encode()), "") if item.get("NameId", 0) > 0 else ""
        match = PLURAL_NAME.match(text)
        return (match.group(1) if match else text).strip()

    items_path = resources_dir / "ClientItemDefinitions.json"
    store_path = resources_dir / "CoinStoreItems.json"
    items = read_json(items_path)
    store = read_json(store_path)
    items_by_id = {item["Id"]: item for item in items}

    house_items = {item_id: house["Id"] for house in read_json(resources_dir / "Houses.json") for item_id in house["ItemDefinitionIds"]}
    mount_ids = {mount["Id"] for mount in read_json(resources_dir / "Mounts.json")}
    fixture_items = {item_id for fixture in read_json(resources_dir / "HousingFixtures.json") for item_id in fixture["ItemDefinitionIds"]}
    # Loot bags open into coins and prizes, like reward boxes.
    loot_abilities = {ability["AbilityId"] for ability in read_json(resources_dir / "ItemAbilities.json") if ability.get("RewardTable")}
    model_files = {row[1].lower() for row in read_table(resources_dir / "Models.txt") if len(row) > 1}
    categories = {int(row[0]) for row in read_table(resources_dir / "ItemCategories.txt")}
    tint_groups = defaultdict(list)
    for row in read_table(args.client / "Resources" / "TintGroupMappings.txt"):
        tint_groups[int(row[0])].append((int(row[2]), int(row[1])))
    tint_groups = {group: [tint for _, tint in sorted(rows)] for group, rows in tint_groups.items()}

    def left_out(item):
        """Why an item can't go in the store, or None."""
        name = item_name(item)
        if not name or DEVELOPER_NAME.search(name) or ("_" in name and " " not in name) or name in MISNAMED:
            return "no player name"
        if not item.get("Icon", {}).get("Id"):
            return "no icon"
        if (item.get("Type", 0) in LEFT_OUT_TYPES or item.get("CategoryId") in LEFT_OUT_CATEGORIES
                or item.get("ActivatableAbilityId") in loot_abilities):
            return "earned, not bought"
        if item.get("Type") == PET_TYPE and (item.get("ModelName") or "").lower() not in model_files:
            return "pet without a model"
        if item.get("Type") == MOUNT_TYPE and item.get("Param1") not in mount_ids:
            return "mount without a definition"
        if item.get("Type") == HOUSE_TYPE and item["Id"] not in house_items:
            return "house without a zone"
        return None

    def store_category(item):
        """The store category an item belongs in, or None."""
        item_type, category = item.get("Type", 0), item.get("CategoryId") or 0
        category = CATEGORY_MOVES.get(category, category)
        if item_type == PET_TYPE:
            category = category if category == 200 else 2
        elif item_type == KART_TYPE:
            # Derby karts are wreckers and crushers; the rest race.
            category = 2248 if re.search(r"wrecker|crusher", item_name(item), re.IGNORECASE) else 2247
        elif item_type == HOUSE_TYPE:
            category = 2206 if re.search(r"\blot\b|\bfarm\b", item_name(item), re.IGNORECASE) else 2125
        elif item_type == WALLPAPER_TYPE:
            category = category if category in (51, 55, 59) else 59
        elif item_type == MOUNT_TYPE:
            mount = store_mounts.get((item.get("ModelName") or "").lower())
            category = mount["CategoryId"] if mount else NO_WINGS_CATEGORY
        elif item_type == STYLE_CARD_TYPE:
            # Without a category they're character creation's own choices.
            category = category if 141 <= category <= 146 else 0
        elif item_type in TYPE_CATEGORIES:
            category = TYPE_CATEGORIES[item_type]
        return category if category in CATEGORY_PRICES and category in categories else None

    def family(item):
        """What tells items apart for players: every colour of an item is the same item."""
        key = (item.get("Type", 0), item_name(item), item.get("Class", 0), item.get("MinProfileRank", 0))
        if item.get("Type", 0) in WEARABLE_TYPES:
            key += (item.get("Slot", 0), item.get("GenderUsage", 0), (item.get("ModelName") or "").lower(),
                    item.get("TextureAlias"), item.get("Icon", {}).get("Id", 0))
        elif item.get("Type") == PET_TYPE:
            # No tint group holds pets' coats, so each coat is its own pet.
            key += ((item.get("ModelName") or "").lower(), item.get("TextureAlias"), item.get("TintAlias"),
                    item.get("Icon", {}).get("Id", 0), item.get("Icon", {}).get("TintId", 0))
        elif item.get("Type") == MOUNT_TYPE:
            key += (item.get("Param1", 0),)
        elif item.get("Type") == HOUSE_TYPE:
            key = (HOUSE_TYPE, house_items.get(item["Id"], -item["Id"]))
        return key

    families = defaultdict(list)
    for item in items:
        if left_out(item) is None:
            families[family(item)].append(item)

    # Custom items live in the database, not ClientItemDefinitions.json; their cards are left as they are.
    def defined(entries):
        return [entry for entry in entries if entry["Id"] in items_by_id]

    # Families with a colour already in the store are in it.
    in_store = {family(items_by_id[entry["Id"]]) for entry in defined(store)}
    # A new mount takes the category and colours of a mount on the same model.
    store_mounts = {}
    for entry in defined(store):
        item = items_by_id[entry["Id"]]
        if item.get("Type") == MOUNT_TYPE and entry.get("CategoryId") in CATEGORY_PRICES:
            store_mounts.setdefault((item.get("ModelName") or "").lower(), entry)

    # New cards go after the store's own, in id order.
    added = Counter()
    for key, members in sorted(families.items(), key=lambda pair: min(item["Id"] for item in pair[1])):
        if key in in_store:
            continue
        category = store_category(members[0])
        if category is None:
            continue
        tints = {item.get("Icon", {}).get("TintId", 0) for item in members}
        entry = {"Id": min(item["Id"] for item in members), "CategoryId": category}
        mount = store_mounts.get((members[0].get("ModelName") or "").lower()) if key[0] == MOUNT_TYPE else None
        if mount and mount.get("TintGroupId"):
            entry["TintGroupId"] = mount["TintGroupId"]
        # More than one colour: sell one card with the client's tint group that has the most of them.
        elif len(tints) > 1 and members[0].get("TintAlias"):
            group, shared = max(((group, [tint for tint in group_tints if tint in tints])
                                 for group, group_tints in tint_groups.items()), key=lambda pair: (len(pair[1]), -pair[0]))
            if len(shared) > 1:
                entry["Id"] = min(item["Id"] for item in members if item.get("Icon", {}).get("TintId") == shared[0])
                entry["TintGroupId"] = group
        store.append(entry)
        added[category] += 1

    def price(item, category):
        if category == MYSTERIOUS_MERCHANT_CATEGORY:
            category = store_category(item)
            if category is None:
                return None
        if category not in CATEGORY_PRICES:
            return None
        if item["Id"] in fixture_items:
            return FIXTURE_PRICE
        base, ranked = CATEGORY_PRICES[category]
        rank = max(1, item.get("MinProfileRank") or 1)
        return nice_price(base * (1 + RANK_STEP * (rank - 1)) if ranked else base)

    # Every colour of a store item costs the same, wherever it's sold.
    priced = {}
    for entry in defined(store):
        item = items_by_id[entry["Id"]]
        cost = price(item, entry.get("CategoryId", 0))
        if cost is None:
            continue
        members = families.get(family(item), []) if left_out(item) is None else []
        for member in members or [item]:
            priced[member["Id"]] = cost

    changed = 0
    for item_id, cost in priced.items():
        item = items_by_id[item_id]
        before = (item.get("Cost", 0), item.get("MemberDiscount", 0), item.get("ResellValue", 0))
        set_after(item, "Cost", cost, AFTER_COST)
        if item.get("MemberDiscount"):
            item["MemberDiscount"] = 0
        if item.get("ResellValue", 0) > cost // RESELL_SHARE:
            item["ResellValue"] = cost // RESELL_SHARE
        changed += before != (item.get("Cost", 0), item.get("MemberDiscount", 0), item.get("ResellValue", 0))

    write_json(store_path, store)
    write_json(items_path, items)

    print(f"Added {sum(added.values())} items to the coin store, which now sells {len(store)}:")
    for category, count in sorted(added.items(), key=lambda pair: -pair[1]):
        print(f"  {category:5}: {count}")
    print(f"Priced {len(priced)} item definitions ({changed} changed)")


if __name__ == "__main__":
    main()
