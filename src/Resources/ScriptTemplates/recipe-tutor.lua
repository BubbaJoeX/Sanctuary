-- @name recipe-tutor
-- @description Teaches a recipe (a Recipes.json id) for a fee in coins, to anyone who doesn't know it yet
-- @param recipe int 4
-- @param coins int 100

function onInteract(player)
    npc:face(player)

    if player:knowsRecipe({{recipe}}) then
        npc:sayTo(player, "You already know that one. Off to the workbench with you!")
        return
    end

    if {{coins}} > 0 and not player:takeCoins({{coins}}) then
        npc:sayTo(player, "Lessons are " .. {{coins}} .. " coins. Come back when you have them.")
        return
    end

    if player:learnRecipe({{recipe}}) then
        npc:say("There you go, " .. player.name .. ". Now you know how it's done!")
        return
    end

    -- There's no such recipe: give the fee back.
    if {{coins}} > 0 then
        player:giveCoins({{coins}})
    end

    npc:sayTo(player, "Hmm, I seem to have forgotten that one myself.")
end
