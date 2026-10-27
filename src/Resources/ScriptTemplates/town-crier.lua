-- @name town-crier
-- @description Rings a bell and calls out the time and the news every few minutes; click to hear the latest
-- @param interval int 300
-- @param radius float 80
-- @param news1 string Hear ye! The pinata in the square is full of candy!
-- @param news2 string Hear ye! Mind the chickens, they have escaped again!
-- @param news3 string Hear ye! Be kind to your fellow adventurers!

local news = { "{{news1}}", "{{news2}}", "{{news3}}" }
local nextNews = 1

local function timeOfDay()
    local hour = tonumber(os.date("%H"))

    if hour < 6 then return "deep in the night" end
    if hour < 12 then return "a fine morning" end
    if hour < 18 then return "a lovely afternoon" end
    return "a quiet evening"
end

local function cry()
    local position = npc.position

    npc:emote(3344) -- salute
    npc.zone:broadcastNear("Town Crier: It is " .. os.date("%H:%M") .. ", " .. timeOfDay() .. "! " .. news[nextNews],
        position.x, position.z, {{radius}})

    nextNews = nextNews % #news + 1
end

local crier = nil

function onSpawn()
    -- onSpawn runs again on "!npc script test", so don't start a second crier.
    crier = crier or every({{interval}}, cry)
end

function onInteract(player)
    npc:face(player)
    npc:sayTo(player, "Next call in about " .. countdown({{interval}}) .. ". The latest: " .. news[nextNews])
end
