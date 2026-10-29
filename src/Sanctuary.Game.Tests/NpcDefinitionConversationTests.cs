using System;
using System.IO;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sanctuary.Game.Resources;

namespace Sanctuary.Game.Tests;

[TestClass]
public sealed class NpcDefinitionConversationTests
{
    [TestMethod]
    public void Load_KeepsAValidConversationAndDropsABrokenOne()
    {
        var path = Path.Combine(Path.GetTempPath(), $"npcs-{Guid.NewGuid():N}.json");

        File.WriteAllText(path, """
            [
              { "Id": 50001, "Name": "Talker", "ModelId": 1, "ModelFileName": "a.adr",
                "Conversation": { "Start": 1, "Nodes": [
                  { "Id": 1, "Text": "Hello!", "Choices": [ { "Text": "And?", "Next": 2 } ] },
                  { "Id": 2, "Text": "That's all.", "Choices": [] } ] } },
              { "Id": 50002, "Name": "Broken", "ModelId": 1, "ModelFileName": "a.adr",
                "Conversation": { "Start": 1, "Nodes": [ { "Id": 1, "Text": "Hi", "Choices": [ { "Text": "Go", "Next": 9 } ] } ] } },
              { "Id": 50003, "Name": "Quiet", "ModelId": 1, "ModelFileName": "a.adr" }
            ]
            """);

        try
        {
            var npcs = new NpcDefinitionCollection(NullLogger.Instance);

            Assert.IsTrue(npcs.Load(path));

            var talker = npcs[50001].Conversation;
            Assert.IsNotNull(talker);
            var next = talker.GetNode(talker.Start)!.Choices[0].Next;
            Assert.AreEqual("That's all.", talker.GetNode(next)!.Text);

            Assert.IsNull(npcs[50002].Conversation, "A choice leading to a missing node makes the conversation unusable.");
            Assert.IsTrue(npcs.ContainsKey(50002), "The NPC itself still loads.");
            Assert.IsNull(npcs[50003].Conversation);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
