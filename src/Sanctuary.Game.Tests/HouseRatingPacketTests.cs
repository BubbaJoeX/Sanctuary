using System;
using System.Text;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sanctuary.Packet;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game.Tests;

[TestClass]
public class HouseRatingPacketTests
{
    [TestMethod]
    public void CandidateInfoRequest_ReadsTheClientsRequests()
    {
        // From the Gateway's logs: entering house 11 (guid 178), and the player's own house list.
        Assert.IsTrue(RatingPacketCandidateInfoRequest.TryDeserialize(Concat("AE0010", Str("Housing"), Str("178"), "0000000000000000", "2100000000000000"), out var one));
        Assert.AreEqual("Housing", one.System);
        Assert.AreEqual("178", one.CandidateId);
        Assert.AreEqual(0UL, one.OwnerGuid);
        Assert.AreEqual(33UL, one.PlayerGuid);

        Assert.IsTrue(RatingPacketCandidateInfoRequest.TryDeserialize(Concat("AE0010", Str("Housing"), Str(""), "2100000000000000", "2100000000000000"), out var mine));
        Assert.AreEqual(string.Empty, mine.CandidateId);
        Assert.AreEqual(33UL, mine.OwnerGuid);
    }

    [TestMethod]
    public void CandidateInfoReply_MatchesTheLiveBytes()
    {
        // p12.pcap @270.109: a house that isn't listed, at the live server's 3 stars with no votes.
        var reply = new RatingPacketCandidateInfoReply
        {
            PlayerGuid = 0x4B52CC6A9573CA91,
            Candidates = [new RatingCandidateInfo { CandidateId = "573418857690894554", Rating = 3f }]
        };

        Assert.AreEqual(
            "AE001191CA73956ACC524B01000000" + Str("573418857690894554") + "00000000" + "00000000" + "00004040" + "00000000" + "00" + "00",
            Convert.ToHexString(reply.Serialize()));
    }

    [TestMethod]
    public void AddCandidate_ReadsTheLiveSubmission()
    {
        // p12.pcap @1396308276.907: "{n1} Cool Castle" submitted to the directory.
        var bytes = Concat("AE0006", Str("Housing"), Str("573418857690894554"), Str(""), Str("{n1} Cool Castle"), "91CA73956ACC524B", Str(""), Str(""), Str(""));

        Assert.IsTrue(RatingPacketAddCandidate.TryDeserialize(bytes, out var request));
        Assert.AreEqual("573418857690894554", request.CandidateId);
        Assert.AreEqual("{n1} Cool Castle", request.Name);
        Assert.AreEqual(0x4B52CC6A9573CA91UL, request.OwnerGuid);
    }

    [TestMethod]
    public void BrowserRequests_ReadTheirLayouts()
    {
        // The Popular tab opening with nothing listed yet, from the Gateway's logs.
        Assert.IsTrue(RatingPacketClientDataRequest.TryDeserialize(Concat("AE0003", Str("Housing"), "01000000", "00000000"), out var popular));
        Assert.AreEqual(RatingPacketClientDataRequest.Popular, popular.RequestType);

        // RequestFeatured's number is whatever was on the client's stack.
        Assert.IsTrue(RatingPacketRequestFeatured.TryDeserialize(Concat("AE0014", Str("Housing"), "EC770B783017EB08"), out _));

        // Search puts the player's guid before the system.
        Assert.IsTrue(RatingPacketSearchRequest.TryDeserialize(Concat("AE000C", "2100000000000000", Str("Housing"), Str("castle")), out var search));
        Assert.AreEqual("castle", search.SearchTerm);

        Assert.IsTrue(RatingPacketAddVote.TryDeserialize(Concat("AE0008", Str("Housing"), Str("178"), Str(""), Str("33"), "00008040"), out var vote));
        Assert.AreEqual(4f, vote.Vote);
    }

    [TestMethod]
    public void DataReply_KeysEachCardAndKeepsVotesAsAFloat()
    {
        var reply = new RatingPacketDataReply
        {
            PlayerGuid = 33,
            RequestType = RatingPacketClientDataRequest.Popular,
            Candidates = [new RatingCandidate { CandidateId = "178", OwnerName = "Bob", Name = "Bob's House", OwnerGuid = 33, Snapshot = "wugachug_lot", Rating = 4f, NumVotes = 2f }]
        };

        Assert.AreEqual(
            "AE00052100000000000000" + Str("Housing") + "01000000" + "00000000" +
            Str("178") + Str("Bob") + Str("Bob's House") + "2100000000000000" + Str("wugachug_lot") + Str("") + Str("") + "00008040" + "00000040" +
            "01000000",
            Convert.ToHexString(reply.Serialize()));
    }

    private static string Str(string value) =>
        Convert.ToHexString(BitConverter.GetBytes(value.Length)) + Convert.ToHexString(Encoding.ASCII.GetBytes(value));

    private static byte[] Concat(params string[] hex) => Convert.FromHexString(string.Concat(hex));
}
