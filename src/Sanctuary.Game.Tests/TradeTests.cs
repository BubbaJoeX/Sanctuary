using System;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sanctuary.Game.Trading;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game.Tests;

[TestClass]
public sealed class TradeTests
{
    private const ulong Ann = 1, Bob = 2, Cat = 3;

    private static TradeSession StartedSession()
    {
        var session = new TradeSession(Ann, Bob);
        session.Start();
        return session;
    }

    [TestMethod]
    public void NothingChangesBeforeTheInviteIsAccepted()
    {
        var session = new TradeSession(Ann, Bob);

        Assert.AreEqual(TradeOfferChange.Unchanged, session.SetItem(Ann, 10, 1));
        Assert.IsFalse(session.SetCoins(Ann, 50));
        Assert.IsFalse(session.SetLocked(Ann, session.Revision, true));
    }

    [TestMethod]
    public void SetItem_TellsTheWindowWhatToDo()
    {
        var session = StartedSession();

        Assert.AreEqual(TradeOfferChange.Added, session.SetItem(Ann, 10, 2));
        Assert.AreEqual(TradeOfferChange.Unchanged, session.SetItem(Ann, 10, 2));
        Assert.AreEqual(TradeOfferChange.Changed, session.SetItem(Ann, 10, 5));
        Assert.AreEqual(TradeOfferChange.Removed, session.SetItem(Ann, 10, 0));
        Assert.AreEqual(TradeOfferChange.Unchanged, session.SetItem(Ann, 10, 0));
        Assert.AreEqual(TradeOfferChange.Unchanged, session.SetItem(Cat, 10, 1), "Not in this trade.");
        Assert.AreEqual(TradeOfferChange.Unchanged, session.SetItem(Bob, 11, -1));
    }

    [TestMethod]
    public void BothLockAndAccept_AtTheSameRevision()
    {
        var session = StartedSession();

        session.SetItem(Ann, 10, 1);
        session.SetCoins(Bob, 100);

        Assert.IsFalse(session.Accept(Ann, session.Revision), "Nobody has locked yet.");

        Assert.IsTrue(session.SetLocked(Ann, session.Revision, true));
        Assert.IsFalse(session.Accept(Ann, session.Revision), "Only one side has locked.");

        Assert.IsTrue(session.SetLocked(Bob, session.Revision, true));
        Assert.IsTrue(session.BothLocked);

        Assert.IsFalse(session.Accept(Ann, session.Revision - 1), "An older offer doesn't count.");
        Assert.IsTrue(session.Accept(Ann, session.Revision));
        Assert.IsFalse(session.BothAccepted);
        Assert.IsTrue(session.Accept(Bob, session.Revision));
        Assert.IsTrue(session.BothAccepted);
    }

    [TestMethod]
    public void ChangingAnOffer_UnlocksBothSides()
    {
        var session = StartedSession();

        session.SetItem(Ann, 10, 1);
        var revision = session.Revision;

        session.SetLocked(Ann, revision, true);
        session.SetLocked(Bob, revision, true);
        session.Accept(Ann, revision);

        Assert.IsTrue(session.SetCoins(Bob, 25));

        Assert.AreEqual(revision + 1, session.Revision);
        Assert.IsFalse(session.Inviter.Locked);
        Assert.IsFalse(session.Invitee.Locked);
        Assert.IsFalse(session.Inviter.Accepted);

        Assert.IsFalse(session.SetLocked(Ann, revision, true), "Locking what they saw before the change.");
        Assert.IsTrue(session.SetLocked(Ann, session.Revision, true));
    }

    [TestMethod]
    public void Unlocking_TakesBackAnAccept()
    {
        var session = StartedSession();

        session.SetLocked(Ann, session.Revision, true);
        session.SetLocked(Bob, session.Revision, true);
        session.Accept(Ann, session.Revision);

        Assert.IsTrue(session.SetLocked(Ann, 0, false), "Unlocking works at any revision.");
        Assert.IsFalse(session.Inviter.Accepted);
        Assert.IsFalse(session.BothLocked);
    }

    [TestMethod]
    public void Sides_AreFoundByEitherPlayer()
    {
        var session = StartedSession();

        Assert.AreSame(session.Inviter, session.GetSide(Ann));
        Assert.AreSame(session.Invitee, session.GetPartner(Ann));
        Assert.AreSame(session.Inviter, session.GetPartner(Bob));
        Assert.IsNull(session.GetSide(Cat));
        Assert.AreNotEqual(new TradeSession(Ann, Bob).Id, new TradeSession(Ann, Bob).Id);
    }

    [TestMethod]
    public void Packets_MatchTheClient()
    {
        // Header: short 59, then a byte sub-opcode.
        CollectionAssert.AreEqual(new byte[] { 59, 0, 5, 9, 0, 0, 0 }, new TradeEndSessionPacket { SessionId = 9 }.Serialize());
        CollectionAssert.AreEqual(new byte[] { 59, 0, 15, 6 }, new TradeMessagePacket { MessageId = TradeMessageId.Completed }.Serialize());
        CollectionAssert.AreEqual(new byte[] { 59, 0, 17, 9, 0, 0, 0, 1 }, new TradeUpdateLockPacket { SessionId = 9, Locked = true }.Serialize());
        CollectionAssert.AreEqual(new byte[] { 59, 0, 11, 9, 0, 0, 0, 2, 0, 0, 0, 0, 100, 0, 0, 0 },
            new TradeUpdateCoinCountPacket { SessionId = 9, Revision = 2, Coins = 100 }.Serialize());

        // The row is a whole ItemInstance: definition, tint, id, count, consumed, last cast, rental flag, ability count.
        var row = new TradeUpdateAddItemPacket { SessionId = 9, Revision = 2, IsMine = true, Item = new ItemInstance { Definition = 5, Id = 3, Count = 2 } }.Serialize();
        Assert.AreEqual(3 + 4 + 4 + 1 + 6 * 4 + 1 + 4, row.Length);

        // The invite starts with the inviter's name, the start with the partner's guid.
        var invite = new TradeInvitePacket { InviterName = new NameData { FirstName = "Ann" }, InviterGuid = 7, SessionId = 9 }.Serialize();
        Assert.AreEqual(3 + 3 * 4 + 4 + 3 + 4 + 8 + 4, invite.Length);
        Assert.AreEqual(7ul, BitConverter.ToUInt64(invite, invite.Length - 12));
    }

    [TestMethod]
    public void ClientPackets_ReadExactly()
    {
        byte[] offer = [59, 0, 9, 9, 0, 0, 0, 3, 0, 0, 0, 2, 0, 0, 0];

        Assert.IsTrue(TradeOfferItemPacket.TryDeserialize(offer, out var packet));
        Assert.AreEqual(9, packet.SessionId);
        Assert.AreEqual(3, packet.ItemId);
        Assert.AreEqual(2, packet.Count);

        Assert.IsFalse(TradeOfferItemPacket.TryDeserialize([.. offer, 0], out _), "Trailing bytes.");
        Assert.IsTrue(TradeLockPacket.TryDeserialize([59, 0, 8, 9, 0, 0, 0, 2, 0, 0, 0, 1], out var lockPacket));
        Assert.IsTrue(lockPacket.Locked);
        Assert.IsTrue(TradeInviteReplyPacket.TryDeserialize([59, 0, 3, 9, 0, 0, 0, 0], out var reply));
        Assert.IsFalse(reply.Accepted);
    }
}
