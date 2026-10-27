-- @name boss-mob
-- @description For a mob (!npc mob): taunts as it weakens, stomps everyone close while it fights, and pays the killer's group bonus stars
-- @param stompDamage int 40
-- @param stompEvery int 8
-- @param stompRadius float 6
-- @param bonusStars int 50

local taunts = {
    { 0.75, "Is that all you've got?" },
    { 0.5, "Now you've made me angry!" },
    { 0.25, "I won't go down that easily!" },
}

local taunted = {}
local fightSeconds = 0

function onHurt(player, amount)
    for _, taunt in ipairs(taunts) do
        if not taunted[taunt[1]] and npc.health <= npc.maxHealth * taunt[1] then
            taunted[taunt[1]] = true
            npc:say(taunt[2])
        end
    end
end

function onSecond()
    if not npc.isMob or npc.isDead or npc.target == nil then
        fightSeconds = 0
        return
    end

    fightSeconds = fightSeconds + 1

    if fightSeconds % {{stompEvery}} ~= 0 then
        return
    end

    npc:say("STOMP!")

    for _, player in ipairs(npc:getPlayers({{stompRadius}})) do
        npc:attack(player, {{stompDamage}})
    end
end

function onDeath(killer)
    npc:say(killer.name .. " has bested me...")

    for _, player in ipairs(npc:getPlayers(60)) do
        if player.guid == killer.guid or killer:isGroupedWith(player) then
            player:giveStars({{bonusStars}})
            player:message("You helped defeat " .. npc.name .. "!")
        end
    end
end

function onRespawn()
    taunted = {}
    fightSeconds = 0
    npc:say("I have returned!")
end
