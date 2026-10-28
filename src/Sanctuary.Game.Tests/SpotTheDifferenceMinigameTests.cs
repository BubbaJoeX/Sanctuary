using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sanctuary.Game.Minigames;
using Sanctuary.Game.Minigames.SpotTheDifference;
using Sanctuary.Game.Resources.Definitions;

namespace Sanctuary.Game.Tests;

[TestClass]
public sealed class SpotTheDifferenceMinigameTests
{
    private long _now;
    private List<string> _sent = null!;
    private bool? _result;

    [TestInitialize]
    public void Initialize()
    {
        _now = 0;
        _sent = [];
        _result = null;
    }

    /// <summary>
    /// The game's data in the repository: Resources/Minigames/SpotTheDifference.
    /// </summary>
    private static string DataDirectory
    {
        get
        {
            for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            {
                var candidate = Path.Combine(directory.FullName, "Resources", "Minigames", "SpotTheDifference");

                if (File.Exists(Path.Combine(candidate, SpotTheDifferencePuzzles.FileName)))
                    return candidate;
            }

            throw new DirectoryNotFoundException("Resources/Minigames/SpotTheDifference wasn't found.");
        }
    }

    private SpotTheDifferenceGame Start(int level = 1, string? dataDirectory = null)
    {
        var definition = new MinigameDefinition { Id = 1040, Comment = "Spot the Difference", Game = "SpotTheDifference", Level = level };
        var game = FlashMinigame.CreateDetached<SpotTheDifferenceGame>(definition, dataDirectory ?? DataDirectory, _sent.Add,
            won => _result = won, () => _now);

        game.OnStart();
        game.OnMessage("OnConnectMsg", default);

        CollectionAssert.AreEqual(new[] { "OnActiveMsg\tfalse", "OnServerReadyMsg" }, _sent);

        _sent.Clear();
        game.OnMessage("OnStartGameMsg", new FlashArgs(["0"]));

        return game;
    }

    private static void Click(SpotTheDifferenceGame game, int x, int y)
    {
        game.OnMessage("OnReleaseMsg", new FlashArgs([x.ToString(), y.ToString()]));
    }

    private static (int X, int Y) Middle(SpotTheDifferenceSpot spot) => ((spot.Left + spot.Right) / 2, (spot.Top + spot.Bottom) / 2);

    /// <summary>
    /// A point on the picture outside every difference.
    /// </summary>
    private static (int X, int Y) Miss(SpotTheDifferenceRound round)
    {
        for (var y = 0; y < 312; y += 4)
        {
            for (var x = 0; x < 380; x += 4)
            {
                if (round.Spots.All(spot => !spot.Contains(x, y)))
                    return (x, y);
            }
        }

        throw new InvalidOperationException("The picture is all differences.");
    }

    [TestMethod]
    public void Puzzles_AreThe_MoviesBoards()
    {
        var puzzles = SpotTheDifferencePuzzles.Load(Path.Combine(DataDirectory, SpotTheDifferencePuzzles.FileName));

        CollectionAssert.AreEqual(Enumerable.Range(4, 9).ToArray(), puzzles.Boards.Select(board => board.Frame).ToArray());
        CollectionAssert.AreEqual(new[] { 1, 2, 3 }, puzzles.Levels.Select(level => level.Level).ToArray());

        foreach (var board in puzzles.Boards)
        {
            Assert.AreEqual(5, board.Differences.Count);
            Assert.IsTrue(board.Differences.All(spot => spot.Left < spot.Right && spot.Top < spot.Bottom && spot.Right <= 380 && spot.Bottom <= 312));
        }

        foreach (var level in puzzles.Levels)
        {
            Assert.AreEqual(level.Rounds, level.Frames.Count);
            Assert.IsTrue(level.Frames.All(frame => puzzles.Boards.Exists(board => board.Frame == frame)));
        }
    }

    [TestMethod]
    public void PlaysFiveRounds_ThroughTheMoviesMessages_AndWins()
    {
        var game = Start();
        var settings = game.Settings!;

        for (var level = 1; level <= settings.Rounds; level++)
        {
            // OnNextLevelMsg (level, goal, best), then the movie's OnStartLevelMsg starts the round.
            Assert.AreEqual($"OnNextLevelMsg\t{level}\t5\t0", _sent[^1]);

            _sent.Clear();
            game.OnMessage("OnStartLevelMsg", default);

            var round = game.Round!;
            Assert.AreEqual(settings.Frames[level - 1], round.Board.Frame);

            CollectionAssert.AreEqual(new[]
            {
                $"OnScoreMsg\t0\t0",
                $"OnLevelInitMsg\t{round.Board.Frame}\t{settings.RoundSeconds}\t5\t0\t0",
                "OnLevelHourglassInitMsg\t0\t0",
                $"OnAddItemMsg\t0\t118186\t0",
                $"OnAddItemMsg\t1\t118187\t0",
                $"OnAddItemMsg\t2\t118188\t0",
                $"OnAddItemMsg\t3\t118189\t0",
                $"OnAddItemMsg\t4\t118190\t0",
                "OnActiveMsg\ttrue"
            }, _sent);

            // A miss, then every difference; a found one doesn't count twice.
            _sent.Clear();
            var (missX, missY) = Miss(round);
            Click(game, missX, missY);
            Assert.AreEqual("OnLevelEventMsg\t12", _sent[^1]);

            for (var i = 0; i < round.Spots.Count; i++)
            {
                _now += 1000;
                _sent.Clear();

                var (x, y) = Middle(round.Spots[i]);
                Click(game, x, y);

                Assert.AreEqual($"OnLevelEventMsg\t11\t{i}\t{round.Spots[i].Clip}\t{round.Spots[i].NameId}", _sent[0]);
                Assert.AreEqual($"OnScoreMsg\t{i + 1}\t{i + 1}", _sent[1]);
                Assert.IsTrue(_sent[2].StartsWith("OnLevelEventMsg\t14\t"), _sent[2]);

                if (i == 0)
                {
                    _sent.Clear();
                    Click(game, x, y);
                    Assert.AreEqual("OnLevelEventMsg\t12", _sent[^1]);
                }
            }

            Assert.IsTrue(round.IsCleared);

            // The round ends on the next tick; the movie asks for the next one from its round-end screen.
            _sent.Clear();
            game.Tick();
            CollectionAssert.AreEqual(new[] { "OnActiveMsg\tfalse", "OnStopLevelMsg\t" + level + "\t5\t5\t100" }, _sent);

            _sent.Clear();
            game.OnMessage("OnNextLevelMsg", default);
        }

        CollectionAssert.AreEqual(new[] { "OnGameWonMsg" }, _sent);
        Assert.AreEqual(true, _result);
        Assert.IsTrue(game.Score > 0);
    }

    [TestMethod]
    public void RunningOutOfTime_LosesTheGame()
    {
        var game = Start(level: 3);
        game.OnMessage("OnStartLevelMsg", default);

        var round = game.Round!;
        var (x, y) = Middle(round.Spots[0]);
        Click(game, x, y);

        // Asking for the next round while this one runs does nothing.
        _sent.Clear();
        game.OnMessage("OnNextLevelMsg", default);
        Assert.AreEqual(0, _sent.Count);

        _now += game.Settings!.RoundSeconds * 1000L + 1;
        game.Tick();
        CollectionAssert.AreEqual(new[] { "OnActiveMsg\tfalse", "OnStopLevelMsg\t1\t5\t1\t20" }, _sent);

        // Clicks after the round are ignored.
        _sent.Clear();
        Click(game, x, y);
        Assert.AreEqual(0, _sent.Count);

        game.OnMessage("OnNextLevelMsg", default);
        CollectionAssert.AreEqual(new[] { "OnGameLostMsg" }, _sent);
        Assert.AreEqual(false, _result);
    }

    [TestMethod]
    public void MissesInARow_CostTime()
    {
        var game = Start(level: 2);
        game.OnMessage("OnStartLevelMsg", default);

        var round = game.Round!;
        var settings = game.Settings!;
        var (x, y) = Miss(round);

        var before = round.GetTimeLeft();
        _sent.Clear();

        for (var i = 0; i < settings.WrongClicks; i++)
            Click(game, x, y);

        // The movie's LEVEL_CLICK_WRONG: the seconds lost and where, after plain misses.
        CollectionAssert.AreEqual(Enumerable.Repeat("OnLevelEventMsg\t12", settings.WrongClicks - 1)
            .Append($"OnLevelEventMsg\t13\t{settings.TimePenalty}\t{x}\t{y}").ToArray(), _sent);

        Assert.AreEqual(before - settings.TimePenalty * 1000L, round.GetTimeLeft());
        Assert.AreEqual(0, round.WrongCount);
    }

    [TestMethod]
    public void Hints_PointOutADifference_UntilTheyRunOut()
    {
        var directory = Path.Combine(Path.GetTempPath(), "SpotTheDifferenceTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        try
        {
            File.WriteAllText(Path.Combine(directory, SpotTheDifferencePuzzles.FileName), """
                {
                  // One picture, two differences, one hint.
                  "Boards": [
                    { "Frame": 4, "Differences": [
                      { "Clip": "m1", "NameId": 118186, "Left": 10, "Top": 10, "Right": 20, "Bottom": 20 },
                      { "Clip": "m2", "NameId": 118187, "Left": 30, "Top": 30, "Right": 40, "Bottom": 40 }
                    ] }
                  ],
                  "Levels": [ { "Level": 1, "Rounds": 1, "Frames": [ 4 ], "RoundSeconds": 30, "HintsMax": 3, "HintsAtStart": 1 } ]
                }
                """);

            var game = Start(dataDirectory: directory);
            game.OnMessage("OnStartLevelMsg", default);

            Assert.IsTrue(_sent.Contains("OnLevelInitMsg\t4\t30\t2\t1\t3"));

            Click(game, 15, 15);

            _sent.Clear();
            game.OnMessage("OnHintRequestMsg", default);
            CollectionAssert.AreEqual(new[] { "OnHintUsedMsg\t0\tm2" }, _sent);

            _sent.Clear();
            game.OnMessage("OnHintRequestMsg", default);
            Assert.AreEqual(0, _sent.Count);
            Assert.AreEqual(0, game.HintsLeft);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
