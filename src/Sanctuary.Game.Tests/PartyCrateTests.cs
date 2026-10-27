using System;
using System.IO;
using System.Linq;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sanctuary.Game.Resources;
using Sanctuary.Game.Resources.Definitions;
using Sanctuary.Game.Resources.Definitions.Rewards;

namespace Sanctuary.Game.Tests;

[TestClass]
public sealed class PartyCrateTests
{
    private static readonly string Resources = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Resources"));

    [TestMethod]
    public void Consumables_LoadEveryCrate()
    {
        var consumables = new ConsumableCollection(NullLogger.Instance);
        Assert.IsTrue(consumables.Load(Path.Combine(Resources, "Consumables.json")));

        // Party Crate (x2), 5M GM Crate, Birthday Party Crate, Fireworks Crate (x2)
        foreach (var itemId in new[] { 67888, 68932, 62515, 4829, 15915, 57013 })
        {
            Assert.IsTrue(consumables.Cakes.TryGetValue(itemId, out var crate), $"Item {itemId}");
            Assert.AreEqual(CakeItemType.PartyCrate, crate.Type, $"Item {itemId}");
            Assert.IsTrue(crate.BurstEffectIds.Length > 0, $"Item {itemId}");
        }
    }

    [TestMethod]
    public void Cakes_UseItemsTheAbilityHandlerAccepts()
    {
        var consumables = new ConsumableCollection(NullLogger.Instance);
        Assert.IsTrue(consumables.Load(Path.Combine(Resources, "Consumables.json")));

        var items = new ClientItemDefinitionCollection(NullLogger.Instance);
        Assert.IsTrue(items.Load(Path.Combine(Resources, "ClientItemDefinitions.json")));

        var models = new ModelDefinitionCollection(NullLogger.Instance);
        Assert.IsTrue(models.Load(Path.Combine(Resources, "Models.txt")));

        foreach (var cake in consumables.Cakes.Values)
        {
            // The handler drops item uses without an ability before any cake can match.
            Assert.IsTrue(items.TryGetValue(cake.ItemId, out var item) && item.ActivatableAbilityId != 0, $"Item {cake.ItemId}");
            Assert.IsTrue(models.ContainsKey(cake.ModelId), $"Item {cake.ItemId} model {cake.ModelId}");
        }
    }

    [TestMethod]
    public void Crates_RollRewardTablesThatExist()
    {
        var consumables = new ConsumableCollection(NullLogger.Instance);
        Assert.IsTrue(consumables.Load(Path.Combine(Resources, "Consumables.json")));

        var rewards = new RewardTableDefinitionCollection(NullLogger.Instance);
        Assert.IsTrue(rewards.Load(Path.Combine(Resources, "Rewards.json")));

        var items = new ClientItemDefinitionCollection(NullLogger.Instance);
        Assert.IsTrue(items.Load(Path.Combine(Resources, "ClientItemDefinitions.json")));

        foreach (var crate in consumables.Cakes.Values.Where(cake => !string.IsNullOrEmpty(cake.RewardTableKey)))
        {
            Assert.IsTrue(rewards.TryGetValue(crate.RewardTableKey!.ToLowerInvariant(), out var table), $"Item {crate.ItemId}: {crate.RewardTableKey}");

            foreach (var drop in table.DropTable.OfType<ItemRewardDropDefinition>())
                Assert.IsTrue(items.ContainsKey(drop.ItemDefinitionId), $"{crate.RewardTableKey}: item {drop.ItemDefinitionId}");
        }
    }

    [TestMethod]
    public void MobLootAndLootBags_RollRewardTablesThatExist()
    {
        var mobs = new MobDefinitionCollection(NullLogger.Instance);
        Assert.IsTrue(mobs.Load(Path.Combine(Resources, "Mobs.json")));

        var abilities = new ItemAbilityDefinitionCollection(NullLogger.Instance);
        Assert.IsTrue(abilities.Load(Path.Combine(Resources, "ItemAbilities.json")));

        var rewards = new RewardTableDefinitionCollection(NullLogger.Instance);
        Assert.IsTrue(rewards.Load(Path.Combine(Resources, "Rewards.json")));

        var items = new ClientItemDefinitionCollection(NullLogger.Instance);
        Assert.IsTrue(items.Load(Path.Combine(Resources, "ClientItemDefinitions.json")));

        var keys = mobs.Values.Select(mob => mob.RewardTable)
            .Concat(abilities.Values.Select(ability => ability.RewardTable))
            .Where(key => !string.IsNullOrEmpty(key))
            .ToList();

        Assert.IsTrue(keys.Contains("mob-loot") && keys.Contains("pocket-book"), "Mobs drop loot and the Thick Pocket Book opens.");

        foreach (var key in keys)
        {
            Assert.IsTrue(rewards.TryGetValue(key!.ToLowerInvariant(), out var table), key);

            foreach (var drop in table.DropTable.OfType<ItemRewardDropDefinition>())
                Assert.IsTrue(items.ContainsKey(drop.ItemDefinitionId), $"{key}: item {drop.ItemDefinitionId}");
        }
    }
}
