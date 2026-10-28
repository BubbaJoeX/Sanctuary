using System.Collections.Generic;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sanctuary.Game.Minigames;
using Sanctuary.Game.Minigames.Soe;
using Sanctuary.Game.Resources.Definitions;

namespace Sanctuary.Game.Tests;

[TestClass]
public sealed class MinigameFrameworkTests
{
    /// <summary>
    /// Two timed levels; the second ends the game won.
    /// </summary>
    private sealed class TwoLevelGame : SoeGameServer
    {
        protected override void GameInitialize()
        {
            for (var level = 1; level <= 2; level++)
            {
                var soeLevel = new SoeLevel(this);
                soeLevel.SetEndTime(10);
                AddLevel(level, soeLevel);
            }
        }

        protected override void GameApplyTick(long delta)
        {
            if (IsInProgress && !IsPaused && CheckLevelDone())
                StopLevel();
        }

        protected override void OnNextLevel()
        {
            SendNextLevel(Level, 0, 0);
        }

        protected override void OnStopLevel()
        {
            SendStopLevel(Level, 0, CurrentLevel.Score, 100);
        }

        protected override bool OnGameMessage(string name, FlashArgs args) => false;
    }

    [TestMethod]
    public void Messages_AreTabSeparated_WithListsJoinedByCommas()
    {
        Assert.AreEqual("OnBuildMapMsg\t7\t8\t1,2,3", FlashMinigame.FormatMessage("OnBuildMapMsg", 7, 8, new[] { 1, 2, 3 }));
        Assert.AreEqual("OnActiveMsg\ttrue", FlashMinigame.FormatMessage("OnActiveMsg", true));
        Assert.AreEqual("OnSpeedMsg\t1.5\tundefined", FlashMinigame.FormatMessage("OnSpeedMsg", 1.5, null));
    }

    [TestMethod]
    public void ParseMessage_DropsTheMoviesPadding()
    {
        var (name, args) = MinigameManager.ParseMessage("OnChainMsg\t1,2,3\t\u0000 ");

        Assert.AreEqual("OnChainMsg", name);
        CollectionAssert.AreEqual(new[] { 1, 2, 3 }, args.IntList(0));
        Assert.IsTrue(new FlashArgs(["true"]).Bool(0));
        Assert.AreEqual(4, new FlashArgs(["4"]).Int(0));
    }

    [TestMethod]
    public void SoeGameServer_PlaysItsLevels_OnItsClock_AndPausesTheTimer()
    {
        var now = 0L;
        var sent = new List<string>();
        bool? result = null;

        var game = FlashMinigame.CreateDetached<TwoLevelGame>(new MinigameDefinition { Id = 1, Game = "Test" }, "",
            sent.Add, won => result = won, () => now);

        game.OnStart();
        game.OnMessage("OnConnectMsg", default);
        CollectionAssert.Contains(sent, "OnServerReadyMsg");

        game.OnMessage("OnStartGameMsg", new FlashArgs(["0"]));
        CollectionAssert.Contains(sent, "OnNextLevelMsg\t1\t0\t0");

        game.OnMessage("OnStartLevelMsg", default);

        // Paused for 20 s of the level's 10: the timer stands still.
        now = 1000;
        game.OnPause(true);
        now = 21000;
        game.Tick();
        Assert.IsFalse(sent.Exists(x => x.StartsWith("OnStopLevelMsg")));

        game.OnPause(false);
        now = 31000;
        game.Tick();
        CollectionAssert.Contains(sent, "OnStopLevelMsg\t1\t0\t0\t100");

        game.OnMessage("OnNextLevelMsg", default);
        CollectionAssert.Contains(sent, "OnNextLevelMsg\t2\t0\t0");

        game.OnMessage("OnStartLevelMsg", default);
        now = 42000;
        game.Tick();
        game.OnMessage("OnNextLevelMsg", default);

        CollectionAssert.Contains(sent, "OnGameWonMsg");
        Assert.AreEqual(true, result);
        Assert.IsTrue(game.Finished);
    }

    [TestMethod]
    public void SoeEventQueue_FiresDueEvents_InOrder_AndRecurringOnesAgain()
    {
        var now = 0L;
        var fired = new List<string>();
        var queue = new SoeEventQueue(() => now) { OnEvent = (_, type, _) => fired.Add(type) };

        queue.AddEvent("b", 200);
        queue.AddEvent("a", 100);
        queue.AddEvent("tick", 150, recur: true);

        now = 320;
        queue.Update();

        CollectionAssert.AreEqual(new[] { "a", "tick", "b" }, fired);

        now = 480;
        queue.Update();
        CollectionAssert.AreEqual(new[] { "a", "tick", "b", "tick" }, fired);
    }
}
