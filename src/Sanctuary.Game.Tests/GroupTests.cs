using System.Linq;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sanctuary.Core.IO;
using Sanctuary.Game.Groups;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game.Tests;

[TestClass]
public sealed class GroupTests
{
    private const ulong Ann = 1, Bob = 2, Cat = 3, Dan = 4;

    [TestMethod]
    public void AcceptingStartsTheInvitersGroup_WithTheInviterLeading()
    {
        var book = new GroupBook();

        Assert.AreEqual(GroupInviteResult.Sent, book.Invite(Ann, Bob, 0));
        Assert.AreEqual(GroupAcceptResult.Joined, book.Accept(Bob, 0, out var inviter, out var group));

        Assert.AreEqual(Ann, inviter);
        CollectionAssert.AreEqual(new[] { Ann, Bob }, group!.Members.ToArray());
        Assert.AreEqual(Ann, group.LeaderGuid);
        Assert.AreSame(group, book.GetGroup(Ann));
        Assert.AreSame(group, book.GetGroup(Bob));
    }

    [TestMethod]
    public void Invite_RefusesWhatTheClientHasMessagesFor()
    {
        var book = new GroupBook();

        Assert.AreEqual(GroupInviteResult.NotFound, book.Invite(Ann, Ann, 0));

        book.Invite(Ann, Bob, 0);
        Assert.AreEqual(GroupInviteResult.AlreadyInvited, book.Invite(Ann, Bob, 0));
        Assert.AreEqual(GroupInviteResult.AlreadyHasInvite, book.Invite(Cat, Bob, 0));

        book.Accept(Bob, 0, out _, out _);
        Assert.AreEqual(GroupInviteResult.NotLeader, book.Invite(Bob, Cat, 0));
        Assert.AreEqual(GroupInviteResult.AlreadyGrouped, book.Invite(Cat, Bob, 0));
    }

    [TestMethod]
    public void Invite_StopsAtFiveMembers()
    {
        var book = new GroupBook();

        for (ulong guid = 2; guid <= PlayerGroup.MaxMembers; guid++)
        {
            book.Invite(Ann, guid, 0);
            book.Accept(guid, 0, out _, out _);
        }

        Assert.AreEqual(PlayerGroup.MaxMembers, book.GetGroup(Ann)!.Members.Count);
        Assert.AreEqual(GroupInviteResult.GroupFull, book.Invite(Ann, 99, 0));
    }

    [TestMethod]
    public void Invitations_RunOut()
    {
        var book = new GroupBook();

        book.Invite(Ann, Bob, 0);

        // A new invitation is fine once the old one has run out, and the old one can't be taken up.
        Assert.AreEqual(GroupInviteResult.Sent, book.Invite(Cat, Bob, GroupBook.InviteTimeoutMs));
        Assert.AreEqual(GroupAcceptResult.NotInvited, book.Accept(Bob, 2 * GroupBook.InviteTimeoutMs, out _, out _));
        Assert.AreEqual(GroupAcceptResult.NotInvited, book.Accept(Dan, 0, out _, out _));
    }

    [TestMethod]
    public void Declining_TellsWhoAsked()
    {
        var book = new GroupBook();

        book.Invite(Ann, Bob, 0);

        Assert.AreEqual(Ann, book.Decline(Bob, 0));
        Assert.AreEqual(0ul, book.Decline(Bob, 0));
        Assert.IsNull(book.GetGroup(Ann));
    }

    [TestMethod]
    public void LeaderLeaving_HandsTheGroupOn_AndTheLastTwoBreakUp()
    {
        var book = new GroupBook();

        foreach (var guid in new[] { Bob, Cat })
        {
            book.Invite(Ann, guid, 0);
            book.Accept(guid, 0, out _, out _);
        }

        var group = book.Leave(Ann, out var disbanded);

        Assert.AreEqual(0ul, disbanded);
        Assert.AreEqual(Bob, group!.LeaderGuid);
        Assert.IsNull(book.GetGroup(Ann));

        book.Leave(Cat, out disbanded);

        Assert.AreEqual(Bob, disbanded, "A group of one breaks up.");
        Assert.IsNull(book.GetGroup(Bob));
        Assert.AreEqual(0, group.Members.Count);
        Assert.IsNull(book.Leave(Bob, out _));
    }

    [TestMethod]
    public void Kick_OnlyByTheLeader_AndOnlyMembers()
    {
        var book = new GroupBook();

        Assert.AreEqual(GroupKickResult.NotInGroup, book.Kick(Ann, Bob, out _, out _));

        foreach (var guid in new[] { Bob, Cat })
        {
            book.Invite(Ann, guid, 0);
            book.Accept(guid, 0, out _, out _);
        }

        Assert.AreEqual(GroupKickResult.NotOwner, book.Kick(Bob, Cat, out _, out _));
        Assert.AreEqual(GroupKickResult.TargetNotInGroup, book.Kick(Ann, Dan, out _, out _));
        Assert.AreEqual(GroupKickResult.TargetNotInGroup, book.Kick(Ann, Ann, out _, out _));

        Assert.AreEqual(GroupKickResult.Kicked, book.Kick(Ann, Cat, out var group, out var disbanded));
        Assert.AreEqual(0ul, disbanded);
        CollectionAssert.AreEqual(new[] { Ann, Bob }, group!.Members.ToArray());
        Assert.IsNull(book.GetGroup(Cat));
    }

    [TestMethod]
    public void ForgetInvites_DropsBothWays()
    {
        var book = new GroupBook();

        book.Invite(Ann, Bob, 0);
        book.Invite(Cat, Ann, 0);
        book.ForgetInvites(Ann);

        Assert.AreEqual(GroupAcceptResult.NotInvited, book.Accept(Bob, 0, out _, out _));
        Assert.AreEqual(GroupAcceptResult.NotInvited, book.Accept(Ann, 0, out _, out _));
    }

    [TestMethod]
    public void InvitePacket_RoundTripsTheClientsLayout()
    {
        // Header (40, 1), then the inviter and invitee (guid + name each), the group id and five more ints.
        var invite = new GroupPacketGroupInvite
        {
            Data = new GroupInviteData
            {
                InviterGuid = 7,
                InviterName = new NameData { FirstName = "Ann" },
                InviteeName = new NameData { FirstName = "Bob Smith" },
                GroupId = 3
            }
        };

        var bytes = invite.Serialize();

        CollectionAssert.AreEqual(new byte[] { 40, 0, 1, 0, 7, 0, 0, 0, 0, 0, 0, 0 }, bytes.Take(12).ToArray());
        Assert.AreEqual(4 + 2 * (8 + 3 * 4 + 4 + 4) + "Ann".Length + "Bob Smith".Length + 4 + 5 * 4, bytes.Length);

        Assert.IsTrue(GroupPacketGroupInvite.TryDeserialize(bytes, out var read));
        Assert.AreEqual("Bob Smith", read.Data.InviteeName.FullName);
        Assert.AreEqual(3, read.Data.GroupId);
    }

    [TestMethod]
    public void AcceptPacket_CarriesTheAnswerAfterTheInvitation()
    {
        var accept = new GroupPacketGroupAccept { Accepted = true };
        var bytes = accept.Serialize();

        var answerAt = 4 + new GroupInviteData().SerializeLength();
        Assert.AreEqual(1, bytes[answerAt]);
        Assert.AreEqual(answerAt + 1 + 5 * 4, bytes.Length);

        Assert.IsTrue(GroupPacketGroupAccept.TryDeserialize(bytes, out var read));
        Assert.IsTrue(read.Accepted);
    }

    [TestMethod]
    public void ReplyAndRosterPackets_MatchTheClient()
    {
        CollectionAssert.AreEqual(new byte[] { 40, 0, 2, 0, 7, 0, 0, 0 }, new GroupPacketGroupInviteReply { Result = GroupInviteResult.GroupFull }.Serialize());
        CollectionAssert.AreEqual(new byte[] { 40, 0, 5, 0, 2, 0, 0, 0 }, new GroupPacketGroupAcceptReply { Result = GroupAcceptResult.NoRoom }.Serialize());
        CollectionAssert.AreEqual(new byte[] { 40, 0, 7, 0, 2, 0, 0, 0 }, new GroupPacketGroupKickReply { Result = GroupKickResult.NotOwner }.Serialize());

        // A member: guid, name, seven ints (job id, icons and rank among them), two bools and the minigame id.
        var roster = new GroupPacketGroupUpdate();
        roster.Members.Add(new GroupMember { Guid = 9, Name = new NameData { FirstName = "Ann" }, ProfileRank = 4 });

        var bytes = roster.Serialize();

        Assert.AreEqual(4 + 4 + 8 + (3 * 4 + 4 + 3 + 4) + 7 * 4 + 2 + 4, bytes.Length);
        Assert.AreEqual(1, bytes[4]);
    }
}

file static class GroupInviteDataExtensions
{
    public static int SerializeLength(this GroupInviteData data)
    {
        using var writer = new PacketWriter();
        data.Serialize(writer);
        return writer.Buffer.Length;
    }
}
