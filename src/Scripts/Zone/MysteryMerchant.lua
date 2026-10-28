local EventSchedule = require("EventSchedule")

local CragaraNpcId = 33624

local ArriveCompositeEffectId = 21

-- Each hour she opens shop at the next stop, packs up near the end and is gone for the rest of it.
local OpenSeconds = 45 * EventSchedule.SecondsPerMinute
local PackingSeconds = 5 * EventSchedule.SecondsPerMinute

-- Ten stops over a 24-hour day, so she turns up somewhere different at each hour from one day to the next.
local Stops = {
    { name = "Snowhill", x = 130.87897, y = 28.729305, z = 290.85706, heading = -0.0971219953 },
    { name = "Blackspore", x = -2138.47, y = -33.02, z = 1035.99, heading = -0.393 },
    { name = "Lakeshore", x = -729.71, y = 9.07, z = 950.22, heading = -2.357 },
    { name = "Wugachug", x = -70.42, y = 37.62, z = -439.17, heading = 3.142 },
    { name = "Sanctuary", x = -1403.55, y = -27.62, z = 356.16, heading = -1.964 },
    { name = "Shrouded Glade", x = 6.71, y = 40.92, z = 1112.66, heading = -1.178 },
    { name = "Seaside", x = -630.96, y = 9.38, z = -1041.61, heading = -1.572 },
    { name = "Briarwood", x = -812.63, y = -21.05, z = 1627.55, heading = 0.786 },
    { name = "Merry Vale", x = -1187.45, y = -34.77, z = -437.87, heading = 1.178 },
    { name = "Cobblestone Village", x = -1908.70, y = -39.80, z = 417.56, heading = 2.357 },
}

local ArriveMessage = "Cragara the Mysterious Merchant has set up shop in %s! Her wares are rare, and she never stays long."
local PackingMessage = "Cragara is packing up her wares in %s. Five minutes left to browse!"
local GoneMessage = "Cragara has vanished from %s. Who knows where she'll turn up next..."
local HereMessage = "Cragara the Mysterious Merchant is selling rare wares in %s for another %d minutes."

local Phase = {
    Open = "Open",
    Packing = "Packing",
    Gone = "Gone",
}

local currentSlot = nil
local currentPhase = Phase.Gone
local cragara = nil

local function getSlot(time)
    return math.floor(time / EventSchedule.MerchantStopSeconds)
end

local function getStop(slot)
    return Stops[slot % #Stops + 1]
end

local function getPhase(time)
    local secondOfSlot = time % EventSchedule.MerchantStopSeconds

    if secondOfSlot < OpenSeconds - PackingSeconds then
        return Phase.Open
    end

    if secondOfSlot < OpenSeconds then
        return Phase.Packing
    end

    return Phase.Gone
end

local function removeCragara()
    if cragara then
        cragara:remove(ArriveCompositeEffectId)
        cragara = nil
    end
end

local function spawnCragara(zone, stop)
    cragara = zone:spawnNpc(CragaraNpcId, stop.x, stop.y, stop.z, stop.heading)

    if cragara then
        cragara:playEffect(ArriveCompositeEffectId)
    end
end

local function enterPhase(zone, slot, phase)
    local stop = getStop(slot)

    if phase == Phase.Gone then
        if cragara then
            removeCragara()
            zone:broadcast(string.format(GoneMessage, getStop(currentSlot).name))
        end
    elseif not cragara or slot ~= currentSlot then
        removeCragara()
        spawnCragara(zone, stop)

        if cragara and phase == Phase.Open then
            zone:broadcast(string.format(ArriveMessage, stop.name))
        end
    end

    if cragara and phase == Phase.Packing then
        zone:broadcast(string.format(PackingMessage, stop.name))
    end

    currentSlot = slot
    currentPhase = phase
end

local function update(zone)
    local time = os.time()
    local slot = getSlot(time)
    local phase = getPhase(time)

    if slot ~= currentSlot or phase ~= currentPhase then
        enterPhase(zone, slot, phase)
    end
end

local function tellWhereCragaraIs(zone, player)
    if not cragara then
        return
    end

    local closesAt = currentSlot * EventSchedule.MerchantStopSeconds + OpenSeconds
    local minutesLeft = math.ceil((closesAt - os.time()) / EventSchedule.SecondsPerMinute)

    player:message(string.format(HereMessage, getStop(currentSlot).name, math.max(minutesLeft, 1)))
end

registerCallback("start", update)

registerCallback("second", update)

registerCallback("playerEnter", tellWhereCragaraIs)
