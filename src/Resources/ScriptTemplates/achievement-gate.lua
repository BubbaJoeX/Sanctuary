-- @name achievement-gate
-- @description Lets players who've earned an achievement (an Achievements.json id) through to the far side of it when clicked
-- @param achievement int 52
-- @param distance float 4
-- @param refusal string Only those who've reached rank 20 as an Adventurer may pass.

-- The spot past the gate, straight on from where the player stands.
local function farSide(player)
    local from, gate = player.position, npc.position
    local dx, dz = gate.x - from.x, gate.z - from.z
    local length = math.sqrt(dx * dx + dz * dz)

    if length < 0.1 then
        dx, dz, length = 0, 1, 1
    end

    return gate.x + dx / length * {{distance}}, gate.y, gate.z + dz / length * {{distance}}
end

function onInteract(player)
    npc:face(player)

    if not player:hasAchievement({{achievement}}) then
        npc:sayTo(player, "{{refusal}}")
        return
    end

    npc:sayTo(player, "Welcome, " .. player.name .. ". Go on through.")
    player:teleport(farSide(player))
end
