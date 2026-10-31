using System;
using System.Text;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sanctuary.Packet;

namespace Sanctuary.Game.Tests;

[TestClass]
public class UxPacketTests
{
    [TestMethod]
    public void GroupMapPing_ReadsTheClientsAndWritesTheSame()
    {
        // From the Gateway's logs: guid 33, X 504, Z -252.
        var bytes = Convert.FromHexString("28001000210000000000000" + "0F801000004FFFFFF");

        Assert.IsTrue(GroupPacketMapPing.TryDeserialize(bytes, out var ping));
        Assert.AreEqual(33UL, ping.Guid);
        Assert.AreEqual(504, ping.X);
        Assert.AreEqual(-252, ping.Z);

        CollectionAssert.AreEqual(bytes, new GroupPacketMapPing { Guid = 33, X = 504, Z = -252 }.Serialize());
    }

    [TestMethod]
    public void GroupStatusPackets_HaveTheClientsLayout()
    {
        Assert.AreEqual("28000F00210000000000000001", Convert.ToHexString(new GroupPacketChangingJobUpdate { MemberGuid = 33, IsChangingJob = true }.Serialize()));
        Assert.AreEqual("28001100210000000000000000", Convert.ToHexString(new GroupPacketInMinigame { MemberGuid = 33 }.Serialize()));

        Assert.IsTrue(GroupPacketChangingJob.TryDeserialize(Convert.FromHexString("28000E0001"), out var changing));
        Assert.IsTrue(changing.IsChangingJob);
    }

    [TestMethod]
    public void ReportPlayer_ReadsTheChatLines()
    {
        var bytes = Concat(
            "44000100", Str("Griefer"), "01000000",
            "01000000", Str("go away"), "00F1536500000000");

        Assert.IsTrue(ReportPacketReportPlayer.TryDeserialize(bytes, out var report));
        Assert.AreEqual("Griefer", report.ReportedName);
        Assert.AreEqual(1, report.Lines.Count);
        Assert.AreEqual((1, "go away", 0x6553F100UL), report.Lines[0]);

        // More than the client's ten lines isn't a real report.
        Assert.IsFalse(ReportPacketReportPlayer.TryDeserialize(Concat("44000100", Str("x"), "0B000000"), out _));
    }

    [TestMethod]
    public void ReportHouse_ReadsTheHouseAndOwner()
    {
        var bytes = Concat("44000300", "B200000000000000", "2100000000000000", Str("Wugachug Lot"), Str("Bob"));

        Assert.IsTrue(ReportPacketReportHouse.TryDeserialize(bytes, out var report));
        Assert.AreEqual(178UL, report.HouseGuid);
        Assert.AreEqual(33UL, report.OwnerGuid);
        Assert.AreEqual("Wugachug Lot", report.HouseName);
        Assert.AreEqual("Bob", report.OwnerName);
    }

    private static string Str(string value) =>
        Convert.ToHexString(BitConverter.GetBytes(value.Length)) + Convert.ToHexString(Encoding.ASCII.GetBytes(value));

    private static byte[] Concat(params string[] hex) => Convert.FromHexString(string.Concat(hex));
}
