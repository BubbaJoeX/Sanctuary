using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sanctuary.Game.Minigames;
using Sanctuary.Game.Resources;

namespace Sanctuary.Game.Tests;

[TestClass]
public sealed class MinigameCatalogTests
{
    private static readonly string Resources = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Resources"));

    private static readonly string MinigamesDirectory = Path.Combine(Resources, "Minigames");

    /// <summary>
    /// ActivityCategories.txt: the Games window's tiles.
    /// </summary>
    private static readonly int[] Categories = [10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 26];

    private static MinigameDefinitionCollection LoadCatalog()
    {
        var catalog = new MinigameDefinitionCollection(NullLogger.Instance);

        Assert.IsTrue(catalog.Load(MinigamesDirectory));

        return catalog;
    }

    [TestMethod]
    public void EveryEntryLoads_WithAnIdOfItsOwn()
    {
        var catalog = LoadCatalog();

        var options = new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip };
        var ids = Directory.GetFiles(MinigamesDirectory, "*.json")
            .SelectMany(file => JsonDocument.Parse(File.ReadAllText(file), options).RootElement.EnumerateArray()
                .Select(entry => entry.GetProperty("Id").GetInt32()))
            .ToList();

        var duplicates = ids.GroupBy(id => id).Where(group => group.Count() > 1).Select(group => group.Key).ToList();

        Assert.AreEqual(0, duplicates.Count, $"Ids in more than one entry: {string.Join(", ", duplicates)}");
        Assert.AreEqual(ids.Count, catalog.Count, "Every entry passed validation.");
    }

    [TestMethod]
    public void EveryEntry_HasAServer_AndATileOrNone()
    {
        var games = typeof(FlashMinigame).Assembly.GetTypes()
            .Select(type => type.GetCustomAttribute<FlashMinigameAttribute>()?.Name)
            .OfType<string>()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var definition in LoadCatalog().Values)
        {
            Assert.IsTrue(games.Contains(definition.Game), $"{definition.Id} ({definition.Comment}) runs on \"{definition.Game}\", which nothing registers.");
            Assert.IsTrue(definition.Category == 0 || Categories.Contains(definition.Category), $"{definition.Id} ({definition.Comment}) is on tile {definition.Category}.");
            Assert.IsTrue(definition.Swf.EndsWith(".swf", StringComparison.OrdinalIgnoreCase), $"{definition.Id} ({definition.Comment}) loads \"{definition.Swf}\".");
            Assert.IsTrue(definition.NameId > 0, $"{definition.Id} ({definition.Comment}) has no name.");
            Assert.IsFalse(string.IsNullOrWhiteSpace(definition.Comment), $"{definition.Id} has no English name.");
        }
    }

    [TestMethod]
    public void EveryRewardItem_IsARealItem()
    {
        var items = new ClientItemDefinitionCollection(NullLogger.Instance);
        Assert.IsTrue(items.Load(Path.Combine(Resources, "ClientItemDefinitions.json")));

        foreach (var definition in LoadCatalog().Values)
        {
            foreach (var item in definition.Items)
                Assert.IsTrue(items.ContainsKey(item.ItemId), $"{definition.Id} ({definition.Comment}) rewards item {item.ItemId}, which doesn't exist.");
        }
    }
}
