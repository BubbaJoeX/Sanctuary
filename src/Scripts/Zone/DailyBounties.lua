-- Three bounties a day for the world's activities. Each pays coins and stars once a day, and finishing all three
-- pays a bonus. Players hear the day's bounties when they first arrive each day, and their progress as they go.
-- Progress is kept until the server restarts.
local Bounties = {
    { key = "mobs", goal = 25, step = 5, text = "Defeat %d creatures" },
    { key = "minigames", goal = 3, step = 1, text = "Win %d minigames" },
    { key = "challenges", goal = 2, step = 1, text = "Win %d challenges" },
    { key = "quests", goal = 3, step = 1, text = "Complete %d quests" },
    { key = "crafts", goal = 2, step = 1, text = "Craft %d items" },
}

local PerDay = 3

-- Each day's bounties are every second one round the list from a start that moves on each day. Two and the five
-- bounties share no factor, so the three are always different.
local Stride = 2

local RewardCoins = 100
local RewardStars = 50
local AllDoneCoins = 150

local FireworksCompositeEffectId = 5349

local progress = {} -- by player guid: the day, counts by bounty key, and whether they've heard the list

local function today()
    local date = os.date("*t")

    return date.year * 366 + date.yday
end

local function bountiesOf(day)
    local list = {}

    for i = 0, PerDay - 1 do
        list[#list + 1] = Bounties[(day + i * Stride) % #Bounties + 1]
    end

    return list
end

local function progressOf(player)
    local day = today()
    local state = progress[player.guid]

    if not state or state.day ~= day then
        state = { day = day, counts = {}, told = false }
        progress[player.guid] = state
    end

    return state
end

local function goalText(bounty)
    return string.format(bounty.text, bounty.goal)
end

local function describe(state, bounty)
    local count = state.counts[bounty.key] or 0

    if count >= bounty.goal then
        return goalText(bounty) .. " (done)"
    end

    return string.format("%s (%d/%d)", goalText(bounty), count, bounty.goal)
end

local function tellBounties(player, state)
    local parts = {}

    for _, bounty in ipairs(bountiesOf(state.day)) do
        parts[#parts + 1] = describe(state, bounty)
    end

    player:message(string.format("Today's bounties: %s. Each pays %d coins and %d stars, and finishing all three pays %d more coins.",
        table.concat(parts, ", "), RewardCoins, RewardStars, AllDoneCoins))
    state.told = true
end

local function allDone(state)
    for _, bounty in ipairs(bountiesOf(state.day)) do
        if (state.counts[bounty.key] or 0) < bounty.goal then
            return false
        end
    end

    return true
end

local function record(player, key)
    local state = progressOf(player)
    local bounty = nil

    for _, candidate in ipairs(bountiesOf(state.day)) do
        if candidate.key == key then
            bounty = candidate
        end
    end

    local count = state.counts[key] or 0

    if not bounty or count >= bounty.goal then
        return
    end

    count = count + 1
    state.counts[key] = count

    if count < bounty.goal then
        if count % bounty.step == 0 then
            player:message(string.format("Bounty: %s, %d/%d.", goalText(bounty), count, bounty.goal))
        end

        return
    end

    player:giveCoins(RewardCoins)
    player:giveStars(RewardStars)
    player:playEffect(FireworksCompositeEffectId)
    player:message(string.format("Bounty done: %s! +%d coins and %d stars.", goalText(bounty), RewardCoins, RewardStars))

    if allDone(state) then
        player:giveCoins(AllDoneCoins)
        player:message(string.format("That's all of today's bounties! +%d bonus coins. There are new ones tomorrow.", AllDoneCoins))
    end
end

registerCallback("playerEnter", function(zone, player)
    local state = progressOf(player)

    if not state.told then
        tellBounties(player, state)
    end
end)

registerCallback("mobKilled", function(zone, npc, killer)
    if killer then
        record(killer, "mobs")
    end
end)

registerCallback("minigameEnd", function(zone, player, minigameId, won)
    if won then
        record(player, "minigames")
    end
end)

registerCallback("challengeEnd", function(zone, player, challengeId, won)
    if won then
        record(player, "challenges")
    end
end)

registerCallback("questComplete", function(zone, player, questId)
    record(player, "quests")
end)

registerCallback("itemCrafted", function(zone, player, recipeId, itemId)
    record(player, "crafts")
end)
