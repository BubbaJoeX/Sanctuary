-- @name toll-keeper
-- @description Takes a ticket item (or else coins) and sends the player to a world zone's start; handy for event portals
-- @param zone int 1036
-- @param ticket item 0
-- @param coins int 100
-- @param greeting string Want a ride? Bring a ticket or some coins.

function onInteract(player)
    npc:face(player)

    local paid

    if {{ticket}} > 0 and player:takeItem({{ticket}}, 1) then
        paid = "ticket"
    elseif {{coins}} > 0 and player:takeCoins({{coins}}) then
        paid = "coins"
    elseif {{ticket}} <= 0 and {{coins}} <= 0 then
        paid = "free"
    else
        npc:sayTo(player, "{{greeting}}")
        return
    end

    npc:say("Off you go, " .. player.name .. "!")

    if player:warp({{zone}}) then
        return
    end

    -- The zone isn't a world zone (or can't be reached): give the fare back.
    if paid == "ticket" then
        player:giveItem({{ticket}}, 1)
    elseif paid == "coins" then
        player:giveCoins({{coins}})
    end

    player:message("That ride is closed right now.")
end
