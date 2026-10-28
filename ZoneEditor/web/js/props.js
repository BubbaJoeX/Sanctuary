// Props (placed actors), point lights and zone markers in the viewport.
//
// A prop record is what the server sends and gets back: { uid?, copyOf?, file, position, rotation, scale, tile?,
// material?, tintAlias?, tint?, drawDistance? }. Rotation is (yaw, pitch, roll) in radians, applied like
// D3DXMatrixRotationYawPitchRoll: three's 'YXZ' order. material names one of the actor's texture aliases (a
// texture variant) and tintAlias one of its tint aliases, with tint the object's own [r, g, b, 0] (0..1).
import * as THREE from "three";
import { ASSET_VERSION, decodeBase64, getJson } from "./api.js";
import { loadTexture } from "./textures.js";

export class PropLibrary {
  constructor() {
    this.models = new Map();
    this.materials = new Map();
    this.placeholder = new THREE.BoxGeometry(1, 1, 1).translate(0, 0.5, 0);
    this.placeholderMaterial = new THREE.MeshLambertMaterial({ color: 0xff44aa, wireframe: true });
  }

  // alpha is the material's use of its texture's alpha: "test" (cutout), "blend" (glass), "water" (drawn as
  // tinted water: the client's water shader takes wave normal maps) or null (a mask, ignored).
  // tint ([r, g, b], 0..1) multiplies the texture, as an object's tint alias does in game.
  material(texture, alpha, tint = null) {
    const key = `${texture || ""}|${alpha || ""}|${tint ? tint.slice(0, 3).map((v) => v.toFixed(3)).join(",") : ""}`;
    if (alpha === "water" && !this.materials.has(key)) {
      this.materials.set(key, new THREE.MeshPhongMaterial({
        color: 0x2b7d9c, specular: 0x9fd4ff, shininess: 80, transparent: true, opacity: 0.62, depthWrite: false, side: THREE.DoubleSide,
      }));
    }
    if (!this.materials.has(key)) {
      const material = new THREE.MeshLambertMaterial({
        color: texture ? 0xffffff : 0xb0b0b0, side: alpha ? THREE.DoubleSide : THREE.FrontSide,
        alphaTest: alpha === "test" ? 0.4 : 0, transparent: alpha === "blend", opacity: alpha === "blend" ? 0.75 : 1,
        depthWrite: alpha !== "blend", map: texture ? loadTexture(texture) : null,
      });
      if (tint) material.color.setRGB(tint[0], tint[1], tint[2], THREE.SRGBColorSpace);
      this.materials.set(key, material);
    }
    return this.materials.get(key);
  }

  // A template group for an actor, shared by every instance of it.
  model(file) {
    const key = file.toLowerCase();
    if (!this.models.has(key)) {
      this.models.set(key, getJson(`api/model?name=${encodeURIComponent(file)}&v=${ASSET_VERSION}`).then((data) => {
        const group = new THREE.Group();
        for (const mesh of data.meshes) {
          const geometry = new THREE.BufferGeometry();
          geometry.setAttribute("position", new THREE.BufferAttribute(decodeBase64(mesh.positions, Float32Array), 3));
          geometry.setAttribute("uv", new THREE.BufferAttribute(decodeBase64(mesh.uvs, Float32Array), 2));
          geometry.setIndex(new THREE.BufferAttribute(decodeBase64(mesh.indices, Uint32Array), 1));
          if (mesh.normals) geometry.setAttribute("normal", new THREE.BufferAttribute(decodeBase64(mesh.normals, Float32Array), 3));
          else geometry.computeVertexNormals();
          geometry.computeBoundingSphere();
          const part = new THREE.Mesh(geometry, this.material(mesh.texture, mesh.alpha));
          part.castShadow = mesh.alpha !== "blend" && mesh.alpha !== "water";
          part.receiveShadow = true;
          // overlay and mask: a DualTexture material's second layer (face paint) and a TintMask material's mask.
          part.userData.base = { texture: mesh.texture, alpha: mesh.alpha, overlay: mesh.overlay, mask: mesh.mask, shader: mesh.shader };
          group.add(part);
        }
        group.userData.aliases = data.aliases || NO_ALIASES;
        // A character part's skeleton, and the bones gear (a weapon) fastens to.
        group.userData.skeleton = data.skeleton || null;
        group.userData.attach = data.attach || {};
        return group;
      }).catch(() => {
        const group = new THREE.Group();
        group.add(new THREE.Mesh(this.placeholder, this.placeholderMaterial));
        group.userData.aliases = NO_ALIASES;
        group.userData.attach = {};
        group.userData.missing = true;
        return group;
      }));
    }
    return this.models.get(key);
  }
}

const NO_ALIASES = { textures: [], tints: [] };

// The texture variants an actor offers (its texture alias names), when there's a choice.
export function variantNames(aliases) {
  const names = [...new Set((aliases?.textures || []).map((t) => t.name))];
  return names.length > 1 ? names : [];
}

// The texture a mesh draws with under texture alias `material` (none: the actor's default alias). The mesh's
// own texture is whichever of a slot's alias textures the material file lists, so any of them becomes the
// one the alias names there; one the alias already names stays (a tree's two _Default textures). A character
// part wears several (an .agr lists one per slot, e.g. skintone3 and a face paint): each slot takes the first
// of them it has.
export function variantTexture(aliases, material, texture) {
  if (!texture) return texture;
  const entries = aliases?.textures || [];
  const lower = texture.toLowerCase();
  const wanted = (Array.isArray(material) ? material : [material]).filter(Boolean).map((m) => m.toLowerCase());
  for (const slot of new Set(entries.map((t) => t.slot))) {
    const inSlot = entries.filter((t) => t.slot === slot);
    if (!inSlot.some((t) => t.texture.toLowerCase() === lower)) continue;
    const match = wanted.map((w) => inSlot.find((t) => t.name.toLowerCase() === w)).find(Boolean);
    const name = match ? match.name : (inSlot.find((t) => t.default) || inSlot[0]).name;
    const named = inSlot.filter((t) => t.name === name);
    return named.some((t) => t.texture.toLowerCase() === lower) ? texture : named[0].texture;
  }
  return texture;
}

export function applyTransform(object, record) {
  const [x, y, z] = record.position;
  const [yaw, pitch, roll] = record.rotation;
  object.position.set(x, y, z);
  object.rotation.set(pitch, yaw, roll, "YXZ");
  object.scale.setScalar(record.scale ?? 1);
  object.updateMatrixWorld(true);
}

export function readTransform(object, record) {
  object.rotation.reorder("YXZ");
  record.position = [object.position.x, object.position.y, object.position.z];
  record.rotation = [object.rotation.y, object.rotation.x, object.rotation.z];
  record.scale = (object.scale.x + object.scale.y + object.scale.z) / 3;
}

export class PropLayer {
  constructor(scene, library) {
    this.scene = scene;
    this.library = library;
    this.groups = new Map();           // chunk key -> THREE.Group holding its props and lights
    this.pickables = [];
    this.lightMaterial = (rgb) => new THREE.MeshBasicMaterial({ color: new THREE.Color(rgb[0] / 255, rgb[1] / 255, rgb[2] / 255) });
    this.lightGeometry = new THREE.SphereGeometry(0.8, 12, 8);
    this.showLights = true;
    this.hiddenFiles = new Set();      // lowercase actor names hidden in the view
  }

  groupFor(chunk) {
    if (!this.groups.has(chunk.key)) {
      const group = new THREE.Group();
      group.userData.chunkKey = chunk.key;
      this.scene.add(group);
      this.groups.set(chunk.key, group);
    }
    return this.groups.get(chunk.key);
  }

  addChunk(chunk) {
    for (const record of chunk.objects) this.addProp(chunk, record);
    for (const record of chunk.lights) this.addLight(chunk, record);
  }

  removeChunk(chunk) {
    const group = this.groups.get(chunk.key);
    if (!group) return;
    this.scene.remove(group);
    this.groups.delete(chunk.key);
    this.pickables = this.pickables.filter((o) => o.userData.chunk !== chunk);
  }

  addProp(chunk, record) {
    const object = new THREE.Group();
    object.userData = { kind: "prop", record, chunk, aliases: NO_ALIASES };
    applyTransform(object, record);
    object.visible = !this.hiddenFiles.has(record.file.toLowerCase());
    this.groupFor(chunk).add(object);
    this.pickables.push(object);
    this.loadMeshes(object);
    record.object = object;
    return object;
  }

  // The actor's meshes into the object (again, after its record's file changed).
  loadMeshes(object) {
    const file = object.userData.record.file;
    object.userData.file = file;
    for (const child of [...object.children]) object.remove(child);
    return this.library.model(file).then((template) => {
      if (object.userData.file !== file) return;
      object.userData.aliases = template.userData.aliases;
      for (const child of template.children) {
        const mesh = child.clone();
        mesh.base = child.userData.base;
        mesh.userData = object.userData;
        object.add(mesh);
      }
      this.applyAppearance(object);
    });
  }

  // The record's texture variant and tint on the object's meshes; a changed file reloads them.
  refresh(object) {
    const { record } = object.userData;
    applyTransform(object, record);
    if (object.userData.file !== record.file) return this.loadMeshes(object);
    this.applyAppearance(object);
    return Promise.resolve();
  }

  applyAppearance(object) {
    const { record, aliases } = object.userData;
    const tint = record.tintAlias && record.tint ? record.tint : null;
    for (const mesh of object.children) {
      if (!mesh.isMesh || !mesh.base) continue;
      mesh.material = this.library.material(variantTexture(aliases, record.material, mesh.base.texture), mesh.base.alpha, tint);
    }
  }

  // Hides (or shows again) every object of an actor, e.g. trees in the way of terrain work.
  setFileHidden(file, hidden) {
    const key = file.toLowerCase();
    if (hidden) this.hiddenFiles.add(key); else this.hiddenFiles.delete(key);
    for (const object of this.pickables) {
      if (object.userData.kind === "prop" && object.userData.record.file.toLowerCase() === key) object.visible = !hidden;
    }
  }

  showAll() {
    this.hiddenFiles.clear();
    for (const object of this.pickables) if (object.userData.kind === "prop") object.visible = true;
  }

  addLight(chunk, record) {
    const object = new THREE.Mesh(this.lightGeometry, this.lightMaterial(record.color));
    object.userData = { kind: "light", record, chunk };
    object.visible = this.showLights;
    applyTransform(object, { position: record.position, rotation: [0, 0, 0], scale: 1 });
    this.groupFor(chunk).add(object);
    this.pickables.push(object);
    record.object = object;
    return object;
  }

  refreshLight(record) {
    record.object.material.color.setRGB(record.color[0] / 255, record.color[1] / 255, record.color[2] / 255);
  }

  remove(object) {
    object.parent?.remove(object);
    this.pickables = this.pickables.filter((o) => o !== object);
  }

  // Moves an object's record and node to another chunk (it was dragged into it).
  rehome(object, chunk) {
    const { record, kind } = object.userData;
    const old = object.userData.chunk;
    const list = kind === "light" ? "lights" : "objects";
    old[list] = old[list].filter((r) => r !== record);
    chunk[list].push(record);
    old.dirty[list] = chunk.dirty[list] = true;
    object.userData.chunk = chunk;
    object.traverse((child) => { child.userData = object.userData; });
    this.groupFor(chunk).attach(object);
  }

  setLightsVisible(visible) {
    this.showLights = visible;
    for (const object of this.pickables) if (object.userData.kind === "light") object.visible = visible;
  }

  pick(raycaster) {
    const hits = raycaster.intersectObjects(this.pickables.filter((o) => o.visible), true);
    for (const hit of hits) {
      let node = hit.object;
      while (node && !node.userData.kind) node = node.parent;
      if (node) return { object: node.userData.record.object || node, distance: hit.distance };
    }
    return null;
  }
}

// A draggable marker: the zone's spawn point, a house's spawn or exit.
export function makeMarker(colour, label) {
  const group = new THREE.Group();
  const body = new THREE.Mesh(new THREE.CylinderGeometry(0.6, 0.6, 3.6, 12).translate(0, 1.8, 0), new THREE.MeshBasicMaterial({ color: colour }));
  const arrow = new THREE.Mesh(new THREE.ConeGeometry(0.7, 1.6, 12).rotateZ(-Math.PI / 2).translate(1.4, 2.6, 0), new THREE.MeshBasicMaterial({ color: colour }));
  group.add(body, arrow);
  group.userData = { kind: "marker", label };
  for (const child of group.children) child.userData = group.userData;
  return group;
}
