using System;
using System.IO;
using System.Linq;
using System.Text.Json;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sanctuary.Game.Resources;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game.Tests;

[TestClass]
public class CraftingTests
{
    [TestMethod]
    public void Recipes_LoadAndPointAtRealData()
    {
        var resources = FindSourceResources();
        var recipes = new RecipeDefinitionCollection(NullLogger.Instance);

        Assert.IsTrue(recipes.Load(Path.Combine(resources, "Recipes.json")));
        Assert.IsTrue(recipes.Count >= 70);

        // The 17 the live server sent, exactly.
        Assert.AreEqual(17, recipes.Values.Count(recipe => !recipe.Authored));

        var copperBar = recipes[4];
        Assert.AreEqual(21785, copperBar.ResultItemId);
        Assert.AreEqual(9, copperBar.FilterType);
        CollectionAssert.AreEqual(new[] { (879, 3), (2739, 5) }, copperBar.Components.Select(component => (component.ItemId, component.Required)).ToArray());

        var minigameIds = Directory.GetFiles(Path.Combine(resources, "Minigames"), "*.json")
            .SelectMany(file => ReadArray(file).Select(game => game.GetProperty("Id").GetInt32()))
            .ToHashSet();

        foreach (var recipe in recipes.Values.Where(recipe => recipe.MinigameId is not null))
            Assert.IsTrue(minigameIds.Contains(recipe.MinigameId!.Value), $"Recipe {recipe.Id} is made by minigame {recipe.MinigameId}, which isn't run.");

        Assert.IsTrue(recipes.TryGetByMinigame(77, out var smelted) && smelted.Id == 4);
    }

    [TestMethod]
    public void Stations_PointAtRealNpcsAndGames()
    {
        var resources = FindSourceResources();
        var stations = new StationDefinitionCollection(NullLogger.Instance);

        Assert.IsTrue(stations.Load(Path.Combine(resources, "Stations.json")));
        Assert.IsTrue(stations.Count > 100);

        var npcIds = ReadArray(Path.Combine(resources, "Npcs.json")).Select(npc => npc.GetProperty("Id").GetInt32()).ToHashSet();
        var minigameIds = Directory.GetFiles(Path.Combine(resources, "Minigames"), "*.json")
            .SelectMany(file => ReadArray(file).Select(game => game.GetProperty("Id").GetInt32()))
            .ToHashSet();

        foreach (var station in stations.Values)
        {
            Assert.IsTrue(npcIds.Contains(station.NpcId), $"Station {station.NpcId} ({station.Comment}) isn't in Npcs.json.");
            Assert.IsTrue(station.MinigameIds.All(minigameIds.Contains), $"Station {station.NpcId} ({station.Comment}) starts a game that isn't run.");
            Assert.IsTrue(station.RecipeFilters.All(filter => filter is 9 or 10 or 12), $"Station {station.NpcId} has an unknown recipe filter.");
        }

        // The anvils smelt as well as forge: no furnace stands in the world.
        Assert.IsTrue(stations.Values.Any(station => station.RecipeFilters.Contains(9)));
    }

    [TestMethod]
    public void UnlockIngredientStacks_LetsCappedPartsStack()
    {
        var resources = FindSourceResources();
        var recipes = new RecipeDefinitionCollection(NullLogger.Instance);
        var items = new ClientItemDefinitionCollection(NullLogger.Instance);

        Assert.IsTrue(recipes.Load(Path.Combine(resources, "Recipes.json")));
        Assert.IsTrue(items.Load(Path.Combine(resources, "ClientItemDefinitions.json")));

        // Belt Strap and Wooden Plank: live recipe 165 needs 12 and 6.
        Assert.AreEqual(1, items[54].MaxStackSize);

        Assert.IsTrue(recipes.UnlockIngredientStacks(items) > 0);
        Assert.AreEqual(-1, items[54].MaxStackSize);
        Assert.AreEqual(-1, items[55].MaxStackSize);
    }

    [TestMethod]
    public void RecipeData_MatchesTheLiveBytes()
    {
        // Live recipe 4 (Copper Bar) as the 2014 server sent it in SendSelf, in a RecipeAdd.
        var packet = new RecipeAddPacket
        {
            Recipe = new RecipeData
            {
                Id = 4,
                NameId = 104,
                IconId = 462,
                DescriptionId = 3021,
                FilterType = 9,
                ResultItemId = 21785,
                Components =
                [
                    new RecipeComponentData { NameId = 878, IconId = 1795, DescriptionId = 883, Required = 3, ItemId = 879 },
                    new RecipeComponentData { NameId = 21917, IconId = 698, IconTint = 100, DescriptionId = 3021, Required = 5, ItemInstanceId = 1995, ItemId = 2739 }
                ]
            }
        };

        Assert.AreEqual(
            "3F0001" +
            "0400000068000000CE01000000000000CD0B00000100000000" + "09000000" +
            "02000000" +
            "6E03000003070000000000007303000003000000" + "00000000FFFFFFFF6F030000" +
            "9D550000BA02000064000000CD0B000005000000" + "CB070000FFFFFFFFB30A0000" +
            "19550000" + "01" + "00000000",
            Convert.ToHexString(packet.Serialize()));
    }

    [TestMethod]
    public void RecipePackets_HaveTheirLiveLayouts()
    {
        // p2.pcap: Sugar (stack 4596) arrives, and recipe 43's ingredient 3 is pointed at it.
        Assert.AreEqual("3F00022B00000003000000F4110000",
            Convert.ToHexString(new RecipeComponentUpdatePacket { RecipeId = 43, ComponentIndex = 3, ItemInstanceId = 4596 }.Serialize()));

        Assert.AreEqual("1A0019000A00000000000000",
            Convert.ToHexString(new CommandPacketShowRecipeWindow { FilterType = 10 }.Serialize()));

        Assert.IsTrue(CommandPacketRecipeStart.TryDeserialize(Convert.FromHexString("1A00180004000000"), out var start));
        Assert.AreEqual(4, start.RecipeId);
        Assert.IsFalse(CommandPacketRecipeStart.TryDeserialize(Convert.FromHexString("1A0018000400000000"), out _));
    }

    private static JsonElement[] ReadArray(string file)
    {
        var text = string.Join('\n', File.ReadAllLines(file).Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal)));

        return [.. JsonDocument.Parse(text).RootElement.EnumerateArray()];
    }

    private static string FindSourceResources()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Resources", "Recipes.json")))
            directory = directory.Parent;

        Assert.IsNotNull(directory, "src/Resources/Recipes.json not found above the test output.");

        return Path.Combine(directory.FullName, "Resources");
    }
}
