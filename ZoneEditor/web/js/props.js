// Props (placed actors), point lights and zone markers in the viewport.
//
// A prop record is what the server sends and gets back: { uid?, copyOf?, file, position, rotation, scale, tile? }.
// Rotation is (yaw, pitch, roll) in radians, applied like D3DXMatrixRotationYawPitchRoll: three's 'YXZ' order.
import * as THREE from "three";
import { decodeBase64, getJson } from "./api.js";

const textureLoader = new THREE.TextureLoader();

export class PropLibrary {
  constructor() {
    this.models = new Map();
    this.materials = new Map();
    this.placeholder = new THREE.BoxGeometry(1, 1, 1).translate(0, 0.5, 0);
    this.placeholderMaterial = new THREE.MeshLambertMaterial({ color: 0xff44aa, wireframe: true });
  }

  // alpha is the material's use of its texture's alpha: "test" (cutout), "blend" (water, glass) or null (a mask, ignored).
  material(texture, alpha) {
    const key = `${texture || ""}|${alpha || ""}`;
    if (!this.materials.has(key)) {
      const material = new THREE.MeshLambertMaterial({
        color: texture ? 0xffffff : 0xb0b0b0, side: THREE.DoubleSide,
        alphaTest: alpha === "test" ? 0.4 : 0, transparent: alpha === "blend", opacity: alpha === "blend" ? 0.75 : 1,
        depthWrite: alpha !== "blend",
      });
      if (texture) {
        textureLoader.load(`/api/texture?name=${encodeURIComponent(texture)}`, (map) => {
          map.wrapS = map.wrapT = THREE.RepeatWrapping;
          map.colorSpace = THREE.NoColorSpace;
          material.map = map;
          material.needsUpdate = true;
        }, undefined, () => material.color.set(0xb0b0b0));
      }
      this.materials.set(key, material);
    }
    return this.materials.get(key);
  }

  // A template group for an actor, shared by every instance of it.
  model(file) {
    const key = file.toLowerCase();
    if (!this.models.has(key)) {
      this.models.set(key, getJson(`/api/model?name=${encodeURIComponent(file)}`).then((data) => {
        const group = new THREE.Group();
        for (const mesh of data.meshes) {
          const geometry = new THREE.BufferGeometry();
          geometry.setAttribute("position", new THREE.BufferAttribute(decodeBase64(mesh.positions, Float32Array), 3));
          geometry.setAttribute("uv", new THREE.BufferAttribute(decodeBase64(mesh.uvs, Float32Array), 2));
          geometry.setIndex(new THREE.BufferAttribute(decodeBase64(mesh.indices, Uint32Array), 1));
          geometry.computeVertexNormals();
          geometry.computeBoundingSphere();
          group.add(new THREE.Mesh(geometry, this.material(mesh.texture, mesh.alpha)));
        }
        return group;
      }).catch(() => {
        const group = new THREE.Group();
        group.add(new THREE.Mesh(this.placeholder, this.placeholderMaterial));
        return group;
      }));
    }
    return this.models.get(key);
  }
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
    object.userData = { kind: "prop", record, chunk };
    applyTransform(object, record);
    this.groupFor(chunk).add(object);
    this.pickables.push(object);
    this.library.model(record.file).then((template) => {
      for (const child of template.children) {
        const mesh = child.clone();
        mesh.userData = object.userData;
        object.add(mesh);
      }
    });
    record.object = object;
    return object;
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
