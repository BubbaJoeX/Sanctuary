using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Text.RegularExpressions;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sanctuary.Core.IO;
using Sanctuary.Game.Fishing;
using Sanctuary.Game.Resources;
using Sanctuary.Game.Resources.Definitions;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game.Tests;

[TestClass]
public sealed class FishingTests
{
    private static string GetResourcesDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Resources", "Fishing.json")))
            directory = directory.Parent;

        Assert.IsNotNull(directory, "Could not find the Resources directory.");

        return Path.Combine(directory.FullName, "Resources");
    }

    private static FishingCollection LoadFishing()
    {
        var fishing = new FishingCollection(NullLogger.Instance);
        Assert.IsTrue(fishing.Load(Path.Combine(GetResourcesDirectory(), "Fishing.json")));
        return fishing;
    }

    private static Dictionary<int, JsonElement> LoadById(string fileName)
    {
        using var stream = File.OpenRead(Path.Combine(GetResourcesDirectory(), fileName));
        using var document = JsonDocument.Parse(stream);

        return document.RootElement.EnumerateArray().ToDictionary(x => x.GetProperty("Id").GetInt32(), x => x.Clone());
    }

    private static HashSet<int> LoadModelIds() =>
        File.ReadLines(Path.Combine(GetResourcesDirectory(), "Models.txt"))
            .Where(line => line.Length > 0 && char.IsDigit(line[0]))
            .Select(line => int.Parse(line.Split('^')[0]))
            .ToHashSet();

    #region Data

    [TestMethod]
    public void Fishing_ItemsAreTheCatchesTheyName()
    {
        var fishing = LoadFishing();
        var items = LoadById("ClientItemDefinitions.json");

        foreach (var fish in fishing.Definition.Fish)
        {
            foreach (var (size, itemId) in fish.Items)
            {
                Assert.IsTrue(items.TryGetValue(itemId, out var item), $"{fish.Comment}: item {itemId} doesn't exist.");

                var name = item.GetProperty("Comment").GetString()!;

                // The legends come in one size and their items carry no size in the name.
                if (fish.Rarity < 4)
                    Assert.IsTrue(name.EndsWith(fish.Comment!) && name.StartsWith(size == FishSize.ExtraLarge ? "Extra Large" : size.ToString()),
                        $"{fish.Comment} {size} is item {itemId}, \"{name}\".");

                Assert.AreEqual(fish.IconId, item.GetProperty("Icon").GetProperty("Id").GetInt32(), $"{name}'s icon");
            }
        }

        foreach (var junk in fishing.Definition.Junk)
            Assert.IsTrue(items[junk.ItemId].TryGetProperty("Icon", out _), $"Junk item {junk.ItemId} has no icon.");

        foreach (var rod in fishing.Definition.Rods)
            Assert.AreEqual(rod.BobberModelId, items[rod.ItemId].GetProperty("Param1").GetInt32(), $"{rod.Comment}'s bobber");

        Assert.IsNotNull(fishing.GetRod(items[fishing.Definition.StarterRodItemId].GetProperty("Param1").GetInt32()), "The starter rod isn't a rod.");
    }

    [TestMethod]
    public void Fishing_EveryLureTemptsThreeFishAndIsUsable()
    {
        var fishing = LoadFishing();
        var items = LoadById("ClientItemDefinitions.json");

        var abilities = new ItemAbilityDefinitionCollection(NullLogger.Instance);
        Assert.IsTrue(abilities.Load(Path.Combine(GetResourcesDirectory(), "ItemAbilities.json")));

        var lures = fishing.Definition.Fish.Where(fish => fish.LureItemId != 0).GroupBy(fish => fish.LureItemId).ToList();

        Assert.AreEqual(12, lures.Count);

        foreach (var lure in lures)
        {
            // One fish of each region, as each lure's description lists them.
            CollectionAssert.AreEquivalent(Enum.GetValues<FishingRegion>(), lure.Select(fish => fish.Region!.Value).ToArray(), $"Lure {lure.Key}");
        }

        // The Treasure Magnet (68164) tempts no fish but is used the same way.
        foreach (var lureItemId in lures.Select(lure => lure.Key).Append(68164))
        {
            var abilityId = items[lureItemId].GetProperty("ActivatableAbilityId").GetInt32();

            Assert.IsTrue(abilities.TryGetValue(abilityId, out var ability), $"Lure {lureItemId} has no item ability.");
            Assert.AreEqual("FishingLure", ability.Group);
            Assert.IsTrue(ability.DurationMs > 0);
        }
    }

    [TestMethod]
    public void Fishing_ModelsAreInModelsTxt()
    {
        // The client looks these up without a check: an unknown one crashes it.
        var fishing = LoadFishing();
        var models = LoadModelIds();

        var used = fishing.Definition.Fish.Select(fish => fish.ModelId)
            .Concat(fishing.Definition.Rods.Select(rod => rod.BobberModelId))
            .Concat(fishing.Ponds.SelectMany(pond => pond.Schools).Select(school => school.ModelId))
            .Concat(Enum.GetValues<FishSize>().Select(FishingRules.CaughtFishModelId))
            .Concat([FishingRules.TreasureChestModelId, FishingRules.CaughtJunkModelId, FishingRules.LureModelId, FishingRules.StarterBobberModelId]);

        foreach (var modelId in used)
            Assert.IsTrue(models.Contains(modelId), $"Model {modelId} isn't in Models.txt.");
    }

    [TestMethod]
    public void Fishing_PondsAreZonesWithWaysInAndOut()
    {
        var fishing = LoadFishing();
        var resourcesDirectory = GetResourcesDirectory();
        var npcs = LoadById("Npcs.json");

        var fabledRealmsScript = File.ReadAllText(Path.Combine(resourcesDirectory, "..", "Scripts", "Zone", "FabledRealms.lua"));

        Assert.AreEqual(6, fishing.Ponds.Count);
        Assert.IsTrue(npcs.ContainsKey(fishing.Definition.GuideNpcId), "The pond guide isn't in Npcs.json.");

        foreach (var pond in fishing.Ponds)
        {
            var zoneFile = Path.Combine(resourcesDirectory, "Zones", pond.Zone + ".json");
            Assert.IsTrue(File.Exists(zoneFile), $"{pond.Zone} has no zone definition.");

            using (var zone = JsonDocument.Parse(File.ReadAllText(zoneFile)))
            {
                Assert.AreEqual("World", zone.RootElement.GetProperty("$type").GetString());
                Assert.AreEqual(pond.ZoneId, zone.RootElement.GetProperty("Id").GetInt32(), pond.Zone);
                Assert.AreEqual(pond.Zone, zone.RootElement.GetProperty("Name").GetString());
            }

            // Its fishing hole is a Fabled Realms NPC the zone script places.
            Assert.IsTrue(npcs.ContainsKey(pond.HoleNpcId), $"{pond.Zone}'s fishing hole {pond.HoleNpcId} isn't in Npcs.json.");
            Assert.IsTrue(Regex.IsMatch(fabledRealmsScript, $@"spawnNpcWithGuid\({pond.HoleNpcId},"), $"FabledRealms.lua doesn't place fishing hole {pond.HoleNpcId}.");

            // Every fish of the pond's region swims in one of its schools; the Fish Finder points there.
            var regional = fishing.Definition.Fish.Where(fish => fish.Region == pond.Region).Select(fish => fish.Id).ToHashSet();
            var schooled = pond.Schools.SelectMany(school => school.Fish).ToHashSet();

            Assert.IsTrue(regional.SetEquals(schooled), $"{pond.Zone}'s schools don't hold its region's fish.");
            Assert.IsTrue(regional.IsSubsetOf(pond.Fish));

            // Schools sit on the water, away from the underwater view the client keeps at z 485.
            foreach (var school in pond.Schools)
            {
                Assert.AreEqual(pond.WaterHeight, school.Position[1], 0.01f);
                Assert.IsFalse(school.Position[0] < 160 && school.Position[2] is > 420 and < 560, $"{pond.Zone} school {school.Id} is in the underwater view.");
            }

            // Players arrive within a cast of a school.
            var arrival = new Vector2(pond.Arrival.Position[0], pond.Arrival.Position[2]);
            var nearest = pond.Schools.Min(school => Vector2.Distance(arrival, new Vector2(school.Position[0], school.Position[2])));
            Assert.IsTrue(nearest <= fishing.Definition.Rods.Min(rod => rod.MaxCastDistance) + FishingRules.SchoolRadius, $"{pond.Zone}'s nearest school is {nearest:0} m out.");

            // The way out stands beside them: the fisherman's Leave, as the client's fishing screen has none.
            var guide = Vector2.Distance(arrival, new Vector2(pond.Guide.Position[0], pond.Guide.Position[2]));
            Assert.IsTrue(guide <= 4f, $"{pond.Zone}'s fisherman is {guide:0.0} m from where players arrive.");
        }
    }

    [TestMethod]
    public void Fishing_GamesWindowEntriesLeadToPonds()
    {
        var fishing = LoadFishing();

        // The client's seven fishing entries (MiniGameData.txt type 21): Learn to Fish! and one per pond.
        CollectionAssert.AreEquivalent(new[] { 595, 596, 597, 598, 599, 600, 601 },
            fishing.Ponds.SelectMany(pond => pond.Activities).Select(activity => activity.Id).ToArray());

        foreach (var pond in fishing.Ponds)
        {
            Assert.IsTrue(pond.Activities.Count > 0, $"{pond.Zone} isn't in the Games window.");

            foreach (var activity in pond.Activities)
            {
                Assert.IsTrue(fishing.TryGetPondByActivity(activity.Id, out var found) && found == pond);
                Assert.IsTrue(activity.NameId > 0 && activity.DescriptionId > 0);
            }
        }
    }

    [TestMethod]
    public void Fishing_TreasurePaysFromRewardTables()
    {
        var fishing = LoadFishing();

        using var stream = File.OpenRead(Path.Combine(GetResourcesDirectory(), "Rewards.json"));
        using var document = JsonDocument.Parse(stream);

        var tables = document.RootElement.EnumerateArray().Select(x => x.GetProperty("Key").GetString()).ToHashSet();

        foreach (var treasure in fishing.Definition.Treasure)
            Assert.IsTrue(tables.Contains(treasure.RewardTable), $"{treasure.Comment}'s reward table {treasure.RewardTable} isn't in Rewards.json.");
    }

    [TestMethod]
    public void Fishing_Validate_RejectsBrokenData()
    {
        var fishing = LoadFishing();
        var definition = JsonSerializer.Deserialize<FishingDefinition>(
            File.ReadAllText(Path.Combine(GetResourcesDirectory(), "Fishing.json")),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true, Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } })!;

        Assert.IsNull(FishingCollection.Validate(definition));

        definition.Ponds[0].Schools[0].Fish.Add(999);
        Assert.IsNotNull(FishingCollection.Validate(definition));

        definition.Ponds[0].Schools[0].Fish.Remove(999);
        definition.Fish.Add(definition.Fish[0]);
        Assert.IsNotNull(FishingCollection.Validate(definition));

        // A file that fails keeps what was loaded.
        Assert.IsTrue(fishing.TryGetPond("sg_fishing_medpond", out _));
    }

    #endregion

    #region Rules

    private static FishDefinition Fish(int id, int rarity, int lure = 0, params FishSize[] sizes) => new()
    {
        Id = id,
        Comment = $"Fish {id}",
        NameId = 1,
        ModelId = 1671,
        Rarity = rarity,
        LureItemId = lure,
        Items = (sizes.Length > 0 ? sizes : [FishSize.Small, FishSize.Medium]).ToDictionary(size => size, size => 1000 * id + (int)size)
    };

    private static Dictionary<FishingCatchKind, int> Count(FishingCastContext context, int casts, Func<FishingCatch, bool>? fish = null)
    {
        var random = new Random(1234);
        var counts = Enum.GetValues<FishingCatchKind>().ToDictionary(kind => kind, _ => 0);

        for (var i = 0; i < casts; i++)
        {
            var fishingCatch = FishingRules.Roll(context, random);

            if (fish is null || fish(fishingCatch))
                counts[fishingCatch.Kind]++;
        }

        return counts;
    }

    [TestMethod]
    public void Roll_SchoolsAndLuresBringUpTheirFish()
    {
        List<FishDefinition> pond = [Fish(1, 1, 500), Fish(2, 1), Fish(3, 1), Fish(4, 2), Fish(5, 3)];

        const int casts = 20000;

        double Share(FishingCastContext context, int fishId) =>
            Count(context, casts, fishingCatch => fishingCatch.Fish?.Id == fishId)[FishingCatchKind.Fish] / (double)casts;

        var open = new FishingCastContext { PondFish = pond };
        var openShare = Share(open, 2);

        // Casting onto a school brings its fish up far more often.
        Assert.IsTrue(Share(new FishingCastContext { PondFish = pond, SchoolFish = [2] }, 2) > openShare * 2);

        // A lure adds about a tenth of all casts to the fish it tempts.
        var lured = Share(new FishingCastContext { PondFish = pond, LureItemId = 500 }, 1);
        var unlured = Share(open, 1);

        Assert.AreEqual(FishingRules.LureBonus, lured - unlured, 0.03);

        // And the Treasure Magnet to treasure.
        List<FishingTreasureDefinition> treasure = [new() { RewardTable = "t", NameId = 1, Weight = 1, Size = FishSize.Small }];

        var plain = Count(new FishingCastContext { PondFish = pond, Treasure = treasure }, casts)[FishingCatchKind.Treasure];
        var magnet = Count(new FishingCastContext { PondFish = pond, Treasure = treasure, TreasureLure = true }, casts)[FishingCatchKind.Treasure];

        Assert.AreEqual(FishingRules.LureBonus, (magnet - plain) / (double)casts, 0.03);
    }

    [TestMethod]
    public void Roll_OpenWaterSometimesBringsUpNothingOrJunk()
    {
        List<FishDefinition> pond = [Fish(1, 1), Fish(2, 2)];
        List<FishingJunkDefinition> junk = [new() { ItemId = 7 }];

        var counts = Count(new FishingCastContext { PondFish = pond, Junk = junk }, 10000);

        Assert.IsTrue(counts[FishingCatchKind.Fish] > counts[FishingCatchKind.Junk]);
        Assert.IsTrue(counts[FishingCatchKind.Nothing] > 0);
        Assert.IsTrue(counts[FishingCatchKind.Junk] > 0);

        var onSchool = Count(new FishingCastContext { PondFish = pond, Junk = junk, SchoolFish = [1] }, 10000);

        Assert.IsTrue(onSchool[FishingCatchKind.Nothing] < counts[FishingCatchKind.Nothing]);
    }

    [TestMethod]
    public void RollFish_OnlyTheSizesAFishComesIn_BiggerWithBetterRods()
    {
        var fish = Fish(1, 2, 0, FishSize.Medium, FishSize.Large, FishSize.ExtraLarge);
        var random = new Random(99);

        double MeanSize(int tier)
        {
            var total = 0;

            for (var i = 0; i < 5000; i++)
            {
                var fishingCatch = FishingRules.RollFish(fish, tier, random);

                Assert.IsTrue(fish.Items.ContainsKey(fishingCatch.Size));
                Assert.AreEqual(fish.Items[fishingCatch.Size], fishingCatch.ItemId);

                var (low, high) = fishingCatch.Size switch
                {
                    FishSize.Medium => (2f, 5f),
                    FishSize.Large => (5f, 10f),
                    _ => (10f, 20f)
                };

                Assert.IsTrue(fishingCatch.Weight >= low && fishingCatch.Weight <= high, $"{fishingCatch.Size} weighs {fishingCatch.Weight}");

                total += (int)fishingCatch.Size;
            }

            return total / 5000d;
        }

        Assert.IsTrue(MeanSize(4) > MeanSize(0) + 0.15);

        var single = Fish(2, 4, 0, FishSize.ExtraLarge);
        Assert.AreEqual(FishSize.ExtraLarge, FishingRules.RollFish(single, 0, random).Size);
    }

    [TestMethod]
    public void Timing_RareFishAreHarder_BetterRodsHelp()
    {
        Assert.IsTrue(FishingRules.BiteWindowMs(4, 0) < FishingRules.BiteWindowMs(1, 0));
        Assert.IsTrue(FishingRules.BiteWindowMs(4, 4) > FishingRules.BiteWindowMs(4, 0));
        Assert.IsTrue(FishingRules.ReelMs(4, 0) > FishingRules.ReelMs(1, 0));
        Assert.IsTrue(FishingRules.ReelMs(1, 4) >= 1000);

        Assert.AreEqual(0, FishingRules.ReelEscapeChance(1, 0));
        Assert.IsTrue(FishingRules.ReelEscapeChance(4, 0) > FishingRules.ReelEscapeChance(3, 0));
        Assert.IsTrue(FishingRules.ReelEscapeChance(4, 4) < FishingRules.ReelEscapeChance(4, 0));

        // A legendary in a big size earns more than a small common fish.
        var small = new FishingCatch(FishingCatchKind.Fish, Fish(1, 1), FishSize.Small, 1f);
        var legend = new FishingCatch(FishingCatchKind.Fish, Fish(2, 4), FishSize.ExtraLarge, 25f);

        Assert.IsTrue(FishingRules.Stars(legend) > FishingRules.Stars(small));
        Assert.IsTrue(FishingRules.Score(legend) > FishingRules.Score(small));
    }

    #endregion

    #region Packets

    [TestMethod]
    public void ClientRequests_Deserialize()
    {
        static byte[] Packet(ushort subOpCode, Action<PacketWriter> body)
        {
            using var writer = new PacketWriter();
            writer.Write(BaseFishingPacket.OpCode);
            writer.Write(subOpCode);
            body(writer);
            return writer.Buffer;
        }

        var register = Packet(FishingPacketRegisterPlayerRequest.OpCode, writer =>
        {
            writer.Write(0x1122334455667788UL);
            writer.Write((byte)0xCD); // never set by the client
        });

        Assert.IsTrue(FishingPacketRegisterPlayerRequest.TryDeserialize(register, out var registerPacket));
        Assert.AreEqual(0x1122334455667788UL, registerPacket.Guid);
        Assert.IsFalse(FishingPacketRegisterPlayerRequest.TryDeserialize(register[..^1], out _));

        var cast = Packet(FishingPacketCastRequest.OpCode, writer =>
        {
            writer.Write(42UL);
            writer.Write(new Vector4(1f, -3f, 2f, 1f));
            writer.Write(true);
        });

        Assert.IsTrue(FishingPacketCastRequest.TryDeserialize(cast, out var castPacket));
        Assert.AreEqual(new Vector4(1f, -3f, 2f, 1f), castPacket.Target);

        // The sub-op is a short: a cast packet isn't a reel.
        Assert.IsFalse(FishingPacketReelInRequest.TryDeserialize(cast, out _));

        var castAnim = Packet(FishingPacketCastAnimRequest.OpCode, writer =>
        {
            writer.Write(42UL);
            writer.Write(12.5f);
            writer.Write(new Vector4(3f, 0f, 4f, 0f));
        });

        Assert.IsTrue(FishingPacketCastAnimRequest.TryDeserialize(castAnim, out var castAnimPacket));
        Assert.AreEqual(12.5f, castAnimPacket.Distance);

        // Sent on to other players, it's written back the same way.
        CollectionAssert.AreEqual(castAnim, castAnimPacket.Serialize());

        var reel = Packet(FishingPacketReelInRequest.OpCode, writer =>
        {
            writer.Write(42UL);
            writer.Write(true);
        });

        Assert.IsTrue(FishingPacketReelInRequest.TryDeserialize(reel, out var reelPacket));
        Assert.IsTrue(reelPacket.Reeling);
    }

    [TestMethod]
    public void FishingResult_WritesTheClientsFieldOrder()
    {
        var result = new FishingPacketFishingResult
        {
            Guid = 7,
            Type = FishingResultType.Caught,
            NameId = 99942,
            IconId = 4282,
            Weight = 1.5f,
            Size = 2,
            CaughtModelId = 1621,
            CatchCompositeEffectId = 15880,
            ResetToIdle = true,
            DisplayScale = 2
        }.Serialize();

        var reader = new PacketReader(result);

        Assert.IsTrue(reader.TryRead(out short opCode) && opCode == BaseFishingPacket.OpCode);
        Assert.IsTrue(reader.TryRead(out ushort subOpCode) && subOpCode == FishingPacketFishingResult.OpCode);
        Assert.IsTrue(reader.TryRead(out ulong guid) && guid == 7);
        Assert.IsTrue(reader.TryRead(out int type) && type == 5);
        Assert.IsTrue(reader.TryRead(out bool isItem) && !isItem);
        Assert.IsTrue(reader.TryRead(out int nameId) && nameId == 99942);
        Assert.IsTrue(reader.TryRead(out int iconId) && iconId == 4282);
        Assert.IsTrue(reader.TryRead(out string unknown) && unknown.Length == 0);
        Assert.IsTrue(reader.TryRead(out float weight) && weight == 1.5f);
        Assert.IsTrue(reader.TryRead(out int size) && size == 2);
        reader.Advance(4 * 4); // time to catch, difficulty, rarity, score
        Assert.IsTrue(reader.TryRead(out int caughtModelId) && caughtModelId == 1621);
        Assert.IsTrue(reader.TryRead(out string _) && reader.TryRead(out string _));
        Assert.IsTrue(reader.TryRead(out int effectId) && effectId == 15880);

        // The last three go in a different order on the wire than in the client's struct.
        Assert.IsTrue(reader.TryRead(out float _));
        Assert.IsTrue(reader.TryRead(out bool resetToIdle) && resetToIdle);
        Assert.IsTrue(reader.TryRead(out int displayScale) && displayScale == 2);
        Assert.AreEqual(0, reader.RemainingLength);
    }

    [TestMethod]
    public void FishRun_WritesElevenSwimParametersPerFish()
    {
        var run = new FishingPacketSpawnFishRun
        {
            LureModelId = FishingRules.LureModelId,
            Fish = [new UnderwaterFishSpawnInfo { Id = 1, ModelId = 1671, SizeClass = 2 }]
        }.Serialize();

        // opcode, sub-op, bool, lure, empty alias, count; then id, model, two empty aliases, size, float, bool, 11 floats.
        Assert.AreEqual(2 + 2 + 1 + 4 + 4 + 4 + (4 + 4 + 4 + 4 + 4 + 4 + 1 + 11 * 4), run.Length);

        var registerResponse = new FishingPacketRegisterPlayerResponse().Serialize();

        // 16 player config fields, then the zone config (an empty sky string and 5 more), then two empty lists.
        Assert.AreEqual(2 + 2 + 16 * 4 + (4 + 5 * 4) + 4 + 4, registerResponse.Length);
    }

    #endregion
}
