# Zone editor

A browser-based editor for Free Realms zones. Open FabledRealms (or any zone) straight from your client,
add new chunks of land, sculpt terrain, paint eco layers and the colour map, place props and point lights,
set the sky and spawn, and save. Saving writes the client files the WebAPI streams to players and the
Sanctuary data that makes the zone playable.

```
pip install numpy pillow
python ZoneEditor/zone_editor.py
```

It indexes the client (default `%LOCALAPPDATA%/OSFRLauncher/Servers/OSFR Public Server/Client`, or `--client`),
opens http://127.0.0.1:7050 and saves into the Sanctuary checkout it lives in (`--sanctuary` for another one).
Assets the client doesn't ship are read from `--assets` (default `<sanctuary>/assets`, the local copy of the
asset server) and then from the upstream asset server (`--upstream`, `''` to stay offline). The viewport
loads three.js from jsDelivr.

## Stock zones and new zones

The client won't take a replacement for a file it ships in its read-only packs: it downloads the new crc,
fails with "Attempt to update data that already exists in a non-writeable pack file", and a replaced
`.gzne` leaves the zone hanging on load. Only new file names stream. So:

- **In a stock zone such as FabledRealms**, the stock chunks are read-only (shown greyed). Use the Chunks
  tool to add chunks in the empty cells inside the zone's bounds (FabledRealms' run to x 6144 and z 5120;
  stock land stops well short of that) and build there. A new chunk's edges are copied from the stock
  land around it and stay pinned while you sculpt, so the seams don't open. The zone's eco list and bounds
  are fixed, because its `.gzne` can't be replaced.
- **A new zone** (New zone…) is all new names, so all of it is editable and it grows when you add chunks
  outside it. Start from blank terrain or from a copy of an existing zone, such as a housing lot. A Housing
  zone also gets a Houses.json entry; a copied lot keeps the original's spawn, exit door and build areas.

## What a save writes

| File | What it is |
| --- | --- |
| `src/Resources/ClientMods/<zone>_<row>_<col>.gcnk` | Terrain chunk: mesh, collision, eco blend map, props, lights, tile environments |
| `src/Resources/ClientMods/<zone>_<row>_<col>.gck2` | The chunk's colour map |
| `src/Resources/ClientMods/<zone>.gzne.z` | A new zone's eco list, flora, bounds and walls |
| `src/Resources/Zones/<zone>.json` | Sanctuary's zone definition: id, type, bounds, spawn, sky |
| `src/Resources/Zones/Environments/<zone>.json` | The server's per-tile environment grid, where the zone has one (FabledRealms) |
| `src/Resources/Houses.json` | The house for a housing zone |
| `ClientMods/sky/<sky>.xml` | A custom sky, also copied into your client's `Resources/SKY` |

Only files that changed are written, and Sanctuary's hand-laid-out JSON (FabledRealms.json, Houses.json)
is patched in place rather than reflowed. If the servers have been built, the same files are copied into `src/Sanctuary.WebAPI/bin/*/*/ClientMods` and
`src/Sanctuary.Gateway/bin/*/*/Resources`. Restart the servers (`launch.ps1`) to load new zones; the WebAPI
adds the client files to the manifest it serves and clients download them when they need them.

Getting there in game: `!moveplayer zone <zone id> <your name>` for a world zone; for a housing zone,
`!house grant <house id>` and then `!house enter`. Skies don't stream, so other players need
`ClientMods/install_client_mods.py` to install a custom one (it leaves zone files out of its pack, since
they stream).

## Tools

| Tool | Key | |
| --- | --- | --- |
| Select | V | Click a prop, light or marker. G move, R rotate, T scale, Del delete, Ctrl+D duplicate, F focus. A prop dragged into another editable chunk moves into it. |
| Sculpt | B | Raise, lower (Shift swaps them), smooth, flatten (to where the stroke started), level (to a set height), noise. |
| Eco | E | Paint one of the zone's ecos; the highlight shows where the selected eco is. Also tints the colour map toward the eco's colour, which is how the stock colour maps look (a flat tint per eco). A tile holds up to 5 ecos. |
| Colour | C | Paint the colour map directly; Alt+click picks a colour. |
| Env | N | Paint tile environments, the `ObjectTerrainData.xml` row that picks a tile's sky, minimap and atlas. 0 uses the zone's sky. |
| Props | P | Search the client's actors, click to place. Shift+wheel turns the preview. |
| Lights | L | Click the ground to add a point light; edit colour, range and intensity. |
| Chunks | K | Click an empty cell, in the view or on the minimap, to add a chunk. |
| Zone | Z | Spawn point and heading, zone sky or a custom sky XML, adding ecos to a new zone (from FabledRealms' list or a client `.eco`), house exit door and build areas. |

Fly with the right mouse button and WASD/QE (Shift for speed, the wheel changes speed; in brush tools the
wheel sizes the brush and Ctrl+wheel sets strength). Ctrl+Z undoes, Ctrl+S saves. The minimap shows stock
chunks grey, editable ones green and unsaved ones gold; click it to fly there.

## How it works

Chunks are read and written with `ClientMods/world_chunks.py` (every stock version 5 and 6 chunk
round-trips byte for byte); `zoneeditor/formats.py` adds the `.gzne` zone file, point lights, the eco blend
map as per-eco weights and colour maps. The editor holds each chunk as a 2-unit heightfield, which is the
stock meshes' own grid; saving rebuilds only the tiles whose heights changed, with their Bullet collision
trees (`ClientMods/zone_terrain.py`), and keeps the rest as they were. The eco blend map's channel order
and layering were matched against the stock colour maps.

Not covered yet: chunks older than version 5 (a few minigame zones) don't load, flora and
eco detail textures aren't drawn in the viewport (the colour map is), and invisible walls, client areas
and groups are kept as they are but not editable.
