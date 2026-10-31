using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Text.RegularExpressions;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sanctuary.Game.Battles;
using Sanctuary.Game.Pathfinding;
using Sanctuary.Game.Resources;
using Sanctuary.Game.Resources.Definitions;
using Sanctuary.Game.Terrain;

namespace Sanctuary.Game.Tests;

[TestClass]
public sealed class BattleTests
{
    [TestMethod]
    public void EncounterId_PutsTheInstanceOverTheBattle()
    {
        // As live: instance 2 of battle 174.
        Assert.AreEqual(0x200AE, BattleRules.EncounterId(2, 174));
        Assert.AreEqual(0x7FFF0001, BattleRules.EncounterId(0x7FFF, 1));
    }

    [TestMethod]
    public void Payout_PaysWinnersInFullAndLosersForWhatTheyBeat()
    {
        var battle = new BattleDefinition { Stars = 100, Coins = 30 };

        Assert.AreEqual((100, 30), BattleRules.Payout(battle, won: true, defeated: 37, total: 37));
        Assert.AreEqual((25, 0), BattleRules.Payout(battle, won: false, defeated: 10, total: 20));
        Assert.AreEqual((0, 0), BattleRules.Payout(battle, won: false, defeated: 0, total: 20));
    }

    [TestMethod]
    public void EnemyCount_CountsEveryWave()
    {
        var battle = new BattleDefinition
        {
            Waves =
            [
                new BattleWaveDefinition { Spawns = [new(), new()] },
                new BattleWaveDefinition { AtStart = true, Spawns = [new()] }
            ]
        };

        Assert.AreEqual(3, BattleRules.EnemyCount(battle));
    }

    [TestMethod]
    public void EntryExit_StandsBesideTheEntryNotBehindIt()
    {
        var entry = new Vector3(10f, 2f, 20f);

        // Facing +Z, with room all round: to one side, level with the entry (behind it is where the camera starts).
        var open = BattleRules.EntryExitPosition(entry, 0f, (start, goal) => goal);

        Assert.AreEqual(entry.X + BattleRules.EntryExitDistance, open.X, 0.01f);
        Assert.AreEqual(entry.Z, open.Z, 0.01f);

        // A wall a metre that way: the other side.
        static Vector3 WallToOneSide(Vector3 start, Vector3 goal) =>
            goal.X <= start.X + 1f ? goal : Vector3.Lerp(start, goal, 1f / (goal.X - start.X));

        var walled = BattleRules.EntryExitPosition(entry, 0f, WallToOneSide);

        Assert.AreEqual(entry.X - BattleRules.EntryExitDistance, walled.X, 0.01f);
        Assert.AreEqual(entry.Z, walled.Z, 0.01f);
    }

    [TestMethod]
    public void Battles_LoadAndPointAtRealData()
    {
        var resources = FindSourceResources();
        var battles = new BattleDefinitionCollection(NullLogger.Instance);

        Assert.IsTrue(battles.Load(Path.Combine(resources, "Battles.json")));
        Assert.IsTrue(battles.Count > 0);

        var zoneIds = Directory.GetFiles(Path.Combine(resources, "Zones"), "*.json")
            .Select(path => JsonDocument.Parse(File.ReadAllText(path)).RootElement)
            .Where(zone => zone.TryGetProperty("Id", out _))
            .ToDictionary(zone => zone.GetProperty("Id").GetInt32(), zone => zone.GetProperty("Name").GetString());
        var npcIds = JsonDocument.Parse(File.ReadAllText(Path.Combine(resources, "Npcs.json"))).RootElement.EnumerateArray()
            .Select(npc => npc.GetProperty("Id").GetInt32()).ToHashSet();
        var mobKeys = JsonDocument.Parse(File.ReadAllText(Path.Combine(resources, "Mobs.json"))).RootElement.EnumerateArray()
            .Select(mob => mob.GetProperty("Key").GetString()).ToHashSet();

        // The Exit every battle puts up when it's won.
        Assert.IsTrue(npcIds.Contains(34061));

        foreach (var battle in battles.Values)
        {
            Assert.IsTrue(zoneIds.ContainsKey(battle.ZoneId), $"Battle {battle.Id} ({battle.Comment}): no zone {battle.ZoneId}.");

            foreach (var starter in battle.StarterNpcIds)
                Assert.IsTrue(npcIds.Contains(starter), $"Battle {battle.Id} ({battle.Comment}): no starter NPC {starter}.");

            foreach (var spawn in battle.Waves.SelectMany(wave => wave.Spawns))
            {
                Assert.IsTrue(npcIds.Contains(spawn.NpcId), $"Battle {battle.Id} ({battle.Comment}): no NPC {spawn.NpcId}.");
                Assert.IsTrue(spawn.Mob is null || mobKeys.Contains(spawn.Mob), $"Battle {battle.Id} ({battle.Comment}): no mob {spawn.Mob}.");
            }
        }
    }

    [TestMethod]
    public void Dungeons_PostTheirGuardsAndSaveTheBossForLast()
    {
        var resources = FindSourceResources();
        var battles = new BattleDefinitionCollection(NullLogger.Instance);

        Assert.IsTrue(battles.Load(Path.Combine(resources, "Battles.json")));

        var mobs = JsonDocument.Parse(File.ReadAllText(Path.Combine(resources, "Mobs.json"))).RootElement.EnumerateArray()
            .ToDictionary(mob => mob.GetProperty("Key").GetString()!, mob => mob);

        // A dungeon's guards stand at their posts from the start.
        var dungeons = battles.Values.Where(battle => battle.Waves.Any(wave => wave.AtStart)).ToList();

        Assert.IsTrue(dungeons.Count > 0);

        foreach (var dungeon in dungeons)
        {
            var bosses = dungeon.Waves.SelectMany(wave => wave.Spawns).Where(spawn => spawn.Boss).ToList();

            Assert.AreEqual(1, bosses.Count, $"Battle {dungeon.Id} ({dungeon.Comment}): a dungeon has one boss.");
            Assert.IsNotNull(dungeon.Exit, $"Battle {dungeon.Id} ({dungeon.Comment}): its Exit goes up in the boss's room.");

            // The boss comes once every guard is beaten.
            var last = dungeon.Waves[^1];

            Assert.IsFalse(last.AtStart, $"Battle {dungeon.Id} ({dungeon.Comment}): the boss's wave comes last.");
            Assert.IsTrue(last.Spawns.Contains(bosses[0]), $"Battle {dungeon.Id} ({dungeon.Comment}): the boss comes with the last wave.");
            Assert.IsTrue(dungeon.Waves.SkipLast(1).All(wave => wave.AtStart), $"Battle {dungeon.Id} ({dungeon.Comment}): every wave but the boss's is posted.");

            var bossHealth = mobs[bosses[0].Mob!].GetProperty("MaxHitpoints").GetInt32();

            foreach (var guard in dungeon.Waves.Where(wave => wave.AtStart).SelectMany(wave => wave.Spawns))
            {
                var mob = mobs[guard.Mob!];

                // Guards only come for players near their post, not across the dungeon at the entry.
                Assert.IsTrue(mob.GetProperty("AggroRadius").GetSingle() <= 20f, $"Battle {dungeon.Id} ({dungeon.Comment}): {guard.Mob} would leave its post.");
                Assert.IsTrue(mob.GetProperty("MaxHitpoints").GetInt32() < bossHealth, $"Battle {dungeon.Id} ({dungeon.Comment}): its boss is tougher than its guards.");
            }
        }
    }

    [TestMethod]
    public void BattleZones_LetEveryEnemyReachTheEntry()
    {
        var resources = FindSourceResources();
        var battles = new BattleDefinitionCollection(NullLogger.Instance);

        Assert.IsTrue(battles.Load(Path.Combine(resources, "Battles.json")));

        var zoneNames = Directory.GetFiles(Path.Combine(resources, "Zones"), "*.json")
            .Select(path => JsonDocument.Parse(File.ReadAllText(path)).RootElement)
            .Where(zone => zone.TryGetProperty("Id", out _))
            .ToDictionary(zone => zone.GetProperty("Id").GetInt32(), zone => zone.GetProperty("Name").GetString()!);

        var zones = new Dictionary<int, (TerrainMesh Terrain, NavMeshPathfinder Pathfinder)>();

        foreach (var battle in battles.Values)
        {
            if (!zones.TryGetValue(battle.ZoneId, out var zone))
            {
                var terrainPath = Path.Combine(resources, "Terrain", zoneNames[battle.ZoneId] + ".terrain");

                Assert.IsTrue(File.Exists(terrainPath), $"Battle {battle.Id} ({battle.Comment}): its zone has no terrain.");
                Assert.IsTrue(TerrainLoader.TryLoad(terrainPath, NullLogger.Instance, out var terrain));

                var navMesh = NavMeshBuilder.Build(terrain!);

                Assert.IsNotNull(navMesh, $"Battle {battle.Id} ({battle.Comment}): nothing walkable in its zone.");

                zone = zones[battle.ZoneId] = (terrain!, new NavMeshPathfinder(navMesh));
            }

            var pathfinder = zone.Pathfinder;

            var entry = new Vector3(battle.Entry[0], battle.Entry[1], battle.Entry[2]);
            var places = battle.Waves.SelectMany(wave => wave.Spawns).Select(spawn => spawn.Position).ToList();

            if (battle.Exit is not null)
                places.Add(battle.Exit);

            foreach (var place in places)
            {
                var goal = new Vector3(place[0], place[1], place[2]);
                var waypoints = new Queue<Vector3>();

                Assert.IsTrue(pathfinder.TryFindPath(entry, goal, waypoints), $"Battle {battle.Id} ({battle.Comment}): no way from the entry to {goal}.");

                var end = waypoints.Count > 0 ? waypoints.Last() : entry;

                Assert.IsTrue(Vector2.Distance(new Vector2(end.X, end.Z), new Vector2(goal.X, goal.Z)) < 2.5f,
                    $"Battle {battle.Id} ({battle.Comment}): {goal} can't be reached from the entry (the way ends at {end}).");
            }

            // The Exit that's up from the start stands clear of the entry, on ground the players can walk to.
            var entryExit = BattleRules.EntryExitPosition(entry, battle.EntryHeading, (start, goal) =>
            {
                pathfinder.TryWalkStraight(start, goal, out var end);
                return end;
            });

            Assert.IsTrue(Vector2.Distance(new Vector2(entryExit.X, entryExit.Z), new Vector2(entry.X, entry.Z)) >= 2f,
                $"Battle {battle.Id} ({battle.Comment}): no room behind or beside the entry for its Exit (only {entryExit}).");
            Assert.IsTrue(zone.Terrain.TryGetGroundHeight(entryExit.X, entryExit.Z, entryExit.Y + 3f, 6f, out _),
                $"Battle {battle.Id} ({battle.Comment}): its entry's Exit at {entryExit} has no ground under it.");

            // A dungeon's players, enemies and Exit stand on the ground: not over it, not in it. (A random encounter's
            // enemies keep the spots the live servers sent.)
            if (!battle.Waves.Any(wave => wave.AtStart))
                continue;

            foreach (var place in places.Prepend(battle.Entry))
            {
                Assert.IsTrue(zone.Terrain.TryGetGroundHeight(place[0], place[2], place[1] + 1f, 2f, out var ground) && MathF.Abs(ground - place[1]) < 0.75f,
                    $"Battle {battle.Id} ({battle.Comment}): ({place[0]}, {place[1]}, {place[2]}) isn't on the ground.");
            }
        }
    }

    [TestMethod]
    public void Battles_RunEveryLiveBattle()
    {
        var resources = FindSourceResources();
        var battles = new BattleDefinitionCollection(NullLogger.Instance);

        Assert.IsTrue(battles.Load(Path.Combine(resources, "Battles.json")));

        var names = battles.Values.Select(battle => battle.NameId).ToHashSet();

        // The live Activities list's 38 dungeons (each with a Games window tile) and its 62 random, wandering and hidden
        // encounters, by name.
        int[] dungeons =
        [
            2273, 4229, 21188, 4469, 6488, 5078, 4187, 162, 6456, 5133, 5172, 5493, 3269, 5697, 5698, 5699, 5701, 5702, 5703, 5704, 5705,
            5706, 5707, 5708, 5710, 5711, 18143, 7697, 18219, 5700, 71694, 71769, 71770, 71771, 71772, 23555, 90478, 414849
        ];
        int[] encounters =
        [
            42169, 6564, 41569, 42062, 42055, 42056, 70112, 42053, 71695, 74374, 71698, 73711, 73712, 73713, 73977, 74377, 73989, 74637,
            74754, 75449, 75450, 75451, 75452, 76624, 75999, 76748, 93132, 78089, 78143, 78144, 78145, 78146, 90148, 93260, 93261, 93262,
            93264, 93265, 93268, 93269, 93271, 93272, 93274, 93276, 93277, 94365, 103031, 115822, 116053, 116054, 116055, 116058, 130341,
            141037, 141038, 141039, 141040, 141043, 383185, 415192, 422969, 71697
        ];

        foreach (var nameId in dungeons.Concat(encounters))
            Assert.IsTrue(names.Contains(nameId), $"No battle for the live battle named {nameId}.");

        foreach (var nameId in dungeons)
            Assert.IsNotNull(battles.Values.Single(battle => battle.NameId == nameId).Image, $"Dungeon {nameId} has no Games window tile.");
    }

    [TestMethod]
    public void Battles_AreOfferedByDoorsAndCreaturesInTheWorld()
    {
        var resources = FindSourceResources();
        var battles = new BattleDefinitionCollection(NullLogger.Instance);

        Assert.IsTrue(battles.Load(Path.Combine(resources, "Battles.json")));

        var npcs = JsonDocument.Parse(File.ReadAllText(Path.Combine(resources, "Npcs.json"))).RootElement.EnumerateArray()
            .ToDictionary(npc => npc.GetProperty("Id").GetInt32());

        // What the world's zone scripts stand in the world, and where.
        var spawns = Directory.GetFiles(Path.Combine(resources, "..", "Scripts", "Zone"), "*.lua")
            .SelectMany(path => Regex.Matches(File.ReadAllText(path), @"spawnNpc(?:WithGuid)?\((\d+), (?:\d+, )?(-?[\d.]+), (-?[\d.]+), (-?[\d.]+)"))
            .Select(match => (Id: int.Parse(match.Groups[1].Value),
                Position: new Vector2(float.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture), float.Parse(match.Groups[4].Value, CultureInfo.InvariantCulture))))
            .ToList();
        var spawned = spawns.Select(spawn => spawn.Id).ToHashSet();

        foreach (var battle in battles.Values)
        {
            var starters = battle.StarterNpcIds.Where(spawned.Contains).ToList();

            Assert.IsTrue(starters.Count > 0, $"Battle {battle.Id} ({battle.Comment}): nothing in the world offers it.");

            // The crossed swords over its head say it starts a battle.
            foreach (var starter in starters)
            {
                Assert.IsTrue(npcs[starter].TryGetProperty("Notifications", out var notifications)
                    && notifications.EnumerateArray().Any(notification => notification.GetProperty("NotificationType").GetInt32() == 3
                        && notification.GetProperty("IconId").GetInt32() == 7 && notification.GetProperty("IconState").GetInt32() == 24),
                    $"Battle {battle.Id} ({battle.Comment}): its starter {starter} doesn't show the battle icon.");
            }
        }

        // Each dungeon entrance on the atlas has its dungeon's door beside it.
        var entrances = JsonDocument.Parse(File.ReadAllText(Path.Combine(resources, "PointOfInterests.json"))).RootElement.EnumerateArray()
            .Where(poi => poi.TryGetProperty("NotificationType", out var type) && type.GetInt32() == 3);
        var doors = 0;

        foreach (var entrance in entrances)
        {
            var battle = battles.Values.FirstOrDefault(battle => battle.NameId == entrance.GetProperty("NameId").GetInt32());

            if (battle is null)
                continue;

            var position = entrance.GetProperty("Position");
            var at = new Vector2(position[0].GetSingle(), position[2].GetSingle());

            Assert.IsTrue(spawns.Any(spawn => battle.StarterNpcIds.Contains(spawn.Id) && Vector2.Distance(spawn.Position, at) < 12f),
                $"Battle {battle.Id} ({battle.Comment}): no door at its atlas entrance {at}.");
            doors++;
        }

        Assert.IsTrue(doors >= 36, $"Only {doors} atlas entrances have their door.");
    }

    private static string FindSourceResources()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Resources", "Battles.json")))
            directory = directory.Parent;

        Assert.IsNotNull(directory, "src/Resources/Battles.json not found above the test output.");

        return Path.Combine(directory.FullName, "Resources");
    }
}
