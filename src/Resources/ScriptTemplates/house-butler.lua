-- @name house-butler
-- @description In a house, greets its owner by name when they come home, and welcomes their guests
-- @param welcome string Make yourself at home!

local BowAnimationId = 3342
local WaveAnimationId = 3333

local greeted = {}

local function isOwner(player)
    return npc.zone.isHouse and player.guid == npc.zone.owner
end

function onProximity(player)
    if greeted[player.guid] then
        return
    end

    greeted[player.guid] = true
    npc:face(player)

    if isOwner(player) then
        npc:emote(BowAnimationId)
        npc:sayTo(player, "Welcome home, " .. player.name .. "!")
    else
        npc:emote(WaveAnimationId)
        npc:sayTo(player, "Welcome, " .. player.name .. "! {{welcome}}")
    end
end

function onLeave(player)
    greeted[player.guid] = nil
end

function onInteract(player)
    npc:face(player)

    if isOwner(player) then
        npc:sayTo(player, "Everything is just as you left it.")
    else
        npc:sayTo(player, "The owner isn't far, I'm sure. {{welcome}}")
    end
end
