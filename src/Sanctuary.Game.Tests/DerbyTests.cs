using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sanctuary.Game.Resources;
using Sanctuary.Game.Resources.Definitions;
using Sanctuary.Game.Terrain;
using Sanctuary.Game.VehicleGames;
using Sanctuary.Game.VehicleGames.Derby;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game.Tests;

[TestClass]
public class DerbyTests
{
    private const ulong CharacterGuid = 0x4B52CC6A9573CA91;

    [TestMethod]
    public void VehicleGames_Load()
    {
        var games = LoadGames();

        Assert.IsTrue(games.TryGetStage(39, out var derby));
        Assert.AreEqual(VehicleGameKind.Derby, derby.Kind);

        // Every live kart and derby car reads whole, as the client reads it.
        foreach (var name in new[] { "kt_kart08", "kt_speedster_335m", "kt_zipster", "dm_crusher_claw", "dm_ripper_pirate", "dm_crusher_hotrod" })
            Assert.IsTrue(games.TryGetVehicle(name, out _), name);

        Assert.AreEqual(4176, games.Definition.Derby.PlayerVehicle is { } vehicle && games.TryGetVehicle(vehicle, out var config) ? config.Length : 0);
    }

    [TestMethod]
    public void DerbyStages_HaveTheirZoneDriversAndStarter()
    {
        var resources = FindSourceResources();
        var games = LoadGames();

        var zoneIds = Directory.GetFiles(Path.Combine(resources, "Zones"), "*.json")
            .Select(path => JsonDocument.Parse(File.ReadAllText(path)).RootElement)
            .Where(zone => zone.TryGetProperty("Id", out _))
            .ToDictionary(zone => zone.GetProperty("Id").GetInt32(), zone => zone.GetProperty("Name").GetString());

        var npcIds = JsonDocument.Parse(File.ReadAllText(Path.Combine(resources, "Npcs.json"))).RootElement.EnumerateArray()
            .Select(npc => npc.GetProperty("Id").GetInt32()).ToHashSet();

        foreach (var stage in games.Stages)
        {
            Assert.IsTrue(zoneIds.ContainsKey(stage.ZoneId), $"{stage.Comment}: no zone {stage.ZoneId}.");

            foreach (var npcId in stage.StarterNpcIds)
                Assert.IsTrue(npcIds.Contains(npcId), $"{stage.Comment}: no starter NPC {npcId}.");
        }

        Assert.AreEqual("sg_dm_arena", zoneIds[games.Stages.Single(stage => stage.Kind == VehicleGameKind.Derby).ZoneId]);

        // Seven AI cars, as live, each with a driver of its own.
        var drivers = games.Definition.Derby.Drivers;

        Assert.AreEqual(7, drivers.Count);
        Assert.AreEqual(7, drivers.Select(driver => driver.NpcId).Distinct().Count());

        foreach (var driver in drivers)
            Assert.IsTrue(npcIds.Contains(driver.NpcId), $"No derby driver {driver.NpcId}.");
    }

    [TestMethod]
    public void Offer_MatchesTheLiveDerby()
    {
        var games = LoadGames();

        Assert.IsTrue(games.TryGetStage(39, out var stage));

        var participant = new EncounterParticipant
        {
            Key = unchecked((int)0xDE2106FB),
            Guid = CharacterGuid,
            Position = stage.EntryPosition,
            Name = "Maÿhem",
            ServerId = 1008
        };

        var live = VehicleGamePacketTests.Live("derby", "S>C 41/114");

        CollectionAssert.AreEqual(live[0], VehicleGameManager.BuildDetails(0x00140041, stage, [participant], isUpdate: false).Serialize(), "The offer");

        participant.Entered = true;

        CollectionAssert.AreEqual(live[1], VehicleGameManager.BuildDetails(0x00140041, stage, [participant], isUpdate: true).Serialize(), "The update once in");
    }

    [TestMethod]
    public void CreateProxied_BuildsTheLiveCars()
    {
        var games = LoadGames();
        var arena = games.Definition.Derby;
        var live = VehicleGamePacketTests.Live("derby", "S>C 10");

        Assert.IsTrue(VehicleDemolitionDerbyPacketCreateProxiedVehicleDemolitionDerby.TryDeserialize(live[0], out var player));
        Assert.IsTrue(games.TryGetVehicle(arena.PlayerVehicle, out var vehicle));

        // The player's car from the arena's own data: the live bytes, but for float rounding in the spawn.
        var built = new VehicleDemolitionDerbyPacketCreateProxiedVehicleDemolitionDerby
        {
            Guid = CharacterGuid,
            Name = " ",
            Config = vehicle,
            Spawn = ArenaRules.Transform(arena.PlayerSpawn.Point, ArenaRules.Forward(arena.PlayerSpawn.Heading), Vector3.UnitY),
            IsPlayer = true,
            Unknown = arena.TimeLimitSeconds
        };

        Assert.IsTrue(VehicleDemolitionDerbyPacketCreateProxiedVehicleDemolitionDerby.TryDeserialize(built.Serialize(), out var reread));

        CollectionAssert.AreEqual(player.Config.Data, reread.Config.Data);
        Assert.AreEqual(player.Name, reread.Name);
        Assert.AreEqual(player.IsPlayer, reread.IsPlayer);
        Assert.AreEqual(player.Unknown, reread.Unknown);

        for (var row = 0; row < 4; row++)
        {
            for (var column = 0; column < 4; column++)
                Assert.AreEqual(player.Spawn[row, column], reread.Spawn[row, column], 0.002f, $"Spawn [{row}, {column}]");
        }

        // An AI car: no name, not a player's.
        Assert.IsTrue(VehicleDemolitionDerbyPacketCreateProxiedVehicleDemolitionDerby.TryDeserialize(live[1], out var ai));
        Assert.AreEqual(string.Empty, ai.Name);
        Assert.IsFalse(ai.IsPlayer);
        Assert.IsTrue(arena.Drivers.Any(driver => games.TryGetVehicle(driver.Vehicle, out var config) && config.Data.SequenceEqual(ai.Config.Data)));
    }

    [TestMethod]
    public void CrashDamage_TakesWhatLiveCrashesTook()
    {
        Assert.AreEqual(0, DerbyRules.CrashDamage(2f));
        Assert.AreEqual(10, DerbyRules.CrashDamage(5f));
        Assert.AreEqual(20, DerbyRules.CrashDamage(12f));
        Assert.AreEqual(42, DerbyRules.CrashDamage(18f));

        // Driving into a car standing still: only the attacker deals damage.
        var closing = DerbyRules.Closing(Vector3.Zero, new Vector3(0f, 0f, 12f), new Vector3(0f, 0f, 3f), Vector3.Zero);

        Assert.IsNotNull(closing);
        Assert.AreEqual((20, 0), DerbyRules.CrashDamage(closing.Value.SpeedA, closing.Value.SpeedB));

        // Head on: both.
        var headOn = DerbyRules.Closing(Vector3.Zero, new Vector3(0f, 0f, 5f), new Vector3(0f, 0f, 3f), new Vector3(0f, 0f, -5f));

        Assert.IsNotNull(headOn);
        Assert.AreEqual((10, 10), DerbyRules.CrashDamage(headOn.Value.SpeedA, headOn.Value.SpeedB));

        // Driving apart isn't a crash.
        Assert.IsNull(DerbyRules.Closing(Vector3.Zero, new Vector3(0f, 0f, -5f), new Vector3(0f, 0f, 3f), Vector3.Zero));
    }

    [TestMethod]
    public void Hit_NeverTakesMoreThanLeftAndNoneThroughAShield()
    {
        var attacker = Car(1);
        var victim = Car(2);

        victim.Health = 7;

        // Live: the player's 20-damage hit on a car with 7 left dealt 7 and wrecked it.
        Assert.AreEqual(7, DerbyRules.Hit(attacker, victim, 20, now: 1000));
        Assert.AreEqual(0, victim.Health);
        Assert.AreEqual(7, attacker.DamageDealt);
        Assert.AreEqual(1ul, victim.LastAttacker);
        Assert.IsTrue(victim.IsWrecked);

        var shielded = Car(3);
        shielded.ShieldUntil = 5000;

        Assert.AreEqual(0, DerbyRules.Hit(attacker, shielded, 42, now: 1000));
        Assert.AreEqual(100, shielded.Health);

        var boosted = Car(4);
        boosted.AttackUpUntil = 5000;

        Assert.AreEqual(30, DerbyRules.Hit(boosted, Car(5), 20, now: 1000));
    }

    /// <summary>
    /// The live match's wrecks and respawns, in order, and the scores its state data showed after each.
    /// </summary>
    [TestMethod]
    public void Scoring_ReplaysTheLiveMatch()
    {
        var player = Car(0);
        var cars = new Dictionary<int, DerbyCar> { [0] = player };

        for (var i = 1; i <= 7; i++)
            cars[i] = Car((ulong)i);

        void Wreck(int attacker, int victim)
        {
            var leader = DerbyRules.Leader(cars.Values);

            cars[victim].Health = 0;
            DerbyRules.Wreck(cars[attacker], cars[victim], leader == cars[victim], now: 1);
        }

        void Score(params (int Car, int Score)[] expected)
        {
            foreach (var (car, score) in expected)
                Assert.AreEqual(score, cars[car].Score, $"Car {car}");
        }

        Wreck(2, 7);                            // 213.691
        Score((2, 2));
        Assert.AreSame(cars[2], DerbyRules.Leader(cars.Values));

        DerbyRules.Respawn(cars[7]);            // 217.705
        Score((7, 0));
        Assert.AreEqual(1, cars[7].TimesWrecked);

        Wreck(2, 0);                            // 250.361: the player
        Score((2, 4));
        DerbyRules.Respawn(player);             // 254.326
        Score((0, 0));

        Wreck(5, 2);                            // 256.513: the leader, worth 3
        Score((5, 3), (2, 4));

        Wreck(6, 1);                            // 260.022
        Score((6, 2));
        DerbyRules.Respawn(cars[2]);            // 260.516
        Score((2, 3));
        Assert.IsNull(DerbyRules.Leader(cars.Values), "Cars 2 and 5 share the lead.");

        Wreck(0, 5);                            // 261.845
        Score((0, 2));
        DerbyRules.Respawn(cars[1]);            // 264.031
        DerbyRules.Respawn(cars[5]);            // 265.865
        Score((1, 0), (5, 2));
        Assert.AreSame(cars[2], DerbyRules.Leader(cars.Values));

        Wreck(0, 7);                            // 271.250
        Score((0, 4));
        Assert.AreSame(player, DerbyRules.Leader(cars.Values));
        DerbyRules.Respawn(cars[7]);            // 274.810
        Assert.AreEqual(2, cars[7].TimesWrecked);

        Wreck(7, 6);                            // 291.447
        Wreck(0, 4);                            // 292.050
        Score((7, 2), (0, 6));
        DerbyRules.Respawn(cars[6]);            // 295.453
        DerbyRules.Respawn(cars[4]);            // 296.072
        Score((6, 1), (4, 0));

        Wreck(6, 7);                            // 308.363
        Score((6, 3));

        Assert.AreSame(player, DerbyRules.Standings(cars.Values)[0]);
    }

    [TestMethod]
    public void Respawn_RepairsAtTheSpawn()
    {
        var car = Car(1);

        car.Position = new Vector3(10f, 0f, 10f);
        car.Health = 0;
        car.Parts[0] = false;
        car.Dents[2] = 0.1f;

        DerbyRules.Respawn(car);

        Assert.AreEqual(100, car.Health);
        Assert.AreEqual(car.SpawnPosition, car.Position);
        Assert.IsTrue(car.Parts.All(part => part));
        Assert.IsTrue(car.Dents.All(dent => dent == 0f));
    }

    /// <summary>
    /// The arena floor under the live AI cars: the server's terrain must put them where live did.
    /// </summary>
    [TestMethod]
    public void ArenaTerrain_HoldsTheLiveCars()
    {
        var resources = FindSourceResources();

        Assert.IsTrue(TerrainLoader.TryLoad(Path.Combine(resources, "Terrain", "sg_dm_arena.terrain"), NullLogger.Instance, out var terrain));

        foreach (var live in VehicleGamePacketTests.Live("derby", "S>C 5"))
        {
            Assert.IsTrue(VehicleDemolitionDerbyPacketUpdateVehicleDemolitionDerbyData.TryDeserialize(live, out var state));

            var position = state.Transform.Translation;

            // A car at its spawn is dropped in from above; one on the floor sits about the ride height over it.
            Assert.IsTrue(terrain!.TryGetGroundHeight(position.X, position.Z, position.Y + 1f, 30f, out var ground), $"No floor under {position}.");
            Assert.IsTrue(position.Y - ground > 0f, $"{position} is under the floor ({ground}).");

        }

        // Live AI cars mid-match (S>C 34/5) rode the same height over the server's floor everywhere.
        Vector3[] driving =
        [
            new(276.19f, 7.115f, 295.45f), new(260.596f, 7.119f, 305.628f), new(235.498f, 6.511f, 232.758f),
            new(294.774f, 7.119f, 283.851f), new(253.717f, 7.116f, 288.938f), new(253.065f, 6.435f, 249.943f)
        ];

        foreach (var position in driving)
        {
            Assert.IsTrue(terrain!.TryGetGroundHeight(position.X, position.Z, position.Y + 4f, 30f, out var ground), $"No floor under {position}.");
            Assert.AreEqual(DerbyRules.RideHeight, position.Y - ground, 0.005f, $"The live car at {position}.");
        }

        var games = LoadGames();
        var arena = games.Definition.Derby;

        foreach (var pickup in arena.Pickups)
            Assert.IsTrue(terrain!.TryGetGroundHeight(pickup.Position[0], pickup.Position[2], pickup.Position[1] + 10f, 30f, out _), $"Pickup {pickup.Id} isn't over the floor.");
    }

    /// <summary>
    /// Four minutes of the seven live AI cars (and an eighth in the player's spot) on the server's arena floor: they
    /// stay in the arena and on its floor, find each other, crash, wreck and come back, and take the boxes.
    /// </summary>
    [TestMethod]
    public void AiCars_PlayAWholeMatch()
    {
        var resources = FindSourceResources();
        var games = LoadGames();
        var arena = games.Definition.Derby;

        Assert.IsTrue(TerrainLoader.TryLoad(Path.Combine(resources, "Terrain", "sg_dm_arena.terrain"), NullLogger.Instance, out var terrain));

        var simulation = new DerbySimulation(arena, terrain!.TryGetGroundHeight, new Random(39));

        var crashes = 0;
        var wrecks = 0;
        var respawns = 0;
        var pickups = 0;

        simulation.Crashed = (_, _, _) => crashes++;
        simulation.Wrecked = (_, _) => wrecks++;
        simulation.Respawned = _ => respawns++;
        simulation.PickupTaken = (_, _, _, _) => pickups++;

        var spawns = arena.Drivers.Select(driver => driver.Spawn).Prepend(arena.PlayerSpawn).ToList();

        foreach (var spawn in spawns)
            Assert.IsTrue(simulation.ArenaRadius(spawn.Point) < 1f, $"The spawn at {spawn.Point} is outside the arena.");

        for (var i = 0; i < spawns.Count; i++)
            simulation.Add(new DerbyCar((ulong)(i + 1), new VehicleConfig([]), spawns[i].Point, spawns[i].Heading));

        simulation.PlacePickups();

        Assert.AreEqual(arena.Pickups.Count, simulation.Pickups.Count(pickup => pickup.Active));

        const int tickMs = 50;

        for (long now = 1; now <= (long)(arena.TimeLimitSeconds * 1000f); now += tickMs)
        {
            simulation.Update(now, tickMs / 1000f);

            foreach (var car in simulation.Cars)
            {
                Assert.IsFalse(float.IsNaN(car.Position.X) || float.IsNaN(car.Position.Y) || float.IsNaN(car.Position.Z), $"Car {car.Guid} at {now} ms.");
                Assert.IsTrue(simulation.ArenaRadius(car.Position) <= 1.001f, $"Car {car.Guid} left the arena at {car.Position}.");
                Assert.IsTrue(car.Health is >= 0 and <= DerbyRules.MaxHealth);

                if (now > 2000 && terrain.TryGetGroundHeight(car.Position.X, car.Position.Z, car.Position.Y + 0.1f, 2f, out var ground))
                    Assert.AreEqual(ground + DerbyRules.RideHeight, car.Position.Y, 0.05f, $"Car {car.Guid} off the floor at {car.Position}.");
            }
        }

        // The live match had 8 wrecks in 110 seconds.
        Assert.IsTrue(crashes > 20, $"Only {crashes} crashes.");
        Assert.IsTrue(wrecks >= 4, $"Only {wrecks} wrecks.");
        Assert.IsTrue(respawns >= wrecks - simulation.Cars.Count, "Wrecked cars didn't come back.");
        Assert.IsTrue(pickups > 0, "Nobody took a box.");
        Assert.AreEqual(wrecks, simulation.Cars.Sum(car => car.Wrecks), "Every wreck credited.");
        Assert.IsTrue(simulation.Cars.Sum(car => car.Score) > 0);
    }

    [TestMethod]
    public void Payout_GoesDownTheOrder()
    {
        var stage = new VehicleGameStageDefinition { Kind = VehicleGameKind.Derby, Stars = 120, Coins = 20 };

        Assert.AreEqual((120, 20), ArenaRules.Payout(stage, 0, won: true));
        Assert.AreEqual((90, 12), ArenaRules.Payout(stage, 1, won: false));
        Assert.AreEqual((30, 0), ArenaRules.Payout(stage, 7, won: false));
    }

    private static DerbyCar Car(ulong guid) => new(guid, new VehicleConfig([]), Vector3.Zero, 0f);

    internal static VehicleGameCollection LoadGames()
    {
        var resources = FindSourceResources();
        var games = new VehicleGameCollection(NullLogger.Instance);

        Assert.IsTrue(games.Load(Path.Combine(resources, "VehicleGames.json"), Path.Combine(resources, "VehicleConfigs.json")));

        return games;
    }

    internal static string FindSourceResources()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Resources", "VehicleGames.json")))
            directory = directory.Parent;

        Assert.IsNotNull(directory, "src/Resources/VehicleGames.json not found above the test output.");

        return Path.Combine(directory.FullName, "Resources");
    }
}
