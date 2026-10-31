# NPC and prop scripts

GMs attach Lua scripts to NPCs and props with `!npc script` / `!prop script`, or apply one of the
templates in this folder with `!npc script use <name> key=value ...`. Each script runs in its own
sandbox, and every call is stopped after 100 ms.

## Template header

```lua
-- @name my-template
-- @description One line shown in the library
-- @param cooldown int 30        -- int, float, item or string, then the default
local wait = {{cooldown}}        -- placeholders are filled in when the template is applied
```

## Templates for the game's systems

| Template | Parameters | |
|---|---|---|
| `battle-herald` | `battle` (a `Battles.json` id), `greeting` | Opens the battle's start panel when clicked, and asks the player's group along. |
| `challenge-giver` | `challenge` (a `Challenges.json` id), `prize`, `greeting` | Offers the challenge, laid out around itself, and gives the prize item for a win. |
| `recipe-tutor` | `recipe` (a `Recipes.json` id), `coins` | Teaches the recipe for a fee, and refunds it if the recipe doesn't exist. |
| `dance-teacher` | `dance` (one of `!emote`'s dances), `greeting` | Shows the move, and the player dances it until they walk away. |
| `achievement-gate` | `achievement` (an `Achievements.json` id), `distance`, `refusal` | Steps players who've earned the achievement through to its far side. |
| `house-butler` | `welcome` | Greets a house's owner by name and welcomes their guests. |
| `night-owl` | `dusk`, `dawn` | Wakes and sleeps by the game's clock, the same day and night the sky shows. |

## Hooks

| Hook | When |
|---|---|
| `onSpawn()` | The script is attached, or the NPC spawns. It also runs again on `!npc script test`, so don't start a second timer. |
| `onSecond()` | Every second. |
| `onInteract(player)` | A player clicks. |
| `onProximity(player)` | A player comes within 8 units. |
| `onLeave(player)` | That player goes back beyond 10 units. |
| `onArrive()` | A walk started with `moveTo` or `returnHome` has finished. |
| `onHear(player, message)` | A player says something nearby. |
| `onChoice(player, hook)` | A conversation choice with that hook is picked. |
| `onSignal(name, data, fromGuid)` | Another NPC's script called `npc:signal`. |
| `onHurt(player, amount)`, `onDeath(killer)`, `onRespawn()` | Mobs only: a hit it survived, the killing blow, and coming back. |
| `onChallengeEnd(player, challengeId, won)` | A challenge this NPC gave with `npc:offerChallenge` ended, unless it was cancelled. The `challenge-giver` template pays a prize for a win: `!npc script use challenge-giver challenge=25 prize=<itemId>`. |

## Globals

| Global | |
|---|---|
| `after(seconds, fn)` | Runs `fn` once, after the delay; returns a timer id. |
| `every(seconds, fn)` | Runs `fn` repeatedly; returns a timer id. A timer that errors is cancelled. |
| `cancel(id)` | Stops a timer. |
| `distance(a, b)` | Ground distance between players, NPCs or `{x, z}` tables. |
| `pick(list)`, `chance(p)`, `shuffle(list)` | Random helpers. |
| `countdown(seconds)` | Formats seconds as `m:ss`. |
| `os.time()`, `os.clock()`, `os.date(format)`, `os.difftime(a, b)` | The server clock. |

The standard `math`, `string` and `table` libraries are available, along with `pairs`, `ipairs`,
`pcall` and similar functions. `io`, `load`, `require` and the rest of `os` are not.

Timers count whole seconds, and a script can have up to 64 of them.

## `npc`

Fields: `guid`, `name`, `position`, `home` (where it was placed), `zone`, `isMoving`.

| Method | |
|---|---|
| `say(text)`, `sayLocalized(id)`, `sayTo(player, text)` | Speak to everyone nearby, or to one player only. |
| `emote(animation, loop)`, `playAnimation(id)`, `setBaseAnimation(id)` | |
| `moveTo(x, y, z, direct)`, `returnHome()`, `setSpeed(speed)`, `setHeading(radians)`, `face(player)` | |
| `getPlayers(radius)` | Nearby players. |
| `getPlayer(guid)` | One visible player, or nil. |
| `getNearbyPlayers(radius)`, `getPlayersOnTop(radius, minHeight, maxHeight)` | Return guids. |
| `addEffectTag(effect)` returns a tag; `removeEffectTag(tag)`; `playEffect(effect)` | |
| `setScale(0.1 to 10)`, `rename(name)`, `setCollidable(bool)` | |
| `startConversation(player, node)` | |
| `signal(name, data, radius)` | Runs `onSignal` on other scripted NPCs; a radius of 0 reaches the whole zone. Each NPC can send 10 signals a second. |
| `remove(effect)` | |
| `offerChallenge(player, id)` | Opens a challenge's start screen (a `Resources/Challenges.json` id), laid out around this NPC. |

## `player`

Fields: `guid`, `name`, `position`, `isGm`, `isMounted`.

| Method | |
|---|---|
| `message(text)` | A system line only this player sees. |
| `giveItem(id, 1 to 999)`, `giveCoins(1 to 100000)` | |
| `acceptQuest(id)`, `hasCompletedQuest(id)` | |
| `teleport(x, y, z, heading)` | Within the zone. |
| `dismount()`, `heal(amount)` | |
| `playEffect(effect)`, `emote(name or animation)`, `stopEmote()` | Seen by everyone nearby. Names are `!emote`'s; dances loop until the player walks away. |
| `transform(model, 1 to 3600 seconds)`, `clearTransform()` | |
| `resize(0.25 to 4, 1 to 3600 seconds)` | |
| `startMinigame(id)` | Opens a minigame's start screen (ids from `!minigame list`); false if the server doesn't run it. The `minigame-table` template turns any NPC or prop into a game table: `!prop script use minigame-table games=20` for chess, or `games=46,173,174` for a random recipe, with an optional `coins=`/`ticket=` fee and `cooldown=`. |

| `startBattle(id)`, `hasAchievement(id)`, `knowsRecipe(id)`, `learnRecipe(id)` | Also the fields `battleId`, `inBattle`, `inMinigame` and `isFishing`. |

`src/Scripts/README.md` lists everything else a player has, such as health, jobs, coins, groups, items and warps.

## `npc.zone`

Fields: `id`, `name`, `gameHour`, `battleId`, `isHouse`, `owner`.

| Method | |
|---|---|
| `broadcast(text)`, `broadcastNear(text, x, z, radius)` | |
| `getFlag(key)`, `setFlag(key, value)`, `addFlag(key, amount)` | Shared by every script in the zone (numbers, booleans, or strings up to 256 characters; at most 256 flags). They are not saved. |
| `spawnNpc(...)`, `spawnNpcWithGuid(...)`, `setClientAreaEnabled(id, bool)`, `isSeasonActive(name)` | |

Guids are always strings: a Lua number can't hold all 64 bits.
