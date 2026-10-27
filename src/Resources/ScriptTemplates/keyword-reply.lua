-- @name keyword-reply
-- @description Answers when a player nearby says a word (the chat bot stays quiet for this NPC)
-- @param keyword string howdy
-- @param reply string Howdy, partner!
-- @param cooldown int 5

local lastSaid = -1000
local clock = 0

function onSecond()
    clock = clock + 1
end

function onHear(player, message)
    if not string.find(string.lower(message), string.lower("{{keyword}}"), 1, true) then
        return
    end

    if clock - lastSaid < {{cooldown}} then
        return
    end

    lastSaid = clock
    npc:face(player)
    npc:say("{{reply}}")
end
