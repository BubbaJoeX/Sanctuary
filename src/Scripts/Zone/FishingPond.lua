-- Each fishing pond runs this. Every tenth catch of a trip pays a few bonus stars, and treasure chests are cheered
-- across the pond. While staff run the Fishing Derby (!event on fishingderby), every fish caught counts, and when
-- they end it (!event off fishingderby) the pond's top three anglers who are still there win coins.
local DerbyKey = "fishingderby"

local BonusEveryCatches = 10
local BonusStars = 10
local DerbyPrizeCoins = { 300, 200, 100 }

local FireworksCompositeEffectId = 5349

local catches = {} -- this trip's catches, by player guid
local derby = nil -- while the derby runs: scores by player guid ({ name, fish }) and the leader's guid

local function standings()
    local ranked = {}

    for guid, score in pairs(derby.scores) do
        ranked[#ranked + 1] = { guid = guid, name = score.name, fish = score.fish }
    end

    table.sort(ranked, function(a, b)
        if a.fish ~= b.fish then
            return a.fish > b.fish
        end

        return a.name < b.name
    end)

    return ranked
end

local function startDerby(zone)
    derby = { scores = {}, leader = nil }
    zone:broadcast("The Fishing Derby has begun! Every fish you catch here counts, and the pond's top three anglers win coins. Be here when it ends to collect.")
end

local function endDerby(zone)
    local ranked = standings()
    derby = nil

    if #ranked == 0 then
        zone:broadcast("The Fishing Derby is over. Nobody landed a fish here this time!")
        return
    end

    zone:broadcast("The Fishing Derby is over! The pond's top anglers:")

    for place = 1, math.min(#ranked, #DerbyPrizeCoins) do
        local entry = ranked[place]
        local coins = DerbyPrizeCoins[place]
        local angler = zone:getPlayer(entry.guid)

        if angler then
            angler:giveCoins(coins)
            zone:broadcast(string.format("%d. %s, %d fish: %d coins!", place, entry.name, entry.fish, coins))
        else
            zone:broadcast(string.format("%d. %s, %d fish (gone before the prizes)", place, entry.name, entry.fish))
        end
    end
end

local function countForDerby(zone, player)
    local score = derby.scores[player.guid] or { name = player.name, fish = 0 }
    score.fish = score.fish + 1
    derby.scores[player.guid] = score

    local leader = derby.leader and derby.scores[derby.leader]

    if derby.leader ~= player.guid and (not leader or score.fish > leader.fish) then
        derby.leader = player.guid
        zone:broadcast(string.format("%s takes the lead in the Fishing Derby with %d fish!", player.name, score.fish))
    else
        player:message(string.format("Fishing Derby: %d fish.", score.fish))
    end
end

registerCallback("start", function(zone)
    -- The pond started while the derby was already on.
    if zone:isEventActive(DerbyKey) then
        derby = { scores = {}, leader = nil }
    end
end)

registerCallback("eventStart", function(zone, key)
    if string.lower(key) == DerbyKey and not derby then
        startDerby(zone)
    end
end)

registerCallback("eventEnd", function(zone, key)
    if string.lower(key) == DerbyKey and derby then
        endDerby(zone)
    end
end)

registerCallback("playerEnter", function(zone, player)
    catches[player.guid] = 0

    if derby then
        local score = derby.scores[player.guid]

        player:message(score
            and string.format("The Fishing Derby is on, and you have %d fish. The top three when it ends win coins.", score.fish)
            or "The Fishing Derby is on! Every fish you catch here counts, and the top three when it ends win coins.")
    end
end)

registerCallback("playerLeave", function(zone, player)
    catches[player.guid] = nil
end)

registerCallback("fishCaught", function(zone, player, kind, itemId)
    local count = (catches[player.guid] or 0) + 1
    catches[player.guid] = count

    if count % BonusEveryCatches == 0 then
        player:giveStars(BonusStars)
        player:message(string.format("That's %d catches this trip! +%d bonus stars.", count, BonusStars))
    end

    if kind == "treasure" then
        player:playEffect(FireworksCompositeEffectId)
        zone:broadcast(player.name .. " fished up a treasure chest!")
    end

    if derby and kind == "fish" then
        countForDerby(zone, player)
    end
end)
