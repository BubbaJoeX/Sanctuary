using System;
using System.Numerics;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sanctuary.Core.IO;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game.Tests;

[TestClass]
public class EncounterPacketTests
{
    private const ulong CharacterGuid = 0x4B52CC6A9573CA91;

    // The live battle (Frostfang Growler!, p12.pcap): its offer, then its end.
    private const string LiveOffer =
        "29007200AE000200F0030000AE000200020000000100000001000000FB0621DE91CA73956ACC524B0001000000010000000000A61B0243C5" +
        "20303F6F12F0420000803F070000004D61C3BF68656D0000000000F00300000000000000000000000000000100000000000000010100D007" +
        "0000102700005C6C0100BD6F0000EB9601000100000002000000040000000000000000000000000000000000000000000000000000000000" +
        "0000803F000000000000000000000000000000000000000000000000FFFFFFFFFFFFFFFF0000000000000000012400000000000000000000" +
        "000F00000000000000000000000000803F0000000000000000000000000000000091CA73956ACC524B660000007119000000000000D40E00" +
        "00001400000000000000000000000F00000000000000000000000000803F0000000000000000000000000000000091CA73956ACC524BB309" +
        "000061080200050000000100000001B3090000EA0000006108020001000000B1290100000000000000000000000000000100000000850E00" +
        "00080100005000020001000000902601000000000000000000000000000001000000009D0C0000F700000087030200010000005325010000" +
        "0000000000000000000000000100000000AE04000000000000390002000100000079260100000000000000000000000000000100000001CD" +
        "030000FFFFFFFF0A1A000001000000F228000000000000000000000000000000BD0300000200000000300000EE0800006889010000000000" +
        "000000000000000000000000000000000000000000000000803F000000000000000000000000000000000000000000000000FFFFFFFFFFFF" +
        "FFFF000000000000000000000000000000000000000001000000000000000062310000F0960100F19601000000000000000A000000000000" +
        "000F00000000000000000000000000803F0000000000000000000000000000000091CA73956ACC524B3D0F00007009000000000000781400" +
        "0000000000000000000000000000000000000000000001010101010000000001000000010000000000000000AE000200000100AE00000005" +
        "00000092080000670B0000680B0000690B0000A80E0000";

    private const string LiveComplete =
        "2900030000000000000000000100000000000000004A000000000C00000054000000000000000000000000000000000000000000803F0000" +
        "00000000000000000000000000000000000000000000FFFFFFFFFFFFFFFF0000000000000000001600000000000000000000000000000000" +
        "000000000000000000803F000000000000000000000000000000000000000000000000FFFFFFFFFFFFFFFF00000000000000000000000000" +
        "00000000000000000000000000000000000000000000803F000000000000000000000000000000000000000000000000FFFFFFFFFFFFFFFF" +
        "0000000000000000";

    [TestMethod]
    public void Offer_MatchesTheLiveBattle()
    {
        var position = new Vector4(
            BitConverter.Int32BitsToSingle(0x43021BA6),
            BitConverter.Int32BitsToSingle(0x3F3020C5),
            BitConverter.Int32BitsToSingle(0x42F0126F),
            1f);

        var details = new EncounterDetails
        {
            Id = 0x200AE,
            Unknown = 2,
            Teams =
            [
                [
                    new EncounterParticipant
                    {
                        Key = unchecked((int)0xDE2106FB),
                        Guid = CharacterGuid,
                        Position = position,
                        Name = "Maÿhem",
                        ServerId = 1008
                    }
                ]
            ],
            TimeLimitSeconds = 2000,
            RespawnMilliseconds = 10000,
            Info = new MiniGameInfoPacket
            {
                NameId = 93276,
                IconId = 28605,
                DescriptionId = 104171,
                Difficulty = 1,
                ProfileType = 2,
                Type = 4,
                Reward = Empty(),
                MemberReward = new RewardBundleBase
                {
                    Coins = 36,
                    Unknown3 = 15,
                    PlayerGuid = CharacterGuid,
                    IconId = 102,
                    NameId = 6513,
                    DescriptionId = 3796
                },
                RewardPreview = Preview(),
                Objectives =
                [
                    new QuestObjectiveInfo { ObjectiveId = 12288, NameId = 2286, DescriptionId = 100712, RewardBundle = Empty(), Kind = 1 },
                    new QuestObjectiveInfo
                    {
                        ObjectiveId = 12642,
                        NameId = 104176,
                        DescriptionId = 104177,
                        RewardBundle = new RewardBundleBase
                        {
                            Success = false,
                            Experience = 10,
                            Unknown3 = 15,
                            PlayerGuid = CharacterGuid,
                            IconId = 3901,
                            NameId = 2416,
                            DescriptionId = 5240
                        }
                    }
                ],
                ShowStarCounter = true,
                ShowStatusIcon = true,
                ShowActionBar = true,
                ShowStartScreen = true,
                ShowEndScreen = true,
                Unknown = 1,
                Unknown2 = true,
                Unknown6 = 0x200AE
            }
        };

        var packet = new EncounterDetailsResponsePacket
        {
            EncounterId = 0x200AE,
            ReplyId = 1008,
            Details = details,
            MarketplaceId = 174,
            MarketplaceAds = [2194, 2919, 2920, 2921, 3752]
        };

        Assert.AreEqual(LiveOffer, Convert.ToHexString(packet.Serialize()));
    }

    [TestMethod]
    public void Complete_MatchesTheLiveBattle()
    {
        var packet = new EncounterPacketEncounterComplete
        {
            Won = true,
            ElapsedSeconds = 74,
            Reward = new RewardBundleBase { Success = false, Coins = 12, Experience = 84, Unknown3 = 0, IconId = -1, NameId = -1 },
            MemberReward = new RewardBundleBase { Success = false, Coins = 22, Unknown3 = 0, IconId = -1, NameId = -1 },
            Unused = Empty()
        };

        Assert.AreEqual(LiveComplete, Convert.ToHexString(packet.Serialize()));
    }

    [TestMethod]
    public void SmallPackets_MatchTheLiveBattle()
    {
        Assert.AreEqual("29006A00AE000200F003000002000000", Hex(new EncounterStatePacket { EncounterId = 0x200AE, ReplyId = 1008, State = 2 }));
        Assert.AreEqual("29006B00AE000200F0030000", Hex(new EncounterZoneIsReadyPacket { EncounterId = 0x200AE, ReplyId = 1008 }));
        Assert.AreEqual("29000200AE000200F003000091CA73956ACC524B00", Hex(new EncounterPacketPlayerEnter { EncounterId = 0x200AE, ReplyId = 1008, Guid = CharacterGuid }));
        Assert.AreEqual("29007900AE000200F003000000", Hex(new EncounterParticipantTerminateEncounterPacket { EncounterId = 0x200AE, ReplyId = 1008 }));
        Assert.AreEqual("2900780000000000000000007A020000C07F12000000000000000000", Hex(new EncounterParticipantMessagePacket { MessageId = 634, Guid = 0x127FC0 }));
        Assert.AreEqual("29007D00000000000000000000000000E8030000", Hex(new EncounterShowRespawnWindowPacket { ReviveCost = 1000 }));
        Assert.AreEqual("2000090091CA73956ACC524B01", Hex(new CombatPacketEnableBossDisplay { BossGuid = CharacterGuid, Enable = true }));
        Assert.AreEqual("28000D0091CA73956ACC524B01", Hex(new GroupPacketAnnounceEncounterReply { MemberGuid = CharacterGuid, Accepted = true }));
    }

    [TestMethod]
    public void EncounterDataCommon_MatchesTheLiveZones()
    {
        Assert.AreEqual("3E000101010101010101010101FFFFFFFF0000FA4301000000A0860100F40100002C010000A861000088130000", Hex(EncounterDataCommonPacket.Battle));
        Assert.AreEqual("3E000000000000000000000000FFFFFFFF0000803F100E00000000000000000000010000000000000000000000", Hex(EncounterDataCommonPacket.Default));
    }

    [TestMethod]
    public void ClientRequests_ReadAsTheLiveClientSentThem()
    {
        Assert.IsTrue(EncounterParticipantRequestEntrancePacket.TryDeserialize(Convert.FromHexString("29006C00AE000200F003000091CA73956ACC524B"), out var entrance));
        Assert.AreEqual(0x200AE, entrance.EncounterId);
        Assert.AreEqual(1008, entrance.ReplyId);
        Assert.AreEqual(CharacterGuid, entrance.Guid);

        Assert.IsTrue(EncounterParticipantRequestExitPacket.TryDeserialize(Convert.FromHexString("29006D000000000000000000"), out _));

        Assert.IsTrue(EncounterParticipantResumePacket.TryDeserialize(Convert.FromHexString("29007A00000000000000000001"), out var resume));
        Assert.IsTrue(resume.Paid);

        // Strict: a request with bytes left over, or another sub-op, is dropped.
        Assert.IsFalse(EncounterParticipantRequestExitPacket.TryDeserialize(Convert.FromHexString("29006D00000000000000000000"), out _));
        Assert.IsFalse(EncounterParticipantRequestExitPacket.TryDeserialize(Convert.FromHexString("29006C000000000000000000"), out _));
    }

    private static string Hex(ISerializablePacket packet) => Convert.ToHexString(packet.Serialize());

    private static RewardBundleBase Empty() => new() { Success = false, Unknown3 = 0, IconId = -1, NameId = -1 };

    private static RewardBundleBase Preview()
    {
        var preview = new RewardBundleBase
        {
            Success = false,
            Coins = 20,
            Unknown3 = 15,
            PlayerGuid = CharacterGuid,
            IconId = 2483,
            NameId = 133217,
            DescriptionId = 957
        };

        preview.Entries.Add(new RewardBundleEntryItem { IsHidden = true, IconId = 2483, TintId = 234, NameId = 133217, DefinitionId = 76209 });
        preview.Entries.Add(new RewardBundleEntryItem { IconId = 3717, TintId = 264, NameId = 131152, DefinitionId = 75408 });
        preview.Entries.Add(new RewardBundleEntryItem { IconId = 3229, TintId = 247, NameId = 131975, DefinitionId = 75091 });
        preview.Entries.Add(new RewardBundleEntryItem { IconId = 1198, NameId = 131129, DefinitionId = 75385 });
        preview.Entries.Add(new RewardBundleEntryItem { IsHidden = true, IconId = 973, TintId = -1, NameId = 6666, DefinitionId = 10482 });

        return preview;
    }
}
