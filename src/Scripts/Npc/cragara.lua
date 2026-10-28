local WaveAnimationId = 3333
local GreetRadius = 10
local RegreetCooldownSeconds = 180

local Greetings = {
    "Psst... looking for something you won't find in any shop?",
    "Rare wares from realms unknown. Have a look, but don't dawdle.",
    "I never stay long. Buy now, or wonder forever.",
    "Fresh off the caravan, and gone again before you know it.",
}

local GreetOnApproach = require("GreetOnApproach")

local function greet(npc)
    npc:say(Greetings[math.random(#Greetings)])
    npc:playAnimation(WaveAnimationId)
end

registerCallback("second", GreetOnApproach.create(GreetRadius, RegreetCooldownSeconds, greet))
