-- @name hot-potato
-- @description Click to start; the potato jumps between nearby players, and whoever holds it when it blows becomes a chicken
-- @param fuse int 20
-- @param radius float 20
-- @param chicken int 30

local holder = nil
local fuseTimer = nil
local tickTimer = nil

local function stop()
    if fuseTimer then cancel(fuseTimer) end
    if tickTimer then cancel(tickTimer) end
    holder, fuseTimer, tickTimer = nil, nil, nil
end

local function giveTo(player)
    holder = player.guid
    player:playEffect(906)
    player:message("You've got the HOT POTATO! Click the potato vendor to toss it on!")
    npc:say(player.name .. " has the potato!")
end

local function passFrom(guid)
    local others = {}

    for _, player in ipairs(npc:getPlayers({{radius}})) do
        if player.guid ~= guid then
            table.insert(others, player)
        end
    end

    if #others == 0 then
        return false
    end

    giveTo(pick(others))
    return true
end

local function explode()
    local victim = holder and npc:getPlayer(holder)
    stop()

    if not victim then
        npc:say("The potato fizzled out. Nobody's around!")
        return
    end

    victim:playEffect(5354)
    victim:transform(212, {{chicken}}) -- chick
    npc:say("BOOM! " .. victim.name .. " is a chicken now! Bawk!")
end

function onInteract(player)
    if not holder then
        local players = npc:getPlayers({{radius}})

        if #players < 2 then
            player:message("Hot potato needs at least two players nearby.")
            return
        end

        npc:say("Hot potato! The fuse is lit!")
        giveTo(pick(players))
        fuseTimer = after({{fuse}}, explode)
        tickTimer = every(5, function() npc:say("*tick tick tick*") end)
        return
    end

    if player.guid ~= holder then
        player:message("You don't have the potato. Run!")
        return
    end

    if not passFrom(holder) then
        player:message("There's nobody to toss it to!")
    end
end
