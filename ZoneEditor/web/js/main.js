// Sanctuary zone editor: the viewport, tools and panels.
import * as THREE from "three";
import { TransformControls } from "three/addons/controls/TransformControls.js";
import { getJson, postJson, save } from "./api.js";
import { CHUNK, TerrainManager, environmentColour } from "./terrain.js";
import { PropLayer, PropLibrary, applyTransform, makeMarker, readTransform } from "./props.js";

const $ = (selector) => document.querySelector(selector);
const canvas = $("#canvas");
const panel = $("#panel");

// --- scene ---------------------------------------------------------------------------------------------

const renderer = new THREE.WebGLRenderer({ canvas, antialias: true });
renderer.outputColorSpace = THREE.LinearSRGBColorSpace;
renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
const scene = new THREE.Scene();
const camera = new THREE.PerspectiveCamera(60, 1, 0.5, 6000);
camera.rotation.order = "YXZ";
const sun = new THREE.DirectionalLight(0xffffff, 1.0);
const hemisphere = new THREE.HemisphereLight(0xffffff, 0x666655, 0.9);
scene.add(sun, sun.target, hemisphere);

const terrain = new TerrainManager(scene, {
  onChunkLoaded: (chunk) => { props.addChunk(chunk); drawMinimap(); },
  onChunkUnloaded: (chunk) => { props.removeChunk(chunk); if (selected?.userData.chunk === chunk) select(null); },
  status,
});
const library = new PropLibrary();
const props = new PropLayer(scene, library);
scene.background = terrain.uniforms.fogColor.value;

// The client's world is left-handed: mirror x in the projection, so the scene graph stays in its coordinates.
// Mirroring flips every triangle's winding on screen, so front faces are clockwise from here on.
{
  const gl = renderer.getContext();
  const frontFace = gl.frontFace.bind(gl);
  gl.frontFace = (mode) => frontFace(mode === gl.CW ? gl.CCW : gl.CW);
  renderer.state.setFlipSided(true);
  renderer.state.setFlipSided(false);
}

function updateProjection() {
  const { clientWidth: w, clientHeight: h } = canvas;
  renderer.setSize(w, h, false);
  camera.aspect = w / Math.max(h, 1);
  camera.updateProjectionMatrix();
  camera.projectionMatrix.elements[0] *= -1;
  camera.projectionMatrixInverse.copy(camera.projectionMatrix).invert();
}
new ResizeObserver(updateProjection).observe(canvas);

const gizmo = new TransformControls(camera, canvas);
gizmo.setSize(0.8);
scene.add(gizmo);

// --- state ---------------------------------------------------------------------------------------------

const state = {
  zones: [],
  zone: null,
  tool: "select",
  brush: { radius: 16, strength: 0.5, mode: "raise" },
  eco: null,
  ecoTint: true,
  ecoTintAmount: 0.85,
  ecoColours: new Map(),
  colour: [96, 140, 70],
  environment: 0,
  actor: null,
  newChunk: { height: 0, eco: null, environment: 0 },
  settings: {},
  houses: null,
  sky: undefined,
  customSky: null,
  zoneDirty: false,
};
let selected = null;
let ghost = null;
const markers = [];
const undoStack = [];

const pointer = { x: 0, y: 0, ndc: new THREE.Vector2(), inside: false, buttons: 0 };
const look = { yaw: 0, pitch: -0.35, speed: 60 };
const keys = new Set();
const raycaster = new THREE.Raycaster();
let hover = null;          // terrain hit under the pointer
let stroke = null;         // brush stroke in progress

function status(text) {
  $("#status-text").textContent = text;
}

let toastTimer = null;
function toast(text, kind = "") {
  const element = $("#toast");
  element.textContent = text;
  element.className = kind;
  element.style.display = "block";
  clearTimeout(toastTimer);
  toastTimer = setTimeout(() => { element.style.display = "none"; }, kind === "error" ? 9000 : 4500);
}

// --- zones ---------------------------------------------------------------------------------------------

async function loadZones() {
  const data = await getJson("/api/zones");
  state.zones = data.zones;
  $("#zone-list").innerHTML = data.zones.map((z) =>
    `<option value="${z.name}">${z.stock ? "stock" : "custom"}${z.type ? " · " + z.type + " " + z.id : ""}</option>`).join("");
  status(`Saving into ${data.mods} · client ${data.client}`);
}

async function openZone(name, keepCamera = false) {
  if (!name) return;
  if (!keepCamera && dirtyChunks().length && !confirm("Discard unsaved changes?")) return;
  status(`Opening ${name}…`);
  let zone;
  try {
    zone = await getJson(`/api/zone?name=${encodeURIComponent(name)}`);
  } catch (error) {
    toast(error.message, "error");
    return;
  }
  select(null);
  state.zone = zone;
  state.houses = zone.houses ? structuredClone(zone.houses) : [];
  state.sky = undefined;
  state.customSky = null;
  state.settings = {};
  state.zoneDirty = false;
  state.eco = zone.ecos[0]?.index ?? null;
  state.newChunk.eco = state.eco;
  terrain.setZone(zone);
  terrain.setSelectedEco(state.eco);
  undoStack.length = 0;
  try { localStorage.setItem("zone-editor-zone", zone.name); } catch { /* private mode */ }
  $("#zone-search").value = zone.name;
  const editable = zone.chunks.filter((c) => c.editable).length;
  $("#zone-title").textContent = `${zone.name} · ${zone.stock ? "stock zone: " + editable + " of " + zone.chunks.length + " chunks editable" : "custom zone"}${zone.definition ? " · " + zone.definition.Type + " " + zone.definition.Id : " · no Sanctuary definition"}`;
  if (!keepCamera) {
    const spawn = zone.definition?.SpawnPosition;
    if (spawn) camera.position.set(spawn[0], spawn[1] + 40, spawn[2] - 60);
    else if (zone.chunks.length) {
      const c = zone.chunks[Math.floor(zone.chunks.length / 2)];
      camera.position.set(c.col * 64 + 128, 120, c.row * 64 + 128);
    } else camera.position.set((zone.bounds.startX + zone.bounds.endX) * 32, 120, (zone.bounds.startZ + zone.bounds.endZ) * 32);
    look.yaw = 0;
    look.pitch = -0.4;
  }
  buildMarkers();
  if (zone.stock && !keepCamera) {
    const stockCount = zone.chunks.length - editable;
    toast(`${zone.name} is a stock zone: its ${stockCount} stock chunks are read-only (the client won't take replacements).\n` +
      (editable ? `${editable} chunk(s) you added are editable. ` : "") + "Add new chunks in empty cells with the Chunks tool (K) and build there.");
  }
  updateLightingFromSky(zone.definition?.Sky);
  setTool(state.tool);
}

function dirtyChunks() {
  return [...terrain.chunks.values()].filter((c) => c.anyDirty);
}

// --- markers (spawn, houses) ---------------------------------------------------------------------------

function buildMarkers() {
  for (const marker of markers) scene.remove(marker);
  markers.length = 0;
  const zone = state.zone;
  if (zone.definition?.SpawnPosition) {
    const marker = makeMarker(0x3fd0ff, "Zone spawn");
    const [x, y, z] = zone.definition.SpawnPosition;
    marker.position.set(x, y, z);
    const rotation = zone.definition.SpawnRotation || [1, 0];
    const [dx, dz] = rotation.length === 2 ? rotation : [rotation[0], rotation[2]];
    marker.rotation.y = Math.atan2(-dz, dx);
    marker.userData.apply = () => {
      state.settings.spawnPosition = [marker.position.x, marker.position.y, marker.position.z];
      state.settings.spawnHeading = THREE.MathUtils.radToDeg(-marker.rotation.y);
      state.zoneDirty = true;
    };
    markers.push(marker);
  }
  for (const house of state.houses) {
    for (const [field, colour] of [["SpawnPosition", 0x6be27a], ["ExitPosition", 0xff8a5c]]) {
      const marker = makeMarker(colour, `House ${house.Id} ${field === "SpawnPosition" ? "spawn" : "exit"}`);
      marker.position.set(house[field][0], house[field][1], house[field][2]);
      marker.userData.apply = () => {
        house[field] = [marker.position.x, marker.position.y, marker.position.z, 0].map((v) => Math.round(v * 100) / 100);
        state.zoneDirty = true;
      };
      markers.push(marker);
    }
    for (const area of house.BuildAreas || []) {
      const box = new THREE.Box3(new THREE.Vector3(...area.Min), new THREE.Vector3(...area.Max));
      const helper = new THREE.Box3Helper(box, 0xe8b84a);
      helper.userData = { kind: "area" };
      markers.push(helper);
    }
  }
  for (const marker of markers) scene.add(marker);
}

// --- lighting preview ----------------------------------------------------------------------------------

async function updateLightingFromSky(sky, xml) {
  const u = terrain.uniforms;
  try {
    if (!xml && sky) xml = await (await fetch(`/api/sky?name=${encodeURIComponent(sky)}`)).text();
  } catch { xml = null; }
  const noon = (section) => {
    const block = xml?.match(new RegExp(`<${section}>([\\s\\S]*?)</${section}>`))?.[1] || "";
    const colours = [...block.matchAll(/<Color[^>]*Time="(\d+):\d+"[^>]*R="([\d.]+)"[^>]*G="([\d.]+)"[^>]*B="([\d.]+)"[^>]*Brightness="([\d.]+)"/g)];
    const pick = colours.find((m) => +m[1] === 12) || colours[0];
    return pick ? [pick[2] / 255, pick[3] / 255, pick[4] / 255, +pick[5]] : null;
  };
  const sunColour = noon("SunLight");
  const ambientColour = noon("Ambient");
  if (sunColour) u.sunColor.value.setRGB(...sunColour.slice(0, 3)).multiplyScalar(Math.min(sunColour[3], 2) * 0.9);
  else u.sunColor.value.setRGB(0.85, 0.85, 0.8);
  if (ambientColour) u.ambient.value.setRGB(...ambientColour.slice(0, 3)).multiplyScalar(Math.min(ambientColour[3], 2.5) * 0.45);
  else u.ambient.value.setRGB(0.55, 0.57, 0.62);
  if (u.ambient.value.r + u.ambient.value.g + u.ambient.value.b < 0.9) u.ambient.value.addScalar(0.25);
  const dir = xml?.match(/<Direction Time="12:00" Heading="([\d.-]+)" Pitch="([\d.-]+)"/);
  if (dir) {
    const heading = THREE.MathUtils.degToRad(+dir[1]), pitch = THREE.MathUtils.degToRad(+dir[2]);
    u.sunDir.value.set(Math.sin(heading) * Math.cos(pitch), Math.sin(pitch), Math.cos(heading) * Math.cos(pitch)).normalize();
  }
  sun.color.copy(u.sunColor.value);
  sun.position.copy(u.sunDir.value).multiplyScalar(100);
  hemisphere.color.copy(u.ambient.value).multiplyScalar(1.4);
}

// --- tools and panels ----------------------------------------------------------------------------------

const BRUSH_TOOLS = new Set(["sculpt", "eco", "colour", "environment"]);

function setTool(tool) {
  state.tool = tool;
  for (const button of document.querySelectorAll("#toolbar button")) button.classList.toggle("active", button.dataset.tool === tool);
  terrain.uniforms.ecoOverlay.value = tool === "eco" ? 1 : 0;
  terrain.uniforms.envOverlay.value = tool === "environment" ? 1 : 0;
  terrain.uniforms.brush.value.z = 0;
  if (ghost) { scene.remove(ghost); ghost = null; }
  if (tool === "props" && state.actor) makeGhost();
  renderPanel();
}

function html(strings, ...values) {
  return strings.reduce((out, s, i) => out + s + (i < values.length ? values[i] : ""), "");
}

function escape(text) {
  return String(text).replace(/[&<>"]/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;" })[c]);
}

function brushControls(modes) {
  return html`
    ${modes ? `<div class="seg">${modes.map((m) => `<button data-mode="${m}" class="${state.brush.mode === m ? "active" : ""}">${m}</button>`).join("")}</div>` : ""}
    <label>Radius <span id="radius-value">${state.brush.radius}</span><input id="brush-radius" type="range" min="2" max="128" value="${state.brush.radius}"></label>
    <label>Strength <span id="strength-value">${state.brush.strength.toFixed(2)}</span><input id="brush-strength" type="range" min="0.02" max="1" step="0.01" value="${state.brush.strength}"></label>`;
}

function bindBrush() {
  panel.querySelector("#brush-radius")?.addEventListener("input", (e) => { state.brush.radius = +e.target.value; $("#radius-value").textContent = state.brush.radius; });
  panel.querySelector("#brush-strength")?.addEventListener("input", (e) => { state.brush.strength = +e.target.value; $("#strength-value").textContent = state.brush.strength.toFixed(2); });
  for (const button of panel.querySelectorAll("[data-mode]")) {
    button.addEventListener("click", () => { state.brush.mode = button.dataset.mode; renderPanel(); });
  }
}

function ecoSwatch(index) {
  const colour = state.ecoColours.get(index);
  return colour ? `<span class="swatch" style="background:rgb(${colour.join(",")})"></span>` : `<span class="swatch"></span>`;
}

async function ecoColour(index) {
  if (!state.ecoColours.has(index)) {
    state.ecoColours.set(index, await getJson(`/api/eco-colour?zone=${encodeURIComponent(state.zone.name)}&index=${index}`));
  }
  return state.ecoColours.get(index);
}

function noZonePanel() {
  panel.innerHTML = `<h3>No zone open</h3><p class="hint">Type a zone name at the top (FabledRealms, a housing lot such as hsg_emptylot_castle_01, or one you made) and press Open, or make a new zone.</p>`;
}

function renderPanel() {
  const zone = state.zone;
  if (!zone) return noZonePanel();
  const editableCount = [...terrain.chunks.values()].filter((c) => c.editable).length;
  const noEditable = !editableCount && BRUSH_TOOLS.has(state.tool)
    ? `<p class="warn">No editable chunk is loaded here. ${zone.stock ? "Stock chunks are read-only; add a chunk with the Chunks tool (K)." : ""}</p>` : "";

  if (state.tool === "select") {
    panel.innerHTML = `<h3>Select</h3><p class="hint">Click a prop, light or marker. G move, R rotate, T scale, Del delete, Ctrl+D duplicate, F focus.</p><div id="inspector"></div>`;
    renderInspector();
  } else if (state.tool === "sculpt") {
    panel.innerHTML = `<h3>Sculpt</h3>${noEditable}${brushControls(["raise", "lower", "smooth", "flatten", "level", "noise"])}
      <label id="level-row" style="${state.brush.mode === "level" ? "" : "display:none"}">Level height <input id="level-height" type="number" step="0.5" value="${state.brush.level ?? 0}"></label>
      <p class="hint">Hold the left button to sculpt; Shift swaps raise/lower. Flatten levels to the height where the stroke started. Edges shared with stock chunks stay pinned so the seams don't open.</p>`;
    bindBrush();
    panel.querySelector("#level-height").addEventListener("input", (e) => { state.brush.level = +e.target.value; });
  } else if (state.tool === "eco") {
    panel.innerHTML = `<h3>Eco layers</h3>${noEditable}${brushControls()}
      <label class="check"><input id="eco-tint" type="checkbox" ${state.ecoTint ? "checked" : ""}> Also tint the colour map</label>
      <label>Tint amount <input id="eco-tint-amount" type="range" min="0" max="1" step="0.05" value="${state.ecoTintAmount}"></label>
      <h4>${zone.name}'s ecos</h4>
      <div class="list" id="eco-list">${zone.ecos.map((e) => `<div data-eco="${e.index}" class="${e.index === state.eco ? "active" : ""}">${ecoSwatch(e.index)}${escape(e.name)} <small>${escape(e.texture)}${e.floras.length ? " · " + e.floras.length + " flora" : ""}</small></div>`).join("")}</div>
      <p class="hint">The highlighted area shows where the selected eco is. A tile holds up to 5 ecos; past that the lightest are dropped on save. ${zone.stock ? "A stock zone's eco list is fixed." : "Add more ecos in the Zone tool."}</p>`;
    bindBrush();
    panel.querySelector("#eco-tint").addEventListener("change", (e) => { state.ecoTint = e.target.checked; });
    panel.querySelector("#eco-tint-amount").addEventListener("input", (e) => { state.ecoTintAmount = +e.target.value; });
    for (const row of panel.querySelectorAll("[data-eco]")) {
      row.addEventListener("click", () => { state.eco = +row.dataset.eco; terrain.setSelectedEco(state.eco); renderPanel(); });
    }
    Promise.all(zone.ecos.map((e) => ecoColour(e.index))).then(() => {
      if (state.tool === "eco") for (const row of panel.querySelectorAll("[data-eco]")) row.querySelector(".swatch").outerHTML = ecoSwatch(+row.dataset.eco);
    }).catch(() => {});
  } else if (state.tool === "colour") {
    const hex = "#" + state.colour.map((v) => v.toString(16).padStart(2, "0")).join("");
    panel.innerHTML = `<h3>Colour map</h3>${noEditable}${brushControls()}
      <label>Colour <input id="paint-colour" type="color" value="${hex}"></label>
      <p class="hint">Alt+click picks a colour from the ground. The colour map is the ground's base tint; eco detail textures draw over it in game.</p>`;
    bindBrush();
    panel.querySelector("#paint-colour").addEventListener("input", (e) => {
      const v = e.target.value;
      state.colour = [1, 3, 5].map((i) => parseInt(v.slice(i, i + 2), 16));
    });
  } else if (state.tool === "environment") {
    panel.innerHTML = `<h3>Tile environments</h3>${noEditable}${brushControls()}
      <p class="hint">A tile's environment (ObjectTerrainData.xml) picks its sky, minimap and atlas in game. 0 is none: the zone's own sky.</p>
      <div class="list">${[{ id: 0, name: "None", sky: "zone sky" }, ...zone.environments].map((e) => `<div data-env="${e.id}" class="${e.id === state.environment ? "active" : ""}"><span class="swatch" style="background:rgb(${environmentColour(e.id).join(",")})"></span>${e.id} ${escape(e.name)} <small>${escape(e.sky)}</small></div>`).join("")}</div>`;
    bindBrush();
    for (const row of panel.querySelectorAll("[data-env]")) {
      row.addEventListener("click", () => { state.environment = +row.dataset.env; renderPanel(); });
    }
  } else if (state.tool === "props") {
    panel.innerHTML = `<h3>Props</h3>
      <input id="actor-search" placeholder="Search actors (tree, fence, housing_…)" value="${escape(state.actorQuery || "")}">
      <p class="hint" id="actor-count"></p>
      <div class="list" id="actor-list"></div>
      <p class="hint">Pick an actor, then click the ground to place it (it lands in the editable chunk under it, else the nearest one loaded). Esc stops placing.</p>
      <div id="inspector"></div>`;
    const search = panel.querySelector("#actor-search");
    const refresh = async () => {
      state.actorQuery = search.value;
      const data = await getJson(`/api/actors?q=${encodeURIComponent(search.value)}&limit=400`);
      panel.querySelector("#actor-count").textContent = `${data.total} actors${data.total > data.names.length ? `, showing ${data.names.length}` : ""}`;
      panel.querySelector("#actor-list").innerHTML = data.names.map((n) => `<div data-actor="${escape(n)}" class="${n === state.actor ? "active" : ""}">${escape(n)}</div>`).join("");
      for (const row of panel.querySelectorAll("[data-actor]")) {
        row.addEventListener("click", () => {
          state.actor = row.dataset.actor;
          for (const other of panel.querySelectorAll("[data-actor]")) other.classList.toggle("active", other === row);
          makeGhost();
        });
      }
    };
    let timer;
    search.addEventListener("input", () => { clearTimeout(timer); timer = setTimeout(refresh, 200); });
    refresh();
    renderInspector();
  } else if (state.tool === "lights") {
    panel.innerHTML = `<h3>Point lights</h3><p class="hint">Click the ground to add a light 6 units above it, or click a light to edit it.</p><div id="inspector"></div>`;
    renderInspector();
  } else if (state.tool === "chunks") {
    const options = zone.ecos.map((e) => `<option value="${e.index}" ${e.index === state.newChunk.eco ? "selected" : ""}>${escape(e.name)}</option>`).join("");
    const envs = [{ id: 0, name: "None" }, ...zone.environments].map((e) => `<option value="${e.id}" ${e.id === state.newChunk.environment ? "selected" : ""}>${e.id} ${escape(e.name)}</option>`).join("");
    const b = zone.bounds;
    panel.innerHTML = `<h3>Chunks</h3>
      <p class="hint">Click an empty cell (in the view or on the minimap) to add a 256 x 256 chunk. Its edges join the chunks around it and the inside is smoothed between them; sculpt from there.</p>
      <p class="hint">${zone.stock ? `${zone.name} is a stock zone: new chunks have to fit its bounds (x ${b.startX * 64}…${b.endX * 64}, z ${b.startZ * 64}…${b.endZ * 64}).` : "The zone grows to fit new chunks."}</p>
      <label>Ground height where there's no neighbour <input id="chunk-height" type="number" step="1" value="${state.newChunk.height}"></label>
      <label>Eco <select id="chunk-eco">${options}</select></label>
      <label>Environment <select id="chunk-env">${envs}</select></label>`;
    panel.querySelector("#chunk-height").addEventListener("input", (e) => { state.newChunk.height = +e.target.value; });
    panel.querySelector("#chunk-eco").addEventListener("change", (e) => { state.newChunk.eco = +e.target.value; });
    panel.querySelector("#chunk-env").addEventListener("change", (e) => { state.newChunk.environment = +e.target.value; });
  } else if (state.tool === "zone") {
    renderZonePanel();
  }
}

function renderZonePanel() {
  const zone = state.zone;
  const definition = zone.definition;
  const skies = zone.skies;
  const sky = state.sky !== undefined ? state.sky : definition?.Sky || "";
  const fabled = state.zones.find((z) => z.name.toLowerCase() === "fabledrealms");
  panel.innerHTML = html`<h3>Zone</h3>
    <p class="hint">${escape(zone.name)} · ${zone.stock ? "stock" : "custom"} · ${zone.chunks.length} chunks · bounds x ${zone.bounds.startX * 64}…${zone.bounds.endX * 64}, z ${zone.bounds.startZ * 64}…${zone.bounds.endZ * 64}</p>
    ${definition ? `<p class="hint">Sanctuary ${definition.Type} zone ${definition.Id}. Travel there with <code>!moveplayer zone ${definition.Id} &lt;you&gt;</code> after restarting the servers.</p>` : `<p class="warn">No Sanctuary zone definition (Zones/${escape(zone.name)}.json), so the server can't send players here.</p>`}
    <h4>Spawn</h4>
    ${definition ? `<div class="row"><button id="spawn-here">Spawn at camera target</button></div><p class="hint">Or select the blue marker and move it (G) or turn it (R).</p>` : ""}
    <h4>Sky</h4>
    <label>Zone sky (sent to the client on zoning in)<select id="sky">${["", ...skies].map((s) => `<option value="${escape(s)}" ${s === sky ? "selected" : ""}>${s || "(client default)"}</option>`).join("")}</select></label>
    <div class="row"><button id="custom-sky">Custom sky from this…</button></div>
    <div id="custom-sky-editor" style="${state.customSky ? "" : "display:none"}">
      <label>File <input id="custom-sky-name" value="${escape(state.customSky?.name || `sky_${zone.name.toLowerCase()}.xml`)}"></label>
      <label>Sky XML (sun, ambient, fog and dome by time of day)<textarea id="custom-sky-xml">${escape(state.customSky?.xml || "")}</textarea></label>
      <p class="hint">Saved to ClientMods/sky and copied into your client's Resources/SKY (skies aren't streamed, so other clients need it installed too).</p>
    </div>
    ${zone.stock ? "" : `<h4>Ecos</h4>
      <label>Add from FabledRealms<select id="add-eco-fabled"><option value="">…</option></select></label>
      ${zone.ecoDefinitions.length ? `<label>Add from a client .eco<select id="add-eco-def"><option value="">…</option>${zone.ecoDefinitions.map((d) => `<option>${escape(d)}</option>`).join("")}</select></label>` : ""}`}
    ${state.houses.length ? `<h4>Houses</h4>${state.houses.map((h, i) => `
      <p class="hint">House ${h.Id} (name ${h.NameId}, items ${escape(h.ItemDefinitionIds.join(", ") || "none")}): green marker spawn, orange exit. Exit door object ${h.ExitObjectId || "none (one is spawned)"}.</p>
      <div class="row"><button data-exit-door="${i}">Use selected prop as exit door</button></div>
      ${(h.BuildAreas || []).map((a, j) => `<label>Build area ${j + 1} min / max</label>
        <div class="xyz">${a.Min.map((v, k) => `<input data-area="${i},${j},Min,${k}" type="number" step="0.5" value="${v}">`).join("")}</div>
        <div class="xyz">${a.Max.map((v, k) => `<input data-area="${i},${j},Max,${k}" type="number" step="0.5" value="${v}">`).join("")}</div>`).join("")}`).join("")}` : ""}
    <h4>Walls</h4><p class="hint">${zone.walls.length} invisible walls (shown in red).</p>`;

  panel.querySelector("#spawn-here")?.addEventListener("click", () => {
    const target = centreHit();
    const marker = markers.find((m) => m.userData.label === "Zone spawn");
    if (!target || !marker) return toast("Point the camera at the ground first.");
    marker.position.set(target.x, target.y + 0.5, target.z);
    marker.userData.apply();
    toast("Spawn moved; save to write it.");
  });
  panel.querySelector("#sky").addEventListener("change", (e) => {
    state.sky = e.target.value;
    state.zoneDirty = true;
    updateLightingFromSky(state.sky);
  });
  panel.querySelector("#custom-sky").addEventListener("click", async () => {
    const base = panel.querySelector("#sky").value || "sky.xml";
    const xml = await (await fetch(`/api/sky?name=${encodeURIComponent(base)}`)).text();
    state.customSky = { name: `sky_${zone.name.toLowerCase()}.xml`, xml };
    state.zoneDirty = true;
    renderPanel();
    updateLightingFromSky(null, xml);
  });
  panel.querySelector("#custom-sky-name")?.addEventListener("input", (e) => { state.customSky.name = e.target.value; });
  panel.querySelector("#custom-sky-xml")?.addEventListener("input", (e) => {
    state.customSky.xml = e.target.value;
    updateLightingFromSky(null, e.target.value);
  });
  const fabledSelect = panel.querySelector("#add-eco-fabled");
  if (fabledSelect && fabled) {
    getJson("/api/zone?name=FabledRealms").then((fr) => {
      fabledSelect.innerHTML += fr.ecos.map((e) => `<option value="${e.index}">${escape(e.name)} (${escape(e.texture)})</option>`).join("");
    });
    fabledSelect.addEventListener("change", () => addEco({ sourceZone: "FabledRealms", sourceIndex: +fabledSelect.value }));
  }
  panel.querySelector("#add-eco-def")?.addEventListener("change", (e) => addEco({ definition: e.target.value }));
  for (const button of panel.querySelectorAll("[data-exit-door]")) {
    button.addEventListener("click", () => {
      const record = selected?.userData.kind === "prop" ? selected.userData.record : null;
      if (!record?.id) return toast("Select a saved prop (a door or gate) first.");
      state.houses[+button.dataset.exitDoor].ExitObjectId = record.id;
      state.zoneDirty = true;
      renderPanel();
    });
  }
  for (const input of panel.querySelectorAll("[data-area]")) {
    input.addEventListener("change", () => {
      const [i, j, field, k] = input.dataset.area.split(",");
      state.houses[+i].BuildAreas[+j][field][+k] = +input.value;
      state.zoneDirty = true;
      buildMarkers();
    });
  }
}

async function addEco(body) {
  try {
    const eco = await postJson("/api/zone/eco", { zone: state.zone.name, ...body });
    state.zone.ecos.push(eco);
    state.zoneDirty = true;
    toast(`Added eco ${eco.name}; save to write the zone file.`, "ok");
    renderPanel();
  } catch (error) {
    toast(error.message, "error");
  }
}

function renderInspector() {
  const target = panel.querySelector("#inspector");
  if (!target) return;
  if (!selected) { target.innerHTML = ""; return; }
  const { kind, record, chunk, label } = selected.userData;
  const locked = chunk && !chunk.editable;
  if (kind === "marker") {
    target.innerHTML = `<h4>${escape(label)}</h4><div class="xyz">${["x", "y", "z"].map((a) => `<input data-pos="${a}" type="number" step="0.5" value="${selected.position[a].toFixed(2)}">`).join("")}</div>`;
  } else if (kind === "prop") {
    const deg = record.rotation.map((r) => THREE.MathUtils.radToDeg(r).toFixed(1));
    target.innerHTML = html`<h4>${escape(record.file)}</h4>
      <p class="hint">Chunk ${chunk.key}${record.id ? ` · object ${record.id}` : " · new"}${locked ? " · stock (read-only)" : ""}</p>
      <label>Position</label><div class="xyz">${record.position.map((v, k) => `<input data-pos="${"xyz"[k]}" type="number" step="0.25" value="${v.toFixed(2)}" ${locked ? "disabled" : ""}>`).join("")}</div>
      <label>Yaw / pitch / roll (degrees)</label><div class="xyz">${deg.map((v, k) => `<input data-rot="${k}" type="number" step="5" value="${v}" ${locked ? "disabled" : ""}>`).join("")}</div>
      <label>Scale <input id="prop-scale" type="number" step="0.05" min="0.01" value="${record.scale.toFixed(3)}" ${locked ? "disabled" : ""}></label>
      <div class="row">${locked ? `<button id="copy-editable">Copy into an editable chunk</button>` : `<button id="snap">Snap to ground</button><button id="duplicate">Duplicate</button><button id="delete" class="danger">Delete</button>`}</div>`;
  } else if (kind === "light") {
    const hex = "#" + record.color.slice(0, 3).map((v) => v.toString(16).padStart(2, "0")).join("");
    target.innerHTML = html`<h4>Light ${escape(record.name)}</h4>
      <p class="hint">Chunk ${chunk.key}${locked ? " · stock (read-only)" : ""}</p>
      <label>Position</label><div class="xyz">${record.position.map((v, k) => `<input data-pos="${"xyz"[k]}" type="number" step="0.25" value="${v.toFixed(2)}" ${locked ? "disabled" : ""}>`).join("")}</div>
      <label>Colour <input id="light-colour" type="color" value="${hex}" ${locked ? "disabled" : ""}></label>
      <label>Colour label <input id="light-colour-name" value="${escape(record.colorName)}" ${locked ? "disabled" : ""}></label>
      <label>Range <input id="light-range" type="number" step="1" min="1" value="${record.range}" ${locked ? "disabled" : ""}></label>
      <label>Intensity <input id="light-intensity" type="number" step="0.05" min="0" value="${record.intensity}" ${locked ? "disabled" : ""}></label>
      ${locked ? "" : `<div class="row"><button id="duplicate">Duplicate</button><button id="delete" class="danger">Delete</button></div>`}`;
  }
  const changed = () => {
    if (kind === "marker") { selected.userData.apply?.(); return; }
    applyTransform(selected, kind === "light" ? { position: record.position, rotation: [0, 0, 0], scale: 1 } : record);
    markDirty(chunk, kind);
  };
  for (const input of target.querySelectorAll("[data-pos]")) {
    input.addEventListener("change", () => {
      const k = "xyz".indexOf(input.dataset.pos);
      if (kind === "marker") selected.position.setComponent(k, +input.value);
      else record.position[k] = +input.value;
      changed();
    });
  }
  for (const input of target.querySelectorAll("[data-rot]")) {
    input.addEventListener("change", () => { record.rotation[+input.dataset.rot] = THREE.MathUtils.degToRad(+input.value); changed(); });
  }
  target.querySelector("#prop-scale")?.addEventListener("change", (e) => { record.scale = Math.max(0.01, +e.target.value); changed(); });
  target.querySelector("#light-colour")?.addEventListener("input", (e) => {
    const v = e.target.value;
    record.color = [...[1, 3, 5].map((i) => parseInt(v.slice(i, i + 2), 16)), 255];
    props.refreshLight(record);
    markDirty(chunk, kind);
  });
  target.querySelector("#light-colour-name")?.addEventListener("change", (e) => { record.colorName = e.target.value; markDirty(chunk, kind); });
  target.querySelector("#light-range")?.addEventListener("change", (e) => { record.range = +e.target.value; markDirty(chunk, kind); });
  target.querySelector("#light-intensity")?.addEventListener("change", (e) => { record.intensity = +e.target.value; markDirty(chunk, kind); });
  target.querySelector("#snap")?.addEventListener("click", () => {
    const h = terrain.heightAt(record.position[0], record.position[2]);
    if (h !== null) { record.position[1] = h; changed(); renderInspector(); }
  });
  target.querySelector("#duplicate")?.addEventListener("click", duplicateSelected);
  target.querySelector("#delete")?.addEventListener("click", deleteSelected);
  target.querySelector("#copy-editable")?.addEventListener("click", () => {
    const destination = editableChunkFor(record.position[0], record.position[2]);
    if (!destination) return toast("No editable chunk is loaded nearby; add one with the Chunks tool.");
    const copy = { copyOf: record.uid, file: record.file, position: [...record.position], rotation: [...record.rotation], scale: record.scale };
    addRecord(destination, "prop", copy);
  });
}

function markDirty(chunk, kind) {
  chunk.dirty[kind === "light" ? "lights" : "objects"] = true;
  updateDirty();
}

function updateDirty() {
  const count = dirtyChunks().length;
  $("#status-dirty").textContent = count || state.zoneDirty ? `${count} chunk(s) unsaved${state.zoneDirty ? " + zone settings" : ""}` : "saved";
}

// --- selection and the transform gizmo -----------------------------------------------------------------

function select(object) {
  selected = object;
  gizmo.detach();
  if (object) {
    const { kind, chunk } = object.userData;
    const editable = kind === "marker" || (chunk && chunk.editable);
    if (editable) gizmo.attach(object);
    gizmo.showY = true;
    if (kind === "light" || (kind === "marker" && gizmo.mode === "scale")) gizmo.setMode("translate");
  }
  renderInspector();
}

let dragStart = null;
gizmo.addEventListener("dragging-changed", (event) => {
  if (event.value) {
    const { kind, record } = selected.userData;
    dragStart = kind === "marker" ? null : structuredClone({ position: record.position, rotation: record.rotation, scale: record.scale });
    return;
  }
  if (!selected) return;
  const { kind, record, chunk } = selected.userData;
  if (kind === "marker") { selected.userData.apply?.(); renderInspector(); updateDirty(); return; }
  // Dragged into another editable chunk: it belongs there now.
  const target = terrain.chunkAt(record.position[0], record.position[2]);
  if (target && target !== chunk && target.editable) props.rehome(selected, target);
  const object = selected, before = dragStart;
  if (before) undoStack.push(() => { Object.assign(record, before); applyTransform(object, kind === "light" ? { position: record.position, rotation: [0, 0, 0], scale: 1 } : record); markDirty(object.userData.chunk, kind); });
  renderInspector();
});
gizmo.addEventListener("objectChange", () => {
  if (!selected) return;
  const { kind, record, chunk } = selected.userData;
  if (kind === "marker") return;
  if (kind === "light") record.position = [selected.position.x, selected.position.y, selected.position.z];
  else readTransform(selected, record);
  markDirty(chunk, kind);
});

function editableChunkFor(x, z) {
  const under = terrain.chunkAt(x, z);
  if (under?.editable) return under;
  let best = null, bestDistance = Infinity;
  for (const chunk of terrain.chunks.values()) {
    if (!chunk.editable) continue;
    const d = Math.hypot(chunk.x0 + 128 - x, chunk.z0 + 128 - z);
    if (d < bestDistance) { best = chunk; bestDistance = d; }
  }
  return best;
}

function addRecord(chunk, kind, record) {
  const list = kind === "light" ? chunk.lights : chunk.objects;
  list.push(record);
  const object = kind === "light" ? props.addLight(chunk, record) : props.addProp(chunk, record);
  markDirty(chunk, kind);
  undoStack.push(() => {
    const owner = object.userData.chunk;
    const ownerList = kind === "light" ? owner.lights : owner.objects;
    ownerList.splice(ownerList.indexOf(record), 1);
    props.remove(object);
    if (selected === object) select(null);
    markDirty(owner, kind);
  });
  select(object);
  return object;
}

function deleteSelected() {
  if (!selected || selected.userData.kind === "marker") return;
  const { kind, record, chunk } = selected.userData;
  if (!chunk.editable) return toast("That's in a stock chunk, which is read-only.");
  const list = kind === "light" ? chunk.lights : chunk.objects;
  list.splice(list.indexOf(record), 1);
  const object = selected;
  props.remove(object);
  markDirty(chunk, kind);
  undoStack.push(() => {
    list.push(record);
    const restored = kind === "light" ? props.addLight(chunk, record) : props.addProp(chunk, record);
    restored.userData.chunk = chunk;
    markDirty(chunk, kind);
  });
  select(null);
}

function duplicateSelected() {
  if (!selected || selected.userData.kind === "marker") return;
  const { kind, record, chunk } = selected.userData;
  const destination = chunk.editable ? chunk : editableChunkFor(record.position[0], record.position[2]);
  if (!destination) return toast("No editable chunk is loaded nearby.");
  const copy = structuredClone({ ...record, object: undefined });
  delete copy.object;
  delete copy.uid;
  delete copy.id;
  copy.copyOf = record.uid || record.copyOf;
  copy.position = [record.position[0] + 2, record.position[1], record.position[2] + 2];
  addRecord(destination, kind, copy);
}

// --- ghost prop for placing ----------------------------------------------------------------------------

function makeGhost() {
  if (ghost) scene.remove(ghost);
  ghost = null;
  if (state.tool !== "props" || !state.actor) return;
  const holder = new THREE.Group();
  ghost = holder;
  scene.add(holder);
  library.model(state.actor).then((template) => {
    if (ghost !== holder) return;
    for (const child of template.children) {
      const mesh = child.clone();
      mesh.material = mesh.material.clone();
      mesh.material.transparent = true;
      mesh.material.opacity = 0.55;
      holder.add(mesh);
    }
  });
}

// --- pointer and keyboard ------------------------------------------------------------------------------

function updatePointer(event) {
  const rect = canvas.getBoundingClientRect();
  pointer.x = event.clientX - rect.left;
  pointer.y = event.clientY - rect.top;
  pointer.ndc.set((pointer.x / rect.width) * 2 - 1, -(pointer.y / rect.height) * 2 + 1);
}

function terrainHit(ndc = pointer.ndc) {
  raycaster.setFromCamera(ndc, camera);
  const { origin, direction } = raycaster.ray;
  return terrain.raycast(origin, direction);
}

function centreHit() {
  return terrainHit(new THREE.Vector2(0, 0));
}

// The cell under the pointer when there's no ground there: the ray meets the camera's ground plane.
function cellUnderPointer() {
  raycaster.setFromCamera(pointer.ndc, camera);
  const { origin, direction } = raycaster.ray;
  const hit = terrain.raycast(origin, direction);
  if (hit) return terrain.cellAt(hit.x, hit.z);
  const planeY = state.newChunk.height;
  if (Math.abs(direction.y) < 1e-4) return null;
  const t = (planeY - origin.y) / direction.y;
  if (t <= 0) return null;
  return terrain.cellAt(origin.x + direction.x * t, origin.z + direction.z * t);
}

canvas.addEventListener("contextmenu", (e) => e.preventDefault());
canvas.addEventListener("pointermove", (event) => {
  updatePointer(event);
  pointer.inside = true;
  if (pointer.buttons & 2) {
    // Mirrored view: dragging right turns right.
    look.yaw += event.movementX * 0.003;
    look.pitch = Math.max(-1.55, Math.min(1.55, look.pitch - event.movementY * 0.003));
  }
});
canvas.addEventListener("pointerleave", () => { pointer.inside = false; });
canvas.addEventListener("pointerdown", async (event) => {
  updatePointer(event);
  pointer.buttons = event.buttons;
  canvas.setPointerCapture(event.pointerId);
  if (event.button !== 0 || gizmo.dragging || gizmo.axis) return;
  if (!state.zone) return;
  const tool = state.tool;
  if (BRUSH_TOOLS.has(tool)) {
    if (tool === "colour" && event.altKey) {
      const hit = terrainHit();
      const picked = hit && terrain.sampleColour(hit.x, hit.z);
      if (picked) { state.colour = picked; renderPanel(); }
      return;
    }
    const hit = terrainHit();
    stroke = { snapshot: new Map(), target: hit?.y ?? 0, last: performance.now() };
    return;
  }
  if (tool === "chunks") {
    const cell = cellUnderPointer();
    if (cell) await addChunk(cell.row, cell.col);
    return;
  }
  raycaster.setFromCamera(pointer.ndc, camera);
  const markerHit = raycaster.intersectObjects(markers.filter((m) => m.userData.kind === "marker"), true)[0];
  const propHit = props.pick(raycaster);
  if (tool === "props" && state.actor && !event.shiftKey) {
    const hit = terrainHit();
    if (!hit) return;
    const chunk = editableChunkFor(hit.x, hit.z);
    if (!chunk) return toast("No editable chunk is loaded here. Add one with the Chunks tool (K).");
    const yaw = ghost ? ghost.rotation.y : 0;
    addRecord(chunk, "prop", { file: state.actor, position: [hit.x, hit.y, hit.z], rotation: [yaw, 0, 0], scale: 1 });
    return;
  }
  if (tool === "lights" && !propHit) {
    const hit = terrainHit();
    if (!hit) return;
    const chunk = editableChunkFor(hit.x, hit.z);
    if (!chunk) return toast("No editable chunk is loaded here. Add one with the Chunks tool (K).");
    addRecord(chunk, "light", { name: "newLight", colorName: "warm01", type: 0, position: [hit.x, hit.y + 6, hit.z], range: 20, intensity: 0.75, color: [239, 197, 25, 255] });
    return;
  }
  if (markerHit && (!propHit || markerHit.distance < propHit.distance)) {
    let node = markerHit.object;
    while (node.parent && !markers.includes(node)) node = node.parent;
    select(node);
  } else select(propHit ? propHit.object : null);
});
canvas.addEventListener("pointerup", (event) => {
  pointer.buttons = event.buttons;
  if (stroke) endStroke();
});
canvas.addEventListener("wheel", (event) => {
  event.preventDefault();
  const direction = Math.sign(event.deltaY);
  if (BRUSH_TOOLS.has(state.tool)) {
    if (event.ctrlKey) state.brush.strength = Math.max(0.02, Math.min(1, state.brush.strength * (direction > 0 ? 0.9 : 1.1)));
    else state.brush.radius = Math.max(2, Math.min(128, Math.round(state.brush.radius * (direction > 0 ? 0.9 : 1.1))));
    renderPanel();
  } else if (state.tool === "props" && ghost && event.shiftKey) {
    ghost.rotation.y += direction * Math.PI / 12;
  } else {
    look.speed = Math.max(5, Math.min(800, look.speed * (direction > 0 ? 0.85 : 1.18)));
    status(`Fly speed ${Math.round(look.speed)}`);
  }
}, { passive: false });

window.addEventListener("keydown", (event) => {
  if (event.target.closest("input, textarea, select")) return;
  const key = event.key.toLowerCase();
  if (event.ctrlKey && key === "s") { event.preventDefault(); saveAll(); return; }
  if (event.ctrlKey && key === "z") { event.preventDefault(); undo(); return; }
  if (event.ctrlKey && key === "d") { event.preventDefault(); duplicateSelected(); return; }
  if (event.ctrlKey) return;
  keys.add(key);
  const tools = { v: "select", b: "sculpt", e: "eco", c: "colour", n: "environment", p: "props", l: "lights", k: "chunks", z: "zone" };
  if (key === "escape") { select(null); state.actor = null; if (ghost) { scene.remove(ghost); ghost = null; } renderPanel(); }
  else if (key === "delete" || key === "backspace") deleteSelected();
  else if (key === "g") gizmo.setMode("translate");
  else if (key === "r") gizmo.setMode("rotate");
  else if (key === "t" && selected?.userData.kind === "prop") gizmo.setMode("scale");
  else if (key === "f" && selected) {
    const forward = new THREE.Vector3();
    camera.getWorldDirection(forward);
    const p = new THREE.Vector3();
    selected.getWorldPosition(p);
    camera.position.copy(p).addScaledVector(forward, -35).add(new THREE.Vector3(0, 8, 0));
  } else if (tools[key] && !"wasdqe".includes(key)) setTool(tools[key]);
});
window.addEventListener("keyup", (event) => keys.delete(event.key.toLowerCase()));
window.addEventListener("blur", () => keys.clear());

// --- brush strokes and undo ----------------------------------------------------------------------------

function snapshotChunks(chunks) {
  for (const chunk of chunks) {
    if (stroke.snapshot.has(chunk)) continue;
    stroke.snapshot.set(chunk, {
      heights: state.tool === "sculpt" ? chunk.heights.slice() : null,
      tileDirty: chunk.tileDirty.slice(),
      ecos: state.tool === "eco" ? chunk.ecos.slice() : null,
      weights: state.tool === "eco" ? chunk.weights.slice() : null,
      colour: state.tool === "eco" || state.tool === "colour" ? chunk.colour.slice() : null,
      tiles: state.tool === "environment" ? chunk.tiles.map((t) => t.environment) : null,
    });
  }
}

function applyStroke(dt) {
  if (!hover) return;
  const { radius, strength, mode } = state.brush;
  snapshotChunks(terrain.editableChunksNear(hover.x, hover.z, radius));
  if (state.tool === "sculpt") {
    let m = mode;
    if (keys.has("shift") && m === "raise") m = "lower";
    else if (keys.has("shift") && m === "lower") m = "raise";
    const target = mode === "level" ? (state.brush.level ?? 0) : stroke.target;
    terrain.sculpt(m, hover.x, hover.z, radius, strength, dt, target);
  } else if (state.tool === "eco" && state.eco !== null) {
    terrain.paintEco(state.eco, hover.x, hover.z, radius, strength, dt, state.ecoTint ? state.ecoColours.get(state.eco) : null, state.ecoTintAmount);
  } else if (state.tool === "colour") {
    terrain.paintColour(state.colour, hover.x, hover.z, radius, strength, dt);
  } else if (state.tool === "environment") {
    terrain.paintEnvironment(state.environment, hover.x, hover.z, radius);
  }
}

function endStroke() {
  const snapshot = stroke.snapshot;
  stroke = null;
  if (!snapshot.size) return;
  undoStack.push(() => {
    for (const [chunk, saved] of snapshot) {
      if (saved.heights) { chunk.heights.set(saved.heights); chunk.updatePositions(); }
      chunk.tileDirty = saved.tileDirty;
      if (saved.weights) { chunk.ecos = saved.ecos; chunk.weights = saved.weights; chunk.updateEcoTexture(terrain.selectedEco); }
      if (saved.colour) { chunk.colour.set(saved.colour); chunk.colourTexture.needsUpdate = true; }
      if (saved.tiles) { saved.tiles.forEach((e, t) => { chunk.tiles[t].environment = e; }); chunk.updateEnvironmentTexture(); }
    }
  });
  if (undoStack.length > 40) undoStack.shift();
  updateDirty();
}

function undo() {
  const step = undoStack.pop();
  if (!step) return status("Nothing to undo.");
  step();
  renderInspector();
  updateDirty();
  status("Undone.");
}

// --- chunks --------------------------------------------------------------------------------------------

async function addChunk(row, col) {
  const key = `${row}_${col}`;
  if (terrain.cells.has(key)) return status(`${key} already has a chunk.`);
  if (state.newChunk.eco === null) return toast("This zone has no ecos; add one in the Zone tool first.");
  try {
    const tint = await ecoColour(state.newChunk.eco).catch(() => [110, 140, 80]);
    const chunk = terrain.createChunk(row, col, state.newChunk.height, state.newChunk.eco, state.newChunk.environment, tint);
    undoStack.push(() => {
      if (!chunk.isNew) return;
      terrain.cells.delete(chunk.key);
      terrain.unload(chunk);
      drawMinimap();
    });
    toast(`Added chunk ${key} (x ${col * 64}…${col * 64 + 256}, z ${row * 64}…${row * 64 + 256}). Save to write it.`, "ok");
    drawMinimap();
    updateDirty();
  } catch (error) {
    toast(error.message, "error");
  }
}

// --- save ----------------------------------------------------------------------------------------------

function serialize(record, kind) {
  if (kind === "light") {
    const { uid, tile, name, colorName, type, position, range, intensity, color } = record;
    return { uid, tile, name, colorName, type, position, range, intensity, color };
  }
  const { uid, copyOf, tile, file, position, rotation, scale } = record;
  return { uid, copyOf, tile, file, position, rotation, scale };
}

async function saveAll() {
  if (!state.zone) return;
  const chunks = dirtyChunks();
  const settings = { ...state.settings };
  if (state.sky !== undefined) settings.sky = state.sky;
  if (state.customSky) { settings.customSky = state.customSky; settings.sky = state.customSky.name; }
  if (state.zoneDirty && state.houses.length) settings.houses = state.houses;
  if (!chunks.length && !state.zoneDirty) return toast("Nothing to save.");
  const entries = [], blobs = [];
  for (const chunk of chunks) {
    const entry = {
      row: chunk.row, col: chunk.col, heights: true,
      meta: {
        ecos: chunk.ecos, tileDirty: chunk.tileDirty, environments: chunk.tiles.map((t) => t.environment),
        objects: chunk.objects.map((r) => serialize(r, "prop")), lights: chunk.lights.map((r) => serialize(r, "light")),
      },
    };
    blobs.push(terrain.padded(chunk));
    if (chunk.dirty.ecos || chunk.isNew) { entry.weights = true; blobs.push(chunk.weights); }
    if (chunk.dirty.colour || chunk.isNew) { entry.colour = true; blobs.push(chunk.colour); }
    entries.push(entry);
  }
  $("#save").disabled = true;
  status(`Saving ${chunks.length} chunk(s)… (rebuilding terrain collision takes a few seconds per chunk)`);
  try {
    const result = await save({ zone: state.zone.name, chunks: entries, settings }, blobs);
    const files = [...result.chunks.flatMap((c) => c.files), ...result.zone.files];
    const unique = [...new Set(files)];
    toast(`Saved ${unique.length} file(s):\n${unique.map((f) => f.split(/[\\/]/).slice(-2).join("/")).join("\n")}${result.zone.mirrored.length ? `\n…and copied ${result.zone.mirrored.length} into the built servers.` : ""}\nRestart the servers (Gateway, WebAPI) to serve them.`, "ok");
    const position = camera.position.clone(), yaw = look.yaw, pitch = look.pitch;
    await openZone(state.zone.name, true);
    camera.position.copy(position);
    look.yaw = yaw;
    look.pitch = pitch;
    await loadZones();
  } catch (error) {
    toast(`Save failed: ${error.message}`, "error");
  } finally {
    $("#save").disabled = false;
    updateDirty();
  }
}

// --- minimap -------------------------------------------------------------------------------------------

// The area the minimap covers, in tiles: the zone's bounds and every chunk, plus a ring of empty cells
// around a custom zone to grow it into.
function minimapArea() {
  const b = state.zone.bounds;
  let [x0, z0, x1, z1] = [b.startX, b.startZ, b.endX, b.endZ];
  for (const c of terrain.cells.values()) {
    x0 = Math.min(x0, c.col); z0 = Math.min(z0, c.row);
    x1 = Math.max(x1, c.col + 4); z1 = Math.max(z1, c.row + 4);
  }
  return state.zone.stock ? { x0, z0, x1, z1 } : { x0: x0 - 4, z0: z0 - 4, x1: x1 + 4, z1: z1 + 4 };
}

function minimapTransform() {
  const a = minimapArea();
  const size = 220, pad = 8;
  const width = (a.x1 - a.x0) * 64, depth = (a.z1 - a.z0) * 64;
  const scale = (size - pad * 2) / Math.max(width, depth);
  // Mirrored like the view: +x to the left, +z up.
  return {
    scale,
    toMap: (x, z) => [size - pad - (x - a.x0 * 64) * scale, size - pad - (z - a.z0 * 64) * scale],
    toWorld: (u, v) => [(size - pad - u) / scale + a.x0 * 64, (size - pad - v) / scale + a.z0 * 64],
  };
}

function drawMinimap() {
  const context = $("#minimap").getContext("2d");
  context.clearRect(0, 0, 220, 220);
  if (!state.zone) return;
  const { scale, toMap } = minimapTransform();
  const b = state.zone.bounds;
  const [bx0, bz0] = toMap(b.startX * 64, b.startZ * 64), [bx1, bz1] = toMap(b.endX * 64, b.endZ * 64);
  context.strokeStyle = "#556";
  context.strokeRect(Math.min(bx0, bx1), Math.min(bz0, bz1), Math.abs(bx1 - bx0), Math.abs(bz1 - bz0));
  const cell = CHUNK * scale;
  for (const [key, c] of terrain.cells) {
    const [u, v] = toMap(c.col * 64 + 256, c.row * 64 + 256);
    const chunk = terrain.chunks.get(key);
    context.fillStyle = chunk?.anyDirty ? "#e8b84a" : c.editable ? "#5fbf7a" : "#4a5263";
    context.fillRect(u, v, Math.max(cell - 0.5, 1), Math.max(cell - 0.5, 1));
    if (chunk) { context.strokeStyle = "#cfd6e4"; context.strokeRect(u + 0.5, v + 0.5, cell - 1.5, cell - 1.5); }
  }
  for (const wall of state.zone.walls) {
    context.strokeStyle = "#e2675f";
    context.beginPath();
    wall.forEach((p, i) => { const [u, v] = toMap(p[0], p[2]); i ? context.lineTo(u, v) : context.moveTo(u, v); });
    context.stroke();
  }
  const [cu, cv] = toMap(camera.position.x, camera.position.z);
  const forward = new THREE.Vector3();
  camera.getWorldDirection(forward);
  context.fillStyle = "#3fd0ff";
  context.beginPath();
  context.arc(cu, cv, 4, 0, Math.PI * 2);
  context.fill();
  context.strokeStyle = "#3fd0ff";
  context.beginPath();
  context.moveTo(cu, cv);
  const [fu, fv] = toMap(camera.position.x + forward.x * 300, camera.position.z + forward.z * 300);
  const length = Math.hypot(fu - cu, fv - cv) || 1;
  context.lineTo(cu + (fu - cu) / length * 14, cv + (fv - cv) / length * 14);
  context.stroke();
}

$("#minimap").addEventListener("pointerdown", (event) => {
  if (!state.zone) return;
  const rect = event.target.getBoundingClientRect();
  const [x, z] = minimapTransform().toWorld(event.clientX - rect.left, event.clientY - rect.top);
  if (state.tool === "chunks" && event.button === 0 && !event.altKey) {
    const cell = terrain.cellAt(x, z);
    if (!terrain.cells.has(cell.key)) { addChunk(cell.row, cell.col); return; }
  }
  const h = terrain.heightAt(x, z);
  camera.position.set(x, (h ?? camera.position.y - 60) + 60, z - 40);
});

// --- walls ---------------------------------------------------------------------------------------------

let wallMesh = null;
function buildWalls() {
  if (wallMesh) scene.remove(wallMesh);
  wallMesh = null;
  if (!state.zone?.walls.length) return;
  const positions = [];
  for (const wall of state.zone.walls) {
    for (let i = 0; i + 3 < wall.length; i += 2) {
      const [a, b, c, d] = [wall[i], wall[i + 1], wall[i + 2], wall[i + 3]];
      positions.push(...a, ...b, ...c, ...b, ...d, ...c);
    }
  }
  const geometry = new THREE.BufferGeometry();
  geometry.setAttribute("position", new THREE.Float32BufferAttribute(positions, 3));
  wallMesh = new THREE.Mesh(geometry, new THREE.MeshBasicMaterial({ color: 0xe2675f, transparent: true, opacity: 0.18, side: THREE.DoubleSide, depthWrite: false }));
  scene.add(wallMesh);
}

// --- frame loop ----------------------------------------------------------------------------------------

const clock = new THREE.Clock();
let minimapTimer = 0;
function frame() {
  const dt = Math.min(clock.getDelta(), 0.1);
  // Fly: the view is mirrored, so screen-right is the camera's -x.
  camera.rotation.set(look.pitch, look.yaw, 0, "YXZ");
  const forward = new THREE.Vector3(), right = new THREE.Vector3();
  camera.getWorldDirection(forward);
  right.crossVectors(forward, camera.up).normalize().negate();
  const speed = look.speed * (keys.has("shift") ? 4 : 1) * dt;
  if (!document.activeElement?.closest?.("input, textarea, select")) {
    if (keys.has("w")) camera.position.addScaledVector(forward, speed);
    if (keys.has("s")) camera.position.addScaledVector(forward, -speed);
    if (keys.has("d")) camera.position.addScaledVector(right, speed);
    if (keys.has("a")) camera.position.addScaledVector(right, -speed);
    if (keys.has("e")) camera.position.y += speed;
    if (keys.has("q")) camera.position.y -= speed;
  }
  camera.updateMatrixWorld();

  if (state.zone) {
    terrain.update(camera.position.x, camera.position.z);
    hover = pointer.inside && !(pointer.buttons & 2) ? terrainHit() : null;
    const brushing = BRUSH_TOOLS.has(state.tool) && hover;
    terrain.uniforms.brush.value.set(hover?.x ?? 0, hover?.z ?? 0, brushing ? state.brush.radius : 0);
    if (stroke && pointer.buttons & 1) {
      applyStroke(dt);
      updateDirty();
    }
    if (ghost && hover) ghost.position.set(hover.x, hover.y, hover.z);
    if (hover) {
      const chunk = hover.chunk;
      const tile = chunk ? Math.floor((hover.z - chunk.z0) / 64) * 4 + Math.floor((hover.x - chunk.x0) / 64) : null;
      $("#status-cell").textContent = chunk
        ? `chunk ${chunk.key}${chunk.editable ? (chunk.isNew ? " (new)" : "") : " (stock, read-only)"} · tile ${tile} env ${chunk.tiles[tile]?.environment} · ground ${hover.y.toFixed(1)}`
        : "";
    }
    $("#status-pos").textContent = `x ${camera.position.x.toFixed(0)}  y ${camera.position.y.toFixed(0)}  z ${camera.position.z.toFixed(0)}`;
    minimapTimer += dt;
    if (minimapTimer > 0.25) { minimapTimer = 0; drawMinimap(); }
  }
  renderer.render(scene, camera);
  requestAnimationFrame(frame);
}

// --- top bar -------------------------------------------------------------------------------------------

for (const button of document.querySelectorAll("#toolbar button")) button.addEventListener("click", () => setTool(button.dataset.tool));
$("#open-zone").addEventListener("click", () => openZone($("#zone-search").value.trim()).then(buildWalls));
$("#zone-search").addEventListener("keydown", (e) => { if (e.key === "Enter") openZone(e.target.value.trim()).then(buildWalls); });
$("#save").addEventListener("click", saveAll);
$("#undo").addEventListener("click", undo);
$("#view-radius").addEventListener("input", (e) => { terrain.radius = +e.target.value; terrain.uniforms.fogFar.value = 500 + 400 * terrain.radius; terrain.uniforms.fogNear.value = terrain.uniforms.fogFar.value * 0.6; });
$("#show-grid").addEventListener("change", (e) => { terrain.uniforms.showGrid.value = e.target.checked ? 1 : 0; });
$("#show-lights").addEventListener("change", (e) => props.setLightsVisible(e.target.checked));
window.addEventListener("beforeunload", (e) => { if (dirtyChunks().length || state.zoneDirty) e.preventDefault(); });

const dialog = $("#new-zone-dialog");
$("#new-zone").addEventListener("click", async () => {
  const form = dialog.querySelector("form");
  form.source.innerHTML = `<option value="">Blank terrain</option>` + state.zones
    .filter((z) => z.name.toLowerCase() !== "fabledrealms")
    .map((z) => `<option value="${escape(z.name)}">Copy of ${escape(z.name)}${z.type ? ` (${z.type})` : ""}</option>`).join("");
  try {
    const fabled = await getJson("/api/zone?name=FabledRealms");
    form.eco.innerHTML = fabled.ecos.map((e) => `<option value="${e.index}" ${e.name.toLowerCase() === "sg_grass" ? "selected" : ""}>${escape(e.name)}</option>`).join("");
  } catch { form.eco.innerHTML = ""; }
  form.source.onchange = () => { dialog.querySelector(".blank-only").style.display = form.source.value ? "none" : ""; };
  dialog.showModal();
});
dialog.addEventListener("close", async () => {
  if (dialog.returnValue !== "create") return;
  const form = dialog.querySelector("form");
  status(`Creating ${form.name.value}…`);
  try {
    const zone = await postJson("/api/zone/new", {
      name: form.name.value, type: form.type.value, source: form.source.value,
      width: +form.width.value, depth: +form.depth.value, height: +form.height.value,
      ecoZone: form.eco.value !== "" ? "FabledRealms" : null, ecoIndex: form.eco.value !== "" ? +form.eco.value : null,
    });
    await loadZones();
    await openZone(zone.name);
    buildWalls();
    toast(`Created ${zone.name}: client files in ClientMods, Zones/${zone.name}.json${form.type.value === "Housing" ? " and a Houses.json entry" : ""}.`, "ok");
  } catch (error) {
    toast(error.message, "error");
  }
});

// For poking at the editor from the browser console.
window.zoneEditor = { THREE, scene, camera, look, terrain, props, state, openZone, saveAll, addChunk, setTool };

noZonePanel();
updateDirty();
loadZones().then(() => {
  let last = null;
  try { last = localStorage.getItem("zone-editor-zone"); } catch { /* private mode */ }
  $("#zone-search").value = last || "FabledRealms";
}).catch((error) => toast(error.message, "error"));
requestAnimationFrame(frame);
