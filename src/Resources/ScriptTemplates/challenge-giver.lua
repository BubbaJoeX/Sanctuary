-- @name challenge-giver
-- @description Offers a challenge (a Challenges.json id) when clicked, laid out around itself, and gives a prize to whoever wins it
-- @param challenge int 25
-- @param prize item 0
-- @param greeting string Think you can beat my challenge?

function onInteract(player)
    npc:face(player)

    if npc:offerChallenge(player, {{challenge}}) then
        npc:sayTo(player, "{{greeting}}")
    else
        npc:sayTo(player, "Come back when you're free.")
    end
end

function onChallengeEnd(player, challengeId, won)
    if not won then
        npc:sayTo(player, "So close! Try again any time.")
        return
    end

    npc:say(player.name .. " beat my challenge!")

    if {{prize}} > 0 then
        player:giveItem({{prize}}, 1)
    end
end
