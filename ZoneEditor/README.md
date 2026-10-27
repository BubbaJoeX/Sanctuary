# Zone editor

A browser-based editor for Free Realms zones. Open FabledRealms (or any zone) straight from your client,
sculpt terrain, paint eco layers and the colour map, place props and point lights, add chunks, set the sky
and spawn, save, and test it in game with one click. Saving writes the client files the WebAPI streams to
players and the Sanctuary data that makes the zone playable.

```
pip install numpy pillow
python ZoneEditor/zone_editor.py --sanctuary D:\Sanctuary
```

It indexes the client (default `%LOCALAPPDATA%/OSFRLauncher/Servers/OSFR Public Server/Client`, or `--client`),
opens http://127.0.0.1:7050 and saves into the Sanctuary checkout given by `--sanctuary` (default: the one it
lives in). Assets the client doesn't ship are read from `--assets` (default `<sanctuary>/assets`, the local
copy of the asset server) and then from the upstream asset server (`--upstream`, `''` to stay offline). The
viewport loads three.js from jsDelivr.

## Editing any zone, stock ones included

Every zone is editable, FabledRealms too. A new zone (New zone…, blank or a copy of one such as a housing
lot) saves under new names; a Housing zone also gets a Houses.json entry, and a copied lot keeps the
original's spawn, exit door and build areas. Zones grow when chunks are added outside them: the `.gzne`
holds each zone's start and size in tiles (z, then x, like chunk names `<zone>_<z>_<x>`), and a size that
doesn't cover a chunk keeps it from loading. FabledRealms' 480 chunks fill its bounds exactly, so new land
there always grows its zone file.

Edits to a stock zone replace stock files, and the client won't update a file it holds in its read-only
`Assets_NNN` packs: it fails with "Attempt to update data that already exists in a non-writeable pack file",
and a replaced `FabledRealms.gzne` (which is in one) leaves the zone hanging on load. Most FabledRealms chunks
were streamed into the client's writable `AssetsW` packs and update normally. For the rest, a save hides the
client's stock copy (`install_client_mods.sync_stock_overrides`: the entry's name in the pack index starts
with `~` instead, recorded in `sanctuary_stock_overrides.txt`), so the client downloads the edited file like
any new one. That needs the client closed; Test in game does it, and `install_client_mods.py` applies it to
other clients (`--uninstall` puts the names back).

## Saving and testing

| File | What it is |
| --- | --- |
| `src/Resources/ClientMods/<zone>_<row>_<col>.gcnk` | Terrain chunk: mesh, collision, eco blend map, props, lights, tile environments |
| `src/Resources/ClientMods/<zone>_<row>_<col>.gck2` | The chunk's colour map |
| `src/Resources/ClientMods/<zone>.gzne.z` | The zone's eco list, flora, bounds and walls, when they change |
| `src/Resources/Zones/<zone>.json` | Sanctuary's zone definition: id, type, bounds, spawn, sky |
| `src/Resources/Zones/Environments/<zone>.json` | The server's per-tile environment grid, where the zone has one (FabledRealms) |
| `src/Resources/Houses.json` | The house for a housing zone |
| `ClientMods/sky/<sky>.xml` | A custom sky, also copied into your client's `Resources/SKY` |

Only files that changed are written. Sanctuary's JSON is read again at save time and only the editor's
changes applied, so edits made meanwhile by other tools stay, and hand-laid-out files (FabledRealms.json,
Houses.json) are patched in place rather than reflowed. Each save also copies the files into the built
servers' folders (`src/Sanctuary.WebAPI/bin/*/*/ClientMods`, `src/Sanctuary.Gateway/bin/*/*/Resources`): the
WebAPI rebuilds the manifest it serves as soon as they change, so the asset server has them right away.

**Test in game** saves, closes the client, hides replaced stock files and starts it again with
`launch.ps1 -ClientOnly`, which loads your terrain, props and lights. Zone definitions, spawns and houses
load when the Gateway starts, so a save that changes them ticks **+ servers**, which restarts everything
with `launch.ps1 -NoBuild` instead. Getting there: `!moveplayer zone <zone id> <your name>` for a world zone;
for a housing zone, `!house grant <house id>` and then `!house enter`.

## Tools

| Tool | Key | |
| --- | --- | --- |
| Select | V | Click a prop, light or marker. G move, R rotate, T scale, Del delete, Ctrl+D duplicate, F focus. A prop dragged into another chunk moves into it. |
| Sculpt | B | Raise, lower (Shift swaps them), smooth, flatten (to where the stroke started), level (to a set height), noise. |
| Eco | E | Paint any eco: the zone's own, or any of the client's (every zone's eco lists and its `.eco` definitions, 107 in all), searchable with texture swatches. One the zone doesn't have joins its eco list on the first stroke. The highlight shows where the selected eco is, and painting also tints the colour map toward the eco's colour, which is how the stock colour maps look (a flat tint per eco). A tile holds up to 5 ecos. |
| Colour | C | Paint the colour map directly; Alt+click picks a colour. |
| Env | N | Paint tile environments, the `ObjectTerrainData.xml` row that picks a tile's sky, minimap and atlas. 0 uses the zone's sky. |
| Props | P | Place objects from the content browser (O): click a tile, then the ground, or drag a tile into the view. Shift+wheel turns the preview. |
| Lights | L | Click the ground to add a point light; edit colour, range and intensity. |
| Chunks | K | Click an empty cell, in the view or on the minimap, to add a chunk. Its edges join the land around it. |
| Zone | Z | Spawn point and heading, zone sky or a custom sky XML, adding ecos (from FabledRealms' list or a client `.eco`), house exit door and build areas. |

The **content browser** (Content, or O) shows all of the client's 10,000-odd actors and actor sets as rendered
thumbnails. It opens on what zones place (counted across every zone's chunks the first time, then cached,
most placed first), with groups for what the loaded chunks use, ★ favourites, recently placed, all actors and
name prefixes (bw_ Briarwood, sh_ Snowhill, hsg_ housing, ...), and filters by kind (trees, rocks, plants,
buildings, ...) and name. Characters and gear stay hidden unless asked for. Thumbnails are rendered in the
browser and kept in `ZoneEditor/.cache/thumbs`.

Fly with the right mouse button and WASD/QE (Shift for speed, the wheel changes speed; in brush tools the
wheel sizes the brush and Ctrl+wheel sets strength). Ctrl+Z undoes, Ctrl+S saves. The minimap shows chunks
green and unsaved ones gold; click it to fly there.

## The viewport

It draws the world as the client does, with its coordinates as they are. The ground is the colour map with
each eco's detail texture blended in by the eco weights (up to 4 per chunk). Props use their own normals and
diffuse textures, uploaded as the client's DDS files with their mipmaps; cutout foliage, glass and water
follow their material definitions. Lighting comes from the sky file in use, by time of day (the Time slider):
sun colour, brightness and direction, fog, and sky and ground light from its ambient gradient. Without a zone
sky it follows the environment of the tile under the camera, like the client. The sun casts shadows near the
camera, and the zone's own point lights nearest the camera light the scene.

## How it works

Chunks are read and written with `ClientMods/world_chunks.py` (every stock version 5 and 6 chunk
round-trips byte for byte); `zoneeditor/formats.py` adds the `.gzne` zone file, point lights, the eco blend
map as per-eco weights and colour maps. The editor holds each chunk as a 2-unit heightfield, which is the
stock meshes' own grid; saving rebuilds only the tiles whose heights changed, with their Bullet collision
trees (`ClientMods/zone_terrain.py`), and keeps the rest as they were. The eco blend map's channel order
and layering were matched against the stock colour maps.

Not covered yet: chunks older than version 5 (a few minigame zones) don't load, flora isn't drawn, and
invisible walls, client areas and groups are kept as they are but not editable.
