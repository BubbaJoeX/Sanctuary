-- @name wishing-fountain
-- @description Toss a coin once a day: most wishes give a few coins back, a lucky few hit the jackpot
-- @param small int 25
-- @param jackpot int 1000
-- @param odds float 0.05

local wishedOn = {}

local wishes = {
    "You wish for endless cupcakes. The fountain burbles approvingly.",
    "You wish for a dragon. The fountain says maybe next year.",
    "You wish for world peace. The fountain is touched.",
    "You wish for more wishes. The fountain sees what you did there."
}

function onInteract(player)
    local today = os.date("%Y-%m-%d")

    if wishedOn[player.guid] == today then
        player:message("The fountain only grants one wish a day. Come back tomorrow!")
        return
    end

    wishedOn[player.guid] = today
    player:emote(3302)
    player:message(pick(wishes))

    if chance({{odds}}) then
        player:playEffect(15093) -- gold coins burst
        player:giveCoins({{jackpot}})
        npc.zone:broadcastNear(player.name .. " hit the JACKPOT at the wishing fountain!", npc.position.x, npc.position.z, 60)
    elseif {{small}} > 0 then
        player:playEffect(5353)
        player:giveCoins({{small}})
    end
end
