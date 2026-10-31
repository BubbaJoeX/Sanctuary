using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Sanctuary.Scripting.Tests;

/// <summary>
/// The shipped zone scripts (Scripts/Zone), each loaded alone into a zone and sent the events the game fires.
/// </summary>
[TestClass]
public class ZoneScriptTests
{
    private const int ResurrectionScrollItemId = 11018;

    private ServiceProvider _serviceProvider = null!;
    private ScriptManager _scriptManager = null!;

    [TestInitialize]
    public void Setup()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ScriptManager>();

        _serviceProvider = services.BuildServiceProvider();
        _scriptManager = _serviceProvider.GetRequiredService<ScriptManager>();
        _scriptManager.Load();
    }

    [TestCleanup]
    public void Cleanup() => _serviceProvider.Dispose();

    private async Task<ScriptContext> LoadAsync(MockScriptZone zone, string script)
    {
        _scriptManager.GetOrCreateContext(zone, out var context);
        Assert.IsTrue(await context.LoadScriptAsync(Path.Combine(ScriptManager.BaseDirectory, "Zone", script + ".lua")), script);

        return context;
    }

    private static MockScriptZone CreateZone(params TestPlayer[] players)
    {
        var zone = new MockScriptZone(NullLogger.Instance);
        zone.Players.AddRange(players);

        return zone;
    }

    private static List<ulong> Group(params TestPlayer[] players) => [.. players.Select(player => player.Guid)];

    [TestMethod]
    public async Task Battle_WinningWithFriendsPaysATeamworkBonus()
    {
        var ann = new TestPlayer(1, "Ann");
        var bob = new TestPlayer(2, "Bob");
        var cal = new TestPlayer(3, "Cal");
        ann.GroupMemberGuids = bob.GroupMemberGuids = cal.GroupMemberGuids = Group(ann, bob, cal);

        var solo = new TestPlayer(4, "Solo");
        var zone = CreateZone(ann, bob, solo); // Cal isn't in the battle
        zone.BattleId = 3;
        var context = await LoadAsync(zone, "Battle");

        context.FireEvent("battleEnd", ann, 3, true);
        context.FireEvent("battleEnd", solo, 3, true);
        await context.WaitForCallbacksAsync();

        Assert.AreEqual(5, ann.Stars, "One friend (Bob) was there.");
        CollectionAssert.Contains(ann.Messages, "Teamwork bonus: +5 stars for winning with 1 friend.");
        Assert.AreEqual(0, solo.Stars);
        CollectionAssert.AreEqual(new[] { 5349 }, solo.Effects);
    }

    [TestMethod]
    public async Task Battle_LosingAfterAKnockOutGivesATip()
    {
        var ann = new TestPlayer(1, "Ann");
        var bob = new TestPlayer(2, "Bob");
        var zone = CreateZone(ann, bob);
        zone.BattleId = 3;
        var context = await LoadAsync(zone, "Battle");

        context.FireEvent("playerKnockedOut", ann);
        await context.WaitForCallbacksAsync();
        context.FireEvent("battleEnd", ann, 3, false);
        context.FireEvent("battleEnd", bob, 3, false);
        await context.WaitForCallbacksAsync();

        Assert.AreEqual(1, ann.Messages.Count(message => message.StartsWith("Tip: a friend with a Resurrection Scroll")));
        Assert.AreEqual(0, bob.Messages.Count, "Bob wasn't knocked out.");
        Assert.AreEqual(0, ann.Stars);
    }

    [TestMethod]
    public async Task Rescue_TellsTheGroupAndPointsScrollHoldersAtTheDownedPlayer()
    {
        var ann = new TestPlayer(1, "Ann") { Position = (0, 0, 0) };
        var bob = new TestPlayer(2, "Bob") { Position = (10, 0, 0) };
        var cal = new TestPlayer(3, "Cal") { Position = (200, 0, 0) };
        var dee = new TestPlayer(4, "Dee") { Position = (5, 0, 0) }; // not in the group
        bob.Inventory[ResurrectionScrollItemId] = 1;
        dee.Inventory[ResurrectionScrollItemId] = 1;
        ann.GroupMemberGuids = bob.GroupMemberGuids = cal.GroupMemberGuids = Group(ann, bob, cal);

        var context = await LoadAsync(CreateZone(ann, bob, cal, dee), "Rescue");

        context.FireEvent("playerKnockedOut", ann);
        await context.WaitForCallbacksAsync();

        CollectionAssert.AreEqual(new[] { "Ann was knocked out nearby! Use a Resurrection Scroll close to them to get them back up." }, bob.Messages);
        CollectionAssert.AreEqual(new[] { "Ann was knocked out, 200 m away." }, cal.Messages);
        Assert.AreEqual(0, dee.Messages.Count);
        CollectionAssert.AreEqual(new[] { "Hang on: Bob has a Resurrection Scroll." }, ann.Messages);
    }

    [TestMethod]
    public async Task Rescue_InABattleEveryoneThereCanHelp()
    {
        var ann = new TestPlayer(1, "Ann") { Position = (0, 0, 0) };
        var dee = new TestPlayer(4, "Dee") { Position = (5, 0, 0) };
        var zone = CreateZone(ann, dee);
        zone.BattleId = 3;
        var context = await LoadAsync(zone, "Rescue");

        context.FireEvent("playerKnockedOut", ann);
        await context.WaitForCallbacksAsync();

        CollectionAssert.AreEqual(new[] { "Ann was knocked out nearby!" }, dee.Messages);
        Assert.AreEqual(0, ann.Messages.Count, "Nobody nearby has a scroll.");
    }

    [TestMethod]
    public async Task Milestones_CheerEveryRankAndTheWholeZoneEveryFifth()
    {
        var ann = new TestPlayer(1, "Ann") { Position = (7, 0, 9) };
        var zone = CreateZone(ann);
        var context = await LoadAsync(zone, "Milestones");

        context.FireEvent("jobRankUp", ann, 35, 7);
        context.FireEvent("jobRankUp", ann, 45, 10);
        context.FireEvent("jobRankUp", ann, 137, 20);
        await context.WaitForCallbacksAsync();

        CollectionAssert.AreEqual(new[] { ("Ann reached rank 7 as an Archer.", 7f, 9f, 40f) }, zone.NearBroadcasts);
        CollectionAssert.AreEquivalent(new[] { "Ann is now a rank 10 Chef!", "Ann has mastered the Fisherman job, reaching rank 20!" }, zone.Broadcasts);
        CollectionAssert.AreEqual(new[] { 5349, 5349 }, ann.Effects);
    }

    [TestMethod]
    public async Task FishingPond_PaysEveryTenthCatchAndCheersTreasure()
    {
        var ann = new TestPlayer(1, "Ann");
        var zone = CreateZone(ann);
        var context = await LoadAsync(zone, "FishingPond");

        context.FireEvent("playerEnter", ann);
        await context.WaitForCallbacksAsync();

        for (var i = 0; i < 9; i++)
        {
            context.FireEvent("fishCaught", ann, "fish", 3012);
            await context.WaitForCallbacksAsync();
        }

        Assert.AreEqual(0, ann.Stars);

        context.FireEvent("fishCaught", ann, "treasure", 0);
        await context.WaitForCallbacksAsync();

        Assert.AreEqual(10, ann.Stars);
        CollectionAssert.Contains(ann.Messages, "That's 10 catches this trip! +10 bonus stars.");
        CollectionAssert.AreEqual(new[] { "Ann fished up a treasure chest!" }, zone.Broadcasts);
    }

    [TestMethod]
    public async Task FishingPond_DerbyPaysTheTopAnglersStillThere()
    {
        var ann = new TestPlayer(1, "Ann");
        var bob = new TestPlayer(2, "Bob");
        var cal = new TestPlayer(3, "Cal");
        var dee = new TestPlayer(4, "Dee");
        var zone = CreateZone(ann, bob, cal, dee);
        var context = await LoadAsync(zone, "FishingPond");

        context.FireEvent("eventStart", "FishingDerby");
        await context.WaitForCallbacksAsync();

        async Task Catch(TestPlayer player, int count, string kind = "fish")
        {
            for (var i = 0; i < count; i++)
            {
                context.FireEvent("fishCaught", player, kind, 3012);
                await context.WaitForCallbacksAsync();
            }
        }

        await Catch(ann, 2);
        await Catch(bob, 3);
        await Catch(cal, 1);
        await Catch(dee, 4);
        await Catch(ann, 5, "junk"); // junk doesn't count

        zone.Players.Remove(dee); // the leader leaves before the end

        context.FireEvent("eventEnd", "fishingderby");
        await context.WaitForCallbacksAsync();

        CollectionAssert.Contains(zone.Broadcasts, "Bob takes the lead in the Fishing Derby with 3 fish!");
        CollectionAssert.Contains(zone.Broadcasts, "Dee takes the lead in the Fishing Derby with 4 fish!");
        CollectionAssert.Contains(zone.Broadcasts, "1. Dee, 4 fish (gone before the prizes)");
        CollectionAssert.Contains(zone.Broadcasts, "2. Bob, 3 fish: 200 coins!");
        CollectionAssert.Contains(zone.Broadcasts, "3. Ann, 2 fish: 100 coins!");
        Assert.AreEqual(100, ann.Coins);
        Assert.AreEqual(200, bob.Coins);
        Assert.AreEqual(0, cal.Coins);
        Assert.AreEqual(0, dee.Coins);
    }

    [TestMethod]
    public async Task DailyBounties_ListTheDayAndPayEachOnce()
    {
        var ann = new TestPlayer(1, "Ann");
        var zone = CreateZone(ann);
        var mob = new MockScriptNpc(zone);
        var context = await LoadAsync(zone, "DailyBounties");

        context.FireEvent("playerEnter", ann);
        context.FireEvent("playerEnter", ann);
        await context.WaitForCallbacksAsync();

        Assert.AreEqual(1, ann.Messages.Count, "The day's bounties are told once.");
        StringAssert.StartsWith(ann.Messages[0], "Today's bounties: ");

        // Do more of everything than any bounty asks: the day's three pay once each, then the bonus.
        for (var i = 0; i < 30; i++)
        {
            context.FireEvent("mobKilled", mob, ann);
            context.FireEvent("minigameEnd", ann, 20, true);
            context.FireEvent("minigameEnd", ann, 20, false);
            context.FireEvent("challengeEnd", ann, 25, true);
            context.FireEvent("questComplete", ann, 100);
            context.FireEvent("itemCrafted", ann, 4, 1000);
            await context.WaitForCallbacksAsync();
        }

        Assert.AreEqual(3 * 100 + 150, ann.Coins);
        Assert.AreEqual(3 * 50, ann.Stars);
        Assert.AreEqual(3, ann.Messages.Count(message => message.StartsWith("Bounty done: ")));
        CollectionAssert.Contains(ann.Messages, "That's all of today's bounties! +150 bonus coins. There are new ones tomorrow.");
    }
}
