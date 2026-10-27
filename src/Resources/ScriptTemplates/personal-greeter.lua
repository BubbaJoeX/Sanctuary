-- @name personal-greeter
-- @description Whispers a personal hello when you arrive and a goodbye when you leave, and remembers how often you visit
-- @param greeting string Welcome back to town

local visits = {}

function onProximity(player)
    local count = (visits[player.guid] or 0) + 1
    visits[player.guid] = count

    npc:face(player)
    npc:emote(3333) -- wave

    if player.isGm then
        npc:sayTo(player, "Oh! The boss is here! Everyone look busy!")
    elseif count == 1 then
        npc:sayTo(player, "Hello, " .. player.name .. "! First time here? Click me if you need anything.")
    else
        npc:sayTo(player, "{{greeting}}, " .. player.name .. "! That's visit number " .. tostring(count) .. ".")
    end
end

function onLeave(player)
    npc:sayTo(player, pick({ "Safe travels!", "See you soon!", "Don't be a stranger!" }))
end

function onInteract(player)
    npc:face(player)
    npc:sayTo(player, "You've stopped by " .. tostring(visits[player.guid] or 0) .. " times. I'm keeping count!")
end
