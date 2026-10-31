-- Every battle's zone runs this (the battles add it to each zone they make). Winning with friends from your group
-- pays a teamwork bonus, and a player who was knocked out in a battle that was lost hears how friends can help.
local TeamworkStarsPerFriend = 5

local FireworksCompositeEffectId = 5349

local knockOuts = {}

local function friendsHere(zone, player)
    local count = 0

    for _, guid in ipairs(player.groupMembers) do
        if guid ~= player.guid and zone:getPlayer(guid) then
            count = count + 1
        end
    end

    return count
end

registerCallback("playerKnockedOut", function(zone, player)
    knockOuts[player.guid] = (knockOuts[player.guid] or 0) + 1
end)

registerCallback("battleEnd", function(zone, player, battleId, won)
    local knockedOut = knockOuts[player.guid] or 0
    knockOuts[player.guid] = nil

    if not won then
        if knockedOut > 0 then
            player:message("Tip: a friend with a Resurrection Scroll can get you back up where you fall, so you don't have to start over.")
        end

        return
    end

    player:playEffect(FireworksCompositeEffectId)

    local friends = friendsHere(zone, player)

    if friends > 0 then
        local stars = friends * TeamworkStarsPerFriend

        player:giveStars(stars)
        player:message(string.format("Teamwork bonus: +%d stars for winning with %d friend%s.", stars, friends, friends == 1 and "" or "s"))
    end
end)
