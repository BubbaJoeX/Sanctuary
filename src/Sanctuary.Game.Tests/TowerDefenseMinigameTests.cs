using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sanctuary.Game.Minigames;
using Sanctuary.Game.Minigames.TowerDefense;
using Sanctuary.Game.Resources;
using Sanctuary.Game.Resources.Definitions;

namespace Sanctuary.Game.Tests;

[TestClass]
public sealed class TowerDefenseMinigameTests
{
    /// <summary>
    /// The movie's own column numbers for the rows the server sends (Game.MAP_*, TOWER_*, ENEMY_*).
    /// </summary>
    private const int ClientMapFields = 25;
    private const int ClientMapType = 3;
    private const int ClientMapLevel = 4;
    private const int ClientMapNumPaths = 5;
    private const int ClientMapMoney = 6;
    private const int ClientMapLives = 7;
    private const int ClientMapStages = 8;
    private const int ClientMapEnemyHpMultiplier = 24;

    private const int ClientTowerFields = 22;
    private const int ClientTowerStringId = 3;
    private const int ClientTowerType = 5;
    private const int ClientTowerShootFrame = 14;

    private const int ClientEnemyFields = 11;
    private const int ClientEnemySwfName = 1;
    private const int ClientEnemySpeed = 5;

    /// <summary>
    /// The live games: hard 150-161, medium 250-261, easy 350-361.
    /// </summary>
    private static readonly int[] LiveGames = [.. Enumerable.Range(150, 12), .. Enumerable.Range(250, 12), .. Enumerable.Range(350, 12)];

    /// <summary>
    /// The tower movies there are art for (TowerSwfs/tower&lt;id&gt;u&lt;0-4&gt;) and that have sounds in Free Realms;
    /// 12-15 are the Ancient Battle world's, which never went live.
    /// </summary>
    private static readonly int[] LiveTowerIds = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 16, 17, 18, 19];

    /// <summary>
    /// The enemies the movie's wave list has pictures for (the scroller's frame labels).
    /// </summary>
    private static readonly string[] ScrollerEnemies =
    [
        "Frost Giant", "Yeti", "Sasquatch", "Ice Tree", "Snowman", "Bear Vinegolem", "Forest Giant 1", "Forest Giant 2",
        "Shroom Giant", "Flower", "Floren", "Toad", "Wolf", "Warthog", "Changeling Thief", "Bear", "Chugawug Bandit",
        "Sprite 1", "Sprite 2", "Sprite 3", "Mauthdog"
    ];

    /// <summary>
    /// Frames of each delayed tower's weapon (the frame it fires on has to be one of them), by tower and rank.
    /// </summary>
    private static readonly Dictionary<int, int[]> WeaponFrames = new()
    {
        [3] = [37, 37, 37, 37, 37],
        [5] = [15, 15, 15, 15, 15],
        [7] = [32, 32, 32, 32, 32],
        [8] = [15, 15, 15, 15, 15],
        [9] = [21, 21, 21, 21, 21],
        [11] = [47, 18, 18, 18, 18],
        [16] = [42, 42, 42, 42, 42],
        [17] = [15, 15, 15, 15, 15],
        [19] = [17, 17, 17, 17, 17]
    };

    private static string GetMinigamesDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Resources", "Minigames", "TowerDefense.json")))
            directory = directory.Parent;

        Assert.IsNotNull(directory, "Could not find Resources/Minigames/TowerDefense.json.");

        return Path.Combine(directory.FullName, "Resources", "Minigames");
    }

    private static MinigameDefinitionCollection LoadCatalog()
    {
        var catalog = new MinigameDefinitionCollection(NullLogger.Instance);

        Assert.IsTrue(catalog.Load(GetMinigamesDirectory()));

        return catalog;
    }

    private sealed class Session
    {
        public required TowerDefenseGame Game { get; init; }
        public List<string> Sent { get; } = [];
        public bool? Result { get; set; }

        /// <summary>
        /// A message from the movie, the way MinigameManager hands it over.
        /// </summary>
        public bool Receive(string payload)
        {
            var (name, args) = MinigameManager.ParseMessage(payload);

            return Game.OnMessage(name, args);
        }
    }

    private static Session Start(int id)
    {
        var definition = LoadCatalog()[id];
        var dataDirectory = Path.Combine(GetMinigamesDirectory(), "TowerDefense");

        Session? session = null;

        var game = FlashMinigame.CreateDetached<TowerDefenseGame>(definition, dataDirectory, x => session!.Sent.Add(x),
            won => session!.Result = won);

        session = new Session { Game = game };
        game.OnStart();

        return session;
    }

    /// <summary>
    /// The movie's start (GameClient.Connect): FRServer_Connect, then FRServer_ReadyForGameData.
    /// </summary>
    private static Session StartAndLoad(int id)
    {
        var session = Start(id);

        Assert.IsTrue(session.Receive("OnConnectMsg"));
        Assert.IsTrue(session.Receive("FRServer_ReadyForGameData"));

        return session;
    }

    private static string[] Row(string message, string name)
    {
        var parts = message.Split('\t');

        Assert.AreEqual(2, parts.Length, message);
        Assert.AreEqual(name, parts[0]);

        return parts[1].Split('^');
    }

    [TestMethod]
    public void Catalog_ListsEveryLiveGame_InTheTowerDefenseTile()
    {
        var games = LoadCatalog().Values.Where(x => x.Game == "TowerDefense").ToList();

        CollectionAssert.AreEquivalent(LiveGames, games.Select(x => x.Id).ToArray());
        Assert.AreEqual("TowerDefense", typeof(TowerDefenseGame).GetCustomAttribute<FlashMinigameAttribute>()?.Name);

        foreach (var game in games)
        {
            var difficulty = (game.Id / 100) switch
            {
                1 => "hard",
                2 => "medium",
                _ => "easy"
            };

            Assert.AreEqual("towerdefense.swf", game.Swf);
            Assert.AreEqual(23, game.Category);
            Assert.AreEqual(game.Id, game.Level);
            Assert.AreEqual(difficulty switch { "hard" => 5, "medium" => 3, _ => 1 }, game.Difficulty, game.Comment);
            StringAssert.EndsWith(game.DetailImage, $"_{difficulty}_detail.dds");
            StringAssert.EndsWith(game.ThumbnailImage, $"_{difficulty}_thumb.dds");
            StringAssert.EndsWith(game.Comment, " - " + char.ToUpperInvariant(difficulty[0]) + difficulty[1..]);
            Assert.IsTrue(game.Coins > 0 && game.Stars > 0, game.Comment);
        }
    }

    [TestMethod]
    public void EveryGame_HasAMap_ItsMovieCanDraw()
    {
        var data = TowerDefenseData.Load(Path.Combine(GetMinigamesDirectory(), "TowerDefense"));

        foreach (var definition in LoadCatalog().Values.Where(x => x.Game == "TowerDefense"))
        {
            var setup = data.Resolve(definition.Level);

            Assert.IsNotNull(setup, definition.Comment);

            // Maps/BG_W<world>M<level>, the W<world>M<level>_mask and place_ clips; the Bixie maps have three paths.
            Assert.IsTrue(setup.World is >= 1 and <= 4, definition.Comment);
            Assert.IsTrue(setup.Level is >= 1 and <= 3, definition.Comment);
            Assert.AreEqual(setup.World == 4 ? 3 : 1, setup.NumPaths, definition.Comment);

            // Each difficulty's ids (from 150, 250 and 350) go world by world, three maps to a world.
            Assert.AreEqual(definition.Id % 50 / 3 + 1, setup.World, definition.Comment);
            Assert.AreEqual(definition.Id % 50 % 3 + 1, setup.Level, definition.Comment);

            // Four tower buttons, each tower with ranks 0-4 (the movie looks a tower's ranks up until one is missing).
            Assert.AreEqual(4, setup.TowerTypes.Count, definition.Comment);

            foreach (var type in setup.TowerTypes)
            {
                CollectionAssert.Contains(LiveTowerIds, type, definition.Comment);

                var ranks = setup.Towers.Where(x => TowerDefenseData.Int(x, TowerDefenseData.TowerTowerId) == type)
                    .Select(x => TowerDefenseData.Int(x, TowerDefenseData.TowerRank)).ToArray();

                CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 4 }, ranks, $"{definition.Comment}: tower {type}");
            }

            Assert.IsTrue(setup.Enemies.Count >= 3, definition.Comment);
            Assert.IsTrue(setup.Stages > 0 && setup.Lives > 0, definition.Comment);
        }
    }

    [TestMethod]
    public void TowerAndEnemyRows_FitTheirArt()
    {
        var data = TowerDefenseData.Load(Path.Combine(GetMinigamesDirectory(), "TowerDefense"));

        foreach (var tower in data.Towers)
        {
            Assert.AreEqual(ClientTowerFields, tower.Length, string.Join('^', tower));

            var type = TowerDefenseData.Int(tower, TowerDefenseData.TowerTowerId);
            var rank = TowerDefenseData.Int(tower, TowerDefenseData.TowerRank);

            CollectionAssert.Contains(LiveTowerIds, type);

            // A delayed tower (hammer, mushroom, crit, delayed, random) fires when its weapon reaches the frame.
            if (WeaponFrames.TryGetValue(type, out var frames))
            {
                var shootFrame = TowerDefenseData.Int(tower, ClientTowerShootFrame);
                Assert.IsTrue(shootFrame >= 2 && shootFrame <= frames[rank], $"tower {type} rank {rank} fires on frame {shootFrame}");
            }

            // The tower's name is the locale's (Penguin Tower ... Cow Tower, 384031-384055).
            Assert.IsTrue(TowerDefenseData.Int(tower, ClientTowerStringId) is >= 384031 and <= 384055);
            Assert.IsTrue(TowerDefenseData.Int(tower, ClientTowerType) is >= 0 and <= 8);
        }

        foreach (var enemy in data.Enemies)
        {
            Assert.AreEqual(ClientEnemyFields, enemy.Length, string.Join('^', enemy));
            CollectionAssert.Contains(ScrollerEnemies, enemy[ClientEnemySwfName]);

            // The Bixie paths end in 22 frames of enemyPass; a faster enemy would skip them onto the next path.
            Assert.IsTrue(TowerDefenseData.Number(enemy, ClientEnemySpeed) is > 0 and < 22, enemy[ClientEnemySwfName]);
        }
    }

    [TestMethod]
    public void ReadyForGameData_SendsTheGame_InTheOrderTheMovieReadsIt()
    {
        var session = StartAndLoad(150);
        var sent = session.Sent;

        // Penguin Produce - Hard: the game id, the map, 4 towers x 5 ranks, its 3 enemies, then the start.
        Assert.AreEqual(1 + 1 + 20 + 3 + 1, sent.Count);
        Assert.AreEqual("OnGameDataMsg\t150", sent[0]);

        var map = Row(sent[1], "OnMapDataMsg");
        Assert.AreEqual(ClientMapFields, map.Length);
        Assert.AreEqual("150", map[0]);
        Assert.AreEqual("1", map[ClientMapType]);
        Assert.AreEqual("1", map[ClientMapLevel]);
        Assert.AreEqual("1", map[ClientMapNumPaths]);
        Assert.AreEqual("25", map[ClientMapMoney]);
        Assert.AreEqual("10", map[ClientMapLives]);
        Assert.AreEqual("40", map[ClientMapStages]);
        Assert.AreEqual("0.8", map[ClientMapEnemyHpMultiplier]);

        var towers = sent.Skip(2).Take(20).Select(x => Row(x, "OnTowerDataMsg")).ToList();
        Assert.IsTrue(towers.All(x => x.Length == ClientTowerFields));

        // The buttons, in the order the movie meets the towers: Mail Penguin, Penguin Tower, Hammer Penguin, Balloon Penguin.
        CollectionAssert.AreEqual(new[] { "8", "0", "7", "17" }, towers.Select(x => x[1]).Distinct().ToArray());
        CollectionAssert.AreEqual(new[] { "0", "1", "2", "3", "4" }, towers.Take(5).Select(x => x[2]).ToArray());

        var enemies = sent.Skip(22).Take(3).Select(x => Row(x, "OnEnemyDataMsg")).ToList();
        CollectionAssert.AreEqual(new[] { "Snowman", "Sasquatch", "Yeti" }, enemies.Select(x => x[ClientEnemySwfName]).ToArray());

        Assert.AreEqual("OnGameDataCompleteMsg", sent[^1]);
    }

    [TestMethod]
    public void BixieMaps_SendThreePaths_AndEasyMapsTheirOwnLimits()
    {
        var bixies = StartAndLoad(361);
        var map = Row(bixies.Sent[1], "OnMapDataMsg");

        Assert.AreEqual("4", map[ClientMapType]);
        Assert.AreEqual("3", map[ClientMapLevel]);
        Assert.AreEqual("3", map[ClientMapNumPaths]);
        Assert.AreEqual("20", map[ClientMapStages]);
        Assert.AreEqual("20", map[ClientMapLives]);
    }

    [TestMethod]
    public void GameData_IsSentOnce()
    {
        var session = StartAndLoad(253);
        var count = session.Sent.Count;

        Assert.IsTrue(session.Receive("FRServer_ReadyForGameData"));
        Assert.AreEqual(count, session.Sent.Count);
    }

    [TestMethod]
    public void AWin_ThroughEveryStage_PaysOut()
    {
        var session = StartAndLoad(356);
        var stages = session.Game.Setup!.Stages;

        // The first game shows the tutorial paused; the movie then starts, builds, and plays every wave.
        Assert.IsTrue(session.Receive("OnPauseMsg\ttrue"));
        Assert.IsTrue(session.Receive("OnPauseMsg\tfalse"));
        Assert.IsTrue(session.Receive("FRServer_LivesCount\t20"));
        Assert.IsTrue(session.Receive("FRServer_MoneySpent\t10"));
        Assert.IsTrue(session.Receive("FRServer_TowerBuilt\t0\t30"));
        Assert.IsTrue(session.Receive("FRServer_MoneySpent\t15"));
        Assert.IsTrue(session.Receive("FRServer_TowerUpgraded\t0\t31\t1"));

        for (var stage = 0; stage <= stages; stage++)
        {
            Assert.IsTrue(session.Receive($"FRServer_CompletedStage\t{stage}"));

            if (stage < stages)
            {
                Assert.IsTrue(session.Receive("FRServer_EnemyDied\t12"));
                Assert.IsTrue(session.Receive("FRServer_MoneyEarned\t2"));
            }
        }

        Assert.IsTrue(session.Receive("FRServer_LivesCount\t19"));
        Assert.IsTrue(session.Receive("FRServer_GameWon\t123456"));

        Assert.AreEqual(true, session.Result);
        Assert.AreEqual(123456, session.Game.Score);
        Assert.AreEqual(stages, session.Game.StagesCompleted);
        Assert.AreEqual(stages, session.Game.EnemiesDefeated);
        Assert.AreEqual(1, session.Game.TowersBuilt);
        Assert.AreEqual(1, session.Game.TowersUpgraded);

        // Three seconds later the movie closes itself; the result stands.
        Assert.IsTrue(session.Receive("FRServer_GameOver"));
        Assert.AreEqual(true, session.Game.Won);
    }

    [TestMethod]
    public void AWin_BeforeTheLastStage_IsALoss()
    {
        var session = StartAndLoad(157);

        for (var stage = 0; stage <= 5; stage++)
            Assert.IsTrue(session.Receive($"FRServer_CompletedStage\t{stage}"));

        Assert.IsTrue(session.Receive("FRServer_GameWon\t999999"));

        Assert.AreEqual(false, session.Result);
        Assert.IsFalse(session.Game.Won);
    }

    [TestMethod]
    public void ALoss_PaysTheLossShare()
    {
        var session = StartAndLoad(259);

        Assert.IsTrue(session.Receive("FRServer_CompletedStage\t3"));
        Assert.IsTrue(session.Receive("FRServer_LivesCount\t0"));
        Assert.IsTrue(session.Receive("FRServer_GameLost\t4200"));

        Assert.AreEqual(false, session.Result);
        Assert.AreEqual(4200, session.Game.Score);

        Assert.IsTrue(session.Receive("FRServer_GameOver"));
        Assert.AreEqual(false, session.Result);
    }

    [TestMethod]
    public void PenguinWars_EndsWithoutGameLost_WhenTheFishAreGone()
    {
        var session = StartAndLoad(351);

        Assert.IsTrue(session.Receive("FRServer_CompletedStage\t0"));
        Assert.IsTrue(session.Receive("FRServer_CompletedStage\t1"));

        // TD_SnowGame.loseLife: the last fish carried off goes straight to the game over.
        for (var lives = 19; lives >= 0; lives--)
            Assert.IsTrue(session.Receive($"FRServer_LivesCount\t{lives}"));

        Assert.IsNull(session.Result);
        Assert.IsTrue(session.Receive("FRServer_GameOver"));

        Assert.AreEqual(false, session.Result);
        Assert.AreEqual(0, session.Game.Lives);
    }

    [TestMethod]
    public void AGame_WithoutData_SendsNothing()
    {
        var definition = new MinigameDefinition { Id = 150, Game = "TowerDefense", Level = 9999 };
        var sent = new List<string>();

        var game = FlashMinigame.CreateDetached<TowerDefenseGame>(definition, Path.Combine(GetMinigamesDirectory(), "TowerDefense"), sent.Add);
        game.OnStart();

        Assert.IsNull(game.Setup);
        Assert.IsTrue(game.OnMessage("FRServer_ReadyForGameData", default));
        Assert.AreEqual(0, sent.Count);
        Assert.IsFalse(game.OnMessage("OnSomethingElseMsg", default));
    }
}
