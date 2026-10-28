// The World tool (J): what's in the world beside a zone's chunks, edited where it stands.
// - Places: the map's points of interest (src/Resources/PointOfInterests.json), the warpstones' destinations among
//   them. Each has a place marker (gold) and where a warp arrives (blue, facing the way players will). A new
//   warpstone is a destination and, in the running game, its stone: an NPC that opens the atlas, on a definition of
//   its own in Npcs.json.
// - Areas: the zone's named areas (its Sanctuary definition's AreaDefinitions), whose name shows as players walk in.
// - Portals: circles that send a player on to a spot, in this zone or another (Portals).
// - Barriers: the zone file's invisible walls, posts the client won't let players walk between.
// - Seasons: seasonal holidays (src/Resources/Seasons.json), event keys that are on between two days of the year.
// Names are locale strings: new ones are Sanctuary's own (api/names). All of it but seasons saves with the zone.
import * as THREE from "three";
import { getJson, postJson } from "./api.js";
import { makeMarker } from "./props.js";

const MODES = [["places", "Places"], ["areas", "Areas"], ["portals", "Portals"], ["barriers", "Barriers"], ["seasons", "Seasons"]];
const POI_TYPES = { 7: "Warpstone destination", 6: "Place", 3: "Dungeon", 10: "Ruins" };
// A warp destination as the game's own are: the warpstone icon, "Press to Teleport..." under its name.
const HUB = { IconId: 1345, NotificationType: 7, SubNameId: 382845, Unknown: 651, Unknown2: 30 };
const PLACE = { IconId: 7555, NotificationType: 6, SubNameId: 382862 };
// The stock warpstone new stones are copies of (Shrouded Glade's): sg_warpstone_01, opening the atlas.
const WARPSTONE = { id: 2163, model: "sg_warpstone_01.adr" };
const SANCTUARY_STRINGS = 9500000;
const WALL_HEIGHT = 100;
const COLOURS = { place: 0xe8b84a, arrival: 0x3fd0ff, area: 0x6be27a, portal: 0xc77dff, destination: 0xff8a5c, post: 0xe2675f };
const MONTHS = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];

function escape(text) {
  return String(text ?? "").replace(/[&<>"']/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" })[c]);
}

const round = (v, places = 3) => Math.round(v * 10 ** places) / 10 ** places;

export class WorldTool {
  // ctx: { state, live, toast(text, kind), status(text), changed() (the zone has unsaved world edits), redraw()
  // (markers and walls again), markers() -> the scene's markers, centre() -> the ground in the middle of the view,
  // ground(x, z) -> height, environmentAt(x, z) -> a tile's environment, select(object), focus(object),
  // inspector() -> the panel's inspector element, renderPanel(), forward() -> the camera's heading (radians, 0
  // along +z, towards +x) }
  constructor(ctx) {
    this.ctx = ctx;
    this.mode = "places";
    this.zone = null;
    this.selectedWall = null;
    this.clear();
  }

  clear() {
    this.pois = [];
    this.poisDeleted = [];
    this.areas = [];
    this.portals = [];
    this.walls = [];
    this.names = {};
    this.dirty = new Set();
  }

  // A zone opened (or opened again after a save): its places, areas, portals and walls as saved.
  load(zone) {
    this.clear();
    this.zone = zone;
    this.pois = structuredClone(zone.pois ?? []);
    this.areas = structuredClone(zone.definition?.AreaDefinitions ?? []);
    this.portals = structuredClone(zone.definition?.Portals ?? []);
    this.walls = structuredClone(zone.walls ?? []);
    this.names = { ...(zone.names ?? {}) };
    this.selectedWall = null;
  }

  // What a save sends (only the parts changed): settings for the server's save_zone.
  settings() {
    const out = {};
    if (this.dirty.has("pois")) { out.pois = this.pois; out.poisDeleted = this.poisDeleted; }
    if (this.dirty.has("areas")) out.areas = this.areas;
    if (this.dirty.has("portals")) out.portals = this.portals;
    if (this.dirty.has("walls")) out.walls = this.walls;
    return out;
  }

  touch(part) {
    this.dirty.add(part);
    this.ctx.changed();
  }

  name(id) {
    return id ? this.names[id] ?? `string ${id}` : "";
  }

  // A name typed for something whose NameId is nameId: one of Sanctuary's own strings changes; a stock one stays
  // and a new Sanctuary string takes its place. Returns the id to use.
  async rename(nameId, text) {
    text = text.trim();
    if (!text) throw new Error("Give it a name.");
    const { id } = nameId >= SANCTUARY_STRINGS
      ? await postJson("api/names/set", { id: nameId, text })
      : await postJson("api/names/new", { text });
    this.names[id] = text;
    return id;
  }

  // --- the view ------------------------------------------------------------------------------------------

  // Markers to drag (kind "marker", with userData.world and apply) and outlines to see, for buildMarkers.
  markers() {
    const out = [];
    const ground = (x, z, fallback = 0) => this.ctx.ground(x, z) ?? fallback;
    const marker = (colour, label, world, [x, y, z], heading = null) => {
      const m = makeMarker(colour, label);
      m.position.set(x, y, z);
      if (heading !== null) m.rotation.y = heading;
      m.userData.world = world;
      m.userData.apply = () => { this.moved(m); };
      out.push(m);
      return m;
    };
    this.pois.forEach((poi, i) => {
      const label = this.name(poi.NameId) || `Place ${poi.Id ?? "(new)"}`;
      marker(COLOURS.place, label, { type: "poi", index: i }, poi.Position);
      // Arrival faces the server's heading h, along (sin h, cos h) as PacketZoneTeleportRequestHandler sends it; the
      // marker's arrow points along its +x.
      marker(COLOURS.arrival, `${label}: arrival`, { type: "arrival", index: i }, poi.SpawnPosition, poi.Heading - Math.PI / 2);
    });
    this.areas.forEach((area, i) => {
      if (area.Shape === "Circle") {
        marker(COLOURS.area, this.name(area.NameId) || area.Comment || `Area ${area.Id}`, { type: "area", index: i }, [area.X1, ground(area.X1, area.Z1), area.Z1]);
        out.push(this.ring(area.X1, area.Z1, area.Radius, COLOURS.area));
      } else if (area.Shape === "Rectangle") {
        marker(COLOURS.area, `${this.name(area.NameId) || area.Comment || `Area ${area.Id}`} (corner)`, { type: "area", index: i }, [area.X1, ground(area.X1, area.Z1), area.Z1]);
        marker(COLOURS.area, `${this.name(area.NameId) || area.Comment || `Area ${area.Id}`} (other corner)`, { type: "area2", index: i }, [area.X2, ground(area.X2, area.Z2), area.Z2]);
        out.push(this.outline([[area.X1, area.Z1], [area.X2, area.Z1], [area.X2, area.Z2], [area.X1, area.Z2]], COLOURS.area));
      }
    });
    this.portals.forEach((portal, i) => {
      if (portal.Shape !== "Circle" && portal.Shape !== "Rectangle") return;
      marker(COLOURS.portal, portal.Comment || `Portal ${i + 1}`, { type: "portal", index: i }, [portal.X1, ground(portal.X1, portal.Z1), portal.Z1]);
      out.push(portal.Shape === "Circle" ? this.ring(portal.X1, portal.Z1, portal.Radius, COLOURS.portal)
        : this.outline([[portal.X1, portal.Z1], [portal.X2, portal.Z1], [portal.X2, portal.Z2], [portal.X1, portal.Z2]], COLOURS.portal));
      if (portal.ToZoneId === this.zone?.definition?.Id && portal.ToPosition) {
        marker(COLOURS.destination, `${portal.Comment || `Portal ${i + 1}`}: arrival`, { type: "destination", index: i }, portal.ToPosition, (portal.ToHeading ?? 0) - Math.PI / 2);
      }
    });
    if (this.mode === "barriers" && this.selectedWall !== null) {
      const wall = this.walls[this.selectedWall];
      for (let p = 0; p + 1 < (wall?.length ?? 0); p += 2) {
        marker(COLOURS.post, `Barrier ${this.selectedWall + 1}, post ${p / 2 + 1}`, { type: "post", wall: this.selectedWall, post: p / 2 }, wall[p]);
      }
    }
    return out;
  }

  ring(x, z, radius, colour) {
    const points = [];
    for (let i = 0; i <= 48; i++) {
      const a = (i / 48) * Math.PI * 2;
      const px = x + Math.cos(a) * radius, pz = z + Math.sin(a) * radius;
      points.push(new THREE.Vector3(px, (this.ctx.ground(px, pz) ?? 0) + 0.4, pz));
    }
    return this.line(points, colour);
  }

  outline(corners, colour) {
    const points = [...corners, corners[0]].map(([x, z]) => new THREE.Vector3(x, (this.ctx.ground(x, z) ?? 0) + 0.4, z));
    return this.line(points, colour);
  }

  line(points, colour) {
    const line = new THREE.Line(new THREE.BufferGeometry().setFromPoints(points), new THREE.LineBasicMaterial({ color: colour, depthTest: false }));
    line.renderOrder = 4;
    line.userData = { kind: "area" };
    return line;
  }

  // A marker was dragged or turned: what it stands for follows it.
  moved(m) {
    const { type, index, wall, post } = m.userData.world;
    const at = [round(m.position.x), round(m.position.y), round(m.position.z)];
    if (type === "poi") { this.pois[index].Position = at; this.touch("pois"); }
    else if (type === "arrival") {
      Object.assign(this.pois[index], { SpawnPosition: at, Heading: round(m.rotation.y + Math.PI / 2, 4) });
      this.touch("pois");
    } else if (type === "area") { Object.assign(this.areas[index], { X1: at[0], Z1: at[2] }); this.touch("areas"); }
    else if (type === "area2") { Object.assign(this.areas[index], { X2: at[0], Z2: at[2] }); this.touch("areas"); }
    else if (type === "portal") {
      const portal = this.portals[index];
      if (portal.Shape === "Rectangle") Object.assign(portal, { X2: round(portal.X2 + at[0] - portal.X1), Z2: round(portal.Z2 + at[2] - portal.Z1) });
      Object.assign(portal, { X1: at[0], Z1: at[2] });
      this.touch("portals");
    } else if (type === "destination") {
      Object.assign(this.portals[index], { ToPosition: at, ToHeading: round(m.rotation.y + Math.PI / 2, 4) });
      this.touch("portals");
    } else if (type === "post") {
      const points = this.walls[wall];
      const height = points[post * 2 + 1][1] - points[post * 2][1] || WALL_HEIGHT;
      points[post * 2] = at;
      points[post * 2 + 1] = [at[0], round(at[1] + height), at[2]];
      this.touch("walls");
    }
    this.ctx.redraw();
  }

  // --- the panel ---------------------------------------------------------------------------------------------

  renderPanel(panel) {
    const live = !!this.ctx.live.zone;
    const definition = this.zone?.definition;
    panel.innerHTML = `<h3>World</h3>
      <div class="seg">${MODES.map(([key, label]) => `<button data-world-mode="${key}" class="${this.mode === key ? "active" : ""}">${label}</button>`).join("")}</div>
      ${definition || this.mode === "barriers" || this.mode === "seasons" ? this[`${this.mode}Panel`](live) : `<p class="warn">This zone has no Sanctuary definition (Zones/${escape(this.zone?.name)}.json), so it has no places, areas or portals.</p>`}
      <div id="inspector"></div>`;
    for (const button of panel.querySelectorAll("[data-world-mode]")) {
      button.addEventListener("click", () => { this.mode = button.dataset.worldMode; this.selectedWall = null; this.ctx.redraw(); this.ctx.renderPanel(); });
    }
    this[`${this.mode}Bind`]?.(panel);
  }

  focusMarker(type, index) {
    const m = this.ctx.markers().find((x) => x.userData.world?.type === type && x.userData.world.index === index);
    if (!m) return;
    this.ctx.select(m);
    this.ctx.focus(m);
  }

  // Places --------------------------------------------------------------------------------------------------

  placesPanel(live) {
    const rows = this.pois.map((poi, i) => ({ poi, i })).sort((a, b) => (b.poi.NotificationType === 7) - (a.poi.NotificationType === 7) || this.name(a.poi.NameId).localeCompare(this.name(b.poi.NameId)));
    return `<p class="hint">The map's places: gold where it is, blue where a warp arrives (turn it to face the way players will).
        Warpstones offer every destination; saving reloads them in the running game, and players get them as they next enter the zone.</p>
      <label>Name <input id="world-name" maxlength="120" placeholder="Mushroom Hollow"></label>
      <div class="row"><button id="add-warpstone" title="A destination here${live ? ", and its stone" : ""} (the middle of the view)">Add a warpstone</button><button id="add-place">Add a place</button></div>
      ${live ? "" : '<p class="hint">Opened from the admin panel with the zone running, a new warpstone gets its stone (an NPC that opens the atlas) too.</p>'}
      <div class="list">${rows.map(({ poi, i }) => `<div data-poi="${i}">${poi.NotificationType === 7 ? "⬢ " : ""}${escape(this.name(poi.NameId) || poi.Comment || `Place ${poi.Id}`)}
        <small>${escape(POI_TYPES[poi.NotificationType] ?? "")}${poi.Id ? ` · #${poi.Id}` : " · new"}</small></div>`).join("") || '<p class="hint">None yet.</p>'}</div>`;
  }

  placesBind(panel) {
    for (const row of panel.querySelectorAll("[data-poi]")) row.addEventListener("click", () => this.focusMarker("poi", +row.dataset.poi));
    panel.querySelector("#add-warpstone").addEventListener("click", () => this.addPlace(panel.querySelector("#world-name").value, true));
    panel.querySelector("#add-place").addEventListener("click", () => this.addPlace(panel.querySelector("#world-name").value, false));
  }

  async addPlace(text, warpstone) {
    const at = this.ctx.centre();
    if (!at) return this.ctx.toast("Point the view at the ground where it goes.");
    let nameId;
    try {
      nameId = await this.rename(0, text || (warpstone ? "New Warpstone" : "New Place"));
    } catch (error) {
      return this.ctx.toast(error.message, "error");
    }
    // Players arrive a few metres in front of it, facing away from the camera.
    const heading = this.ctx.forward();
    const position = [round(at.x), round(at.y), round(at.z)];
    const arrival = [round(at.x + Math.sin(heading) * 4), round(this.ctx.ground(at.x + Math.sin(heading) * 4, at.z + Math.cos(heading) * 4) ?? at.y), round(at.z + Math.cos(heading) * 4)];
    const poi = { Comment: text || undefined, Id: null, NameId: nameId, Position: position, SpawnPosition: arrival, Heading: round(heading, 4),
      ...(warpstone ? HUB : PLACE), AtlasName: this.atlas(at) };
    this.pois.push(poi);
    this.touch("pois");
    this.ctx.redraw();
    this.ctx.renderPanel();
    if (warpstone && this.ctx.live.zone) await this.spawnStone(poi);
    this.ctx.status(`Added ${text || "a place"}; Save writes it to PointOfInterests.json.`);
  }

  // The atlas page a spot is on: its tile environment's (ObjectTerrainData), else the zone's first.
  atlas(at) {
    const env = this.ctx.environmentAt?.(at.x, at.z);
    const row = this.zone.environments.find((e) => e.id === env);
    return row?.atlas || this.zone.environments.find((e) => e.atlas)?.atlas || "sacredgrove";
  }

  // A warpstone's stone in the running game: a copy of the game's own, on a definition of its own named like the
  // destination, spawned beside it.
  async spawnStone(poi) {
    const name = this.name(poi.NameId);
    try {
      const { definition } = await postJson("api/npcs/new", { model: WARPSTONE.model, name, copyOf: WARPSTONE.id });
      definition.Notification = { ...(definition.Notification ?? { NotificationType: 6, IconId: 3 }), NameId: poi.NameId };
      delete definition.NameId;
      await postJson("api/npcs/save", definition);
      await this.ctx.live.reloadDefinitions().catch(() => new Promise((resolve) => setTimeout(resolve, 1500)));
      const [x, y, z] = poi.Position;
      const ok = await this.ctx.live.spawnNpc(definition.Id, name, { position: [x, y, z], rotation: [poi.Heading, 0, 0], scale: 1 });
      if (ok) this.ctx.status(`Spawned ${name}'s stone (NPC #${definition.Id}) in game.`);
    } catch (error) {
      this.ctx.toast(`Couldn't spawn the stone: ${error.message}`, "error");
    }
  }

  inspectPoi(target, index) {
    const poi = this.pois[index];
    const atlases = [...new Set(this.zone.environments.map((e) => e.atlas).filter(Boolean))];
    target.innerHTML = `<h4>${escape(this.name(poi.NameId) || "Place")}</h4>
      <p class="hint">${poi.Id ? `Point of interest #${poi.Id}` : "New: saving gives it an id"} · name string ${poi.NameId}${poi.NameId >= SANCTUARY_STRINGS ? " (Sanctuary's)" : " (the game's: renaming makes a new one)"}</p>
      <label>Name <input data-poi-name value="${escape(this.name(poi.NameId))}"></label>
      <div class="two">
        <label>Kind <select data-poi-field="NotificationType">${Object.entries(POI_TYPES).map(([k, v]) => `<option value="${k}" ${+k === poi.NotificationType ? "selected" : ""}>${v}</option>`).join("")}</select></label>
        <label>Atlas page <select data-poi-field="AtlasName">${atlases.map((a) => `<option ${a === poi.AtlasName ? "selected" : ""}>${escape(a)}</option>`).join("")}</select></label>
      </div>
      <label>Arrival heading (degrees) <input type="number" step="5" data-poi-heading value="${round(poi.Heading * 180 / Math.PI, 1)}"></label>
      <div class="row">${this.ctx.live.zone && poi.NotificationType === 7 ? '<button data-poi-stone>Spawn its stone here</button>' : ""}<button data-poi-delete class="danger">Delete</button></div>`;
    target.querySelector("[data-poi-name]").addEventListener("change", async (e) => {
      try {
        poi.NameId = await this.rename(poi.NameId, e.target.value);
        this.touch("pois");
        this.ctx.redraw();
        this.ctx.renderPanel();
      } catch (error) {
        this.ctx.toast(error.message, "error");
      }
    });
    for (const select of target.querySelectorAll("[data-poi-field]")) {
      select.addEventListener("change", () => {
        const field = select.dataset.poiField;
        poi[field] = field === "NotificationType" ? +select.value : select.value;
        if (field === "NotificationType") Object.assign(poi, +select.value === 7 ? HUB : { IconId: poi.IconId === HUB.IconId ? PLACE.IconId : poi.IconId });
        this.touch("pois");
        this.ctx.renderPanel();
      });
    }
    target.querySelector("[data-poi-heading]").addEventListener("change", (e) => {
      poi.Heading = round(+e.target.value * Math.PI / 180, 4);
      this.touch("pois");
      this.ctx.redraw();
    });
    target.querySelector("[data-poi-stone]")?.addEventListener("click", () => this.spawnStone(poi));
    target.querySelector("[data-poi-delete]").addEventListener("click", () => {
      if (!confirm(`Delete ${this.name(poi.NameId) || "this place"}? Warpstones stop offering it once saved.`)) return;
      if (poi.Id) this.poisDeleted.push(poi.Id);
      this.pois.splice(index, 1);
      this.touch("pois");
      this.ctx.select(null);
      this.ctx.redraw();
      this.ctx.renderPanel();
    });
  }

  // Areas -----------------------------------------------------------------------------------------------------

  areasPanel() {
    return `<p class="hint">Named areas: their name shows in game as players walk in. Circles and rectangles are drawn green; drag their markers.
        Areas by tile or environment keep their ground and change name here. They load when the Gateway starts (Test in game with + servers).</p>
      <label>Name <input id="world-name" maxlength="120" placeholder="Mushroom Hollow"></label>
      <div class="row"><button id="add-circle">Add a round area</button><button id="add-rectangle">Add a square one</button></div>
      <div class="list">${this.areas.map((area, i) => `<div data-area="${i}">${escape(this.name(area.NameId) || area.Comment || `Area ${area.Id}`)}
        <small>${area.Shape ? escape(area.Shape) : area.Tiles?.length ? `${area.Tiles.length} tiles` : area.EnvironmentIds?.length ? `environments ${area.EnvironmentIds.join(", ")}` : ""} · #${area.Id}</small></div>`).join("") || '<p class="hint">None yet.</p>'}</div>`;
  }

  areasBind(panel) {
    for (const row of panel.querySelectorAll("[data-area]")) {
      row.addEventListener("click", () => {
        const area = this.areas[+row.dataset.area];
        if (area.Shape === "Circle" || area.Shape === "Rectangle") this.focusMarker("area", +row.dataset.area);
        else this.inspectStandalone("area", +row.dataset.area);
      });
    }
    panel.querySelector("#add-circle").addEventListener("click", () => this.addArea(panel.querySelector("#world-name").value, "Circle"));
    panel.querySelector("#add-rectangle").addEventListener("click", () => this.addArea(panel.querySelector("#world-name").value, "Rectangle"));
  }

  async addArea(text, shape) {
    const at = this.ctx.centre();
    if (!at) return this.ctx.toast("Point the view at the ground where it goes.");
    let nameId;
    try {
      nameId = await this.rename(0, text || "New Area");
    } catch (error) {
      return this.ctx.toast(error.message, "error");
    }
    const id = Math.max(0, ...this.areas.map((a) => a.Id ?? 0)) + 1;
    const x = round(at.x), z = round(at.z);
    const area = { Comment: text || undefined, Id: id, NameId: nameId, AreaId: 0, ChatAreaNameId: nameId, PointOfInterestId: 0, Shape: shape,
      X1: shape === "Circle" ? x : x - 40, Z1: shape === "Circle" ? z : z - 40, ...(shape === "Circle" ? { Radius: 40 } : { X2: x + 40, Z2: z + 40 }) };
    this.areas.push(area);
    this.touch("areas");
    this.ctx.redraw();
    this.ctx.renderPanel();
    this.focusMarker("area", this.areas.length - 1);
  }

  inspectArea(target, index) {
    const area = this.areas[index];
    target.innerHTML = `<h4>${escape(this.name(area.NameId) || area.Comment || `Area ${area.Id}`)}</h4>
      <p class="hint">Area #${area.Id} · ${escape(area.Shape || "tiles and environments")}</p>
      <label>Name <input data-area-name value="${escape(this.name(area.NameId))}"></label>
      ${area.Shape === "Circle" ? `<label>Radius (m) <input type="number" min="1" step="1" data-area-radius value="${area.Radius}"></label>` : ""}
      <label>Its place <select data-area-poi><option value="0">none</option>${this.pois.filter((p) => p.Id).map((p) => `<option value="${p.Id}" ${p.Id === area.PointOfInterestId ? "selected" : ""}>${escape(this.name(p.NameId) || `#${p.Id}`)}</option>`).join("")}</select></label>
      <div class="row"><button data-area-delete class="danger">Delete</button></div>`;
    target.querySelector("[data-area-name]").addEventListener("change", async (e) => {
      try {
        area.NameId = await this.rename(area.NameId, e.target.value);
        area.ChatAreaNameId = area.NameId;
        this.touch("areas");
        this.ctx.redraw();
        this.ctx.renderPanel();
      } catch (error) {
        this.ctx.toast(error.message, "error");
      }
    });
    target.querySelector("[data-area-radius]")?.addEventListener("change", (e) => { area.Radius = Math.max(1, +e.target.value); this.touch("areas"); this.ctx.redraw(); });
    target.querySelector("[data-area-poi]").addEventListener("change", (e) => { area.PointOfInterestId = +e.target.value; this.touch("areas"); });
    target.querySelector("[data-area-delete]").addEventListener("click", () => {
      if (!confirm(`Delete the area ${this.name(area.NameId) || area.Id}?`)) return;
      this.areas.splice(index, 1);
      this.touch("areas");
      this.ctx.select(null);
      this.ctx.redraw();
      this.ctx.renderPanel();
    });
  }

  // An area with no marker of its own (by tile or environment): its inspector in the panel.
  inspectStandalone(type, index) {
    this.ctx.select(null);
    const target = this.ctx.inspector();
    if (target && type === "area") this.inspectArea(target, index);
  }

  // Portals ---------------------------------------------------------------------------------------------------

  portalsPanel() {
    return `<p class="hint">Walking into a portal (purple) sends a player on: to the orange marker in this zone, or to a spot in another.
        Portals load when the Gateway starts (Test in game with + servers).</p>
      <div class="row"><button id="add-portal">Add a portal</button></div>
      <div class="list">${this.portals.map((portal, i) => `<div data-portal="${i}">${escape(portal.Comment || `Portal ${i + 1}`)}
        <small>to ${escape(this.zoneName(portal.ToZoneId))}</small></div>`).join("") || '<p class="hint">None yet.</p>'}</div>`;
  }

  portalsBind(panel) {
    for (const row of panel.querySelectorAll("[data-portal]")) row.addEventListener("click", () => this.focusMarker("portal", +row.dataset.portal));
    panel.querySelector("#add-portal").addEventListener("click", () => {
      const at = this.ctx.centre();
      if (!at) return this.ctx.toast("Point the view at the ground where it goes.");
      const heading = this.ctx.forward();
      const to = [round(at.x + Math.sin(heading) * 30), round(at.y), round(at.z + Math.cos(heading) * 30)];
      to[1] = round(this.ctx.ground(to[0], to[2]) ?? at.y);
      this.portals.push({ Comment: "New portal", Shape: "Circle", X1: round(at.x), Z1: round(at.z), Radius: 6,
        ToZoneId: this.zone.definition.Id, ToPosition: to, ToHeading: round(heading, 4) });
      this.touch("portals");
      this.ctx.redraw();
      this.ctx.renderPanel();
      this.focusMarker("portal", this.portals.length - 1);
    });
  }

  zoneName(id) {
    return this.ctx.state.zones.find((z) => z.id === id)?.name ?? `zone ${id}`;
  }

  inspectPortal(target, index) {
    const portal = this.portals[index];
    const zones = this.ctx.state.zones.filter((z) => z.id);
    target.innerHTML = `<h4>${escape(portal.Comment || `Portal ${index + 1}`)}</h4>
      <label>Label <input data-portal-comment value="${escape(portal.Comment ?? "")}"></label>
      ${portal.Shape === "Circle" ? `<label>Radius (m) <input type="number" min="1" step="1" data-portal-radius value="${portal.Radius}"></label>` : ""}
      <label>On to <select data-portal-zone>${zones.map((z) => `<option value="${z.id}" ${z.id === portal.ToZoneId ? "selected" : ""}>${escape(z.name)} (${z.id})</option>`).join("")}</select></label>
      <label>Arriving at</label><div class="xyz">${portal.ToPosition.map((v, k) => `<input data-portal-to="${k}" type="number" step="0.5" value="${v}">`).join("")}</div>
      <label>Facing (degrees) <input type="number" step="5" data-portal-heading value="${round((portal.ToHeading ?? 0) * 180 / Math.PI, 1)}"></label>
      <div class="row"><button data-portal-delete class="danger">Delete</button></div>`;
    const change = () => { this.touch("portals"); this.ctx.redraw(); };
    target.querySelector("[data-portal-comment]").addEventListener("change", (e) => { portal.Comment = e.target.value.trim() || undefined; change(); this.ctx.renderPanel(); });
    target.querySelector("[data-portal-radius]")?.addEventListener("change", (e) => { portal.Radius = Math.max(1, +e.target.value); change(); });
    target.querySelector("[data-portal-zone]").addEventListener("change", (e) => { portal.ToZoneId = +e.target.value; change(); });
    for (const input of target.querySelectorAll("[data-portal-to]")) input.addEventListener("change", () => { portal.ToPosition[+input.dataset.portalTo] = +input.value; change(); });
    target.querySelector("[data-portal-heading]").addEventListener("change", (e) => { portal.ToHeading = round(+e.target.value * Math.PI / 180, 4); change(); });
    target.querySelector("[data-portal-delete]").addEventListener("click", () => {
      this.portals.splice(index, 1);
      this.touch("portals");
      this.ctx.select(null);
      this.ctx.redraw();
      this.ctx.renderPanel();
    });
  }

  // Barriers --------------------------------------------------------------------------------------------------

  barriersPanel() {
    return `<p class="hint">Invisible walls in the zone file (drawn red): the client stops players between each wall's posts.
        Pick one to move its posts; they reach the client with the zone file (re-enter the zone in game).</p>
      <div class="row"><button id="add-barrier" title="Two posts across the middle of the view">Add a barrier</button></div>
      <div class="list">${this.walls.map((wall, i) => `<div data-wall="${i}" class="${i === this.selectedWall ? "active" : ""}">Barrier ${i + 1}
        <small>${wall.length / 2} posts · ${Math.round(this.wallLength(wall))} m</small></div>`).join("") || '<p class="hint">None.</p>'}</div>
      ${this.selectedWall !== null ? `<div class="row"><button id="barrier-add-post">Add a post at the view</button><button id="barrier-delete" class="danger">Delete barrier</button></div>
        <label>Height (m) <input id="barrier-height" type="number" min="1" step="5" value="${round(this.wallHeight(this.walls[this.selectedWall]), 1)}"></label>` : ""}`;
  }

  wallLength(wall) {
    let length = 0;
    for (let i = 2; i < wall.length; i += 2) length += Math.hypot(wall[i][0] - wall[i - 2][0], wall[i][2] - wall[i - 2][2]);
    return length;
  }

  wallHeight(wall) {
    return wall?.length >= 2 ? wall[1][1] - wall[0][1] : WALL_HEIGHT;
  }

  barriersBind(panel) {
    for (const row of panel.querySelectorAll("[data-wall]")) {
      row.addEventListener("click", () => {
        this.selectedWall = +row.dataset.wall;
        this.ctx.redraw();
        this.ctx.renderPanel();
        const first = this.ctx.markers().find((m) => m.userData.world?.type === "post");
        if (first) { this.ctx.select(first); this.ctx.focus(first); }
      });
    }
    panel.querySelector("#add-barrier").addEventListener("click", () => {
      const at = this.ctx.centre();
      if (!at) return this.ctx.toast("Point the view at the ground where it goes.");
      const heading = this.ctx.forward();
      // Across the view: posts 10 m either side of the middle.
      const dx = Math.cos(heading) * 10, dz = -Math.sin(heading) * 10;
      const post = (x, z) => { const y = this.ctx.ground(x, z) ?? at.y; return [[round(x), round(y), round(z)], [round(x), round(y + WALL_HEIGHT), round(z)]]; };
      this.walls.push([...post(at.x - dx, at.z - dz), ...post(at.x + dx, at.z + dz)]);
      this.selectedWall = this.walls.length - 1;
      this.touch("walls");
      this.ctx.redraw();
      this.ctx.renderPanel();
    });
    panel.querySelector("#barrier-add-post")?.addEventListener("click", () => {
      const at = this.ctx.centre();
      if (!at) return this.ctx.toast("Point the view at the ground where the post goes.");
      const wall = this.walls[this.selectedWall];
      const height = this.wallHeight(wall);
      wall.push([round(at.x), round(at.y), round(at.z)], [round(at.x), round(at.y + height), round(at.z)]);
      this.touch("walls");
      this.ctx.redraw();
      this.ctx.renderPanel();
    });
    panel.querySelector("#barrier-delete")?.addEventListener("click", () => {
      this.walls.splice(this.selectedWall, 1);
      this.selectedWall = null;
      this.touch("walls");
      this.ctx.select(null);
      this.ctx.redraw();
      this.ctx.renderPanel();
    });
    panel.querySelector("#barrier-height")?.addEventListener("change", (e) => {
      const wall = this.walls[this.selectedWall];
      const height = Math.max(1, +e.target.value);
      for (let i = 0; i + 1 < wall.length; i += 2) wall[i + 1] = [wall[i][0], round(wall[i][1] + height), wall[i][2]];
      this.touch("walls");
      this.ctx.redraw();
    });
  }

  inspectPost(target, { wall, post }) {
    const points = this.walls[wall];
    target.innerHTML = `<h4>Barrier ${wall + 1}, post ${post + 1} of ${points.length / 2}</h4>
      <div class="row"><button data-post-delete class="danger" ${points.length <= 4 ? "disabled title=\"A barrier needs two posts\"" : ""}>Delete post</button></div>`;
    target.querySelector("[data-post-delete]").addEventListener("click", () => {
      points.splice(post * 2, 2);
      this.touch("walls");
      this.ctx.select(null);
      this.ctx.redraw();
      this.ctx.renderPanel();
    });
  }

  // Seasons ---------------------------------------------------------------------------------------------------

  seasonsPanel() {
    if (!this.seasons) {
      getJson("api/seasons").then((seasons) => { this.seasons = seasons; this.ctx.renderPanel(); }).catch((error) => this.ctx.toast(error.message, "error"));
      return '<p class="hint">Loading…</p>';
    }
    const day = (month, date) => `<select data-month>${MONTHS.map((m, i) => `<option value="${i + 1}" ${i + 1 === month ? "selected" : ""}>${m}</option>`).join("")}</select>
      <input data-day type="number" min="1" max="31" value="${date}">`;
    return `<p class="hint">Seasonal holidays: each is an event key that's on between two days of the year, every year (and whenever !event turns it on).
        Flag an NPC for one (its Event), or give anything placed the condition, and it's there only then.</p>
      ${this.seasons.map((s, i) => `<div class="season" data-season="${i}">
        <div class="two"><label>Name <input data-field="Name" value="${escape(s.Name)}"></label><label>Key <input data-field="Key" value="${escape(s.Key)}"></label></div>
        <div class="two"><label class="dates">From ${day(s.StartMonth, s.StartDay).replace("data-month", 'data-month="Start"').replace("data-day", 'data-day="Start"')}</label>
          <label class="dates">To ${day(s.EndMonth, s.EndDay).replace("data-month", 'data-month="End"').replace("data-day", 'data-day="End"')}</label></div>
        <div class="row"><button data-season-delete="${i}" class="danger">Remove</button></div></div>`).join("")}
      <div class="row"><button id="season-add">Add a season</button><button id="season-save" class="primary">Save seasons</button></div>`;
  }

  seasonsBind(panel) {
    if (!this.seasons) return;
    for (const box of panel.querySelectorAll("[data-season]")) {
      const season = this.seasons[+box.dataset.season];
      for (const input of box.querySelectorAll("[data-field]")) input.addEventListener("input", () => { season[input.dataset.field] = input.value; });
      for (const select of box.querySelectorAll("[data-month]")) select.addEventListener("change", () => { season[`${select.dataset.month}Month`] = +select.value; });
      for (const input of box.querySelectorAll("[data-day]")) input.addEventListener("change", () => { season[`${input.dataset.day}Day`] = +input.value; });
    }
    for (const button of panel.querySelectorAll("[data-season-delete]")) {
      button.addEventListener("click", () => { this.seasons.splice(+button.dataset.seasonDelete, 1); this.ctx.renderPanel(); });
    }
    panel.querySelector("#season-add").addEventListener("click", () => {
      this.seasons.push({ Key: "", Name: "", StartMonth: 1, StartDay: 1, EndMonth: 1, EndDay: 7 });
      this.ctx.renderPanel();
    });
    panel.querySelector("#season-save").addEventListener("click", async () => {
      try {
        const { seasons } = await postJson("api/seasons/save", this.seasons);
        this.seasons = seasons;
        let note = "The Gateway reads Seasons.json when it starts.";
        if (this.ctx.live.zone) {
          try {
            await this.ctx.live.reloadResource("seasons");
            note = "The running game has them now; NPCs waiting for one come and go within a minute.";
          } catch (error) {
            note = `The running game didn't reload them: ${error.message}`;
          }
        }
        this.ctx.toast(`Saved ${seasons.length} season(s). ${note}`, "ok");
        this.ctx.renderPanel();
      } catch (error) {
        this.ctx.toast(error.message, "error");
      }
    });
  }

  // --- the inspector for a world marker -----------------------------------------------------------------------

  renderInspector(target, m) {
    const world = m.userData.world;
    if (world.type === "poi" || world.type === "arrival") return this.inspectPoi(target, world.index);
    if (world.type === "area" || world.type === "area2") return this.inspectArea(target, world.index);
    if (world.type === "portal" || world.type === "destination") return this.inspectPortal(target, world.index);
    if (world.type === "post") return this.inspectPost(target, world);
  }
}

// --- environments (the Env tool) -----------------------------------------------------------------------------

// The Env tool's section for making a new tile environment or changing one: its sky, minimap tile set and atlas
// page, in the ObjectTerrainData.xml client patch. hooks: { toast, saved(environments) }
export async function renderEnvironmentEditor(container, environment, hooks) {
  let options;
  try {
    options = await getJson("api/environments");
  } catch (error) {
    container.innerHTML = `<p class="hint">${escape(error.message)}</p>`;
    return;
  }
  const e = environment ?? { id: null, name: "", sky: "", minimap: "", atlas: options.atlases[0] ?? "sacredgrove" };
  container.innerHTML = `<h4>${e.id === null ? "New environment" : `Environment ${e.id}`}</h4>
    ${e.stock ? '<p class="warn">One of the game\'s own: a change shows on every tile that has it, in every zone.</p>' : ""}
    <label>Name <input data-env-field="name" value="${escape(e.name)}" placeholder="MushroomHollow"></label>
    <label>Sky <select data-env-field="sky"><option value="">(none)</option>${options.skies.map((s) => `<option ${s.toLowerCase() === (e.sky || "").toLowerCase() ? "selected" : ""}>${escape(s)}</option>`).join("")}</select></label>
    <label>Minimap tiles <select data-env-field="minimap"><option value="">(none)</option>${options.minimaps.map((m) => `<option ${m.toLowerCase() === (e.minimap || "").toLowerCase() ? "selected" : ""}>${escape(m)}</option>`).join("")}</select></label>
    <label>Atlas page <select data-env-field="atlas">${options.atlases.map((a) => `<option ${a === e.atlas ? "selected" : ""}>${escape(a)}</option>`).join("")}</select></label>
    <div class="row"><button data-env-save class="primary">${e.id === null ? "Add it" : "Save it"}</button></div>
    <p class="hint">It goes to clients in a client patch, which they read when they start (Test in game restarts yours).</p>`;
  container.querySelector("[data-env-save]").addEventListener("click", async () => {
    const value = (field) => container.querySelector(`[data-env-field="${field}"]`).value.trim();
    try {
      const result = await postJson("api/environments/save", { id: e.id, name: value("name"), sky: value("sky"), minimap: value("minimap"), atlas: value("atlas") });
      hooks.toast(`${e.id === null ? "Added" : "Saved"} environment ${result.id}; paint it on tiles with the brush.`, "ok");
      hooks.saved(result.environments, result.id);
    } catch (error) {
      hooks.toast(error.message, "error");
    }
  });
}
