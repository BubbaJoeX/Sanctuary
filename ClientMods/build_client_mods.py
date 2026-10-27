#!/usr/bin/env python3
"""Builds Sanctuary's client mods into src/Resources/ClientMods.

The WebAPI asset endpoint serves every file in that folder and patches it into the asset
manifest it hands the client. The client only downloads assets missing from its local packs,
so mods are published under new names, and install_client_mods.py points the client's UI at them.

Mods:
  sanctuaryChatWindowCtrl.gfx (+ .lst.z) - chat window with the referee (GM) panel, popped in from a Referee
                                           tab on the screen edge for referees (chatWindowCtrl/gm_panel.as).
  sanctuaryGameDock.gfx (+ .lst.z)       - main hub with the Marketplace icon as the Coin Shop
                                           (gamedock/coin_shop_button.as appended).
  sanctuaryDialog.gfx (+ .lst.z)         - NPC dialog that stays hidden when sent empty, so GM conversations
                                           get its camera zoom with the panel's text (dialog/dialog_hide.as).
  sanctuaryHousingEditPanel.gfx (+ .lst.z) - decorate-mode furniture tray with 10 items per page and a
                                           search box (housing/tray.as).
  sanctuaryLoadingScreen.gfx (+ .lst.z)  - loading screen with "Open Source Free Realms" curved around the
                                           OSFR tree (loadingscreen/background.py), keeping the tip and bar.
  sanctuaryAtlas.gfx (+ .lst.z)          - the atlas with a Lavender Coast page (atlas/lavendercoast.png), opened from
                                           the Sacred Grove page's Lavender Coast label (atlas/lavender_coast.as).
  sanctuaryLavenderCoastNotification.gfx (+ .lst.z) - the "Now entering" badge for Lavender Coast: Merry Vale's with
                                           its art repainted (notifications/badges.py) and the region's name, played
                                           by the client extensions (lua/client_extensions.lua).

  Station Cash and membership no longer exist:
  sanctuaryGameDock.gfx                  - the Marketplace icon is labelled Coin Shop (gamedock/coin_shop_button.as).
  sanctuaryMarketplace.gfx               - the Marketplace window only shows its coin store page
                                           (marketplace/coin_store_only.as).
  sanctuaryNavigation.gfx                - the Marketplace tab bar only has the Coin Shop tab
                                           (navigation/coin_store_tabs.as).
  sanctuaryMysteryRewardBrowser(Small).gfx, sanctuaryPaidNameChange.gfx, sanctuaryUnifiedMessageWindow.gfx,
  sanctuaryAboutMe.gfx, sanctuaryWelcome.gfx, sanctuaryActivityPortal.gfx, sanctuaryHousingPreview.gfx,
  sanctuaryStorage.gfx                   - no Station Cash wallets or Add Station Cash buttons
                                           (<movie>/no_station_cash.as).

The stock movies are read from your Free Realms client's local asset packs (--client), or from a
local copy of the asset server (--assets). Nothing from the game is stored in this repository.

Requires JPEXS FFDec (ffdec.jar), Java and Pillow. See README.md.

Example:
  python build_client_mods.py --client "%LOCALAPPDATA%/OSFRLauncher/Servers/OSFR Public Server/Client" --ffdec path/to/ffdec.jar
"""

import io
import re
import shutil
import struct
import subprocess
import sys
import tempfile
import zlib
from argparse import ArgumentParser
from pathlib import Path

from PIL import Image

from hud import reskin
from loadingscreen import background
from notifications import badges

script_dir = Path(__file__).resolve().parent
output_dir = script_dir.parent / "src" / "Resources" / "ClientMods"

COMPRESSED_MAGIC = 0xA1B2C3D4

NO_STATION_CASH = "no_station_cash.as"

# (stock movie, published name, script inside the movie to append to, patch sources, recolored symbols to add)
MODS = [
    ("chatWindowCtrl", "sanctuaryChatWindowCtrl", "\\frame_2\\DoAction", [script_dir / "chatWindowCtrl" / "gm_panel.as"], []),
    ("gamedock", "sanctuaryGameDock", "\\frame_1\\DoAction",
     [script_dir / "gamedock" / "coin_shop_button.as"], []),
    ("dialog", "sanctuaryDialog", "\\frame_1\\DoAction", [script_dir / "dialog" / "dialog_hide.as"], []),
    ("housingEditPanel", "sanctuaryHousingEditPanel", "\\frame_1\\DoAction", [script_dir / "housing" / "tray.as"], []),
    ("marketplace", "sanctuaryMarketplace", "\\frame_1\\DoAction", [script_dir / "marketplace" / "coin_store_only.as"], []),
    ("navigation", "sanctuaryNavigation", "\\frame_1\\DoAction", [script_dir / "navigation" / "coin_store_tabs.as"], []),
    ("MysteryRewardBrowser", "sanctuaryMysteryRewardBrowser", "\\frame_1\\DoAction",
     [script_dir / "MysteryRewardBrowser" / NO_STATION_CASH], []),
    # Same side panel as MysteryRewardBrowser.
    ("MysteryRewardBrowserSmall", "sanctuaryMysteryRewardBrowserSmall", "\\frame_1\\DoAction",
     [script_dir / "MysteryRewardBrowser" / NO_STATION_CASH], []),
    ("PaidNameChange", "sanctuaryPaidNameChange", "\\frame_1\\DoAction", [script_dir / "PaidNameChange" / NO_STATION_CASH], []),
    ("UnifiedMessageWindow", "sanctuaryUnifiedMessageWindow", "\\frame_1\\DoAction",
     [script_dir / "UnifiedMessageWindow" / NO_STATION_CASH], []),
    ("aboutme", "sanctuaryAboutMe", "\\frame_1\\DoAction", [script_dir / "aboutme" / NO_STATION_CASH], []),
    ("welcome", "sanctuaryWelcome", "\\frame_1\\DoAction",
     [script_dir / "welcome" / NO_STATION_CASH, script_dir / "welcome" / "home.as"], []),
    ("ActivityPortal", "sanctuaryActivityPortal", "\\frame_1\\DoAction", [script_dir / "ActivityPortal" / NO_STATION_CASH], []),
    ("HousingPreview", "sanctuaryHousingPreview", "\\frame_1\\DoAction", [script_dir / "HousingPreview" / NO_STATION_CASH], []),
    ("storage", "sanctuaryStorage", "\\frame_1\\DoAction", [script_dir / "storage" / NO_STATION_CASH], []),
    ("MyStuffBrowser", "sanctuaryMyStuffBrowser", "\\frame_1\\DoAction", [script_dir / "MyStuffBrowser" / "main_menu.as"], []),
    ("atlas", "sanctuaryAtlas", "\\frame_1\\DoAction", [script_dir / "atlas" / "lavender_coast.as"], []),
    # HUD pieces that are only repainted (RESKINS), with no script changes.
    ("FRUtilNavigation", "sanctuaryFRUtilNavigation", None, [], []),
    ("mini_map", "sanctuaryMiniMap", None, [], []),
    ("social", "sanctuarySocial", None, [], []),
]

# Stock movie -> [(external texture, texture to use instead)]. Station Cash no longer exists, so the
# hub's Marketplace (now Coin Shop) icon shows the coin from the coin store's wallet instead of the SC logo.
TEXTURE_SWAPS = {
    "gamedock": [("img9752574847671325523.dds", "img11900526720825140648.dds")],
    # The welcome screen's header wallet, which now shows coins.
    "welcome": [("img9752574847671325523.dds", "img11900526720825140648.dds")],
}

# Stock movie -> [(client texture, export name, size, hue shift in degrees)]. Each texture is cropped to
# its art, fitted into a size x size square and embedded as an exported sprite the patch scripts attach.
TEXTURE_SYMBOLS = {
    # The referee panel's title and its edge tab: the hub tree in the Referee Tools purple.
    "chatWindowCtrl": [("img7070878317473001703.dds", "sanctuaryIconRefereeTree", 64, 155)],
    # The welcome screen (welcome/home.as): Main Menu tile art for the quick links and Referee Picks, the
    # coin store coin, the friends icon, and the hub tree in the Referee Tools purple.
    "welcome": [
        ("img11740438261774166106.dds", "sanctuaryIconHouse", 96, 0),
        ("img3353027197221927245.dds", "sanctuaryIconBag", 96, 0),
        ("img1837001925841496856.dds", "sanctuaryIconJournal", 96, 0),
        ("img15906260194121039444.dds", "sanctuaryIconTrophy", 96, 0),
        ("img1469495624995028744.dds", "sanctuaryIconBook", 96, 0),
        ("img8405532797012043402.dds", "sanctuaryIconPaw", 96, 0),
        ("img12661814977694140028.dds", "sanctuaryIconCoin", 96, 0),
        ("img1084093282544248052.dds", "sanctuaryIconFriends", 64, 0),
        ("img7070878317473001703.dds", "sanctuaryIconTree", 64, 0),
        ("img7070878317473001703.dds", "sanctuaryIconRefereeTree", 64, 155),
        ("img16372258640993360071.dds", "sanctuaryIconWheel", 96, 0),
        ("img3694898642873637100.dds", "sanctuaryIconScroll", 64, 0),
    ],
    # The Main Menu (MyStuffBrowser/main_menu.as): its own tile art, with the coin store coin for Shop.
    "MyStuffBrowser": [
        ("img12661814977694140028.dds", "sanctuaryMenuCoin", 96, 0),
        ("img15906260194121039444.dds", "sanctuaryMenuTrophy", 96, 0),
        ("img1837001925841496856.dds", "sanctuaryMenuJournal", 96, 0),
        ("img3353027197221927245.dds", "sanctuaryMenuBag", 96, 0),
        ("img1469495624995028744.dds", "sanctuaryMenuBook", 96, 0),
        ("img5645809723664368221.dds", "sanctuaryMenuProfile", 96, 0),
        ("img8405532797012043402.dds", "sanctuaryMenuPaw", 96, 0),
        ("img6228164867429240843.dds", "sanctuaryMenuSaddle", 96, 0),
        ("img11740438261774166106.dds", "sanctuaryMenuHouse", 96, 0),
        ("img10946043591668366760.dds", "sanctuaryMenuPalette", 96, 0),
    ],
}

# Stock movie -> [(image file, export name)]: pictures of our own, embedded like TEXTURE_SYMBOLS at their pixel size.
IMAGE_SYMBOLS = {
    # Lavender Coast's atlas page, at the size the stock pages show their paintings (1653x843).
    "atlas": [(script_dir / "atlas" / "lavendercoast.png", "sanctuaryLavenderCoastPage")],
}

# Stock movie -> [(export name, text, font size, gradient top, gradient bottom)]: text in the loading screen's
# lettering (the client's Free Realms font, outlined and gradient filled), embedded like TEXTURE_SYMBOLS.
TEXT_SYMBOLS = {
    # The home screen's Play button.
    "welcome": [("sanctuaryPlayLabel", "PLAY!", 40, (255, 246, 196), (255, 168, 36))],
    # The Main Menu's title.
    "MyStuffBrowser": [("sanctuaryMainMenuTitle", "Main Menu", 46, (255, 246, 196), (255, 168, 36))],
}

# Stock movie -> root timeline depths whose placements are dropped: plain shapes, which ActionScript can't hide.
REMOVED_DEPTHS = {
    # The member badge's orange plate.
    "welcome": [115],
}

# Stock movie -> {client texture: hud/reskin.py recipe}. The texture is repainted at the size the movie draws
# it and embedded in its place, so the HUD keeps its layout: purple, brown and beige chrome becomes navy glass,
# frosted glass and gold, and the minimap gets a new ring.
RESKINS = {
    # The dock: title plate, slot frames, action bar base and its round slots (health and mana keep their colors).
    "gamedock": {
        "img7261148070336999820.dds": "navy", "img1059227586001590212.dds": "navy",
        "img11007847080817277960.dds": "navy", "img15087113162217541573.dds": "navy",
        "img12347292446648657273.dds": "navy", "img4008478653138261676.dds": "navy",
        "img2547489642989162929.dds": "glass", "img571722968246882682.dds": "glass",
        "img17894775867228385230.dds": "glass", "img13179357351024049733.dds": "glass",
        "img3433151271243245196.dds": "glass", "img3660832219945813116.dds": "glass",
    },
    # The top utility bar.
    "FRUtilNavigation": {"img3488721174386834823.dds": "navy"},
    # The minimap: its backdrop, ring and zoom buttons.
    "mini_map": {
        "img4695131534032394228.dds": "navy", "img10708622630870636103.dds": "ring",
        "img13179357351024049733.dds": "glass", "img3433151271243245196.dds": "glass",
    },
    # The friends panel: purple frames, the brown header and orange buttons, and the beige buttons.
    "social": {
        "img14307746584820960033.dds": "navy", "img3118227998292618224.dds": "navy",
        "img7481755510491628842.dds": "navy", "img16725234778555798738.dds": "navy",
        "img5060708491987452422.dds": "navy", "img1059227586001590212.dds": "navy",
        "img17324286690744305172.dds": "navy", "img835877099391186097.dds": "navy",
        "img11341374780922977356.dds": "navy", "img3027467735542961691.dds": "navy",
        "img13788486141241226455.dds": "navy", "img15103948945701512237.dds": "navy",
        "img15087759986106883599.dds": "navy", "img6055351301255611254.dds": "navy",
        "img8512871844800064570.dds": "navy", "img17687862331118531982.dds": "navy",
        "img10601195574189710825.dds": "navy", "img15913745394084125270.dds": "navy",
        "img14948868264591843625.dds": "navy",
        "img16504749052500928201.dds": "gold", "img4848014849839955794.dds": "gold",
        "img11313097541566256479.dds": "gold",
        "img17721979831663157310.dds": "glass", "img11997853953748690251.dds": "glass",
        "img9797266604789056704.dds": "glass", "img13179357351024049733.dds": "glass",
        "img3433151271243245196.dds": "glass", "img626770873669363768.dds": "glass",
        "img12455688932877814193.dds": "glass", "img1810748524064661009.dds": "glass",
    },
}

# Region badges, the emblem notifications.swf drops in with "Now entering ...", for Sanctuary's own areas
# (lua/client_extensions.lua plays them): (stock badge, published name, region name, font height in twips,
# {stock name colour: colour}, notifications/badges.py painter). Each keeps the stock badge's animation, with its
# art repainted and the region's name in place of the stock one's (a localized string key).
REGION_BADGES = [
    # Merry Vale's: its name in cream with an orange outline over an orange shadow outlined in purple becomes
    # lavender white over purple and plum, at Seaside Village's smaller size, which fits the longer name.
    ("merryvale_notification", "sanctuaryLavenderCoastNotification", "Lavender Coast", 1500,
     {(254, 243, 199): (250, 244, 255), (198, 79, 0): (112, 58, 176), (102, 0, 153): (48, 22, 82)}, "lavender_coast"),
]

LOADING_SCREEN = ("loadingscreen", "sanctuaryLoadingScreen")
# Root timeline depths of loadingscreen.gfx that stay: the backdrop, its twinkle, the tip text and the
# loading bar (track, barMask, fill, spinner, queue). The characters, logo and vines go.
LOADING_SCREEN_DEPTHS = {1, 2, 4, 72, 73, 75, 78, 82}
LOADING_SCREEN_BACKDROP = "1"

# ffdec's XML export puts each top-level tag on its own line at this indentation.
TAG_PREFIX = "\n    <item type="
DEFINITION_ID = r'(?:spriteId|shapeId|characterID|buttonId)="(\d+)"'
ID_ATTRIBUTE = r'\b(spriteId|shapeId|characterID|characterId|bitmapId|buttonId)="(\d+)"'


class ClientPacks:
    """Stock assets from a client's Assets_NNN.pack files (stored uncompressed under their plain names)."""

    def __init__(self, client_dir):
        self.entries = {}
        # Later packs win, like the client.
        for pack in sorted(client_dir.glob("Assets_*.pack")):
            with pack.open("rb") as f:
                offset = 0
                while True:
                    f.seek(offset)
                    next_offset, count = struct.unpack(">II", f.read(8))
                    for _ in range(count):
                        name_length, = struct.unpack(">I", f.read(4))
                        name = f.read(name_length).decode("latin-1")
                        data_offset, size, _ = struct.unpack(">III", f.read(12))
                        self.entries[name] = (pack, data_offset, size)
                    if next_offset == 0:
                        break
                    offset = next_offset
        if not self.entries:
            raise FileNotFoundError(f"No Assets_*.pack files in {client_dir}")

    def read(self, name):
        if name not in self.entries:
            raise FileNotFoundError(f"{name} not found in the client's asset packs")
        pack, offset, size = self.entries[name]
        with pack.open("rb") as f:
            f.seek(offset)
            return f.read(size)


class AssetMirror:
    """Stock assets from a local copy of the asset server: <bucket>/<name>, large files as <name>.z."""

    def __init__(self, assets_dir):
        self.assets_dir = assets_dir

    def read(self, name):
        # The bucket isn't derivable from the name, so search them all.
        for candidate in self.assets_dir.glob(f"*/{name}"):
            return candidate.read_bytes()
        for candidate in self.assets_dir.glob(f"*/{name}.z"):
            return decompress(candidate.read_bytes())
        raise FileNotFoundError(f"{name} not found in {self.assets_dir}")


def decompress(data):
    magic, size = struct.unpack(">II", data[:8])
    if magic != COMPRESSED_MAGIC:
        raise ValueError("not a compressed asset")
    return zlib.decompress(data[8:])


def compress(data):
    return struct.pack(">II", COMPRESSED_MAGIC, len(data)) + zlib.compress(data, 9)


def load_texture(stock, file_name):
    return Image.open(io.BytesIO(stock.read(file_name))).convert("RGBA")


def hue_shift(image, degrees):
    alpha = image.getchannel("A")
    hue, saturation, value = image.convert("RGB").convert("HSV").split()
    hue = hue.point(lambda x: (x + round(degrees * 256 / 360)) % 256)
    shifted = Image.merge("HSV", (hue, saturation, value)).convert("RGB")
    shifted.putalpha(alpha)
    return shifted


def lossless_tag(character_id, image):
    # DefineBitsLossless2 holds zlib'd, premultiplied ARGB.
    argb = bytearray()
    for r, g, b, a in image.convert("RGBA").getdata():
        argb += bytes((a, r * a // 255, g * a // 255, b * a // 255))
    return (f'{TAG_PREFIX}"DefineBitsLossless2Tag" bitmapColorTableSize="0" bitmapFormat="5" '
            f'bitmapHeight="{image.height}" bitmapWidth="{image.width}" characterID="{character_id}" '
            f'forceWriteAsLong="true" zlibBitmapData="{zlib.compress(bytes(argb), 9).hex()}"/>')


def add_recolored_symbol(xml, stock, export_name, new_export_name, degrees):
    """Exports a copy of a symbol under a new name, with its external textures hue shifted and embedded."""
    tags = {}
    for match in re.finditer(re.escape(TAG_PREFIX) + r'"(\w+)"[^>]*?' + DEFINITION_ID, xml):
        end = xml.find(TAG_PREFIX, match.start() + 1)
        tags.setdefault(match.group(2), (match.group(1), xml[match.start():end]))

    export = re.search(r'<tags>\s*<item>(\d+)</item>\s*</tags>\s*<names>\s*<item>' + re.escape(export_name) + r'</item>', xml)
    if export is None:
        raise ValueError(f"{export_name} is not exported")

    # Dependencies first, so every copy is defined before it's used.
    order = []

    def visit(character_id):
        if character_id in order or character_id not in tags:
            return
        for child in re.findall(r'(?:characterId|bitmapId)="(\d+)"', tags[character_id][1]):
            if child != character_id:
                visit(child)
        order.append(character_id)

    visit(export.group(1))

    next_id = max(int(x) for x in re.findall(DEFINITION_ID, xml)) + 1
    remap = {old: str(next_id + i) for i, old in enumerate(order)}

    copies = []
    for old in order:
        tag_type, text = tags[old]
        if tag_type == "DefineExternalImage":
            image = load_texture(stock, re.search(r'fileName="([^"]*)"', text).group(1))
            copies.append(lossless_tag(remap[old], hue_shift(image, degrees)))
        else:
            copies.append(re.sub(ID_ATTRIBUTE, lambda m: f'{m.group(1)}="{remap.get(m.group(2), m.group(2))}"', text))

    copies.append(f'{TAG_PREFIX}"ExportAssetsTag" forceWriteAsLong="true">\n'
                  f'      <tags>\n        <item>{remap[export.group(1)]}</item>\n      </tags>\n'
                  f'      <names>\n        <item>{new_export_name}</item>\n      </names>\n    </item>')

    insert_at = xml.find(TAG_PREFIX, export.end())
    return xml[:insert_at] + "".join(copies) + xml[insert_at:]


def signed_bits(*values):
    return max(max(value.bit_length(), (~value).bit_length()) + 1 for value in values)


def add_texture_symbol(xml, stock, file_name, export_name, size, degrees):
    """Exports a sprite showing a client texture, top left at its origin, fitted into a size x size square."""
    image = load_texture(stock, file_name)
    image = image.crop(image.getchannel("A").getbbox())
    if degrees:
        image = hue_shift(image, degrees)
    image.thumbnail((size, size), Image.LANCZOS)
    square = Image.new("RGBA", (size, size))
    square.alpha_composite(image, ((size - image.width) // 2, (size - image.height) // 2))
    return add_image_symbol(xml, square, export_name)


def add_image_symbol(xml, image, export_name):
    """Exports a sprite showing image at its pixel size, top left at its origin."""
    # 65535 is the "no bitmap" fill placeholder, not a character.
    bitmap_id = max(int(x) for x in re.findall(r'(?:Id|ID)="(\d+)"', xml) if int(x) < 65535) + 1
    shape_id, sprite_id = bitmap_id + 1, bitmap_id + 2
    # Twips; the bitmap fill maps 1 pixel to 20.
    width, height = image.width * 20, image.height * 20
    edges = "".join(
        f'\n          <item type="StraightEdgeRecord" {axis}="{delta}" generalLineFlag="false" '
        f'numBits="{signed_bits(delta) - 2}" vertLineFlag="{str(axis == "deltaY").lower()}"/>'
        for axis, delta in [("deltaX", width), ("deltaY", height), ("deltaX", -width), ("deltaY", -height)])
    tags = [
        lossless_tag(str(bitmap_id), image),
        f'{TAG_PREFIX}"DefineShapeTag" forceWriteAsLong="true" shapeId="{shape_id}">\n'
        f'      <shapeBounds type="RECT" Xmax="{width}" Xmin="0" Ymax="{height}" Ymin="0" '
        f'nbits="{signed_bits(width, height)}"/>\n'
        f'      <shapes type="SHAPEWITHSTYLE" numFillBits="1" numLineBits="0">\n'
        f'        <fillStyles type="FILLSTYLEARRAY">\n          <fillStyles>\n'
        f'            <item type="FILLSTYLE" bitmapId="{bitmap_id}" fillStyleType="65">\n'
        f'              <bitmapMatrix type="MATRIX" hasRotate="false" hasScale="true" nRotateBits="0" nScaleBits="22" '
        f'nTranslateBits="0" scaleX="20.0" scaleY="20.0" translateX="0" translateY="0"/>\n'
        f'            </item>\n          </fillStyles>\n        </fillStyles>\n'
        f'        <lineStyles type="LINESTYLEARRAY">\n          <lineStyles/>\n        </lineStyles>\n'
        f'        <shapeRecords>\n'
        f'          <item type="StyleChangeRecord" fillStyle1="1" moveBits="1" moveDeltaX="0" moveDeltaY="0" '
        f'stateFillStyle0="false" stateFillStyle1="true" stateLineStyle="false" stateMoveTo="true" stateNewStyles="false"/>'
        f'{edges}\n          <item type="EndShapeRecord" endOfShape="0"/>\n        </shapeRecords>\n      </shapes>\n    </item>',
        f'{TAG_PREFIX}"DefineSpriteTag" forceWriteAsLong="true" frameCount="1" hasEndTag="true" spriteId="{sprite_id}">\n'
        f'      <subTags>\n'
        f'        <item type="PlaceObject2Tag" characterId="{shape_id}" depth="1" forceWriteAsLong="false" '
        f'placeFlagHasCharacter="true" placeFlagHasClipActions="false" placeFlagHasClipDepth="false" '
        f'placeFlagHasColorTransform="false" placeFlagHasMatrix="true" placeFlagHasName="false" placeFlagHasRatio="false" '
        f'placeFlagMove="false">\n'
        f'          <matrix type="MATRIX" hasRotate="false" hasScale="false" nRotateBits="0" nScaleBits="0" '
        f'nTranslateBits="0" translateX="0" translateY="0"/>\n        </item>\n'
        f'        <item type="ShowFrameTag" forceWriteAsLong="false"/>\n      </subTags>\n    </item>',
        f'{TAG_PREFIX}"ExportAssetsTag" forceWriteAsLong="true">\n'
        f'      <tags>\n        <item>{sprite_id}</item>\n      </tags>\n'
        f'      <names>\n        <item>{export_name}</item>\n      </names>\n    </item>',
    ]

    # Defined before the first frame is shown, so attachMovie finds it from the first frame's script.
    insert_at = xml.find(TAG_PREFIX + '"ShowFrameTag"')
    return xml[:insert_at] + "".join(tags) + xml[insert_at:]


def ffdec(java, jar, *args):
    result = subprocess.run([str(java), "-jar", str(jar), *map(str, args)], capture_output=True, text=True)
    if result.returncode != 0:
        print(result.stdout)
        print(result.stderr, file=sys.stderr)
        raise RuntimeError(f"ffdec failed: {' '.join(map(str, args))}")


def export_font(java, jar, stock, temp):
    """The client's Free Realms font (FreeRealmsDemiBold), exported from gfxfontlib.gfx; no game font is stored here."""
    fontlib_path = temp / "gfxfontlib.gfx"
    fontlib_path.write_bytes(stock.read("gfxfontlib.gfx"))
    ffdec(java, jar, "-export", "font", temp / "fonts", fontlib_path)
    font = next((temp / "fonts").rglob("*FreeRealmsDemiBold*.ttf"), None)
    if font is None:
        raise FileNotFoundError("FreeRealmsDemiBold not found in gfxfontlib.gfx")
    return font


def remove_root_depths(xml, depths):
    head, tags, tail = split_tags(xml)
    placement = re.escape(TAG_PREFIX) + r'"(?:PlaceObject|RemoveObject)\d?Tag"[^>]*\bdepth="(\d+)"'
    return head + "".join(tag for tag in tags if not (re.match(placement, tag) and int(re.match(placement, tag).group(1)) in depths)) + tail


def replace_textures(xml, paint):
    """Embeds paint(texture, drawn size) in place of each external image it returns a picture for."""
    def replace(match):
        attributes = dict(re.findall(r'(\w+)="([^"]*)"', match.group(0)))
        size = (int(attributes["targetWidth"]), int(attributes["targetHeight"]))
        image = paint(attributes["fileName"], size)
        if image is None:
            return match.group(0)
        return lossless_tag(attributes["characterID"], image).lstrip("\n ")

    return re.sub(r'<item type="DefineExternalImage"[^>]*/>', replace, xml)


def apply_reskins(xml, stock, recipes):
    """Embeds a repainted copy of each listed texture in place of the external image, at its drawn size."""
    def repaint(file_name, size):
        recipe = recipes.get(file_name)
        if recipe is None:
            return None
        return reskin.RECIPES[recipe](load_texture(stock, file_name).resize(size, Image.LANCZOS))

    return replace_textures(xml, repaint)


def build_movie(java, jar, stock, stock_name, script_name, patch_paths, symbols, output_path):
    with tempfile.TemporaryDirectory() as temp:
        temp = Path(temp)

        stock_path = temp / f"{stock_name}.gfx"
        stock_path.write_bytes(stock.read(f"{stock_name}.gfx"))

        swaps = TEXTURE_SWAPS.get(stock_name, [])
        textures = TEXTURE_SYMBOLS.get(stock_name, [])
        images = IMAGE_SYMBOLS.get(stock_name, [])
        texts = TEXT_SYMBOLS.get(stock_name, [])
        removed = REMOVED_DEPTHS.get(stock_name, [])
        reskins = RESKINS.get(stock_name, {})

        if symbols or swaps or textures or images or texts or removed or reskins:
            ffdec(java, jar, "-swf2xml", stock_path, temp / "stock.xml")
            xml = (temp / "stock.xml").read_text(encoding="utf-8")
            for symbol in symbols:
                xml = add_recolored_symbol(xml, stock, *symbol)
            for old, new in swaps:
                if f'fileName="{old}"' not in xml:
                    raise ValueError(f"{old} not found in {stock_name}.gfx")
                xml = xml.replace(f'fileName="{old}"', f'fileName="{new}"')
            for texture in textures:
                xml = add_texture_symbol(xml, stock, *texture)
            for image_path, export_name in images:
                xml = add_image_symbol(xml, Image.open(image_path), export_name)
            if texts:
                font = export_font(java, jar, stock, temp)
                for export_name, text, size, top, bottom in texts:
                    xml = add_image_symbol(xml, background.render_word(font, text, size, top, bottom), export_name)
            if removed:
                xml = remove_root_depths(xml, removed)
            if reskins:
                xml = apply_reskins(xml, stock, reskins)
            (temp / "stock.xml").write_text(xml, encoding="utf-8")
            ffdec(java, jar, "-xml2swf", temp / "stock.xml", temp / "stock.gfx")
            stock_path = temp / "stock.gfx"

        # A repaint-only mod has no script to patch.
        if not patch_paths:
            shutil.copyfile(stock_path, output_path)
            return

        ffdec(java, jar, "-export", "script", temp / "export", stock_path)

        script_path = temp / "export" / "scripts" / Path(*script_name.strip("\\").split("\\")).with_suffix(".as")
        if not script_path.exists():
            raise FileNotFoundError(f"{script_name} not found in {stock_path.name}")

        patched = temp / "patched.as"
        patched.write_text(
            "\n".join([script_path.read_text(encoding="utf-8")] + [p.read_text(encoding="utf-8") for p in patch_paths]),
            encoding="utf-8")

        ffdec(java, jar, "-replace", stock_path, output_path, script_name, patched)


def build_list(stock, stock_name, published_name, output_path):
    # The .lst preloads the movie's images and ends with the movie itself.
    text = stock.read(f"{stock_name}.lst").decode("latin-1")
    text = text.replace(f"{stock_name}.gfx", f"{published_name}.gfx")
    for old, new in TEXTURE_SWAPS.get(stock_name, []):
        text = text.replace(old, new)
    output_path.write_bytes(compress(text.encode("latin-1")))


def split_tags(xml):
    """Splits ffdec's XML into the part before the first top-level tag, each top-level tag, and the rest."""
    starts = [m.start() for m in re.finditer(re.escape(TAG_PREFIX), xml)]
    end = xml.find("\n  </tags>", starts[-1])
    bounds = starts + [end]
    return xml[:starts[0]], [xml[a:b] for a, b in zip(bounds, bounds[1:])], xml[end:]


def build_loading_screen(java, jar, stock, stock_name, published_name, movie_path, list_path):
    """Strips the stock loading screen down to its tip and loading bar over the OSFR art."""
    with tempfile.TemporaryDirectory() as temp:
        temp = Path(temp)

        art = background.render(export_font(java, jar, stock, temp))

        stock_path = temp / f"{stock_name}.gfx"
        stock_path.write_bytes(stock.read(f"{stock_name}.gfx"))
        ffdec(java, jar, "-swf2xml", stock_path, temp / "stock.xml")
        head, tags, tail = split_tags((temp / "stock.xml").read_text(encoding="utf-8"))

        def is_tag(tag, pattern):
            return re.match(re.escape(TAG_PREFIX) + f'"{pattern}"', tag)

        tags = [tag for tag in tags if not is_tag(tag, r"(?:PlaceObject|RemoveObject)\d?Tag")
                or int(re.search(r'\bdepth="(\d+)"', tag).group(1)) in LOADING_SCREEN_DEPTHS]

        # Drop the definitions nothing left on screen uses, so their textures aren't loaded.
        definitions = {}
        for i, tag in enumerate(tags):
            match = re.match(re.escape(TAG_PREFIX) + r'"Define\w*"[^>]*?' + DEFINITION_ID, tag)
            if match:
                definitions[match.group(1)] = i
        references = r'(?:characterId|bitmapId|fontId)="(\d+)"'
        pending = [ref for tag in tags if not is_tag(tag, r"Define\w*") for ref in re.findall(references, tag)]
        used = set()
        while pending:
            character_id = pending.pop()
            if character_id in definitions and character_id not in used:
                used.add(character_id)
                pending += re.findall(references, tags[definitions[character_id]])
        dropped = {i for character_id, i in definitions.items() if character_id not in used}
        tags = [tag for i, tag in enumerate(tags) if i not in dropped]

        for i, tag in enumerate(tags):
            if not is_tag(tag, "DefineExternalImage"):
                if re.search(f'bitmapId="{LOADING_SCREEN_BACKDROP}"', tag):
                    # The backdrop shape squashed the stock 1024x1024 texture to the 1024x768 stage;
                    # the art is already stage sized.
                    tags[i] = re.sub(f'(bitmapId="{LOADING_SCREEN_BACKDROP}"' + r'[\s\S]*?<bitmapMatrix[^>]*?scaleX="([\d.]+)" scaleY=")[\d.]+',
                                     lambda m: m.group(1) + m.group(2), tag, count=1)
                continue
            attributes = dict(re.findall(r'(\w+)="([^"]*)"', tag))
            if attributes["characterID"] == LOADING_SCREEN_BACKDROP:
                tags[i] = lossless_tag(LOADING_SCREEN_BACKDROP, art)
            else:
                # Textures are padded to powers of two and drawn at their target size; embedded 1:1
                # the bar and spinner would lose their right and bottom edges.
                texture = load_texture(stock, attributes["fileName"])
                size = (int(attributes["targetWidth"]), int(attributes["targetHeight"]))
                tags[i] = lossless_tag(attributes["characterID"], texture.resize(size, Image.LANCZOS))

        (temp / "mod.xml").write_text(head + "".join(tags) + tail, encoding="utf-8")
        ffdec(java, jar, "-xml2swf", temp / "mod.xml", movie_path)

    # Every texture is embedded now, so the list only preloads the movie.
    lines = stock.read(f"{stock_name}.lst").decode("latin-1").split("\r\n")
    text = "\r\n".join(line for line in lines if not line.endswith(".dds"))
    list_path.write_bytes(compress(text.replace(f"{stock_name}.gfx", f"{published_name}.gfx").encode("latin-1")))


def build_region_badge(java, jar, stock, stock_name, published_name, name, font_height, colors, painter,
                       movie_path, list_path):
    """A stock region badge with its art repainted (notifications/badges.py) and the region's name for its own."""
    art = badges.PAINTERS[painter](lambda file_name: load_texture(stock, file_name))

    with tempfile.TemporaryDirectory() as temp:
        temp = Path(temp)

        stock_path = temp / f"{stock_name}.gfx"
        stock_path.write_bytes(stock.read(f"{stock_name}.gfx"))
        ffdec(java, jar, "-swf2xml", stock_path, temp / "stock.xml")
        xml = (temp / "stock.xml").read_text(encoding="utf-8")

        xml = replace_textures(xml, lambda file_name, size: art[file_name].resize(size, Image.LANCZOS)
                               if file_name in art else None)

        # The name is in two text fields, a shadow and the name over it.
        def rename(match):
            tag = re.sub(r'initialText="[^"]*"', f'initialText="{name}"', match.group(0))
            return re.sub(r'fontHeight="\d+"', f'fontHeight="{font_height}"', tag)

        xml, count = re.subn(r'<item type="DefineEditTextTag"[^>]*initialText="S##[^"]*"[^>]*>', rename, xml)
        if count == 0:
            raise ValueError(f"No region name in {stock_name}.gfx")

        def recolor(match):
            color = colors.get((int(match.group("red")), int(match.group("green")), int(match.group("blue"))))
            if color is None:
                return match.group(0)
            red, green, blue = color
            return f'<{match.group("tag")} type="RGBA" alpha="{match.group("alpha")}" blue="{blue}" green="{green}" red="{red}"/>'

        xml = re.sub(r'<(?P<tag>textColor|glowColor) type="RGBA" alpha="(?P<alpha>\d+)" blue="(?P<blue>\d+)" '
                     r'green="(?P<green>\d+)" red="(?P<red>\d+)"/>', recolor, xml)

        (temp / "badge.xml").write_text(xml, encoding="utf-8")
        ffdec(java, jar, "-xml2swf", temp / "badge.xml", movie_path)

    # The repainted textures are embedded now, so the list doesn't preload them.
    lines = stock.read(f"{stock_name}.lst").decode("latin-1").split("\r\n")
    text = "\r\n".join(line for line in lines if line not in art)
    list_path.write_bytes(compress(text.replace(f"{stock_name}.gfx", f"{published_name}.gfx").encode("latin-1")))


def main():
    parser = ArgumentParser(description=__doc__.splitlines()[0])
    source = parser.add_mutually_exclusive_group(required=True)
    source.add_argument("--client", type=Path, help="Free Realms client folder (reads its Assets_*.pack files)")
    source.add_argument("--assets", type=Path, help="Local copy of the asset server (<bucket>/<name> folders)")
    parser.add_argument("--ffdec", type=Path, required=True, help="Path to ffdec.jar")
    parser.add_argument("--java", type=Path, default=Path(shutil.which("java") or "java"), help="Java executable")
    parser.add_argument("--only", nargs="+", metavar="NAME", help="Build only these published mods (e.g. sanctuaryAtlas)")
    args = parser.parse_args()

    stock = ClientPacks(args.client) if args.client else AssetMirror(args.assets)

    output_dir.mkdir(parents=True, exist_ok=True)

    for stock_name, published_name, script_name, patch_paths, symbols in MODS:
        if args.only and published_name not in args.only:
            continue
        movie_path = output_dir / f"{published_name}.gfx"
        list_path = output_dir / f"{published_name}.lst.z"

        build_movie(args.java, args.ffdec, stock, stock_name, script_name, patch_paths, symbols, movie_path)
        build_list(stock, stock_name, published_name, list_path)

        print(f"Built {movie_path.name} ({movie_path.stat().st_size} bytes) and {list_path.name}")

    for stock_name, published_name, *badge in REGION_BADGES:
        if args.only and published_name not in args.only:
            continue
        movie_path = output_dir / f"{published_name}.gfx"
        list_path = output_dir / f"{published_name}.lst.z"

        build_region_badge(args.java, args.ffdec, stock, stock_name, published_name, *badge, movie_path, list_path)

        print(f"Built {movie_path.name} ({movie_path.stat().st_size} bytes) and {list_path.name}")

    stock_name, published_name = LOADING_SCREEN
    if args.only and published_name not in args.only:
        return
    movie_path = output_dir / f"{published_name}.gfx"
    list_path = output_dir / f"{published_name}.lst.z"
    build_loading_screen(args.java, args.ffdec, stock, stock_name, published_name, movie_path, list_path)
    print(f"Built {movie_path.name} ({movie_path.stat().st_size} bytes) and {list_path.name}")


if __name__ == "__main__":
    main()
