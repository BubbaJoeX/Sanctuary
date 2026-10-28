// The GM script editor for a live placed object (a world NPC, placed model or furniture prop): the in-game GM panel's
// Script tab in the editor. The script is Lua with hooks (onInteract, onProximity, ...) that make the object talk, move
// and react; saving compiles and runs it on the server first, so a broken script never replaces a working one. The
// library beside it holds the shipped templates (read-only, with parameters) and referees' saved scripts.

// What a script can call, as the GM panel lists it (ClientMods/chatWindowCtrl/gm_panel.as).
const API = `HOOKS (define any of them)
onSpawn()  when saved or loaded
onSecond()  every second
onInteract(player)  clicked
onProximity(player)  walks up (8 m)
onLeave(player)  walks away again
onHear(player, message)  says something nearby (10 m)
onChoice(player, hook)  dialogue answer with a hook
onArrive()  reached its moveTo target
onSignal(name, data, fromGuid)  another script's npc:signal
onHurt(player, amount)  onDeath(killer)  onRespawn()  mobs only

TIMERS (on the one-second tick)
after(seconds, fn)  every(seconds, fn)  return an id
cancel(id)

NPC (the global npc)
npc.guid  npc.name  npc.position  npc.home  npc.zone  npc.isMoving
npc:say(text)  npc:sayTo(player, text)
npc:sayLocalized(stringId)
npc:face(player)  npc:setHeading(degrees)
npc:moveTo(x, y, z [, direct])  npc:returnHome()
npc:setSpeed(speed)  npc:setScale(scale)  npc:rename(name)
npc:emote(animId [, loop])  npc:playAnimation(animId)
npc:setBaseAnimation(animId)
npc:playEffect(effectId)
npc:addEffectTag(effectId)  npc:removeEffectTag(tagId)
npc:setCollidable(true|false)
npc:getPlayers(radius)  npc:getPlayer(guid or name)
npc:getNearbyPlayers(radius)
npc:getPlayersOnTop(radius, minHeight, maxHeight)
npc:startConversation(player [, nodeId])
npc:signal(name [, data [, radius]])

MOBS (!npc mob)
npc.isMob  npc.health  npc.maxHealth  npc.isDead  npc.target (guid or nil)
npc:hurt(player, amount)  npc:attack(player, amount)

PLAYER
player.name  player.guid  player.position  player.isGm  player.isMounted
player:message(text)
player:giveItem(itemId [, qty])  player:giveCoins(amount)
player:heal(amount)
player:teleport(x, y, z [, heading])  player:dismount()
player:emote(animId)  player:playEffect(effectId)
player:transform(modelId, seconds)  player:clearTransform()
player:resize(scale, seconds)
player:acceptQuest(questId)  player:hasCompletedQuest(questId)
player.zoneId  player.zoneName  player:warp(zoneId [, x, y, z [, heading]])
player:startMinigame(minigameId)  (ids: !minigame list)
player.health  player.maxHealth  player.inCombat  player.isKnockedOut
player:revive([healthPercent])
player.job  player.jobRank  player:giveStars(amount)
player.coins  player:takeCoins(amount)
player:itemCount(itemId)  player:hasItem(itemId [, qty])
player:takeItem(itemId [, qty])
player.groupId  player.groupMembers  player.isGroupLeader
player:isGroupedWith(other)

ZONE (npc.zone)
zone.id  zone.name
zone:broadcast(text)  zone:broadcastNear(text, x, z, radius)
zone:getFlag(name)  zone:setFlag(name, value)  zone:addFlag(name [, amount])
zone:spawnNpc(npcId, x, y, z, heading)
zone:setClientAreaEnabled(areaId, on)
zone:isSeasonActive(name)  zone:isEventActive(key)
zone.playerCount  zone:getPlayers()  zone:getPlayer(guid or name)

HELPERS
distance(a, b)  pick(list)  chance(probability)  shuffle(list)  countdown(seconds)

LUA
math string table pairs ipairs next select type tostring tonumber pcall error assert unpack print

16 KB per script, 100 ms per hook.`;

// Hook skeletons Insert adds at the cursor.
const SNIPPETS = {
  onSpawn: 'function onSpawn()\n    npc:say("I\'m awake!")\nend',
  onInteract: 'function onInteract(player)\n    npc:face(player)\n    npc:say("Hello, " .. player.name .. "!")\nend',
  onProximity: 'function onProximity(player)\n    npc:face(player)\n    npc:say("Welcome, " .. player.name .. "!")\nend',
  onLeave: 'function onLeave(player)\n    npc:sayTo(player, "Come back soon!")\nend',
  onHear: 'function onHear(player, message)\n    if string.find(string.lower(message), "hello", 1, true) then\n        npc:face(player)\n        npc:say("Hello to you too, " .. player.name .. "!")\n    end\nend',
  onChoice: 'function onChoice(player, hook)\n    if hook == "thanks" then\n        player:message("The NPC smiles at you.")\n    end\nend',
  onSignal: 'function onSignal(name, data, fromGuid)\n    if name == "start" then\n        npc:say("Here we go!")\n    end\nend',
  every: 'every(30, function()\n    npc:say(pick({ "Lovely day!", "Anyone seen my hat?", "Hmm..." }))\nend)',
};

const MAX_LENGTH = 16384;
const LIBRARY_NAME = /^[A-Za-z0-9_-]{1,32}$/;
const NUMERIC = new Set(["int", "float", "item", "npc", "anim"]);

function escape(text) {
  return String(text ?? "").replace(/[&<>"']/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" })[c]);
}

export class ScriptEditor {
  // root: the (hidden) section it draws in; live: the LiveLayer; hooks: { closed() after it closes }.
  constructor(root, live, hooks = {}) {
    this.root = root;
    this.live = live;
    this.hooks = hooks;
    this.object = null;
    this.saved = "";
    this.entries = [];
    this.entry = null;
    this.build();
  }

  get isOpen() {
    return !this.root.hidden;
  }

  get dirty() {
    return this.source.value !== this.saved;
  }

  build() {
    this.root.innerHTML = `
      <header class="se-head">
        <h3 class="se-title"></h3>
        <span class="se-state"></span>
        <span class="se-spacer"></span>
        <button class="se-help-toggle" title="What a script can call">API</button>
        <button class="se-close" title="Close (Esc)">Close</button>
      </header>
      <div class="se-body">
        <aside class="se-library">
          <input class="se-search" type="search" placeholder="Search the library" autocomplete="off">
          <div class="se-list"></div>
          <div class="se-entry"></div>
        </aside>
        <div class="se-main">
          <div class="se-tools">
            <button class="primary se-save" title="Compile, run and save it (Ctrl+S)">Save</button>
            <button class="se-test" title="Run onSpawn, then onInteract on your character (it has to be in the zone); what the hooks do really happens">Test</button>
            <button class="se-revert" title="Back to the saved script">Revert</button>
            <button class="se-clear danger" title="Take the script off it">Remove</button>
            <button class="se-to-library" title="Save this script to the library under a name, to use on other NPCs and props">Save to library…</button>
            <select class="se-insert" title="Insert a hook at the cursor"><option value="">Insert…</option>
              ${Object.keys(SNIPPETS).map((name) => `<option>${name}</option>`).join("")}</select>
            <span class="se-length"></span>
          </div>
          <textarea class="se-source" spellcheck="false" wrap="off" placeholder="-- Lua. Define hooks such as onInteract(player); Insert adds one."></textarea>
          <div class="se-message"></div>
        </div>
        <aside class="se-help" hidden><pre>${escape(API)}</pre></aside>
      </div>`;
    const $ = (selector) => this.root.querySelector(selector);
    this.title = $(".se-title");
    this.state = $(".se-state");
    this.source = $(".se-source");
    this.message = $(".se-message");
    this.length = $(".se-length");
    this.list = $(".se-list");
    this.entryPane = $(".se-entry");
    this.search = $(".se-search");

    $(".se-close").addEventListener("click", () => this.close());
    $(".se-help-toggle").addEventListener("click", (e) => { $(".se-help").hidden = !$(".se-help").hidden; e.currentTarget.classList.toggle("active", !$(".se-help").hidden); });
    $(".se-save").addEventListener("click", () => this.save());
    $(".se-test").addEventListener("click", () => this.test());
    $(".se-revert").addEventListener("click", () => { this.source.value = this.saved; this.changed(); this.say("Back to the saved script."); });
    $(".se-clear").addEventListener("click", () => this.clear());
    $(".se-to-library").addEventListener("click", () => this.saveToLibrary());
    $(".se-insert").addEventListener("change", (e) => {
      if (e.target.value) this.insert(SNIPPETS[e.target.value]);
      e.target.value = "";
    });
    this.search.addEventListener("input", () => this.renderList());
    this.source.addEventListener("input", () => this.changed());
    this.source.addEventListener("keydown", (e) => {
      if (e.key === "Tab" && !e.ctrlKey && !e.altKey) {
        e.preventDefault();
        this.insert("    ", false);
      }
    });
    // Its keys are its own: Ctrl+S saves the script, Esc closes; the editor behind doesn't see them.
    this.root.addEventListener("keydown", (e) => {
      if ((e.ctrlKey || e.metaKey) && e.code === "KeyS") {
        e.preventDefault();
        this.save();
      } else if (e.key === "Escape") {
        e.preventDefault();
        this.close();
      }
      if (e.code !== "F11") e.stopPropagation();
    });
  }

  async open(object) {
    if (this.isOpen && this.dirty && this.object !== object && !confirm("Drop the unsaved changes to this script?")) return;
    this.object = object;
    const data = object.userData.live;
    this.title.textContent = `Script · ${data.label}`;
    this.root.hidden = false;
    this.source.value = "";
    this.saved = "";
    this.changed();
    this.say("Loading…");
    try {
      const [script] = await Promise.all([this.live.getScript(object), this.loadLibrary()]);
      this.saved = script.source;
      this.source.value = script.source;
      this.setState(script.source, script.running);
      this.changed();
      this.say(script.source ? "" : "No script yet: write one, insert a hook, or use one from the library.");
      this.source.focus();
    } catch (error) {
      this.say(error.message, "error");
    }
  }

  close() {
    if (this.dirty && !confirm("Close without saving the script?")) return;
    this.root.hidden = true;
    this.object = null;
    this.hooks.closed?.();
  }

  say(text, kind = "") {
    this.message.textContent = text;
    this.message.className = `se-message ${kind}`;
  }

  setState(source, running) {
    this.state.textContent = !source ? "no script" : running ? "running" : "saved, not running";
    this.state.className = `se-state ${!source ? "" : running ? "ok" : "error"}`;
    this.state.title = source && !running ? "It didn't compile when the object spawned; save it again to see why." : "";
  }

  changed() {
    const length = this.source.value.length;
    this.length.textContent = `${length.toLocaleString()} / ${MAX_LENGTH.toLocaleString()}${this.dirty ? " · unsaved" : ""}`;
    this.length.classList.toggle("over", length > MAX_LENGTH);
  }

  insert(text, ownLine = true) {
    const { selectionStart: start, selectionEnd: end, value } = this.source;
    const before = value.slice(0, start);
    const block = ownLine ? `${before && !before.endsWith("\n") ? "\n\n" : ""}${text}\n` : text;
    this.source.setRangeText(block, start, end, "end");
    this.source.focus();
    this.changed();
  }

  // Each answer says whether it worked; a compile error leaves the old script running.
  async run(action, work) {
    if (!this.object) return null;
    this.say(`${action}…`);
    try {
      const result = await work();
      this.say(result.message, result.ok ? "ok" : "error");
      return result;
    } catch (error) {
      this.say(error.message, "error");
      return null;
    }
  }

  async save() {
    const source = this.source.value;
    if (!source.trim()) return this.clear();
    if (source.length > MAX_LENGTH) return this.say(`Scripts are at most ${MAX_LENGTH.toLocaleString()} characters.`, "error");
    const result = await this.run("Saving", () => this.live.saveScript(this.object, source));
    if (result?.running) {
      // The server fills a template's {{placeholders}}; what it saved is what runs.
      const script = await this.live.getScript(this.object).catch(() => null);
      this.saved = script?.source ?? source;
      if (script) this.source.value = script.source;
      this.setState(this.saved, script?.running ?? true);
      this.changed();
    }
  }

  test() {
    if (this.dirty) this.say("Testing the saved script; save first to test your changes.");
    return this.run("Testing", () => this.live.testScript(this.object));
  }

  async clear() {
    if (!confirm("Take the script off it?")) return;
    const result = await this.run("Removing", () => this.live.clearScript(this.object));
    if (result?.ok) {
      this.saved = "";
      this.source.value = "";
      this.setState("", false);
      this.changed();
    }
  }

  // --- the library ---------------------------------------------------------------------------------------------

  async loadLibrary() {
    this.entries = await this.live.scriptLibrary();
    this.renderList();
  }

  renderList() {
    const words = this.search.value.trim().toLowerCase().split(/\s+/).filter(Boolean);
    const shown = this.entries.filter((e) => words.every((w) => `${e.name} ${e.description}`.toLowerCase().includes(w)));
    const group = (title, items) => items.length
      ? `<h5>${title}</h5>${items.map((e) => `<div data-name="${escape(e.name)}" class="${this.entry?.name === e.name ? "active" : ""}" title="${escape(e.description)}">${escape(e.name)}</div>`).join("")}`
      : "";
    this.list.innerHTML = group("Templates", shown.filter((e) => e.isTemplate)) + group("Saved", shown.filter((e) => !e.isTemplate))
      || `<p class="hint">Nothing matches.</p>`;
    for (const row of this.list.querySelectorAll("[data-name]")) row.addEventListener("click", () => this.showEntry(row.dataset.name));
  }

  async showEntry(name) {
    try {
      this.entry = await this.live.scriptLibraryEntry(name);
    } catch (error) {
      return this.say(error.message, "error");
    }
    const e = this.entry;
    this.renderList();
    this.entryPane.innerHTML = `
      <h4>${escape(e.name)}${e.isTemplate ? ' <small>template</small>' : ""}</h4>
      <p class="hint">${escape(e.description) || "No description."}</p>
      ${e.params.map((p) => `<label>${escape(p.key)} <small>${escape(p.type)}</small>
        <input data-param="${escape(p.key)}" type="${NUMERIC.has(p.type) ? "number" : "text"}" ${p.type === "float" ? 'step="any"' : ""} value="${escape(p.default)}"></label>`).join("")}
      <div class="row">
        <button class="primary se-use" title="Put it on this object with these values, and run it">Use on it</button>
        <button class="se-open" title="Copy its source into the editor to change it; Save fills its {{placeholders}} with the defaults">Open</button>
        ${e.isTemplate ? "" : '<button class="se-delete danger" title="Delete it from the library">Delete</button>'}
      </div>`;
    this.entryPane.querySelector(".se-use").addEventListener("click", () => this.useEntry());
    this.entryPane.querySelector(".se-open").addEventListener("click", () => {
      if (this.dirty && !confirm("Replace the unsaved script in the editor?")) return;
      this.source.value = e.source;
      this.changed();
      this.say(`${e.name} is in the editor; Save puts it on the object.`);
    });
    this.entryPane.querySelector(".se-delete")?.addEventListener("click", () => this.deleteEntry());
  }

  async useEntry() {
    const values = {};
    for (const input of this.entryPane.querySelectorAll("[data-param]")) values[input.dataset.param] = input.value;
    if (this.dirty && !confirm("Using it replaces the unsaved script in the editor. Go ahead?")) return;
    const result = await this.run(`Putting ${this.entry.name} on it`, () => this.live.useScript(this.object, this.entry.name, values));
    if (result?.ok) {
      const script = await this.live.getScript(this.object);
      this.saved = script.source;
      this.source.value = script.source;
      this.setState(script.source, script.running);
      this.changed();
    }
  }

  async saveToLibrary() {
    const source = this.source.value;
    if (!source.trim()) return this.say("Write a script first.", "error");
    const name = prompt("Library name (letters, digits, - and _):", this.entry && !this.entry.isTemplate ? this.entry.name : "");
    if (name === null) return;
    if (!LIBRARY_NAME.test(name)) return this.say("Library names are 1-32 letters, digits, - and _.", "error");
    const description = prompt("What it does (optional):", this.entry?.name === name ? this.entry.description : "") ?? "";
    const result = await this.run(`Saving ${name} to the library`, () => this.live.saveScriptLibraryEntry(name, source, description || null));
    if (result?.ok) {
      await this.loadLibrary();
      await this.showEntry(name);
    }
  }

  async deleteEntry() {
    const name = this.entry.name;
    if (!confirm(`Delete ${name} from the library? NPCs and props using it keep their copy.`)) return;
    try {
      await this.live.deleteScriptLibraryEntry(name);
      this.entry = null;
      this.entryPane.innerHTML = "";
      await this.loadLibrary();
      this.say(`Deleted ${name}.`, "ok");
    } catch (error) {
      this.say(error.message, "error");
    }
  }
}
