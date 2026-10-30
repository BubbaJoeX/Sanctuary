using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Text.RegularExpressions;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sanctuary.Core.Extensions;
using Sanctuary.Core.IO;
using Sanctuary.Game.Challenges;
using Sanctuary.Game.Resources;
using Sanctuary.Game.Resources.Definitions;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game.Tests;

[TestClass]
public sealed class ChallengeTests
{
    private static string GetResourcesDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Resources", "Challenges.json")))
            directory = directory.Parent;

        Assert.IsNotNull(directory, "Could not find the Resources directory.");

        return Path.Combine(directory.FullName, "Resources");
    }

    private static Dictionary<int, JsonElement> LoadById(string fileName)
    {
        using var stream = File.OpenRead(Path.Combine(GetResourcesDirectory(), fileName));
        using var document = JsonDocument.Parse(stream);

        return document.RootElement.EnumerateArray().ToDictionary(x => x.GetProperty("Id").GetInt32(), x => x.Clone());
    }

    [TestMethod]
    public void Challenges_AreOfferedByPeopleInFabledRealms()
    {
        var challenges = new ChallengeDefinitionCollection(NullLogger.Instance);
        Assert.IsTrue(challenges.Load(Path.Combine(GetResourcesDirectory(), "Challenges.json")));
        Assert.IsTrue(challenges.Count >= 25);

        var npcs = LoadById("Npcs.json");
        var profiles = LoadById("Profiles.json");

        var models = File.ReadLines(Path.Combine(GetResourcesDirectory(), "Models.txt"))
            .Where(line => line.Length > 0 && char.IsDigit(line[0]))
            .Select(line => int.Parse(line.Split('^')[0]))
            .ToHashSet();

        var placed = Regex.Matches(File.ReadAllText(Path.Combine(GetResourcesDirectory(), "..", "Scripts", "Zone", "FabledRealms.lua")),
                @"spawnNpcWithGuid\((\d+),")
            .Select(match => int.Parse(match.Groups[1].Value))
            .ToHashSet();

        foreach (var challenge in challenges.Values)
        {
            Assert.IsTrue(npcs.ContainsKey(challenge.GiverNpcId) && placed.Contains(challenge.GiverNpcId),
                $"Challenge {challenge.Id}'s NPC {challenge.GiverNpcId} isn't in Fabled Realms.");

            foreach (var recipient in challenge.RecipientNpcIds)
                Assert.IsTrue(placed.Contains(recipient), $"Challenge {challenge.Id} delivers to {recipient}, who isn't in Fabled Realms.");

            if (challenge.Kind is ChallengeKind.Collect or ChallengeKind.Return or ChallengeKind.Catch)
                Assert.IsTrue(models.Contains(challenge.ModelId), $"Challenge {challenge.Id}'s model {challenge.ModelId} isn't in Models.txt.");

            if (challenge.ProfileId != 0)
                Assert.IsTrue(profiles.ContainsKey(challenge.ProfileId), $"Challenge {challenge.Id} pays job {challenge.ProfileId}, which doesn't exist.");
        }

        // The race flags.
        Assert.IsTrue(models.Contains(371) && models.Contains(374));
    }

    [TestMethod]
    public void IsValid_RejectsWhatCantBePlayed()
    {
        var race = new ChallengeDefinition { Id = 1, NameId = 1, GiverNpcId = 1, Kind = ChallengeKind.Race, Count = 6 };

        Assert.IsTrue(ChallengeDefinitionCollection.IsValid(race, out _));

        race.Count = 1;
        Assert.IsFalse(ChallengeDefinitionCollection.IsValid(race, out _));

        var collect = new ChallengeDefinition { Id = 1, NameId = 1, GiverNpcId = 1, Kind = ChallengeKind.Collect };
        Assert.IsFalse(ChallengeDefinitionCollection.IsValid(collect, out _), "A collect challenge needs a model.");

        var deliver = new ChallengeDefinition { Id = 1, NameId = 1, GiverNpcId = 1, Kind = ChallengeKind.Deliver, Count = 3, RecipientNpcIds = [5, 6] };
        Assert.IsFalse(ChallengeDefinitionCollection.IsValid(deliver, out _), "It names fewer people than it delivers to.");
    }

    private static Func<Vector3, float, Vector3?> FlatSampler(int seed)
    {
        var random = new Random(seed);

        return (spot, radius) =>
        {
            var angle = random.NextSingle() * MathF.Tau;
            var distance = MathF.Sqrt(random.NextSingle()) * radius;
            return spot + new Vector3(MathF.Sin(angle) * distance, 0f, MathF.Cos(angle) * distance);
        };
    }

    [TestMethod]
    public void Scatter_KeepsThingsApartAndAwayFromTheNpc()
    {
        var center = new Vector3(100f, 5f, -40f);
        var points = ChallengeLayout.Scatter(center, 40f, 10, FlatSampler(7));

        Assert.AreEqual(10, points.Count);

        foreach (var point in points)
        {
            Assert.IsTrue(point.DistanceSquaredXZ(center) >= ChallengeLayout.MinDistanceFromCenter * ChallengeLayout.MinDistanceFromCenter);
            Assert.IsTrue(point.DistanceSquaredXZ(center) <= 40f * 40f + 0.01f);
            Assert.IsTrue(points.Where(other => other != point).All(other => other.DistanceSquaredXZ(point) >= ChallengeLayout.MinSpacing * ChallengeLayout.MinSpacing));
        }

        // With nowhere walkable there's nothing to place.
        Assert.AreEqual(0, ChallengeLayout.Scatter(center, 40f, 10, (_, _) => null).Count);
    }

    [TestMethod]
    public void RaceLoop_GoesOutAndFinishesByTheNpc()
    {
        var center = new Vector3(0f, 0f, 0f);
        var flags = ChallengeLayout.RaceLoop(center, 60f, 8, 0.5f, (spot, _) => spot);

        Assert.AreEqual(8, flags.Count);
        Assert.IsTrue(flags[^1].DistanceSquaredXZ(center) <= 10f * 10f, "The finish is by the NPC.");
        Assert.IsTrue(flags.Take(7).All(flag => flag.DistanceSquaredXZ(center) >= 30f * 30f), "The course goes out.");

        // Consecutive flags go round, not back and forth.
        var angles = flags.Take(7).Select(flag => MathF.Atan2(flag.X, flag.Z)).ToList();
        Assert.AreEqual(7, angles.Distinct().Count());
    }

    [TestMethod]
    public void InfoPacket_KeepsItsLengthWithTheFieldsWhereTheClientReadsThem()
    {
        var info = new MiniGameInfoPacket { StageId = 241, Type = 1, MiniGameId = 241, ShowStartScreen = true, ShowEndScreen = true };
        var bytes = info.Serialize();

        // header (u16, u8, 3 ints), 5 ints and a bool, three empty reward bundles, the empty goal list, 5 bools,
        // an empty string, an int, a bool, the id, 4 bools, an int, the trailer.
        var bundle = new RewardBundleBase { Success = false };
        using var bundleWriter = new PacketWriter();
        bundle.Serialize(bundleWriter);
        var bundleLength = bundleWriter.Buffer.Length;

        Assert.AreEqual(2 + 1 + 12 + 6 * 4 + 1 + 3 * bundleLength + 4 + 5 + 4 + 4 + 1 + 4 + 4 + 4 + 4, bytes.Length);

        // The type is the sixth int of the body: the client reads 1 as an in-world game.
        Assert.AreEqual(1, BitConverter.ToInt32(bytes, 15 + 5 * 4));

        var goalStart = 15 + 6 * 4 + 1 + 3 * bundleLength;
        Assert.AreEqual(0, BitConverter.ToInt32(bytes, goalStart), "No goals.");

        // Straight after the empty list: ShowStarCounter, ShowStatusIcon, ShowActionBar, ShowStartScreen, ShowEndScreen.
        CollectionAssert.AreEqual(new byte[] { 0, 0, 0, 1, 1 }, bytes[(goalStart + 4)..(goalStart + 9)]);

        // The MiniGameData id after the swf string, the int and the bool.
        Assert.AreEqual(241, BitConverter.ToInt32(bytes, goalStart + 9 + 4 + 4 + 1));
    }

    [TestMethod]
    public void HudPackets_MatchTheClient()
    {
        // The star total rides in the header's middle int.
        var stars = new MiniGameStarsEarnedPacket { StageId = 25, Stars = 30 }.Serialize();
        CollectionAssert.AreEqual(new byte[] { 0x27, 0, 0x15, 25, 0, 0, 0, 30, 0, 0, 0, 0xff, 0xff, 0xff, 0xff, 0 }, stars);

        var task = new TaskAddPacket { TaskId = 1, IconId = 2, TextId = 3, Category = 1, Total = 10 }.Serialize();
        CollectionAssert.AreEqual(new byte[] { 47, 0, 1, 1, 0, 0, 0, 2, 0, 0, 0, 3, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 10, 0, 0, 0 }, task);

        CollectionAssert.AreEqual(new byte[] { 47, 0, 10, 120, 0, 0, 0 }, new StartTimerPacket { Seconds = 120 }.Serialize());
        CollectionAssert.AreEqual(new byte[] { 177, 0, 1, 1, 0, 0, 0 }, new PacketClientFlashTimer { Start = true, Type = 1 }.Serialize());
        CollectionAssert.AreEqual(new byte[] { 45, 0, 3, 1, 0, 0, 0, 0, 0, 0, 0, 5, 0, 0, 0 },
            new ObjectiveCompletePacket { ObjectiveId = 1, Points = 5 }.Serialize());
    }
}
