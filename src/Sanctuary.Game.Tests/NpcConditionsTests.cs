using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sanctuary.Game.Conversations;
using Sanctuary.Game.Helpers;

namespace Sanctuary.Game.Tests;

[TestClass]
public sealed class NpcConditionsTests
{
    [TestMethod]
    public void InHours_DayWindow()
    {
        Assert.IsTrue(NpcConditions.InHours(9f, 8, 17));
        Assert.IsTrue(NpcConditions.InHours(8f, 8, 17));
        Assert.IsFalse(NpcConditions.InHours(17f, 8, 17));
        Assert.IsFalse(NpcConditions.InHours(3.5f, 8, 17));
    }

    [TestMethod]
    public void InHours_NightWrapsPastMidnight()
    {
        Assert.IsTrue(NpcConditions.InHours(23f, 22, 4));
        Assert.IsTrue(NpcConditions.InHours(0.5f, 22, 4));
        Assert.IsFalse(NpcConditions.InHours(4f, 22, 4));
        Assert.IsFalse(NpcConditions.InHours(12f, 22, 4));
    }

    [TestMethod]
    public void Validate_NeedsPairs()
    {
        Assert.IsNull(new NpcConditions { FromHour = 20, ToHour = 6, QuestId = 5, QuestState = NpcConditions.QuestDone }.Validate());
        Assert.IsNotNull(new NpcConditions { FromHour = 20 }.Validate());
        Assert.IsNotNull(new NpcConditions { QuestId = 5 }.Validate());
        Assert.IsNotNull(new NpcConditions { QuestId = 5, QuestState = "finished" }.Validate());
        Assert.IsNotNull(new NpcConditions { FromHour = 25, ToHour = 2 }.Validate());
        Assert.IsNotNull(new NpcConditions { ItemId = 0 }.Validate());
    }

    [TestMethod]
    public void IsEmpty_OnlyWithoutConditions()
    {
        Assert.IsTrue(new NpcConditions().IsEmpty);
        Assert.IsFalse(new NpcConditions { RefereesOnly = true }.IsEmpty);
        Assert.IsFalse(new NpcConditions { ItemId = 12, ItemAbsent = true }.IsEmpty);
    }

    [TestMethod]
    public void Conversation_Validate()
    {
        var conversation = new Conversation
        {
            Start = 1,
            Nodes =
            [
                new ConversationNode { Id = 1, Text = "Hello!", Choices = [new ConversationChoice { Text = "Hi", Next = 2 }, new ConversationChoice { Text = "Bye" }] },
                new ConversationNode { Id = 2, Text = "Lovely day." }
            ]
        };

        Assert.IsNull(conversation.Validate());

        conversation.Nodes[0].Choices[0].Next = 9;
        Assert.IsNotNull(conversation.Validate(), "a choice to a missing node");

        conversation.Nodes[0].Choices[0].Next = 2;
        conversation.Start = 3;
        Assert.IsNotNull(conversation.Validate(), "a missing start");

        conversation.Start = 1;
        conversation.Nodes[1].Text = " ";
        Assert.IsNotNull(conversation.Validate(), "a node without text");

        conversation.Nodes[1].Text = "Lovely day.";
        conversation.Nodes[1].Id = 1;
        Assert.IsNotNull(conversation.Validate(), "two nodes with one id");
    }
}
