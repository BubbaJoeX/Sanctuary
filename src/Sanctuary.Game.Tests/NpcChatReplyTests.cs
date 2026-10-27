using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sanctuary.Game.Conversations;

namespace Sanctuary.Game.Tests;

[TestClass]
public sealed class NpcChatReplyTests
{
    [TestMethod]
    public void CleanReply_TrimsWhitespaceAndWrappingQuotes()
    {
        Assert.AreEqual("Howdy there, Test.", NpcChatManager.CleanReply("\n\n \"Howdy there, Test.\" ", 240));
    }

    [TestMethod]
    public void CleanReply_DropsReasoningAndActions()
    {
        Assert.AreEqual("Welcome, friend!", NpcChatManager.CleanReply("<think>who is this?</think>*waves* Welcome, friend!", 240));
    }

    [TestMethod]
    public void CleanReply_JoinsLinesIntoOne()
    {
        Assert.AreEqual("Hello. Nice day.", NpcChatManager.CleanReply("Hello.\n\nNice   day.", 240));
    }

    [TestMethod]
    public void CleanReply_CutsAtASentenceEndNearTheLimit()
    {
        var reply = "The anvil rings all day. The forge is hot and the hammer is heavy, so mind your fingers please.";

        Assert.AreEqual("The anvil rings all day.", NpcChatManager.CleanReply(reply, 40));
    }

    [TestMethod]
    public void CleanReply_CutsMidSentenceWithAnEllipsisWhenThereIsNoEarlyStop()
    {
        Assert.AreEqual("abcdefghij...", NpcChatManager.CleanReply("abcdefghijklmnopqrstuvwxyz", 10));
    }
}
