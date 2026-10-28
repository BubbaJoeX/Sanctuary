// Skinned props and what they do standing about (their loc_stand clip: a windmill's sails, a flag in the wind, a bird
// circling), from the client's own Granny clips baked by the server (zoneeditor/granny.py) and played with three's
// animation system. A skinned actor's template carries its skeleton in its bind pose (the .dsk's); each placed copy
// gets its own skeleton and mixer, started somewhere into the clip so copies don't move in step.
import * as THREE from "three";
import * as SkeletonUtils from "three/addons/utils/SkeletonUtils.js";
import { ASSET_VERSION, decodeBase64, getJson } from "./api.js";

// The clip an actor plays when nothing's going on.
export const IDLE_CLIP = "loc_stand";
// Copies farther than this from the camera hold still, which saves the time of hundreds of birds out of sight.
const ANIMATE_RANGE = 400;

const skeletons = new Map();             // .dsk -> Promise of its bones (names, parents, local matrices)
const clips = new Map();                 // ".gr2|.dsk" -> Promise of a THREE.AnimationClip, or null
const players = new Set();               // { mixer, object }: every playing copy

function boneName(name) {
  return THREE.PropertyBinding.sanitizeNodeName(name);
}

function loadSkeleton(name) {
  const key = name.toLowerCase();
  if (!skeletons.has(key)) skeletons.set(key, getJson(`api/skeleton?name=${encodeURIComponent(name)}&v=${ASSET_VERSION}`));
  return skeletons.get(key);
}

// The skeleton's bones in their bind pose, parented as in the .dsk: { roots, bones (in its order), index (name -> i) }.
function buildBones(skeleton) {
  const bones = skeleton.names.map((name, i) => {
    const bone = new THREE.Bone();
    bone.name = boneName(name);
    new THREE.Matrix4().fromArray(skeleton.local[i]).decompose(bone.position, bone.quaternion, bone.scale);
    return bone;
  });
  const roots = [];
  bones.forEach((bone, i) => (skeleton.parents[i] >= 0 ? bones[skeleton.parents[i]].add(bone) : roots.push(bone)));
  return { roots, bones, index: new Map(skeleton.names.map((name, i) => [name, i])) };
}

// Makes a template's meshes skinned to its actor's skeleton, when its model is (data: the server's model JSON;
// parts: the template's meshes, in data.meshes' order). Resolves to the template, ready for copyTemplate.
export async function skinTemplate(group, data, parts) {
  if (!data.skeleton || !data.skinBones?.length || !data.meshes.some((m) => m.joints)) return group;
  const { roots, bones, index } = buildBones(await loadSkeleton(data.skeleton));
  // The mesh's bones are a list of its own; the skeleton's order is what three indexes.
  const remap = Uint16Array.from(data.skinBones, (name) => index.get(name) ?? 0);
  group.add(...roots);
  group.updateMatrixWorld(true);
  const skeleton = new THREE.Skeleton(bones);
  data.meshes.forEach((mesh, i) => {
    const part = parts[i];
    if (!mesh.joints || !part) return;
    const joints = decodeBase64(mesh.joints, Uint16Array).map((j) => remap[j] ?? 0);
    part.geometry.setAttribute("skinIndex", new THREE.Uint16BufferAttribute(joints, 4));
    part.geometry.setAttribute("skinWeight", new THREE.Float32BufferAttribute(decodeBase64(mesh.weights, Float32Array), 4));
    const skinned = new THREE.SkinnedMesh(part.geometry, part.material);
    Object.assign(skinned, { castShadow: part.castShadow, receiveShadow: part.receiveShadow });
    skinned.userData = part.userData;
    // A moving part (a bird's circle) leaves the bind pose's bounds.
    skinned.frustumCulled = false;
    skinned.bind(skeleton);
    group.remove(part);
    group.add(skinned);
    parts[i] = skinned;
  });
  group.userData.skinned = true;
  group.userData.skeletonName = data.skeleton;
  group.userData.idle = data.animations?.[IDLE_CLIP]?.file ?? null;
  return group;
}

// A template's nodes for one more object: its meshes, and for a skinned one its own skeleton bound to them.
export function copyTemplate(template) {
  if (!template.userData.skinned) return template.children.map((child) => child.clone());
  return [...SkeletonUtils.clone(template).children];
}

// A baked clip as three keyframe tracks, only for bones the skeleton has.
function loadClip(file, skeletonName) {
  const key = `${file.toLowerCase()}|${skeletonName.toLowerCase()}`;
  if (!clips.has(key)) {
    clips.set(key, Promise.all([getJson(`api/animation?name=${encodeURIComponent(file)}&v=${ASSET_VERSION}`), loadSkeleton(skeletonName)])
      .then(([baked, skeleton]) => {
        const names = new Set(skeleton.names);
        const step = baked.frames > 1 ? baked.duration / (baked.frames - 1) : 0;
        const times = Float32Array.from({ length: baked.frames }, (_, i) => i * step);
        const values = (channel) => (typeof channel === "string" ? decodeBase64(channel, Float32Array) : Float32Array.from(channel));
        const tracks = [];
        for (const track of baked.tracks) {
          if (!names.has(track.bone)) continue;
          const at = (channel) => (typeof channel === "string" ? times : [0]);
          const node = boneName(track.bone);
          tracks.push(new THREE.QuaternionKeyframeTrack(`${node}.quaternion`, at(track.rotation), values(track.rotation)));
          tracks.push(new THREE.VectorKeyframeTrack(`${node}.position`, at(track.position), values(track.position)));
          tracks.push(new THREE.VectorKeyframeTrack(`${node}.scale`, at(track.scale), values(track.scale)));
        }
        return tracks.length ? new THREE.AnimationClip(file, baked.duration, tracks) : null;
      })
      .catch(() => null));
  }
  return clips.get(key);
}

// Starts a placed copy of a skinned template on its idle clip, from a random point in it.
export function playIdle(object, template) {
  if (template.userData.idle) playClip(object, template.userData.idle, template.userData.skeletonName);
}

// Plays a clip (a .gr2, made for that skeleton) on the bones under object, looping, from a random point in it.
export function playClip(object, file, skeletonName) {
  loadClip(file, skeletonName).then((clip) => {
    if (!clip) return;
    const mixer = new THREE.AnimationMixer(object);
    mixer.clipAction(clip).play();
    mixer.setTime(Math.random() * clip.duration);
    players.add({ mixer, object });
  });
}

// A character's skeleton for its parts to share: { roots, bones, index (NAME -> i), skeleton, name }. The roots go
// under the character's group; skeleton is made once they're there (finishRig).
export async function characterRig(skeletonName) {
  const { roots, bones, index } = buildBones(await loadSkeleton(skeletonName));
  return { roots, bones, index, skeleton: null, name: skeletonName.toLowerCase() };
}

// Binds the rig once its roots are in the character's group (its bind pose then gives the inverse bind matrices).
export function finishRig(rig, group) {
  group.add(...rig.roots);
  group.updateMatrixWorld(true);
  rig.skeleton = new THREE.Skeleton(rig.bones);
}

const remapped = new WeakMap();          // geometry -> Map(rig skeleton -> geometry with its indices)

// A part's geometry with its skin indices on the rig's skeleton: the same when the part was made on it, else
// re-indexed by bone name (parts made on a skeleton of their own, borrowed for this body).
export async function skinOn(geometry, partSkeleton, rig) {
  if (!partSkeleton || partSkeleton.toLowerCase() === rig.name) return geometry;
  if (!remapped.has(geometry)) remapped.set(geometry, new Map());
  const cache = remapped.get(geometry);
  if (!cache.has(rig.name)) {
    const names = (await loadSkeleton(partSkeleton)).names;
    const table = Uint16Array.from(names, (name) => rig.index.get(name) ?? 0);
    const copy = new THREE.BufferGeometry();
    for (const [name, attribute] of Object.entries(geometry.attributes)) copy.setAttribute(name, attribute);
    copy.setIndex(geometry.index);
    copy.setAttribute("skinIndex", new THREE.Uint16BufferAttribute(geometry.attributes.skinIndex.array.map((j) => table[j] ?? 0), 4));
    copy.boundingSphere = geometry.boundingSphere;
    cache.set(rig.name, copy);
  }
  return cache.get(rig.name);
}

function inScene(object) {
  let node = object;
  while (node.parent) node = node.parent;
  return node.isScene;
}

const where = new THREE.Vector3();

// Advances every copy near the camera; copies whose chunk (or character) has gone are let go.
export function updateAnimations(dt, camera) {
  for (const player of players) {
    const { mixer, object } = player;
    if (!inScene(object)) {
      players.delete(player);
      continue;
    }
    if (object.visible && object.getWorldPosition(where).distanceTo(camera.position) < ANIMATE_RANGE) mixer.update(dt);
  }
}
