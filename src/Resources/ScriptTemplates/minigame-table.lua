-- @name minigame-table
-- @description Click to play a minigame (ids from !minigame list): a game table, crafting station or arcade cabinet. List several ids (46,173,174) to pick one at random; a ticket or coin fee is refunded if the game can't start
-- @param games string 46
-- @param ticket item 0
-- @param coins int 0
-- @param cooldown int 0
-- @param effect int 0
-- @param busy string Finish what you're doing first!

local games = {}

for id in string.gmatch("{{games}}", "%d+") do
    games[#games + 1] = tonumber(id)
end

-- When each player last started a game here, for the cooldown.
local lastPlayed = {}

local tagged = false

function onSpawn()
    -- onSpawn runs again on !npc script test; one tag is enough.
    if {{effect}} > 0 and not tagged then
        npc:addEffectTag({{effect}})
        tagged = true
    end
end

local function refund(player, paid)
    if paid == "ticket" then
        player:giveItem({{ticket}}, 1)
    elseif paid == "coins" then
        player:giveCoins({{coins}})
    end
end

function onInteract(player)
    if #games == 0 then
        player:message("There's no game set up here yet.")
        return
    end

    if player.inCombat or player.isKnockedOut then
        npc:sayTo(player, "{{busy}}")
        return
    end

    local now = os.time()
    local last = lastPlayed[player.guid]

    if {{cooldown}} > 0 and last ~= nil and now - last < {{cooldown}} then
        player:message("You can play again in " .. countdown({{cooldown}} - (now - last)) .. ".")
        return
    end

    -- A ticket if they have one, else coins, like the toll keeper.
    local paid

    if {{ticket}} > 0 and player:takeItem({{ticket}}, 1) then
        paid = "ticket"
    elseif {{coins}} > 0 and player:takeCoins({{coins}}) then
        paid = "coins"
    elseif {{ticket}} > 0 or {{coins}} > 0 then
        if {{coins}} > 0 then
            player:message("It costs " .. tostring({{coins}}) .. " coins to play.")
        else
            player:message("You need a ticket to play.")
        end

        return
    end

    if player:startMinigame(pick(games)) then
        lastPlayed[player.guid] = now
        return
    end

    -- An id the server doesn't run (or no longer runs): don't keep the fee.
    refund(player, paid)
    player:message("That game isn't available right now.")
end
