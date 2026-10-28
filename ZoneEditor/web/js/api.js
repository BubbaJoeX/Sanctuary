// The editor server's API (zoneeditor/server.py). Binary bodies are a u32 length, JSON, then the blobs it sizes.

// Models and textures are cached by the browser for a day; bump this when the server's output for them changes.
export const ASSET_VERSION = 3;

export async function getJson(path) {
  const response = await fetch(path);
  const data = await response.json();
  if (!response.ok) throw new Error(data.error || response.statusText);
  return data;
}

export async function postJson(path, body) {
  const response = await fetch(path, { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(body) });
  const data = await response.json();
  if (!response.ok) throw new Error(data.error || response.statusText);
  return data;
}

export function unframe(buffer) {
  const view = new DataView(buffer);
  const length = view.getUint32(0, true);
  const meta = JSON.parse(new TextDecoder().decode(new Uint8Array(buffer, 4, length)));
  let offset = 4 + length;
  const blobs = meta.sizes.map((size) => {
    const blob = buffer.slice(offset, offset + size);
    offset += size;
    return blob;
  });
  return { meta, blobs };
}

export function frame(meta, blobs) {
  const header = new TextEncoder().encode(JSON.stringify({ ...meta, sizes: blobs.map((b) => b.byteLength) }));
  const total = 4 + header.byteLength + blobs.reduce((sum, b) => sum + b.byteLength, 0);
  const out = new Uint8Array(total);
  new DataView(out.buffer).setUint32(0, header.byteLength, true);
  out.set(header, 4);
  let offset = 4 + header.byteLength;
  for (const blob of blobs) {
    out.set(new Uint8Array(blob.buffer ?? blob, blob.byteOffset ?? 0, blob.byteLength), offset);
    offset += blob.byteLength;
  }
  return out;
}

export async function loadChunk(zone, row, col) {
  const response = await fetch(`/api/chunk?zone=${encodeURIComponent(zone)}&row=${row}&col=${col}`);
  if (!response.ok) throw new Error((await response.json()).error);
  const { meta, blobs } = unframe(await response.arrayBuffer());
  return { meta, heights: new Float32Array(blobs[0]), weights: new Uint8Array(blobs[1]), colour: new Uint8Array(blobs[2]) };
}

export async function save(meta, blobs) {
  const response = await fetch("/api/save", { method: "POST", body: frame(meta, blobs) });
  const data = await response.json();
  if (!response.ok) throw new Error(data.error || response.statusText);
  return data;
}

export function decodeBase64(text, Type) {
  const bytes = Uint8Array.from(atob(text), (c) => c.charCodeAt(0));
  return new Type(bytes.buffer);
}
