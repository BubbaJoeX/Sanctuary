-- @name bell-tower
-- @description Click a bell and every bell on the same channel nearby chimes in, one after another
-- @param channel string bells
-- @param reach float 60
-- @param rest int 10

local lastRang = -1000

local chimes = { "*DING*", "*DONG*", "*BONG*", "*ding-a-ling*" }

local function ring(by)
    local now = os.time()

    -- Resting stops two bells from ringing each other forever.
    if now - lastRang < {{rest}} then
        return
    end

    lastRang = now
    npc:playEffect(5354)
    npc:emote(3368)
    npc:say(pick(chimes))
    npc:signal("{{channel}}", by, {{reach}})
end

function onInteract(player)
    ring(player.name)
end

function onSignal(name, data, from)
    if name ~= "{{channel}}" then
        return
    end

    -- A little delay makes the chime ripple outwards.
    after(math.random(1, 3), function() ring(data) end)
end
