-- Lavender Coast (zone 1036). The warpstone on the dais at the plaza, where the main path and the Wildwood trail
-- meet: the stock Lavender Coast Warpstone NPC, which opens the atlas when clicked. Point of interest 200 is where
-- the atlas teleports players to it.
local function spawnStaticNpcs(zone)
    zone:spawnNpcWithGuid(3422, 103600003422, -3222.0, 16.336, 712.0, -1.5708)
end

registerCallback("start", function(zone)
    spawnStaticNpcs(zone)
end)
