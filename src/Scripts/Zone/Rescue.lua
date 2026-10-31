-- When a mob knocks a player out, the people who could help hear about it: their group in the world, and everyone
-- in the battle with them in a battle's zone. Anyone close enough with a Resurrection Scroll is told to use it, and
-- the player hears who's coming. The world and every battle's zone run this.
local Places = require("Places")

local ResurrectionScrollItemId = 11018
local NearRadius = 60

local function helpersOf(zone, player)
    local helpers = {}

    if zone.battleId ~= 0 then
        for _, other in ipairs(zone:getPlayers()) do
            if other.guid ~= player.guid then
                helpers[#helpers + 1] = other
            end
        end

        return helpers
    end

    for _, guid in ipairs(player.groupMembers) do
        local member = guid ~= player.guid and zone:getPlayer(guid)

        if member then
            helpers[#helpers + 1] = member
        end
    end

    return helpers
end

registerCallback("playerKnockedOut", function(zone, player)
    local position = player.position
    local scrollHolder = nil

    for _, helper in ipairs(helpersOf(zone, player)) do
        local distance = Places.groundDistance(helper.position, position)

        if distance > NearRadius then
            helper:message(string.format("%s was knocked out, %d m away.", player.name, math.floor(distance)))
        elseif not helper.isKnockedOut and helper:hasItem(ResurrectionScrollItemId) then
            helper:message(string.format("%s was knocked out nearby! Use a Resurrection Scroll close to them to get them back up.", player.name))
            scrollHolder = scrollHolder or helper
        else
            helper:message(string.format("%s was knocked out nearby!", player.name))
        end
    end

    if scrollHolder then
        player:message(string.format("Hang on: %s has a Resurrection Scroll.", scrollHolder.name))
    end
end)
