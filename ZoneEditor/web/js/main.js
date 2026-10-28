// Sanctuary zone editor: the viewport, tools and panels.
import * as THREE from "three";
import { TransformControls } from "three/addons/controls/TransformControls.js";
import { ASSET_VERSION, frame as frameBody, getJson, postJson, save, unframe } from "./api.js";
import { CHUNK, FALLOFFS, STEP, TILE, TerrainChunk, TerrainManager, environmentColour } from "./terrain.js";
import { PropLayer, PropLibrary, applyTransform, makeMarker, readTransform, variantNames } from "./props.js";
import { ContentBrowser } from "./content.js";
import { LiveLayer } from "./live.js";
import { ScriptEditor } from "./script-editor.js";
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
  onChunkUnloaded: (chunk) => {
    props.removeChunk(chunk);
    const gone = [...selection].filter((o) => o.userData.chunk === chunk);
    if (!gone.length && selected?.userData.chunk !== chunk) return;
    for (const object of gone) selection.delete(object);
    if (selected?.userData.chunk === chunk) selected = [...selection].at(-1) ?? null;
    selectionChanged();
  },
  status,
});
terrain.anisotropy = renderer.capabilities.getMaxAnisotropy();
const library = new PropLibrary();
const props = new PropLayer(scene, library);

// The running game's players and placed objects, when the editor is opened from the admin panel (live.js).
const live = new LiveLayer(scene, library, {
  toast,
  status: updateLiveStatus,
  changed: (object) => { if (selected === object) { attachGizmo(); renderInspector(); } },
  removed: (object) => { if (selected === object) select(null); },
});
// A live object's GM script: the inspector's Script section opens it.
const scriptEditor = new ScriptEditor($("#script-editor"), live, {
  closed: () => { if (selected?.userData.kind === "live") { delete selected.userData.script; renderInspector(); } },
});

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
  // New objects: one at a time or scattered under a brush, with a random turn and scale, stood on the slope,
  // raised by offset, and given a texture variant and tint where their actor has them.
  // live places into the running game when the editor is opened from the admin panel (see live.js).
  place: {
    mode: "single", randomYaw: false, align: false, scaleMin: 1, scaleMax: 1, offset: 0,
    material: "", tintOn: false, tint: "#ffffff", spacing: 6, set: [], live: true,
  },
  // The gizmo's snapping: metres, degrees and scale steps (0 is off).
  snap: { move: 0, turn: 0, scale: 0 },
  recentTints: [],
  findQuery: "",
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
let selected = null;           // the object the inspector shows and the gizmo holds (the last one picked)
const selection = new Set();   // every selected prop and light; a marker is selected alone
const selectionBoxes = new Map();
const pivot = new THREE.Object3D();   // the gizmo's handle on several objects at once
scene.add(pivot);
let ghost = null;
const markers = [];
const undoStack = [];

const pointer = { x: 0, y: 0, ndc: new THREE.Vector2(), inside: false, buttons: 0 };
const look = { yaw: 0, pitch: -0.35, speed: 60 };
// Keys held down, by their place on the keyboard (KeyW, ShiftLeft), so a layout, Shift or Caps Lock can't leave
// one held: its keyup has the same code. All of them are let go whenever the page loses the keyboard (see input).
const held = new Set();
const isHeld = (code) => held.has(code);
const shiftHeld = () => held.has("ShiftLeft") || held.has("ShiftRight");
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
  const data = await getJson("api/zones");
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
    zone = await getJson(`api/zone?name=${encodeURIComponent(name)}`);
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
  if (!keepCamera) live.open(zone.name);
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
  updateTransformBar();
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
    const { path } = await postJson("api/pick-zone", { start });
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
    if (!xml) xml = await (await fetch(`api/sky?name=${encodeURIComponent(skyName)}`)).text();
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
    state.ecoColours.set(index, await getJson(`api/eco-colour?zone=${encodeURIComponent(state.zone.id)}&index=${index}`));
  }
  return state.ecoColours.get(index);
}

// A palette row: the eco's detail texture as a swatch, its name, and a note (by default its texture and flora).
function ecoRow(eco, attributes, active, note) {
  const text = note ?? `${eco.texture}${eco.floras.length ? ` · ${eco.floras.length} flora` : ""}`;
  const used = eco.zones?.length ? `\nUsed in ${eco.zones.join(", ")}${eco.zoneCount > eco.zones.length ? ", …" : ""}` : "";
  return `<div ${attributes} class="${active ? "active" : ""}" title="${escape(eco.texture + used)}">` +
    `<img loading="lazy" alt="" src="api/texture?name=${encodeURIComponent(eco.texture)}&size=48&opaque=1&v=${ASSET_VERSION}">` +
    `${escape(eco.name)} <small>${escape(text)}</small></div>`;
}

// Every eco the client has (zones' lists and .eco definitions), fetched once.
let palettePromise = null;
function loadPalette() {
  palettePromise ??= getJson("api/ecos").catch((error) => { palettePromise = null; throw error; });
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
  const eco = await postJson("api/zone/eco", { zone: state.zone.id, palette: state.ecoPick.key });
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
    const hidden = [...props.hiddenFiles];
    panel.innerHTML = `<h3>Select</h3>
      <p class="hint">Click a prop, light or marker; Shift+click adds or removes one; drag over the ground to box-select (Shift adds).
        The bar over the view moves, turns and scales it (G, R, T), with snapping; hold Shift while dragging to snap.
        Arrows and PgUp/PgDn nudge it, , and . turn it, - and = scale it. Keys ? lists every key.</p>
      ${hidden.length ? `<p class="hint">Hidden: ${hidden.map(escape).join(", ")}</p><div class="row"><button id="show-all">Show them again (Shift+H)</button></div>` : ""}
      <div id="inspector"></div>
      <h4>Find objects</h4>
      <input id="find-objects" placeholder="Name: rock, tree_giant, hsg_block, a player or NPC…" value="${escape(state.findQuery)}">
      <div class="list" id="found-objects"></div>
      <div class="row"><button id="select-found" disabled>Select all found</button></div>`;
    panel.querySelector("#show-all")?.addEventListener("click", () => { props.showAll(); renderPanel(); });
    const find = panel.querySelector("#find-objects");
    let timer;
    find.addEventListener("input", () => { clearTimeout(timer); timer = setTimeout(() => { state.findQuery = find.value; listFound(); }, 150); });
    panel.querySelector("#select-found").addEventListener("click", () => selectMany(foundObjects()));
    listFound();
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
    const p = state.place;
    const scatter = p.mode === "scatter";
    const short = (file) => escape(file.replace(/\.(adr|agr)$/i, ""));
    panel.innerHTML = `<h3>Props</h3>
      <p class="hint">${state.actor ? `Picked <b>${escape(state.actor)}</b>. ` : "Pick an object in the content browser below, or drag one into the view. "}${scatter
        ? "Hold the left button to scatter under the brush; Shift+drag erases those models there."
        : "Click the ground to place it, Shift+wheel turns it, Esc stops; Shift+click selects instead."}</p>
      <div class="row"><button id="open-content">${contentOpen() ? "Hide" : "Show"} content browser (O)</button></div>
      <div class="seg">${[["single", "Place one"], ["scatter", "Scatter"]].map(([m, label]) => `<button data-place-mode="${m}" class="${p.mode === m ? "active" : ""}">${label}</button>`).join("")}</div>
      ${scatter ? `${brushControls()}
        <label>Spacing (units between them) <input id="scatter-spacing" type="number" min="0.5" step="0.5" value="${p.spacing}"></label>
        <h4>Scatter set</h4>
        <div class="chips">${p.set.length ? p.set.map((f, i) => `<span class="chip">${short(f)} <b data-unset="${i}" title="Remove">×</b></span>`).join("") : `<span class="hint">Empty: the content pick is scattered.</span>`}</div>
        <div class="row"><button id="scatter-add" ${state.actor ? "" : "disabled"}>Add ${state.actor ? short(state.actor) : "the content pick"}</button></div>` : ""}
      <h4>New objects</h4>
      ${live.zone && !scatter ? `<label class="check" title="A placed model in the running game, there for players at once; or untick to put it in the zone's files"><input id="place-live" type="checkbox" ${p.live ? "checked" : ""}> <span class="live-badge">LIVE</span> Place in the running game</label>
        ${p.live ? `<p class="hint">Only models with a Models.txt row can be spawned (${live.spawnable.size} of them). Select one later and Bake it into the zone to make it part of the zone's files.</p>` : ""}` : ""}
      <label class="check"><input id="place-turn" type="checkbox" ${p.randomYaw ? "checked" : ""}> Random turn</label>
      <label class="check"><input id="place-align" type="checkbox" ${p.align ? "checked" : ""}> Stand on the slope</label>
      <div class="row"><label>Scale from <input id="place-scale-min" type="number" min="0.05" step="0.05" value="${p.scaleMin}"></label>
        <label>to <input id="place-scale-max" type="number" min="0.05" step="0.05" value="${p.scaleMax}"></label></div>
      <label>Height above the ground (negative sinks them) <input id="place-offset" type="number" step="0.1" value="${p.offset}"></label>
      <div id="place-look"></div>
      <div id="inspector"></div>`;
    panel.querySelector("#open-content").addEventListener("click", () => toggleContent());
    for (const button of panel.querySelectorAll("[data-place-mode]")) {
      button.addEventListener("click", () => { p.mode = button.dataset.placeMode; makeGhost(); renderPanel(); });
    }
    if (scatter) {
      bindBrush();
      panel.querySelector("#scatter-spacing").addEventListener("input", (e) => { p.spacing = Math.max(0.5, +e.target.value); });
      panel.querySelector("#scatter-add").addEventListener("click", () => { if (!p.set.includes(state.actor)) p.set.push(state.actor); renderPanel(); });
      for (const remove of panel.querySelectorAll("[data-unset]")) remove.addEventListener("click", () => { p.set.splice(+remove.dataset.unset, 1); renderPanel(); });
    }
    panel.querySelector("#place-live")?.addEventListener("change", (e) => { p.live = e.target.checked; renderPanel(); });
    panel.querySelector("#place-turn").addEventListener("change", (e) => { p.randomYaw = e.target.checked; });
    panel.querySelector("#place-align").addEventListener("change", (e) => { p.align = e.target.checked; });
    panel.querySelector("#place-scale-min").addEventListener("input", (e) => { p.scaleMin = Math.max(0.05, +e.target.value); });
    panel.querySelector("#place-scale-max").addEventListener("input", (e) => { p.scaleMax = Math.max(0.05, +e.target.value); });
    panel.querySelector("#place-offset").addEventListener("input", (e) => { p.offset = +e.target.value; });
    renderPlaceLook();
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
    const xml = await (await fetch(`api/sky?name=${encodeURIComponent(base)}`)).text();
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
  if (kind === "live") return renderLiveInspector(target);
  if (kind === "live-player") return renderLivePlayerInspector(target);
  if (kind === "live-npc") return renderLiveNpcInspector(target);
  if (kind === "marker") {
    target.innerHTML = `<h4>${escape(label)}</h4><div class="xyz">${["x", "y", "z"].map((a) => `<input data-pos="${a}" type="number" step="0.5" value="${selected.position[a].toFixed(2)}">`).join("")}</div>`;
  } else if (selection.size > 1) {
    return renderGroupInspector(target);
  } else if (kind === "prop") {
    const deg = record.rotation.map((r) => THREE.MathUtils.radToDeg(r).toFixed(1));
    target.innerHTML = html`<h4>${escape(record.file)}</h4>
      <p class="hint">Chunk ${chunk.key}${record.id ? ` · object ${record.id}` : " · new"}${locked ? " · stock (read-only)" : ""}</p>
      <label>Position</label><div class="xyz">${record.position.map((v, k) => `<input data-pos="${"xyz"[k]}" type="number" step="0.25" value="${v.toFixed(2)}" ${locked ? "disabled" : ""}>`).join("")}</div>
      <label>Yaw / pitch / roll (degrees)</label><div class="xyz">${deg.map((v, k) => `<input data-rot="${k}" type="number" step="5" value="${v}" ${locked ? "disabled" : ""}>`).join("")}</div>
      <label>Scale <input id="prop-scale" type="number" step="0.05" min="0.01" value="${record.scale.toFixed(3)}" ${locked ? "disabled" : ""}></label>
      <div id="look"></div>
      ${drawDistanceControl([record])}
      ${locked ? `<div class="row"><button id="copy-editable">Copy into an editable chunk</button></div>` : objectButtons()}`;
    renderLook(target.querySelector("#look"), [selected]);
    bindObjectButtons(target);
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
  if (kind === "light") {
    target.querySelector("#duplicate")?.addEventListener("click", duplicateSelected);
    target.querySelector("#delete")?.addEventListener("click", deleteSelected);
  }
  target.querySelector("#copy-editable")?.addEventListener("click", () => {
    const destination = editableChunkFor(record.position[0], record.position[2]);
    if (!destination) return toast("No editable chunk is loaded nearby; add one with the Chunks tool.");
    const copy = { copyOf: record.uid, file: record.file, position: [...record.position], rotation: [...record.rotation], scale: record.scale };
    addRecord(destination, "prop", copy);
  });
}

// A live object: in the running game, so every change here shows in game at once and is saved on the server.
function renderLiveInspector(target) {
  const object = selected;
  const data = object.userData.live;
  const kinds = { prop: "furniture prop", npc: "world NPC", model: "placed model" };
  const deg = [data.h, data.pitch, data.roll].map((r) => THREE.MathUtils.radToDeg(r).toFixed(1));
  target.innerHTML = html`<h4>${escape(data.label)}</h4>
    <p class="hint"><span class="live-badge">LIVE</span> ${kinds[data.kind] ?? data.kind}${data.model ? ` · ${escape(data.model)}` : ""} · ${data.id}</p>
    <p class="hint">In the running game: changes show for players in view at once and are saved on the server. G, R and T move, turn and scale it.</p>
    <label>Position</label><div class="xyz">${[data.x, data.y, data.z].map((v, k) => `<input data-live-pos="${k}" type="number" step="0.25" value="${v.toFixed(2)}">`).join("")}</div>
    <label>Yaw / pitch / roll (degrees)</label><div class="xyz">${deg.map((v, k) => `<input data-live-rot="${k}" type="number" step="5" value="${v}">`).join("")}</div>
    <label>Scale <input id="live-scale" type="number" step="0.05" min="0.05" max="20" value="${data.scale.toFixed(3)}"></label>
    <div class="row"><button id="live-ground" title="End">Drop to ground</button><button id="live-focus" title="F">Focus</button></div>
    <div class="row"><button id="duplicate">Duplicate</button><button id="delete" class="danger">Delete</button></div>
    <div class="row"><button id="live-bake" ${data.model ? "" : "disabled"} title="Make it one of the zone's own props, in the chunk under it, and take it out of the game">Bake into the zone</button></div>
    <h4>Script</h4>
    <p class="hint" id="live-script-state">${scriptSummary(object.userData.script)}</p>
    <div class="row"><button id="live-script" title="Lua that makes it talk, move and react to players, as the GM panel's Script tab (admins)">Edit script…</button></div>`;
  target.querySelector("#live-script").addEventListener("click", () => scriptEditor.open(object));
  // What its script is, fetched once per selection (the feed redraws this often).
  if (!object.userData.script) {
    live.getScript(object)
      .then((script) => { object.userData.script = script; })
      .catch((error) => { object.userData.script = { error: error.message }; })
      .finally(() => { if (selected === object) $("#live-script-state").innerHTML = scriptSummary(object.userData.script); });
  }
  const send = () => liveStep(live.commit(object), "Updated in game.");
  for (const input of target.querySelectorAll("[data-live-pos]")) {
    input.addEventListener("change", () => { object.position.setComponent(+input.dataset.livePos, +input.value); send(); });
  }
  for (const input of target.querySelectorAll("[data-live-rot]")) {
    input.addEventListener("change", () => {
      object.rotation.reorder("YXZ");
      object.rotation[["y", "x", "z"][+input.dataset.liveRot]] = THREE.MathUtils.degToRad(+input.value);
      send();
    });
  }
  target.querySelector("#live-scale").addEventListener("change", (e) => { object.scale.setScalar(Math.min(20, Math.max(0.05, +e.target.value))); send(); });
  target.querySelector("#live-ground").addEventListener("click", () => dropLiveToGround(object));
  target.querySelector("#live-focus").addEventListener("click", () => focusOn(object, LIVE_FOCUS));
  target.querySelector("#duplicate").addEventListener("click", duplicateSelected);
  target.querySelector("#delete").addEventListener("click", deleteSelected);
  target.querySelector("#live-bake").addEventListener("click", () => bakeLive(object));
}

function scriptSummary(script) {
  if (!script) return "Checking…";
  if (script.error) return escape(script.error);
  if (!script.source) return "None. A script makes it talk, move and react to players.";
  return script.running ? "A script is running on it." : '<span class="warn">It has a script that isn\'t running: it failed to compile when it spawned.</span>';
}

// A player in the running game: where they are, and what a referee can do about them from here.
function renderLivePlayerInspector(target) {
  const player = selected;
  const data = player.userData.live;
  target.innerHTML = html`<h4>${escape(data.name)}</h4>
    <p class="hint"><span class="live-badge">LIVE</span> player · ${data.x.toFixed(1)}, ${data.y.toFixed(1)}, ${data.z.toFixed(1)}</p>
    <p class="hint">${escape(live.lookFiles(player).join(", "))}</p>
    <div class="row"><button id="live-send" title="To the ground in the middle of the view">Send to the middle of the view</button></div>
    <label>Message <input id="live-message" maxlength="500" placeholder="Shows in their chat, from you"></label>
    <div class="row"><button id="live-message-send">Send message</button><button id="live-focus">Focus</button><button id="live-kick" class="danger">Kick</button></div>`;
  target.querySelector("#live-send").addEventListener("click", async () => {
    const hit = centreHit();
    if (!hit) return toast("There's no ground in the middle of the view.");
    if (await live.teleport(player, hit)) status(`Sent ${data.name} there.`);
  });
  target.querySelector("#live-message-send").addEventListener("click", async () => {
    const input = target.querySelector("#live-message");
    if (!input.value.trim()) return;
    if (await live.message(player, input.value.trim())) { status(`Sent to ${data.name}.`); input.value = ""; }
  });
  target.querySelector("#live-focus").addEventListener("click", () => focusOn(player, LIVE_FOCUS));
  target.querySelector("#live-kick").addEventListener("click", async () => {
    if (confirm(`Kick ${data.name} from the server?`) && await live.kick(player)) status(`Kicked ${data.name}.`);
  });
}

// One of the zone's own NPCs, mobs or collection nodes: shown as the game has them, edited in their data files.
function renderLiveNpcInspector(target) {
  const npc = selected;
  const data = npc.userData.live;
  const kinds = { npc: "NPC", mob: "mob", node: "collection node", coin: "collection coin" };
  target.innerHTML = html`<h4>${escape(data.label)}</h4>
    <p class="hint"><span class="live-badge">LIVE</span> zone ${kinds[data.kind] ?? data.kind} · ${data.id}</p>
    <p class="hint">${escape(live.lookFiles(npc).join(", "))}</p>
    <p class="hint">The zone's own NPCs come from its data files (Npcs.json, Mobs.json, the collection pools); spawn a copy with
      Place live to have one to edit here.</p>
    <div class="row"><button id="live-focus">Focus</button></div>`;
  target.querySelector("#live-focus").addEventListener("click", () => focusOn(npc, LIVE_FOCUS));
}

function dropLiveToGround(object) {
  const ground = terrain.heightAt(object.position.x, object.position.z);
  if (ground === null || ground === undefined) return toast("There's no loaded ground under it.");
  object.position.y = ground;
  liveStep(live.commit(object), "Dropped to the ground in game.");
}

// Several objects: what they are, their look and draw distance together, and the actions on all of them.
function renderGroupInspector(target) {
  const objects = selectedObjects();
  const propsSelected = objects.filter((o) => o.userData.kind === "prop");
  const counts = new Map();
  for (const object of objects) {
    const name = object.userData.kind === "light" ? "point light" : object.userData.record.file;
    counts.set(name, (counts.get(name) || 0) + 1);
  }
  const summary = [...counts].sort((a, b) => b[1] - a[1]);
  target.innerHTML = html`<h4>${objects.length} objects selected</h4>
    <p class="hint">${summary.slice(0, 5).map(([name, n]) => `${escape(name)} × ${n}`).join("<br>")}${summary.length > 5 ? `<br>and ${summary.length - 5} more models` : ""}</p>
    <p class="hint">The gizmo moves them together; R turns and T scales them round their centre.</p>
    ${propsSelected.length ? `<div id="look"></div>${drawDistanceControl(propsSelected.map((o) => o.userData.record))}` : ""}
    ${objectButtons()}
    <div class="row"><button id="deselect">Deselect all</button></div>`;
  if (propsSelected.length) renderLook(target.querySelector("#look"), propsSelected);
  bindObjectButtons(target);
  target.querySelector("#deselect").addEventListener("click", () => select(null));
}

function drawDistanceControl(records) {
  const values = new Set(records.map((r) => r.drawDistance ?? null));
  const value = values.size === 1 ? [...values][0] : undefined;
  return `<label>Draw distance <small>(0: the client's default${value === null ? "; this one's stock value isn't a distance" : ""})</small>
    <input id="draw-distance" type="number" min="0" step="25" value="${value ?? ""}" placeholder="${value === undefined ? "mixed" : ""}"></label>`;
}

function objectButtons() {
  return `<h4>Arrange</h4>
    <div class="seg">${Object.entries(OBJECT_ACTIONS).map(([key, [label]]) => `<button data-action="${key}">${label}</button>`).join("")}</div>
    <div class="row"><button id="select-same">Select all like this</button><button id="hide-model" title="H; Shift+H shows them again">Hide this model</button></div>
    <div class="row"><button id="replace-model" ${state.actor ? "" : "disabled"} title="Swap the model for the content browser's pick, keeping place, turn and scale">Replace with ${state.actor ? escape(state.actor.replace(/\.(adr|agr)$/i, "")) : "the content pick"}</button></div>
    <div class="row"><button id="duplicate">Duplicate</button><button id="copy" title="Ctrl+C; Ctrl+V pastes at the pointer">Copy</button><button id="delete" class="danger">Delete</button></div>`;
}

function bindObjectButtons(target) {
  for (const button of target.querySelectorAll("[data-action]")) button.addEventListener("click", () => objectAction(button.dataset.action));
  target.querySelector("#select-same")?.addEventListener("click", selectSameModel);
  target.querySelector("#hide-model")?.addEventListener("click", hideSelectedModels);
  target.querySelector("#replace-model")?.addEventListener("click", replaceSelectedModel);
  target.querySelector("#duplicate")?.addEventListener("click", duplicateSelected);
  target.querySelector("#copy")?.addEventListener("click", copySelected);
  target.querySelector("#delete")?.addEventListener("click", deleteSelected);
  target.querySelector("#draw-distance")?.addEventListener("change", (e) => {
    if (e.target.value === "") return;
    const distance = Math.max(0, +e.target.value);
    editObjects(selectedObjects().filter((o) => o.userData.kind === "prop"), (record) => { record.drawDistance = distance; });
  });
}

let lookClipboard = null;      // a copied texture variant and tint, for Paste look

// The Look section for props: texture variant and tint, for those whose actors offer them (an actor
// lists its texture and tint aliases; the client draws an object with the ones it names).
function renderLook(container, objects) {
  Promise.all(objects.map((o) => library.model(o.userData.record.file))).then((templates) => {
    if (!container.isConnected) return;
    const aliasesOf = new Map(objects.map((o, i) => [o, templates[i].userData.aliases]));
    const records = objects.map((o) => o.userData.record);
    const tintable = objects.filter((o) => aliasesOf.get(o).tints.length);
    const variants = [...new Set(objects.flatMap((o) => variantNames(aliasesOf.get(o))))];
    const materials = new Set(records.map((r) => r.material || ""));
    const material = materials.size === 1 ? [...materials][0] : null;
    const one = objects.length === 1;
    const tinted = tintable.find((o) => o.userData.record.tintAlias)?.userData.record;
    const aliasTints = one ? aliasesOf.get(objects[0]).tints : [];
    const swatches = [
      ...aliasTints.map((t) => ({ hex: tintHex(t.colour), title: `${t.name}${t.default ? " (default)" : ""}`, alias: t.name })),
      ...state.recentTints.map((hex) => ({ hex, title: `Recent: ${hex}` })),
    ];
    let markup = "<h4>Look</h4>";
    if (variants.length) {
      markup += `<label>Texture variant <select id="look-variant">${material === null ? `<option value="" disabled selected>mixed</option>` : ""}
        <option value="" ${material === "" ? "selected" : ""}>default</option>${variants.map((v) => `<option value="${escape(v)}" ${v === material ? "selected" : ""}>${escape(v)}</option>`).join("")}</select></label>`;
    }
    if (tintable.length) {
      if (one) {
        markup += `<label>Tint alias <select id="look-tint-alias"><option value="">none (untinted)</option>${aliasTints.map((t) =>
          `<option value="${escape(t.name)}" ${t.name === records[0].tintAlias ? "selected" : ""}>${escape(t.name)}${t.default ? " (default)" : ""}</option>`).join("")}</select></label>`;
      } else if (tintable.length < objects.length) markup += `<p class="hint">${tintable.length} of these ${objects.length} can be tinted.</p>`;
      markup += `<div class="row"><label>Tint <input id="look-tint" type="color" value="${tintHex(tinted?.tint)}"></label><button id="look-untint">No tint</button></div>
        <div class="swatches">${swatches.map((s) => `<span class="tint-swatch" data-hex="${s.hex}" ${s.alias ? `data-alias="${escape(s.alias)}"` : ""} title="${escape(s.title)}" style="background:${s.hex}"></span>`).join("")}</div>`;
    }
    if (!variants.length && !tintable.length) {
      markup += `<p class="hint">${one ? "This model has" : "These models have"} no texture variants or tint aliases, so the client draws ${one ? "it" : "them"} as authored.</p>`;
    }
    markup += `<div class="row"><button id="look-copy">Copy look</button><button id="look-paste" ${lookClipboard ? "" : "disabled"}>Paste look</button></div>`;
    container.innerHTML = markup;

    container.querySelector("#look-variant")?.addEventListener("change", (e) => {
      const value = e.target.value;
      editObjects(objects.filter((o) => !value || aliasesOf.get(o).textures.some((t) => t.name === value)), (record) => { record.material = value; });
    });
    container.querySelector("#look-tint-alias")?.addEventListener("change", (e) => {
      const alias = aliasTints.find((t) => t.name === e.target.value);
      editObjects(objects, (record) => {
        record.tintAlias = alias?.name ?? "";
        record.tint = alias ? [...alias.colour, 0] : null;
      });
    });
    const picker = container.querySelector("#look-tint");
    if (picker) {
      const edit = liveEdit(tintable);
      picker.addEventListener("input", () => edit.apply((record, object) => setTint(record, object, hexTint(picker.value))));
      picker.addEventListener("change", () => { rememberTint(picker.value); edit.commit(); });
    }
    container.querySelector("#look-untint")?.addEventListener("click", () => editObjects(tintable, (record) => { record.tintAlias = ""; record.tint = null; }));
    for (const swatch of container.querySelectorAll(".tint-swatch")) {
      swatch.addEventListener("click", () => {
        const { hex, alias } = swatch.dataset;
        editObjects(tintable, (record, object) => {
          if (alias && aliasesOf.get(object).tints.some((t) => t.name === alias)) record.tintAlias = alias;
          setTint(record, object, hexTint(hex));
        });
      });
    }
    container.querySelector("#look-copy").addEventListener("click", () => {
      const source = tinted || records[0];
      lookClipboard = { material: records[0].material || "", tintAlias: source.tintAlias || "", tint: source.tint || null };
      status(`Copied the look: ${lookClipboard.material || "default texture"}${lookClipboard.tintAlias ? `, ${lookClipboard.tintAlias} ${tintHex(lookClipboard.tint)}` : ", untinted"}.`);
      renderInspector();
    });
    container.querySelector("#look-paste")?.addEventListener("click", () => {
      const look = lookClipboard;
      editObjects(objects, (record, object) => {
        const aliases = aliasesOf.get(object);
        if (!look.material || aliases.textures.some((t) => t.name === look.material)) record.material = look.material;
        if (!look.tintAlias) { record.tintAlias = ""; record.tint = null; } else if (aliases.tints.length) {
          record.tintAlias = aliases.tints.some((t) => t.name === look.tintAlias) ? look.tintAlias : defaultTintAlias(aliases);
          record.tint = structuredClone(look.tint);
        }
      });
    });
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

// How close Focus brings the camera to a live player, NPC or placed object.
const LIVE_FOCUS = 5;

// Markers and live objects are selected on their own, never with others.
// (live, live-player and live-npc: the running game's placed objects, players and NPCs, see live.js.)
const alone = (object) => object?.userData.kind === "marker" || isLive(object);
const isLive = (object) => !!object?.userData.kind?.startsWith?.("live");

// Selects one object, or with add toggles it in the selection (props and lights; a marker selects alone).
function select(object, add = false) {
  settleNudge();
  if (add && object && !alone(object) && !alone(selected)) {
    if (selection.has(object)) {
      selection.delete(object);
      if (selected === object) selected = [...selection].at(-1) ?? null;
    } else {
      selection.add(object);
      selected = object;
    }
  } else {
    selection.clear();
    selected = object;
    if (object && !alone(object)) selection.add(object);
  }
  selectionChanged();
}

function selectMany(objects, add = false) {
  if (!add || alone(selected)) selection.clear();
  for (const object of objects) selection.add(object);
  selected = objects.at(-1) ?? [...selection].at(-1) ?? null;
  selectionChanged();
}

function selectedObjects() {
  return [...selection].filter((o) => o.parent);
}

function selectionChanged() {
  // A live object isn't in the selection (it has no record), but gets a box all the same.
  const boxed = new Set(selection);
  if (isLive(selected)) boxed.add(selected);
  for (const [object, box] of selectionBoxes) {
    if (!boxed.has(object)) { scene.remove(box); box.dispose(); selectionBoxes.delete(object); }
  }
  for (const object of boxed) {
    if (selectionBoxes.has(object)) continue;
    const box = new THREE.BoxHelper(object, isLive(object) ? 0x3fd0ff : 0xe8b84a);
    box.material.depthTest = false;
    box.renderOrder = 5;
    scene.add(box);
    selectionBoxes.set(object, box);
  }
  attachGizmo();
  renderInspector();
}

// The gizmo moves the one selected object, or a pivot at the selection's centre that carries all of them.
function attachGizmo() {
  gizmo.detach();
  const kind = selected?.userData.kind, chunk = selected?.userData.chunk;
  if (!selected || (kind !== "marker" && kind !== "live" && !(chunk && chunk.editable))) {
    updateTransformBar();
    return;
  }
  if (selection.size > 1) {
    const centre = new THREE.Vector3();
    for (const object of selection) centre.add(object.position);
    pivot.position.copy(centre.divideScalar(selection.size));
    pivot.rotation.set(0, 0, 0);
    pivot.scale.set(1, 1, 1);
    pivot.updateMatrixWorld();
    gizmo.attach(pivot);
  } else gizmo.attach(selected);
  gizmo.showY = true;
  if ((kind === "light" && gizmo.mode !== "translate") || (kind === "marker" && gizmo.mode === "scale")) gizmo.setMode("translate");
  updateTransformBar();
}

const RECORD_KEYS = ["file", "position", "rotation", "scale", "material", "tintAlias", "tint", "drawDistance"];

function recordState(record) {
  return structuredClone(Object.fromEntries(RECORD_KEYS.filter((k) => k in record).map((k) => [k, record[k]])));
}

function restoreRecord(record, saved) {
  for (const key of RECORD_KEYS) {
    if (key in saved) record[key] = structuredClone(saved[key]);
    else delete record[key];
  }
}

// Redraws an object from its record, moving it to the chunk now under it when that changed. Undo steps hold
// objects, and undoing a delete makes new ones for the same records, so the record's current object is used.
function refreshObject(object) {
  object = object.userData.record?.object ?? object;
  if (!object.parent) return;
  const { kind, record } = object.userData;
  if (kind === "light") {
    applyTransform(object, { position: record.position, rotation: [0, 0, 0], scale: 1 });
    props.refreshLight(record);
  } else props.refresh(object);
  const home = terrain.chunkAt(record.position[0], record.position[2]);
  if (home && home !== object.userData.chunk && home.editable) props.rehome(object, home);
  markDirty(object.userData.chunk, kind);
}

// Edits objects' records: apply(mutate) runs mutate(record, object) on each and redraws them, as often as a
// control moves (a colour picker); commit() makes everything applied since one undo step.
function liveEdit(objects) {
  objects = objects.filter((o) => o.userData.kind !== "marker" && o.userData.chunk?.editable);
  let before = null;
  return {
    apply(mutate) {
      before ??= objects.map((o) => [o, recordState(o.userData.record)]);
      for (const object of objects) mutate(object.userData.record, object);
      for (const object of objects) refreshObject(object);
    },
    commit() {
      if (!before) return;
      const saved = before;
      before = null;
      undoStack.push(() => {
        for (const [object, state] of saved) { restoreRecord(object.userData.record, state); refreshObject(object); }
        attachGizmo();
      });
      attachGizmo();
      renderInspector();
    },
  };
}

// Changes objects' records as one undo step: mutate(record, object) on each, then redraws them.
function editObjects(objects, mutate) {
  const edit = liveEdit(objects);
  edit.apply(mutate);
  edit.commit();
}

let dragStart = null;
// Shift held as a drag starts switches snapping for it (Shift pressed or let go mid-drag does too: see keyboard).
gizmo.addEventListener("dragging-changed", (event) => {
  if (event.value) settleNudge();
  applySnap(event.value && shiftHeld());
});
gizmo.addEventListener("dragging-changed", (event) => {
  if (!selected) return;
  if (event.value) {
    if (selected.userData.kind === "marker") { dragStart = null; return; }
    // A live object's server updates wait until the drag ends, then it goes to the game.
    if (selected.userData.kind === "live") { live.held = selected; dragStart = null; return; }
    pivot.updateMatrixWorld();
    dragStart = {
      pivot: pivot.matrixWorld.clone().invert(),
      entries: (gizmo.object === pivot ? selectedObjects() : [selected]).map((object) => {
        object.updateMatrixWorld();
        return { object, saved: recordState(object.userData.record), matrix: object.matrixWorld.clone() };
      }),
    };
    return;
  }
  if (selected.userData.kind === "marker") { selected.userData.apply?.(); renderInspector(); updateDirty(); return; }
  if (selected.userData.kind === "live") { liveStep(live.commit(selected), "Moved in game."); return; }
  const entries = dragStart?.entries || [];
  for (const { object } of entries) {
    // Dragged into another editable chunk: it belongs there now.
    const record = object.userData.record;
    const target = terrain.chunkAt(record.position[0], record.position[2]);
    if (target && target !== object.userData.chunk && target.editable) props.rehome(object, target);
  }
  if (entries.length) {
    undoStack.push(() => {
      for (const { object, saved } of entries) { restoreRecord(object.userData.record, saved); refreshObject(object); }
      attachGizmo();
    });
  }
  renderInspector();
});
gizmo.addEventListener("objectChange", () => {
  if (!selected || alone(selected)) return;
  if (gizmo.object === pivot && dragStart) {
    // Each object keeps its place relative to the pivot: rotating or scaling the pivot swings them round it.
    pivot.updateMatrixWorld();
    const delta = pivot.matrixWorld.clone().multiply(dragStart.pivot);
    const scale = new THREE.Vector3();
    for (const { object, matrix } of dragStart.entries) {
      delta.clone().multiply(matrix).decompose(object.position, object.quaternion, scale);
      object.scale.setScalar((scale.x + scale.y + scale.z) / 3);
      readObject(object);
    }
  } else readObject(selected);
});

function readObject(object) {
  const { kind, record, chunk } = object.userData;
  if (kind === "light") record.position = [object.position.x, object.position.y, object.position.z];
  else readTransform(object, record);
  markDirty(chunk, kind);
}

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
  const [object] = addRecords([{ chunk, kind, record }]);
  select(object);
  return object;
}

// Puts records ({ chunk, kind, record }) into their chunks and the view; returns their objects.
function putRecords(entries) {
  return entries.map(({ chunk, kind, record }) => {
    (kind === "light" ? chunk.lights : chunk.objects).push(record);
    markDirty(chunk, kind);
    return kind === "light" ? props.addLight(chunk, record) : props.addProp(chunk, record);
  });
}

// Takes objects out of their chunks and the view; returns them as entries putRecords can put back.
function takeObjects(objects) {
  const taken = [];
  for (const object of objects) {
    if (!object?.parent) continue;
    const { kind, record, chunk } = object.userData;
    if (kind === "marker" || !chunk.editable) continue;
    const list = kind === "light" ? chunk.lights : chunk.objects;
    if (list.includes(record)) list.splice(list.indexOf(record), 1);
    props.remove(object);
    selection.delete(object);
    if (selected === object) selected = null;
    markDirty(chunk, kind);
    taken.push({ chunk, kind, record });
  }
  if (taken.length) {
    selected ??= [...selection].at(-1) ?? null;
    selectionChanged();
  }
  return taken;
}

// Adds records as one undo step; returns their objects.
function addRecords(entries) {
  const objects = putRecords(entries);
  if (objects.length) undoStack.push(() => takeObjects(entries.map((e) => e.record.object)));
  return objects;
}

// Removes objects as one undo step; returns how many.
function removeObjects(objects) {
  const taken = takeObjects(objects);
  if (taken.length) undoStack.push(() => putRecords(taken));
  return taken.length;
}

function deleteSelected() {
  if (!selected || selected.userData.kind === "marker" || selected.userData.kind?.startsWith("live-")) return;
  if (selected.userData.kind === "live") return liveStep(live.remove(selected), `Deleted ${selected.userData.label} in game.`);
  const count = removeObjects(selectedObjects());
  if (count > 1) status(`Deleted ${count} objects.`);
}

// A live edit's answer: done, it's one undo step (undone on the game server) and the inspector catches up.
async function liveStep(pending, done) {
  if (!(await pending)) return;
  undoStack.push(() => live.undo().then((label) => status(label ? `Undid ${label} in game.` : "Nothing to undo in game.")));
  if (done) status(done);
  renderInspector();
}

// Turns a live object into one of the zone's own props, in the chunk under it, and takes it out of the game: from
// a test build in the live world to the zone's files (Save and Test in game ship it).
async function bakeLive(object) {
  const data = object.userData.live;
  if (!data.model) return toast("This NPC has no model the zone can hold.");
  const chunk = editableChunkFor(data.x, data.z);
  if (!chunk) return toast("No editable chunk is loaded here. Add one with the Chunks tool (K).");
  if (!(await live.remove(object))) return;
  undoStack.push(() => live.undo());
  addRecord(chunk, "prop", { file: data.model, position: [data.x, data.y, data.z], rotation: [data.h, data.pitch, data.roll], scale: data.scale });
  toast(`Baked ${data.label} into chunk ${chunk.key} and took it out of the game. Save, then Test in game, to ship it.`, "ok");
}

// A record as a new object: what save copies from (copyOf), without the original's identity.
function copyRecord(record) {
  const copy = structuredClone({ ...record, object: undefined });
  delete copy.object;
  delete copy.uid;
  delete copy.id;
  copy.copyOf = record.uid || record.copyOf;
  return copy;
}

function duplicateSelected() {
  if (!selected || selected.userData.kind === "marker" || selected.userData.kind?.startsWith("live-")) return;
  if (selected.userData.kind === "live") return liveStep(live.duplicate(selected), `Copied ${selected.userData.label} in game.`);
  pasteRecords(selectedObjects().map((o) => ({ kind: o.userData.kind, record: copyRecord(o.userData.record) })), [2, 0, 2]);
}

// Puts copies of records in the chunks under them, offset by [dx, dy, dz], and selects them.
function pasteRecords(items, offset) {
  const entries = [];
  for (const { kind, record } of items) {
    const copy = structuredClone(record);
    copy.position = record.position.map((v, k) => v + offset[k]);
    const chunk = editableChunkFor(copy.position[0], copy.position[2]);
    if (chunk) entries.push({ chunk, kind, record: copy });
  }
  if (!entries.length) return toast("No editable chunk is loaded there.");
  selectMany(addRecords(entries));
  status(`Placed ${entries.length} object(s).`);
}

let clipboard = null;      // copied records and their centre, for Ctrl+V at the pointer

function copySelected() {
  const objects = selectedObjects().filter((o) => o.userData.kind !== "marker");
  if (!objects.length) return;
  const items = objects.map((o) => ({ kind: o.userData.kind, record: copyRecord(o.userData.record) }));
  const centre = [0, 1, 2].map((k) => items.reduce((sum, i) => sum + i.record.position[k], 0) / items.length);
  clipboard = { items, centre };
  status(`Copied ${items.length} object(s); Ctrl+V pastes them at the pointer.`);
}

function pasteAtPointer() {
  if (!clipboard) return;
  const hit = terrainHit();
  if (!hit) return toast("Point at the ground to paste there.");
  const { items, centre } = clipboard;
  pasteRecords(items, [hit.x - centre[0], hit.y - centre[1], hit.z - centre[2]]);
}

// --- object actions ------------------------------------------------------------------------------------

// The ground's normal at (x, z), from the heights a unit either side.
function groundNormal(x, z) {
  const h = (dx, dz) => terrain.heightAt(x + dx, z + dz);
  const [l, r, b, f] = [h(-1, 0), h(1, 0), h(0, -1), h(0, 1)];
  if ([l, r, b, f].some((v) => v === null)) return new THREE.Vector3(0, 1, 0);
  return new THREE.Vector3(-(r - l) / 2, 1, -(f - b) / 2).normalize();
}

// Pitch and roll that stand an object with yaw on the ground's slope (rotation order YXZ).
function slopeRotation(yaw, normal) {
  const n = normal.clone().applyAxisAngle(new THREE.Vector3(0, 1, 0), -yaw);
  return [yaw, Math.atan2(n.z, n.y), -Math.asin(Math.max(-1, Math.min(1, n.x)))];
}

function randomScale() {
  const { scaleMin, scaleMax } = state.place;
  return Math.round((scaleMin + Math.random() * Math.max(0, scaleMax - scaleMin)) * 1000) / 1000;
}

const OBJECT_ACTIONS = {
  ground: ["Drop to ground", (r) => { const h = terrain.heightAt(r.position[0], r.position[2]); if (h !== null) r.position[1] = h; }],
  slope: ["Align to slope", (r) => { r.rotation = slopeRotation(r.rotation[0], groundNormal(r.position[0], r.position[2])); }],
  upright: ["Stand upright", (r) => { r.rotation = [r.rotation[0], 0, 0]; }],
  turn: ["Random turn", (r) => { r.rotation = [Math.random() * Math.PI * 2, r.rotation[1], r.rotation[2]]; }],
  scale: ["Random scale", (r) => { r.scale = randomScale(); }],
  reset: ["Reset turn and scale", (r) => { r.rotation = [0, 0, 0]; r.scale = 1; }],
};

function objectAction(name) {
  const [, action] = OBJECT_ACTIONS[name];
  editObjects(selectedObjects().filter((o) => o.userData.kind === "prop" || name === "ground"), (record, object) => {
    if (object.userData.kind === "light" && name === "ground") {
      const h = terrain.heightAt(record.position[0], record.position[2]);
      if (h !== null) record.position[1] = h + 6;
    } else action(record);
  });
}

// Every loaded prop of the selected objects' actors.
function selectSameModel() {
  const files = new Set(selectedObjects().filter((o) => o.userData.kind === "prop").map((o) => o.userData.record.file.toLowerCase()));
  selectMany(props.pickables.filter((o) => o.visible && o.userData.kind === "prop" && files.has(o.userData.record.file.toLowerCase())));
  status(`Selected ${selection.size} object(s) of ${[...files].join(", ")} in the loaded chunks.`);
}

function hideSelectedModels() {
  const files = new Set(selectedObjects().filter((o) => o.userData.kind === "prop").map((o) => o.userData.record.file));
  for (const file of files) props.setFileHidden(file, true);
  select(null);
  status(`Hid ${[...files].join(", ")}. Show them again from the Select tool (or Shift+H).`);
  renderPanel();
}

function replaceSelectedModel() {
  if (!state.actor) return toast("Pick the replacement in the content browser first.");
  editObjects(selectedObjects().filter((o) => o.userData.kind === "prop"), (record) => {
    record.file = state.actor;
    // The new actor's texture and tint aliases differ.
    record.material = "";
    record.tintAlias = "";
    record.tint = null;
  });
}

// --- the gizmo's controls (the bar over the view) --------------------------------------------------------

const SNAP_STEPS = { move: [0, 0.1, 0.25, 0.5, 1, 2, 4, 8], turn: [0, 5, 15, 45, 90], scale: [0, 0.05, 0.1, 0.25, 0.5] };
// What Shift snaps to while dragging when no snapping is set.
const SNAP_DEFAULTS = { move: 1, turn: 15, scale: 0.1 };

// The gizmo's snapping as set, or with invert (Shift held while dragging) switched: on to the defaults, or off.
function applySnap(invert = false) {
  const set = state.snap.move || state.snap.turn || state.snap.scale;
  const snap = invert ? (set ? { move: 0, turn: 0, scale: 0 } : SNAP_DEFAULTS) : state.snap;
  gizmo.setTranslationSnap(snap.move || null);
  gizmo.setRotationSnap(snap.turn ? THREE.MathUtils.degToRad(snap.turn) : null);
  gizmo.setScaleSnap(snap.scale || null);
}

// Props and live objects scale; lights and markers only move (markers turn too).
function canScale(object) {
  return object?.userData.kind === "prop" || object?.userData.kind === "live";
}

function setGizmoMode(mode) {
  if (mode === "scale" && selected && !canScale(selected)) return status("Only props and live objects scale.");
  if (mode === "rotate" && selected?.userData.kind === "light") return status("Lights don't turn.");
  gizmo.setMode(mode);
  updateTransformBar();
}

function updateTransformBar() {
  const bar = $("#transform-bar");
  bar.hidden = !state.zone;
  bar.classList.toggle("idle", !gizmo.object);
  for (const button of bar.querySelectorAll("[data-mode]")) button.classList.toggle("active", button.dataset.mode === gizmo.mode);
  bar.querySelector('[data-mode="scale"]').disabled = !!selected && !canScale(selected);
  bar.querySelector('[data-mode="rotate"]').disabled = selected?.userData.kind === "light";
  $("#gizmo-space").textContent = gizmo.space === "local" ? "Local" : "World";
}

function setupTransformBar() {
  const fill = (id, steps, current, format, off) => {
    $(id).innerHTML = steps.map((v) => `<option value="${v}" ${v === current ? "selected" : ""}>${v ? format(v) : off}</option>`).join("");
  };
  fill("#snap-move", SNAP_STEPS.move, state.snap.move, (v) => `${v} m`, "Move: free");
  fill("#snap-turn", SNAP_STEPS.turn, state.snap.turn, (v) => `${v}°`, "Turn: free");
  fill("#snap-scale", SNAP_STEPS.scale, state.snap.scale, (v) => `×${v}`, "Scale: free");
  for (const kind of ["move", "turn", "scale"]) {
    $(`#snap-${kind}`).addEventListener("change", (e) => {
      state.snap[kind] = +e.target.value;
      applySnap();
      e.target.blur();
    });
  }
  for (const button of $("#transform-bar").querySelectorAll("[data-mode]")) button.addEventListener("click", () => setGizmoMode(button.dataset.mode));
  $("#gizmo-space").addEventListener("click", () => {
    gizmo.setSpace(gizmo.space === "local" ? "world" : "local");
    updateTransformBar();
  });
  const resize = (factor) => gizmo.setSize(Math.min(3, Math.max(0.3, gizmo.size * factor)));
  $("#gizmo-smaller").addEventListener("click", () => resize(0.8));
  $("#gizmo-larger").addEventListener("click", () => resize(1.25));
  // Buttons hand the keyboard straight back to the view.
  for (const button of $("#transform-bar").querySelectorAll("button")) button.addEventListener("click", () => button.blur());
  updateTransformBar();
}

// Puts the camera back from an object along the view; nearest is how close it may come (characters are small).
function focusOn(object, nearest = 35) {
  const forward = new THREE.Vector3();
  camera.getWorldDirection(forward);
  const p = new THREE.Vector3();
  object.getWorldPosition(p);
  const size = new THREE.Box3().setFromObject(object).getSize(new THREE.Vector3()).length();
  camera.position.copy(p).addScaledVector(forward, -Math.max(nearest, size * 1.2)).add(new THREE.Vector3(0, Math.min(8, nearest / 3), 0));
}

// Loaded props whose name has every word of the Select tool's search, nearest first.
function foundObjects() {
  const words = state.findQuery.toLowerCase().split(/\s+/).filter(Boolean);
  if (!words.length) return [];
  return props.pickables
    .filter((o) => o.visible && o.userData.kind === "prop" && words.every((w) => o.userData.record.file.toLowerCase().includes(w)))
    .map((o) => [o.position.distanceTo(camera.position), o])
    .sort((a, b) => a[0] - b[0])
    .map(([, o]) => o);
}

function listFound() {
  const list = panel.querySelector("#found-objects");
  if (!list) return;
  const found = foundObjects();
  // The running game's players, placed objects and NPCs by that name come first (live.js).
  const liveFound = live.find(state.findQuery.toLowerCase().split(/\s+/).filter(Boolean)).slice(0, 50);
  const where = (entry) => entry.node?.position ?? new THREE.Vector3(entry.npc.x, entry.npc.y, entry.npc.z);
  list.innerHTML = liveFound.map((entry, i) =>
    `<div data-live-found="${i}"><span class="live-badge">LIVE</span> ${escape(entry.label)} <small>${entry.kind} · ${Math.round(where(entry).distanceTo(camera.position))} away</small></div>`).join("") +
    found.slice(0, 150).map((o, i) =>
    `<div data-found="${i}">${escape(o.userData.record.file)} <small>${Math.round(o.position.distanceTo(camera.position))} away</small></div>`).join("") ||
    (state.findQuery ? `<div class="hint">Nothing by that name in the loaded chunks${live.zone ? " or the game" : ""}.</div>` : "");
  for (const row of list.querySelectorAll("[data-live-found]")) {
    row.addEventListener("click", () => { const node = live.reveal(liveFound[+row.dataset.liveFound]); select(node); focusOn(node, LIVE_FOCUS); });
  }
  for (const row of list.querySelectorAll("[data-found]")) {
    row.addEventListener("click", () => { const object = found[+row.dataset.found]; select(object); focusOn(object); });
  }
  const button = panel.querySelector("#select-found");
  button.disabled = !found.length;
  button.textContent = found.length ? `Select all ${found.length} found` : "Select all found";
}

// A new object's record at point, with the Props tool's turn, scale, slope and height options.
function newRecord(file, point, yaw = 0) {
  const p = state.place;
  const turn = p.randomYaw ? Math.random() * Math.PI * 2 : yaw;
  return {
    file, position: [point.x, point.y + p.offset, point.z], scale: randomScale(),
    rotation: p.align ? slopeRotation(turn, groundNormal(point.x, point.z)) : [turn, 0, 0],
  };
}

// The Props tool's texture variant and tint on a new object, where its actor has them.
function applyPlaceLook(object) {
  const { material, tintOn, tint } = state.place;
  if (!material && !tintOn) return;
  library.model(object.userData.record.file).then((template) => {
    const aliases = template.userData.aliases, record = object.userData.record;
    if (material && aliases.textures.some((t) => t.name === material)) record.material = material;
    if (tintOn && aliases.tints.length) {
      record.tintAlias = defaultTintAlias(aliases);
      record.tint = hexTint(tint);
    }
    props.applyAppearance(object);
  });
}

function renderPlaceLook() {
  const box = panel.querySelector("#place-look");
  const file = state.actor || state.place.set[0];
  if (!box || !file) return;
  library.model(file).then((template) => {
    if (!box.isConnected) return;
    const p = state.place, aliases = template.userData.aliases, variants = variantNames(aliases);
    box.innerHTML = (variants.length ? `<label>Texture variant <select id="place-variant"><option value="">default</option>${variants.map((v) =>
      `<option value="${escape(v)}" ${v === p.material ? "selected" : ""}>${escape(v)}</option>`).join("")}</select></label>` : "") +
      (aliases.tints.length ? `<div class="row"><label class="check"><input id="place-tint-on" type="checkbox" ${p.tintOn ? "checked" : ""}> Tint them</label>
        <input id="place-tint" type="color" value="${p.tint}"></div>` : "") +
      (!variants.length && !aliases.tints.length ? `<p class="hint">${escape(file)} has no texture variants or tint aliases.</p>` : "");
    box.querySelector("#place-variant")?.addEventListener("change", (e) => { p.material = e.target.value; });
    box.querySelector("#place-tint-on")?.addEventListener("change", (e) => { p.tintOn = e.target.checked; });
    box.querySelector("#place-tint")?.addEventListener("input", (e) => { p.tint = e.target.value; p.tintOn = true; box.querySelector("#place-tint-on").checked = true; });
  });
}

// --- scatter brush -------------------------------------------------------------------------------------

let scatter = null;        // a scatter stroke in progress: { placed, erased, erase, budget }

function scatterFiles() {
  return state.place.set.length ? state.place.set : state.actor ? [state.actor] : [];
}

// One frame of a scatter stroke: objects of the set dropped at random under the brush, no closer to each
// other than the spacing; or, erasing, the set's objects under the brush taken away.
function scatterStep(dt) {
  if (!hover) return;
  const { radius, strength } = state.brush;
  const files = scatterFiles();
  const names = new Set(files.map((f) => f.toLowerCase()));
  const near = props.pickables.filter((o) => o.userData.kind === "prop" && names.has(o.userData.record.file.toLowerCase()) &&
    Math.hypot(o.position.x - hover.x, o.position.z - hover.z) <= radius + state.place.spacing);
  if (scatter.erase) {
    const doomed = near.filter((o) => Math.hypot(o.position.x - hover.x, o.position.z - hover.z) <= radius);
    scatter.erased.push(...takeObjects(doomed));
    return;
  }
  const spacing = state.place.spacing;
  // About one object per spacing-sized cell of the brush per second at full strength.
  scatter.budget += strength * dt * Math.max(1, (Math.PI * radius * radius) / (spacing * spacing)) * 1.5;
  const points = near.map((o) => [o.position.x, o.position.z]);
  const entries = [];
  for (let tries = 0; scatter.budget >= 1 && tries < 30; tries++) {
    const angle = Math.random() * Math.PI * 2, distance = radius * Math.sqrt(Math.random());
    const x = hover.x + Math.cos(angle) * distance, z = hover.z + Math.sin(angle) * distance;
    if (points.some(([px, pz]) => Math.hypot(px - x, pz - z) < spacing)) continue;
    const y = terrain.heightAt(x, z);
    const chunk = y === null ? null : editableChunkFor(x, z);
    if (!chunk || terrain.chunkAt(x, z) !== chunk) continue;
    scatter.budget -= 1;
    points.push([x, z]);
    entries.push({ chunk, kind: "prop", record: newRecord(files[Math.floor(Math.random() * files.length)], { x, y, z }) });
  }
  scatter.budget = Math.min(scatter.budget, 3);
  for (const object of putRecords(entries)) { applyPlaceLook(object); scatter.placed.push(object); }
}

function endScatter() {
  const { placed, erased } = scatter;
  scatter = null;
  if (!placed.length && !erased.length) return;
  const records = placed.map((o) => o.userData.record);
  undoStack.push(() => { takeObjects(records.map((r) => r.object)); putRecords(erased); });
  status(placed.length ? `Scattered ${placed.length} object(s).` : `Erased ${erased.length} object(s).`);
}

// --- tint and texture variants -------------------------------------------------------------------------

function tintHex(tint) {
  return rgbHex((tint || [1, 1, 1]).slice(0, 3).map((v) => Math.max(0, Math.min(255, Math.round(v * 255)))));
}

function hexTint(hex) {
  return [...[1, 3, 5].map((i) => parseInt(hex.slice(i, i + 2), 16) / 255), 0];
}

function defaultTintAlias(aliases) {
  return (aliases.tints.find((t) => t.default) || aliases.tints[0])?.name ?? "";
}

// Tints an object whose actor has tint aliases (keeping its alias, else taking the actor's default).
function setTint(record, object, tint) {
  const aliases = object.userData.aliases;
  if (!aliases?.tints.length) return;
  if (!aliases.tints.some((t) => t.name === record.tintAlias)) record.tintAlias = defaultTintAlias(aliases);
  record.tint = tint;
}

function rememberTint(hex) {
  state.recentTints = [hex, ...state.recentTints.filter((h) => h !== hex)].slice(0, 10);
}

// --- ghost prop for placing ----------------------------------------------------------------------------

function makeGhost() {
  if (ghost) scene.remove(ghost);
  ghost = null;
  if (state.tool !== "props" || !state.actor || state.place.mode === "scatter") return;
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
  applyPlaceLook(addRecord(chunk, "prop", newRecord(name, hit, yaw)));
  content.placed(name);
}

// Places the actor in the running game instead (Place live): a placed model, there for players at once.
function placeLive(name, hit, yaw = 0) {
  if (!live.canPlace(name)) {
    return toast(`${name} has no Models.txt row, so the game can't spawn it live. Untick Place live to put it in the zone's files instead.`);
  }
  liveStep(live.placeModel(name, newRecord(name, hit, yaw)), `Placed ${name} in game.`);
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

let boxSelect = null;      // a press in the Select tool: { x, y, add, picked, active (dragged into a box) }

// Selects the props and lights whose centres are inside the screen rectangle from a to b.
function selectInBox(a, b, add) {
  const rect = canvas.getBoundingClientRect();
  const [x0, x1] = [Math.min(a.x, b.x), Math.max(a.x, b.x)], [y0, y1] = [Math.min(a.y, b.y), Math.max(a.y, b.y)];
  const v = new THREE.Vector3();
  const inside = props.pickables.filter((object) => {
    if (!object.visible) return false;
    v.copy(object.position).project(camera);
    if (v.z < -1 || v.z > 1) return false;
    const sx = rect.left + (v.x + 1) / 2 * rect.width, sy = rect.top + (1 - v.y) / 2 * rect.height;
    return sx >= x0 && sx <= x1 && sy >= y0 && sy <= y1;
  });
  selectMany(inside, add);
  status(`${selection.size} object(s) selected.`);
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
  // Each move says which buttons are down, so a button let go outside the window doesn't keep the view turning
  // or a brush painting.
  pointer.buttons = event.buttons;
  if (pointer.buttons & 2) {
    look.yaw -= event.movementX * 0.003;
    look.pitch = Math.max(-1.55, Math.min(1.55, look.pitch - event.movementY * 0.003));
  }
  if (!(pointer.buttons & 1)) letGoOfLeft(event);
});
canvas.addEventListener("pointerleave", () => { pointer.inside = false; });
canvas.addEventListener("lostpointercapture", (event) => {
  pointer.buttons = 0;
  letGoOfLeft(event);
});

// Ends what a left drag started when its button came up where the canvas didn't hear it.
function letGoOfLeft(event) {
  if (boxSelect) {
    boxSelect = null;
    $("#select-box").style.display = "none";
  }
  if (scatter) endScatter();
  if (stroke) {
    rampLine.visible = false;
    endStroke();
  }
}
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
  if (tool === "props" && state.place.mode === "scatter" && scatterFiles().length) {
    scatter = { placed: [], erased: [], erase: event.shiftKey, budget: 1 };
    return;
  }
  if (tool === "props" && state.actor && !event.shiftKey) {
    const hit = terrainHit();
    if (hit && state.place.live && live.zone) placeLive(state.actor, hit, ghost ? ghost.rotation.y : 0);
    else if (hit) placeActor(state.actor, hit, ghost ? ghost.rotation.y : 0);
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
  let picked = propHit ? propHit.object : null;
  let pickedDistance = propHit?.distance ?? Infinity;
  if (markerHit && markerHit.distance < pickedDistance) {
    picked = markerHit.object;
    pickedDistance = markerHit.distance;
    while (picked.parent && !markers.includes(picked)) picked = picked.parent;
  }
  const liveHit = live.pick(raycaster);
  if (liveHit && liveHit.distance < pickedDistance) picked = liveHit.object;
  // The Select tool decides on release: a drag over the view is a box selection, a click picks.
  if (tool === "select") {
    boxSelect = { x: event.clientX, y: event.clientY, add: event.shiftKey, picked, active: false };
    return;
  }
  select(picked, event.shiftKey && !!picked);
});
canvas.addEventListener("pointermove", (event) => {
  if (!boxSelect || !(event.buttons & 1)) return;
  const dx = event.clientX - boxSelect.x, dy = event.clientY - boxSelect.y;
  if (!boxSelect.active && Math.hypot(dx, dy) < 6) return;
  boxSelect.active = true;
  const view = $("#viewport").getBoundingClientRect(), box = $("#select-box");
  Object.assign(box.style, {
    display: "block", left: `${Math.min(event.clientX, boxSelect.x) - view.left}px`, top: `${Math.min(event.clientY, boxSelect.y) - view.top}px`,
    width: `${Math.abs(dx)}px`, height: `${Math.abs(dy)}px`,
  });
});
canvas.addEventListener("pointerup", (event) => {
  pointer.buttons = event.buttons;
  if (boxSelect) {
    const box = boxSelect;
    boxSelect = null;
    $("#select-box").style.display = "none";
    if (box.active) selectInBox(box, { x: event.clientX, y: event.clientY }, box.add);
    else select(box.picked, box.add && !!box.picked);
  }
  if (scatter) endScatter();
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

// --- keyboard ------------------------------------------------------------------------------------------

// Fields that take typing keep their keys; everything else (checkboxes, sliders, lists, buttons) leaves them to the
// editor, so clicking one doesn't silently switch the shortcuts off.
const TEXT_TYPES = new Set(["text", "search", "number", "email", "password", "url", "tel", ""]);
function isTextEntry(element) {
  if (!element?.closest) return false;
  if (element.closest("textarea, [contenteditable]")) return true;
  return element.tagName === "INPUT" && TEXT_TYPES.has((element.getAttribute("type") || "").toLowerCase());
}

// Browser shortcuts that would take the keyboard away from the editor (and with it the keyup of a fly key):
// Ctrl+D bookmarks, Ctrl+E, K and L go to the address bar, Ctrl+F and G find, Ctrl+H and J open history and
// downloads, Ctrl+O, P and U open, print and view source, F3, F6 and F7 find, the address bar and caret browsing,
// Alt and F10 the menu. No page can stop Ctrl+W, T, N or Tab, so the editor never needs Ctrl with a fly key.
const BROWSER_SHORTCUTS = new Set(["KeyA", "KeyC", "KeyD", "KeyE", "KeyF", "KeyG", "KeyH", "KeyJ", "KeyK", "KeyL",
  "KeyO", "KeyP", "KeyS", "KeyU", "KeyV", "KeyY", "KeyZ"]);
const BROWSER_KEYS = new Set(["F3", "F6", "F7", "F10", "AltLeft", "AltRight"]);
const FLY_KEYS = new Set(["KeyW", "KeyA", "KeyS", "KeyD", "KeyQ", "KeyE"]);
const TOOL_KEYS = { v: "select", b: "sculpt", x: "exclude", m: "eco", c: "colour", n: "environment", p: "props", l: "lights", k: "chunks", z: "zone" };

window.addEventListener("keydown", (event) => {
  if (BROWSER_KEYS.has(event.code)) event.preventDefault();
  if (event.code === "F11" && !event.repeat) {
    // The editor's page full screen rather than the browser's window (which in the panel keeps its frame).
    event.preventDefault();
    toggleFullscreen();
    return;
  }
  // A dialog (New zone) has the keyboard to itself.
  if (document.querySelector("dialog[open]")) return;
  if (isTextEntry(event.target)) {
    if (event.key === "Escape") event.target.blur();
    return;
  }
  held.add(event.code);
  const key = event.key.toLowerCase();

  // Shift while dragging the gizmo switches snapping (on to the defaults, or off when it's set).
  if (event.key === "Shift" && gizmo.dragging && !event.repeat) applySnap(true);

  if (event.ctrlKey || event.metaKey) {
    if (BROWSER_SHORTCUTS.has(event.code)) event.preventDefault();
    if (event.repeat) return;
    settleNudge();
    if (key === "s") saveAll();
    else if (key === "z") undo();
    else if (key === "d") duplicateSelected();
    else if (key === "c") copySelected();
    else if (key === "v") pasteAtPointer();
    return;
  }
  if (event.altKey) return;

  // Nudges repeat while the key is held; everything else happens once per press.
  if (nudgeKey(event)) {
    event.preventDefault();
    return;
  }
  if (event.repeat) return;

  const digit = "1234567890".indexOf(key);
  if (key === "?") toggleKeysSheet();
  else if (key === "escape") onEscape();
  else if (digit >= 0 && state.tool === "sculpt" && SCULPT_MODES[digit]) { state.brush.mode = SCULPT_MODES[digit]; renderPanel(); }
  else if (digit >= 0 && digit < 2 && state.tool === "exclude") { state.exclude.mode = ["cut", "restore"][digit]; renderPanel(); }
  else if ((event.code === "BracketLeft" || event.code === "BracketRight") && BRUSH_TOOLS.has(state.tool)) {
    const up = event.code === "BracketRight";
    if (event.shiftKey) state.brush.strength = Math.max(0.02, Math.min(1, state.brush.strength * (up ? 1.15 : 0.87)));
    else state.brush.radius = Math.max(2, Math.min(128, Math.round(state.brush.radius * (up ? 1.15 : 0.87))));
    renderPanel();
  }
  else if (key === "i" && state.zone) eyedrop();
  else if (key === "delete" || key === "backspace") deleteSelected();
  else if (key === "o") toggleContent();
  else if (event.code === "Backslash") togglePanel();
  else if (key === "g") setGizmoMode("translate");
  else if (key === "r") setGizmoMode("rotate");
  else if (key === "t") setGizmoMode("scale");
  else if (key === "f" && selected) focusOn(selected, isLive(selected) ? LIVE_FOCUS : undefined);
  else if (key === "h" && event.shiftKey) { props.showAll(); renderPanel(); }
  else if (key === "h" && selected) hideSelectedModels();
  else if (key === "end" && selected?.userData.kind === "live") dropLiveToGround(selected);
  else if (key === "end" && selected) objectAction("ground");
  else if (TOOL_KEYS[key] && !FLY_KEYS.has(event.code)) setTool(TOOL_KEYS[key]);
});
window.addEventListener("keyup", (event) => {
  held.delete(event.code);
  if (BROWSER_KEYS.has(event.code)) event.preventDefault();
  if (event.key === "Shift" && gizmo.dragging) applySnap(false);
});
// Wherever the keyboard goes (another window, a dialog, the browser's own bar), the keys' keyups go with it.
window.addEventListener("blur", () => held.clear());
document.addEventListener("visibilitychange", () => { if (document.hidden) held.clear(); });
document.addEventListener("focusin", (event) => { if (isTextEntry(event.target)) held.clear(); });

// Esc closes what's open over the view first, then lets go of the selection and the content pick.
function onEscape() {
  if (!$("#view-menu").hidden || !$("#keys-sheet").hidden) {
    setViewMenu(false);
    $("#keys-sheet").hidden = true;
    return;
  }
  select(null);
  state.actor = null;
  if (ghost) { scene.remove(ghost); ghost = null; }
  renderPanel();
}

function toggleKeysSheet() {
  $("#keys-sheet").hidden = !$("#keys-sheet").hidden;
}

// --- nudging -------------------------------------------------------------------------------------------

// Arrows move the selection along the ground (on the world axis nearest the view's), PgUp and PgDn up and down,
// , and . turn it, - and = scale it. Steps follow the gizmo's snapping. Presses close together make one undo
// step; on a live object they go to the game together once the keys stop.
const NUDGE_SETTLE = 500;
let nudging = null;            // { objects, edit, timer } or { live, timer }

function nudgeKey(event) {
  if (!selected || BRUSH_TOOLS.has(state.tool)) return false;
  const code = event.code;
  if (code.startsWith("Arrow") || code === "PageUp" || code === "PageDown") {
    const step = (state.snap.move || 0.25) * (event.shiftKey ? 4 : 1);
    if (code === "PageUp" || code === "PageDown") return nudge({ move: new THREE.Vector3(0, code === "PageUp" ? step : -step, 0) });
    const forward = new THREE.Vector3();
    camera.getWorldDirection(forward);
    const [fx, fz] = Math.abs(forward.x) > Math.abs(forward.z) ? [Math.sign(forward.x), 0] : [0, Math.sign(forward.z)];
    const [dx, dz] = { ArrowUp: [fx, fz], ArrowDown: [-fx, -fz], ArrowRight: [-fz, fx], ArrowLeft: [fz, -fx] }[code];
    return nudge({ move: new THREE.Vector3(dx * step, 0, dz * step) });
  }
  if (code === "Comma" || code === "Period") {
    const degrees = event.shiftKey ? 90 : state.snap.turn || 15;
    // Seen from above, . turns clockwise.
    return nudge({ turn: THREE.MathUtils.degToRad(code === "Period" ? -degrees : degrees) });
  }
  if (code === "Minus" || code === "Equal" || code === "NumpadSubtract" || code === "NumpadAdd") {
    const step = 1 + (state.snap.scale || 0.1);
    return nudge({ scale: code === "Minus" || code === "NumpadSubtract" ? 1 / step : step });
  }
  return false;
}

// Applies a nudge to what's selected; true if anything could take it.
function nudge({ move, turn, scale }) {
  const kind = selected.userData.kind;
  if (kind === "marker") {
    if (scale) return false;
    if (move) selected.position.add(move);
    if (turn) selected.rotation.y += turn;
    selected.userData.apply?.();
    updateDirty();
    attachGizmo();
    renderInspector();
    return true;
  }
  if (kind === "live") {
    const object = selected;
    object.rotation.reorder("YXZ");
    if (move) object.position.add(move);
    if (turn) object.rotation.y += turn;
    if (scale) object.scale.setScalar(Math.min(20, Math.max(0.05, object.scale.x * scale)));
    if (nudging?.live !== object) settleNudge();
    live.held = object;
    clearTimeout(nudging?.timer);
    nudging = { live: object, timer: setTimeout(settleNudge, NUDGE_SETTLE) };
    attachGizmo();
    return true;
  }
  if (kind?.startsWith("live-")) return false;

  const objects = selectedObjects().filter((o) => o.userData.chunk?.editable);
  if (!objects.length) return false;
  const same = nudging?.objects && nudging.objects.length === objects.length && objects.every((o, i) => o === nudging.objects[i]);
  if (!same) {
    settleNudge();
    nudging = { objects, edit: liveEdit(objects) };
  }
  const centre = turn && objects.length > 1
    ? objects.reduce((sum, o) => sum.add(o.position), new THREE.Vector3()).divideScalar(objects.length) : null;
  const [sin, cos] = turn ? [Math.sin(turn), Math.cos(turn)] : [0, 1];
  nudging.edit.apply((record, object) => {
    const position = [...record.position];
    if (move) [0, 1, 2].forEach((k) => { position[k] += move.getComponent(k); });
    if (centre) {
      // Several turn together round their centre, as the gizmo turns them.
      const dx = position[0] - centre.x, dz = position[2] - centre.z;
      position[0] = centre.x + dx * cos + dz * sin;
      position[2] = centre.z - dx * sin + dz * cos;
    }
    record.position = position;
    if (object.userData.kind !== "prop") return;
    if (turn) record.rotation = [record.rotation[0] + turn, record.rotation[1], record.rotation[2]];
    if (scale) record.scale = Math.min(100, Math.max(0.01, record.scale * scale));
  });
  clearTimeout(nudging.timer);
  nudging.timer = setTimeout(settleNudge, NUDGE_SETTLE);
  attachGizmo();
  return true;
}

// Ends a run of nudges: one undo step, or the live object's change sent to the game.
function settleNudge() {
  if (!nudging) return;
  const done = nudging;
  nudging = null;
  clearTimeout(done.timer);
  if (done.edit) done.edit.commit();
  if (done.live) liveStep(live.commit(done.live), "Moved in game.");
}

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
    if (shiftHeld() && m === "raise") m = "lower";
    else if (shiftHeld() && m === "lower") m = "raise";
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
  settleNudge();
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
      const eco = await postJson("api/zone/eco", { zone: state.zone.id, palette: state.newChunk.ecoKey });
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
  const { uid, copyOf, tile, file, position, rotation, scale, material, tintAlias, tint, drawDistance } = record;
  return { uid, copyOf, tile, file, position, rotation, scale, material, tintAlias, tint, drawDistance };
}

// showLive: whether a save the running client takes live also reloads its view (not when it's about to restart).
async function saveAll({ showLive = true } = {}) {
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
    // What went straight to the running client's live files needs no restart, and no unhiding from its packs.
    const published = result.live;
    const sent = published?.updated ?? [];
    const sentNames = new Set(sent.map((name) => name.toLowerCase()));
    const overrides = zone.overrides.filter((name) => !sentNames.has(name.toLowerCase()));
    const notes = [
      zone.mirrored.length ? `Copied ${zone.mirrored.length} into the running servers' folders; the asset server serves them now.` : "",
      overrides.length && unlock?.applied ? `Hid the client's stock copy of ${overrides.length} file(s), so it downloads yours.` : "",
      overrides.length && !unlock?.applied ? "The client is running, so it still reads its stock copies: Test in game closes it and fixes that." : "",
      sent.length ? `Sent ${sent.length} file(s) to the running client.` : "",
      sent.some((name) => name.toLowerCase().endsWith(".gzne")) ? "The zone file changed: it loads when you next enter the zone in game." : "",
      published?.missed.length ? `${published.missed.length} file(s) aren't among the client's live files (it lists them when it starts): Test in game to load them.` : "",
      zone.serverData ? "Zone or house data changed: Test in game with + servers to load it."
        : sent.length && !published.missed.length ? "" : "Test in game restarts the client to load it.",
    ].filter(Boolean);
    toast(`Saved ${unique.length} file(s):\n${unique.slice(0, 12).map((f) => f.split(/[\\/]/).slice(-2).join("/")).join("\n")}${unique.length > 12 ? "\n…" : ""}\n${notes.join("\n")}`, "ok");
    if (sent.length && showLive) showInGame(sent);
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

// After a save reached the running client's live files: its chunks show once the client loads that ground again,
// which it does when the referee's character steps far off and comes back (LiveLayer.refreshView).
async function showInGame(names) {
  const centre = (row, col) => ({ x: (+col + 2) * TILE, z: (+row + 2) * TILE });
  const changed = names.map((name) => name.match(/_(-?\d+)_(-?\d+)\.gc(?:nk|k2)$/i)).filter(Boolean)
    .map(([, row, col]) => centre(row, col));
  if (!changed.length || !state.zone) return;
  try {
    const result = await live.refreshView(changed, state.zone.chunks.map((c) => centre(c.row, c.col)));
    status({
      offline: "In game, go a couple of kilometres off and come back to see it.",
      "no-character": "None of your characters is in the game; the client shows it the next time it loads that ground.",
      elsewhere: `${result.name} is in another zone; the client shows it when they come here.`,
      far: `The client shows it when ${result.name} gets there.`,
      small: "The zone has no ground far enough away to step over to: re-enter it in game to see it.",
    }[result.reason] ?? `Reloaded the ground round ${result.name} in game.`);
  } catch (error) {
    toast(`Couldn't reload it in game: ${error.message}`, "error");
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
    const response = await fetch(`api/draft/chunk${query}`, { method: "POST", body });
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
      await postJson(`api/draft/zone${query}`, JSON.parse(zone));
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
  return postJson(`api/draft/clear?zone=${encodeURIComponent(zone)}`, {}).catch(() => {});
}

// After opening a zone: if an autosave of it outlived the page that wrote it, offer to restore it.
async function offerDraft() {
  const zone = state.zone, query = `zone=${encodeURIComponent(zone.id)}`;
  const draft = await getJson(`api/draft?${query}`).catch(() => null);
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
    const response = await fetch(`api/draft/chunk?${query}&row=${row}&col=${col}`);
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
  // The running game's players (live.js).
  context.fillStyle = "#7cc4ff";
  for (const player of live.visible ? live.players.values() : []) {
    const [u, v] = toMap(player.position.x, player.position.z);
    context.beginPath();
    context.arc(u, v, 3, 0, Math.PI * 2);
    context.fill();
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
  // The minimap shrinks in a small view: its drawing stays at the canvas's own size.
  const rect = event.target.getBoundingClientRect();
  const scale = event.target.width / rect.width;
  const [x, z] = minimapTransform().toWorld((event.clientX - rect.left) * scale, (event.clientY - rect.top) * scale);
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
  // A page that has lost the keyboard (the browser's menu or address bar, a dialog) never hears its keys go up.
  if (held.size && !document.hasFocus()) held.clear();
  const speed = look.speed * (shiftHeld() ? 4 : 1) * dt;
  const typing = isTextEntry(document.activeElement);
  // Not while Ctrl, Alt or Meta is down: those keys are shortcuts then (Ctrl+S saves, it doesn't fly back).
  const chord = ["ControlLeft", "ControlRight", "AltLeft", "AltRight", "MetaLeft", "MetaRight"].some(isHeld);
  if (!typing && !chord) {
    if (isHeld("KeyW")) camera.position.addScaledVector(forward, speed);
    if (isHeld("KeyS")) camera.position.addScaledVector(forward, -speed);
    if (isHeld("KeyD")) camera.position.addScaledVector(right, speed);
    if (isHeld("KeyA")) camera.position.addScaledVector(right, -speed);
    if (isHeld("KeyE")) camera.position.y += speed;
    if (isHeld("KeyQ")) camera.position.y -= speed;
  }
  camera.updateMatrixWorld();

  if (state.zone) {
    terrain.update(camera.position.x, camera.position.z);
    hover = pointer.inside && !(pointer.buttons & 2) ? terrainHit() : null;
    const scattering = state.tool === "props" && state.place.mode === "scatter";
    const brushing = (BRUSH_TOOLS.has(state.tool) || scattering) && hover;
    terrain.uniforms.brush.value.set(hover?.x ?? 0, hover?.z ?? 0, brushing ? state.brush.radius : 0);
    if (stroke && pointer.buttons & 1) {
      applyStroke(dt);
      updateDirty();
    }
    if (scatter && pointer.buttons & 1) scatterStep(dt);
    live.update(dt, camera);
    for (const box of selectionBoxes.values()) box.update();
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

// --- the live game -------------------------------------------------------------------------------------

// The Live switch shows when the editor was opened from the admin panel; its dot says whether this zone is live.
function updateLiveStatus() {
  const control = $("#live-control");
  control.hidden = !live.available;
  $("#live-state").className = live.zone ? (live.connected ? "on" : "off") : "";
  control.title = !live.zone
    ? "This zone isn't running on the game server (only world zones are live)."
    : live.connected
      ? `${live.zone.name} is live: its players, NPCs and placed objects as the game has them now. Placed objects edited here change in game at once.`
      : "Reconnecting to the game server…";
  if (state.tool === "props") renderPanel();
}

$("#show-live").addEventListener("change", (e) => {
  live.setVisible(e.target.checked);
  if (!e.target.checked && isLive(selected)) select(null);
});
live.connect().then((available) => {
  if (available && state.zone) live.open(state.zone.name);
  updateLiveStatus();
});

// --- top bar -------------------------------------------------------------------------------------------

for (const button of document.querySelectorAll("#toolbar button")) button.addEventListener("click", () => setTool(button.dataset.tool));
$("#open-zone").addEventListener("click", () => openZone($("#zone-search").value.trim()).then(buildWalls));
$("#open-file").addEventListener("click", openZoneFile);
$("#zone-search").addEventListener("keydown", (e) => { if (e.key === "Enter") openZone(e.target.value.trim()).then(buildWalls); });
$("#save").addEventListener("click", saveAll);
$("#play").addEventListener("click", async () => {
  if (dirtyChunks().length || state.zoneDirty) await saveAll({ showLive: false });
  const servers = $("#play-servers").checked;
  $("#play").disabled = true;
  toast(servers ? "Restarting the servers and the client…" : "Restarting the client…");
  try {
    // The chunks in view go to the client as live files, so saving them later shows in game without a restart.
    const cells = [...terrain.chunks.values()].map((chunk) => [chunk.row, chunk.col]);
    const result = await postJson("api/play", { servers, zone: state.zone?.id, cells });
    const unlock = result.unlock?.applied
      ? (result.unlock.hidden.length ? `\nHid ${result.unlock.hidden.length} stock file(s) from the client's packs.` : "")
      : `\n${result.unlock?.message || ""}`;
    const liveNote = result.live?.chunks ? `\nSaves to the ${result.live.chunks} chunks round the view now show in game without a restart.` : "";
    toast(`${result.ok ? (servers ? "Servers and client restarted." : "Client restarted.") : "launch.ps1 failed:"}${unlock}${liveNote}\n${result.output.split("\n").slice(-4).join("\n")}`, result.ok ? "ok" : "error");
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

// The View menu: view distance, time of day and what's drawn. A click anywhere else closes it.
function setViewMenu(open) {
  $("#view-menu").hidden = !open;
  $("#view-button").setAttribute("aria-expanded", String(open));
}
$("#view-button").addEventListener("click", () => setViewMenu($("#view-menu").hidden));
document.addEventListener("pointerdown", (event) => {
  if (!$("#view-menu").hidden && !event.target.closest(".menu")) setViewMenu(false);
});
$("#keys-button").addEventListener("click", (event) => { toggleKeysSheet(); event.currentTarget.blur(); });
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
$("#new-zone-cancel").addEventListener("click", () => dialog.close("cancel"));
// Names are file names: lowercase, with _ for spaces and dashes; what's still wrong is said, not just refused.
dialog.querySelector("input[name=name]").addEventListener("input", (event) => {
  const input = event.target;
  const fixed = input.value.toLowerCase().replace(/[\s-]+/g, "_").replace(/[^a-z0-9_]/g, "");
  if (fixed !== input.value) input.value = fixed;
  input.setCustomValidity(!fixed || /^[a-z][a-z0-9_]{2,47}$/.test(fixed) ? ""
    : /^[a-z]/.test(fixed) ? "3 to 48 letters, digits and _." : "Start with a letter.");
});
dialog.addEventListener("close", async () => {
  if (dialog.returnValue !== "create") return;
  const form = dialog.querySelector("form");
  status(`Creating ${form.name.value}…`);
  try {
    const zone = await postJson("api/zone/new", {
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

// --- layout: full screen, the side panel and the minimap ----------------------------------------------

const LAYOUT_KEY = "zone-editor-layout";
const PANEL_MIN = 240, PANEL_MAX = 720;
const layout = { panelWidth: 320, panelHidden: false, minimapHidden: false };
try { Object.assign(layout, JSON.parse(localStorage.getItem(LAYOUT_KEY) || "{}")); } catch { /* private mode */ }

function saveLayout() {
  try { localStorage.setItem(LAYOUT_KEY, JSON.stringify(layout)); } catch { /* private mode */ }
}

function applyLayout() {
  document.body.style.setProperty("--panel-width", `${layout.panelHidden ? 0 : layout.panelWidth}px`);
  document.body.classList.toggle("panel-hidden", layout.panelHidden);
  $("#panel-toggle").textContent = layout.panelHidden ? "‹" : "›";
  $("#panel-toggle").title = `${layout.panelHidden ? "Show" : "Hide"} the side panel (\\)`;
  $("#minimap-box").classList.toggle("collapsed", layout.minimapHidden);
  $("#minimap-toggle").textContent = layout.minimapHidden ? "Map ▴" : "▾";
  $("#minimap-toggle").title = layout.minimapHidden ? "Show the minimap" : "Hide the minimap";
}

function togglePanel() {
  layout.panelHidden = !layout.panelHidden;
  applyLayout();
  saveLayout();
}

// The editor's own page goes full screen (in the panel's frame too, which allows it).
function toggleFullscreen() {
  if (document.fullscreenElement) return void document.exitFullscreen();
  const refused = () => toast("Full screen didn't start here: open the editor in its own tab, or use the browser's own.", "error");
  document.documentElement.requestFullscreen().catch(refused);
  // Some embedded browsers neither grant nor refuse it.
  setTimeout(() => { if (!document.fullscreenElement) refused(); }, 1500);
}

function setupLayout() {
  applyLayout();
  const button = $("#fullscreen");
  button.addEventListener("click", () => { toggleFullscreen(); button.blur(); });
  document.addEventListener("fullscreenchange", () => {
    const on = !!document.fullscreenElement;
    button.classList.toggle("active", on);
    button.title = on ? "Leave full screen (F11 or Esc)" : "Full screen (F11)";
  });
  $("#panel-toggle").addEventListener("click", (event) => { event.stopPropagation(); togglePanel(); event.currentTarget.blur(); });
  $("#minimap-toggle").addEventListener("click", (event) => {
    layout.minimapHidden = !layout.minimapHidden;
    applyLayout();
    saveLayout();
    event.currentTarget.blur();
  });

  // Dragging the panel's edge sets its width (and brings a hidden panel back).
  const handle = $("#panel-handle");
  handle.addEventListener("pointerdown", (event) => {
    if (event.target !== handle || event.button !== 0) return;
    handle.setPointerCapture(event.pointerId);
    handle.classList.add("dragging");
    const move = (e) => {
      const right = document.body.getBoundingClientRect().right;
      layout.panelWidth = Math.round(Math.max(PANEL_MIN, Math.min(PANEL_MAX, right - e.clientX)));
      layout.panelHidden = false;
      applyLayout();
    };
    const end = () => {
      handle.classList.remove("dragging");
      handle.removeEventListener("pointermove", move);
      handle.removeEventListener("lostpointercapture", end);
      saveLayout();
    };
    handle.addEventListener("pointermove", move);
    handle.addEventListener("lostpointercapture", end);
  });
}

// For poking at the editor from the browser console.
window.zoneEditor = { THREE, scene, camera, look, gizmo, terrain, props, library, live, state, selection, openZone, saveAll, addChunk, setTool, select, selectMany };

setupLayout();
setupTransformBar();
noZonePanel();
updateDirty();
loadZones().then(() => {
  let last = null;
  try { last = localStorage.getItem("zone-editor-zone"); } catch { /* private mode */ }
  $("#zone-search").value = last || "FabledRealms";
}).catch((error) => toast(error.message, "error"));
requestAnimationFrame(frame);
