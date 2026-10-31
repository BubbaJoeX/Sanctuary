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

using Sanctuary.Game.Entities;
using Sanctuary.Game.Resources;
using Sanctuary.Game.Resources.Definitions;
using Sanctuary.Game.Resources.Definitions.Zones;
using Sanctuary.Game.Zones;

namespace Sanctuary.Game.Tests;

/// <summary>
/// The client's combat area (EncounterOverworldCombatPacket), which live sent per area: 0 in towns, 1 in the wilds.
/// In one, the job picker won't let a combat job go for another kind; out of one, hostile NPCs aren't red.
/// </summary>
[TestClass]
public sealed class CombatAreaTests
{
    private const int FabledRealmsZoneId = 1;

    // Snowhill, Briarwood, Sanctuary, Shrouded Glade, Wugachug, Seaside, Merry Vale, Blackspore and Lavender Coast.
    private static readonly int[] TownPointOfInterestIds = [3, 4, 5, 8, 12, 13, 14, 16, 200];

    // Greenwood Forest, Lakeshore, Lavender Coast Pass, Highroad Junction, Stillwater Crossing and Sunstone Valley.
    private static readonly int[] WildsPointOfInterestIds = [6, 9, 15, 17, 18, 152];

    private static readonly string[] SpawningScripts = ["FabledRealms.lua", "Battles.lua", "lavendercoast.lua"];

    private static readonly Regex SpawnNpc = new(@"spawnNpc(?:WithGuid)?\((\d+),(?:\s*\d+,)?\s*(-?[\d.]+),\s*(-?[\d.]+),\s*(-?[\d.]+)");

    private static string GetResourcesDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Resources", "Zones", "FabledRealms.json")))
            directory = directory.Parent;

        Assert.IsNotNull(directory, "Could not find the Resources directory.");

        return Path.Combine(directory.FullName, "Resources");
    }

    private sealed class FabledRealms
    {
        private readonly WorldZoneDefinition _definition;
        private readonly ZoneEnvironmentDefinition _environment;

        public ZoneAreaMap Map { get; }

        public FabledRealms()
        {
            var resourcesDirectory = GetResourcesDirectory();

            var zones = new ZoneDefinitionCollection(NullLogger.Instance);
            Assert.IsTrue(zones.Load(Path.Combine(resourcesDirectory, "Zones")));

            var environments = new ZoneEnvironmentCollection(NullLogger.Instance);
            Assert.IsTrue(environments.Load(Path.Combine(resourcesDirectory, "Zones", "Environments")));

            _definition = (WorldZoneDefinition)zones[FabledRealmsZoneId];
            _environment = environments[_definition.Name];

            Map = new ZoneAreaMap(_definition);
        }

        /// <summary>
        /// As <see cref="BaseZone.GetTileFromPosition"/> finds it.
        /// </summary>
        private ZoneTile GetTile(Vector4 position)
        {
            var latitude = (int)Math.Floor(position.X / _definition.TileSize);
            var longitude = (int)Math.Floor(position.Z / _definition.TileSize);

            return new ZoneTile(longitude, latitude, _environment.GetEnvironmentId(longitude, latitude));
        }

        public ZoneAreaDefinition? GetZoneArea(Vector4 position) => Map.GetZoneArea(position, GetTile(position));

        public bool IsCombatArea(float x, float z)
        {
            var position = new Vector4(x, 0f, z, 1f);

            return Map.IsCombatArea(position, GetZoneArea(position));
        }
    }

    private static Dictionary<int, JsonElement> LoadById(string fileName)
    {
        using var stream = File.OpenRead(Path.Combine(GetResourcesDirectory(), fileName));
        using var document = JsonDocument.Parse(stream);

        return document.RootElement.EnumerateArray().ToDictionary(x => x.GetProperty("Id").GetInt32(), x => x.Clone());
    }

    private static (float X, float Z) GetPointOfInterestPosition(Dictionary<int, JsonElement> pointOfInterests, int id)
    {
        var position = pointOfInterests[id].GetProperty("Position");

        return (position[0].GetSingle(), position[2].GetSingle());
    }

    [TestMethod]
    public void Towns_AreNotCombatAreas()
    {
        var fabledRealms = new FabledRealms();
        var pointOfInterests = LoadById("PointOfInterests.json");

        foreach (var id in TownPointOfInterestIds)
        {
            var (x, z) = GetPointOfInterestPosition(pointOfInterests, id);

            Assert.IsFalse(fabledRealms.IsCombatArea(x, z), $"{pointOfInterests[id].GetProperty("Comment")} is a combat area, so a combat job can't pick another job there.");
        }
    }

    [TestMethod]
    public void Wilds_AreCombatAreas()
    {
        var fabledRealms = new FabledRealms();
        var pointOfInterests = LoadById("PointOfInterests.json");

        foreach (var id in WildsPointOfInterestIds)
        {
            var (x, z) = GetPointOfInterestPosition(pointOfInterests, id);

            Assert.IsTrue(fabledRealms.IsCombatArea(x, z), $"{pointOfInterests[id].GetProperty("Comment")} isn't a combat area.");
        }
    }

    [TestMethod]
    public void LiveCaptures_MatchTheirCombatAreas()
    {
        var fabledRealms = new FabledRealms();

        // Where live sent 0 (Snowhill, on arriving) and 1 (Sunstone Valley and Lavender Coast Pass, after logging in).
        Assert.IsFalse(fabledRealms.IsCombatArea(129.7f, 296.8f));
        Assert.IsTrue(fabledRealms.IsCombatArea(908.8f, 2234.1f));
        Assert.IsTrue(fabledRealms.IsCombatArea(-2125.4f, -292.1f));
    }

    [TestMethod]
    public void HostileNpcs_StandInCombatAreas()
    {
        // Out of a combat area the client shows no enemy as one, so every hostile NPC the zone places needs one: an area
        // marked IsCombatArea, or one of the zone's CombatAreas around it in a town.
        var fabledRealms = new FabledRealms();
        var npcs = LoadById("Npcs.json");
        var scriptsDirectory = Path.Combine(GetResourcesDirectory(), "..", "Scripts", "Zone");
        var hostileCount = 0;

        foreach (var script in SpawningScripts)
        {
            foreach (Match match in SpawnNpc.Matches(File.ReadAllText(Path.Combine(scriptsDirectory, script))))
            {
                var npcId = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);

                if (!npcs.TryGetValue(npcId, out var npc) || !npc.TryGetProperty("Disposition", out var disposition) || disposition.GetInt32() != Npc.HostileDisposition)
                    continue;

                var x = float.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
                var z = float.Parse(match.Groups[4].Value, CultureInfo.InvariantCulture);

                hostileCount++;

                Assert.IsTrue(fabledRealms.IsCombatArea(x, z), $"{script} places hostile NPC {npcId} at {x}, {z} outside every combat area.");
            }
        }

        Assert.IsTrue(hostileCount > 100, "No hostile NPCs found in the zone scripts.");
    }

    [TestMethod]
    public void WildsAreas_AreTheCombatAreas()
    {
        var resourcesDirectory = GetResourcesDirectory();

        var zones = new ZoneDefinitionCollection(NullLogger.Instance);
        Assert.IsTrue(zones.Load(Path.Combine(resourcesDirectory, "Zones")));

        var combatAreas = ((WorldZoneDefinition)zones[FabledRealmsZoneId]).AreaDefinitions
            .Where(x => x.IsCombatArea)
            .Select(x => x.Id)
            .Order()
            .ToArray();

        // Sunstone Valley and the north and south Sacred Grove, where the open world's enemies roam.
        CollectionAssert.AreEqual(new[] { 10, 11, 12 }, combatAreas);

        // A 3D game, a fishing pond and a battle's own zone have no areas of their own.
        foreach (var zone in zones.Values.OfType<WorldZoneDefinition>().Where(x => x.Id != FabledRealmsZoneId))
            Assert.IsFalse(zone.AreaDefinitions.Any(x => x.IsCombatArea) || zone.CombatAreas.Any(x => x.IsCombatArea), zone.Name);
    }

    [TestMethod]
    public void ZoneCombatAreas_OverrideTheAreaTheyAreIn()
    {
        var town = new ZoneAreaDefinition { Id = 1 };
        var wilds = new ZoneAreaDefinition { Id = 2, IsCombatArea = true };

        var map = new ZoneAreaMap(new WorldZoneDefinition
        {
            Name = "test",
            AreaDefinitions = [town, wilds],
            CombatAreas =
            [
                new ZoneCombatAreaDefinition { Shape = ZoneAreaShape.Circle, X1 = 100f, Z1 = 100f, Radius = 20f },
                new ZoneCombatAreaDefinition { Shape = ZoneAreaShape.Rectangle, X1 = -50f, Z1 = -50f, X2 = -30f, Z2 = -30f, IsCombatArea = false }
            ]
        });

        Assert.IsFalse(map.IsCombatArea(new Vector4(0f, 0f, 0f, 1f), town));
        Assert.IsTrue(map.IsCombatArea(new Vector4(110f, 0f, 100f, 1f), town));
        Assert.IsFalse(map.IsCombatArea(new Vector4(125f, 0f, 100f, 1f), town));
        Assert.IsTrue(map.IsCombatArea(new Vector4(0f, 0f, 0f, 1f), wilds));
        Assert.IsFalse(map.IsCombatArea(new Vector4(-40f, 0f, -40f, 1f), wilds));
        Assert.IsTrue(map.IsCombatArea(new Vector4(110f, 0f, 100f, 1f), null));
        Assert.IsFalse(map.IsCombatArea(new Vector4(0f, 0f, 0f, 1f), null));
    }
}
