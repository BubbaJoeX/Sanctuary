using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Numerics;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sanctuary.Core.Helpers;
using Sanctuary.Database;
using Sanctuary.Database.Entities;
using Sanctuary.Database.Sqlite;
using Sanctuary.Game.Entities;
using Sanctuary.Game.Zones;

namespace Sanctuary.Game.Tests;

[TestClass]
public sealed class RefereePickTests
{
    private static readonly HashSet<int> PointsOfInterest = [4, 5];

    private static string GetResourcesDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Resources", "RefereePicks.json")))
            directory = directory.Parent;

        Assert.IsNotNull(directory, "Could not find the Resources directory.");

        return Path.Combine(directory.FullName, "Resources");
    }

    private static DbRefereePick Pick(string title = "Title", string text = "Text", string action = "Marketplace", int param1 = 0) =>
        new() { Title = title, Text = text, Action = action, Param1 = param1, Icon = "coin" };

    /// <summary>
    /// A migrated in-memory database that lives as long as the returned connection.
    /// </summary>
    private static (SqliteConnection Connection, IDbContextFactory<DatabaseContext> Factory) CreateDatabase()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<DatabaseContext>().UseSqlite(connection).Options;
        var factory = new SqliteDatabaseFactory(options);

        using (var dbContext = factory.CreateDbContext())
            dbContext.Database.Migrate();

        return (connection, factory);
    }

    [TestMethod]
    public void Validate_AcceptsEveryKind()
    {
        foreach (var kind in RefereePickHelper.Kinds.Values)
        {
            var pick = Pick(action: kind.Action, param1: kind.Action == "Teleport" ? 5 : 0);

            Assert.IsNull(RefereePickHelper.Validate(pick, PointsOfInterest.Contains), kind.Action);
        }
    }

    [TestMethod]
    public void Validate_RefusesBadPicks()
    {
        Assert.IsNotNull(RefereePickHelper.Validate(Pick(action: "Teleport", param1: 999), PointsOfInterest.Contains));
        Assert.IsNotNull(RefereePickHelper.Validate(Pick(action: "Logout"), PointsOfInterest.Contains));
        Assert.IsNotNull(RefereePickHelper.Validate(Pick(title: " "), PointsOfInterest.Contains));
        Assert.IsNotNull(RefereePickHelper.Validate(Pick(title: new string('a', RefereePickHelper.MaxTitleLength + 1)), PointsOfInterest.Contains));
        Assert.IsNotNull(RefereePickHelper.Validate(Pick(text: new string('a', RefereePickHelper.MaxTextLength + 1)), PointsOfInterest.Contains));
    }

    [TestMethod]
    public void WelcomeCardActions_AreThePickKinds()
    {
        CollectionAssert.AreEquivalent(
            RefereePickHelper.Kinds.Values.Select(kind => kind.Action).ToList(),
            WelcomeCard.Actions.Keys.ToList());
    }

    [TestMethod]
    public void Migration_SeedsTheStockPicks()
    {
        var (connection, factory) = CreateDatabase();

        using (connection)
        using (var dbContext = factory.CreateDbContext())
        {
            var file = Path.Combine(GetResourcesDirectory(), "RefereePicks.json");
            var defaults = RefereePickHelper.LoadDefaults(file, PointsOfInterest.Contains);
            var seeded = RefereePickHelper.Load(dbContext);

            Assert.AreEqual(RefereePickHelper.MaxPicks, defaults.Count, "Every stock pick should be valid.");

            // The migration copies RefereePicks.json, so a fresh server starts with the picks !pick reset restores.
            CollectionAssert.AreEqual(
                defaults.Select(x => (x.Title, x.Text, x.Action, x.Param1, x.Icon, x.Button)).ToList(),
                seeded.Select(x => (x.Title, x.Text, x.Action, x.Param1, x.Icon, x.Button)).ToList());
        }
    }

    [TestMethod]
    public void Replace_KeepsOrderUpToMaxPicks()
    {
        var (connection, factory) = CreateDatabase();

        using (connection)
        using (var dbContext = factory.CreateDbContext())
        {
            var picks = Enumerable.Range(1, RefereePickHelper.MaxPicks + 1).Select(i => Pick(title: $"Pick {i}")).ToList();

            RefereePickHelper.Replace(dbContext, picks, null);

            CollectionAssert.AreEqual(
                picks.Take(RefereePickHelper.MaxPicks).Select(x => x.Title).ToList(),
                RefereePickHelper.Load(dbContext).Select(x => x.Title).ToList());
        }
    }

    [TestMethod]
    public void Manager_PicksUpOutsideEdits()
    {
        var (connection, factory) = CreateDatabase();

        Directory.CreateDirectory(ResourceManager.BaseDirectory);

        using (connection)
        using (var resourceManager = new ResourceManager(NullLogger<ResourceManager>.Instance))
        {
            var services = new ServiceCollection().AddSingleton<IZoneManager, NoZoneManager>().BuildServiceProvider();
            var manager = new RefereePickManager(NullLogger<RefereePickManager>.Instance, services, resourceManager, factory);

            Assert.AreEqual(RefereePickHelper.MaxPicks, manager.Current.Count);
            Assert.IsFalse(manager.TryAdd(new WelcomeCard("Full", "No room"), null));

            // An admin site save, straight to the table.
            using (var dbContext = factory.CreateDbContext())
                RefereePickHelper.Replace(dbContext, [Pick(title: "From the web")], null);

            Assert.IsTrue(manager.Refresh());
            Assert.IsFalse(manager.Refresh());
            Assert.AreEqual("From the web", manager.Current.Single().Title);

            Assert.IsTrue(manager.TryAdd(new WelcomeCard("From !pick", "In game", "Communicator", Icon: "friends"), 7));
            Assert.IsTrue(manager.TryRemoveAt(0));
            Assert.AreEqual("From !pick", manager.Current.Single().Title);

            manager.Clear();
            Assert.AreEqual(0, manager.Current.Count);
        }
    }

    private sealed class NoZoneManager : IZoneManager
    {
        public int StartingZoneDefinitionId => throw new NotSupportedException();
        public WorldZone StartingZone => throw new NotSupportedException();
        public IEnumerable<IZone> Zones => [];

        public bool Load() => throw new NotSupportedException();

        public bool TryGetPlayer(ulong guid, [MaybeNullWhen(false)] out Player player) => throw new NotSupportedException();
        public bool TryGetPlayer(string name, [MaybeNullWhen(false)] out Player player) => throw new NotSupportedException();

        public bool TryMovePlayerToZone(int zoneDefinitionId, ulong? ownerId, Player player, Vector4 position, Quaternion rotation, out IZone zone) =>
            throw new NotSupportedException();

        public bool TryRemoveZoneInstance(IZone zone) => throw new NotSupportedException();
        public void EvictIfEmpty(IZone zone) => throw new NotSupportedException();
    }
}
