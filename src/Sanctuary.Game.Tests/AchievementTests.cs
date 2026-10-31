using System;
using System.IO;
using System.Linq;
using System.Text.Json;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sanctuary.Core.IO;
using Sanctuary.Game.Achievements;
using Sanctuary.Game.Entities;
using Sanctuary.Game.Resources;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game.Tests;

[TestClass]
public class AchievementTests
{
    // Brawler: Level 16 and Level 20, and Master of Levels' Brawler objective (the live ids).
    private const int BrawlerLevel16 = 66;
    private const int BrawlerLevel20 = 67;
    private const int BrawlerLevel20Objective = 1821;
    private const int MasterOfLevels = 323;
    private const int MasterOfLevelsBrawler = 19052;
    private const int BrawlerProfileId = 43;

    [TestMethod]
    public void Achievements_LoadAndPointAtRealData()
    {
        var resources = FindSourceResources();
        var achievements = Load(resources);

        Assert.AreEqual(321, achievements.Count);

        var profileIds = JsonDocument.Parse(File.ReadAllText(Path.Combine(resources, "Profiles.json"))).RootElement.EnumerateArray()
            .Select(profile => profile.GetProperty("Id").GetInt32())
            .ToHashSet();

        foreach (var (achievement, objective) in achievements.CountedBy(AchievementEvent.JobRank))
            Assert.IsTrue(objective.Trigger!.Keys!.All(profileIds.Contains), $"Achievement {achievement.Id} counts a job that doesn't exist.");

        foreach (var (achievement, objective) in achievements.CountedBy(AchievementEvent.Achievement))
            Assert.IsTrue(objective.Trigger!.Keys!.All(achievements.ContainsKey), $"Achievement {achievement.Id} counts an achievement that doesn't exist.");

        Assert.IsTrue(achievements.CountedBy(AchievementEvent.Kill).Count >= 27);
        Assert.AreEqual(15, achievements.PointsOf(achievements[BrawlerLevel20]));
    }

    [TestMethod]
    public void KillFamilies_MatchTheirOwnMobsOnly()
    {
        var achievements = Load(FindSourceResources());

        var bixies = achievements[1006].Objectives[0];
        var ninjas = achievements[1012].Objectives[0];

        Assert.IsTrue(achievements.CountsKill(bixies, "Bixie Drone", 0));
        Assert.IsFalse(achievements.CountsKill(bixies, "Thugawug Bandit", 0));

        Assert.IsTrue(achievements.CountsKill(ninjas, "Shadow Ninja", 0));
        Assert.IsFalse(achievements.CountsKill(ninjas, "Ghost Ninja", 0));
        Assert.IsFalse(achievements.CountsKill(ninjas, "Ninjawug", 0));
    }

    [TestMethod]
    public void Load_CreditsTheRanksAPlayerAlreadyHas()
    {
        var achievements = Load(FindSourceResources());
        var manager = new AchievementManager(NullLogger.Instance, achievements);
        var player = new Player(null!, null!, null!, null!);

        player.Profiles.Add(new ClientPcProfile { Id = BrawlerProfileId, Rank = 16 });

        manager.Load(player, []);

        Assert.IsTrue(player.Achievements.IsFinished(BrawlerLevel16));
        Assert.IsFalse(player.Achievements.IsFinished(BrawlerLevel20));
        Assert.AreEqual(16, player.Achievements.CountOf(BrawlerLevel20, BrawlerLevel20Objective));

        // A lower rank never lowers the count.
        player.Achievements.Record(AchievementEvent.JobRank, BrawlerProfileId, 3);
        Assert.AreEqual(16, player.Achievements.CountOf(BrawlerLevel20, BrawlerLevel20Objective));

        // Rank 20 finishes it, and that counts toward Master of Levels.
        player.Achievements.Record(AchievementEvent.JobRank, BrawlerProfileId, 20);
        Assert.IsTrue(player.Achievements.IsFinished(BrawlerLevel20));
        Assert.AreEqual(1, player.Achievements.CountOf(MasterOfLevels, MasterOfLevelsBrawler));
        Assert.IsFalse(player.Achievements.IsFinished(MasterOfLevels));

        var started = manager.GetStarted(player, out var points);

        Assert.IsTrue(started.Any(achievement => achievement.Id == MasterOfLevels && achievement.TimeFinished == 0));
        Assert.AreEqual(5 + 5 + 10 + 10 + 15, points, "Brawler levels 4, 8, 12, 16 and 20.");
    }

    [TestMethod]
    public void Counters_AddUpAcrossEvents()
    {
        var achievements = Load(FindSourceResources());
        var manager = new AchievementManager(NullLogger.Instance, achievements);
        var player = new Player(null!, null!, null!, null!);

        manager.Load(player, []);

        // Cash Cow: 500,000 coins through rewards.
        player.Achievements.Record(AchievementEvent.RewardCoins, 0, 22);
        player.Achievements.Record(AchievementEvent.RewardCoins, 0, 12);

        Assert.AreEqual(34, player.Achievements.CountOf(228, 18768));
        Assert.IsFalse(player.Achievements.IsFinished(228));

        // Tricky: one pet trick.
        player.Achievements.Record(AchievementEvent.PetTrick);
        Assert.IsTrue(player.Achievements.IsFinished(240));
        Assert.AreEqual(1, player.Achievements.CountOf(241, 18789));
    }

    [TestMethod]
    public void ClientAchievement_MatchesTheLiveBytes()
    {
        // p1.pcap, the first record of the live AchievementInitialize: Adventurer: Level 4, done.
        var achievement = new ClientAchievement
        {
            Id = 48,
            NameId = 440970,
            DescriptionId = 440971,
            TimeStarted = 1361470144,
            TimeFinished = 1361470144,
            Progress = 1f,
            IconId = 1037,
            TrophyId = 5,
            CategoryId = 10
        };

        using var writer = new PacketWriter();
        achievement.Serialize(writer);

        Assert.AreEqual(
            "300000008ABA06008BBA060000C062265100000000C0622651000000000000803F" +
            "000000000000000000000000000000000000000000000000000000803F" +
            "000000000000000000000000000000000000000000000000FFFFFFFFFFFFFFFF" +
            "0000000000000000" +
            "00000000" +
            "0D040000000000000500000000000000000A000000",
            Convert.ToHexString(writer.Buffer));
        Assert.AreEqual(127, writer.Buffer.Length);
    }

    [TestMethod]
    public void Packets_MatchTheLiveBytes()
    {
        Assert.AreEqual("960003000000DF000000002A0D0000", Hex(new AchievementCompletePacket { AchievementId = 223 }));

        Assert.AreEqual("960006000000E10000004C490000000000000F2700000000000001", Hex(new AchievementObjectiveActivatedPacket
        {
            AchievementId = 225,
            ObjectiveId = 18764,
            RequiredCount = 9999
        }));

        Assert.AreEqual("960007000000E600000052490000502B00002755113B", Hex(new AchievementObjectiveUpdatePacket
        {
            AchievementId = 230,
            ObjectiveId = 18770,
            CurrentCount = 11088,
            Progress = BitConverter.Int32BitsToSingle(0x3B115527)
        }));

        Assert.AreEqual("960008000000DF0000004A4900000000803F0000000000", Hex(new AchievementObjectiveCompletePacket
        {
            AchievementId = 223,
            ObjectiveId = 18762,
            Progress = 1f
        }));
    }

    private static AchievementDefinitionCollection Load(string resources)
    {
        var achievements = new AchievementDefinitionCollection(NullLogger.Instance);

        Assert.IsTrue(achievements.Load(Path.Combine(resources, "Achievements.json")));

        return achievements;
    }

    private static string Hex(ISerializablePacket packet) => Convert.ToHexString(packet.Serialize());

    private static string FindSourceResources()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Resources", "Achievements.json")))
            directory = directory.Parent;

        Assert.IsNotNull(directory, "src/Resources/Achievements.json not found above the test output.");

        return Path.Combine(directory.FullName, "Resources");
    }
}
