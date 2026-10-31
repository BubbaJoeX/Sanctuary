using System;
using System.IO;
using System.Linq;
using System.Text.Json;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Sanctuary.Game.Tests;

[TestClass]
public class MerchantStockTests
{
    [TestMethod]
    public void Merchants_SellRealItemsTheCoinStorePrices()
    {
        var resources = FindSourceResources();

        var items = Read(Path.Combine(resources, "ClientItemDefinitions.json")).Select(item => item.GetProperty("Id").GetInt32()).ToHashSet();
        var stocked = Read(Path.Combine(resources, "CoinStoreItems.json")).Select(item => item.GetProperty("Id").GetInt32()).ToHashSet();
        var merchants = Read(Path.Combine(resources, "Npcs.json")).Where(npc => npc.TryGetProperty("MerchantList", out _)).ToList();

        Assert.IsTrue(merchants.Count >= 60, "The live merchants should all have stock.");

        foreach (var merchant in merchants)
        {
            var id = merchant.GetProperty("Id").GetInt32();

            // A merchant only opens with the merchant interaction (type 17).
            Assert.IsTrue(merchant.TryGetProperty("InteractionList", out var interactionList) &&
                interactionList.GetProperty("Interactions").EnumerateArray().Any(interaction => interaction.GetProperty("Type").GetInt32() == 17),
                $"Merchant {id} has no merchant interaction.");

            foreach (var entry in merchant.GetProperty("MerchantList").GetProperty("Entries").EnumerateArray())
            {
                var itemId = entry.GetProperty("ItemDefinitionId").GetInt32();

                // Sanctuary's custom items (9000000 and up) come from the database, not ClientItemDefinitions.json.
                if (itemId >= 9_000_000)
                    continue;

                Assert.IsTrue(items.Contains(itemId), $"Merchant {id} sells item {itemId}, which doesn't exist.");
                Assert.IsTrue(stocked.Contains(itemId), $"Merchant {id} sells item {itemId}, which the Coin Store doesn't price.");
            }
        }
    }

    private static JsonElement[] Read(string file) => [.. JsonDocument.Parse(File.ReadAllText(file)).RootElement.EnumerateArray()];

    private static string FindSourceResources()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Resources", "Npcs.json")))
            directory = directory.Parent;

        Assert.IsNotNull(directory, "src/Resources/Npcs.json not found above the test output.");

        return Path.Combine(directory.FullName, "Resources");
    }
}
