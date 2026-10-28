// The content browser: every actor the client has, as a grid of rendered thumbnails to place in the world.
//
// It opens on what zones actually place (counted across every zone's chunks), with what the loaded chunks use,
// favourites, recently placed and name prefixes (bw_ Briarwood, sh_ Snowhill, hsg_ housing, ...) as groups,
// and kinds (trees, rocks, buildings, ...) matched from names. Click a tile to place it, or drag it into the view.
// Its NPCs side lists Sanctuary's NPC definitions (Npcs.json) instead, to spawn in the running game.
import * as THREE from "three";
import { copyTemplate } from "./animation.js";
import { getJson } from "./api.js";

const NPC_GROUPS = [["all", "All NPCs"], ["mine", "Made here"], ["placed", "Placed in the world"], ["scripted", "With scripts"], ["merchants", "Merchants"]];

const REGIONS = {
  bw: "Briarwood", bs: "Blackspore", sh: "Snowhill", ss: "Seaside", mv: "Merry Vale", sg: "Sacred Grove (shared)",
  gl: "Gloam", sv: "Sunstone", wc: "Wilds", gw: "Wilds", lc: "Lavender Coast", hsg: "Housing", housing: "Housing",
  water: "Water", cave: "Caves", mkt: "Market", evnt: "Events", event: "Events", farming: "Farming", kt: "Kart tracks",
};

// Character, pet and equipment actors: hidden unless "Characters and gear" is on.
const CHARACTER_PREFIXES = new Set(["freestyle", "human", "pixie", "pet", "fairy", "warrior", "worker", "ninja", "superhero",
  "caster", "brawler", "archer", "courier", "fisherman", "druid", "dog", "sword", "military", "medic", "chef", "adventurer",
  "postman", "card", "kart", "mount", "player", "npc"]);

export const KINDS = [
  ["Trees", /tree|palm|pine|oak|willow|stump|trunk|log(?!o)/],
  ["Rocks", /rock|boulder|stone|cliff|crystal|pebble|ore/],
  ["Plants", /bush|shrub|flower|plant|grass|fern|mushroom|vine|hedge|reed|cactus|leaf|lily|topiary|rose/],
  ["Buildings", /house|hut|tower|castle|building|shop|inn|cottage|tent|barn|mill|temple|bldg|arch|gate|door|bridge|dock|stair/],
  ["Walls & fences", /fence|wall|rail|post|pillar|column|barrier/],
  ["Furniture", /chair|table|bench|crate|barrel|box|cart|chest|bed|shelf|pot|basket|sack|stool|desk|rug|wagon/],
  ["Lights", /lamp|lantern|torch|light|candle|fire|brazier/],
  ["Water", /water|waterfall|river|pond|fountain|ocean|lake/],
  ["Signs", /sign|banner|flag|poster|board|marker/],
];

function escapeText(text) {
  return String(text ?? "").replace(/[&<>"]/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;" })[c]);
}

function storageGet(key, fallback) {
  try { return JSON.parse(localStorage.getItem(key)) ?? fallback; } catch { return fallback; }
}

function storageSet(key, value) {
  try { localStorage.setItem(key, JSON.stringify(value)); } catch { /* private mode */ }
}

// Renders actor thumbnails one at a time in a small offscreen renderer and keeps them on the server.
class Thumbnails {
  constructor(library) {
    this.library = library;
    this.size = 128;
    this.renderer = new THREE.WebGLRenderer({ antialias: true, alpha: true, preserveDrawingBuffer: true });
    this.renderer.setSize(this.size, this.size);
    this.renderer.outputColorSpace = THREE.SRGBColorSpace;
    this.scene = new THREE.Scene();
    this.scene.add(new THREE.HemisphereLight(0xffffff, 0x77705f, 1.6));
    const sun = new THREE.DirectionalLight(0xffffff, 2.2);
    sun.position.set(-3, 5, 4);
    this.scene.add(sun);
    this.camera = new THREE.PerspectiveCamera(30, 1, 0.05, 5000);
    this.queue = [];
    this.busy = false;
    this.done = new Map();
  }

  // Shows name's thumbnail in img: the server's saved one, else one rendered now (and saved).
  request(name, img) {
    // Hidden until there's a picture, rather than a broken-image icon while it renders.
    img.style.visibility = "hidden";
    img.onload = () => { img.style.visibility = ""; };
    if (this.done.has(name)) { img.src = this.done.get(name); return; }
    img.src = `api/thumb?name=${encodeURIComponent(name)}`;
    img.onerror = () => {
      img.onerror = null;
      this.queue.push([name, img]);
      this.pump();
    };
  }

  async pump() {
    if (this.busy) return;
    this.busy = true;
    while (this.queue.length) {
      const [name, img] = this.queue.pop();
      if (!img.isConnected) continue;
      try {
        const url = await this.render(name);
        this.done.set(name, url);
        img.src = url;
      } catch {
        img.classList.add("broken");
        img.removeAttribute("src");
        img.style.visibility = "";
      }
    }
    this.busy = false;
  }

  async render(name) {
    const template = await this.library.model(name);
    const group = new THREE.Group();
    for (const node of copyTemplate(template)) group.add(node);
    // Give the textures a moment to arrive.
    const maps = [];
    group.traverse((o) => { if (o.material?.map) maps.push(o.material.map); });
    const deadline = performance.now() + 2500;
    while (maps.some((m) => !m.image) && performance.now() < deadline) await new Promise((r) => setTimeout(r, 60));

    const box = new THREE.Box3().setFromObject(group);
    const centre = box.getCenter(new THREE.Vector3());
    const radius = Math.max(box.getSize(new THREE.Vector3()).length() / 2, 0.1);
    const distance = radius / Math.sin(THREE.MathUtils.degToRad(this.camera.fov / 2)) * 1.05;
    const direction = new THREE.Vector3(0.75, 0.55, 1).normalize();
    this.camera.position.copy(centre).addScaledVector(direction, distance);
    this.camera.near = distance / 100;
    this.camera.far = distance * 4;
    this.camera.updateProjectionMatrix();
    this.camera.lookAt(centre);
    this.scene.add(group);
    this.renderer.render(this.scene, this.camera);
    this.scene.remove(group);
    const url = this.renderer.domElement.toDataURL("image/png");
    fetch(`api/thumb?name=${encodeURIComponent(name)}`, { method: "POST", body: await (await fetch(url)).blob() }).catch(() => {});
    return url;
  }
}

export class ContentBrowser {
  // events: { pick(name), pickNpc({ id, name, model }), editNpc(id), nearby() -> Map(name -> count) }
  constructor(element, library, events) {
    this.element = element;
    this.events = events;
    this.thumbnails = new Thumbnails(library);
    this.names = [];
    this.usage = null;
    this.group = "placed";
    this.kind = null;
    this.query = "";
    this.characters = false;
    this.mode = storageGet("zone-editor-content-mode", "actors");   // actors, or npcs (definitions)
    this.npcGroup = "all";
    this.definitions = [];
    this.favourites = new Set(storageGet("zone-editor-favourites", []));
    this.recent = storageGet("zone-editor-recent", []);
    this.selected = null;
    this.shown = 0;
    this.observer = new IntersectionObserver((entries) => {
      for (const entry of entries) {
        if (!entry.isIntersecting) continue;
        this.observer.unobserve(entry.target);
        this.thumbnails.request(entry.target.dataset.actor, entry.target.querySelector("img"));
      }
    }, { root: null, rootMargin: "200px" });
    this.build();
  }

  build() {
    this.element.innerHTML = `
      <div class="cb-resize" title="Drag to resize"></div>
      <div class="cb-groups"></div>
      <div class="cb-main">
        <div class="cb-bar">
          <div class="seg cb-mode"><button data-mode="actors">Actors</button><button data-mode="npcs" title="NPC definitions (Npcs.json), to spawn in the running game">NPCs</button></div>
          <input class="cb-search" placeholder="Search actors: tree, fence, bw_house, lantern…">
          <div class="cb-kinds">${KINDS.map(([kind]) => `<button data-kind="${kind}">${kind}</button>`).join("")}</div>
          <label class="inline"><input class="cb-characters" type="checkbox"> Characters and gear</label>
          <span class="cb-count"></span>
        </div>
        <div class="cb-grid"></div>
      </div>`;
    this.element.querySelector(".cb-search").addEventListener("input", (e) => {
      clearTimeout(this.timer);
      this.timer = setTimeout(() => { this.query = e.target.value.toLowerCase(); this.mode === "npcs" ? this.loadNpcs() : this.render(); }, 150);
    });
    for (const button of this.element.querySelectorAll("[data-mode]")) {
      button.addEventListener("click", () => {
        this.mode = button.dataset.mode;
        storageSet("zone-editor-content-mode", this.mode);
        this.mode === "npcs" ? this.loadNpcs() : this.render();
      });
    }
    for (const button of this.element.querySelectorAll("[data-kind]")) {
      button.addEventListener("click", () => { this.kind = this.kind === button.dataset.kind ? null : button.dataset.kind; this.render(); });
    }
    this.element.querySelector(".cb-characters").addEventListener("change", (e) => { this.characters = e.target.checked; this.render(); });
    const grid = this.element.querySelector(".cb-grid");
    grid.addEventListener("scroll", () => {
      if (grid.scrollTop + grid.clientHeight > grid.scrollHeight - 300) this.renderMore();
    });

    // Drag the top edge to resize the drawer; the height is remembered.
    const viewport = this.element.parentElement;
    const setHeight = (height) => {
      const clamped = Math.round(Math.max(160, Math.min(height, viewport.clientHeight * 0.8)));
      viewport.style.setProperty("--cb-height", `${clamped}px`);
      return clamped;
    };
    setHeight(storageGet("zone-editor-content-height", 280));
    const handle = this.element.querySelector(".cb-resize");
    handle.addEventListener("pointerdown", (event) => {
      event.preventDefault();
      handle.setPointerCapture(event.pointerId);
      handle.classList.add("dragging");
      const bottom = viewport.getBoundingClientRect().bottom;
      const move = (e) => setHeight(bottom - e.clientY);
      const up = (e) => {
        handle.releasePointerCapture(event.pointerId);
        handle.classList.remove("dragging");
        handle.removeEventListener("pointermove", move);
        handle.removeEventListener("pointerup", up);
        storageSet("zone-editor-content-height", setHeight(bottom - e.clientY));
      };
      handle.addEventListener("pointermove", move);
      handle.addEventListener("pointerup", up);
    });
  }

  async load() {
    const data = await getJson("api/actors");
    // Asked again while the server works things out; the grid is left alone (and where it's scrolled to) until
    // there's something new.
    const changed = data.names.length !== this.names.length || !data.usage !== !this.usage;
    this.names = data.names;
    this.usage = data.usage;              // by lowercase name
    this.byLower = new Map(this.names.map((n) => [n.toLowerCase(), n]));
    if (changed && this.mode !== "npcs") this.render();
    if (this.mode === "npcs" && !this.definitions.length) this.loadNpcs();
    // The zones' usage, and the actors with nothing to draw, are worked out in the background the first time; ask
    // again until both are there.
    if (!this.usage || !data.checked) setTimeout(() => this.load(), 8000);
  }

  isCharacter(name) {
    return CHARACTER_PREFIXES.has(name.split("_")[0].toLowerCase());
  }

  uses(name) {
    return this.usage?.[name.toLowerCase()];
  }

  // The names for the current group, kind, search and character filter, best first.
  filtered() {
    const words = this.query.split(/\s+/).filter(Boolean);
    const kind = KINDS.find(([k]) => k === this.kind)?.[1];
    let names;
    const nearby = this.events.nearby();
    // Zones place some actors the client has nothing to draw with; those aren't listed.
    if (this.group === "placed") names = this.usage ? Object.keys(this.usage).filter((n) => this.byLower.has(n)).map((n) => this.byLower.get(n)) : this.names;
    else if (this.group === "nearby") names = [...nearby.keys()];
    else if (this.group === "favourites") names = [...this.favourites];
    else if (this.group === "recent") names = [...this.recent];
    else if (this.group === "all") names = this.names;
    else names = this.names.filter((n) => n.split("_")[0].toLowerCase() === this.group);
    names = names.filter((n) => {
      const lower = n.toLowerCase();
      return (this.characters || this.group === "favourites" || !this.isCharacter(n))
        && words.every((w) => lower.includes(w)) && (!kind || kind.test(lower));
    });
    const rank = (n) => this.group === "nearby" ? nearby.get(n) : this.uses(n)?.[0] ?? 0;
    if (this.group !== "recent") names.sort((a, b) => rank(b) - rank(a) || a.localeCompare(b));
    return names;
  }

  renderGroups() {
    if (this.mode === "npcs") return;
    const counts = new Map();
    for (const name of this.names) {
      if (!this.characters && this.isCharacter(name)) continue;
      const prefix = name.split("_")[0].toLowerCase();
      counts.set(prefix, (counts.get(prefix) || 0) + 1);
    }
    const prefixes = [...counts].filter(([, count]) => count >= 8).sort((a, b) => (REGIONS[b[0]] ? 1 : 0) - (REGIONS[a[0]] ? 1 : 0) || b[1] - a[1]);
    const nearby = this.events.nearby();
    const fixed = [
      ["placed", `Placed in zones${this.usage ? ` (${Object.keys(this.usage).filter((n) => this.byLower.has(n)).length})` : " (counting…)"}`],
      ["nearby", `Used nearby (${nearby.size})`],
      ["favourites", `★ Favourites (${this.favourites.size})`],
      ["recent", `Recent (${this.recent.length})`],
      ["all", `All actors (${this.names.length})`],
    ];
    this.element.querySelector(".cb-groups").innerHTML =
      fixed.map(([key, label]) => `<div data-group="${key}" class="${this.group === key ? "active" : ""}">${label}</div>`).join("") +
      `<h5>By name</h5>` +
      prefixes.map(([prefix, count]) => `<div data-group="${prefix}" class="${this.group === prefix ? "active" : ""}">${prefix}_ ${REGIONS[prefix] ? `<small>${REGIONS[prefix]}</small>` : ""} <span>${count}</span></div>`).join("");
    for (const row of this.element.querySelectorAll("[data-group]")) {
      row.addEventListener("click", () => { this.group = row.dataset.group; this.render(); });
    }
  }

  render() {
    this.element.classList.toggle("npcs", this.mode === "npcs");
    for (const button of this.element.querySelectorAll("[data-mode]")) button.classList.toggle("active", button.dataset.mode === this.mode);
    this.element.querySelector(".cb-search").placeholder = this.mode === "npcs" ? "Search NPCs: name, actor or id…" : "Search actors: tree, fence, bw_house, lantern…";
    if (this.mode === "npcs") return this.renderNpcs();
    this.renderGroups();
    for (const button of this.element.querySelectorAll("[data-kind]")) button.classList.toggle("active", button.dataset.kind === this.kind);
    this.list = this.filtered();
    this.element.querySelector(".cb-count").textContent = `${this.list.length} actor${this.list.length === 1 ? "" : "s"}`;
    const grid = this.element.querySelector(".cb-grid");
    grid.innerHTML = this.list.length ? "" : `<p class="hint">Nothing here${this.group === "favourites" ? " yet: star a tile to keep it here" : ""}.</p>`;
    grid.scrollTop = 0;
    this.shown = 0;
    this.renderMore();
  }

  renderMore() {
    if (this.mode === "npcs") return;
    const grid = this.element.querySelector(".cb-grid");
    const next = this.list.slice(this.shown, this.shown + 120);
    this.shown += next.length;
    const nearby = this.events.nearby();
    for (const name of next) {
      const usage = this.uses(name);
      const uses = this.group === "nearby" ? nearby.get(name) : usage?.[0];
      const tile = document.createElement("div");
      tile.className = `cb-tile${name === this.selected ? " active" : ""}`;
      tile.dataset.actor = name;
      tile.draggable = true;
      tile.title = `${name}${uses ? `\nPlaced ${uses} time${uses > 1 ? "s" : ""}${usage && this.group !== "nearby" ? ` in ${usage[1]} zone${usage[1] > 1 ? "s" : ""}` : " nearby"}` : ""}\nClick to place, or drag into the view`;
      tile.innerHTML = `<img alt=""><span>${name.replace(/\.a[dg]r$/i, "")}</span><b class="${this.favourites.has(name) ? "on" : ""}" title="Favourite">★</b>`;
      tile.addEventListener("click", (event) => {
        if (event.target.tagName === "B") return this.toggleFavourite(name, event.target);
        this.select(name);
        this.events.pick(name);
      });
      tile.addEventListener("dragstart", (event) => {
        event.dataTransfer.setData("text/x-zone-actor", name);
        event.dataTransfer.effectAllowed = "copy";
        this.select(name);
        this.events.pick(name);
      });
      grid.appendChild(tile);
      this.observer.observe(tile);
    }
  }

  // --- NPC definitions ---------------------------------------------------------------------------------------------

  async loadNpcs() {
    try {
      const data = await getJson(`api/npcs?q=${encodeURIComponent(this.query)}&limit=600`);
      this.definitions = data.definitions;
      this.npcTotal = data.total;
      this.npcError = null;
    } catch (error) {
      this.definitions = [];
      this.npcError = error.message;
    }
    this.render();
  }

  renderNpcs() {
    const counts = {
      all: this.definitions.length,
      mine: this.definitions.filter((d) => d.editorMade).length,
      placed: this.definitions.filter((d) => d.placedInWorld > 0).length,
      scripted: this.definitions.filter((d) => d.scripts.length).length,
      merchants: this.definitions.filter((d) => d.merchant).length,
    };
    this.element.querySelector(".cb-groups").innerHTML = NPC_GROUPS
      .map(([key, label]) => `<div data-npc-group="${key}" class="${this.npcGroup === key ? "active" : ""}">${label} <span>${counts[key]}</span></div>`).join("") +
      `<p class="hint">Spawn one in the running game: click it, then the ground. ✎ edits its definition.</p>`;
    for (const row of this.element.querySelectorAll("[data-npc-group]")) {
      row.addEventListener("click", () => { this.npcGroup = row.dataset.npcGroup; this.renderNpcs(); });
    }
    const show = this.definitions.filter((d) => ({
      all: true, mine: d.editorMade, placed: d.placedInWorld > 0, scripted: d.scripts.length > 0, merchants: d.merchant,
    })[this.npcGroup]);
    this.element.querySelector(".cb-count").textContent = `${show.length} NPC${show.length === 1 ? "" : "s"}${this.npcTotal > this.definitions.length ? ` (first ${this.definitions.length} of ${this.npcTotal})` : ""}`;
    const grid = this.element.querySelector(".cb-grid");
    grid.scrollTop = 0;
    grid.innerHTML = show.length ? "" : `<p class="hint">${this.npcError ? escapeText(this.npcError) : "Nothing here."}</p>`;
    for (const definition of show) {
      const tile = document.createElement("div");
      tile.className = `cb-tile${this.selectedNpc === definition.id ? " active" : ""}`;
      tile.dataset.actor = definition.model ?? "";
      tile.title = `#${definition.id}${definition.model ? ` · ${definition.model}` : ""}${definition.scripts.length ? `
Scripts: ${definition.scripts.join(", ")}` : ""}` +
        `${definition.placedInWorld ? `
Placed in the world: ${definition.placedInWorld}` : ""}
Click to spawn one, then click the ground`;
      tile.innerHTML = `<img alt=""><span>${escapeText(definition.name ?? definition.model?.replace(/\.a[dg]r$/i, "") ?? `NPC ${definition.id}`)}</span>
        <small>#${definition.id}</small><b class="edit" title="Edit its definition">✎</b>`;
      tile.addEventListener("click", (event) => {
        if (event.target.tagName === "B") return this.events.editNpc(definition.id);
        this.selectedNpc = definition.id;
        for (const other of grid.querySelectorAll(".cb-tile")) other.classList.toggle("active", other === tile);
        this.events.pickNpc({ id: definition.id, name: definition.name, model: definition.model });
      });
      grid.appendChild(tile);
      if (definition.model) this.observer.observe(tile);
    }
  }

  select(name) {
    this.selected = name;
    for (const tile of this.element.querySelectorAll(".cb-tile")) tile.classList.toggle("active", tile.dataset.actor === name);
  }

  toggleFavourite(name, star) {
    if (this.favourites.has(name)) this.favourites.delete(name); else this.favourites.add(name);
    star.classList.toggle("on", this.favourites.has(name));
    storageSet("zone-editor-favourites", [...this.favourites]);
    this.renderGroups();
  }

  // Called when an actor is placed, for the Recent group.
  placed(name) {
    this.recent = [name, ...this.recent.filter((n) => n !== name)].slice(0, 40);
    storageSet("zone-editor-recent", this.recent);
  }
}
