using System.Numerics;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Sanctuary.Game.Tests;

[TestClass]
public sealed class EventJoinBookTests
{
    private static EventJoinBook.JoinPoint Point(string key) =>
        new(key, 1, new Vector4(1, 2, 3, 1), Quaternion.Identity, "Host");

    [TestMethod]
    public void TryFind_WithoutKey_NeedsExactlyOneOpenEvent()
    {
        var book = new EventJoinBook();

        Assert.AreEqual(EventJoinBook.FindResult.NoneOpen, book.TryFind(null, out _));

        book.OpenJoin(Point("race"));

        Assert.AreEqual(EventJoinBook.FindResult.Found, book.TryFind(null, out var point));
        Assert.AreEqual("race", point!.Key);

        book.OpenJoin(Point("dance"));

        Assert.AreEqual(EventJoinBook.FindResult.Ambiguous, book.TryFind(null, out _));
        Assert.AreEqual(EventJoinBook.FindResult.Found, book.TryFind("DANCE", out _), "Keys ignore case.");
        Assert.AreEqual(EventJoinBook.FindResult.NotOpen, book.TryFind("hunt", out _));
    }

    [TestMethod]
    public void Join_CountsEachPlayerOnce()
    {
        var book = new EventJoinBook();
        book.OpenJoin(Point("race"));

        Assert.IsTrue(book.Join("race", 7, "Tester"));
        Assert.IsFalse(book.Join("race", 7, "Tester"));
        Assert.IsTrue(book.HasJoined("race", 7));
        Assert.AreEqual(1, book.Participants("race").Count);
    }

    [TestMethod]
    public void Close_KeepsSignUpsButEndForgetsThem()
    {
        var book = new EventJoinBook();
        book.OpenJoin(Point("race"));
        book.Join("race", 7, "Tester");

        Assert.IsTrue(book.Close("race"));
        Assert.AreEqual(EventJoinBook.FindResult.NotOpen, book.TryFind("race", out _));
        Assert.AreEqual(1, book.Participants("race").Count, "Prizes still reach players after joining closes.");

        var ended = book.End("race");

        Assert.AreEqual("Tester", ended[7]);
        Assert.AreEqual(0, book.Participants("race").Count);
        Assert.IsFalse(book.HasJoined("race", 7));
    }

    [TestMethod]
    public void OpenJoin_AgainMovesTheSpotAndKeepsSignUps()
    {
        var book = new EventJoinBook();
        book.OpenJoin(Point("race"));
        book.Join("race", 7, "Tester");

        book.OpenJoin(Point("race") with { Position = new Vector4(9, 9, 9, 1) });

        Assert.AreEqual(EventJoinBook.FindResult.Found, book.TryFind("race", out var point));
        Assert.AreEqual(new Vector4(9, 9, 9, 1), point!.Position);
        Assert.IsTrue(book.HasJoined("race", 7));
    }
}
