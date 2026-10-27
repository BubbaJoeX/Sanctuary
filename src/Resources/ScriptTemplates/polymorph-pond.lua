-- @name polymorph-pond
-- @description Click to splash in the magic pond and turn into a random critter for a while
-- @param seconds int 60
-- @param cooldown int 10

-- Models from the illusion foods: chick, penguin, bat, owl, treeble, deer, spider, man-eating plant.
local critters = {
    { 212, "a chick" }, { 16, "a penguin" }, { 28, "a bat" }, { 129, "an owl" },
    { 376, "a treeble" }, { 3427, "a deer" }, { 1924, "a spider" }, { 88, "a man-eating plant" }
}

local lastSplash = {}

function onInteract(player)
    local now = os.time()

    if lastSplash[player.guid] and now - lastSplash[player.guid] < {{cooldown}} then
        player:message("The pond is still settling. Try again in a moment.")
        return
    end

    lastSplash[player.guid] = now

    local critter = pick(critters)

    player:transform(critter[1], {{seconds}})
    player:playEffect(21) -- poof
    npc:say("*SPLASH* " .. player.name .. " is now " .. critter[2] .. "!")
end

function onChoice(player, hook)
    -- Hook "undo" on a conversation choice turns the player back.
    if hook == "undo" then
        player:clearTransform()
        player:playEffect(21)
    end
end
