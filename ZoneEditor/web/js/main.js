// Sanctuary zone editor: the viewport, tools and panels.
import * as THREE from "three";
import { TransformControls } from "three/addons/controls/TransformControls.js";
import { ASSET_VERSION, frame as frameBody, getJson, postJson, save, unframe } from "./api.js";
import { CHUNK, FALLOFFS, STEP, TerrainChunk, TerrainManager, environmentColour } from "./terrain.js";
import { PropLayer, PropLibrary, applyTransform, makeMarker, readTransform } from "./props.js";
import { ContentBrowser } from "./content.js";
import { parseSky, sampleSky } from "./sky.js";
import { gradientColours, initTextures } from "./textures.js";

const $ = (selector) => document.querySelector(selector);
const canvas = $("#canvas");
const panel = $("#panel");

// --- scene ---------------------------------------------------------------------------------------------

const renderer = new THREE.WebGLRenderer({ canvas, antialias: true });
renderer.outputColorSpace = THREE.SRGBColorSpace;
renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
renderer.shadowMap.enabled = true;
renderer.shadowMap.type = THREE.PCFSoftShadowMap;
initTextures(renderer);
const scene = new THREE.Scene();
const camera = new THREE.PerspectiveCamera(60, 1, 0.5, 6000);
camera.rotation.order = "YXZ";

// Lit like the client: a sun that casts shadows around the camera, sky and ground light from the sky's
// ambient gradient, its fog, and the zone's own point lights nearest the camera.
const sun = new THREE.DirectionalLight(0xffffff, 2.2);
sun.castShadow = true;
sun.shadow.mapSize.set(2048, 2048);
Object.assign(sun.shadow.camera, { left: -180, right: 180, top: 180, bottom: -180, near: 1, far: 1600 });
sun.shadow.bias = -0.0004;
sun.shadow.normalBias = 0.6;
const sunDirection = new THREE.Vector3(-0.45, 0.8, -0.4).normalize();
const hemisphere = new THREE.HemisphereLight(0xdde8ff, 0x6b5f4a, 1.1);
scene.add(sun, sun.target, hemisphere);
scene.fog = new THREE.Fog(0x9fc6e8, 600, 1500);
const pointLights = Array.from({ length: 8 }, () => {
  const light = new THREE.PointLight(0xffffff, 0, 20, 1);
  scene.add(light);
  return light;
});

const skyDome = new THREE.Mesh(new THREE.SphereGeometry(5000, 32, 16), new THREE.ShaderMaterial({
  side: THREE.BackSide, depthWrite: false, fog: false,
  uniforms: { top: { value: new THREE.Color(0x3d7fd6) }, horizon: { value: new THREE.Color(0x9fc6e8) } },
  vertexShader: "varying vec3 vDir; void main() { vDir = normalize(position); gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0); }",
  fragmentShader: `uniform vec3 top; uniform vec3 horizon; varying vec3 vDir;
    void main() { float t = pow(clamp(vDir.y, 0.0, 1.0), 0.6); gl_FragColor = vec4(mix(horizon, top, t), 1.0);
      #include <colorspace_fragment>
    }`,
}));
skyDome.renderOrder = -1;
scene.add(skyDome);

const terrain = new TerrainManager(scene, {
  onChunkLoaded: (chunk) => { props.addChunk(chunk); drawMinimap(); if (contentOpen()) content.renderGroups(); },
  onChunkUnloaded: (chunk) => { props.removeChunk(chunk); if (selected?.userData.chunk === chunk) select(null); },
  status,
});
terrain.anisotropy = renderer.capabilities.getMaxAnisotropy();
const library = new PropLibrary();
const props = new PropLayer(scene, library);

// The client shows its coordinates as they are (x, y up, z), so the scene uses them directly.
function updateProjection() {
  const { clientWidth: w, clientHeight: h } = canvas;
  renderer.setSize(w, h, false);
  camera.aspect = w / Math.max(h, 1);
  camera.updateProjectionMatrix();
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
  brush: { radius: 16, strength: 0.5, mode: "raise", step: 4 },
  eco: null,
  ecoPick: null,
  ecoTint: true,
  ecoTintAmount: 0.85,
  ecoTintPick: null,       // a tint the eyedropper took from the ground, painted instead of the eco's own colour
  exclude: { mode: "cut", target: null, onlyInside: true },
  ecoColours: new Map(),
  colour: [96, 140, 70],
  environment: 0,
  actor: null,
  newChunk: { height: 0, eco: null, ecoKey: null, environment: 0 },
  settings: {},
  houses: null,
  sky: undefined,
  customSky: null,
  zoneDirty: false,
  time: 12,
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

// The ramp brush's line, from where the drag started to the pointer.
const rampLine = new THREE.Line(new THREE.BufferGeometry().setFromPoints([new THREE.Vector3(), new THREE.Vector3()]),
  new THREE.LineBasicMaterial({ color: 0xe8b84a, depthTest: false }));
rampLine.visible = false;
rampLine.renderOrder = 10;
scene.add(rampLine);

function setRampLine(start, end) {
  const position = rampLine.geometry.attributes.position;
  position.setXYZ(0, start.x, start.y + 0.5, start.z);
  position.setXYZ(1, end.x, end.y + 0.5, end.z);
  position.needsUpdate = true;
  rampLine.geometry.computeBoundingSphere();
  rampLine.visible = true;
}

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
  listZones();
  status(`Saving into ${data.mods} · client ${data.client}`);
}

async function openZone(name, keepCamera = false) {
  if (!name) return;
  if (!keepCamera && state.zone) {
    if ((dirtyChunks().length || state.zoneDirty) && !confirm("Discard unsaved changes?")) return;
    // Whatever this page autosaved is discarded or undone now.
    if (drafted.size || zoneAutosaved) await clearDraft(state.zone.id);
  }
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
  zoneAutosaved = "";
  drafted.clear();
  state.eco = zone.ecos[0]?.index ?? null;
  state.newChunk.eco = state.eco;
  terrain.setZone(zone);
  terrain.setSelectedEco(state.eco);
  undoStack.length = 0;
  try { localStorage.setItem("zone-editor-zone", zone.id); } catch { /* private mode */ }
  $("#zone-search").value = zone.id;
  const editable = zone.chunks.filter((c) => c.editable).length;
  $("#zone-title").textContent = `${zone.name} · ${zoneSource(zone)} · ${zone.chunks.length} chunks${zone.definition ? " · " + zone.definition.Type + " " + zone.definition.Id : " · no Sanctuary definition"}`;
  $("#zone-title").title = zone.file || "";
  if (zone.file) rememberFile(zone.file);
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
  if (zone.file && !keepCamera) {
    toast(`Opened ${zone.file}: its zone files come only from that folder, nothing from stock.\n` +
      "Saving writes them back there and publishes them to ClientMods for the servers.");
  } else if (zone.installed && !keepCamera) {
    toast(`${zone.name}'s files are in the pack install_client_mods.py wrote, which the client can't update.\n` +
      "Run install_client_mods.py again (it now leaves zone files out) before testing edits to it in game.");
  } else if (zone.stock && !keepCamera) {
    toast(`${zone.name} is a stock zone. Saving a stock chunk also hides the client's own copy so it downloads yours; ` +
      "that needs the client closed, which Test in game does for you.");
  }
  updateLightingFromSky(zone.definition?.Sky);
  status(`${zone.name}: ${zone.chunks.length} chunks`);
  if (!keepCamera) offerDraft();
  setTool(state.tool);
  updateDirty();
}

function dirtyChunks() {
  return [...terrain.chunks.values()].filter((c) => c.anyDirty);
}

function zoneSource(zone) {
  if (zone.file) return `file ${zone.file.split(/[\\/]/).slice(-2).join("\\")}`;
  return zone.stock ? "stock zone" : "custom zone";
}

// Zone files opened recently, offered in the zone box above the zone names.
function recentFiles() {
  try { return JSON.parse(localStorage.getItem("zone-editor-files")) || []; } catch { return []; }
}

function rememberFile(path) {
  const files = [path, ...recentFiles().filter((f) => f.toLowerCase() !== path.toLowerCase())].slice(0, 8);
  try { localStorage.setItem("zone-editor-files", JSON.stringify(files)); } catch { /* private mode */ }
  listZones();
}

function listZones() {
  $("#zone-list").innerHTML = recentFiles().map((f) => `<option value="${escape(f)}">recent file</option>`).join("") +
    state.zones.map((z) => `<option value="${z.name}">${z.stock ? "stock" : "custom"}${z.type ? " · " + z.type + " " + z.id : ""}</option>`).join("");
}

async function openZoneFile() {
  const start = state.zone?.file || recentFiles()[0] || null;
  status("Pick a zone file in the dialog…");
  try {
    const { path } = await postJson("/api/pick-zone", { start });
    if (!path) return status("No file picked.");
    await openZone(path);
    buildWalls();
  } catch (error) {
    toast(`Open file failed: ${error.message}`, "error");
  }
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

// The sky in use, parsed; the zone's own (Sky), the one picked in the Zone tool, or the client's default.
let skyTracks = null;
let skyName = null;
const ambientGradients = new Map();

async function updateLightingFromSky(sky, xml) {
  skyName = xml ? null : sky || "sky.xml";
  try {
    if (!xml) xml = await (await fetch(`/api/sky?name=${encodeURIComponent(skyName)}`)).text();
  } catch { xml = null; }
  skyTracks = xml ? parseSky(xml) : null;
  applyTimeOfDay();
}

// Without a zone sky the client lights each tile by its environment's sky (Seaside, Briarwood, ...).
function updateEnvironmentSky() {
  const zoneSky = state.customSky || (state.sky !== undefined ? state.sky : state.zone?.definition?.Sky);
  if (zoneSky || !state.zone) return;
  const chunk = terrain.chunkAt(camera.position.x, camera.position.z);
  if (!chunk) return;
  const tile = Math.floor((camera.position.z - chunk.z0) / 64) * 4 + Math.floor((camera.position.x - chunk.x0) / 64);
  const environment = state.zone.environments.find((e) => e.id === chunk.tiles[tile]?.environment);
  const sky = environment?.sky || "sky.xml";
  if (sky.toLowerCase() !== skyName?.toLowerCase()) updateLightingFromSky(sky);
}

// Lights the scene for state.time (hours) from the sky's keyframes.
function applyTimeOfDay() {
  const sample = skyTracks ? sampleSky(skyTracks, state.time) : {};
  if (sample.sun) {
    // The client multiplies by the sky's sun colour as it is, so it isn't decoded from sRGB.
    sun.color.setRGB(...sample.sun.rgb, THREE.LinearSRGBColorSpace);
    sun.intensity = 2.4 * Math.min(sample.sun.brightness, 2.5);
  } else {
    sun.color.set(0xfff4e0);
    sun.intensity = 2.2;
  }
  if (sample.direction) {
    const heading = THREE.MathUtils.degToRad(sample.direction.heading), pitch = THREE.MathUtils.degToRad(Math.max(sample.direction.pitch, 8));
    sunDirection.set(Math.sin(heading) * Math.cos(pitch), Math.sin(pitch), Math.cos(heading) * Math.cos(pitch)).normalize();
  }
  if (sample.fog) {
    scene.fog.color.setRGB(...sample.fog, THREE.SRGBColorSpace);
    skyDome.material.uniforms.horizon.value.copy(scene.fog.color);
    skyDome.material.uniforms.top.value.copy(scene.fog.color).lerp(new THREE.Color(0x1d4fa0), 0.55);
  }
  if (sample.range) {
    // The client's fog starts close; the editor keeps the near half clear so there's something to edit.
    scene.fog.far = Math.max(sample.range.far, 500) * 1.4;
    scene.fog.near = scene.fog.far * 0.45;
  }
  const ambient = sample.ambient;
  if (ambient?.file) {
    const apply = (colours) => {
      hemisphere.color.copy(colours.sky).lerp(new THREE.Color(1, 1, 1), 0.35);
      hemisphere.groundColor.copy(colours.ground).lerp(new THREE.Color(1, 1, 1), 0.2);
      hemisphere.intensity = 1.6 * ambient.brightness;
    };
    if (ambientGradients.has(ambient.file)) apply(ambientGradients.get(ambient.file));
    else gradientColours(ambient.file).then((colours) => { ambientGradients.set(ambient.file, colours); apply(colours); }).catch(() => {});
  }
}

// The zone's lights nearest the camera become real point lights.
function updatePointLights() {
  const nearby = [];
  for (const chunk of terrain.chunks.values()) {
    for (const record of chunk.lights) {
      const [x, y, z] = record.position;
      nearby.push([camera.position.distanceToSquared(new THREE.Vector3(x, y, z)), record]);
    }
  }
  nearby.sort((a, b) => a[0] - b[0]);
  pointLights.forEach((light, i) => {
    const record = nearby[i]?.[0] < 300 * 300 ? nearby[i][1] : null;
    light.visible = !!record;
    if (!record) return;
    light.position.set(...record.position);
    light.color.setRGB(record.color[0] / 255, record.color[1] / 255, record.color[2] / 255, THREE.SRGBColorSpace);
    light.intensity = 60 * Math.max(record.intensity, 0.1);
    light.distance = record.range * 2;
  });
}

// --- tools and panels ----------------------------------------------------------------------------------

const BRUSH_TOOLS = new Set(["sculpt", "exclude", "eco", "colour", "environment"]);
// The sculpt modes, in the order the number keys pick them (1 to 0; noise has no key).
const SCULPT_MODES = ["raise", "lower", "smooth", "flatten", "slope", "level", "ramp", "terrace", "erode", "sharpen", "noise"];
const SCULPT_HINTS = {
  raise: "Raises the ground; Shift lowers.", lower: "Lowers the ground; Shift raises.",
  smooth: "Evens out bumps.", flatten: "Pulls the ground to the height where the stroke started.",
  slope: "Pulls the ground onto the slope under the brush where the stroke started: flattens lumps without levelling a hillside.",
  level: "Sets the ground to the level height below (Alt+click picks it from the ground).",
  ramp: "Drag from one point to another: the ground between them becomes a straight ramp as wide as the brush.",
  terrace: "Cuts the ground into steps of the step height.", erode: "Slides cliffs and spikes down to a natural 35° slope.",
  sharpen: "The opposite of smooth: brings out edges and ridges.", noise: "Roughens the ground.",
};

function setTool(tool) {
  state.tool = tool;
  for (const button of document.querySelectorAll("#toolbar button")) button.classList.toggle("active", button.dataset.tool === tool);
  terrain.uniforms.ecoOverlay.value = tool === "eco" ? 1 : 0;
  terrain.uniforms.envOverlay.value = tool === "environment" ? 1 : 0;
  terrain.uniforms.brush.value.z = 0;
  if (ghost) { scene.remove(ghost); ghost = null; }
  if (tool === "props" && state.actor) makeGhost();
  if (tool === "props" && state.zone && !contentOpen()) toggleContent(true);
  renderPanel();
}

function html(strings, ...values) {
  return strings.reduce((out, s, i) => out + s + (i < values.length ? values[i] : ""), "");
}

function escape(text) {
  return String(text).replace(/[&<>"]/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;" })[c]);
}

function brushControls(modes, current = state.brush.mode) {
  return html`
    ${modes ? `<div class="seg">${modes.map((m, k) => `<button data-mode="${m}" class="${current === m ? "active" : ""}" title="${k < 10 ? `Key ${(k + 1) % 10}` : ""}">${m}</button>`).join("")}</div>` : ""}
    <label>Radius <span id="radius-value">${state.brush.radius}</span><input id="brush-radius" type="range" min="2" max="128" value="${state.brush.radius}"></label>
    <label>Strength <span id="strength-value">${state.brush.strength.toFixed(2)}</span><input id="brush-strength" type="range" min="0.02" max="1" step="0.01" value="${state.brush.strength}"></label>
    <label>Falloff <select id="brush-falloff">${FALLOFFS.map((f) => `<option ${terrain.falloff === f ? "selected" : ""}>${f}</option>`).join("")}</select></label>`;
}

function bindBrush(setMode = (mode) => { state.brush.mode = mode; }) {
  panel.querySelector("#brush-radius")?.addEventListener("input", (e) => { state.brush.radius = +e.target.value; $("#radius-value").textContent = state.brush.radius; });
  panel.querySelector("#brush-strength")?.addEventListener("input", (e) => { state.brush.strength = +e.target.value; $("#strength-value").textContent = state.brush.strength.toFixed(2); });
  panel.querySelector("#brush-falloff")?.addEventListener("change", (e) => { terrain.falloff = e.target.value; });
  for (const button of panel.querySelectorAll("[data-mode]")) {
    button.addEventListener("click", () => { setMode(button.dataset.mode); renderPanel(); });
  }
}

function ecoName(index) {
  return state.zone?.ecos.find((e) => e.index === index)?.name ?? `eco ${index}`;
}

function rgbHex(rgb) {
  return "#" + rgb.map((v) => v.toString(16).padStart(2, "0")).join("");
}

// The eyedropper: what's on the ground under the pointer becomes what the brushes paint. part limits it to
// one tool's (Alt+click: "eco" takes the eco and its tint, "colour" the colour, "sculpt" the height); I takes all.
function eyedrop(part = null) {
  const hit = terrainHit();
  if (!hit) return toast("No ground under the pointer to pick from.");
  const picked = [];
  const tint = terrain.sampleColour(hit.x, hit.z);
  if (!part || part === "eco") {
    const [top] = terrain.sampleEcos(hit.x, hit.z);
    if (top) {
      pickEco(top.index, null);
      picked.push(`eco ${ecoName(top.index)} (${Math.round(top.weight * 100)}%)`);
    }
    if (tint) { state.ecoTintPick = tint; picked.push(`tint ${rgbHex(tint)}`); }
  }
  if ((!part || part === "colour") && tint) {
    state.colour = tint;
    if (part) picked.push(`colour ${rgbHex(tint)}`);
  }
  if (!part || part === "sculpt") {
    state.brush.level = Math.round(hit.y * 4) / 4;
    picked.push(`height ${state.brush.level}`);
  }
  renderPanel();
  status(`Picked ${picked.join(" · ")}`);
}

// A test for points inside a prop's bounds, in its own frame so a turned model's box turns with it.
function insideTest(object) {
  object.updateMatrixWorld(true);
  const inverse = object.matrixWorld.clone().invert();
  const box = new THREE.Box3(), relative = new THREE.Matrix4();
  object.traverse((node) => {
    if (!node.isMesh || !node.geometry) return;
    if (!node.geometry.boundingBox) node.geometry.computeBoundingBox();
    box.union(node.geometry.boundingBox.clone().applyMatrix4(relative.multiplyMatrices(inverse, node.matrixWorld)));
  });
  const point = new THREE.Vector3();
  return (x, y, z) => box.containsPoint(point.set(x, y, z).applyMatrix4(inverse));
}

function excludeTarget() {
  const target = state.exclude.target;
  return target?.parent ? target : null;
}

// Cuts out (or restores) all the ground inside the exclude target's bounds, as one undo step.
function cutInsideTarget(restore) {
  const target = excludeTarget();
  if (!target) return toast("Shift+click a cave model first.");
  const box = new THREE.Box3().setFromObject(target);
  const centre = box.getCenter(new THREE.Vector3());
  const radius = Math.hypot(box.max.x - box.min.x, box.max.z - box.min.z) / 2 + STEP;
  stroke = { snapshot: new Map() };
  snapshotChunks(terrain.editableChunksNear(centre.x, centre.z, radius));
  const chunks = terrain.exclude(centre.x, centre.z, radius, restore, insideTest(target));
  endStroke();
  updateDirty();
  toast(chunks ? `${restore ? "Restored" : "Cut out"} the ground inside ${target.userData.record.file} (${chunks} chunk(s)).` : "No ground inside it to change.", chunks ? "ok" : "");
}

async function ecoColour(index) {
  if (!state.ecoColours.has(index)) {
    state.ecoColours.set(index, await getJson(`/api/eco-colour?zone=${encodeURIComponent(state.zone.id)}&index=${index}`));
  }
  return state.ecoColours.get(index);
}

// A palette row: the eco's detail texture as a swatch, its name, and a note (by default its texture and flora).
function ecoRow(eco, attributes, active, note) {
  const text = note ?? `${eco.texture}${eco.floras.length ? ` · ${eco.floras.length} flora` : ""}`;
  const used = eco.zones?.length ? `\nUsed in ${eco.zones.join(", ")}${eco.zoneCount > eco.zones.length ? ", …" : ""}` : "";
  return `<div ${attributes} class="${active ? "active" : ""}" title="${escape(eco.texture + used)}">` +
    `<img loading="lazy" alt="" src="/api/texture?name=${encodeURIComponent(eco.texture)}&size=48&opaque=1&v=${ASSET_VERSION}">` +
    `${escape(eco.name)} <small>${escape(text)}</small></div>`;
}

// Every eco the client has (zones' lists and .eco definitions), fetched once.
let palettePromise = null;
function loadPalette() {
  palettePromise ??= getJson("/api/ecos").catch((error) => { palettePromise = null; throw error; });
  return palettePromise;
}

// The eco to paint: one the zone has (index), or a palette entry it doesn't, added on first use.
function pickEco(index, entry) {
  state.eco = index;
  state.ecoPick = entry;
  state.ecoTintPick = null;
  terrain.setSelectedEco(index);
}

async function ensureEco() {
  if (!state.ecoPick) return state.eco;
  const eco = await postJson("/api/zone/eco", { zone: state.zone.id, palette: state.ecoPick.key });
  if (!state.zone.ecos.some((e) => e.index === eco.index)) state.zone.ecos.push(eco);
  state.zoneDirty = true;
  pickEco(eco.index, null);
  await ecoColour(eco.index).catch(() => {});
  toast(`Added ${eco.name} to ${state.zone.name}'s ecos; it's saved with the zone.`, "ok");
  if (state.tool === "eco") renderPanel();
  return eco.index;
}

function noZonePanel() {
  panel.innerHTML = `<h3>No zone open</h3><p class="hint">Type a zone name at the top (FabledRealms, a housing lot such as hsg_emptylot_castle_01, or one you made) and press Open, or make a new zone.</p>`;
}

function renderPanel() {
  const zone = state.zone;
  if (!zone) return noZonePanel();
  const editableCount = [...terrain.chunks.values()].filter((c) => c.editable).length;
  const noEditable = !editableCount && BRUSH_TOOLS.has(state.tool)
    ? `<p class="warn">No chunk is loaded here yet.</p>` : "";

  if (state.tool === "select") {
    panel.innerHTML = `<h3>Select</h3><p class="hint">Click a prop, light or marker. G move, R rotate, T scale, Del delete, Ctrl+D duplicate, F focus.</p><div id="inspector"></div>`;
    renderInspector();
  } else if (state.tool === "sculpt") {
    const mode = state.brush.mode;
    panel.innerHTML = `<h3>Sculpt</h3>${noEditable}${brushControls(SCULPT_MODES)}
      <label style="${mode === "level" ? "" : "display:none"}">Level height <input id="level-height" type="number" step="0.25" value="${state.brush.level ?? 0}"></label>
      <label style="${mode === "terrace" ? "" : "display:none"}">Step height <input id="terrace-step" type="number" min="0.5" step="0.5" value="${state.brush.step}"></label>
      <p class="hint"><b>${mode}</b>: ${SCULPT_HINTS[mode]}</p>
      <p class="hint">Hold the left button to sculpt. Keys 1–0 pick a mode, Alt+click picks the ground's height, I picks height, eco and tint. Edges shared with chunks that aren't loaded stay pinned so the seams don't open.</p>`;
    bindBrush();
    panel.querySelector("#level-height").addEventListener("input", (e) => { state.brush.level = +e.target.value; });
    panel.querySelector("#terrace-step").addEventListener("input", (e) => { state.brush.step = Math.max(0.5, +e.target.value); });
  } else if (state.tool === "exclude") {
    const target = excludeTarget();
    panel.innerHTML = `<h3>Exclude</h3>${noEditable}${brushControls(["cut", "restore"], state.exclude.mode)}
      <h4>Cave model</h4>
      <p class="hint">${target ? `<b>${escape(target.userData.record.file)}</b>` : "None: Shift+click a cave (or any model) to cut the ground inside it."}</p>
      <label class="check"><input id="exclude-inside" type="checkbox" ${state.exclude.onlyInside ? "checked" : ""} ${target ? "" : "disabled"}> Brush only cuts inside the model</label>
      <div class="row"><button id="exclude-all" ${target ? "" : "disabled"}>Cut all inside it</button><button id="restore-all" ${target ? "" : "disabled"}>Restore</button></div>
      <p class="hint">Cuts holes in the ground, so a cave's mouth and inside aren't buried under terrain; the client leaves holes out of the ground's mesh and collision. Restore puts ground back: as it was if it was cut here, else at its surroundings' height. Keys 1 cut, 2 restore.</p>`;
    bindBrush((value) => { state.exclude.mode = value; });
    panel.querySelector("#exclude-inside").addEventListener("change", (e) => { state.exclude.onlyInside = e.target.checked; });
    panel.querySelector("#exclude-all").addEventListener("click", () => cutInsideTarget(false));
    panel.querySelector("#restore-all").addEventListener("click", () => cutInsideTarget(true));
  } else if (state.tool === "eco") {
    panel.innerHTML = `<h3>Eco layers</h3>${noEditable}${brushControls()}
      <label class="check"><input id="eco-tint" type="checkbox" ${state.ecoTint ? "checked" : ""}> Also tint the colour map</label>
      <label>Tint amount <input id="eco-tint-amount" type="range" min="0" max="1" step="0.05" value="${state.ecoTintAmount}"></label>
      <div class="row"><span>Tint: ${state.ecoTintPick ? `<span class="swatch" style="background:${rgbHex(state.ecoTintPick)}"></span>picked ${rgbHex(state.ecoTintPick)}` : "the eco's own colour"}</span>${state.ecoTintPick ? `<button id="eco-tint-reset">Use the eco's</button>` : ""}</div>
      <p class="hint">Eyedropper: Alt+click (or I over the ground) picks the eco under the pointer and its tint, to paint more of the same.</p>
      <h4>In ${escape(zone.name)}</h4>
      <div class="list palette">${zone.ecos.map((e) => ecoRow(e, `data-eco="${e.index}"`, e.index === state.eco && !state.ecoPick)).join("")}</div>
      <h4>All ecos <span id="palette-count"></span></h4>
      <input id="palette-search" placeholder="Search: grass, sand, snow, bw_, cobblestone…" value="${escape(state.paletteQuery || "")}">
      <div class="list palette" id="palette-list"><div>Loading…</div></div>
      <p class="hint">Every eco the client has: those other zones use and its .eco definitions. Painting with one ${escape(zone.name)} doesn't have adds it to the zone's eco list, saved with the zone. A tile holds up to 5 ecos; past that the lightest are dropped on save.</p>`;
    bindBrush();
    panel.querySelector("#eco-tint").addEventListener("change", (e) => { state.ecoTint = e.target.checked; });
    panel.querySelector("#eco-tint-amount").addEventListener("input", (e) => { state.ecoTintAmount = +e.target.value; });
    panel.querySelector("#eco-tint-reset")?.addEventListener("click", () => { state.ecoTintPick = null; renderPanel(); });
    for (const row of panel.querySelectorAll("[data-eco]")) {
      row.addEventListener("click", () => { pickEco(+row.dataset.eco, null); renderPanel(); });
    }
    const search = panel.querySelector("#palette-search");
    const fill = async () => {
      const palette = await loadPalette();
      if (state.tool !== "eco") return;
      state.paletteQuery = search.value;
      const words = search.value.toLowerCase().split(/\s+/).filter(Boolean);
      const shown = palette.filter((p) => words.every((w) => `${p.name} ${p.texture} ${p.zones.join(" ")}`.toLowerCase().includes(w)));
      panel.querySelector("#palette-count").textContent = `(${shown.length} of ${palette.length})`;
      panel.querySelector("#palette-list").innerHTML = shown.map((p) => {
        const inZone = zone.ecos.find((e) => e.name.toLowerCase() === p.name.toLowerCase() && e.texture.toLowerCase() === p.texture.toLowerCase());
        const active = state.ecoPick?.key === p.key || (inZone && inZone.index === state.eco && !state.ecoPick);
        const used = p.zoneCount ? `${p.zoneCount} zone${p.zoneCount > 1 ? "s" : ""}` : `.eco ${p.definition}`;
        return ecoRow(p, `data-key="${escape(p.key)}"`, active, `${inZone ? "in zone · " : ""}${used}`);
      }).join("");
      for (const row of panel.querySelectorAll("[data-key]")) {
        row.addEventListener("click", () => {
          const entry = palette.find((p) => p.key === row.dataset.key);
          const inZone = zone.ecos.find((e) => e.name.toLowerCase() === entry.name.toLowerCase() && e.texture.toLowerCase() === entry.texture.toLowerCase());
          pickEco(inZone ? inZone.index : null, inZone ? null : entry);
          renderPanel();
        });
      }
    };
    let timer;
    search.addEventListener("input", () => { clearTimeout(timer); timer = setTimeout(fill, 150); });
    fill().catch((error) => toast(error.message, "error"));
  } else if (state.tool === "colour") {
    panel.innerHTML = `<h3>Colour map</h3>${noEditable}${brushControls()}
      <label>Colour <input id="paint-colour" type="color" value="${rgbHex(state.colour)}"></label>
      <p class="hint">Alt+click (or I) picks a colour from the ground. The colour map is the ground's base tint; eco detail textures draw over it in game.</p>`;
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
      <p class="hint">${state.actor ? `Placing <b>${escape(state.actor)}</b>: click the ground to place it, Shift+wheel turns it, Esc stops.` : "Pick an object in the content browser below, or drag one into the view."}</p>
      <div class="row"><button id="open-content">${contentOpen() ? "Hide" : "Show"} content browser (O)</button></div>
      <p class="hint">A placed object goes in the chunk under it. Shift+click selects instead of placing.</p>
      <div id="inspector"></div>`;
    panel.querySelector("#open-content").addEventListener("click", () => toggleContent());
    renderInspector();
  } else if (state.tool === "lights") {
    panel.innerHTML = `<h3>Point lights</h3><p class="hint">Click the ground to add a light 6 units above it, or click a light to edit it.</p><div id="inspector"></div>`;
    renderInspector();
  } else if (state.tool === "chunks") {
    const options = `<optgroup label="In ${escape(zone.name)}">` +
      zone.ecos.map((e) => `<option value="${e.index}" ${e.index === state.newChunk.eco && !state.newChunk.ecoKey ? "selected" : ""}>${escape(e.name)}</option>`).join("") +
      `</optgroup><optgroup label="All ecos (added to the zone)" id="chunk-palette"></optgroup>`;
    const envs = [{ id: 0, name: "None" }, ...zone.environments].map((e) => `<option value="${e.id}" ${e.id === state.newChunk.environment ? "selected" : ""}>${e.id} ${escape(e.name)}</option>`).join("");
    const b = zone.bounds;
    panel.innerHTML = `<h3>Chunks</h3>
      <p class="hint">Click an empty cell (in the view or on the minimap) to add a 256 x 256 chunk. Its edges join the chunks around it and the inside is smoothed between them; sculpt from there.</p>
      <p class="hint">The zone grows to fit new chunks (now x ${b.startX * 64}…${b.endX * 64}, z ${b.startZ * 64}…${b.endZ * 64}).</p>
      <label>Ground height where there's no neighbour <input id="chunk-height" type="number" step="1" value="${state.newChunk.height}"></label>
      <label>Eco <select id="chunk-eco">${options}</select></label>
      <label>Environment <select id="chunk-env">${envs}</select></label>`;
    panel.querySelector("#chunk-height").addEventListener("input", (e) => { state.newChunk.height = +e.target.value; });
    panel.querySelector("#chunk-eco").addEventListener("change", (e) => {
      const value = e.target.value;
      state.newChunk.ecoKey = value.startsWith("palette:") ? value.slice(8) : null;
      if (!state.newChunk.ecoKey) state.newChunk.eco = +value;
    });
    panel.querySelector("#chunk-env").addEventListener("change", (e) => { state.newChunk.environment = +e.target.value; });
    loadPalette().then((palette) => {
      const group = panel.querySelector("#chunk-palette");
      if (!group) return;
      const has = (p) => zone.ecos.some((e) => e.name.toLowerCase() === p.name.toLowerCase() && e.texture.toLowerCase() === p.texture.toLowerCase());
      group.innerHTML = palette.filter((p) => !has(p)).map((p) =>
        `<option value="palette:${escape(p.key)}" ${state.newChunk.ecoKey === p.key ? "selected" : ""}>${escape(p.name)} (${escape(p.texture)})</option>`).join("");
    }).catch(() => {});
  } else if (state.tool === "zone") {
    renderZonePanel();
  }
}

function renderZonePanel() {
  const zone = state.zone;
  const definition = zone.definition;
  const skies = zone.skies;
  const sky = state.sky !== undefined ? state.sky : definition?.Sky || "";
  panel.innerHTML = html`<h3>Zone</h3>
    <p class="hint">${escape(zone.name)} · ${escape(zoneSource(zone))} · ${zone.chunks.length} chunks · bounds x ${zone.bounds.startX * 64}…${zone.bounds.endX * 64}, z ${zone.bounds.startZ * 64}…${zone.bounds.endZ * 64}</p>
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
    <h4>Ecos</h4><p class="hint">${zone.ecos.length} in the zone's list. Any other eco in the client is in the Eco tool's palette (E) and the Chunks tool; it joins the list when you use it.</p>
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

// --- content browser -----------------------------------------------------------------------------------

function placeActor(name, hit, yaw = 0) {
  const chunk = editableChunkFor(hit.x, hit.z);
  if (!chunk) return toast("No chunk is loaded here. Add one with the Chunks tool (K).");
  addRecord(chunk, "prop", { file: name, position: [hit.x, hit.y, hit.z], rotation: [yaw, 0, 0], scale: 1 });
  content.placed(name);
}

// How often each actor is placed in the loaded chunks, for the browser's "Used nearby".
function nearbyActors() {
  const counts = new Map();
  for (const chunk of terrain.chunks.values()) {
    for (const record of chunk.objects) counts.set(record.file, (counts.get(record.file) || 0) + 1);
  }
  return counts;
}

const content = new ContentBrowser($("#content-browser"), library, {
  pick: (name) => {
    state.actor = name;
    if (state.tool !== "props") setTool("props");
    else { makeGhost(); renderPanel(); }
  },
  nearby: nearbyActors,
});
let contentLoaded = false;

function contentOpen() {
  return !$("#content-browser").hidden;
}

function toggleContent(open = !contentOpen()) {
  $("#content-browser").hidden = !open;
  $("#viewport").classList.toggle("content-open", open);
  $("#toggle-content").classList.toggle("active", open);
  if (open && !contentLoaded) {
    contentLoaded = true;
    content.load().catch((error) => { contentLoaded = false; toast(error.message, "error"); });
  } else if (open) content.render();
  if (state.tool === "props") renderPanel();
}
$("#toggle-content").addEventListener("click", () => toggleContent());

// Dragging a tile picks it (so the preview follows the pointer); dropping it on the ground places it.
canvas.addEventListener("dragover", (event) => {
  if (!event.dataTransfer.types.includes("text/x-zone-actor")) return;
  event.preventDefault();
  event.dataTransfer.dropEffect = "copy";
  updatePointer(event);
  pointer.inside = true;
});
canvas.addEventListener("drop", (event) => {
  const name = event.dataTransfer.getData("text/x-zone-actor");
  if (!name) return;
  event.preventDefault();
  updatePointer(event);
  const hit = terrainHit();
  if (!hit) return toast("Drop it on the ground.");
  placeActor(name, hit, ghost ? ghost.rotation.y : 0);
});

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
    look.yaw -= event.movementX * 0.003;
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
    if (event.altKey && ["sculpt", "eco", "colour"].includes(tool)) return eyedrop(tool);
    if (tool === "exclude" && event.shiftKey) {
      raycaster.setFromCamera(pointer.ndc, camera);
      const propHit = props.pick(raycaster);
      if (propHit?.object.userData.kind !== "prop") return toast("No model under the pointer.");
      state.exclude.target = propHit.object;
      return renderPanel();
    }
    if (tool === "eco" && state.ecoPick) {
      try { await ensureEco(); } catch (error) { return toast(error.message, "error"); }
    }
    const hit = terrainHit();
    stroke = { snapshot: new Map(), target: hit?.y ?? 0, last: performance.now() };
    if (tool === "sculpt" && hit && state.brush.mode === "slope") stroke.plane = terrain.fitPlane(hit.x, hit.z, state.brush.radius);
    if (tool === "sculpt" && hit && state.brush.mode === "ramp") stroke.start = { x: hit.x, y: hit.y, z: hit.z };
    if (tool === "exclude") stroke.inside = excludeTarget() && state.exclude.onlyInside ? insideTest(excludeTarget()) : null;
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
    if (hit) placeActor(state.actor, hit, ghost ? ghost.rotation.y : 0);
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
  if (stroke?.start && hover) {
    const { radius, strength } = state.brush;
    const end = { x: hover.x, y: hover.y, z: hover.z };
    snapshotChunks(terrain.editableChunksNear((stroke.start.x + end.x) / 2, (stroke.start.z + end.z) / 2,
      Math.hypot(end.x - stroke.start.x, end.z - stroke.start.z) / 2 + radius));
    terrain.ramp(stroke.start, end, radius, strength);
    updateDirty();
  }
  rampLine.visible = false;
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
  const tools = { v: "select", b: "sculpt", x: "exclude", e: "eco", c: "colour", n: "environment", p: "props", l: "lights", k: "chunks", z: "zone" };
  const digit = "1234567890".indexOf(key);
  if (digit >= 0 && state.tool === "sculpt" && SCULPT_MODES[digit]) { state.brush.mode = SCULPT_MODES[digit]; renderPanel(); }
  else if (digit >= 0 && digit < 2 && state.tool === "exclude") { state.exclude.mode = ["cut", "restore"][digit]; renderPanel(); }
  else if (key === "i" && state.zone) eyedrop();
  else if (key === "escape") { select(null); state.actor = null; if (ghost) { scene.remove(ghost); ghost = null; } renderPanel(); }
  else if (key === "delete" || key === "backspace") deleteSelected();
  else if (key === "o") toggleContent();
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
      heights: state.tool === "sculpt" || state.tool === "exclude" ? chunk.heights.slice() : null,
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
    if (mode === "ramp") {
      // Applied on release; meanwhile a line shows where it goes.
      if (stroke.start) setRampLine(stroke.start, hover);
      return;
    }
    const target = mode === "level" ? (state.brush.level ?? 0) : stroke.target;
    terrain.sculpt(m, hover.x, hover.z, radius, strength, dt, { target, plane: stroke.plane, step: state.brush.step });
  } else if (state.tool === "exclude") {
    terrain.exclude(hover.x, hover.z, radius, state.exclude.mode === "restore", stroke.inside);
  } else if (state.tool === "eco" && state.eco !== null) {
    const tint = state.ecoTint ? state.ecoTintPick ?? state.ecoColours.get(state.eco) : null;
    terrain.paintEco(state.eco, hover.x, hover.z, radius, strength, dt, tint, state.ecoTintAmount);
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
      if (saved.heights) { chunk.heights.set(saved.heights); chunk.updateIndex(); chunk.updatePositions(); chunk.updateNormals(); }
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
  try {
    if (state.newChunk.ecoKey) {
      const eco = await postJson("/api/zone/eco", { zone: state.zone.id, palette: state.newChunk.ecoKey });
      if (!state.zone.ecos.some((e) => e.index === eco.index)) state.zone.ecos.push(eco);
      state.zoneDirty = true;
      state.newChunk.eco = eco.index;
      state.newChunk.ecoKey = null;
    }
    if (state.newChunk.eco === null) return toast("Pick an eco for the new chunk in the Chunks tool first.");
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
    // The zone's eco list as this page knows it, so a restarted server can re-add palette ecos it lost.
    const ecos = state.zone.ecos.map(({ index, name, texture }) => ({ index, name, texture }));
    const result = await save({ zone: state.zone.id, chunks: entries, settings, ecos }, blobs);
    const files = [...result.chunks.flatMap((c) => c.files), ...result.zone.files];
    const unique = [...new Set(files)];
    const zone = result.zone;
    if (zone.serverData) $("#play-servers").checked = true;
    const unlock = zone.unlock;
    const notes = [
      zone.mirrored.length ? `Copied ${zone.mirrored.length} into the running servers' folders; the asset server serves them now.` : "",
      zone.overrides.length && unlock?.applied ? `Hid the client's stock copy of ${zone.overrides.length} file(s), so it downloads yours.` : "",
      zone.overrides.length && !unlock?.applied ? "The client is running, so it still reads its stock copies: Test in game closes it and fixes that." : "",
      zone.serverData ? "Zone or house data changed: Test in game with + servers to load it." : "Test in game restarts the client to load it.",
    ].filter(Boolean);
    toast(`Saved ${unique.length} file(s):\n${unique.slice(0, 12).map((f) => f.split(/[\\/]/).slice(-2).join("/")).join("\n")}${unique.length > 12 ? "\n…" : ""}\n${notes.join("\n")}`, "ok");
    await autosaving;
    await clearDraft(state.zone.id);
    const position = camera.position.clone(), yaw = look.yaw, pitch = look.pitch;
    await openZone(state.zone.id, true);
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

// --- autosave ------------------------------------------------------------------------------------------

// Chunks edited since the last autosave go to the server's drafts folder (.cache/drafts/<zone>) every
// AUTOSAVE_SECONDS and when the tab is hidden, so a crash loses at most that much. Saving clears the draft;
// opening the zone again offers to restore it.
const AUTOSAVE_SECONDS = 15;
let autosaving = null;     // the autosave in flight, which saveAll waits for before clearing the draft
let zoneAutosaved = "";    // the zone settings last autosaved, as JSON
const drafted = new Set(); // chunk keys in the draft, so one dropped since (an undone new chunk) can be voided

function autosave() {
  if (!autosaving && state.zone && !$("#save").disabled) autosaving = writeDraft().finally(() => { autosaving = null; });
  return autosaving;
}

async function writeDraft() {
  const zoneId = state.zone.id;
  const query = `?zone=${encodeURIComponent(zoneId)}`;
  const chunks = [...terrain.chunks.values()].filter((c) => c.edits !== c.autosaved);
  const zone = state.zoneDirty ? JSON.stringify({
    settings: state.settings, sky: state.sky, customSky: state.customSky, houses: state.houses,
    ecos: state.zone.ecos, zoneDirty: state.zoneDirty,
  }) : "";
  const dropped = [...drafted].filter((key) => !terrain.chunks.has(key));
  if (!chunks.length && !dropped.length && (!zone || zone === zoneAutosaved)) return;
  // The page's own records carry their scene objects; the draft keeps the data.
  const plain = ({ object, ...record }) => record;
  const post = async (body) => {
    const response = await fetch(`/api/draft/chunk${query}`, { method: "POST", body });
    if (!response.ok) throw new Error((await response.json()).error);
  };
  try {
    for (const key of dropped) {
      // A draft chunk with nothing dirty leaves the draft.
      const [row, col] = key.split("_").map(Number);
      await post(frameBody({ row, col, dirty: {} }, []));
      drafted.delete(key);
    }
    for (const chunk of chunks) {
      const edits = chunk.edits;
      const meta = {
        row: chunk.row, col: chunk.col, version: chunk.version, isNew: chunk.isNew, editable: chunk.editable,
        ecos: chunk.ecos, tiles: chunk.tiles, tileDirty: chunk.tileDirty, dirty: { ...chunk.dirty },
        objects: chunk.objects.map(plain), lights: chunk.lights.map(plain), colourSize: chunk.colourSize,
      };
      await post(frameBody(meta, [chunk.heights, chunk.weights, chunk.colour]));
      chunk.autosaved = edits;
      drafted.add(chunk.key);
    }
    if (zone && zone !== zoneAutosaved) {
      await postJson(`/api/draft/zone${query}`, JSON.parse(zone));
      zoneAutosaved = zone;
    }
    $("#status-autosave").textContent = `autosaved ${new Date().toLocaleTimeString()}`;
  } catch (error) {
    $("#status-autosave").textContent = `autosave failed: ${error.message}`;
  }
}

function clearDraft(zone) {
  zoneAutosaved = "";
  drafted.clear();
  $("#status-autosave").textContent = "";
  return postJson(`/api/draft/clear?zone=${encodeURIComponent(zone)}`, {}).catch(() => {});
}

// After opening a zone: if an autosave of it outlived the page that wrote it, offer to restore it.
async function offerDraft() {
  const zone = state.zone, query = `zone=${encodeURIComponent(zone.id)}`;
  const draft = await getJson(`/api/draft?${query}`).catch(() => null);
  if (!draft || state.zone !== zone) return;
  const what = [draft.chunks.length ? `${draft.chunks.length} chunk(s)` : "", draft.zone ? "zone settings" : ""].filter(Boolean).join(" and ");
  const when = new Date(draft.time * 1000).toLocaleString();
  if (!what || !confirm(`${zone.name} has unsaved edits from ${when} (${what}), autosaved before the editor closed.\n\nRestore them? Cancel discards them.`)) {
    return clearDraft(zone.id);
  }
  const settings = draft.zone;
  if (settings) {
    state.settings = settings.settings || {};
    state.sky = settings.sky;
    state.customSky = settings.customSky || null;
    if (settings.houses) state.houses = settings.houses;
    if (settings.ecos) zone.ecos = settings.ecos;
    state.zoneDirty = true;
    zoneAutosaved = JSON.stringify(settings);
    buildMarkers();
    if (state.customSky) updateLightingFromSky(null, state.customSky.xml);
    else if (state.sky) updateLightingFromSky(state.sky);
  }
  let restored = 0;
  for (const { row, col } of draft.chunks) {
    const response = await fetch(`/api/draft/chunk?${query}&row=${row}&col=${col}`);
    if (!response.ok || state.zone !== zone) continue;
    const { meta, blobs } = unframe(await response.arrayBuffer());
    if (!Object.values(meta.dirty || {}).some(Boolean)) continue;
    const key = `${row}_${col}`;
    if (!terrain.cells.has(key)) terrain.cells.set(key, { row, col, editable: true, source: "new" });
    const chunk = new TerrainChunk(terrain, row, col, {
      editable: meta.editable, isNew: meta.isNew, version: meta.version, heights: new Float32Array(blobs[0]),
      ecos: meta.ecos, weights: new Uint8Array(blobs[1]), tiles: meta.tiles, objects: meta.objects, lights: meta.lights,
      colour: new Uint8Array(blobs[2]), colourSize: meta.colourSize, dirty: meta.dirty,
    });
    chunk.tileDirty = meta.tileDirty;
    chunk.autosaved = chunk.edits;
    terrain.add(chunk);
    drafted.add(key);
    restored++;
  }
  setTool(state.tool);
  updateDirty();
  toast(`Restored ${restored} chunk(s)${settings ? " and the zone settings" : ""} from the autosave. Save to keep them.`, "ok");
}

setInterval(autosave, AUTOSAVE_SECONDS * 1000);
document.addEventListener("visibilitychange", () => { if (document.hidden) autosave(); });

// --- minimap -------------------------------------------------------------------------------------------

// The area the minimap covers, in tiles: the zone's bounds and every chunk, plus a ring of empty cells
// around it to grow the zone into.
function minimapArea() {
  const b = state.zone.bounds;
  let [x0, z0, x1, z1] = [b.startX, b.startZ, b.endX, b.endZ];
  for (const c of terrain.cells.values()) {
    x0 = Math.min(x0, c.col); z0 = Math.min(z0, c.row);
    x1 = Math.max(x1, c.col + 4); z1 = Math.max(z1, c.row + 4);
  }
  return { x0: x0 - 4, z0: z0 - 4, x1: x1 + 4, z1: z1 + 4 };
}

function minimapTransform() {
  const a = minimapArea();
  const size = 220, pad = 8;
  const width = (a.x1 - a.x0) * 64, depth = (a.z1 - a.z0) * 64;
  const scale = (size - pad * 2) / Math.max(width, depth);
  // As the view looks from above with +z ahead: +x to the left, +z up.
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
  camera.rotation.set(look.pitch, look.yaw, 0, "YXZ");
  const forward = new THREE.Vector3(), right = new THREE.Vector3();
  camera.getWorldDirection(forward);
  right.crossVectors(forward, camera.up).normalize();
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
      const [eco] = chunk ? terrain.sampleEcos(hover.x, hover.z) : [];
      $("#status-cell").textContent = chunk
        ? `chunk ${chunk.key}${chunk.isNew ? " (new)" : ""} · tile ${tile} env ${chunk.tiles[tile]?.environment} · ground ${hover.y.toFixed(1)}` +
          (eco ? ` · ${ecoName(eco.index)} ${Math.round(eco.weight * 100)}%` : "")
        : "";
    }
    $("#status-pos").textContent = `x ${camera.position.x.toFixed(0)}  y ${camera.position.y.toFixed(0)}  z ${camera.position.z.toFixed(0)}`;
    minimapTimer += dt;
    if (minimapTimer > 0.25) { minimapTimer = 0; drawMinimap(); updatePointLights(); updateEnvironmentSky(); }
  }
  // The shadowed area follows the ground ahead of the camera, snapped to shadow texels so it doesn't shimmer.
  const focus = camera.position.clone().addScaledVector(new THREE.Vector3(forward.x, 0, forward.z).normalize(), 120);
  const texel = (sun.shadow.camera.right - sun.shadow.camera.left) / sun.shadow.mapSize.x;
  focus.set(Math.round(focus.x / texel) * texel, terrain.heightAt(focus.x, focus.z) ?? camera.position.y - 50, Math.round(focus.z / texel) * texel);
  sun.target.position.copy(focus);
  sun.position.copy(focus).addScaledVector(sunDirection, 700);
  skyDome.position.copy(camera.position);
  renderer.render(scene, camera);
  requestAnimationFrame(frame);
}

// --- top bar -------------------------------------------------------------------------------------------

for (const button of document.querySelectorAll("#toolbar button")) button.addEventListener("click", () => setTool(button.dataset.tool));
$("#open-zone").addEventListener("click", () => openZone($("#zone-search").value.trim()).then(buildWalls));
$("#open-file").addEventListener("click", openZoneFile);
$("#zone-search").addEventListener("keydown", (e) => { if (e.key === "Enter") openZone(e.target.value.trim()).then(buildWalls); });
$("#save").addEventListener("click", saveAll);
$("#play").addEventListener("click", async () => {
  if (dirtyChunks().length || state.zoneDirty) await saveAll();
  const servers = $("#play-servers").checked;
  $("#play").disabled = true;
  toast(servers ? "Restarting the servers and the client…" : "Restarting the client…");
  try {
    const result = await postJson("/api/play", { servers });
    const unlock = result.unlock?.applied
      ? (result.unlock.hidden.length ? `\nHid ${result.unlock.hidden.length} stock file(s) from the client's packs.` : "")
      : `\n${result.unlock?.message || ""}`;
    toast(`${result.ok ? (servers ? "Servers and client restarted." : "Client restarted.") : "launch.ps1 failed:"}${unlock}\n${result.output.split("\n").slice(-4).join("\n")}`, result.ok ? "ok" : "error");
    if (result.ok && servers) $("#play-servers").checked = false;
  } catch (error) {
    toast(`Test in game failed: ${error.message}`, "error");
  } finally {
    $("#play").disabled = false;
  }
});
$("#undo").addEventListener("click", undo);
$("#view-radius").addEventListener("input", (e) => { terrain.radius = +e.target.value; });
$("#time-of-day").addEventListener("input", (e) => {
  state.time = +e.target.value;
  $("#time-label").textContent = `${String(Math.floor(state.time)).padStart(2, "0")}:${String(Math.round(state.time % 1 * 60)).padStart(2, "0")}`;
  applyTimeOfDay();
});
$("#show-details").addEventListener("change", (e) => { terrain.uniforms.detailStrength.value = e.target.checked ? 1 : 0; });
$("#show-shadows").addEventListener("change", (e) => { sun.castShadow = e.target.checked; });
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
    const palette = await loadPalette();
    form.eco.innerHTML = palette.map((p) => `<option value="${escape(p.key)}" ${p.key === "sg_grass|sg_grass.dds" ? "selected" : ""}>${escape(p.name)} (${escape(p.texture)})</option>`).join("");
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
      ecoKey: form.eco.value || null,
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
