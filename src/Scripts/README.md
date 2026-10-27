# Server scripts

Lua that the server runs, in two kinds:

- **Zone and NPC scripts** (this folder). A zone loads the scripts listed under `Scripts` in its `Resources/Zones/*.json` file, and an NPC loads the ones listed under `Scripts` in `Npcs.json`. `Shared/` holds modules that the other scripts `require`. Each script registers callbacks with `registerCallback(event, fn)`.
- **GM scripts** are attached in game to one NPC or prop from the Referee Tools Scripts tab (`!script`). They run sandboxed (16 KB, 100 ms per hook) and define global hooks. Templates live in `src/Resources/ScriptTemplates`. The panel's **Show API** list is the reference for these.

Both kinds see the same `zone`, `npc` and `player` objects. Guids are always text, because a Lua number can't hold all 64 bits.

## Zone events

| Event | Callback |
|---|---|
| `start` | `fn(zone)`. Runs once when the zone starts. |
| `second` | `fn(zone)`. Runs every second. |
| `playerEnter` | `fn(zone, player)`. Runs when a player has finished loading into the zone. |
| `playerLeave` | `fn(zone, player)`. Runs when a player leaves the zone (zoning out or logging out). |
| `mobKilled` | `fn(zone, npc, killer)`. Runs when a player lands a mob's killing blow. |
| `eventStart`, `eventEnd` | `fn(zone, key)`. Runs in every world zone when staff turn an event on or off (`!event on/off <key>`). |
| `eventJoin` | `fn(zone, player, key)`. Runs in the event's zone the first time a player joins it with `!join`. |

NPC scripts in `Npc/` get `tick` and `second`, each with the NPC.

## GM script hooks

`onSpawn()`, `onSecond()`, `onInteract(player)`, `onProximity(player)`, `onLeave(player)`, `onHear(player, message)`, `onChoice(player, hook)`, `onArrive()`, `onSignal(name, data, fromGuid)`.

Mobs (hostile NPCs made with `!npc mob`) also get these:

| Hook | When |
|---|---|
| `onHurt(player, amount)` | A player hit the mob and it survived. |
| `onDeath(killer)` | A player landed the killing blow. Kill rewards have already been paid. |
| `onRespawn()` | The mob came back after its respawn time. |

## Objects

### zone

| Member | |
|---|---|
| `id`, `name` | |
| `playerCount`, `getPlayers()`, `getPlayer(guid or name)` | Players in this zone. |
| `broadcast(text)`, `broadcastNear(text, x, z, radius)` | |
| `getFlag(name)`, `setFlag(name, value)`, `addFlag(name [, amount])` | Shared state for the zone's scripts. Lost on restart. |
| `spawnNpc(npcId, x, y, z, heading [, pitch, roll])`, `spawnNpcWithGuid(npcId, guid, ...)` | Returns the npc, or nil. |
| `setClientAreaEnabled(areaId, on)`, `isSeasonActive(name)` | |
| `isEventActive(key)` | Whether staff have turned the event on with `!event on <key>`. |

### npc

| Member | |
|---|---|
| `guid`, `name`, `zone`, `position`, `home`, `isMoving` | |
| `say`, `sayTo`, `sayLocalized`, `face`, `setHeading`, `moveTo`, `returnHome`, `setSpeed`, `setScale`, `rename` | |
| `emote`, `playAnimation`, `setBaseAnimation`, `playEffect`, `addEffectTag`, `removeEffectTag`, `setCollidable` | |
| `getPlayers(radius)`, `getPlayer(guid)`, `getNearbyPlayers(radius)`, `getPlayersOnTop(radius, minHeight, maxHeight)` | |
| `startConversation(player [, nodeId])`, `signal(name [, data [, radius]])` | |
| `isMob`, `health`, `maxHealth`, `isDead`, `target` | Mob state. For other NPCs these are false or 0, and `target` is nil. |
| `hurt(player, amount)` | The mob takes a hit as if from that player, with threat, a kill and rewards. Returns the damage it took. |
| `attack(player, amount)` | The NPC hits a player in its zone. The hit is mitigated like a mob's, and god mode ignores it. Returns the damage the player took. |

### player

| Member | |
|---|---|
| `guid`, `name`, `position`, `isGm`, `isMounted` | |
| `zoneId`, `zoneName` | The zone's definition id is what `warp` takes. |
| `health`, `maxHealth`, `inCombat`, `isKnockedOut` | |
| `job`, `jobRank` | The active job's profile id and its rank. |
| `coins` | |
| `groupId`, `groupMembers`, `isGroupLeader`, `isGroupedWith(other)` | `groupId` is 0 without a group. `groupMembers` lists guids, leader first. |
| `message(text)` | |
| `giveItem(itemId [, qty])`, `itemCount(itemId)`, `hasItem(itemId [, qty])`, `takeItem(itemId [, qty])` | `takeItem` takes nothing and returns false if the player has too few. It never takes equipped items. |
| `giveCoins(amount)`, `takeCoins(amount)` | |
| `giveStars(amount)` | Adds stars to the active job, with star scrolls applied. |
| `heal(amount)`, `dismount()`, `emote(animId)`, `playEffect(effectId)` | |
| `transform(modelId, seconds)`, `clearTransform()`, `resize(scale, seconds)` | |
| `teleport(x, y, z [, heading])` | Moves the player within their zone. |
| `warp(zoneId [, x, y, z [, heading]])` | Sends the player to a world zone, either at that spot or at its start. Returns false for houses and other zones that need an owner. |
| `acceptQuest(questId)`, `hasCompletedQuest(questId)` | |

## Example

```lua
-- Zone/lavendercoast.lua
registerCallback("playerEnter", function(zone, player)
    player:message("Welcome to " .. zone.name .. "! " .. zone.playerCount .. " players are here.")
end)

registerCallback("mobKilled", function(zone, npc, killer)
    if zone:addFlag("kills") % 100 == 0 then
        zone:broadcast(killer.name .. " defeated the coast's 100th mob!")
    end
end)
```
