-- @name treasure-clue
-- @description One clue of a zone-wide treasure hunt: put it on several props with clue 1, 2, 3...; finding them all pays out
-- @param clue int 1
-- @param total int 3
-- @param hunt string hunt
-- @param coins int 500
-- @param hint string Look where the water sings.

-- Each player's progress is a zone flag shared by every clue of the same hunt, like ",1,3,".
local function progressKey(player)
    return "{{hunt}}:" .. player.guid
end

local function countFound(found)
    local count = 0
    for _ in string.gmatch(found, "%d+") do
        count = count + 1
    end
    return count
end

function onSpawn()
    npc:addEffectTag(5723) -- sparkle
end

function onInteract(player)
    local zone = npc.zone
    local key = progressKey(player)
    local found = zone:getFlag(key) or ","

    if string.find(found, "," .. tostring({{clue}}) .. ",", 1, true) then
        player:message("You already found this clue. Hint: {{hint}}")
        return
    end

    if found == "done" then
        player:message("You've already finished this hunt. Well done!")
        return
    end

    found = found .. tostring({{clue}}) .. ","
    local count = countFound(found)

    player:playEffect(5353)

    if count < {{total}} then
        zone:setFlag(key, found)
        player:message("Clue " .. tostring(count) .. " of " .. tostring({{total}}) .. " found! Next hint: {{hint}}")
        return
    end

    zone:setFlag(key, "done")
    player:playEffect(15093) -- gold coins burst

    if {{coins}} > 0 then
        player:giveCoins({{coins}})
    end

    npc:say(player.name .. " found every clue of the hunt!")
    npc:signal("{{hunt}}-complete", player.name)
end
