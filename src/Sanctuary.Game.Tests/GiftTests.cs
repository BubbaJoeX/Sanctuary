using System;
using System.IO;
using System.Linq;
using System.Text.Json;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sanctuary.Game.Resources;
using Sanctuary.Game.Resources.Definitions.Rewards;
using Sanctuary.Packet;

namespace Sanctuary.Game.Tests;

[TestClass]
public class GiftTests
{
    [TestMethod]
    public void Gifts_LoadAndPointAtRealItemsTablesAndJobs()
    {
        var resources = FindSourceResources();

        var rewards = new RewardTableDefinitionCollection(NullLogger.Instance);
        var items = new ClientItemDefinitionCollection(NullLogger.Instance);
        var gifts = new GiftDefinitionCollection(NullLogger.Instance);

        Assert.IsTrue(rewards.Load(Path.Combine(resources, "Rewards.json")));
        Assert.IsTrue(items.Load(Path.Combine(resources, "ClientItemDefinitions.json")));
        Assert.IsTrue(gifts.Load(Path.Combine(resources, "Gifts.json"), rewards));

        Assert.IsTrue(gifts.Count > 150);

        var profileIds = JsonDocument.Parse(File.ReadAllText(Path.Combine(resources, "Profiles.json"))).RootElement.EnumerateArray()
            .Select(profile => profile.GetProperty("Id").GetInt32())
            .ToHashSet();

        foreach (var (itemId, gift) in gifts)
        {
            Assert.IsTrue(items.ContainsKey(itemId), $"Gift {gift.Comment} opens item {itemId}, which doesn't exist.");

            var drops = gift.Fixed.Concat(gift.Rolls
                .SelectMany(roll => roll.Choose.Count > 0 ? roll.Choose.Select(choice => choice.Table) : [roll.Table])
                .Where(table => table is not null)
                .SelectMany(table => rewards[table!].DropTable));

            foreach (var drop in drops)
            {
                if (drop is ItemRewardDropDefinition item)
                    Assert.IsTrue(items.ContainsKey(item.ItemDefinitionId), $"Gift {gift.Comment} gives item {item.ItemDefinitionId}, which doesn't exist.");
                else if (drop is ExperienceRewardDropDefinition { ProfileId: not 0 } stars)
                    Assert.IsTrue(profileIds.Contains(stars.ProfileId), $"Gift {gift.Comment} gives stars to job {stars.ProfileId}, which doesn't exist.");
            }
        }

        // Served as the type the client opens with a Use button.
        gifts.MarkOpenable(items);
        Assert.AreEqual(GiftDefinitionCollection.OpenableItemType, items[16382].Type);
    }

    [TestMethod]
    public void ImmediateActivationPackets_HaveTheClientsLayouts()
    {
        // The client drops a confirm that isn't exactly 25 bytes.
        var check = new ClientUpdatePacketImmediateActivationCheck { ItemGuid = 4250, TitleStringId = 4863, BodyStringId = 441928 };

        Assert.AreEqual("26001E009A1000000000000000000000FF12000048BE060001", Convert.ToHexString(check.Serialize()));

        Assert.IsTrue(InventoryPacketCheckImmediateActivationItem.TryDeserialize(Convert.FromHexString("2A0010009A100000"), out var asked));
        Assert.AreEqual(4250, asked.ItemGuid);

        Assert.IsTrue(InventoryPacketUseImmediateActivationItem.TryDeserialize(Convert.FromHexString("2A000F009A100000"), out var used));
        Assert.AreEqual(4250, used.ItemGuid);
    }

    private static string FindSourceResources()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Resources", "Gifts.json")))
            directory = directory.Parent;

        Assert.IsNotNull(directory, "src/Resources/Gifts.json not found above the test output.");

        return Path.Combine(directory.FullName, "Resources");
    }
}
