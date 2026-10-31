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
| `battleEnd` | `fn(zone, player, battleId, won)`. Runs for each player who saw a battle (a `Resources/Battles.json` id) through to its end, in the battle's zone. |
| `minigameEnd` | `fn(zone, player, minigameId, won)`. Runs when a player finishes a minigame or a 3D game (kart race, derby, soccer), won or not. |
| `challengeEnd` | `fn(zone, player, challengeId, won)`. Runs when an in-world challenge (`Resources/Challenges.json`) is won, finished too slowly or runs out of time. Cancelled challenges don't count. |
| `questComplete` | `fn(zone, player, questId)`. |
| `itemCrafted` | `fn(zone, player, recipeId, itemId)`. Runs when a recipe makes its item. |
| `recipeLearned` | `fn(zone, player, recipeId)`. Runs when a player learns a recipe from a card or `learnRecipe`, but not at login. |
| `fishCaught` | `fn(zone, player, kind, itemId)`. `kind` is `fish`, `junk` or `treasure`, and `itemId` is 0 for treasure. |
| `jobRankUp` | `fn(zone, player, jobId, rank)`. |
| `achievementEarned` | `fn(zone, player, achievementId)`. Runs when an achievement is finished, but not for the ones credited at login. |
| `playerKnockedOut` | `fn(zone, player)`. Runs when a mob knocks a player out. |

Player events run in the zone the player is in when it happens.

NPC scripts in `Npc/` get `tick` and `second`, each with the NPC.

## GM script hooks

`onSpawn()`, `onSecond()`, `onInteract(player)`, `onProximity(player)`, `onLeave(player)`, `onHear(player, message)`, `onChoice(player, hook)`, `onArrive()`, `onSignal(name, data, fromGuid)`.

Mobs (hostile NPCs made with `!npc mob`) also get these:

| Hook | When |
|---|---|
| `onHurt(player, amount)` | A player hit the mob and it survived. |
| `onDeath(killer)` | A player landed the killing blow. Kill rewards have already been paid. |
| `onRespawn()` | The mob came back after its respawn time. |

An NPC that gives a challenge with `npc:offerChallenge` also gets `onChallengeEnd(player, challengeId, won)` when that challenge ends, unless it's cancelled.

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
| `isEventActive(key)` | Whether staff have turned the event on with `!event on <key>`, or (a season in `Resources/Seasons.json`, such as `halloween`) it's between its days of the year. |
| `gameHour` | The hour of the game day, from 0 to just under 24, matching the clients' sky. A game day lasts three real hours. |
| `battleId` | The battle this zone was made for, or 0. |
| `isHouse`, `owner` | Whether the zone is a player's house, and the owner's guid (nil for other zones). |

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
| `offerChallenge(player, challengeId)` | Opens a challenge's start screen for a player in the NPC's zone, with the NPC as the giver its course is laid out around. Returns false if there's no such challenge, it isn't a world zone, or the player is busy with another challenge or game. |

### player

| Member | |
|---|---|
| `guid`, `name`, `position`, `isGm`, `isMounted` | |
| `zoneId`, `zoneName` | The zone's definition id is what `warp` takes. |
| `health`, `maxHealth`, `inCombat`, `isKnockedOut` | `inCombat` is true while a mob has aggro on the player, and for 6 seconds after an `attack` from an NPC that doesn't. |
| `job`, `jobRank` | The active job's profile id and its rank. |
| `coins` | |
| `groupId`, `groupMembers`, `isGroupLeader`, `isGroupedWith(other)` | `groupId` is 0 without a group. `groupMembers` lists guids, leader first. |
| `message(text)` | |
| `giveItem(itemId [, qty])`, `itemCount(itemId)`, `hasItem(itemId [, qty])`, `takeItem(itemId [, qty])` | `takeItem` takes nothing and returns false if the player has too few. It never takes equipped items. |
| `giveCoins(amount)`, `takeCoins(amount)` | |
| `giveStars(amount)` | Adds stars to the active job, with star scrolls applied. |
| `revive([healthPercent])` | Stands a knocked-out player up where they lie, with 50% health unless another share is given, as a Resurrection Scroll does. Returns false if they aren't knocked out. |
| `heal(amount)`, `dismount()`, `playEffect(effectId)` | |
| `emote(name or animId)`, `stopEmote()` | A name is one of `!emote`'s, such as `wave`, `griddy` or `floss`; dances loop until the player walks away or `stopEmote` runs. Returns false for an unknown name, or while the player rides. |
| `transform(modelId, seconds)`, `clearTransform()`, `resize(scale, seconds)` | |
| `teleport(x, y, z [, heading])` | Moves the player within their zone. |
| `warp(zoneId [, x, y, z [, heading]])` | Sends the player to a world zone, either at that spot or at its start. Returns false for houses and other zones that need an owner. |
| `acceptQuest(questId)`, `hasCompletedQuest(questId)` | |
| `startMinigame(minigameId)` | Opens a minigame's start screen, as picking it in the Games window does. The ids are MiniGameData.txt's, listed with `!minigame list`, and include the kart race, derby and soccer stages. Returns false for a game the server doesn't run. |
| `inMinigame` | Playing a minigame or 3D game, or on its start screen. |
| `battleId`, `inBattle`, `startBattle(battleId)` | The battle the player is in (a `Resources/Battles.json` id, or 0). `startBattle` opens a battle's start panel and asks the player's group along, and returns false if there's no such battle. |
| `isFishing` | |
| `hasAchievement(achievementId)` | Whether the player has finished it. |
| `knowsRecipe(recipeId)`, `learnRecipe(recipeId)` | `learnRecipe` teaches a `Resources/Recipes.json` recipe as its card does. It returns false if the recipe doesn't exist or is already known. |

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

registerCallback("fishCaught", function(zone, player, kind, itemId)
    if kind == "treasure" then
        zone:broadcast(player.name .. " fished up a treasure chest!")
    end
end)
```
