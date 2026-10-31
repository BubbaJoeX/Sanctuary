using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;

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

    private static string FindSourceResources()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Resources", "Battles.json")))
            directory = directory.Parent;

        Assert.IsNotNull(directory, "src/Resources/Battles.json not found above the test output.");

        return Path.Combine(directory.FullName, "Resources");
    }
}
