-- @name teleport-pad
-- @description Stand on the pad for a short countdown and get beamed to the destination
-- @param x float 0
-- @param y float 0
-- @param z float 0
-- @param heading float 0
-- @param charge int 3
-- @param radius float 2
-- @param height float 3

-- How long each player has been standing on the pad.
local charging = {}

function onSpawn()
    npc:addEffectTag(5723) -- sparkle so people notice it
end

function onSecond()
    local onPad = {}

    for _, guid in ipairs(npc:getPlayersOnTop({{radius}}, -1, {{height}})) do
        onPad[guid] = true

        local player = npc:getPlayer(guid)

        if player then
            local seconds = (charging[guid] or 0) + 1
            charging[guid] = seconds

            if seconds >= {{charge}} then
                charging[guid] = nil
                player:playEffect(46) -- teleport flash
                player:teleport({{x}}, {{y}}, {{z}}, {{heading}})
            else
                player:message("Teleporting in " .. tostring({{charge}} - seconds) .. "...")
            end
        end
    end

    -- Stepping off cancels the countdown.
    for guid in pairs(charging) do
        if not onPad[guid] then
            charging[guid] = nil
        end
    end
end

function onInteract(player)
    player:message("Stand on the pad for " .. tostring({{charge}}) .. " seconds to teleport.")
end
