-- @name patrol-guard
-- @description Marches a square around its post, pausing at each corner, and salutes anyone who clicks
-- @param size float 10
-- @param pause int 3
-- @param speed float 4

local corners = {}
local nextCorner = 1
local paused = false

local function march()
    local corner = corners[nextCorner]
    npc:moveTo(corner.x, corner.y, corner.z)
end

function onSpawn()
    local home = npc.home
    local half = {{size}} / 2

    corners = {
        { x = home.x + half, y = home.y, z = home.z + half },
        { x = home.x + half, y = home.y, z = home.z - half },
        { x = home.x - half, y = home.y, z = home.z - half },
        { x = home.x - half, y = home.y, z = home.z + half }
    }

    npc:setSpeed({{speed}})
    march()
end

function onArrive()
    if paused then
        return
    end

    npc:emote(3344) -- salute at each corner
    nextCorner = nextCorner % #corners + 1

    after({{pause}}, march)
end

function onInteract(player)
    paused = true
    npc:face(player)
    npc:emote(3344)
    npc:say("Halt! Oh, it's you, " .. player.name .. ". Carry on, citizen.")

    after(3, function()
        paused = false
        march()
    end)
end
