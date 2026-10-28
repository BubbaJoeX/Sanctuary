#!/usr/bin/env python3
"""Gives every actor the client can draw a Models.txt row, so the zone editor can place any of them live.

The game spawns models by id: a placed model (the "Placed model" NPC) carries a Models.txt row's id, which the
server and the client each look up in their own copy of the table. The stock rows cover about a third of the
client's actors. This appends a row for each other .adr and .agr the content browser lists whose mesh the client
can get (from its packs, the asset server or Sanctuary's ClientMods), with ids from 10000 up (stock ids stop
below 5000 and fr_maya_mcp's start at 20000) and the stock rows' defaults. An actor whose mesh is missing
everywhere gets none: the client has nothing to draw.

It writes:
  src/Resources/Models.txt                            the server's, and the running servers' bin copies,
                                                      which reload it when it changes
  src/Resources/ClientPatches/Resources/Models.txt    the client's (the same file): launchers download it
                                                      before Play, and install_client_mods.py puts it in a
                                                      local client (the client reads it at start-up)

Rows are only ever appended, so stock rows and their ids stay as they are. Run it again after publishing new
actors, and once in a fresh checkout: the client patches aren't in git.

    python add_model_rows.py
    python add_model_rows.py --dry-run
"""

import os
import sys
from argparse import ArgumentParser
from pathlib import Path

script_dir = Path(__file__).resolve().parent
sys.path.insert(0, str(script_dir.parent / "ClientMods"))

from zoneeditor.assets import AssetSource  # noqa: E402
from zoneeditor.workspace import CACHE_DIR, actor_drawable  # noqa: E402

DEFAULT_CLIENT = Path(os.environ.get("LOCALAPPDATA", "")) / "OSFRLauncher" / "Servers" / "OSFR Public Server" / "Client"

FIRST_ID = 10000
LAST_ID = 19999          # fr_maya_mcp's new models start at 20000
# The defaults most stock rows have (an object: race 0, no gender, material 1, the standard capsule).
ROW = "{id}^{file}^0^0.000000^^0^0^^1^0.000000^0^0.000000^0.000000^0^0.000000^1.900000^0.000000^"


def main():
    parser = ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--client", type=Path, default=DEFAULT_CLIENT, help="Free Realms client folder")
    parser.add_argument("--sanctuary", type=Path, default=script_dir.parent, help="Sanctuary checkout (default: this one)")
    parser.add_argument("--assets", type=Path, help="Local copy of the asset server (default: <sanctuary>/assets if present)")
    parser.add_argument("--upstream", default="http://osfr.editz.dev/assets", help="Asset server for anything not local")
    parser.add_argument("--dry-run", action="store_true", help="Count the rows without writing them")
    args = parser.parse_args()

    resources = args.sanctuary / "src" / "Resources"
    mirror = args.assets or (args.sanctuary / "assets")
    assets = AssetSource(args.client, resources / "ClientMods", mirror, args.upstream or None, CACHE_DIR / "assets")

    path = resources / "Models.txt"
    # Latin-1 and the file's own CRLF line ends, as the client reads it.
    text = path.read_bytes().decode("latin-1")
    rows = [line.split("^") for line in text.splitlines() if line and not line.startswith("#")]
    have = {row[1].lower() for row in rows if len(row) > 1}
    ids = [int(row[0]) for row in rows if row[0].isdigit()]
    next_id = max([FIRST_ID - 1] + [i for i in ids if FIRST_ID <= i <= LAST_ID]) + 1

    # The content browser's names, as the manifest lists them.
    actors = sorted({assets.manifest.get(n, (n,))[0].removesuffix(".z") for n in assets.names(".adr") | assets.names(".agr")},
                    key=str.lower)
    added, missing = [], []
    for actor in actors:
        if actor.lower() in have:
            continue
        if not actor_drawable(assets, actor):
            missing.append(actor)
            continue
        if next_id > LAST_ID:
            raise SystemExit(f"No ids left below {LAST_ID + 1} for {actor}")
        added.append(ROW.format(id=next_id, file=actor))
        next_id += 1

    print(f"{len(actors)} actors: {len(actors) - len(added) - len(missing)} had a row, {len(added)} added, "
          f"{len(missing)} with no mesh anywhere left out")
    for actor in missing:
        print(f"  no mesh: {actor}")
    if args.dry_run:
        return

    newline = "\r\n" if "\r\n" in text else "\n"
    if added:
        text = text.rstrip("\r\n") + newline + newline.join(added) + newline
    patched = text.encode("latin-1")
    # The client patch isn't in git, so a fresh checkout gets it here even with no rows to add.
    targets = [path, resources / "ClientPatches" / "Resources" / "Models.txt"]
    # The running servers read their own copies (the Gateway and Login reload theirs on change), and the WebAPI
    # serves the client patches from its own.
    for project, folder in (("Sanctuary.Gateway", "Resources"), ("Sanctuary.Login", "Resources"),
                            ("Sanctuary.WebAPI", "ClientPatches/Resources")):
        targets += sorted(p / folder / "Models.txt" for p in (args.sanctuary / "src" / project / "bin").glob("*/net*")
                          if (p / folder).is_dir())
    written = [target for target in targets if not target.exists() or target.read_bytes() != patched]
    for target in written:
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(patched)
        print(f"Wrote {target}")
    if written:
        print("launch.ps1 and ClientMods/install_client_mods.py put it in a local client; launchers get it on Play.")


if __name__ == "__main__":
    main()
