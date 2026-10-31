-- Job rank-ups, cheered by the players around, and by the whole zone at every fifth rank and at the top. The world,
-- the fishing ponds and every battle's zone run this, wherever stars are earned.
local Jobs = require("Jobs")

local NearbyRadius = 40
local MilestoneEvery = 5

local FireworksCompositeEffectId = 5349

registerCallback("jobRankUp", function(zone, player, jobId, rank)
    local job = Jobs.name(jobId)

    if rank >= Jobs.MaxRank then
        zone:broadcast(string.format("%s has mastered the %s job, reaching rank %d!", player.name, job, rank))
        player:playEffect(FireworksCompositeEffectId)
    elseif rank % MilestoneEvery == 0 then
        zone:broadcast(string.format("%s is now a rank %d %s!", player.name, rank, job))
        player:playEffect(FireworksCompositeEffectId)
    else
        local position = player.position

        zone:broadcastNear(string.format("%s reached rank %d as %s.", player.name, rank, Jobs.withArticle(jobId)),
            position.x, position.z, NearbyRadius)
    end
end)
