-- Distances along the ground (x and z), as zone:broadcastNear measures them.
local Places = {}

-- Between two spots: anything with x and z, such as player.position or a table of a stage's coordinates.
function Places.groundDistance(a, b)
    local dx, dz = a.x - b.x, a.z - b.z

    return math.sqrt(dx * dx + dz * dz)
end

-- The players in the zone within radius of a spot.
function Places.playersNear(zone, spot, radius)
    local near = {}

    for _, player in ipairs(zone:getPlayers()) do
        if Places.groundDistance(player.position, spot) <= radius then
            near[#near + 1] = player
        end
    end

    return near
end

return Places
