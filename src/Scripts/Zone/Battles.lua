-- The battles' doors and starters in the world (written by the battle tools; see Resources/Battles.json):
-- each dungeon's entrance on the atlas, and the creatures and dens that offer the random encounters.

local function spawnDoors(zone)
    zone:spawnNpcWithGuid(46184, 100000046184, -801.812, -70.303, 2153.669, 3.1379) -- Arachnia's Lair
    zone:spawnNpcWithGuid(46185, 100000046185, 263.663, 51.688, 164.315, -2.9496) -- Hot Springs Haven
    zone:spawnNpcWithGuid(46186, 100000046186, -1698.971, -5.099, -574.389, 0.0) -- Bandit Hideout
    zone:spawnNpcWithGuid(46187, 100000046187, -419.089, 18.802, -246.099, -0.1183) -- Tavern Cellar
    zone:spawnNpcWithGuid(46188, 100000046188, -901.375, -49.96, 1835.925, 0.5871) -- The Snarling Hedges
    zone:spawnNpcWithGuid(46189, 100000046189, 421.329, 84.547, 643.128, -0.0079) -- Snowy Canyon
    zone:spawnNpcWithGuid(46190, 100000046190, 496.568, 47.072, 357.1, -2.1991) -- The Bat Cave!
    zone:spawnNpcWithGuid(46191, 100000046191, -537.532, 26.039, 772.462, -0.7437) -- Robgoblin Treasure Trove
    zone:spawnNpcWithGuid(46192, 100000046192, 489.989, 47.291, 273.661, -1.4063) -- Deep Mines
    zone:spawnNpcWithGuid(46193, 100000046193, -1445.676, -38.342, 49.892, -1.6845) -- Bixie Hive
    zone:spawnNpcWithGuid(46194, 100000046194, -912.187, -63.66, 399.106, -1.0755) -- Forgotten Caves
    zone:spawnNpcWithGuid(46195, 100000046195, -1433.58, -13.73, -904.22, 2.683) -- Danger Peaks
    zone:spawnNpcWithGuid(46196, 100000046196, -433.54, -0.111, 1345.532, -2.4298) -- Forest Troll Fort
    zone:spawnNpcWithGuid(46197, 100000046197, -39.802, -0.004, 2904.154, 2.6556) -- Briarheart Caverns
    zone:spawnNpcWithGuid(46198, 100000046198, -546.05, -37.062, 2595.832, 0.4363) -- Trail of Betrayal
    zone:spawnNpcWithGuid(46199, 100000046199, -2182.35, -36.544, 141.077, -0.4467) -- Highroad Hijinx
    zone:spawnNpcWithGuid(46200, 100000046200, -223.126, 3.544, -804.55, -0.5657) -- Floren Forest
    zone:spawnNpcWithGuid(46201, 100000046201, -115.596, 20.983, 800.971, -1.3332) -- Mugwort's Hollow
    zone:spawnNpcWithGuid(46202, 100000046202, -407.724, -54.931, 2097.395, 0.9624) -- Briar Patch
    zone:spawnNpcWithGuid(46203, 100000046203, -319.82, -102.05, 2593.831, -0.0683) -- Croaking Vale
    zone:spawnNpcWithGuid(46204, 100000046204, 387.373, 117.924, -16.925, -0.6481) -- Howling Hills
    zone:spawnNpcWithGuid(46205, 100000046205, -649.029, -50.775, 2506.95, 2.2111) -- Dark Spore Depths
    zone:spawnNpcWithGuid(46206, 100000046206, -67.668, -30.336, 2313.788, -2.1225) -- Mushroom Cavern
    zone:spawnNpcWithGuid(46207, 100000046207, -385.926, -68.036, 1871.808, 2.9264) -- Bristlewood Glade
    zone:spawnNpcWithGuid(46208, 100000046208, -850.799, -61.678, 2307.195, -1.574) -- Vale of the Ancients
    zone:spawnNpcWithGuid(46209, 100000046209, -712.791, -49.829, 1752.531, -0.8979) -- Treeleaf's Retreat
    zone:spawnNpcWithGuid(46210, 100000046210, -680.377, 44.233, 1489.32, 2.3182) -- Grexan's Camp
    zone:spawnNpcWithGuid(46211, 100000046211, -903.477, -47.869, 239.337, -3.1372) -- Darvon's Descent
    zone:spawnNpcWithGuid(46212, 100000046212, 145.904, 37.941, -469.974, 2.8937) -- Sweetwater Climb
    zone:spawnNpcWithGuid(46213, 100000046213, -807.608, -50.769, -677.203, -1.0473) -- Den of Secrets
    zone:spawnNpcWithGuid(46214, 100000046214, -45.176, -31.049, -1172.106, 0.019) -- Cray Caves
    zone:spawnNpcWithGuid(46215, 100000046215, -2267.698, -29.346, 896.016, -2.6452) -- Cracked Claw Caverns
    zone:spawnNpcWithGuid(46216, 100000046216, -360.277, -83.082, 2403.682, 0.7216) -- Tanglewood Fort
    zone:spawnNpcWithGuid(46217, 100000046217, -2114.054, -57.044, 279.464, -0.0366) -- Sheep Watch
    zone:spawnNpcWithGuid(46218, 100000046218, -2587.947, -19.11, 1024.189, -1.9772) -- Haunted Mines
    zone:spawnNpcWithGuid(46219, 100000046219, 158.584, 44.722, 1075.002, 2.4054) -- Misty Mountain
    zone:spawnNpcWithGuid(46220, 100000046220, -2386.582, -23.788, 1236.642, -0.1586) -- Bone Bog Cemetery
    zone:spawnNpcWithGuid(46221, 100000046221, -1160.368, -40.694, 744.348, -0.4641) -- Cursed Graveyard!
end

local function spawnStarters(zone)
    zone:spawnNpc(46222, 36.337, 29.508, 368.576, 1.9164) -- Picnic Pandemonium
    zone:spawnNpc(46224, 257.428, 26.57, 434.694, 1.9348) -- Cable Guy
    zone:spawnNpc(46225, -1564.932, -33.054, -662.08, 0.0963) -- Robgoblin Creek!
    zone:spawnNpc(46226, -392.127, -52.635, 2093.826, -1.3458) -- Nettleseed Nibblers!
    zone:spawnNpc(46227, -538.309, -81.926, 2352.443, -2.8027) -- Moldy Shamblers!
    zone:spawnNpc(46228, -64.345, -28.512, 2298.137, -0.2092) -- The Mushroom Gigas!
    zone:spawnNpc(46229, -511.7, -44.221, -1420.262, -2.8571) -- Cray Marauders
    zone:spawnNpc(46229, -379.714, -49.047, -1506.599, 1.5193) -- Cray Marauders
    zone:spawnNpc(46230, -1877.321, -29.354, -632.98, -2.8352) -- Thugawug Bumbler!
    zone:spawnNpc(46231, -508.423, -65.032, 1887.235, 3.1364) -- Mushroom Mania!
    zone:spawnNpc(46232, -923.23, -43.949, -1166.129, 0.9574) -- Stealthy Despoilers
    zone:spawnNpc(46233, -1825.475, -54.203, -40.395, -1.6681) -- Thugawug Sneak!
    zone:spawnNpc(46234, -1473.012, -19.451, -858.962, -0.4087) -- Fleetfoot Ninja!
    zone:spawnNpc(46235, -1534.276, -35.793, 396.997, 2.4987) -- Bixies Gone Wild!
    zone:spawnNpc(46235, -1419.013, -33.646, 256.881, 2.9794) -- Bixies Gone Wild!
    zone:spawnNpc(46236, 26.78, 91.637, 488.281, 2.1629) -- Ambushed!
    zone:spawnNpc(46237, -2376.57, -23.965, 1224.161, -0.676) -- Tormented Spirits!
    zone:spawnNpc(46238, -1486.33, -36.638, 323.642, -0.0438) -- Medic: Surgical Precision
    zone:spawnNpc(46239, -790.144, -69.618, 2369.367, 1.1102) -- Medic: Shock Paddles and You
    zone:spawnNpc(46240, -2593.912, -19.875, 1009.342, 0.382) -- Bergram Stumpfinger's Ghost
    zone:spawnNpc(46241, -57.435, -33.661, -1182.388, 0.8729) -- Mudshell
    zone:spawnNpc(46242, -826.622, 13.824, -990.182, -1.436) -- Snatching Snappers!
    zone:spawnNpc(46242, -272.698, -45.941, -1306.829, 2.3854) -- Snatching Snappers!
    zone:spawnNpc(46243, -832.156, 13.605, -993.323, 0.4035) -- Seaside Swoopers
    zone:spawnNpc(46244, -731.536, -65.499, 1978.208, 2.953) -- Vulture Watch!
    zone:spawnNpc(46244, -718.211, -66.819, 1975.571, 3.1089) -- Vulture Watch!
    zone:spawnNpc(46245, -770.307, -68.925, 2106.278, -1.7133) -- Eight-Legged Monstrosities!
    zone:spawnNpc(46246, -817.88, -52.298, 1911.783, 2.4847) -- Chugawump!
    zone:spawnNpc(46247, -724.303, -50, 1763.643, 2.3386) -- Snakes in a Maze!
    zone:spawnNpc(46248, -899.518, -50, 1851.817, -3.0252) -- When Plants Attack!
    zone:spawnNpc(46249, -2167.927, -39.235, -90.898, 1.3037) -- Thugawug Bandits!
    zone:spawnNpc(46250, -377.34, -48.598, -1500.202, 2.8855) -- Oasis of Peril!
    zone:spawnNpc(46251, -1831.369, -54.348, -45.576, 0.0211) -- Thugamug
    zone:spawnNpc(46252, -38.976, -32.74, -1186.856, -0.3979) -- Prince of Low Tide!
    zone:spawnNpc(46253, -522.218, 23.08, 777.094, -1.8645) -- Brutus the Brute!
    zone:spawnNpc(46254, 85.559, 64.711, 540.606, 0.4709) -- Call of the Wildest!
    zone:spawnNpc(46255, -2598.181, -16.322, 1036.488, 2.4476) -- Spawn of Necrosis!
    zone:spawnNpc(46256, -1911.49, -36.726, -637.757, -1.1533) -- Brugo's Fishing Spot
    zone:spawnNpc(46257, -2407.563, -32.103, 1169.211, -2.2052) -- Grave Danger!
    zone:spawnNpc(46258, -2401.752, -23.979, 1231.556, 1.2473) -- Wraiths of Wrath!
    zone:spawnNpc(46259, -766.345, -68.762, -728.867, -0.3233) -- Pixie Hunters!
    zone:spawnNpc(46260, -611.092, -2.746, -689.03, 1.8783) -- Feisty Floren!
    zone:spawnNpc(46261, 24.384, 24.708, -387.57, -1.6618) -- Return to Sender!
    zone:spawnNpc(46262, -565.088, -68.524, 2034.539, -1.1323) -- Venomous Frogs!
    zone:spawnNpc(46263, -1233.284, -30.6, -719.074, 1.8842) -- Village in distress!
    zone:spawnNpc(46264, -182.636, 3.355, 1131.556, 0.0444) -- Hidden Troll Fort
end

registerCallback("start", function(zone)
    spawnDoors(zone)
    spawnStarters(zone)
end)
