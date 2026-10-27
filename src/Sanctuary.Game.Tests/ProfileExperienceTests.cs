using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sanctuary.Game.Helpers;
using Sanctuary.Packet;

namespace Sanctuary.Game.Tests;

[TestClass]
public sealed class ProfileExperienceTests
{
    [TestMethod]
    public void EachRankAsksForMore_AndTheTopRankForNothing()
    {
        Assert.AreEqual(100, ProfileExperience.ExperienceToNextRank(1));
        Assert.AreEqual(175, ProfileExperience.ExperienceToNextRank(2));
        Assert.AreEqual(1450, ProfileExperience.ExperienceToNextRank(ProfileExperience.MaxRank - 1));
        Assert.AreEqual(0, ProfileExperience.ExperienceToNextRank(ProfileExperience.MaxRank));

        for (var rank = 2; rank < ProfileExperience.MaxRank; rank++)
            Assert.IsTrue(ProfileExperience.ExperienceToNextRank(rank) > ProfileExperience.ExperienceToNextRank(rank - 1));
    }

    [TestMethod]
    public void Add_FillsTheBar_ThenCarriesTheLeftoverIntoTheNextRank()
    {
        Assert.AreEqual(new ProfileProgress(1, 75), ProfileExperience.Add(new ProfileProgress(1, 50), 25));
        Assert.AreEqual(new ProfileProgress(2, 0), ProfileExperience.Add(new ProfileProgress(1, 75), 25));
        Assert.AreEqual(new ProfileProgress(2, 30), ProfileExperience.Add(new ProfileProgress(1, 80), 50));
    }

    [TestMethod]
    public void Add_CanClimbSeveralRanksAtOnce()
    {
        // 100 + 175 + 250 to reach rank 4, with 25 to spare.
        Assert.AreEqual(new ProfileProgress(4, 25), ProfileExperience.Add(new ProfileProgress(1, 0), 550));
    }

    [TestMethod]
    public void Add_StopsAtTheTopRank()
    {
        Assert.AreEqual(new ProfileProgress(ProfileExperience.MaxRank, 0), ProfileExperience.Add(new ProfileProgress(19, 0), int.MaxValue));
        Assert.AreEqual(new ProfileProgress(ProfileExperience.MaxRank, 0), ProfileExperience.Add(new ProfileProgress(ProfileExperience.MaxRank, 0), 500));
    }

    [TestMethod]
    public void Add_TreatsBadStoredValuesAsRankOneWithNothing()
    {
        // Older rows can have a rank of 0; nothing earned is nothing earned.
        Assert.AreEqual(new ProfileProgress(1, 40), ProfileExperience.Add(new ProfileProgress(0, -5), 40));
        Assert.AreEqual(new ProfileProgress(3, 10), ProfileExperience.Add(new ProfileProgress(3, 10), -20));
    }

    [TestMethod]
    public void RankPercent_NeverShowsFullUntilTheTopRank()
    {
        Assert.AreEqual(0, ProfileExperience.RankPercent(new ProfileProgress(1, 0)));
        Assert.AreEqual(50, ProfileExperience.RankPercent(new ProfileProgress(1, 50)));
        Assert.AreEqual(99, ProfileExperience.RankPercent(new ProfileProgress(1, 99)));
        Assert.AreEqual(100, ProfileExperience.RankPercent(new ProfileProgress(ProfileExperience.MaxRank, 0)));
    }

    [TestMethod]
    public void ProgressPackets_MatchTheClient()
    {
        // BaseClientUpdatePacket 38, then the client's sub-opcodes 14 (six ints) and 18 (profile, rank).
        CollectionAssert.AreEqual(new byte[]
        {
            38, 0, 14, 0,
            5, 0, 0, 0,
            3, 0, 0, 0,
            40, 0, 0, 0,
            0, 0, 0, 0,
            0, 0, 0, 0,
            0, 0, 0, 0
        }, new ClientUpdatePacketUpdateProfileExperience { ProfileId = 5, Rank = 3, RankPercent = 40 }.Serialize());

        CollectionAssert.AreEqual(new byte[] { 38, 0, 18, 0, 5, 0, 0, 0, 4, 0, 0, 0 },
            new ClientUpdatePacketUpdateProfileRank { ProfileId = 5, Rank = 4 }.Serialize());
    }
}
