using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sanctuary.Game.Minigames;
using Sanctuary.Game.Minigames.MatchThree;
using Sanctuary.Game.Resources;
using Sanctuary.Game.Resources.Definitions;

namespace Sanctuary.Game.Tests;

[TestClass]
public sealed class MatchThreeMinigameTests
{
    private static readonly string Resources = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Resources"));
    private static readonly string MinigamesDirectory = Path.Combine(Resources, "Minigames");
    private static readonly string DataDirectory = Path.Combine(MinigamesDirectory, "MatchThree");

    /// <summary>
    /// What each movie can draw: its bins (mBin1-5, forging's mBin1-2), its selectable art (SelectableTile1-N), its
    /// collectable art (by the client's TileGenerator names) and whether it has gems.
    /// </summary>
    private static readonly Dictionary<string, (int Bins, int Selectables, int Collectables, bool Bonus)> Movies = new()
    {
        ["game_mining.swf"] = (5, 5, 15, true),
        ["game_forging.swf"] = (2, 5, 28, false),
        ["game_halloween.swf"] = (5, 5, 1, true),
        ["game_mailsorting.swf"] = (5, 8, 0, false),
        ["game_artifact.swf"] = (5, 11, 0, false),
        ["game_farming.swf"] = (5, 16, 0, false)
    };

    private static List<MinigameDefinition> LoadCatalog()
    {
        var minigames = new MinigameDefinitionCollection(NullLogger.Instance);
        Assert.IsTrue(minigames.Load(MinigamesDirectory));

        return minigames.Values.Where(x => x.Game == "MatchThree").OrderBy(x => x.Id).ToList();
    }

    private static MinigameDefinition Definition(int id) => LoadCatalog().Single(x => x.Id == id);

    [TestMethod]
    public void Catalog_ListsTheGames_WithLevelsTheirMoviesCanDraw()
    {
        var catalog = LoadCatalog();
        var itemIds = LoadItemIds();

        Assert.IsTrue(catalog.Count > 90, "the match-three games are listed");
        Assert.IsTrue(MinigameManagerGames().Contains("MatchThree"));

        foreach (var definition in catalog)
        {
            var name = $"{definition.Id} ({definition.Comment})";

            Assert.IsTrue(Movies.TryGetValue(definition.Swf, out var movie), $"{name} plays a match-three movie");
            Assert.IsTrue(definition.Category is 0 or 14 or 15 or 18 or 19, $"{name} is under a match-three tile");
            Assert.IsTrue(definition.DetailImage.EndsWith("_detail.dds") && definition.ThumbnailImage.EndsWith("_thumb.dds"), name);
            Assert.IsTrue(definition.Items.All(x => itemIds.Contains(x.ItemId)), $"{name} pays items that exist");

            var sent = new List<string>();
            var game = FlashMinigame.CreateDetached<MatchThreeGame>(definition, DataDirectory, sent.Add);
            game.OnStart();
            game.OnMessage("OnConnectMsg", default);

            CollectionAssert.Contains(sent, "OnServerReadyMsg", $"{name} has its data");
            Assert.IsTrue(game.LevelCount > 0, name);

            foreach (var level in game.Levels)
            {
                var levelName = $"{name} level {level.Index}";
                var shape = level.GridShape;

                Assert.IsTrue(shape.Length > 0 && shape.All(row => row.Length == shape[0].Length), $"{levelName} has a board");
                Assert.IsTrue(shape[0].All(x => x == 0) && shape.All(row => row[0] == 0), $"{levelName} keeps row 0 and column 0 empty");
                Assert.IsTrue(level.Type.HasFlag(MatchThreeLevelType.StopAtQuota), levelName);
                Assert.IsTrue(level.Bins.Count() <= movie.Bins, $"{levelName} fits its movie's bins");
                Assert.IsTrue(level.Bins.Any(x => x.Quota > 0), $"{levelName} has a quota");
                Assert.IsTrue(level.SelectableSpawnData.Keys.All(x => x >= 1 && x <= movie.Selectables), $"{levelName} has selectable art");
                Assert.IsTrue(level.CollectableSpawnData.Keys.All(x => x >= 0 && x < movie.Collectables), $"{levelName} has collectable art");
                Assert.IsTrue(movie.Bonus || level.BonusSpawnData.Count == 0, $"{levelName} has gem art");
                Assert.IsTrue(level.PowerupSpawnData.Keys.All(x => x is MatchThreeTiles.PowerupFreeze or >= MatchThreeTiles.PowerupPressMe),
                    $"{levelName} only has powerups with art");

                // Collected kinds (collectables, gems, blockers, the time freeze) leave only through a drop zone.
                var dropsTiles = level.CollectableSpawnData.Count > 0 || level.BonusSpawnData.Count > 0 || level.BlockerSpawnChance > 0
                    || level.PowerupSpawnData.ContainsKey(MatchThreeTiles.PowerupFreeze);

                Assert.AreEqual(dropsTiles, shape.Any(row => row.Contains(4)), $"{levelName} has a drop zone if it needs one");
            }
        }
    }

    [TestMethod]
    public void Mining_SendsTheLevel_CollectsOreInTheDropZone_AndWins()
    {
        var bot = new Bot(Definition(91), seed: 1);

        bot.Connect();
        CollectionAssert.Contains(bot.Sent, "OnActiveMsg\tfalse");
        CollectionAssert.Contains(bot.Sent, "OnServerReadyMsg");

        bot.StartGame();
        Assert.AreEqual("OnNextLevelMsg\t1\t0\t0", bot.Sent[^1]);

        var start = bot.Sent.Count;
        bot.StartLevel();
        var level = bot.Sent.Skip(start).ToList();

        // The art for each value, then the board's tiles, the board, more tiles and the random numbers.
        CollectionAssert.Contains(level, "OnTileInfoMsg\t613\t13");
        CollectionAssert.Contains(level, "OnTileInfoMsg\t614\t14");
        CollectionAssert.Contains(level, "OnTileInfoMsg\t401\t1");

        var tileQueue = level.First(x => x.StartsWith("OnTileQueueMsg\t"));
        var buildMap = level.Single(x => x.StartsWith("OnBuildMapMsg\t"));
        Assert.IsTrue(level.IndexOf(tileQueue) < level.IndexOf(buildMap));
        Assert.AreEqual(12 * 12, tileQueue.Split('\t')[2].Split(',').Length, "the movie builds the board off the tile queue");
        StringAssert.StartsWith(buildMap, "OnBuildMapMsg\t12\t12\t0,0,0,");
        Assert.AreEqual(70, level.First(x => x.StartsWith("OnRandomQueueMsg\t")).Split('\t')[1].Split(',').Length);

        CollectionAssert.Contains(level, "OnLevelInitMsg\t1\t75\t2");
        CollectionAssert.Contains(level, "OnQuotaInitMsg\t0\t613\t3\t1\t698");
        CollectionAssert.Contains(level, "OnQuotaInitMsg\t1\t614\t1\t1\t698");
        Assert.IsFalse(level.Any(x => x.StartsWith("OnQuotaInitMsg\t2")), "the rocks have no bins");
        CollectionAssert.Contains(level, "OnLevelEventMsg\t32\t0\t4");
        CollectionAssert.Contains(level, "OnPowerupInitMsg\t900\t5\t0");
        CollectionAssert.Contains(level, "OnPowerupInitMsg\t950\t0\t6");
        Assert.AreEqual("OnActiveMsg\ttrue", level[^1]);

        // A chain: two seconds a tile, 400 points and 200 for each tile past three.
        start = bot.Sent.Count;
        Assert.IsTrue(bot.PlayChain());
        var chain = bot.Sent.Skip(start).ToList();
        var length = bot.LastChain;
        var points = 400 + (length - 3) * 200;
        CollectionAssert.Contains(chain, $"OnLevelEventMsg\t25\t{2 * length}\t1");
        CollectionAssert.Contains(chain, $"OnLevelEventMsg\t21\t{points}\t0");
        Assert.AreEqual(points, bot.Game.Score);

        bot.PlayLevel();

        CollectionAssert.Contains(bot.Sent, "OnQuotaUpdateMsg\t0\t3\t3\t1");
        CollectionAssert.Contains(bot.Sent, "OnQuotaUpdateMsg\t1\t1\t1\t1");
        Assert.AreEqual("OnStopLevel\t1\ttrue", bot.Sent.Last(x => x.StartsWith("OnStopLevel")));
        Assert.AreEqual("OnActiveMsg\ttrue", bot.Sent.Last(x => x.StartsWith("OnActiveMsg")),
            "the movie takes OnActiveMsg false as a pause, which would stop it asking for the next level");
        Assert.IsTrue(bot.Fell > 0, "the ore fell into the drop zone");
        Assert.IsNull(bot.Result, "the game waits for the movie to show its success");

        bot.NextLevel();

        CollectionAssert.Contains(bot.Sent, "OnGameWonMsg");
        Assert.AreEqual(true, bot.Result);
        Assert.IsTrue(bot.Game.Finished && bot.Game.Won);
        Assert.IsFalse(bot.Sent.Contains("OnGameStoppedMsg"));
    }

    /// <summary>
    /// One game of each movie, through every level.
    /// </summary>
    [TestMethod]
    [DataRow(99, 1, DisplayName = "Gold Mining (game_mining)")]
    [DataRow(344, 1, DisplayName = "Rare Sunstone Gold Vein (game_mining, gems)")]
    [DataRow(138, 1, DisplayName = "Forging: Quick Shot Bow (game_forging)")]
    [DataRow(380, 3, DisplayName = "Shrouded Glade Mail Sorting (game_mailsorting)")]
    [DataRow(111, 2, DisplayName = "Farnum's Farm Harvesting (game_farming)")]
    [DataRow(681, 3, DisplayName = "Ambrose's Dig (game_artifact)")]
    [DataRow(712, 1, DisplayName = "Goo'd Candy (game_halloween)")]
    public void Game_PlaysEveryLevel_AndIsWon(int id, int levels)
    {
        var definition = Definition(id);
        var bot = new Bot(definition, seed: id);

        bot.Connect();
        bot.StartGame();

        for (var level = 1; level <= levels; level++)
        {
            Assert.AreEqual($"OnNextLevelMsg\t{level}\t0\t0", bot.Sent.Last(x => x.StartsWith("OnNextLevelMsg")));

            bot.StartLevel();
            bot.PlayLevel();

            Assert.AreEqual($"OnStopLevel\t{level}\ttrue", bot.Sent.Last(x => x.StartsWith("OnStopLevel")), $"{definition.Comment} level {level}");

            bot.NextLevel();
        }

        CollectionAssert.Contains(bot.Sent, "OnGameWonMsg");
        Assert.AreEqual(true, bot.Result);
        Assert.IsTrue(bot.Game.Score > 0);
        Assert.IsFalse(bot.Sent.Contains("OnGameStoppedMsg"));
    }

    /// <summary>
    /// Every game in the catalog can be played through and won (the bot presses powerups too).
    /// </summary>
    [TestMethod]
    public void EveryGame_CanBePlayedThrough()
    {
        foreach (var definition in LoadCatalog())
        {
            for (var seed = 0; seed < SeedsPerGame; seed++)
            {
                var bot = new Bot(definition, seed: definition.Id * 31 + seed) { PreferPowerups = seed % 2 == 1 };

                bot.Connect();
                bot.StartGame();

                for (var level = 1; level <= bot.Game.LevelCount; level++)
                {
                    bot.StartLevel();
                    bot.PlayLevel();
                    Assert.AreEqual($"OnStopLevel	{level}	true", bot.Sent.Last(x => x.StartsWith("OnStopLevel")),
                        $"{definition.Id} ({definition.Comment}) level {level}, seed {seed}");
                    bot.NextLevel();
                }

                Assert.AreEqual(true, bot.Result, $"{definition.Id} ({definition.Comment}), seed {seed}");
            }
        }
    }

    private const int SeedsPerGame = 3;

    [TestMethod]
    public void Level_RunsOutOfTime_BeforeItsQuota_AndTheGameIsLost()
    {
        var bot = new Bot(Definition(91), seed: 2);

        bot.Connect();
        bot.StartGame();
        bot.StartLevel();

        // Paused (the movie's menu) the timer stands still.
        bot.Now += 1000;
        bot.Game.OnPause(true);
        CollectionAssert.Contains(bot.Sent, "OnPauseMsg\tfalse");
        bot.Now += 120_000;
        bot.Game.Tick();
        Assert.IsFalse(bot.Sent.Any(x => x.StartsWith("OnStopLevel")));

        bot.Game.OnPause(false);
        bot.Now += 76_000;
        bot.Game.Tick();

        Assert.AreEqual("OnStopLevel\t1\tfalse", bot.Sent.Last(x => x.StartsWith("OnStopLevel")));
        Assert.AreEqual(false, bot.Result);

        // The movie plays its failure and ends the game itself.
        Assert.IsFalse(bot.Sent.Contains("OnGameLostMsg"));

        bot.Send("OnNextLevelMsg");
        Assert.IsFalse(bot.Sent.Contains("OnGameWonMsg"), "a lost game has no next level");
    }

    [TestMethod]
    public void Chain_ThatTheBoardCantHave_StopsTheGame()
    {
        var bot = new Bot(Definition(91), seed: 3);

        bot.Connect();
        bot.StartGame();
        bot.StartLevel();

        // Three cells that aren't in a line.
        bot.Send("OnChainMsg", "2,9,4,9,6,9,");

        CollectionAssert.Contains(bot.Sent, "OnGameStoppedMsg");
        Assert.IsNull(bot.Result, "a stopped game pays nothing");
        Assert.IsFalse(bot.Game.Finished);
    }

    [TestMethod]
    public void Powerups_PickTheirTiles_OffTheSharedRandomQueue()
    {
        var used = new HashSet<int>();

        foreach (var id in new[] { 99, 97, 95, 380, 379, 344 })
        {
            if (used.Count == 4)
                break;

            var bot = new Bot(Definition(id), seed: id + 100) { PreferPowerups = true };

            bot.Connect();
            bot.StartGame();
            bot.StartLevel();
            bot.PlayLevel();

            Assert.IsFalse(bot.Sent.Contains("OnGameStoppedMsg"), $"{id}: the server followed every powerup");
            used.UnionWith(bot.PowerupsUsed);
        }

        CollectionAssert.IsSubsetOf(new[] { MatchThreeTiles.PowerupDynamite, MatchThreeTiles.PowerupWildTile,
            MatchThreeTiles.PowerupDestruct, MatchThreeTiles.PowerupShuffle }, used.ToList());
    }

    [TestMethod]
    public void TileQueue_NewWave_ReplacesTheQueue_OnceTheMovieAccepts()
    {
        var bot = new Bot(Definition(376), seed: 5) { AcceptTileQueues = false };

        bot.Connect();
        bot.StartGame();
        bot.StartLevel();
        Assert.IsTrue(bot.PlayChain());
        Assert.IsTrue(bot.PlayChain());

        var grid = bot.Game.PlayingLevel!.Grid!;
        var queued = grid.TileQueueLength;
        var start = bot.Sent.Count;

        // A wave (every hundred tiles collected) offers the movie a new queue, if it holds as many tiles as the server.
        grid.NextWave();

        var offer = bot.Sent.Skip(start).Single(x => x.StartsWith("OnTileQueueMsg	"));
        Assert.AreEqual(queued.ToString(), offer.Split('	')[1]);
        Assert.AreEqual(queued, grid.TileQueueLength, "the old queue stands until the movie takes the new one");

        // Rejected (the movie had used a tile the server hadn't heard of yet): offered again.
        start = bot.Sent.Count;
        bot.Send("OnLevelEventMsg", "61");
        Assert.AreEqual(offer, bot.Sent.Skip(start).Single(x => x.StartsWith("OnTileQueueMsg	")));

        bot.Send("OnLevelEventMsg", "60");
        Assert.AreEqual(offer.Split('	')[2].Split(',').Length, grid.TileQueueLength, "the new queue replaced the old");
        Assert.AreNotEqual(queued, grid.TileQueueLength);
    }

    private static HashSet<int> LoadItemIds()
    {
        using var stream = File.OpenRead(Path.Combine(Resources, "ClientItemDefinitions.json"));
        using var document = JsonDocument.Parse(stream);

        return document.RootElement.EnumerateArray().Select(x => x.GetProperty("Id").GetInt32()).ToHashSet();
    }

    private static HashSet<string> MinigameManagerGames()
    {
        return typeof(FlashMinigame).Assembly.GetTypes()
            .Select(x => x.GetCustomAttributes(typeof(FlashMinigameAttribute), false).OfType<FlashMinigameAttribute>().FirstOrDefault()?.Name)
            .OfType<string>()
            .ToHashSet();
    }

    /// <summary>
    /// Plays a game through its messages the way the movie's client half does: it reports tiles that came to rest and
    /// ore that fell into the drop zone, drags chains, and presses powerups, drawing their tiles off the random queue
    /// the server sent (as the movie's Grid does, over the same tiles in the same order).
    /// </summary>
    private sealed class Bot
    {
        private readonly Queue<int> _randomNumbers = new();
        private int _read;

        public Bot(MinigameDefinition definition, int seed)
        {
            Game = FlashMinigame.CreateDetached<MatchThreeGame>(definition, DataDirectory, Sent.Add, won => Result = won, () => Now);
            Game.Random = new Random(seed);
        }

        public MatchThreeGame Game { get; }

        public List<string> Sent { get; } = [];

        public long Now { get; set; } = 1000;

        public bool? Result { get; private set; }

        public int Fell { get; private set; }

        /// <summary>
        /// The moves the last level took.
        /// </summary>
        public int Moves { get; private set; }

        /// <summary>
        /// The tiles in the last chain dragged.
        /// </summary>
        public int LastChain { get; private set; }

        public bool PreferPowerups { get; init; }

        public bool AcceptTileQueues { get; init; } = true;

        public HashSet<int> PowerupsUsed { get; } = [];

        private MatchThreeLevel Level => Game.PlayingLevel!;

        private MatchThreeGrid Grid => Level.Grid!;

        public void Send(string name, params string[] args)
        {
            Game.OnMessage(name, new FlashArgs(args));
            Pump();
        }

        public void Connect()
        {
            Game.OnStart();
            Send("OnConnectMsg");
        }

        public void StartGame() => Send("OnStartGameMsg", "0");

        public void StartLevel()
        {
            _randomNumbers.Clear();
            Send("OnStartLevelMsg", "0");
        }

        public void NextLevel() => Send("OnNextLevelMsg");

        private bool LevelStopped => Sent.Skip(_levelStart).Any(x => x.StartsWith("OnStopLevel\t"));

        private int _levelStart;

        public void PlayLevel()
        {
            _levelStart = Sent.FindLastIndex(x => x.StartsWith("OnLevelInitMsg\t"));

            for (Moves = 0; Moves < 3000 && !LevelStopped; Moves++)
            {
                Assert.IsFalse(Sent.Contains("OnGameStoppedMsg"), "the server followed the board");

                if (PreferPowerups && PressPowerup())
                    continue;

                if (!PlayChain() && !PressPowerup())
                    Assert.Fail("the board has no move");
            }

            Assert.IsTrue(LevelStopped, $"{Game.Definition.Comment} level {Level.Index} ended: {Describe()}");
        }

        /// <summary>
        /// The bins and the board, for a level that didn't end.
        /// </summary>
        private string Describe()
        {
            var bins = Level.Bins.Select(b => $"{b.Value}: {Collected(b)}/{b.Quota}");
            var rows = Enumerable.Range(0, Grid.Height).Select(y => string.Join(' ', Enumerable.Range(0, Grid.Width)
                .Select(x => Grid.TileAt(x, y) is { } t ? $"{t.NetworkValue,4}{(t.IsFallOffBottom ? '!' : t.State.ToString()[0])}" : "    .")));

            return string.Join(", ", bins) + Environment.NewLine + string.Join(Environment.NewLine, rows);
        }

        private int Collected(MatchThreeSpawnData bin)
        {
            return bin.Category switch
            {
                MatchThreeSpawnData.CategorySelectable => Level.CollectedSelectables(bin.Type),
                MatchThreeSpawnData.CategoryCollectable => Level.CollectedCollectables(bin.Type),
                _ => Level.CollectedBonuses(bin.Type)
            };
        }

        /// <summary>
        /// Settles the board as the movie reports it, then drags a chain (one that drops ore, or fills a bin, if it
        /// can), and lets time pass.
        /// </summary>
        public bool PlayChain()
        {
            ReportFallsAndIdle();

            if (LevelStoppedOrGone())
                return true;

            var chain = FindChain();

            if (chain is null)
                return false;

            LastChain = chain.Count;
            Send("OnChainMsg", string.Concat(chain.Select(t => $"{t.X},{t.Y},")));
            Tick();

            return true;
        }

        private bool LevelStoppedOrGone() => Game.PlayingLevel?.Grid is null || !Game.IsInProgress || LevelStopped;

        private void Tick()
        {
            Now += 400;
            Game.Tick();
            Pump();
        }

        private void ReportFallsAndIdle()
        {
            // Ore flagged to drop falls into the drop zone (the movie reports where it landed), which can drop more.
            for (var i = 0; i < 100; i++)
            {
                var falling = Grid.LiveTiles.FirstOrDefault(t => t.IsFallOffBottom && Grid.TileAt(t.X, t.Y) == t);

                if (falling is null)
                    break;

                Send("OnLevelEventMsg", "50", falling.X.ToString(), falling.Y.ToString());
                Fell++;
            }

            var settling = Grid.LiveTiles.Where(t => t.State is MatchThreeTileState.Pending or MatchThreeTileState.Appear).ToList();

            if (settling.Count > 0)
                Send("OnIdleMsg", string.Concat(settling.Select(t => $"{t.X},{t.Y},")));
        }

        /// <summary>
        /// The best chain a player would drag, up to <see cref="MaximumChain"/> tiles.
        /// </summary>
        private List<MatchThreeTile>? FindChain()
        {
            List<MatchThreeTile>? best = null;
            var bestScore = int.MinValue;
            var chain = new List<MatchThreeTile>();

            void Extend()
            {
                if (chain.Count >= MatchThreeTiles.MinimumChainLength)
                {
                    var score = Score(chain);

                    if (score > bestScore)
                    {
                        best = [.. chain];
                        bestScore = score;
                    }
                }

                if (chain.Count >= MaximumChain)
                    return;

                foreach (var next in Neighbours(chain[^1]).Where(t => CanAdd(chain, t)).ToList())
                {
                    chain.Add(next);
                    Extend();
                    chain.RemoveAt(chain.Count - 1);
                }
            }

            for (var x = 0; x < Grid.Width; x++)
            {
                for (var y = 0; y < Grid.Height; y++)
                {
                    var first = Grid.TileAt(x, y);

                    if (first is null || !CanAdd(chain, first))
                        continue;

                    chain.Add(first);
                    Extend();
                    chain.Clear();
                }
            }

            return best;
        }

        private const int MaximumChain = 7;

        /// <summary>
        /// The movie's (and server's) chain rule.
        /// </summary>
        private static bool CanAdd(List<MatchThreeTile> chain, MatchThreeTile tile)
        {
            if (chain.Contains(tile))
                return false;

            var value = chain.LastOrDefault(t => t.Category == MatchThreeTiles.Selectable)?.Value ?? 0;

            if (tile.Value == MatchThreeTiles.Wildcard)
                return true;

            return value != 0 ? tile.Value == value : tile.Category == MatchThreeTiles.Selectable;
        }

        private IEnumerable<MatchThreeTile> Neighbours(MatchThreeTile tile)
        {
            return new[] { Grid.TileAt(tile.X + 1, tile.Y), Grid.TileAt(tile.X - 1, tile.Y), Grid.TileAt(tile.X, tile.Y + 1), Grid.TileAt(tile.X, tile.Y - 1) }
                .OfType<MatchThreeTile>();
        }

        /// <summary>
        /// Prefers chains under ore (so it drops), chains of a kind a bin still wants, and low ones.
        /// </summary>
        private int Score(List<MatchThreeTile> chain)
        {
            var score = chain.Count * 5;

            foreach (var tile in chain)
            {
                score += tile.Y;

                for (var y = tile.Y - 1; y >= 0; y--)
                {
                    if (Grid.TileAt(tile.X, y)?.Category is MatchThreeTiles.Collectable or MatchThreeTiles.Bonus)
                        score += 20;
                }
            }

            var value = chain.FirstOrDefault(t => t.Category == MatchThreeTiles.Selectable)?.Value;

            if (value is not null && Level.SelectableSpawnData.TryGetValue(value.Value - MatchThreeTiles.Selectable, out var data)
                && data.Quota > Level.CollectedSelectables(data.Type))
            {
                score += 100;
            }

            return score;
        }

        /// <summary>
        /// Presses an idle powerup as the movie does, choosing its tiles the same way off the same random numbers.
        /// </summary>
        private bool PressPowerup()
        {
            ReportFallsAndIdle();

            if (LevelStoppedOrGone())
                return true;

            var pressed = Grid.LiveTiles.FirstOrDefault(t => t.Value >= MatchThreeTiles.PowerupPressMe && t.State == MatchThreeTileState.Idle
                && Grid.TileAt(t.X, t.Y) == t);

            if (pressed is null || !Level.PowerupSpawnData.TryGetValue(pressed.Value, out var data) || _randomNumbers.Count < data.Power)
                return false;

            var tiles = pressed.Value switch
            {
                MatchThreeTiles.PowerupDynamite => Dynamite(pressed, data.Power),
                MatchThreeTiles.PowerupWildTile => RandomTiles(pressed, data.Power, t => t.Category == MatchThreeTiles.Selectable),
                MatchThreeTiles.PowerupDestruct => RandomTiles(pressed, data.Power, t => t.Category == MatchThreeTiles.Selectable, t => t.Category == MatchThreeTiles.Blocker),
                _ => RandomTiles(pressed, data.Power - 1, t => t != pressed && t.Category >= MatchThreeTiles.Visible, upToCandidates: true)
            };

            Send("OnActionMsg", pressed.Value.ToString(), string.Concat(tiles.Select(t => $"{t.X},{t.Y},")));
            PowerupsUsed.Add(pressed.Value);
            Tick();

            return true;
        }

        /// <summary>
        /// Wild tiles and destruct: the pressed tile, then random idle tiles of the kinds (each kind in turn, in the
        /// board's order). Shuffle: the pressed tile and that many more.
        /// </summary>
        private List<MatchThreeTile> RandomTiles(MatchThreeTile pressed, int count, Func<MatchThreeTile, bool> kind,
            Func<MatchThreeTile, bool>? moreKind = null, bool upToCandidates = false)
        {
            var candidates = Grid.LiveTiles.Where(t => t.State == MatchThreeTileState.Idle && kind(t)).ToList();

            if (moreKind is not null)
                candidates.AddRange(Grid.LiveTiles.Where(t => t.State == MatchThreeTileState.Idle && moreKind(t)));

            List<MatchThreeTile> tiles = [pressed];
            var draws = upToCandidates ? Math.Min(count, candidates.Count) : count - 1;

            for (var i = 0; i < draws && candidates.Count > 0; i++)
            {
                var index = _randomNumbers.Dequeue() % candidates.Count;
                tiles.Add(candidates[index]);
                candidates.RemoveAt(index);
            }

            return tiles;
        }

        /// <summary>
        /// Dynamite: the movie's blast, spreading to random idle neighbours of what it has blasted.
        /// </summary>
        private List<MatchThreeTile> Dynamite(MatchThreeTile pressed, int power)
        {
            var blasted = new List<MatchThreeTile>();
            var next = new List<MatchThreeTile>();
            var current = new List<MatchThreeTile>();
            MatchThreeTile? tile = pressed;

            MatchThreeTile? At(int x, int y)
            {
                var at = Grid.TileAt(x, y);
                return at is null || blasted.Contains(at) ? null : at;
            }

            while (power > 0 && tile is not null)
            {
                if (tile.State == MatchThreeTileState.Idle && !blasted.Contains(tile))
                {
                    blasted.Add(tile);

                    foreach (var neighbour in new[] { At(tile.X, tile.Y + 1), At(tile.X, tile.Y - 1), At(tile.X + 1, tile.Y), At(tile.X - 1, tile.Y) })
                    {
                        if (neighbour is { State: MatchThreeTileState.Idle, Category: MatchThreeTiles.Selectable or MatchThreeTiles.Wildcard or MatchThreeTiles.Blocker }
                            && !next.Contains(neighbour))
                        {
                            next.Add(neighbour);
                        }
                    }

                    power--;
                }

                if (power > 0)
                {
                    if (current.Count == 0)
                    {
                        current = [.. next];
                        next = [];
                    }

                    tile = null;

                    if (current.Count > 0)
                    {
                        var index = _randomNumbers.Dequeue() % current.Count;
                        tile = current[index];
                        current.RemoveAt(index);
                    }
                }
            }

            return blasted;
        }

        /// <summary>
        /// Reads what the server sent: its random numbers, and new tile queues to accept.
        /// </summary>
        private void Pump()
        {
            while (_read < Sent.Count)
            {
                var message = Sent[_read++].Split('\t');

                if (message[0] == "OnRandomQueueMsg")
                {
                    foreach (var number in message[1].Split(','))
                        _randomNumbers.Enqueue(int.Parse(number));
                }
                else if (message[0] == "OnTileQueueMsg" && message[1] != "-1" && AcceptTileQueues)
                {
                    Game.OnMessage("OnLevelEventMsg", new FlashArgs(["60"]));
                }
            }
        }
    }
}
