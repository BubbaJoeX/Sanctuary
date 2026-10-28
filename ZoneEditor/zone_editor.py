#!/usr/bin/env python3
"""Sanctuary's zone editor: sculpt, paint and dress Free Realms zones in the browser.

Opens FabledRealms (or any zone) from your client, lets you add chunks, sculpt terrain, paint ecos and
colour, place props and lights, and saves client mods plus Sanctuary's zone data. See README.md.

    python zone_editor.py
    python zone_editor.py --client "%LOCALAPPDATA%/OSFRLauncher/Servers/OSFR Public Server/Client" --port 7050
"""

import os
import sys
import webbrowser
from argparse import ArgumentParser
from pathlib import Path

script_dir = Path(__file__).resolve().parent
# zone_terrain.py (collision trees, DXT1) lives with the other client mod tools.
sys.path.insert(0, str(script_dir.parent / "ClientMods"))

from zoneeditor import granny  # noqa: E402
from zoneeditor.server import serve  # noqa: E402
from zoneeditor.workspace import Workspace  # noqa: E402

DEFAULT_CLIENT = Path(os.environ.get("LOCALAPPDATA", "")) / "OSFRLauncher" / "Servers" / "OSFR Public Server" / "Client"


def main():
    parser = ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--client", type=Path, default=DEFAULT_CLIENT, help="Free Realms client folder")
    parser.add_argument("--sanctuary", type=Path, default=script_dir.parent,
                        help="Sanctuary checkout to save into (default: this one)")
    parser.add_argument("--assets", type=Path, help="Local copy of the asset server (default: <sanctuary>/assets if present)")
    parser.add_argument("--upstream", default="http://osfr.editz.dev/assets",
                        help="Asset server for anything not local ('' to stay offline)")
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", type=int, default=7050)
    parser.add_argument("--no-browser", action="store_true", help="Don't open the editor in a browser")
    args = parser.parse_args()

    if not (args.client / "Assets_manifest.txt").exists():
        parser.error(f"{args.client} is not a Free Realms client folder (no Assets_manifest.txt); use --client")

    assets = args.assets or (args.sanctuary / "assets")
    print(f"Indexing {args.client}{' and ' + str(assets) if assets.is_dir() else ''}...")
    workspace = Workspace(args.sanctuary.resolve(), args.client, assets if assets.is_dir() else None, args.upstream or None)
    server = serve(workspace, args.host, args.port)

    url = f"http://{args.host}:{args.port}/"
    print(f"Zone editor at {url} (saving into {workspace.resources}). Ctrl+C to stop.")
    if not granny.available():
        print(f"Animated props and characters stand still: no granny2.dll at {granny.DLL_PATH} (set FR_GRANNY_DLL).")
    if not args.no_browser:
        webbrowser.open(url)
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        pass


if __name__ == "__main__":
    main()
