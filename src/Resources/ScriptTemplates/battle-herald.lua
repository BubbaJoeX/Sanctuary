-- @name battle-herald
-- @description Opens a battle's start panel (a Battles.json id) when clicked, and asks the player's group along
-- @param battle int 1
-- @param greeting string Brave enough to face what lies ahead? Bring your friends!

function onInteract(player)
    npc:face(player)

    if player.inBattle then
        npc:sayTo(player, "You're in the thick of a battle already!")
        return
    end

    if player:startBattle({{battle}}) then
        npc:sayTo(player, "{{greeting}}")
    else
        npc:sayTo(player, "That battle isn't open right now. Come back later!")
    end
end
