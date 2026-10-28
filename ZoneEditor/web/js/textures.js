// Client textures for the viewport: the original DDS (its own mipmaps, full size), else a PNG the server converts.
import * as THREE from "three";
import { DDSLoader } from "three/addons/loaders/DDSLoader.js";
import { ASSET_VERSION } from "./api.js";

const ddsLoader = new DDSLoader();
const pngLoader = new THREE.TextureLoader();
const cache = new Map();
let anisotropy = 1;

export function initTextures(renderer) {
  anisotropy = renderer.capabilities.getMaxAnisotropy();
}

// srgb: colour data (diffuse, colour maps) rather than masks. Mesh UVs are Direct3D's with v negated, which a
// flipped PNG reads as is; a DDS can't be flipped on upload, so its v is negated back.
export function loadTexture(name, { srgb = true } = {}) {
  const key = `${name.toLowerCase()}|${srgb}`;
  if (cache.has(key)) return cache.get(key);
  const holder = new THREE.Texture();
  const setup = (texture, flipped) => {
    texture.wrapS = texture.wrapT = THREE.RepeatWrapping;
    texture.colorSpace = srgb ? THREE.SRGBColorSpace : THREE.NoColorSpace;
    texture.anisotropy = anisotropy;
    if (!flipped) texture.repeat.set(1, -1);
    if (texture.image) texture.needsUpdate = true;
    return texture;
  };
  const fallback = () => pngLoader.load(`api/texture?name=${encodeURIComponent(name)}&size=2048&v=${ASSET_VERSION}`,
    (texture) => { setup(texture, true); swap(texture); }, undefined, () => {});
  const swap = (texture) => {
    // Materials hold the holder; copy the loaded texture into it so they pick it up.
    holder.copy(texture);
    holder.mipmaps = texture.mipmaps;
    holder.isCompressedTexture = texture.isCompressedTexture;
    holder.image = texture.image;
    holder.version++;
    holder.dispatchEvent({ type: "loaded" });
  };
  const url = `api/dds?name=${encodeURIComponent(name)}&v=${ASSET_VERSION}`;
  ddsLoader.load(url, (texture) => {
    if (!texture.mipmaps?.length || texture.format === undefined) return fallback();
    if (texture.mipmaps.length === 1) texture.minFilter = THREE.LinearFilter;
    swap(setup(texture, false));
  }, undefined, fallback);
  setup(holder, false);
  cache.set(key, holder);
  return holder;
}

// The average colour of a texture's top and bottom halves (an ambient gradient: sky and ground light).
export async function gradientColours(name) {
  const response = await fetch(`api/texture?name=${encodeURIComponent(name)}&size=64`);
  if (!response.ok) throw new Error(name);
  const image = await createImageBitmap(await response.blob());
  const canvas = new OffscreenCanvas(image.width, image.height);
  const context = canvas.getContext("2d");
  context.drawImage(image, 0, 0);
  const data = context.getImageData(0, 0, image.width, image.height).data;
  const half = (from, to) => {
    const sum = [0, 0, 0];
    let count = 0;
    for (let y = from; y < to; y++) {
      for (let x = 0; x < image.width; x++) {
        const p = (y * image.width + x) * 4;
        sum[0] += data[p]; sum[1] += data[p + 1]; sum[2] += data[p + 2];
        count++;
      }
    }
    return new THREE.Color().setRGB(sum[0] / count / 255, sum[1] / count / 255, sum[2] / count / 255, THREE.SRGBColorSpace);
  };
  const middle = Math.max(1, Math.floor(image.height / 2));
  return { sky: half(0, middle), ground: half(middle, image.height) };
}
