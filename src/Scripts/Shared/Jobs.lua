-- The jobs by their Resources/Profiles.json id, which is what player.job and the jobRankUp event give.
local Jobs = {}

Jobs.MaxRank = 20

local Names = {
    [1] = "Adventurer",
    [2] = "Ninja",
    [4] = "Postman",
    [11] = "Medic",
    [12] = "Wizard",
    [14] = "Miner",
    [16] = "Blacksmith",
    [20] = "Enforcer",
    [32] = "Warrior",
    [35] = "Archer",
    [43] = "Brawler",
    [45] = "Chef",
    [48] = "Kart Driver",
    [49] = "Demo Derby Driver",
    [52] = "Soccer Star",
    [58] = "Referee",
    [120] = "Card Duelist",
    [137] = "Fisherman",
}

function Jobs.name(jobId)
    return Names[jobId] or "Adventurer"
end

-- "an Archer", "a Ninja".
function Jobs.withArticle(jobId)
    local name = Jobs.name(jobId)

    return (string.match(name, "^[AEIOU]") and "an " or "a ") .. name
end

return Jobs
