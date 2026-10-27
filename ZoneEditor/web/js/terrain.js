// Terrain chunks in the viewport: streaming, drawing, picking and the sculpt and paint brushes.
//
// Everything here is in Free Realms coordinates (x, y up, z), which the viewport shows as they are. A chunk
// is 256 x 256 units: heights every 2 units (129 x 129, [i along x][j along z]), eco weights 512 x 512 and its
// colour map 1024 x 1024, both [x][z].
//
// The ground is drawn like the client's: the colour map tints it, and each eco's detail texture (tiled every
// 256 / repeat units) adds its texture, blended by the eco weights. Up to 4 ecos per chunk are drawn.
import * as THREE from "three";
import { loadChunk } from "./api.js";
import { loadTexture } from "./textures.js";

export const SAMPLES = 129;
export const STEP = 2;
export const CHUNK = 256;
export const TILE = 64;
export const ECO = 512;
export const COLOUR = 1024;
const DETAILS = 4;

const vertexPrelude = /* glsl */ `
  varying vec2 vWorldXZ;`;

const vertexWorld = /* glsl */ `
  vWorldXZ = (modelMatrix * vec4(transformed, 1.0)).xz;`;

const fragmentPrelude = /* glsl */ `
  varying vec2 vWorldXZ;
  uniform sampler2D detailWeights;
  uniform sampler2D detail0, detail1, detail2, detail3;
  uniform vec3 detailMean0, detailMean1, detailMean2, detailMean3;
  uniform vec4 detailPeriod;
  uniform float detailStrength;
  uniform sampler2D ecoMap;
  uniform sampler2D tileEnvMap;
  uniform float ecoOverlay;
  uniform float envOverlay;
  uniform float pending;
  uniform float showGrid;
  uniform vec3 brush;          // x, z, radius; radius 0 hides it
  uniform vec3 brushColor;

  vec3 detailSample(sampler2D tex, vec3 mean, float period) {
    return texture2D(tex, vWorldXZ / period).rgb / max(mean, vec3(0.02));
  }

  float gridLine(vec2 p, float spacing, float width) {
    vec2 d = abs(fract(p / spacing + 0.5) - 0.5) * spacing;
    vec2 w = fwidth(p) * width;
    vec2 l = 1.0 - smoothstep(vec2(0.0), w, d);
    return max(l.x, l.y);
  }`;

// After the colour map: the eco detail textures, each divided by its mean so the colour map keeps the tint.
const detailBlend = /* glsl */ `
  vec4 dw = texture2D(detailWeights, vMapUv);
  float dsum = dw.r + dw.g + dw.b + dw.a;
  if (dsum > 0.001 && detailStrength > 0.0) {
    vec3 detail = dw.r * detailSample(detail0, detailMean0, detailPeriod.x)
                + dw.g * detailSample(detail1, detailMean1, detailPeriod.y)
                + dw.b * detailSample(detail2, detailMean2, detailPeriod.z)
                + dw.a * detailSample(detail3, detailMean3, detailPeriod.w);
    diffuseColor.rgb *= mix(vec3(1.0), clamp(detail / dsum, 0.0, 3.0), detailStrength);
  }`;

// After lighting, before fog: what the editor draws over the ground.
const overlays = /* glsl */ `
  vec3 shown = gl_FragColor.rgb;
  shown = mix(shown, vec3(1.0, 0.05, 0.7), texture2D(ecoMap, vMapUv).r * ecoOverlay * 0.6);
  shown = mix(shown, texture2D(tileEnvMap, vMapUv).rgb, envOverlay * 0.5);
  if (pending > 0.5) shown = mix(shown, vec3(1.0, 0.7, 0.1), 0.1);
  if (showGrid > 0.5) {
    shown = mix(shown, vec3(1.0), gridLine(vWorldXZ, 64.0, 1.0) * 0.15);
    shown = mix(shown, vec3(1.0, 0.8, 0.2), gridLine(vWorldXZ, 256.0, 1.5) * 0.5);
  }
  if (brush.z > 0.0) {
    float d = length(vWorldXZ - brush.xy);
    float ring = 1.0 - smoothstep(0.0, fwidth(d) * 2.0, abs(d - brush.z));
    float inner = (1.0 - smoothstep(brush.z * 0.98, brush.z, d)) * 0.08;
    shown = mix(shown, brushColor, clamp(ring + inner, 0.0, 1.0));
  }
  gl_FragColor.rgb = shown;`;

const blankTexture = new THREE.DataTexture(new Uint8Array([255, 255, 255, 255]), 1, 1);
blankTexture.needsUpdate = true;

const ENV_PALETTE = [
  [230, 90, 90], [90, 200, 110], [90, 140, 240], [240, 200, 70], [200, 110, 230], [80, 210, 210],
  [250, 150, 60], [160, 220, 80], [240, 120, 180], [130, 130, 250], [200, 200, 200], [120, 90, 60],
];

export function environmentColour(id) {
  return id <= 0 ? [60, 60, 60] : ENV_PALETTE[id % ENV_PALETTE.length];
}

function falloff(t) {
  return t >= 1 ? 0 : 0.5 + 0.5 * Math.cos(Math.PI * t);
}

function hash2(x, z) {
  const s = Math.sin(x * 127.1 + z * 311.7) * 43758.5453;
  return s - Math.floor(s);
}

function valueNoise(x, z) {
  const xi = Math.floor(x), zi = Math.floor(z), xf = x - xi, zf = z - zi;
  const u = xf * xf * (3 - 2 * xf), v = zf * zf * (3 - 2 * zf);
  const a = hash2(xi, zi), b = hash2(xi + 1, zi), c = hash2(xi, zi + 1), d = hash2(xi + 1, zi + 1);
  return a + (b - a) * u + (c - a) * v + (a - b - c + d) * u * v;
}

export class TerrainChunk {
  constructor(manager, row, col, data) {
    this.manager = manager;
    this.row = row;
    this.col = col;
    this.key = `${row}_${col}`;
    this.x0 = col * TILE;
    this.z0 = row * TILE;
    this.editable = data.editable;
    this.isNew = !!data.isNew;
    this.version = data.version;
    this.heights = data.heights;
    this.ecos = data.ecos;
    this.weights = data.weights;
    this.tiles = data.tiles;
    this.objects = data.objects;
    this.lights = data.lights;
    this.colourSize = data.colourSize;
    this.colour = data.colour;                      // RGBA, [x][z]
    this.tileDirty = new Array(16).fill(this.isNew);
    this.dirty = { heights: this.isNew, ecos: this.isNew, colour: this.isNew, objects: this.isNew, lights: this.isNew, environment: this.isNew };

    this.colourTexture = new THREE.DataTexture(this.colour, this.colourSize, this.colourSize, THREE.RGBAFormat);
    this.colourTexture.colorSpace = THREE.SRGBColorSpace;
    this.colourTexture.magFilter = THREE.LinearFilter;
    this.colourTexture.minFilter = THREE.LinearMipmapLinearFilter;
    this.colourTexture.generateMipmaps = true;
    this.colourTexture.anisotropy = manager.anisotropy;
    this.colourTexture.needsUpdate = true;
    this.ecoPixels = new Uint8Array(ECO * ECO);
    this.ecoTexture = new THREE.DataTexture(this.ecoPixels, ECO, ECO, THREE.RedFormat);
    this.ecoTexture.magFilter = THREE.LinearFilter;
    this.ecoTexture.needsUpdate = true;
    this.detailPixels = new Uint8Array(ECO * ECO * 4);
    this.detailTexture = new THREE.DataTexture(this.detailPixels, ECO, ECO, THREE.RGBAFormat);
    this.detailTexture.magFilter = THREE.LinearFilter;
    this.envPixels = new Uint8Array(16 * 4);
    this.envTexture = new THREE.DataTexture(this.envPixels, 4, 4, THREE.RGBAFormat);
    this.envTexture.magFilter = this.envTexture.minFilter = THREE.NearestFilter;
    this.updateEnvironmentTexture();

    this.uniforms = {
      detailWeights: { value: this.detailTexture },
      ecoMap: { value: this.ecoTexture },
      tileEnvMap: { value: this.envTexture },
      pending: { value: this.isNew ? 1 : 0 },
      detailPeriod: { value: new THREE.Vector4(32, 32, 32, 32) },
    };
    for (let k = 0; k < DETAILS; k++) {
      this.uniforms[`detail${k}`] = { value: blankTexture };
      this.uniforms[`detailMean${k}`] = { value: new THREE.Color(1, 1, 1) };
    }
    this.updateDetailWeights();

    this.material = new THREE.MeshLambertMaterial({ map: this.colourTexture });
    this.material.onBeforeCompile = (shader) => {
      Object.assign(shader.uniforms, manager.uniforms, this.uniforms);
      shader.vertexShader = shader.vertexShader
        .replace("#include <common>", "#include <common>\n" + vertexPrelude)
        .replace("#include <begin_vertex>", "#include <begin_vertex>\n" + vertexWorld);
      shader.fragmentShader = shader.fragmentShader
        .replace("#include <common>", "#include <common>\n" + fragmentPrelude)
        .replace("#include <map_fragment>", "#include <map_fragment>\n" + detailBlend)
        .replace("#include <opaque_fragment>", "#include <opaque_fragment>\n" + overlays);
    };
    this.material.customProgramCacheKey = () => "fr-terrain";
    this.mesh = new THREE.Mesh(this.buildGeometry(), this.material);
    this.mesh.userData.chunk = this;
    this.mesh.castShadow = this.mesh.receiveShadow = true;
    this.group = new THREE.Group();
    this.group.add(this.mesh);
  }

  // The chunk's 4 heaviest ecos as the detail textures it draws, with their weights renormalized among them.
  updateDetailWeights() {
    const count = this.ecos.length;
    const totals = this.ecos.map((_, k) => {
      let sum = 0;
      for (let t = k; t < this.weights.length; t += count * 97) sum += this.weights[t];
      return sum;
    });
    const chosen = totals.map((total, k) => [total, k]).sort((a, b) => b[0] - a[0]).slice(0, DETAILS).map(([, k]) => k);
    for (let t = 0; t < ECO * ECO; t++) {
      let sum = 0;
      for (const k of chosen) sum += this.weights[t * count + k];
      for (let slot = 0; slot < DETAILS; slot++) {
        const k = chosen[slot];
        this.detailPixels[t * 4 + slot] = k === undefined || !sum ? 0 : Math.round(this.weights[t * count + k] * 255 / sum);
      }
    }
    for (let slot = 0; slot < DETAILS; slot++) {
      const detail = chosen[slot] === undefined ? null : this.manager.detail(this.ecos[chosen[slot]]);
      this.uniforms[`detail${slot}`].value = detail?.texture ?? blankTexture;
      this.uniforms[`detailMean${slot}`].value = detail?.mean ?? new THREE.Color(1, 1, 1);
      this.uniforms.detailPeriod.value.setComponent(slot, detail?.period ?? 32);
    }
    this.detailTexture.needsUpdate = true;
  }

  get anyDirty() {
    return Object.values(this.dirty).some(Boolean);
  }

  buildGeometry() {
    const n = SAMPLES;
    const positions = new Float32Array(n * n * 3);
    const uvs = new Float32Array(n * n * 2);
    const normals = new Float32Array(n * n * 3);
    for (let i = 0; i < n; i++) {
      for (let j = 0; j < n; j++) {
        const v = i * n + j;
        const h = this.heights[v];
        positions.set([this.x0 + i * STEP, Number.isNaN(h) ? 0 : h, this.z0 + j * STEP], v * 3);
        uvs.set([j / (n - 1), i / (n - 1)], v * 2);
      }
    }
    const indices = [];
    for (let i = 0; i < n - 1; i++) {
      for (let j = 0; j < n - 1; j++) {
        const a = i * n + j, b = a + 1, c = a + n, d = c + 1;
        if ([a, b, c, d].some((k) => Number.isNaN(this.heights[k]))) continue;
        // Counter-clockwise seen from above (+y), so the ground's front faces point up.
        indices.push(a, b, c, b, d, c);
      }
    }
    const geometry = new THREE.BufferGeometry();
    geometry.setAttribute("position", new THREE.BufferAttribute(positions, 3));
    geometry.setAttribute("normal", new THREE.BufferAttribute(normals, 3));
    geometry.setAttribute("uv", new THREE.BufferAttribute(uvs, 2));
    geometry.setIndex(indices.length > 65535 ? new THREE.Uint32BufferAttribute(indices, 1) : new THREE.Uint16BufferAttribute(indices, 1));
    this.geometry = geometry;
    this.updateNormals();
    geometry.computeBoundingSphere();
    geometry.computeBoundingBox();
    return geometry;
  }

  // A height sample, reaching into loaded neighbours past the edges (clamped where there are none).
  sample(i, j) {
    if (i >= 0 && i < SAMPLES && j >= 0 && j < SAMPLES) return this.heights[i * SAMPLES + j];
    const h = this.manager.heightAtSample(this.x0 + i * STEP, this.z0 + j * STEP);
    if (h !== null && !Number.isNaN(h)) return h;
    return this.heights[Math.min(Math.max(i, 0), SAMPLES - 1) * SAMPLES + Math.min(Math.max(j, 0), SAMPLES - 1)];
  }

  updateNormals() {
    const n = SAMPLES;
    const normals = this.geometry.attributes.normal.array;
    for (let i = 0; i < n; i++) {
      for (let j = 0; j < n; j++) {
        let dx = (this.sample(i + 1, j) - this.sample(i - 1, j)) / (2 * STEP);
        let dz = (this.sample(i, j + 1) - this.sample(i, j - 1)) / (2 * STEP);
        if (!Number.isFinite(dx)) dx = 0;
        if (!Number.isFinite(dz)) dz = 0;
        const length = Math.sqrt(dx * dx + 1 + dz * dz);
        normals.set([-dx / length, 1 / length, -dz / length], (i * n + j) * 3);
      }
    }
    this.geometry.attributes.normal.needsUpdate = true;
  }

  updatePositions() {
    const positions = this.geometry.attributes.position.array;
    for (let v = 0; v < SAMPLES * SAMPLES; v++) {
      const h = this.heights[v];
      positions[v * 3 + 1] = Number.isNaN(h) ? 0 : h;
    }
    this.geometry.attributes.position.needsUpdate = true;
    this.geometry.computeBoundingSphere();
    this.geometry.computeBoundingBox();
    this.updateNormals();
  }

  ecoChannel(ecoIndex, create) {
    let k = this.ecos.indexOf(ecoIndex);
    if (k >= 0 || !create) return k;
    const count = this.ecos.length;
    const grown = new Uint8Array(ECO * ECO * (count + 1));
    for (let t = 0; t < ECO * ECO; t++) {
      for (let c = 0; c < count; c++) grown[t * (count + 1) + c] = this.weights[t * count + c];
    }
    this.ecos.push(ecoIndex);
    this.weights = grown;
    return count;
  }

  updateEcoTexture(ecoIndex) {
    const k = this.ecos.indexOf(ecoIndex);
    const count = this.ecos.length;
    if (k < 0 || !this.weights.length) this.ecoPixels.fill(0);
    else for (let t = 0; t < ECO * ECO; t++) this.ecoPixels[t] = this.weights[t * count + k];
    this.ecoTexture.needsUpdate = true;
  }

  updateEnvironmentTexture() {
    // Texel (row = tile x, column = tile z) like the colour map; tiles are numbered z-major.
    for (let t = 0; t < 16; t++) {
      const tz = Math.floor(t / 4), tx = t % 4;
      const [r, g, b] = environmentColour(this.tiles[t]?.environment ?? 0);
      this.envPixels.set([r, g, b, 255], (tx * 4 + tz) * 4);
    }
    this.envTexture.needsUpdate = true;
  }

  dispose() {
    this.geometry.dispose();
    this.material.dispose();
    this.colourTexture.dispose();
    this.ecoTexture.dispose();
    this.detailTexture.dispose();
    this.envTexture.dispose();
  }
}

export class TerrainManager {
  constructor(root, events) {
    this.root = root;
    this.events = events;          // { onChunkLoaded(chunk), onChunkUnloaded(chunk), status(text) }
    this.chunks = new Map();       // key -> TerrainChunk
    this.loading = new Map();      // key -> Promise
    this.cells = new Map();        // key -> { row, col, editable, source }
    this.zone = null;
    this.radius = 2;
    this.selectedEco = null;
    this.anisotropy = 1;
    this.details = new Map();      // eco index -> { texture, mean, period }
    this.uniforms = {
      ecoOverlay: { value: 0 },
      envOverlay: { value: 0 },
      showGrid: { value: 1 },
      detailStrength: { value: 1 },
      brush: { value: new THREE.Vector3(0, 0, 0) },
      brushColor: { value: new THREE.Color(1, 0.85, 0.2) },
    };
  }

  setZone(zone) {
    for (const chunk of this.chunks.values()) this.unload(chunk);
    this.chunks.clear();
    this.loading.clear();
    this.cells.clear();
    this.details.clear();
    this.zone = zone;
    for (const cell of zone.chunks) this.cells.set(`${cell.row}_${cell.col}`, cell);
  }

  // An eco's detail texture, its mean colour (linear, filled in when known) and how many units one repeat covers.
  detail(ecoIndex) {
    if (!this.details.has(ecoIndex)) {
      const eco = this.zone.ecos.find((e) => e.index === ecoIndex);
      if (!eco) return null;
      const entry = { texture: loadTexture(eco.texture), mean: new THREE.Color(0.5, 0.5, 0.5), period: 256 / Math.max(eco.repeat || 8, 1) };
      fetch(`/api/eco-colour?zone=${encodeURIComponent(this.zone.name)}&index=${ecoIndex}`).then((r) => r.json())
        .then(([r, g, b]) => entry.mean.setRGB(r / 255, g / 255, b / 255, THREE.SRGBColorSpace)).catch(() => {});
      this.details.set(ecoIndex, entry);
    }
    return this.details.get(ecoIndex);
  }

  cellAt(x, z) {
    const row = Math.floor(z / CHUNK) * 4, col = Math.floor(x / CHUNK) * 4;
    return { row, col, key: `${row}_${col}` };
  }

  chunkAt(x, z) {
    return this.chunks.get(this.cellAt(x, z).key) || null;
  }

  inBounds(row, col) {
    const b = this.zone.bounds;
    return b.startX <= col && col + 4 <= b.endX && b.startZ <= row && row + 4 <= b.endZ;
  }

  // Loads chunks within the radius of (x, z) and drops clean ones well outside it.
  update(x, z) {
    if (!this.zone) return;
    const centre = this.cellAt(x, z);
    const wanted = [];
    for (let dr = -this.radius; dr <= this.radius; dr++) {
      for (let dc = -this.radius; dc <= this.radius; dc++) {
        const key = `${centre.row + dr * 4}_${centre.col + dc * 4}`;
        if (this.cells.has(key)) wanted.push([dr * dr + dc * dc, key]);
      }
    }
    wanted.sort((a, b) => a[0] - b[0]);
    for (const [, key] of wanted) {
      if (this.chunks.has(key) || this.loading.has(key)) continue;
      if (this.loading.size >= 3) break;
      const cell = this.cells.get(key);
      const promise = this.load(cell).finally(() => this.loading.delete(key));
      this.loading.set(key, promise);
    }
    for (const chunk of [...this.chunks.values()]) {
      const far = Math.max(Math.abs(chunk.row - centre.row), Math.abs(chunk.col - centre.col)) / 4 > this.radius + 1;
      if (far && !chunk.anyDirty) this.unload(chunk);
    }
  }

  async load(cell) {
    const zoneName = this.zone.name;
    let data;
    try {
      data = await loadChunk(zoneName, cell.row, cell.col, false);
    } catch (error) {
      this.events.status(`Chunk ${cell.row}_${cell.col}: ${error.message}`);
      cell.failed = true;
      this.cells.delete(`${cell.row}_${cell.col}`);
      return;
    }
    if (!this.zone || this.zone.name !== zoneName) return;
    const size = data.image.width;
    const canvas = new OffscreenCanvas(size, size);
    const context = canvas.getContext("2d", { willReadFrequently: true });
    context.drawImage(data.image, 0, 0);
    const colour = new Uint8Array(context.getImageData(0, 0, size, size).data.buffer);
    const chunk = new TerrainChunk(this, cell.row, cell.col, {
      editable: data.meta.editable, version: data.meta.version, heights: data.heights,
      ecos: data.meta.ecos, weights: data.weights, tiles: data.meta.tiles,
      objects: data.meta.objects, lights: data.meta.lights, colour, colourSize: size,
    });
    this.add(chunk);
  }

  add(chunk) {
    this.chunks.set(chunk.key, chunk);
    this.root.add(chunk.group);
    if (this.selectedEco !== null) chunk.updateEcoTexture(this.selectedEco);
    // Neighbours' edge normals can use this chunk's heights now.
    for (const other of this.neighbours(chunk)) other.updateNormals();
    chunk.updateNormals();
    this.events.onChunkLoaded(chunk);
  }

  unload(chunk) {
    this.root.remove(chunk.group);
    chunk.dispose();
    this.chunks.delete(chunk.key);
    this.events.onChunkUnloaded(chunk);
  }

  neighbours(chunk) {
    const result = [];
    for (let dr = -4; dr <= 4; dr += 4) {
      for (let dc = -4; dc <= 4; dc += 4) {
        const other = (dr || dc) && this.chunks.get(`${chunk.row + dr}_${chunk.col + dc}`);
        if (other) result.push(other);
      }
    }
    return result;
  }

  // The height sample at grid point (x, z) (multiples of 2), from whichever loaded chunk holds it.
  heightAtSample(x, z) {
    for (const [ox, oz] of [[0, 0], [-1, 0], [0, -1], [-1, -1]]) {
      const chunk = this.chunkAt(x + ox, z + oz);
      if (!chunk) continue;
      const i = Math.round((x - chunk.x0) / STEP), j = Math.round((z - chunk.z0) / STEP);
      if (i >= 0 && i < SAMPLES && j >= 0 && j < SAMPLES) return chunk.heights[i * SAMPLES + j];
    }
    return null;
  }

  // Bilinear height at any (x, z) over loaded chunks; null where nothing is loaded or there's a hole.
  heightAt(x, z) {
    const chunk = this.chunkAt(x, z);
    if (!chunk) return null;
    const fi = (x - chunk.x0) / STEP, fj = (z - chunk.z0) / STEP;
    const i = Math.min(Math.floor(fi), SAMPLES - 2), j = Math.min(Math.floor(fj), SAMPLES - 2);
    const u = fi - i, v = fj - j;
    const h = (a, b) => chunk.heights[(i + a) * SAMPLES + j + b];
    const value = (h(0, 0) * (1 - u) + h(1, 0) * u) * (1 - v) + (h(0, 1) * (1 - u) + h(1, 1) * u) * v;
    return Number.isNaN(value) ? null : value;
  }

  // Ray march over the heightfield in zone coordinates: { x, y, z, chunk } or null.
  raycast(origin, direction, maxDistance = 4000) {
    let previous = null;
    let step = 1;
    for (let t = 0; t < maxDistance; t += step) {
      const x = origin.x + direction.x * t, y = origin.y + direction.y * t, z = origin.z + direction.z * t;
      const h = this.heightAt(x, z);
      if (h !== null && y <= h) {
        if (previous === null) return { x, y: h, z, chunk: this.chunkAt(x, z) };
        let lo = previous, hi = t;
        for (let k = 0; k < 12; k++) {
          const mid = (lo + hi) / 2;
          const hm = this.heightAt(origin.x + direction.x * mid, origin.z + direction.z * mid);
          if (hm !== null && origin.y + direction.y * mid <= hm) hi = mid; else lo = mid;
        }
        const px = origin.x + direction.x * hi, pz = origin.z + direction.z * hi;
        return { x: px, y: this.heightAt(px, pz) ?? h, z: pz, chunk: this.chunkAt(px, pz) };
      }
      previous = h === null ? null : t;
      step = t < 200 ? 0.5 : t < 1000 ? 1.5 : 4;
    }
    return null;
  }

  // A sample on a chunk edge shared with a chunk the brush mustn't move (stock, or not loaded) stays put.
  pinned(chunk, i, j) {
    const di = i === 0 ? -4 : i === SAMPLES - 1 ? 4 : 0;
    const dj = j === 0 ? -4 : j === SAMPLES - 1 ? 4 : 0;
    if (!di && !dj) return false;
    const offsets = [[di, 0], [0, dj], [di, dj]].filter(([a, b]) => a || b);
    for (const [dc, dr] of offsets) {
      const key = `${chunk.row + dr}_${chunk.col + dc}`;
      if (!this.cells.has(key)) continue;
      const other = this.chunks.get(key);
      if (!other || !other.editable) return true;
    }
    return false;
  }

  markTiles(chunk, i, j) {
    const tilesX = [Math.min(Math.floor(i / 32), 3)];
    if (i % 32 === 0 && i > 0 && i < SAMPLES - 1) tilesX.push(i / 32 - 1);
    const tilesZ = [Math.min(Math.floor(j / 32), 3)];
    if (j % 32 === 0 && j > 0 && j < SAMPLES - 1) tilesZ.push(j / 32 - 1);
    for (const tx of tilesX) for (const tz of tilesZ) chunk.tileDirty[tz * 4 + tx] = true;
  }

  editableChunksNear(x, z, radius) {
    return [...this.chunks.values()].filter((c) => c.editable &&
      x + radius >= c.x0 && x - radius <= c.x0 + CHUNK && z + radius >= c.z0 && z - radius <= c.z0 + CHUNK);
  }

  sculpt(mode, x, z, radius, strength, dt, target) {
    const changes = [];
    const rate = strength * dt;
    for (const chunk of this.editableChunksNear(x, z, radius)) {
      const i0 = Math.max(0, Math.ceil((x - radius - chunk.x0) / STEP)), i1 = Math.min(SAMPLES - 1, Math.floor((x + radius - chunk.x0) / STEP));
      const j0 = Math.max(0, Math.ceil((z - radius - chunk.z0) / STEP)), j1 = Math.min(SAMPLES - 1, Math.floor((z + radius - chunk.z0) / STEP));
      for (let i = i0; i <= i1; i++) {
        for (let j = j0; j <= j1; j++) {
          const px = chunk.x0 + i * STEP, pz = chunk.z0 + j * STEP;
          const w = falloff(Math.hypot(px - x, pz - z) / radius);
          if (w <= 0 || this.pinned(chunk, i, j)) continue;
          const v = i * SAMPLES + j;
          const h = chunk.heights[v];
          if (Number.isNaN(h)) continue;
          let next = h;
          if (mode === "raise") next = h + rate * 40 * w;
          else if (mode === "lower") next = h - rate * 40 * w;
          else if (mode === "smooth" || mode === "flatten") {
            let goal = target;
            if (mode === "smooth") {
              let sum = 0, count = 0;
              for (let a = -2; a <= 2; a++) for (let b = -2; b <= 2; b++) {
                const s = chunk.sample(i + a, j + b);
                if (!Number.isNaN(s)) { sum += s; count++; }
              }
              goal = sum / count;
            }
            next = h + (goal - h) * Math.min(1, rate * 6 * w);
          } else if (mode === "noise") {
            next = h + (valueNoise(px / 9, pz / 9) - 0.5) * rate * 50 * w;
          } else if (mode === "level") next = h + (target - h) * Math.min(1, w * 4);
          changes.push([chunk, i, j, next]);
        }
      }
    }
    const touched = new Set();
    for (const [chunk, i, j, next] of changes) {
      chunk.heights[i * SAMPLES + j] = next;
      this.markTiles(chunk, i, j);
      touched.add(chunk);
    }
    for (const chunk of touched) {
      chunk.dirty.heights = true;
      chunk.updatePositions();
    }
    for (const chunk of touched) for (const other of this.neighbours(chunk)) if (!touched.has(other)) other.updateNormals();
    return touched.size;
  }

  // Paints eco ecoIndex; tint (rgb or null) also paints the colour map towards that eco's colour.
  paintEco(ecoIndex, x, z, radius, strength, dt, tint, tintAmount) {
    let count = 0;
    for (const chunk of this.editableChunksNear(x, z, radius)) {
      const k = chunk.ecoChannel(ecoIndex, true);
      const channels = chunk.ecos.length;
      const a = Math.min(1, strength * dt * 6);
      const t0 = Math.max(0, Math.floor((x - radius - chunk.x0) * 2)), t1 = Math.min(ECO - 1, Math.ceil((x + radius - chunk.x0) * 2));
      const u0 = Math.max(0, Math.floor((z - radius - chunk.z0) * 2)), u1 = Math.min(ECO - 1, Math.ceil((z + radius - chunk.z0) * 2));
      for (let ti = t0; ti <= t1; ti++) {
        for (let tj = u0; tj <= u1; tj++) {
          const w = falloff(Math.hypot(chunk.x0 + (ti + 0.5) / 2 - x, chunk.z0 + (tj + 0.5) / 2 - z) / radius) * a;
          if (w <= 0) continue;
          const base = (ti * ECO + tj) * channels;
          let total = 0;
          for (let c = 0; c < channels; c++) {
            const value = chunk.weights[base + c] * (1 - w) + (c === k ? 255 * w : 0);
            chunk.weights[base + c] = Math.round(value);
            total += chunk.weights[base + c];
          }
          chunk.weights[base + k] = Math.max(0, Math.min(255, chunk.weights[base + k] + 255 - total));
        }
      }
      if (tint) this.paintColourIn(chunk, tint, x, z, radius, a * tintAmount);
      chunk.dirty.ecos = true;
      if (this.selectedEco !== null) chunk.updateEcoTexture(this.selectedEco);
      chunk.updateDetailWeights();
      count++;
    }
    return count;
  }

  paintColour(rgb, x, z, radius, strength, dt) {
    let count = 0;
    for (const chunk of this.editableChunksNear(x, z, radius)) {
      this.paintColourIn(chunk, rgb, x, z, radius, Math.min(1, strength * dt * 6));
      count++;
    }
    return count;
  }

  paintColourIn(chunk, rgb, x, z, radius, amount) {
    const size = chunk.colourSize, scale = size / CHUNK;
    const t0 = Math.max(0, Math.floor((x - radius - chunk.x0) * scale)), t1 = Math.min(size - 1, Math.ceil((x + radius - chunk.x0) * scale));
    const u0 = Math.max(0, Math.floor((z - radius - chunk.z0) * scale)), u1 = Math.min(size - 1, Math.ceil((z + radius - chunk.z0) * scale));
    for (let ti = t0; ti <= t1; ti++) {
      for (let tj = u0; tj <= u1; tj++) {
        const w = falloff(Math.hypot(chunk.x0 + (ti + 0.5) / scale - x, chunk.z0 + (tj + 0.5) / scale - z) / radius) * amount;
        if (w <= 0) continue;
        const p = (ti * size + tj) * 4;
        for (let c = 0; c < 3; c++) chunk.colour[p + c] = Math.round(chunk.colour[p + c] * (1 - w) + rgb[c] * w);
      }
    }
    chunk.colourTexture.needsUpdate = true;
    chunk.dirty.colour = true;
  }

  sampleColour(x, z) {
    const chunk = this.chunkAt(x, z);
    if (!chunk) return null;
    const scale = chunk.colourSize / CHUNK;
    const ti = Math.min(chunk.colourSize - 1, Math.floor((x - chunk.x0) * scale));
    const tj = Math.min(chunk.colourSize - 1, Math.floor((z - chunk.z0) * scale));
    const p = (ti * chunk.colourSize + tj) * 4;
    return [chunk.colour[p], chunk.colour[p + 1], chunk.colour[p + 2]];
  }

  paintEnvironment(id, x, z, radius) {
    let count = 0;
    for (const chunk of this.editableChunksNear(x, z, radius)) {
      for (let t = 0; t < 16; t++) {
        const tz = Math.floor(t / 4), tx = t % 4;
        const cx = Math.max(chunk.x0 + tx * TILE, Math.min(x, chunk.x0 + (tx + 1) * TILE));
        const cz = Math.max(chunk.z0 + tz * TILE, Math.min(z, chunk.z0 + (tz + 1) * TILE));
        if (Math.hypot(cx - x, cz - z) <= radius && chunk.tiles[t].environment !== id) {
          chunk.tiles[t].environment = id;
          chunk.dirty.environment = true;
          count++;
        }
      }
      chunk.updateEnvironmentTexture();
    }
    return count;
  }

  setSelectedEco(ecoIndex) {
    this.selectedEco = ecoIndex;
    for (const chunk of this.chunks.values()) chunk.updateEcoTexture(ecoIndex);
  }

  // A new chunk in an empty cell: its edges copied from loaded neighbours, the rest relaxed between them.
  createChunk(row, col, baseHeight, ecoIndex, environment, tint) {
    const key = `${row}_${col}`;
    if (this.cells.has(key)) throw new Error(`There's already a chunk at ${key}`);
    const heights = new Float32Array(SAMPLES * SAMPLES).fill(baseHeight);
    const fixed = new Uint8Array(SAMPLES * SAMPLES);
    const x0 = col * TILE, z0 = row * TILE;
    let anyFixed = false;
    for (let i = 0; i < SAMPLES; i++) {
      for (let j = 0; j < SAMPLES; j++) {
        if (i > 0 && i < SAMPLES - 1 && j > 0 && j < SAMPLES - 1) continue;
        const h = this.heightAtSampleOutside(x0 + i * STEP, z0 + j * STEP, key);
        if (h !== null && !Number.isNaN(h)) {
          heights[i * SAMPLES + j] = h;
          fixed[i * SAMPLES + j] = 1;
          anyFixed = true;
        }
      }
    }
    if (anyFixed) {
      // Jacobi relaxation on a coarse-to-fine schedule: a smooth surface between the fixed edges.
      const scratch = new Float32Array(heights);
      for (let iteration = 0; iteration < 600; iteration++) {
        for (let i = 0; i < SAMPLES; i++) {
          for (let j = 0; j < SAMPLES; j++) {
            const v = i * SAMPLES + j;
            if (fixed[v]) continue;
            let sum = 0, count = 0;
            if (i > 0) { sum += heights[v - SAMPLES]; count++; }
            if (i < SAMPLES - 1) { sum += heights[v + SAMPLES]; count++; }
            if (j > 0) { sum += heights[v - 1]; count++; }
            if (j < SAMPLES - 1) { sum += heights[v + 1]; count++; }
            scratch[v] = sum / count;
          }
        }
        for (let v = 0; v < heights.length; v++) if (!fixed[v]) heights[v] = scratch[v];
      }
    }
    const weights = new Uint8Array(ECO * ECO).fill(255);
    const colour = new Uint8Array(COLOUR * COLOUR * 4);
    for (let p = 0; p < COLOUR * COLOUR; p++) colour.set([tint[0], tint[1], tint[2], 255], p * 4);
    const tiles = Array.from({ length: 16 }, (_, t) => ({ row: row + Math.floor(t / 4), col: col + (t % 4), environment, ecos: [ecoIndex] }));
    const cell = { row, col, editable: true, source: "new" };
    this.cells.set(key, cell);
    const chunk = new TerrainChunk(this, row, col, {
      editable: true, isNew: true, version: 6, heights, ecos: [ecoIndex], weights, tiles,
      objects: [], lights: [], colour, colourSize: COLOUR,
    });
    this.add(chunk);
    return chunk;
  }

  heightAtSampleOutside(x, z, excludeKey) {
    for (const chunk of this.chunks.values()) {
      if (chunk.key === excludeKey) continue;
      const i = (x - chunk.x0) / STEP, j = (z - chunk.z0) / STEP;
      if (i >= 0 && i < SAMPLES && j >= 0 && j < SAMPLES && Number.isInteger(i) && Number.isInteger(j)) return chunk.heights[i * SAMPLES + j];
    }
    return null;
  }

  // The heightfield with a one-sample border from the neighbours, for the server to compute normals.
  padded(chunk) {
    const out = new Float32Array(131 * 131);
    for (let i = -1; i <= SAMPLES; i++) for (let j = -1; j <= SAMPLES; j++) out[(i + 1) * 131 + j + 1] = chunk.sample(i, j);
    return out;
  }
}
