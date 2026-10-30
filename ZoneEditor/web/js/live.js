// The live game, when the editor runs inside the OSFR admin panel (its Map editor tab): the running server's players,
// NPCs, mobs and placed objects (props, world NPCs and placed models) in the scene, drawn from their own Forgelight
// actors as the client puts them together, and the placed objects edited live. Each change goes to the Gateway's
// admin API through the panel (/api/game/*, on the panel's own origin), which saves it and shows it in game at once to
// everyone in view. Opened on its own, the editor has no /api/game and the layer stays off.
//
// Server headings, pitch and roll are the editor's yaw, pitch and roll (radians, three's 'YXZ' order).
import * as THREE from "three";
import { characterRig, finishRig, playClip, skinOn } from "./animation.js";
import { getJson, postJson } from "./api.js";
import { applyTransform, variantTexture } from "./props.js";
import { loadTexture } from "./textures.js";

const GAME = "/api/game";
// Sent with every change; the panel refuses changes without it.
const PANEL_HEADER = { "X-Admin-Panel": "1" };
const PLAYER_GLIDE = 0.25;
// The zone's own NPCs (thousands in FabledRealms) are drawn this close to the camera; names closer still.
const NPC_RANGE = 220;
const LABEL_RANGE = 70;
const RANGE_CHECK = 0.5;
// Placed objects, which can be edited here, glow faintly so they stand out from the zone's baked props.
const LIVE_GLOW = 0x0b4a57;
// After a save reaches the client's live files, the referee's own character goes to ground at least this far from the
// saved chunks for a moment and straight back, so the client drops them and reads them again. It keeps a chunk it
// has within about a kilometre (only re-placing its props), and with no ground to stand on it drops nothing. Only
// when the character stands this close to one; farther off, the client reads the new files when it gets there.
const REFRESH_AWAY = 2048;
const REFRESH_PAUSE = 2000;
const REFRESH_NEAR = 1500;
// Milliseconds before reopening a dropped feed, longer each try.
const RECONNECT_DELAYS = [1000, 2000, 5000, 10000];
// The client's tint shaders double the colour in gamma space (a mid-grey texture shows the tint as it is); the
// viewport lights in linear space, where that's 2^2.2.
const MODULATE_2X = Math.pow(2, 2.2);
const HAIR_SLOTS = new Set(["CustomizationHair", "CustomizationModel"]);

async function request(path, method = "GET", body) {
  const response = await fetch(GAME + path, {
    method,
    credentials: "same-origin",
    headers: method === "GET" ? {} : { ...PANEL_HEADER, "Content-Type": "application/json" },
    body: body === undefined ? undefined : JSON.stringify(body),
  });
  const text = await response.text();
  let data = null;
  try { data = text ? JSON.parse(text) : null; } catch { data = text; }
  if (!response.ok) throw new Error(typeof data === "string" ? data : data?.detail || data?.title || `The game server answered ${response.status}.`);
  return data;
}

// --- looks ---------------------------------------------------------------------------------------------------

// Puts characters and props together from a look (the feed's parts: actor, texture aliases, tint alias, Tints.xml
// id, slot), as the client does from AddPc and AddNpc: an .agr lists more parts and its own hair tint; every part's
// meshes share the body's skeleton in its bind pose, except gear with an attach point (a weapon), which is fastened
// to that bone.
class LookBuilder {
  constructor(library) {
    this.library = library;
    this.sets = new Map();
    this.skeletons = new Map();
    this.materials = new Map();
    this.tints = null;
  }

  tintColours() {
    this.tints ??= getJson("api/tints").catch(() => ({}));
    return this.tints;
  }

  actorSet(file) {
    const key = file.toLowerCase();
    if (!this.sets.has(key)) this.sets.set(key, getJson(`api/actor-set?name=${encodeURIComponent(file)}`));
    return this.sets.get(key);
  }

  skeleton(name) {
    const key = name.toLowerCase();
    if (!this.skeletons.has(key)) this.skeletons.set(key, getJson(`api/skeleton?name=${encodeURIComponent(name)}`).catch(() => null));
    return this.skeletons.get(key);
  }

  // The look's parts with every .agr opened up: its actors, wearing its hair tint on the hair and facial hair.
  async parts(look) {
    const parts = [];
    for (const part of look) {
      if (!part.file.toLowerCase().endsWith(".agr")) { parts.push(part); continue; }
      let set;
      try { set = await this.actorSet(part.file); } catch (error) { console.warn(`Live: ${part.file}: ${error.message}`); continue; }
      const hair = set.tints?.HairTint ?? 0;
      for (const actor of set.actors) {
        parts.push({ file: actor.file, textures: actor.textures, tint: actor.tint, tintId: takesHairTint(actor) ? hair : 0, slot: actor.slot });
      }
    }
    return parts;
  }

  async build(look, { glow = false } = {}) {
    const parts = await this.parts(look);
    const [templates, tints] = await Promise.all([Promise.all(parts.map((p) => this.library.model(p.file))), this.tintColours()]);
    const body = templates[0];
    const group = new THREE.Group();
    // A body with a skeleton and an idle clip stands about as in game: every part skinned to the body's skeleton,
    // rigid gear (a weapon's R_weapon, a parachute's backpack) on its bone, and the body's loc_stand playing.
    const rig = body && !body.userData.missing && body.userData.skinned && body.userData.idle
      ? await characterRig(body.userData.skeletonName).catch(() => null) : null;
    if (rig) finishRig(rig, group);
    // Otherwise the bind pose, with gear fastened where its bone is in it. Clothes, and pixie wings, are already
    // where they go in the bind pose. The body comes first, in a player's look and in an .agr; a lone actor fastens
    // to nothing.
    const fastened = !rig && templates.length > 1 && templates.slice(1).some((t) => Object.keys(t.userData.attach).length);
    const skeletonName = fastened ? body.userData.skeleton : null;
    const bones = skeletonName ? (await this.skeleton(skeletonName))?.bones : null;
    for (const [i, part] of parts.entries()) {
      const template = templates[i];
      if (template.userData.missing) { console.warn(`Live: no actor ${part.file}`); continue; }
      const node = new THREE.Group();
      const bone = i > 0 ? Object.values(template.userData.attach)[0]?.toUpperCase() : null;
      const holder = rig && bone && rig.index.has(bone) ? rig.bones[rig.index.get(bone)] : group;
      const matrix = !rig && bone && bones?.[bone];
      if (matrix) {
        node.matrixAutoUpdate = false;
        node.matrix.fromArray(matrix);
      }
      const colour = tintColour(part, template.userData.aliases, tints);
      for (const child of template.children) {
        if (!child.isMesh || !child.userData.base) continue;
        const material = this.material(child.userData.base, template.userData.aliases, part.textures, colour, glow);
        let mesh;
        if (rig && child.isSkinnedMesh && holder === group) {
          mesh = new THREE.SkinnedMesh(await skinOn(child.geometry, template.userData.skeletonName, rig), material);
          mesh.frustumCulled = false;
          mesh.bind(rig.skeleton, new THREE.Matrix4());
        } else {
          mesh = new THREE.Mesh(child.geometry, material);
        }
        mesh.castShadow = child.castShadow;
        mesh.receiveShadow = true;
        node.add(mesh);
      }
      holder.add(node);
    }
    if (rig) playClip(group, body.userData.idle, body.userData.skeletonName);
    return group;
  }

  // A part's material: its texture variants, face paint over the skin, and its tint through its mask (or over the
  // whole mesh, as hair takes its colour).
  material(base, aliases, textures, colour, glow) {
    const diffuse = variantTexture(aliases, textures, base.texture);
    const overlay = diffuse && base.overlay ? variantTexture(aliases, textures, base.overlay) : null;
    const mask = diffuse && colour && base.mask ? variantTexture(aliases, textures, base.mask) : null;
    const tinted = !!(diffuse && colour);
    const key = [diffuse, overlay, mask, tinted && colour.join(","), base.alpha, glow].join("|");
    if (this.materials.has(key)) return this.materials.get(key);

    const alpha = base.alpha;
    const material = new THREE.MeshLambertMaterial({
      color: diffuse ? 0xffffff : 0xb0b0b0, side: alpha ? THREE.DoubleSide : THREE.FrontSide,
      alphaTest: alpha === "test" ? 0.4 : 0, transparent: alpha === "blend" || alpha === "water", opacity: alpha === "blend" ? 0.75 : alpha === "water" ? 0.62 : 1,
      depthWrite: alpha !== "blend" && alpha !== "water", map: diffuse ? loadTexture(diffuse) : null,
    });
    if (glow) material.emissive.setHex(LIVE_GLOW);
    if (overlay || tinted) {
      const tint = tinted ? new THREE.Color().setRGB(colour[0], colour[1], colour[2], THREE.SRGBColorSpace).multiplyScalar(MODULATE_2X) : new THREE.Color(1, 1, 1);
      const overlayMap = overlay ? loadTexture(overlay) : null;
      const maskMap = mask ? loadTexture(mask, { srgb: false }) : null;
      material.onBeforeCompile = (shader) => {
        shader.uniforms.overlayMap = { value: overlayMap };
        shader.uniforms.maskMap = { value: maskMap };
        shader.uniforms.tintColour = { value: tint };
        shader.fragmentShader = shader.fragmentShader
          .replace("#include <common>", "#include <common>\nuniform sampler2D overlayMap;\nuniform sampler2D maskMap;\nuniform vec3 tintColour;")
          .replace("#include <map_fragment>", [
            "#include <map_fragment>",
            overlay ? "vec4 overlayTexel = texture2D(overlayMap, vMapUv); diffuseColor.rgb = mix(diffuseColor.rgb, overlayTexel.rgb, overlayTexel.a);" : "",
            tinted ? `diffuseColor.rgb *= mix(vec3(1.0), tintColour, ${mask ? "texture2D(maskMap, vMapUv).r" : "1.0"});` : "",
          ].join("\n"));
      };
      material.customProgramCacheKey = () => `live-${!!overlay}-${!!mask}-${tinted}`;
    }
    this.materials.set(key, material);
    return material;
  }
}

// Hair, and a human's facial hair, take the hair colour. The same customization slot holds a pixie's wings, which
// keep their own.
function takesHairTint(actor) {
  return HAIR_SLOTS.has(actor.slot) && actor.file.toLowerCase().includes("hair");
}

// A part's tint: its tint alias's colour on the actor, else its Tints.xml colour (a dye, a hair colour); "dyetint"
// is the alias that says the dye decides.
function tintColour(part, aliases, tints) {
  const alias = part.tint && part.tint.toLowerCase() !== "dyetint"
    ? aliases?.tints?.find((t) => t.name.toLowerCase() === part.tint.toLowerCase()) : null;
  if (alias) return alias.colour;
  return part.tintId > 0 ? tints[String(part.tintId)] ?? null : null;
}

// --- the layer -----------------------------------------------------------------------------------------------

export class LiveLayer {
  // hooks: { toast(text, kind), status(), changed(object) when a live object moved under the selection, removed(object) }
  constructor(scene, library, hooks) {
    this.scene = scene;
    this.library = library;
    this.hooks = hooks;
    this.builder = new LookBuilder(library);
    this.group = new THREE.Group();
    this.group.name = "live";
    scene.add(this.group);
    this.available = false;     // the panel's game API answers
    this.zone = null;           // the server's zone this editor zone is, when it's running
    this.zones = [];
    this.spawnable = new Set(); // lowercase actor files the server can place as models
    this.looks = new Map();     // look key -> parts
    this.objects = new Map();   // id -> THREE.Group, kind "live": placed objects, editable
    this.players = new Map();   // id -> THREE.Group, kind "live-player"
    this.npcs = new Map();      // id -> the zone's other NPCs as the feed has them
    this.drawn = new Map();     // id -> THREE.Group, kind "live-npc": those near the camera
    this.held = null;           // the object being dragged: its server updates wait
    this.source = null;
    this.connected = false;
    this.following = null;      // the zone name the feed follows, kept while it reconnects
    this.retry = null;
    this.retries = 0;
    this.visible = true;
    this.sinceRangeCheck = RANGE_CHECK;
  }

  // Whether the panel's game API is there, and what it can place.
  async connect() {
    try {
      this.zones = await request("/zones");
      this.spawnable = new Set(await request("/models"));
      this.available = true;
    } catch {
      this.available = false;
    }
    return this.available;
  }

  // Follows the server's zone with this name, if it's a running world zone.
  open(name) {
    this.close();
    this.following = name;
    this.retries = 0;
    return this.follow();
  }

  follow() {
    const name = this.following;
    if (!this.available || !name) return null;
    this.zone = this.zones.find((z) => z.editable && z.name.toLowerCase() === name.toLowerCase()) ?? null;
    if (!this.zone) { this.hooks.status?.(); return null; }

    const source = new EventSource(`${GAME}/zones/${encodeURIComponent(this.zone.key)}/live?npcs=true`);
    this.source = source;
    source.onopen = () => { this.connected = true; this.retries = 0; this.hooks.status?.(); };
    source.onerror = () => {
      this.connected = false;
      this.hooks.status?.();
      // The browser retries a dropped stream by itself, but not one the panel answered with an error (the Gateway
      // down or restarting).
      if (source.readyState === EventSource.CLOSED) this.reconnect();
    };
    source.addEventListener("looks", (event) => this.addLooks(JSON.parse(event.data)));
    source.addEventListener("players", (event) => this.updatePlayers(JSON.parse(event.data)));
    source.addEventListener("objects", (event) => this.updateObjects(JSON.parse(event.data)));
    source.addEventListener("npcs", (event) => this.updateNpcs(JSON.parse(event.data)));
    source.addEventListener("closed", () => this.reconnect());
    this.hooks.status?.();
    return this.zone;
  }

  // The feed ended (the Gateway restarted, the zone stopped): it's opened again, a little later each time until the
  // server answers, and its first updates have everyone and everything again, so the view never stays out of step.
  reconnect() {
    if (this.retry || !this.following) return;
    this.source?.close();
    this.source = null;
    this.connected = false;
    this.hooks.status?.();
    const delay = RECONNECT_DELAYS[Math.min(this.retries++, RECONNECT_DELAYS.length - 1)];
    this.retry = setTimeout(async () => {
      this.retry = null;
      const name = this.following;
      if (!name) return;
      // The zones again: a restarted server may run them under new keys, or not yet.
      await this.connect();
      if (this.following !== name) return;
      if (!this.follow()) this.reconnect();
    }, delay);
  }

  close() {
    this.following = null;
    clearTimeout(this.retry);
    this.retry = null;
    this.source?.close();
    this.source = null;
    this.connected = false;
    this.zone = null;
    for (const map of [this.objects, this.players, this.drawn]) {
      for (const node of map.values()) this.group.remove(node);
      map.clear();
    }
    this.npcs.clear();
    this.looks.clear();
    this.hooks.status?.();
  }

  setVisible(visible) {
    this.visible = visible;
    this.group.visible = visible;
  }

  canPlace(file) {
    return this.spawnable.has(file.toLowerCase());
  }

  // What the server can place, again: the Gateway reloads Models.txt when it changes (add_model_rows.py).
  async refreshModels() {
    try { this.spawnable = new Set(await request("/models")); } catch { /* keeps the last list */ }
  }

  // The nearest live thing under the ray: a placed object, a player or one of the zone's NPCs.
  pick(raycaster) {
    if (!this.visible) return null;
    const nodes = [...this.objects.values(), ...this.players.values(), ...this.drawn.values()];
    for (const hit of raycaster.intersectObjects(nodes, true)) {
      if (hit.object.isSprite) continue;
      const node = hit.object.userData.node;
      if (node) return { object: node, distance: hit.distance };
    }
    return null;
  }

  // Players glide between the server's updates; the zone's NPCs and names come and go with the camera.
  update(dt, camera) {
    for (const player of this.players.values()) {
      const glide = player.userData.glide;
      glide.t = Math.min(glide.t + dt / PLAYER_GLIDE, 1);
      player.position.lerpVectors(glide.from, glide.to, glide.t);
    }
    for (const npc of this.drawn.values()) {
      // One being edited stays where the editor puts it.
      if (npc === this.held || npc.userData.pinned) continue;
      const glide = npc.userData.glide;
      glide.t = Math.min(glide.t + dt, 1);
      npc.position.lerpVectors(glide.from, glide.to, glide.t);
    }
    this.sinceRangeCheck += dt;
    if (this.sinceRangeCheck < RANGE_CHECK || !this.zone) return;
    this.sinceRangeCheck = 0;
    this.drawNear(camera.position);
    for (const node of [...this.players.values(), ...this.drawn.values()]) {
      const label = node.userData.labelSprite;
      if (label) label.visible = node.position.distanceTo(camera.position) < (node.userData.kind === "live-player" ? LABEL_RANGE * 3 : LABEL_RANGE);
    }
  }

  // --- the feed ------------------------------------------------------------------------------------------

  addLooks(looks) {
    for (const [key, parts] of Object.entries(looks)) this.looks.set(key, parts);
    // Anything that arrived before its look gets it now.
    for (const node of [...this.objects.values(), ...this.players.values(), ...this.drawn.values()]) {
      if (node.userData.wantLook && this.looks.has(node.userData.wantLook)) this.dress(node, node.userData.wantLook);
    }
  }

  // Puts the look's actors on a node (again, when its look changed: new clothes).
  dress(node, key) {
    node.userData.wantLook = null;
    if (node.userData.look === key) return;
    node.userData.look = key;
    const look = key && this.looks.get(key);
    if (key && !look) { node.userData.wantLook = key; node.userData.look = null; return; }
    if (!look) return;
    this.builder.build(look, { glow: node.userData.kind === "live" }).then((body) => {
      if (node.userData.look !== key || !node.parent) return;
      if (node.userData.body) node.remove(node.userData.body);
      for (const mesh of body.children.flatMap((part) => part.children)) mesh.userData = node.userData;
      node.userData.body = body;
      node.add(body);
      this.hooks.changed?.(node);
    }).catch((error) => console.warn(`Live: ${error.message}`));
  }

  updatePlayers(list) {
    const seen = new Set();
    for (const data of list) {
      seen.add(data.id);
      let player = this.players.get(data.id);
      if (!player) {
        player = this.addNode(this.players, data, "live-player", data.name, "#7cc4ff");
        player.position.set(data.x, data.y, data.z);
      }
      player.userData.live = data;
      const glide = player.userData.glide;
      glide.from.copy(player.position);
      glide.to.set(data.x, data.y, data.z);
      glide.t = 0;
      player.rotation.y = data.h;
      this.dress(player, data.look);
    }
    this.drop(this.players, seen);
  }

  updateObjects(list) {
    const seen = new Set();
    for (const data of list) {
      seen.add(data.id);
      let object = this.objects.get(data.id);
      if (!object) {
        object = this.addNode(this.objects, data, "live", null);
        object.userData.apply = () => this.commit(object);
        this.place(object, data);
        this.dress(object, data.look);
        continue;
      }
      const before = object.userData.live;
      object.userData.live = data;
      object.userData.label = data.label;
      this.dress(object, data.look);
      if (object === this.held) continue;
      if (["x", "y", "z", "h", "pitch", "roll", "scale"].some((k) => Math.abs(before[k] - data[k]) > 1e-3)) {
        this.place(object, data);
        this.hooks.changed?.(object);
      }
    }
    this.drop(this.objects, seen);
  }

  updateNpcs(changes) {
    if (changes.reset) {
      this.npcs.clear();
      this.drop(this.drawn, new Set());
    }
    for (const npc of changes.upsert) {
      this.npcs.set(npc.id, npc);
      const node = this.drawn.get(npc.id);
      if (!node) continue;
      node.userData.live = npc;
      node.userData.label = npc.label;
      node.scale.setScalar(npc.scale || 1);
      this.dress(node, npc.look);
      // The feed's numbers are rounded: one pinned to its spawn line stays put unless it went somewhere else.
      const pinned = node.userData.pinned;
      if (node === this.held || pinned && ["x", "y", "z"].every((k) => Math.abs(npc[k] - pinned[k]) < 0.11)) continue;
      node.userData.pinned = null;
      const glide = node.userData.glide;
      glide.from.copy(node.position);
      glide.to.set(npc.x, npc.y, npc.z);
      glide.t = 0;
      node.rotation.set(npc.pitch ?? 0, npc.h, npc.roll ?? 0, "YXZ");
    }
    for (const id of changes.remove) this.npcs.delete(id);
    this.drop(this.drawn, new Set([...this.drawn.keys()].filter((id) => this.npcs.has(id))));
  }

  // Draws the zone's NPCs near the camera and lets go of those that fell behind.
  drawNear(position) {
    const near = new Set();
    for (const npc of this.npcs.values()) {
      const dx = npc.x - position.x, dz = npc.z - position.z;
      if (dx * dx + dz * dz > NPC_RANGE * NPC_RANGE) continue;
      near.add(npc.id);
      this.drawNpc(npc);
    }
    this.drop(this.drawn, near);
  }

  drawNpc(npc) {
    if (this.drawn.has(npc.id)) return this.drawn.get(npc.id);
    const named = npc.kind === "npc" || npc.kind === "mob";
    const node = this.addNode(this.drawn, npc, "live-npc", named ? npc.label : null, npc.kind === "mob" ? "#ff8a7a" : "#e6edf3");
    node.position.set(npc.x, npc.y, npc.z);
    node.userData.glide.from.copy(node.position);
    node.userData.glide.to.copy(node.position);
    node.rotation.set(npc.pitch ?? 0, npc.h, npc.roll ?? 0, "YXZ");
    node.scale.setScalar(npc.scale || 1);
    this.dress(node, npc.look);
    return node;
  }

  // Players, placed objects and the zone's NPCs whose name (or a placed model's file) has every word, for the
  // Select tool's search; NPCs far from the camera are drawn when picked from it.
  find(words) {
    if (!this.visible || !words.length) return [];
    const match = (text) => words.every((w) => text.toLowerCase().includes(w));
    const found = [];
    for (const node of this.players.values()) if (match(node.userData.label)) found.push({ label: node.userData.label, kind: "player", node });
    for (const node of this.objects.values()) {
      if (match(`${node.userData.label} ${node.userData.live.model ?? ""}`)) found.push({ label: node.userData.label, kind: node.userData.live.kind, node });
    }
    for (const npc of this.npcs.values()) {
      if (npc.kind !== "node" && npc.kind !== "coin" && match(npc.label)) found.push({ label: npc.label, kind: npc.kind, npc });
    }
    return found;
  }

  // A found entry's node, drawing an NPC that's out of range.
  reveal(entry) {
    return entry.node ?? this.drawNpc(entry.npc);
  }

  addNode(map, data, kind, label, colour) {
    const node = new THREE.Group();
    // Its meshes share this userData, so a hit on any of them finds the node (as props.js's record.object does).
    node.userData = { kind, node, live: data, label: data.label ?? data.name, look: null, wantLook: null, glide: { from: new THREE.Vector3(), to: new THREE.Vector3(), t: 1 } };
    if (label) {
      const sprite = makeLabel(label, colour);
      node.userData.labelSprite = sprite;
      node.add(sprite);
    }
    map.set(data.id, node);
    this.group.add(node);
    return node;
  }

  drop(map, keep) {
    for (const [id, node] of map) {
      if (keep.has(id)) continue;
      this.group.remove(node);
      map.delete(id);
      this.hooks.removed?.(node);
    }
  }

  place(object, data) {
    applyTransform(object, { position: [data.x, data.y, data.z], rotation: [data.h, data.pitch, data.roll], scale: data.scale });
  }

  // What a selected NPC or player is made of, for the inspector.
  lookFiles(node) {
    return (this.looks.get(node.userData.look) ?? []).map((part) => part.file);
  }

  // --- edits ---------------------------------------------------------------------------------------------

  base() {
    if (!this.zone) throw new Error("This zone isn't running on the game server.");
    return `/zones/${encodeURIComponent(this.zone.key)}`;
  }

  async send(action, path, method, body) {
    try {
      return (await request(path, method, body)) ?? true;
    } catch (error) {
      this.hooks.toast(`${action}: ${error.message}`, "error");
      return false;
    }
  }

  // Sends where the gizmo left an object: its place, yaw, pitch, roll and scale.
  async commit(object) {
    object.rotation.reorder("YXZ");
    const { x, y, z } = object.position;
    const done = await this.send("Couldn't move it", `${this.base()}/objects/${object.userData.live.id}`, "PATCH", {
      x, y, z, h: object.rotation.y, pitch: object.rotation.x, roll: object.rotation.z,
      scale: (object.scale.x + object.scale.y + object.scale.z) / 3,
    });
    this.held = null;
    if (!done) this.place(object, object.userData.live);
    return !!done;
  }

  // Places an actor in the live world where a prop record says (position, [yaw, pitch, roll], scale); it shows up
  // through the feed.
  async placeModel(file, record) {
    const [x, y, z] = record.position;
    const [h, pitch, roll] = record.rotation;
    return !!(await this.send(`Couldn't place ${file}`, `${this.base()}/models`, "POST", { model: file, x, y, z, h, pitch, roll, scale: record.scale }));
  }

  async duplicate(object) {
    const data = object.userData.live;
    return !!(await this.send("Couldn't copy it", `${this.base()}/objects/${data.id}/copy`, "POST", { x: data.x - 2, z: data.z, h: data.h }));
  }

  async remove(object) {
    return !!(await this.send("Couldn't delete it", `${this.base()}/objects/${object.userData.live.id}`, "DELETE"));
  }

  // --- the zone's own NPCs (admins): the lines in the zone's Lua scripts that spawn them -------------------------

  // Where one of the zone's NPCs starts, and the line that spawns it there: { spawn, line }, line being the editor
  // server's answer ({ found, file, line, text, deletable }, or { found: false, scripts }).
  async zoneNpc(node) {
    const id = node.userData.live.id;
    const spawn = await request(`${this.base()}/zone-npcs/${id}`);
    const line = await postJson("api/zone-npcs/find", { zone: this.zone.name, guid: id, def: spawn.def, spawn });
    return { spawn, line };
  }

  // Holds a zone NPC where its spawn line puts it, to the digit (the feed rounds), while it's selected.
  pin(node, spawn) {
    node.userData.pinned = spawn;
    node.position.set(spawn.x, spawn.y, spawn.z);
    node.rotation.set(spawn.pitch, spawn.h, spawn.roll, "YXZ");
    const glide = node.userData.glide;
    glide.from.copy(node.position);
    glide.to.copy(node.position);
    glide.t = 1;
  }

  // Changes a zone NPC's spawn line (op: move, delete), then the game (game: [path, method, body]); a refused
  // game change puts the line back. Answers { edit, result } for the undo step, or null.
  async zoneNpcEdit(action, node, op, extra, game) {
    const { spawn } = node.userData.zoneNpc;
    let edit;
    try {
      edit = await postJson("api/zone-npcs/edit", { zone: this.zone.name, op, guid: node.userData.live.id, def: spawn.def, spawn, ...extra });
    } catch (error) {
      this.hooks.toast(`${action}: ${error.message}`, "error");
      return null;
    }
    const result = await this.send(action, ...game);
    if (!result) {
      await this.undoLine(edit).catch((error) => this.hooks.toast(`Couldn't put the spawn line back: ${error.message}`, "error"));
      return null;
    }
    return { edit, result };
  }

  // Puts back the line an edit changed (a move) or took out (a delete).
  undoLine(edit) {
    return postJson("api/zone-npcs/edit", edit.was !== undefined
      ? { zone: this.zone.name, op: "revert", file: edit.file, line: edit.line, now: edit.now, was: edit.was }
      : { zone: this.zone.name, op: "restore", file: edit.file, line: edit.line, text: edit.text });
  }

  // Moves or turns a zone NPC to where the gizmo or inspector left it. Answers the undo step, or null.
  async moveZoneNpc(node) {
    node.rotation.reorder("YXZ");
    const info = node.userData.zoneNpc;
    const to = { x: node.position.x, y: node.position.y, z: node.position.z, h: node.rotation.y, pitch: node.rotation.x, roll: node.rotation.z };
    const id = node.userData.live.id;
    const done = await this.zoneNpcEdit("Couldn't move it", node, "move", { to }, [`${this.base()}/zone-npcs/${id}`, "PATCH", to]);
    this.held = null;
    const from = info.spawn;
    if (!done) {
      if (info.line.found && !info.spawn.walks) this.pin(node, from);
      return null;
    }
    info.spawn = { ...from, ...to };
    info.line = { ...info.line, line: done.edit.line, text: done.edit.text };
    if (!from.walks) this.pin(node, info.spawn);
    return async () => {
      await this.undo();
      await this.undoLine(done.edit);
      info.spawn = from;
      info.line = { ...info.line, text: done.edit.was.trim() };
      if (!from.walks && node.parent) this.pin(node, from);
    };
  }

  // Takes a zone NPC out of the game and its line out of the script. Answers the undo step, or null.
  async removeZoneNpc(node) {
    const done = await this.zoneNpcEdit("Couldn't delete it", node, "delete", {}, [`${this.base()}/zone-npcs/${node.userData.live.id}`, "DELETE"]);
    return done && (async () => { await this.undo(); await this.undoLine(done.edit); });
  }

  // Makes a zone NPC a world NPC where it stands (saved in the database, edited like a placed one), taking its line
  // out of the script. Answers { id, undo }, or null.
  async adoptZoneNpc(node) {
    const done = await this.zoneNpcEdit(`Couldn't make ${node.userData.label} a world NPC`, node, "delete", {},
      [`${this.base()}/zone-npcs/${node.userData.live.id}/adopt`, "POST", {}]);
    return done && { id: done.result.id, undo: async () => { await this.undo(); await this.undoLine(done.edit); } };
  }

  // The referee's last live edit in this zone, undone on the server.
  async undo() {
    const result = await this.send("Couldn't undo", `${this.base()}/undo`, "POST", {});
    return result?.undid ?? null;
  }

  // A player to a spot in this zone (on the ground there).
  async teleport(player, point) {
    return !!(await this.send(`Couldn't move ${player.userData.label}`, `/players/${player.userData.live.id}/teleport`, "POST",
      { zone: this.zone.key, x: point.x, z: point.z }));
  }

  async message(player, text) {
    return !!(await this.send(`Couldn't message ${player.userData.label}`, `/players/${player.userData.live.id}/message`, "POST", { message: text }));
  }

  async kick(player) {
    return !!(await this.send(`Couldn't kick ${player.userData.label}`, `/players/${player.userData.live.id}/kick`, "POST", {}));
  }

  // --- GM scripts (admins): what the in-game GM panel's Script tab does ------------------------------------

  // A placed object's script as saved ({ kind, source, running }); saving compiles and runs it first, so a broken
  // one never replaces a working one, and answers { ok, message, running }.
  scriptPath(object) {
    return `${this.base()}/objects/${object.userData.live.id}/script`;
  }

  getScript(object) {
    return request(this.scriptPath(object));
  }

  saveScript(object, source) {
    return request(this.scriptPath(object), "PUT", { source });
  }

  clearScript(object) {
    return request(this.scriptPath(object), "DELETE");
  }

  // A library script on the object, its parameters from values (the rest their defaults).
  useScript(object, name, values) {
    return request(`${this.scriptPath(object)}/use`, "POST", { name, values });
  }

  // Runs onSpawn, then onInteract on the referee's own character, which has to be in the zone.
  testScript(object) {
    return request(`${this.scriptPath(object)}/test`, "POST", {});
  }

  // The script library: shipped templates (read-only) and referees' saved scripts.
  scriptLibrary() {
    return request("/scripts/library");
  }

  scriptLibraryEntry(name) {
    return request(`/scripts/library/${encodeURIComponent(name)}`);
  }

  saveScriptLibraryEntry(name, source, description) {
    return request(`/scripts/library/${encodeURIComponent(name)}`, "PUT", { source, description });
  }

  deleteScriptLibraryEntry(name) {
    return request(`/scripts/library/${encodeURIComponent(name)}`, "DELETE");
  }

  // --- NPCs: settings, conditions, conversations and definitions ------------------------------------------

  // The referee using the editor ({ id, name, role, character }), fetched once: admins also edit conversations and
  // NPC definitions.
  me() {
    this.referee ??= request("/me").catch((error) => { this.referee = null; throw error; });
    return this.referee;
  }

  // What settings can be set to (animations, mobs, events, quest states, icons, the game clock's hour), fetched once.
  npcOptions() {
    this.options ??= request("/npc-options").catch((error) => { this.options = null; throw error; });
    return this.options;
  }

  objectPath(object) {
    return `${this.base()}/objects/${object.userData.live.id}`;
  }

  // A placed NPC's, model's or prop's name, look, behaviour, event and conditions (ObjectSettings on the server).
  getSettings(object) {
    return request(`${this.objectPath(object)}/settings`);
  }

  // Changes some of them, as one undo step: { id (null when its event despawned it), undo }.
  saveSettings(object, patch) {
    return request(`${this.objectPath(object)}/settings`, "PUT", patch);
  }

  // A world NPC's conversation (admins): { conversation: { start, nodes } or null }.
  getConversation(object) {
    return request(`${this.objectPath(object)}/conversation`);
  }

  saveConversation(object, conversation) {
    return request(`${this.objectPath(object)}/conversation`, "PUT", conversation);
  }

  clearConversation(object) {
    return request(`${this.objectPath(object)}/conversation`, "DELETE");
  }

  // Spawns an NPC from its definition where a prop record says; it shows up through the feed.
  async spawnNpc(def, label, record) {
    const [x, y, z] = record.position;
    return !!(await this.send(`Couldn't spawn ${label}`, `${this.base()}/npcs`, "POST", { def, x, y, z, h: record.rotation[0], scale: record.scale }));
  }

  // A resource the Gateway can reload (admins): "pois", "seasons", ...
  reloadResource(name) {
    return request(`/resources/${encodeURIComponent(name)}/reload`, "POST", {});
  }

  // After Npcs.json changed: the Gateway reloads it (admins), then this zone's NPCs built on def respawn with it.
  async reloadDefinitions(def) {
    await request("/resources/npcs/reload", "POST", {});
    if (def === undefined || !this.zone) return 0;
    return (await request(`${this.base()}/definitions/${def}/respawn`, "POST", {})).respawned;
  }

  // Shows saved chunks in the referee's own client, which reads them from the editor's live files when it loads that
  // ground again: their character steps over to solid ground far from them and back to where it stood. changed are the
  // saved chunks' centres, ground every chunk centre in the zone (somewhere to stand). Says why when it didn't go:
  // "offline" (no game here), "no-character", "elsewhere" (in another zone), "far" (nowhere near them) or "small"
  // (no ground far enough away).
  async refreshView(changed, ground) {
    if (!this.zone) return { refreshed: false, reason: "offline" };
    const me = await request("/me");
    const player = me?.character ? (await request("/players")).find((p) => p.name === me.character) : null;
    if (!player) return { refreshed: false, reason: "no-character" };
    const name = player.name;
    if (player.zone !== this.zone.key) return { refreshed: false, reason: "elsewhere", name };
    const from = (a, b) => Math.hypot(a.x - b.x, a.z - b.z);
    if (!changed.some((c) => from(c, player) < REFRESH_NEAR)) return { refreshed: false, reason: "far", name };
    // Of the ground far enough off, the spot nearest the middle of the zone: its edges can have chunks the client
    // can't load (out of bounds, missing on the asset server), and with nothing loaded under it, it drops nothing.
    const middle = { x: ground.reduce((sum, g) => sum + g.x, 0) / ground.length, z: ground.reduce((sum, g) => sum + g.z, 0) / ground.length };
    const spot = ground
      .filter((g) => from(g, player) >= REFRESH_AWAY && changed.every((c) => from(g, c) >= REFRESH_AWAY))
      .sort((a, b) => from(a, middle) - from(b, middle))[0];
    if (!spot) return { refreshed: false, reason: "small", name };

    const path = `/players/${player.id}/teleport`;
    await request(path, "POST", { zone: this.zone.key, x: spot.x, z: spot.z });
    await new Promise((resolve) => setTimeout(resolve, REFRESH_PAUSE));
    await request(path, "POST", { zone: this.zone.key, x: player.x, y: player.y, z: player.z });
    return { refreshed: true, name };
  }
}

// A name over a player or NPC, as the client's nameplates.
function makeLabel(text, colour) {
  const canvas = document.createElement("canvas");
  const context = canvas.getContext("2d");
  const font = "600 28px Segoe UI, system-ui, sans-serif";
  context.font = font;
  canvas.width = Math.ceil(context.measureText(text).width) + 16;
  canvas.height = 40;
  context.font = font;
  context.textBaseline = "middle";
  context.lineWidth = 6;
  context.strokeStyle = "rgba(0,0,0,0.8)";
  context.strokeText(text, 8, 20);
  context.fillStyle = colour;
  context.fillText(text, 8, 20);
  const sprite = new THREE.Sprite(new THREE.SpriteMaterial({ map: new THREE.CanvasTexture(canvas), depthTest: false, sizeAttenuation: false }));
  sprite.scale.set(0.032 * canvas.width / canvas.height, 0.032, 1);
  sprite.position.y = 2.4;
  sprite.renderOrder = 10;
  return sprite;
}
