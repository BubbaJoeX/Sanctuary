-- @name follower
-- @description Click to adopt it: it trots after you for a while, then wanders back home
-- @param minutes int 3
-- @param leash float 40
-- @param speed float 7

local owner = nil
local leftAt = 0

local function goHome(line)
    owner = nil
    npc:say(line)
    npc:returnHome()
end

function onSpawn()
    npc:setSpeed({{speed}})
end

function onInteract(player)
    npc:face(player)

    if owner == player.guid then
        goHome("Okay, okay, I'll go home. Bye " .. player.name .. "!")
        return
    end

    if owner then
        npc:sayTo(player, "Sorry, I'm already following someone!")
        return
    end

    owner = player.guid
    leftAt = os.time() + {{minutes}} * 60
    npc:emote(3302)
    npc:say("Yay! I'm coming with you, " .. player.name .. "!")
end

function onSecond()
    if not owner then
        return
    end

    local player = npc:getPlayer(owner)

    if not player or distance(npc, player) > {{leash}} then
        goHome("Where did you go? I'll wait at home...")
        return
    end

    if os.time() >= leftAt then
        goHome("I'm sleepy. Time to go home!")
        return
    end

    -- Keep a polite distance instead of standing on the player's toes.
    if distance(npc, player) > 4 then
        local target = player.position
        npc:moveTo(target.x + 2, target.y, target.z + 2)
    end
end
