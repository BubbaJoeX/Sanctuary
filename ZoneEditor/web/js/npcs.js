// NPCs in the live world, for the zone editor:
// - NpcInspector: a placed NPC's, model's or prop's settings (name, look, behaviour) and who sees it when (its event
//   and conditions), in the inspector. Each change shows in game at once and is one undo step (ObjectSettings on the
//   Gateway: what !npc and !prop set in game).
// - ConversationEditor: a world NPC's dialogue tree, over the view (admins; what !conv builds).
// - DefinitionEditor: Sanctuary's NPC definitions (src/Resources/Npcs.json), which NPCs spawn from: any of them
//   changed, new ones made for actors (admins). The editor's server writes the file; the Gateway then reloads it and
//   respawns this zone's NPCs built on the one that changed.
import { getJson, postJson } from "./api.js";

// The "Placed model" definition placed models are built on: still and solid, a prop more than an NPC.
export const PLACED_MODEL = 45000;
const DISPOSITIONS = ["hostile", "neutral", "ally"];
const BEHAVIORS = { stay: "Stays put", wander: "Wanders about", follow: "Follows your character" };
const ICON_CHOICES = 60;

export function escape(text) {
  return String(text ?? "").replace(/[&<>"']/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" })[c]);
}

const MONTHS = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];
const seasonDates = (s) => `${MONTHS[s.startMonth - 1]} ${s.startDay} to ${MONTHS[s.endMonth - 1]} ${s.endDay}`;

const clock = (hour) => `${String(Math.floor(hour)).padStart(2, "0")}:${String(Math.floor(hour % 1 * 60)).padStart(2, "0")}`;
const iconLabel = (icon) => `${icon.type}/${icon.iconId}/${icon.state}`;

// Keeps the overlay's keys its own: Ctrl+S saves, Esc closes, and the editor behind sees none of them.
function ownKeys(root, save, close) {
  root.addEventListener("keydown", (e) => {
    if ((e.ctrlKey || e.metaKey) && e.code === "KeyS") {
      e.preventDefault();
      save();
    } else if (e.key === "Escape") {
      e.preventDefault();
      close();
    }
    if (e.code !== "F11") e.stopPropagation();
  });
}

// --- the inspector's sections ----------------------------------------------------------------------------------

export class NpcInspector {
  // live: the LiveLayer. hooks: { step(pending, done) (main.js's liveStep), toast(text, kind), refresh(object) to draw
  // the inspector again, editConversation(object), editDefinition(id), makeNpc(object), aliases(file) -> its texture
  // and tint alias names }
  constructor(live, hooks) {
    this.live = live;
    this.hooks = hooks;
    this.options = null;
    this.admin = false;
  }

  // Draws object's sections into container; its settings are fetched once per selection, then kept on it.
  render(container, object) {
    const settings = object.userData.settings;
    if (!settings) {
      container.innerHTML = '<p class="hint">Loading its settings…</p>';
      this.load(object);
      return;
    }
    if (settings.error) {
      container.innerHTML = `<p class="hint">${escape(settings.error)}</p>`;
      return;
    }
    container.innerHTML = this.html(object, settings);
    this.bind(container, object, settings);
  }

  async load(object) {
    if (object.userData.settingsLoading) return;
    object.userData.settingsLoading = true;
    try {
      const [settings, options, me] = await Promise.all([
        this.live.getSettings(object), this.live.npcOptions(), this.live.me().catch(() => null),
      ]);
      this.options = options;
      this.admin = me?.role === "Admin";
      object.userData.settings = settings;
      object.userData.aliases = await this.hooks.aliases(object.userData.live.model).catch(() => null);
    } catch (error) {
      object.userData.settings = { error: error.message };
    } finally {
      object.userData.settingsLoading = false;
    }
    this.hooks.refresh(object);
  }

  html(object, s) {
    const o = this.options;
    const npc = s.kind === "npc";
    const model = s.def === PLACED_MODEL;
    const c = s.conditions ?? {};
    const aliases = object.userData.aliases;
    const animations = o.animations.some((a) => a.id === s.animation) ? o.animations : [{ id: s.animation, label: `Animation ${s.animation}` }, ...o.animations];
    const list = (id, values) => values?.length ? `<datalist id="${id}">${values.map((v) => `<option value="${escape(v)}">`).join("")}</datalist>` : "";
    return `
      <h4>${npc ? (model ? "Placed model" : "NPC") : "Prop"} settings</h4>
      <label>Name <input data-field="name" maxlength="64" value="${escape(s.name ?? "")}" placeholder="${escape(s.defName ?? "No name")}"
        title="Empty: the ${npc ? "definition" : "item"}'s name"></label>
      <label class="check"><input type="checkbox" data-field="nameplate" ${s.nameplate ? "checked" : ""}> Nameplate over it</label>
      <label>Animation <select data-field="animation">${animations.map((a) => `<option value="${a.id}" ${a.id === s.animation ? "selected" : ""}>${escape(a.label)}${a.loop === false ? " (once)" : ""}</option>`).join("")}</select></label>
      ${npc && !model ? `
        <div class="two">
          <label>Disposition <select data-field="disposition">${DISPOSITIONS.map((d) => `<option ${d === s.disposition ? "selected" : ""}>${d}</option>`).join("")}</select></label>
          <label>Mob <select data-field="mob" ${s.disposition === "hostile" ? "" : "disabled"} title="What it fights as while hostile (Mobs.json)">
            ${o.mobs.map((m) => `<option ${m === (s.mob ?? o.mobs[0]) ? "selected" : ""}>${escape(m)}</option>`).join("")}</select></label>
        </div>
        <div class="two">
          <label>Behaviour <select data-field="behavior">${Object.entries(BEHAVIORS).map(([key, label]) => `<option value="${key}" ${key === s.behavior ? "selected" : ""}>${label}</option>`).join("")}</select></label>
          <label>Speed <input type="number" data-field="speed" min="0.25" max="50" step="0.25" value="${s.speed}"></label>
        </div>
        ${s.behavior === "wander" ? `<label>Wander radius (m) <input type="number" data-field="wanderRadius" min="1" max="200" step="1" value="${s.wanderRadius}"></label>` : ""}` : ""}
      <div class="two">
        <label>Texture variant <input data-field="textureAlias" list="npc-textures" value="${escape(s.textureAlias ?? "")}" placeholder="default"></label>
        <label>Tint alias <input data-field="tintAlias" list="npc-tints" value="${escape(s.tintAlias ?? "")}" placeholder="none"></label>
      </div>
      ${list("npc-textures", aliases?.textures)}${list("npc-tints", aliases?.tints)}
      <div class="two">
        <label>Effect id <input type="number" data-field="effect" min="0" value="${s.effect}" title="A composite effect it plays (0: none)"></label>
        <label>Height offset <input type="number" data-field="offset" min="-50" max="50" step="0.1" value="${s.offset}"></label>
      </div>
      <label>Icons over it</label>
      <div class="chips">${s.icons.length ? s.icons.map((icon, i) => `<span class="chip"><img class="chip-icon" alt="" src="api/icon?notification=${icon.iconId}&size=32" onerror="this.remove()">${iconLabel(icon)} <b data-icon-remove="${i}" title="Remove">×</b></span>`).join("") : '<span class="hint">None</span>'}</div>
      <select data-icon-add ${s.icons.length >= 4 ? "disabled" : ""}><option value="">Add an icon…</option>
        ${o.icons.slice(0, ICON_CHOICES).map((icon, i) => `<option value="${i}">${iconLabel(icon)} · ${escape(icon.examples || "unnamed")} (${icon.uses})</option>`).join("")}</select>

      <h4>Who sees it</h4>
      <datalist id="npc-events">${o.seasons.map((x) => `<option value="${escape(x.key)}" label="${escape(x.name)}, ${seasonDates(x)}${x.active ? " (on now)" : ""}">`).join("")}
        ${o.events.filter((e) => !o.seasons.some((x) => x.key === e.key)).map((e) => `<option value="${escape(e.key)}" label="event${e.active ? " (on now)" : ""}">`).join("")}</datalist>
      ${npc ? `<label>Spawned during <input data-field="event" list="npc-events" value="${escape(s.event ?? "")}" placeholder="any time"
          title="It's only in the game while this world event (!event) or season is on"></label>` : ""}
      <label>Seen during <input data-cond="event" list="npc-events" value="${escape(c.event ?? "")}" placeholder="any time"
          title="Players see it only while this world event or season is on${npc ? "; unlike Spawned during, it stays in the game" : ""}"></label>
      <label class="check"><input type="checkbox" data-cond="hours" ${c.fromHour != null ? "checked" : ""}> Only between these hours of the game day</label>
      <div class="two">
        <input type="number" data-cond="fromHour" min="0" max="23" value="${c.fromHour ?? 20}" ${c.fromHour != null ? "" : "disabled"} title="From (0-23)">
        <input type="number" data-cond="toHour" min="0" max="24" value="${c.toHour ?? 6}" ${c.fromHour != null ? "" : "disabled"} title="Until (0-24); earlier than From runs past midnight">
      </div>
      <p class="hint">The game clock says ${clock(o.hour)} now; a game day passes in three hours.</p>
      <div class="two">
        <label>Quest <input type="number" data-cond="questId" min="1" value="${c.questId ?? ""}" placeholder="any"></label>
        <label>Its state <select data-cond="questState">${o.questStates.map((q) => `<option ${q === (c.questState ?? "done") ? "selected" : ""}>${q}</option>`).join("")}</select></label>
      </div>
      <div class="two">
        <label>Item <input type="number" data-cond="itemId" min="1" value="${c.itemId ?? ""}" placeholder="any"></label>
        <label>They <select data-cond="itemAbsent"><option value="">carry it</option><option value="1" ${c.itemAbsent ? "selected" : ""}>don't carry it</option></select></label>
      </div>
      <label class="check"><input type="checkbox" data-cond="refereesOnly" ${c.refereesOnly ? "checked" : ""}> Referees only</label>
      <p class="hint">Players see it while all of these hold, checked every second; referees in god mode always do.</p>

      ${npc ? `<h4>Conversation</h4>
        <p class="hint">${s.hasConversation ? "It has a conversation: clicking it opens it." : "None. A conversation is what it says when clicked, with answers to choose."}</p>
        <div class="row"><button data-action="conversation" ${this.admin ? "" : 'disabled title="Admins edit conversations"'}>Edit conversation…</button></div>
        <h4>Definition</h4>
        ${model ? `<p class="hint">A placed model: still, solid and silent. As an NPC it gets a definition of its own in Npcs.json, and moves and animates.</p>
          <div class="row"><button data-action="make-npc" ${this.admin ? "" : 'disabled title="Admins make NPC definitions"'}>Make it an NPC</button></div>`
        : `<p class="hint">Built on #${s.def}${s.defName ? ` ${escape(s.defName)}` : ""} in Npcs.json; a change there reaches every NPC built on it.</p>
          <div class="row"><button data-action="definition" ${this.admin ? "" : 'disabled title="Admins edit NPC definitions"'}>Edit definition…</button></div>`}` : ""}`;
  }

  bind(container, object, s) {
    const send = (patch, done) => this.hooks.step(this.save(object, patch), done);
    for (const input of container.querySelectorAll("[data-field]")) {
      input.addEventListener("change", () => {
        const field = input.dataset.field;
        const value = input.type === "checkbox" ? input.checked : input.type === "number" ? +input.value : field === "animation" ? +input.value : input.value.trim();
        send({ [field]: value }, `Changed its ${{ textureAlias: "texture variant", tintAlias: "tint alias", wanderRadius: "wander radius", offset: "height offset" }[field] ?? field} in game.`);
      });
    }
    const conditions = () => {
      const get = (key) => container.querySelector(`[data-cond="${key}"]`);
      const hours = get("hours").checked;
      const questId = +get("questId").value || null;
      const itemId = +get("itemId").value || null;
      return {
        fromHour: hours ? +get("fromHour").value : null, toHour: hours ? +get("toHour").value : null,
        questId, questState: questId ? get("questState").value : null,
        itemId, itemAbsent: !!itemId && get("itemAbsent").value === "1",
        refereesOnly: get("refereesOnly").checked,
        event: get("event").value.trim().toLowerCase() || null,
      };
    };
    for (const input of container.querySelectorAll("[data-cond]")) {
      input.addEventListener("change", () => {
        if (input.dataset.cond === "hours") {
          for (const key of ["fromHour", "toHour"]) container.querySelector(`[data-cond="${key}"]`).disabled = !input.checked;
        }
        send({ conditions: conditions() }, "Changed who sees it in game.");
      });
    }
    container.querySelector("[data-icon-add]")?.addEventListener("change", (e) => {
      const icon = this.options.icons[+e.target.value];
      if (icon) send({ icons: [...s.icons, { type: icon.type, iconId: icon.iconId, state: icon.state }] }, "Added the icon in game.");
    });
    for (const button of container.querySelectorAll("[data-icon-remove]")) {
      button.addEventListener("click", () => send({ icons: s.icons.filter((_, i) => i !== +button.dataset.iconRemove) }, "Removed the icon in game."));
    }
    container.querySelector('[data-action="conversation"]')?.addEventListener("click", () => this.hooks.editConversation(object));
    container.querySelector('[data-action="definition"]')?.addEventListener("click", () => this.hooks.editDefinition(s.def));
    container.querySelector('[data-action="make-npc"]')?.addEventListener("click", () => this.hooks.makeNpc(object));
  }

  // Sends a change; the settings are fetched again once it's in. False (and why, in a toast) when it was refused.
  async save(object, patch) {
    try {
      const result = await this.live.saveSettings(object, patch);
      delete object.userData.settings;
      if (result.id === null) this.hooks.toast("It's waiting for its event now, so it left the game until the event is on.", "ok");
      return true;
    } catch (error) {
      this.hooks.toast(`Couldn't change it: ${error.message}`, "error");
      this.hooks.refresh(object);
      return false;
    }
  }
}

// --- conversations -----------------------------------------------------------------------------------------------

const MAX_NODES = 64;
const MAX_CHOICES = 8;
const MAX_TEXT = 400;

export class ConversationEditor {
  // root: the (hidden) section it draws in; live: the LiveLayer; hooks: { closed(object) after it closes, saved(object) }
  constructor(root, live, hooks = {}) {
    this.root = root;
    this.live = live;
    this.hooks = hooks;
    this.object = null;
    this.conversation = null;
    this.saved = "null";
    this.node = null;
    this.build();
  }

  get isOpen() {
    return !this.root.hidden;
  }

  get dirty() {
    return JSON.stringify(this.conversation) !== this.saved;
  }

  build() {
    this.root.innerHTML = `
      <header class="se-head">
        <h3 class="se-title"></h3>
        <span class="se-state"></span>
        <span class="se-spacer"></span>
        <button class="primary ce-save" title="Save it on the NPC (Ctrl+S)">Save</button>
        <button class="ce-revert" title="Back to the saved conversation">Revert</button>
        <button class="ce-clear danger" title="Take the conversation off it">Remove</button>
        <button class="se-close" title="Close (Esc)">Close</button>
      </header>
      <div class="se-body">
        <aside class="se-library">
          <div class="se-list ce-nodes"></div>
          <div class="row ce-node-tools"><button class="ce-add-node">Add a node</button></div>
        </aside>
        <div class="se-main ce-node"></div>
      </div>
      <div class="se-message"></div>`;
    const $ = (selector) => this.root.querySelector(selector);
    this.title = $(".se-title");
    this.state = $(".se-state");
    this.nodes = $(".ce-nodes");
    this.pane = $(".ce-node");
    this.message = $(".se-message");
    $(".se-close").addEventListener("click", () => this.close());
    $(".ce-save").addEventListener("click", () => this.save());
    $(".ce-revert").addEventListener("click", () => { this.conversation = JSON.parse(this.saved); this.node = null; this.render(); this.say("Back to the saved conversation."); });
    $(".ce-clear").addEventListener("click", () => this.clear());
    $(".ce-add-node").addEventListener("click", () => this.addNode());
    ownKeys(this.root, () => this.save(), () => this.close());
  }

  async open(object) {
    if (this.isOpen && this.dirty && this.object !== object && !confirm("Drop the unsaved changes to this conversation?")) return;
    this.object = object;
    this.title.textContent = `Conversation · ${object.userData.live.label}`;
    this.root.hidden = false;
    this.conversation = null;
    this.saved = "null";
    this.node = null;
    this.render();
    this.say("Loading…");
    try {
      const { conversation } = await this.live.getConversation(object);
      this.conversation = conversation ?? { start: 1, nodes: [{ id: 1, text: "", choices: [] }] };
      // A new one isn't "unsaved" until it's written in.
      this.saved = JSON.stringify(this.conversation);
      this.render();
      this.say(conversation ? "" : "No conversation yet: write what it says first, and the answers players can pick.");
    } catch (error) {
      this.say(error.message, "error");
    }
  }

  close() {
    if (this.dirty && !confirm("Close without saving the conversation?")) return;
    this.root.hidden = true;
    const object = this.object;
    this.object = null;
    this.hooks.closed?.(object);
  }

  say(text, kind = "") {
    this.message.textContent = text;
    this.message.className = `se-message ${kind}`;
  }

  render() {
    const conversation = this.conversation;
    if (!conversation) {
      this.nodes.innerHTML = "";
      this.pane.innerHTML = "";
      return;
    }
    this.node ??= conversation.nodes.find((n) => n.id === conversation.start) ?? conversation.nodes[0];
    this.renderNodes();
    this.renderNode();
  }

  // The list of nodes and the count, which typing updates without drawing the node again (that would swallow a click).
  renderNodes() {
    const conversation = this.conversation;
    this.state.textContent = `${conversation.nodes.length} node${conversation.nodes.length === 1 ? "" : "s"}${this.dirty ? " · unsaved" : ""}`;
    this.nodes.innerHTML = conversation.nodes.map((n) => `<div data-node="${n.id}" class="${n === this.node ? "active" : ""}">
      ${n.id === conversation.start ? "▶ " : ""}${n.id}. ${escape(n.text.slice(0, 40) || "(no text)")}</div>`).join("");
    for (const row of this.nodes.querySelectorAll("[data-node]")) {
      row.addEventListener("click", () => { this.node = conversation.nodes.find((n) => n.id === +row.dataset.node); this.render(); });
    }
  }

  renderNode() {
    const conversation = this.conversation;
    const node = this.node;
    if (!node) {
      this.pane.innerHTML = "";
      return;
    }
    const targets = (next) => `<option value="0" ${next === 0 ? "selected" : ""}>Ends the conversation</option>` +
      conversation.nodes.filter((n) => n !== node).map((n) => `<option value="${n.id}" ${n.id === next ? "selected" : ""}>Node ${n.id}: ${escape(n.text.slice(0, 30))}</option>`).join("");
    this.pane.innerHTML = `
      <div class="ce-form">
        <div class="row"><h4>Node ${node.id}</h4>
          <label class="inline"><input type="checkbox" class="ce-start" ${node.id === conversation.start ? "checked disabled" : ""}> Starts here</label>
          <span class="se-spacer"></span><button class="ce-delete-node danger" ${conversation.nodes.length > 1 ? "" : "disabled"}>Delete node</button></div>
        <label>What it says <small>(up to ${MAX_TEXT} characters)</small>
          <textarea class="ce-text" maxlength="${MAX_TEXT}" rows="4">${escape(node.text)}</textarea></label>
        <h4>Answers <small>(up to ${MAX_CHOICES}; none: the window just closes)</small></h4>
        ${node.choices.map((choice, i) => `
          <div class="ce-choice" data-choice="${i}">
            <label>Answer <input data-key="text" maxlength="${MAX_TEXT}" value="${escape(choice.text)}"></label>
            <div class="two">
              <label>Goes to <select data-key="next">${targets(choice.next)}</select></label>
              <label>Script hook <input data-key="hook" value="${escape(choice.hook ?? "")}" placeholder="none" title="Passed to its script's onChoice(player, hook)"></label>
            </div>
            <div class="two">
              <label>Gives item <input type="number" data-key="giveItemId" min="0" value="${choice.giveItemId || ""}" placeholder="none"></label>
              <label>How many <input type="number" data-key="giveQuantity" min="1" max="100" value="${choice.giveQuantity || 1}"></label>
            </div>
            <div class="row"><button data-remove-choice="${i}" class="danger">Remove answer</button></div>
          </div>`).join("")}
        <div class="row"><button class="ce-add-choice" ${node.choices.length >= MAX_CHOICES ? "disabled" : ""}>Add an answer</button>
          <button class="ce-add-linked" ${node.choices.length >= MAX_CHOICES || conversation.nodes.length >= MAX_NODES ? "disabled" : ""}
            title="A new node, and an answer here that goes to it">Add an answer with a new node</button></div>
      </div>`;
    const $ = (selector) => this.pane.querySelector(selector);
    const changed = () => this.renderNodes();
    $(".ce-text").addEventListener("input", (e) => { node.text = e.target.value; changed(); });
    $(".ce-start").addEventListener("change", () => { conversation.start = node.id; this.render(); });
    $(".ce-delete-node").addEventListener("click", () => this.deleteNode(node));
    $(".ce-add-choice").addEventListener("click", () => { node.choices.push({ text: "", next: 0, giveItemId: 0, giveQuantity: 1, hook: null }); this.render(); });
    $(".ce-add-linked").addEventListener("click", () => {
      const added = this.addNode(false);
      node.choices.push({ text: "", next: added.id, giveItemId: 0, giveQuantity: 1, hook: null });
      this.render();
    });
    for (const box of this.pane.querySelectorAll("[data-choice]")) {
      const choice = node.choices[+box.dataset.choice];
      for (const input of box.querySelectorAll("[data-key]")) {
        input.addEventListener("input", () => {
          const key = input.dataset.key;
          choice[key] = key === "text" || key === "hook" ? (input.value || (key === "hook" ? null : "")) : +input.value || 0;
          changed();
        });
      }
      box.querySelector("[data-remove-choice]").addEventListener("click", () => { node.choices.splice(+box.dataset.choice, 1); this.render(); });
    }
  }

  addNode(select = true) {
    const conversation = this.conversation;
    if (conversation.nodes.length >= MAX_NODES) {
      this.say(`A conversation has at most ${MAX_NODES} nodes.`, "error");
      return null;
    }
    const node = { id: Math.max(0, ...conversation.nodes.map((n) => n.id)) + 1, text: "", choices: [] };
    conversation.nodes.push(node);
    if (select) this.node = node;
    this.render();
    return node;
  }

  deleteNode(node) {
    const conversation = this.conversation;
    conversation.nodes = conversation.nodes.filter((n) => n !== node);
    // Answers that went there end the conversation instead.
    for (const other of conversation.nodes) for (const choice of other.choices) if (choice.next === node.id) choice.next = 0;
    if (conversation.start === node.id) conversation.start = conversation.nodes[0].id;
    this.node = null;
    this.render();
  }

  async save() {
    if (!this.object || !this.conversation) return;
    try {
      await this.live.saveConversation(this.object, this.conversation);
      this.saved = JSON.stringify(this.conversation);
      this.render();
      this.say("Saved: players who click it get this conversation now.", "ok");
      this.hooks.saved?.(this.object);
    } catch (error) {
      this.say(error.message, "error");
    }
  }

  async clear() {
    if (!this.object || !confirm("Take the conversation off this NPC?")) return;
    try {
      await this.live.clearConversation(this.object);
      this.conversation = { start: 1, nodes: [{ id: 1, text: "", choices: [] }] };
      this.saved = JSON.stringify(this.conversation);
      this.node = null;
      this.render();
      this.say("Removed.", "ok");
      this.hooks.saved?.(this.object);
    } catch (error) {
      this.say(error.message, "error");
    }
  }
}

// --- NPC definitions (Npcs.json) -------------------------------------------------------------------------------------

// Fields the form edits; the rest of an entry is edited as JSON beside it.
const FORM_KEYS = ["Id", "Name", "ModelId", "ModelFileName", "Scale", "TextureAlias", "TintAlias", "HideNamePlate", "Disposition",
  "Animation", "IsInteractable", "Scripts"];

export class DefinitionEditor {
  // root: the (hidden) section it draws in; live: the LiveLayer, which has the Gateway reload Npcs.json; hooks: {
  // pick() -> the content browser's actor, spawn(summary) to spawn one with the Props tool, closed() }
  constructor(root, live, hooks = {}) {
    this.root = root;
    this.live = live;
    this.hooks = hooks;
    this.entry = null;
    this.summary = null;
    this.scripts = [];
    this.build();
  }

  get isOpen() {
    return !this.root.hidden;
  }

  build() {
    this.root.innerHTML = `
      <header class="se-head">
        <h3 class="se-title">NPC definitions</h3>
        <span class="se-state"></span>
        <span class="se-spacer"></span>
        <button class="primary de-save" title="Save it to Npcs.json and have the game reload it (Ctrl+S)">Save</button>
        <button class="de-spawn" title="Spawn one with the Props tool: click the ground">Spawn one…</button>
        <button class="de-copy" title="A new definition like this one">Copy as new</button>
        <button class="de-delete danger" title="Only definitions made here, and only while no placed NPC is built on them">Delete</button>
        <button class="se-close" title="Close (Esc)">Close</button>
      </header>
      <div class="se-body">
        <aside class="se-library">
          <input class="se-search" type="search" placeholder="Search: name, actor or id" autocomplete="off">
          <div class="row de-filters">
            <label class="inline"><input type="checkbox" class="de-mine"> Made here</label>
            <label class="inline"><input type="checkbox" class="de-placed"> In the world</label>
          </div>
          <div class="se-list de-list"></div>
          <div class="row"><button class="de-new" title="A definition for the actor picked in the content browser">New for the content pick</button></div>
        </aside>
        <div class="se-main de-form"></div>
      </div>
      <div class="se-message"></div>`;
    const $ = (selector) => this.root.querySelector(selector);
    this.state = $(".se-state");
    this.list = $(".de-list");
    this.form = $(".de-form");
    this.message = $(".se-message");
    this.search = $(".se-search");
    $(".se-close").addEventListener("click", () => this.close());
    $(".de-save").addEventListener("click", () => this.save());
    $(".de-spawn").addEventListener("click", () => this.spawn());
    $(".de-copy").addEventListener("click", () => this.copy());
    $(".de-delete").addEventListener("click", () => this.remove());
    $(".de-new").addEventListener("click", () => this.createFor(this.hooks.pick?.()));
    for (const filter of [this.search, $(".de-mine"), $(".de-placed")]) {
      filter.addEventListener(filter === this.search ? "input" : "change", () => { clearTimeout(this.timer); this.timer = setTimeout(() => this.find(), 200); });
    }
    ownKeys(this.root, () => this.save(), () => this.close());
  }

  // Opens on a definition (by id), or on the list alone.
  async open(id = null) {
    this.root.hidden = false;
    await this.find();
    if (id !== null) await this.select(id);
    else if (!this.entry) this.renderForm();
    this.search.focus();
  }

  close() {
    this.root.hidden = true;
    this.hooks.closed?.();
  }

  say(text, kind = "") {
    this.message.textContent = text;
    this.message.className = `se-message ${kind}`;
  }

  async find() {
    try {
      const data = await getJson(`api/npcs?q=${encodeURIComponent(this.search.value)}&limit=400`);
      this.scripts = data.scripts;
      const mine = this.root.querySelector(".de-mine").checked;
      const placed = this.root.querySelector(".de-placed").checked;
      this.found = data.definitions.filter((d) => (!mine || d.editorMade) && (!placed || d.placedInWorld > 0));
      this.state.textContent = `${data.total} match${data.total === 1 ? "" : "es"}`;
      this.renderList();
    } catch (error) {
      this.say(error.message, "error");
    }
  }

  renderList() {
    this.list.innerHTML = this.found.map((d) => `<div data-id="${d.id}" class="${d.id === this.entry?.Id ? "active" : ""}" title="${escape(d.model ?? "")}">
      ${d.editorMade ? "✎ " : ""}${escape(d.name ?? d.model ?? "Unnamed")} <small>#${d.id}${d.placedInWorld ? ` · ${d.placedInWorld} placed` : ""}</small></div>`).join("")
      || '<p class="hint">Nothing matches.</p>';
    for (const row of this.list.querySelectorAll("[data-id]")) row.addEventListener("click", () => this.select(+row.dataset.id));
  }

  async select(id) {
    try {
      this.entry = await getJson(`api/npcs/entry?id=${id}`);
      this.summary = this.found?.find((d) => d.id === id) ?? null;
      // One opened from elsewhere may be past the list's first entries: it goes at the top.
      if (!this.summary) {
        this.summary = (await getJson(`api/npcs?q=${id}&limit=50`)).definitions.find((d) => d.id === id) ?? null;
        if (this.summary) this.found = [this.summary, ...(this.found ?? [])];
      }
      this.renderList();
      this.renderForm();
      this.say("");
    } catch (error) {
      this.say(error.message, "error");
    }
  }

  renderForm() {
    const e = this.entry;
    for (const button of this.root.querySelectorAll(".de-save, .de-spawn, .de-copy, .de-delete")) button.disabled = !e;
    if (!e) {
      this.form.innerHTML = `<div class="de-pad"><p class="hint">Pick a definition on the left, or make one for the actor picked in the
        content browser. NPCs spawn from these; a change reaches every NPC built on it once the game reloads them, which saving does.</p></div>`;
      return;
    }
    this.root.querySelector(".de-delete").disabled = e.Id < 50000;
    const others = Object.fromEntries(Object.entries(e).filter(([key]) => !FORM_KEYS.includes(key)));
    const placedModel = e.Id === PLACED_MODEL;
    this.form.innerHTML = `
      <div class="de-pad">
        <h4>#${e.Id}${e.Id >= 50000 ? " <small>made here</small>" : ""}${this.summary?.placedInWorld ? ` <small>· ${this.summary.placedInWorld} placed in the world</small>` : ""}</h4>
        <label>Name <input data-key="Name" maxlength="64" value="${escape(e.Name ?? "")}" placeholder="${e.NameId ? `localized string ${e.NameId}` : "no name"}"></label>
        <label>Actor <small>(its Models.txt row is model ${e.ModelId ?? "?"})</small>
          <div class="row"><input data-key="ModelFileName" value="${escape(e.ModelFileName ?? "")}" ${placedModel ? "disabled" : ""}>
          <button class="de-use-pick" ${placedModel ? "disabled" : ""} title="The actor picked in the content browser">Use the content pick</button></div></label>
        <div class="two">
          <label>Scale <input type="number" data-key="Scale" min="0.05" max="20" step="0.05" value="${e.Scale ?? ""}" placeholder="1"></label>
          <label>Animation <input type="number" data-key="Animation" min="0" value="${e.Animation ?? ""}" placeholder="1 (idle)"></label>
        </div>
        <div class="two">
          <label>Texture variant <input data-key="TextureAlias" value="${escape(e.TextureAlias ?? "")}" placeholder="default"></label>
          <label>Tint alias <input data-key="TintAlias" value="${escape(e.TintAlias ?? "")}" placeholder="none"></label>
        </div>
        <div class="two">
          <label>Disposition <select data-key="Disposition"><option value="">neutral (default)</option>
            ${DISPOSITIONS.map((d, i) => `<option value="${i}" ${e.Disposition === i ? "selected" : ""}>${d}</option>`).join("")}</select></label>
          <span></span>
        </div>
        <label class="inline"><input type="checkbox" data-key="HideNamePlate" ${e.HideNamePlate ? "checked" : ""}> No nameplate</label>
        <label class="inline"><input type="checkbox" data-key="IsInteractable" ${e.IsInteractable !== false ? "checked" : ""}> Players can click it</label>
        <label>Scripts <small>(src/Scripts/Npc)</small></label>
        <div class="de-scripts">${this.scripts.map((name) => `<label class="inline"><input type="checkbox" data-script="${escape(name)}"
          ${(e.Scripts ?? []).includes(name) ? "checked" : ""}> ${escape(name)}</label>`).join("") || '<span class="hint">None in the checkout.</span>'}</div>
        <label>Everything else <small>(JSON: icons, interaction and merchant lists, cursor, ids…)</small>
          <textarea class="de-json" spellcheck="false" rows="12">${escape(JSON.stringify(others, null, 2))}</textarea></label>
      </div>`;
    this.form.querySelector(".de-use-pick").addEventListener("click", () => {
      const pick = this.hooks.pick?.();
      if (!pick) return this.say("Pick an actor in the content browser first.", "error");
      this.form.querySelector('[data-key="ModelFileName"]').value = pick;
    });
  }

  // The form as an entry: form fields over the JSON beside them.
  collect() {
    let others;
    try {
      others = JSON.parse(this.form.querySelector(".de-json").value || "{}");
    } catch (error) {
      throw new Error(`The JSON isn't valid: ${error.message}`);
    }
    if (typeof others !== "object" || Array.isArray(others) || others === null) throw new Error("The JSON is an object of fields.");
    const value = (key) => this.form.querySelector(`[data-key="${key}"]`);
    const number = (key) => value(key).value === "" ? null : +value(key).value;
    const entry = { Id: this.entry.Id, ...others };
    entry.Name = value("Name").value.trim() || null;
    entry.ModelId = this.entry.ModelId;
    entry.ModelFileName = this.entry.Id === PLACED_MODEL ? this.entry.ModelFileName : value("ModelFileName").value.trim();
    entry.Scale = number("Scale");
    entry.Animation = number("Animation");
    entry.TextureAlias = value("TextureAlias").value.trim() || null;
    entry.TintAlias = value("TintAlias").value.trim() || null;
    entry.Disposition = number("Disposition");
    // Booleans only when they differ from the defaults (or were set already).
    entry.HideNamePlate = value("HideNamePlate").checked || ("HideNamePlate" in this.entry ? false : null);
    entry.IsInteractable = value("IsInteractable").checked ? ("IsInteractable" in this.entry ? true : null) : false;
    entry.Scripts = [...this.form.querySelectorAll("[data-script]:checked")].map((box) => box.dataset.script);
    return entry;
  }

  async save() {
    if (!this.entry) return;
    try {
      const { definition } = await postJson("api/npcs/save", this.collect());
      this.entry = definition;
      await this.reload(definition.Id, `Saved #${definition.Id}.`);
      await this.find();
      this.renderForm();
    } catch (error) {
      this.say(error.message, "error");
    }
  }

  // After Npcs.json changed: the running game reloads it, and this zone's NPCs built on def respawn with it.
  async reload(def, saved) {
    if (!this.live.zone) {
      this.say(`${saved} The game reads Npcs.json when it starts; opened from the admin panel, saving reloads it at once.`, "ok");
      return;
    }
    try {
      const respawned = await this.live.reloadDefinitions(def);
      this.say(`${saved} The game reloaded Npcs.json${respawned ? `; ${respawned} NPC(s) built on it here respawned with it` : ""}.`, "ok");
    } catch (error) {
      this.say(`${saved} The game didn't reload it: ${error.message}`, "error");
    }
  }

  async createFor(model, copyOf = undefined, name = undefined) {
    if (!model) return this.say("Pick an actor in the content browser first.", "error");
    try {
      const { definition } = await postJson("api/npcs/new", { model, name, copyOf });
      await this.reload(undefined, `Made #${definition.Id} for ${model}.`);
      this.search.value = "";
      await this.find();
      await this.select(definition.Id);
      return definition;
    } catch (error) {
      this.say(error.message, "error");
      return null;
    }
  }

  copy() {
    if (this.entry) this.createFor(this.entry.ModelFileName, this.entry.Id, `${this.entry.Name ?? "NPC"} copy`);
  }

  async remove() {
    if (!this.entry || !confirm(`Delete #${this.entry.Id} ${this.entry.Name ?? ""} from Npcs.json?`)) return;
    try {
      await postJson("api/npcs/delete", { id: this.entry.Id });
      await this.reload(undefined, `Deleted #${this.entry.Id}.`);
      this.entry = null;
      await this.find();
      this.renderForm();
    } catch (error) {
      this.say(error.message, "error");
    }
  }

  spawn() {
    if (!this.entry) return;
    if (!this.live.zone) return this.say("NPCs spawn in the running game: open the editor from the admin panel, with the zone running.", "error");
    this.hooks.spawn?.(npcSummary(this.entry));
    this.close();
  }
}

// A definition as the content browser and the Props tool show it.
export function npcSummary(entry) {
  return { id: entry.Id, name: entry.Name ?? null, model: entry.ModelFileName || null };
}

// The editor's own definition for an actor (made here, the first if there are several), or a new one: what placing an
// actor as an NPC spawns from.
export async function definitionFor(model) {
  const { definitions } = await getJson(`api/npcs?model=${encodeURIComponent(model)}&limit=400`);
  const mine = definitions.filter((d) => d.editorMade).sort((a, b) => a.id - b.id)[0];
  if (mine) return { summary: mine, created: false };
  const { definition } = await postJson("api/npcs/new", { model });
  return { summary: npcSummary(definition), created: true };
}
