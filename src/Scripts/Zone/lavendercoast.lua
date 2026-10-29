-- Lavender Coast, the south-west end of FabledRealms (one of its zone scripts; stitch.py writes this).
-- The warpstone on the dais in Lavender Landing's square: the stock Lavender Coast Warpstone NPC, which
-- opens the atlas when clicked, with a guid of the coast's own (FabledRealms.lua spawns the same NPC at
-- Lavender Coast Pass). Point of interest 200 is where the atlas teleports players to it.
local function spawnStaticNpcs(zone)
    zone:spawnNpcWithGuid(3422, 103600003422, -3045.0, 4.6, -180.0, -1.5708)
end

registerCallback("start", function(zone)
    spawnStaticNpcs(zone)
end)
