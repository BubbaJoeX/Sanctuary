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

Every zone is editable, FabledRealms too. A new zone (New…, blank or a copy of one such as a housing lot;
type its name, which becomes lowercase with `_` for spaces, and press Enter or Create) saves under new names; a Housing zone also gets a Houses.json entry, and a copied lot keeps the
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

## Opening a zone file

By name, a zone's files come from wherever the client would get them: ClientMods first, then the client's
packs, then the asset server. **Open file…** (or a `.gzne` / `.gzne.z` path typed in the zone box) opens a
zone from a folder instead: its `.gzne`, `.gcnk` and `.gck2` files come only from that folder, never stock,
so a chunk that isn't there isn't in the zone. Saving writes the changed files back into that folder (in the
form they have there, compressed or not) and publishes them to ClientMods as usual, with the folder's zone
file on the first save. Textures, models and ecos still come from the client. The last few files opened are
listed in the zone box.

## Autosave

Every 15 seconds (and when the tab is hidden) the chunks edited since the last autosave and the zone's
settings go to `ZoneEditor/.cache/drafts/<zone>`. Saving clears the draft; opening a zone that has one
(after a crash, or a closed tab) offers to restore it, and declining discards it.

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

**Saves reach the running client.** Test in game also puts the zone's files, its `.gzne` and every chunk and
colour map (FabledRealms: 480 chunks, about 200 MB, a few seconds), in the client's `zone-editor-live` folder; a
client `launch.ps1` starts on its own gets the zone last opened in the editor. The client takes loose
files there over its packs and the manifest it downloaded: it lists their names when it starts and reads a file
each time it loads it. So while that client runs, a save copies the chunks it rewrites over those files, and
from the admin panel your character then steps over to ground at least 2 km away for two seconds and back to
where it stood, where the client drops the old chunks and reads the new ones: no restart. (Closer than about a
kilometre, the client keeps the chunk and only re-places its props; with no ground under it, it drops nothing.)
Where there's no ground that far off (a house, most zones but FabledRealms), or the save changed the `.gzne`
(new ecos, the sky), the zone loads again round your character where it stands instead: a loading screen, and
then the zone as saved. Opened on its own the editor can't move you, so leave the zone and come back (or, in a
big one, go a couple of kilometres off and back) yourself. What the client didn't list when it started (new
chunks, another zone) still needs Test in game. Models the client has
loaded stay cached until it restarts. The folder goes a few seconds after the client closes, so it can't
shadow later updates.

## Tools

| Tool | Key | |
| --- | --- | --- |
| Select | V | Click a prop, light or marker; Shift+click or drag a box to select several, and drag what's selected to slide it over the ground and props. G move, R rotate, T scale (or the transform bar over the view), arrows and PgUp/PgDn nudge, `,` `.` turn, `-` `=` scale, Del delete, Ctrl+D duplicate, Ctrl+C/V copy and paste, End drop down, H hide, F focus. A right-click opens what can be done to the object or spot under the pointer. A prop dragged into another chunk moves into it. |
| Sculpt | B | Raise and lower (Shift swaps them), smooth, flatten (to where the stroke started), slope (onto the plane under the brush where the stroke started, for lumpy hillsides), level (to a set height; Alt+click picks it), ramp (drag from one point to another), terrace (steps of a set height), erode (cliffs slide to a 35° slope), sharpen and noise. Keys 1 to 0 pick them. *Mould to a model* fits the ground to a prop or live model (right-click it and *Mould the ground to it*, or select it and *Use the selected model*): to its base, the lowest surface over each spot, so a building, platform or rock sits flush, dug in or built up; or to its top, the highest, to stamp a hill, ramp or rock model into the ground (then delete the model). The ground around it eases back over the blend distance; an offset raises or sinks it, and it can go both ways or only raise or only lower. One undo step. |
| Exclude | X | Cut holes in the ground for cave mouths and interiors, or restore it. Shift+click (or right-click) a cave, tunnel or any model, placed or live, to limit the brush to it or cut all the ground in it at once, by its shape: ground with the model above and below it (a cave's inside, a tunnel's bore; a hill over a tunnel stays), all ground under it, or its box. Its Caves and tunnels list places one that cuts its way in as it lands. Holes leave the ground's mesh and collision; a tile cut out entirely keeps one buried, degenerate triangle so its collision tree stays valid. |
| Eco | M | Paint any eco: the zone's own, or any of the client's (every zone's eco lists and its `.eco` definitions, 107 in all), searchable with texture swatches. One the zone doesn't have joins its eco list on the first stroke. The highlight shows where the selected eco is, and painting also tints the colour map toward the eco's colour, which is how the stock colour maps look (a flat tint per eco). Alt+click picks the eco under the pointer and its tint from the colour map, which painting then uses instead. A tile holds up to 5 ecos. |
| Colour | C | Paint the colour map directly; Alt+click picks a colour. |
| Env | N | Paint tile environments, the `ObjectTerrainData.xml` row that picks a tile's sky, minimap and atlas. 0 uses the zone's sky. *New environment…* adds one of your own (a name, sky, minimap tile set and atlas page; ids after the game's last), and *Edit* changes one; they go to clients in the `ClientPatches/Resources/ObjectTerrainData.xml` client patch, which the client reads when it starts. |
| Props | P | Place objects from the content browser (O): click a tile, then the ground, or drag a tile into the view. Shift+wheel turns the preview. Or scatter them with a brush. See Objects below. |
| Lights | L | Click the ground to add a point light; edit colour, range and intensity. |
| Chunks | K | Click an empty cell, in the view or on the minimap, to add a chunk. Its edges join the land around it. |
| Zone | Z | Spawn point and heading, zone sky or a custom sky XML, adding ecos (from FabledRealms' list or a client `.eco`), house exit door and build areas. |
| World | J | Places and warpstones, named areas, combat areas, portals, barriers (invisible walls) and seasons: see [The World tool](#the-world-tool). |

The **content browser** (Content, or O) shows all of the client's 10,000-odd actors and actor sets as rendered
thumbnails. It opens on what zones place (counted across every zone's chunks the first time, then cached,
most placed first), with groups for what the loaded chunks use, ★ favourites, recently placed, all actors and
name prefixes (bw_ Briarwood, sh_ Snowhill, hsg_ housing, ...), and filters by kind (trees, rocks, plants,
buildings, ...) and name. Characters and gear stay hidden unless asked for. Thumbnails are rendered in the
browser and kept in `ZoneEditor/.cache/thumbs2` (an older `thumbs` folder there is from before textures were drawn
the right way up, and can be deleted).

Fly with the right mouse button and WASD/QE (Shift for speed, the wheel changes speed; in brush tools the
wheel or `[` `]` sizes the brush and Ctrl+wheel or Shift+`[` `]` sets strength; every brush has a falloff:
smooth, linear, sphere or flat). I is the eyedropper: the ground under the pointer sets the level height,
the eco to paint and its tint, and the colour; the status bar shows the eco under the pointer. Ctrl+Z
undoes, Ctrl+S saves. The minimap shows chunks green and unsaved ones gold; click it to fly there. **View**
in the top bar holds the view distance, time of day and what's drawn (detail textures, shadows, tile grid,
point lights); **Keys ?** (or `?`) lists every key.

**Room to work.** ⛶ in the top bar (or F11) puts the editor full screen, in the admin panel too (its Map editor
header has Full screen as well). Drag the side panel's edge to widen it, or hide it with its arrow or `\`; ▾ hides
the minimap. The editor remembers these, and in a small view the minimap shrinks and the hint text goes.

Flying goes by the keys' place on the keyboard, so WASD/QE work on any layout. A text box keeps its typing,
but a ticked checkbox or a slider doesn't swallow the editor's keys. The browser's own shortcuts that clash
(Ctrl+D bookmark, Ctrl+S save page, F3 find, Alt and F10 menus, ...) are held back while the editor has
focus; Ctrl+W, Ctrl+T and Ctrl+N can't be. Flying stops when the window loses focus, so a key let go
elsewhere doesn't leave the camera drifting.

## Objects

**Tint and texture variants.** An actor's `.adr` lists texture aliases (named texture variants, such as a
housing block's tiger stripes, polka dots and cheese) and tint aliases (named colours: `dyetint`, `notint`,
and the gear colours). Each placed object names one of each and carries its own tint colour, and the
inspector's Look section sets them: a variant, a tint alias, a colour picker, swatches of the actor's own
tints and recent colours, and Copy look / Paste look. Only actors that list tint aliases can be tinted in
game; the panel says when a model has neither. Version 5 chunks have no room for these, so a chunk is saved
as version 6 when one of its objects gets a variant or tint (nothing else differs between the two). Each
object also has a draw distance (0 is the client's default).

**Selecting.** Shift+click adds or removes an object; dragging over the ground in the Select tool selects
everything in the box (Shift adds). Several selected move, turn (R) and scale (T) together round their
centre, and share an inspector: their look and draw distance, and the actions. Find objects lists the loaded
objects by name, nearest first, to select one or all.

**Moving, turning and scaling.** While something is selected the transform bar over the view picks the
gizmo (Move G, Rotate R, Scale T), World or Local axes, its size, and snapping for each: move in metres,
turn in degrees, scale in steps. Holding Shift while dragging switches snapping off, or on at the default
steps when it's off. The keyboard nudges without the gizmo: the arrows move along the ground (along the world
axis nearest to where the camera looks), PgUp and PgDn up and down, by the move snap or 0.25 m; `,` and `.`
turn by the turn snap or 15°; `-` and `=` scale. Shift makes a nudge 4× (90° for turns). A run of nudges is
one undo step.

**Actions** (on everything selected, from the inspector or a right-click): drop down (End: onto what's below
its base, the ground or a prop's top, so a crate dropped onto a table sits on the table), align to the slope, stand upright, random
turn, random scale (in the Props tool's range), reset; select all like this; hide the model (H, Shift+H
shows everything again), e.g. trees in the way of terrain work; replace the model with the content
browser's pick, keeping place, turn and scale; duplicate (Ctrl+D), copy and paste at the pointer (Ctrl+C,
Ctrl+V) and delete. Each is one undo step.

**Placing.** The Props tool places one object at a time or scatters them under a brush: a set of models (or
the content pick) dropped at random, no closer than a spacing, with Shift+drag erasing them again; one
undo step per stroke. Either way new objects can get a random turn, a random scale in a range, stand on the
slope, sit above or sink into the ground, and take a texture variant and tint. With *Land on props* a new
object lands on top of the prop under the pointer (untick it to place through props onto the ground), and with
*Cut the ground inside it* a cave or tunnel opens its mouth and bore as it lands; with *Mould the ground to it* the
ground meets a building's base (the Sculpt tool's mould settings) as it lands.

**Sliding and the right-click menu.** In the Select tool, press on what's selected and drag: it slides over the
ground and props under the pointer, as one undo step (live objects and the zone's own NPCs go to the game when
let go). A right-click (without dragging, which turns the view) opens what can be done to what's under the
pointer: focus, drop down, align, duplicate, copy, use as the content pick, select all like it, hide, cut the
ground inside it, delete; for live objects, make an NPC, bake, script; on the ground, place the content pick or
paste there, add a light, move the selection there, or send a selected player there.

## The viewport

It draws the world as the client does, with its coordinates as they are. The ground is each eco's detail
texture, blended by the eco weights (a chunk's 8 heaviest ecos; a tile has up to 5, a whole chunk up to 10 in
FabledRealms), times the colour map doubled, in gamma space, as the client's terrain shaders
(`runtimeterrain_*_def.fxo`) do. Every texture is uploaded as it's stored, top row first (the PNG fallback for
formats the DDS loader can't read too), and mesh UVs read it as they are, as the client's vertex shaders pass them
on: v runs down from the top row and wraps (most meshes store it negated, some, like `sh_corkboard_01`, not), so
texture sheets (house trim, the TCG arena's floor) show the right parts and a mask, glow or specular map or second
layer lines up with its colour texture. Props use their own normals and
diffuse textures, uploaded as the client's DDS files with their mipmaps, and their materials are drawn with the
game's shader math (`web/js/materials.js`, from the client's compiled effects):
- cutout foliage and fences drop pixels below the game's 0.78 alpha, and are two-sided where the material says;
- glow lights a surface with its own colour (lamps, lit windows, fairy flora, Blackspore's fly traps), masked by a
  GlowMap or the texture's alpha;
- specular highlights are masked the same way;
- a material's own tint colours it, and TintMask materials (housing walls, balloons) only where their mask says;
- UVs scroll where the material scrolls them, and a DualTexture sign or decal lays its second layer on its second
  UV set;
- water is lit through its three drifting wave maps and gets more opaque at a glancing angle;
- waterfalls slide two layers of their texture and two masks down at their own speeds;
- light beams (a stage's spotlight cones, LightBeam materials) are added light, not surfaces: their gradient
  texture times Intensity and Density, fading toward the cone's outline, which in daylight is a faint glow.

**Animation.** Animated props (birds circling, flags and banners, windmills, string lights, balloons) and the
live layer's NPCs and players play their `loc_stand` clip, as they do standing about in game. Lighting comes from the sky file in use, by time of day (View → Time of day):
sun colour, brightness and direction, fog, and sky and ground light from its ambient gradient. Without a zone
sky it follows the environment of the tile under the camera, like the client. The sun casts shadows near the
camera, and the zone's own point lights nearest the camera light the scene.

## The World tool

What's in the world beside the zone's chunks, edited where it stands: its markers drag (G) and turn (R) like the zone's
spawn, and all of it but seasons saves with the zone (Save).

- **Places and warpstones.** The map's points of interest (`src/Resources/PointOfInterests.json`; those without a
  `ZoneId` are FabledRealms'): a gold marker where each is, a blue one where a warp arrives, facing the way players
  will. A warpstone's atlas offers every warp destination (a hub). *Add a warpstone* adds a destination in the middle
  of the view with the name you give it, and, opened from the admin panel with the zone running, its stone: an NPC
  that opens the atlas, a copy of the game's own on a definition of its own in `Npcs.json`. *Add a place* adds an
  ordinary map place. The inspector renames it, sets its kind and atlas page, and spawns a destination's stone.
  Saving reloads them in the running game; players get them as they next enter the zone.
- **Areas.** The zone's named areas (its Sanctuary definition's `AreaDefinitions`): their name shows as players walk
  in. Round and square ones are drawn green and drag by their markers; areas by tile or environment keep their ground
  and are renamed here. Each can name its place, for the map, and be a combat area.
- **Combat.** The combat areas, where hostile NPCs roam: the wilds, not towns (players are in combat only while a
  mob has aggro on them, and the server's tests keep every hostile NPC a zone script places in one). The ground is
  tinted by tile: red where it's a combat area, blue where it's an area that isn't, grey in no area, found as the
  server finds a spot's area (its own tiles, then shapes, then the tile's environment). Tick a whole area
  (`IsCombatArea`), or add stretches (`CombatAreas`): circles, squares or outlines (drag their corners; add or delete
  corners in the inspector) that are a combat area, red, or aren't one, white, whatever area they're in, such as the
  spots in a town where hostile NPCs stand. The first stretch that holds a spot decides, so a new safe one goes first; *Earlier* and *Later* reorder them.
- **Portals.** Circles (purple) that send a player on: to the orange marker in this zone, or to a spot in another.
- **Barriers.** The zone file's invisible walls (drawn red), posts the client won't let players pass between. Pick
  one to drag its posts, add a post at the view, set its height or delete it; *Add a barrier* puts two posts across
  the view. They reach the client with the zone file: a running client reads it as it enters the zone again.
- **Seasons.** Seasonal holidays (`src/Resources/Seasons.json`: Halloween and winter to start with): event keys that
  are on between two days of the year, every year, as well as whenever `!event` turns them on. An NPC flagged for one
  (its *Spawned during*) is in the game only then, and anything placed can be *Seen during* one. Saving them has the
  running game reload them; a season starting or ending spawns or despawns its NPCs within a minute.

Areas, combat areas and portals are the zone's Sanctuary definition, which the Gateway reads when it starts: Test in game with
**+ servers**. Names are locale strings, which the client shows by id: a new name (or a renamed game one) is one of
Sanctuary's own (`src/Resources/LocaleStrings.json`, ids from 9500000), which goes to clients in the locale client
patch (`ClientMods/locale_strings.py` rebuilds it); the client reads its locale when it starts.

## Live: the running game, from the admin panel

Opened from the OSFR AdminPanel's Map editor tab (which serves it at `/zone-editor/` and signs referees in),
the editor is also a window on the running game: the **Live** switch in the top bar shows, and its dot is green
while the zone being edited is running on the Gateway (a world zone). Opened on its own, there's no game to
reach and the switch stays hidden. The Gateway needs its admin API on (`AdminApi:Key`, see Sanctuary's README).

- **What's there.** The zone's players, its NPCs, mobs and collection nodes within 220 units of the camera, and
  its placed objects (furniture props, world NPCs and placed models), as the server has them now, drawn from
  their own actors as the client puts them together (see How it works). Players glide between the server's
  updates and show on the minimap; names show close up. The Select tool's search finds them by name too.
- **The zone's own NPCs (admins).** An NPC or mob the zone's Lua scripts spawn (`src/Scripts/Zone/<script>.lua`,
  the zone definition's `Scripts`) can be moved, turned, dropped and deleted like a placed object, or made a
  placed NPC (*Make it a placed NPC*: a world NPC in the database where it stands, edited like any other). The
  inspector says which line spawns it (found by its guid, else by its definition and spot); each change shows in
  game at once and rewrites that line (numbers it didn't change stay as the script wrote them), or takes it out,
  so the zone starts that way. Ctrl+Z puts both back. One the script spawns from code (a variable spot), or that
  keeps it in a variable for more code, isn't rewritten, and the inspector says so; collection nodes come from
  their pools.
- **Placed objects are editable, live.** They glow faintly. Select one and G, R and T move, turn (yaw, pitch
  and roll) and scale it, as do the nudge keys; the inspector takes exact numbers, drops it to the ground
  (End), duplicates (Ctrl+D) and deletes it (Del). Each change goes to the server when the gizmo lets go (or
  the nudging stops): it's saved, and players in view see it at once. Ctrl+Z undoes it on the server.
- **Saves show in your client.** After a save that reached the client started by Test in game (see Test in
  game above), your character goes to far-off ground and straight back, so the client reloads the saved chunks.
  It only goes when it stands within 1.5 km of them; the status bar says what happened.
- **Placing live.** In the Props tool, with *Place in the running game* ticked (the default from the panel), a
  click (or a drop from the content browser) places the content pick as a placed model, with the tool's turn,
  scale, slope and height options. The game spawns models by id, so an actor needs a `Models.txt` row, in the
  server's copy and the client's. `add_model_rows.py` gives one to every actor the client can draw (see
  [Models.txt rows](#modelstxt-rows)).
- **Spawning NPCs.** The content browser's **NPCs** side lists Sanctuary's NPC definitions (`Npcs.json`): click
  one and then the ground to spawn it in the running game (only there: NPCs are the server's). With *As an NPC*
  in the Props tool, any actor spawns as an NPC instead of a placed model, on a definition of its own that the
  editor adds to `Npcs.json` the first time (ids from 50000). *Make it an NPC* in a placed model's inspector does
  the same for one already placed.
- **Settings.** A placed NPC's, model's or prop's inspector sets what `!npc` and `!prop` set in game: its name
  (empty: the definition's) and nameplate, animation, texture variant and tint alias, effect, height offset and
  overhead icons, and for an NPC its disposition (hostile ones fight as their Mobs.json mob), behaviour (stay,
  wander within a radius, or follow your character) and speed. Each change shows in game at once and is one
  undo step.
- **Who sees it.** An NPC can be flagged for a world event (it's only spawned while `!event on <key>` is), and
  anything placed can have conditions: only between hours of the game day (the clock the server syncs to clients,
  a day every three real hours), only for players who have a quest done, active, not done or not started, who
  carry an item (or don't), or only for referees. Players see it while all of them hold, checked every second, so
  it appears or goes as they finish the quest or the hour comes; referees in god mode always see it.
- **Conversations (admins).** *Edit conversation…* opens a world NPC's dialogue over the view, as `!conv` builds
  it: nodes of what it says, each with up to eight answers that go to another node or end it, give an item or
  call the NPC script's `onChoice(player, hook)`. Clicking the NPC in game starts it.
- **NPC definitions (admins).** *Edit definition…* (or *NPC definitions…* in the Props tool) opens `Npcs.json`
  over the view: search it, change any definition (name, actor, scale, aliases, disposition, animation, named
  scripts from `src/Scripts/Npc`, and everything else as JSON), make one for the content pick, copy one or delete
  one made here (while nothing placed is built on it). Saving writes `src/Resources/Npcs.json` in its own layout
  and the running Gateway's copy, has the Gateway reload it, and respawns this zone's placed NPCs built on it so
  they take it up (each keeps what was set on it, such as its name and scale). The zone's own NPCs, spawned by its
  Lua, take it up when the Gateway restarts.
- **The admin panel's GameData editor.** The panel's own NPC and reward table editors save through the editor
  too: `GET /api/npcs`, `/api/npcs/entry`, `POST /api/npcs/save|new|delete` for `Npcs.json`, and `GET /api/rewards`,
  `POST /api/rewards/save|delete` for `src/Resources/Rewards.json`. Each reward table lists what rolls it (mobs,
  consumables and item abilities by their `RewardTable` fields, minigames, and the server's code and scripts by
  the key as a string), and one in use can't be renamed or deleted. Saves keep the files' layout and go to the
  running servers' copies; the panel then has the Gateway reload them. A definition can carry a `Conversation`
  (`Start` and `Nodes`, as the game checks them): every NPC spawned from it says that when clicked, unless it has
  its own, and a reload changes what they say from their next click.
- **The admin panel's jobs, abilities, battles and titles.** Each saves every file it spans at once, checked first
  as the servers check them: `GET /api/jobs`, `/api/jobs/entry`, `POST /api/jobs/save|delete` (a job's
  `Profiles.json` entry, its `CombatJobs.json` kit and the `CombatAbilities.json` abilities it fights with; new
  abilities are named by negative stand-in ids until the save gives them theirs, and no kit can be left naming an
  ability that doesn't exist, which would stop the Gateway starting); `GET /api/abilities`, `POST
  /api/abilities/save|delete`; `GET /api/battles`, `/api/battles/options`, `/api/battles/entry`, `POST
  /api/battles/save|delete` (`Battles.json` with the `Mobs.json` mobs its waves fight as); and `GET /api/titles`,
  `POST /api/titles/save|delete` (`PlayerTitles.json`). Names travel as text: text a string already says keeps its
  id, and new text becomes a Sanctuary string with the locale patch rebuilt once per save. `GET /api/icons` and
  `/api/client-ids?kind=animation|effect|imageset` search the client's images, animations, composite effects and
  image sets by name. Saves keep each file's layout, PowerShell's included (`CombatAbilities.json`), and go to the
  Login server's copy of the resources too.
- **The admin panel's Coin Store and icons.** The panel's Store page changes the coin store's files through the
  editor too: `GET /api/store/categories` (what the client calls each category), `POST /api/store/save|remove`
  for `src/Resources/CoinStoreItems.json` and prices (`Cost`, `MemberDiscount`, `ResellValue`) in
  `ClientItemDefinitions.json`, and `POST /api/store/bundle/save|delete` for `src/Resources/CoinStoreBundles.json`.
  Only the entries an edit changes are rewritten, so the 28 MB item file stays as it was otherwise. `GET
  /api/icon?set=|image=|notification=&size=&tint=` draws a game icon as PNG from the client's image tables (an
  item's or mount's image set, a title's or house's image, an NPC's overhead icon), tinted where its mask says;
  the panel shows them wherever it lists items, mounts and houses.
- **Baking.** *Bake into the zone* turns a placed object into one of the chunk's own props, where it stands,
  and takes it out of the game: build in the live world, then make it part of the zone (Save, Test in game).
- **Players.** Selecting one shows what they wear, and sends them to the ground in the middle of the view,
  messages or kicks them. The zone's own NPCs are shown as they are; they're edited in their data files.
- **Scripts (admins).** A placed NPC, model or prop can carry a GM script, Lua with hooks such as
  `onInteract(player)` and `onProximity(player)` that make it talk, move and react, as the in-game GM panel's
  Script tab does. *Edit script…* in its inspector opens the editor over the view: Save compiles and runs it on
  the server first (a broken script never replaces a working one; the message says why), Test runs `onSpawn`
  and then `onInteract` on your character (it has to be in the zone, and what the hooks do really happens),
  Insert adds a hook, and API lists everything a script can call. The library beside it has the shipped
  templates, which *Use on it* puts on the object with the parameters you fill in, and scripts saved there
  with *Save to library…*. Ctrl+S saves, Tab indents, Esc closes.

Terrain, eco and colour paint, the zone's baked props, lights, sky and spawn are still the zone's files: the
game has no way to send those to a client that has the zone loaded, so they ship as before.

### Models.txt rows

A placed model is an NPC whose model is a `Models.txt` row's id, which the server and the client each look up
in their own copy of the table. The stock rows cover about a third of the client's actors, so
`add_model_rows.py` appends a row for every other actor and actor set the client can draw, with ids from 10000
up (stock ids stop below 5000, and `fr_maya_mcp`'s new models start at 20000) and the defaults most stock rows
have. Rows are only ever appended, so a placed model's id keeps meaning the same actor; run it again after
publishing new actors, and once in a fresh checkout (the client patches aren't in git).

```
python ZoneEditor/add_model_rows.py
```

It writes `src/Resources/Models.txt`, and the running Gateway's and Login's copies (they reload it when it
changes), and the same file as the client patch `src/Resources/ClientPatches/Resources/Models.txt`, which
launchers download before Play. The client reads it only at start-up: `launch.ps1` puts the client patches in
before it starts the client, as does `ClientMods/install_client_mods.py`.

Some stock actors name a mesh that the client's manifest lists but the asset server never had, so the client
draws them as nothing; they get no row, and the content browser leaves them out.

## How it works

Chunks are read and written with `ClientMods/world_chunks.py` (every stock version 5 and 6 chunk
round-trips byte for byte); `zoneeditor/formats.py` adds the `.gzne` zone file, point lights, the eco blend
map as per-eco weights and colour maps. The editor holds each chunk as a 2-unit heightfield, which is the
stock meshes' own grid; saving rebuilds only the tiles whose heights changed, with their Bullet collision
trees (`ClientMods/zone_terrain.py`), and keeps the rest as they were. The eco blend map's channel order
and layering were matched against the stock colour maps.

Characters are put together as the client does from its AddPc and AddNpc packets (`web/js/live.js`): the
Gateway sends each player's and NPC's look as actor parts (`zoneeditor/models.py` reads the rest). An `.agr` actor
set lists a character's parts: its race's body in a skin tone, a head with face paint, hair and facial hair in
the set's hair tint, and gear, each with the texture aliases it wears (one per material slot) and its tint alias.
A player's look is the same from their own data, with item actors' `<race>`, `<gender>` and `<hairtype>`
(`hat` under headgear) filled in. Every part's meshes are skinned to the body's skeleton (`.dsk`, `DSKE`: bones
relative to their parents; parts made on another skeleton are matched by bone name); rigid gear with an attach
point in its `.adr` (record 15: a weapon's `R_weapon`) hangs from that bone, so it follows the body as it moves.
A mesh's skinning is in its `.dme` after the meshes: each vertex names palette registers (BlendIndices, four bytes)
with three weights, each draw call loads bones into the registers for its run of vertices, and the mesh's bones
are named by the Jenkins hash of the skeleton's. An actor's clips are its `.adr` record 9 (name, `.gr2` file,
duration); `loc_stand` is the idle. Clips are Granny files, whose curves are compressed in many encodings and
whose sections are Oodle-compressed, so the server samples them with RAD's own runtime (`granny2.dll`, x64:
`FR_GRANNY_DLL`, else `D:\FreeRealms\granny2.dll`; `zoneeditor/granny.py`) 30 times a second into keyframes,
kept in `.cache/anims`. Without the DLL nothing animates. Each mesh's
textures are told apart by its material's parameter names (Jenkins hashes): `Diffuse`, `Diffuse2` (a head's face
paint, laid over the skin by its alpha) and `TintMask` (greyscale: where the tint colours it). A tint is the
part's tint alias's colour on its actor, else its `Tints.xml` colour (a dye, or the hair colour; `dyetint` says
the dye decides), applied as the client's shaders do: twice the colour in gamma space, over the mask or, for
hair, the whole mesh.

Not covered yet: chunks older than version 5 (a few minigame zones) don't load, flora isn't drawn, and
invisible walls, client areas and groups are kept as they are but not editable. Particle effects (fires,
torches, moths, mist) aren't drawn, characters only play their idle, and eye colour isn't applied.
