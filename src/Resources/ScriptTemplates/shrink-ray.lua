-- @name shrink-ray
-- @description A mad inventor zaps whoever clicks: tiny, giant, or a backfire on the inventor
-- @param seconds int 45
-- @param cooldown int 15

local lastZap = {}
local backfired = false

local function zap(player, scale, line)
    player:resize(scale, {{seconds}})
    player:playEffect(906) -- flash
    npc:emote(3302) -- applaud
    npc:say(line)
end

function onInteract(player)
    local now = os.time()

    if lastZap[player.guid] and now - lastZap[player.guid] < {{cooldown}} then
        npc:sayTo(player, "The ray is recharging! Patience, test subject.")
        return
    end

    lastZap[player.guid] = now
    npc:face(player)

    local roll = math.random(10)

    if roll <= 4 then
        zap(player, 0.5, "Shrinkified! " .. player.name .. ", mind the ants.")
    elseif roll <= 8 then
        zap(player, 2, "Embiggened! " .. player.name .. " towers over us all!")
    elseif not backfired then
        backfired = true
        npc:setScale(0.4)
        npc:emote(3368) -- startled
        npc:say("Wait, it's pointing the wrong way-- oh no.")
        after({{seconds}}, function()
            backfired = false
            npc:setScale(1)
            npc:say("Ahem. Back to full size. That never happened.")
        end)
    else
        npc:say("Out of batteries. Come back later!")
    end
end
