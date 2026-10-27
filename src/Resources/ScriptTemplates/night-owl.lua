-- @name night-owl
-- @description Sleeps by day and wakes at night by the server clock, changing its name, size and mood
-- @param dusk int 19
-- @param dawn int 6

local awake = nil
local clock = nil

local function isNight()
    local hour = tonumber(os.date("%H"))
    return hour >= {{dusk}} or hour < {{dawn}}
end

local function update()
    local night = isNight()

    if night == awake then
        return
    end

    awake = night

    if night then
        npc:rename("Night Owl")
        npc:setScale(1.3)
        npc:emote(3302)
        npc:say("Hoo! The night is young!")
    else
        npc:rename("Sleepy Owl")
        npc:setScale(0.8)
        npc:emote(3334) -- yawn
        npc:say("*yawn* Too bright... goodnight...")
    end
end

function onSpawn()
    update()
    clock = clock or every(60, update)
end

function onInteract(player)
    if awake then
        npc:sayTo(player, "Hoo hoo! Did you know it's " .. os.date("%H:%M") .. "? Prime owl time.")
    else
        npc:sayTo(player, "Zzz... come back after " .. tostring({{dusk}}) .. ":00... zzz")
    end
end
