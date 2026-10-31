-- @name dance-teacher
-- @description Teaches a dance when clicked: it shows the move with a cheer, and the player dances it until they walk away
-- @param dance string griddy
-- @param greeting string Follow my lead! Left foot, right foot...

-- The dances' animations, so the teacher can show the move (the player's own is played by name).
local moves = {
    twerk = 3590, griddy = 3592, dab = 3593, floss = 3594, robot = 3595, runningman = 3596, moonwalk = 3597,
    disco = 3598, sprinkler = 3599, carlton = 3600, macarena = 3601, shuffle = 3602, headbang = 3603, dance = 3501,
}

local CheerAnimationId = 3306

local lessons = {}

function onInteract(player)
    npc:face(player)

    if not player:emote("{{dance}}") then
        npc:sayTo(player, "Hop down from your ride, and I'll show you how it's done!")
        return
    end

    npc:emote(moves["{{dance}}"] or CheerAnimationId)
    npc:sayTo(player, "{{greeting}}")

    lessons[player.guid] = (lessons[player.guid] or 0) + 1

    if lessons[player.guid] == 3 then
        npc:say(player.name .. " is a natural!")
    end
end

function onLeave(player)
    lessons[player.guid] = nil
end
