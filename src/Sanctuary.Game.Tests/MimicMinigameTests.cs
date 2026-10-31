using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sanctuary.Game.Minigames;
using Sanctuary.Game.Minigames.Mimic;
using Sanctuary.Game.Resources;
using Sanctuary.Game.Resources.Definitions;

namespace Sanctuary.Game.Tests;

[TestClass]
public sealed class MimicMinigameTests
{
    private static readonly string Resources = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Resources"));
    private static readonly string Minigames = Path.Combine(Resources, "Minigames");

    private static readonly string[] MimicGames = ["Cooking", "Smelting", "Music", "HackNSlash"];

    /// <summary>
    /// One player's game, detached, with what it sends the movie and the result it pays.
    /// </summary>
    private sealed class Play
    {
        public long Now;
        public bool? Result;
        public readonly List<string> Sent = [];
        public MimicGame Game = null!;

        public Play(MinigameDefinition definition)
        {
            var directory = Path.Combine(Minigames, definition.Game);

            Game = definition.Game switch
            {
                "Cooking" => FlashMinigame.CreateDetached<CookingGame>(definition, directory, Sent.Add, Finish, Clock),
                "Smelting" => FlashMinigame.CreateDetached<SmeltingGame>(definition, directory, Sent.Add, Finish, Clock),
                "Music" => FlashMinigame.CreateDetached<MusicGame>(definition, directory, Sent.Add, Finish, Clock),
                "HackNSlash" => FlashMinigame.CreateDetached<HackNSlashGame>(definition, directory, Sent.Add, Finish, Clock),
                _ => throw new ArgumentException(definition.Game)
            };

            Game.OnStart();
        }

        private long Clock() => Now;

        private void Finish(bool won) => Result = won;

        /// <summary>
        /// A message from the movie, as MinigameManager hands it on (it takes the FRServer_* score messages itself).
        /// </summary>
        public bool Movie(string payload)
        {
            var (name, args) = MinigameManager.ParseMessage(payload);

            return Game.OnMessage(name, args);
        }

        /// <summary>
        /// The movie coming up: its network to Free Realms connects and it asks for its data.
        /// </summary>
        public void Connect()
        {
            Assert.IsTrue(Movie("OnConnectMsg"));
            Assert.IsTrue(Movie("FRServer_ReadyForGameData"));
        }
    }

    private static Dictionary<int, MinigameDefinition> LoadCatalog()
    {
        var catalog = new MinigameDefinitionCollection(NullLogger.Instance);
        Assert.IsTrue(catalog.Load(Minigames));

        return catalog.Values.Where(x => MimicGames.Contains(x.Game)).ToDictionary(x => x.Id);
    }

    /// <summary>
    /// What the movie makes of the rows it was sent, the way its GameServer does: OnLevelDataFromFR adds each to
    /// mLevelDataFileContent (which starts out undefined), and OnLevelData keeps the lines whose GAME column is the
    /// id OnGameDataMsg gave, keyed by their SEQUENCE.
    /// </summary>
    private static SortedDictionary<int, string[]> MovieRounds(IEnumerable<string> sent)
    {
        var gameId = -1;
        var content = "undefined";

        foreach (var payload in sent)
        {
            var (name, args) = MinigameManager.ParseMessage(payload);

            switch (name)
            {
                case "OnGameDataMsg":
                    gameId = args.Int(0);
                    break;

                case "OnPhaseDataMsg":
                    Assert.AreEqual(1, args.Count, "A row is one argument.");
                    Assert.IsFalse(double.TryParse(args[0], NumberStyles.Float, CultureInfo.InvariantCulture, out _),
                        "The movie would turn a row it can read as a number into one.");
                    content += args[0] + "\n";
                    break;
            }
        }

        var rounds = new SortedDictionary<int, string[]>();

        foreach (var line in content.Split('\n'))
        {
            var fields = line.Split('^');

            if (fields.Length > MimicRow.SequenceColumn && int.TryParse(fields[MimicRow.GameColumn], out var game) && game == gameId)
                rounds[int.Parse(fields[MimicRow.SequenceColumn])] = fields;
        }

        return rounds;
    }

    [TestMethod]
    public void Catalog_ListsEveryMimicGame_WithItsServerImagesAndRewards()
    {
        var catalog = LoadCatalog();

        Assert.AreEqual(40, catalog.Values.Count(x => x.Game == "Cooking"));
        Assert.AreEqual(5, catalog.Values.Count(x => x.Game == "Smelting"));
        Assert.AreEqual(7, catalog.Values.Count(x => x.Game == "Music"));
        Assert.AreEqual(1, catalog.Values.Count(x => x.Game == "HackNSlash"));

        var runners = typeof(FlashMinigame).Assembly.GetTypes()
            .Select(x => x.GetCustomAttribute<FlashMinigameAttribute>()?.Name)
            .Where(x => x is not null)
            .ToHashSet();

        using var items = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(Resources, "ClientItemDefinitions.json")));
        var itemIds = items.RootElement.EnumerateArray().Select(x => x.GetProperty("Id").GetInt32()).ToHashSet();

        var swfs = new Dictionary<string, string>
        {
            ["Cooking"] = "game_cooking.swf",
            ["Smelting"] = "game_smelting.swf",
            ["Music"] = "game_music.swf",
            ["HackNSlash"] = "game_sword.swf"
        };

        foreach (var definition in catalog.Values)
        {
            Assert.IsTrue(runners.Contains(definition.Game), $"{definition.Comment}: no server runs {definition.Game}.");
            Assert.AreEqual(swfs[definition.Game], definition.Swf);
            Assert.IsTrue(definition.NameId > 0 && definition.IconId > 0, definition.Comment);
            Assert.IsTrue(definition.DetailImage.EndsWith("_detail.dds") && definition.ThumbnailImage.EndsWith("_thumb.dds"), definition.Comment);
            Assert.IsTrue(definition.Coins > 0 && definition.Stars > 0, definition.Comment);

            foreach (var item in definition.Items)
                Assert.IsTrue(itemIds.Contains(item.ItemId), $"{definition.Comment}: no item {item.ItemId}.");

            switch (definition.Game)
            {
                case "Cooking":
                    Assert.AreEqual(45, definition.ProfileId, "Cooking trains the Chef.");
                    CollectionAssert.Contains(new[] { 0, 12 }, definition.Category, definition.Comment);
                    break;

                case "Smelting":
                    Assert.AreEqual(14, definition.ProfileId, "Smelting trains the Miner.");
                    Assert.AreEqual(21, definition.Category);
                    Assert.AreEqual(1, definition.Items.Count, $"{definition.Comment} smelts its bar.");
                    break;

                default:
                    Assert.AreEqual(0, definition.ProfileId);
                    CollectionAssert.Contains(new[] { 0, 18 }, definition.Category, definition.Comment);
                    break;
            }
        }

        // Every dish in the Games window cooks the dish it's named for (Jacque's rolls have no item).
        Assert.AreEqual(36, catalog.Values.Count(x => x.Game == "Cooking" && x.Category > 0 && x.Items.Count == 1));
        Assert.AreEqual(46, catalog[560].Level, "A copy plays its original's recipe.");
    }

    [TestMethod]
    public void DataFiles_AreInTheMoviesLayout_AndPlayableByIt()
    {
        foreach (var definition in LoadCatalog().Values)
        {
            var problems = new Play(definition).Game.CheckData();

            Assert.AreEqual(0, problems.Count, $"{definition.Comment}: {string.Join(" ", problems)}");
        }
    }

    [TestMethod]
    public void CheckData_FlagsRowsTheMovieCantPlay()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"mimic-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);

        try
        {
            File.WriteAllText(Path.Combine(directory, MimicGame.PhaseDataFileName),
                // Ingredients added before any are prepared, then a pour round (no frame) numbered 3 with no round 2.
                "901^9^1^30^0^0^0^17^42^1^500^0^0^0^0^0^0^0^0\n" +
                "903^9^3^30^0^0^0^5^19^0^0^0^0^0^0^0^0^0^0\n");

            var game = FlashMinigame.CreateDetached<CookingGame>(new MinigameDefinition { Id = 9, Game = "Cooking" }, directory, _ => { });
            var problems = game.CheckData();

            Assert.AreEqual(3, problems.Count, string.Join(" ", problems));
            Assert.IsTrue(problems.Any(x => x.Contains("numbered 1 to")));
            Assert.IsTrue(problems.Any(x => x.Contains("nothing is prepared")));
            Assert.IsTrue(problems.Any(x => x.Contains("no round 5")));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [TestMethod]
    public void MimicRow_ReadsTheMoviesCaretRows_AndSkipsNotes()
    {
        Assert.IsFalse(MimicRow.TryParse("# 46 Strong Arm Stew", out _));
        Assert.IsFalse(MimicRow.TryParse("   ", out _));

        Assert.IsTrue(MimicRow.TryParse("4601^46^1^30^0^0^0^1^9^8^25^", out var row));
        Assert.AreEqual("4601^46^1^30^0^0^0^1^9^8^25", row.ToString(), "The client's tables end rows with a caret.");
        Assert.AreEqual(46, row.Game);
        Assert.AreEqual(1, row.Sequence);
        Assert.AreEqual(30, row.Duration);
        Assert.AreEqual(1, row.StageType);
        Assert.AreEqual(9, row.Data(1));
        Assert.AreEqual(0, row.Data(11), "A column the row doesn't have reads 0.");
        Assert.IsTrue(row.IsNumeric);
    }

    [TestMethod]
    public void Cooking_SendsTheRecipesRounds_AndPaysAWinOfEveryRound()
    {
        var play = new Play(LoadCatalog()[46]);

        play.Connect();

        Assert.AreEqual("OnGameDataMsg\t46", play.Sent[0]);
        Assert.AreEqual("OnDataComplete", play.Sent[^1]);

        var rounds = MovieRounds(play.Sent);

        CollectionAssert.AreEqual(new[] { 1, 2, 3, 4, 5 }, rounds.Keys.ToArray());

        // Strong Arm Stew: tenderize the Bear Meat, slice a Tumtumnip, measure the Brown Broth, add, stir the stew.
        var stages = rounds.Values.Select(x => CookingGame.Stages[int.Parse(x[MimicRow.StageTypeColumn])]).ToArray();
        CollectionAssert.AreEqual(new[] { "tenderize", "slice", "measure", "addIngredients", "stir" }, stages);
        Assert.AreEqual("9", rounds[1][MimicRow.FirstDataColumn], "Bear Meat.");

        // Each round: the movie unpauses for it, then pauses for its score screen and sends the new total.
        foreach (var _ in rounds)
        {
            play.Now += 20_000;
            Assert.IsTrue(play.Movie("FRServer_RoundTransitionPause\tfalse"));
            Assert.IsTrue(play.Movie("FRServer_StatUpdate\t1\t0\t2)"));
            Assert.IsTrue(play.Movie("FRServer_RoundTransitionPause\ttrue"));
        }

        Assert.IsTrue(play.Movie("FRServer_GameWon\t12345\t0"));

        Assert.AreEqual(true, play.Result);
        Assert.AreEqual(12345, play.Game.Score);
        Assert.AreEqual("OnGameWonMsg", play.Sent[^1], "The movie ends its game on the server's word.");

        // The movie's GameOver closes it (MinigameManager); the game just takes it.
        Assert.IsTrue(play.Movie("FRServer_GameOver"));
    }

    [TestMethod]
    public void Cooking_WinWithoutItsRounds_IsPaidAsALoss()
    {
        var play = new Play(LoadCatalog()[190]);

        play.Connect();
        Assert.AreEqual(7, play.Game.Phases.Count);

        play.Now += 120_000;
        play.Movie("FRServer_RoundTransitionPause\ttrue");
        play.Movie("FRServer_GameWon\t5000\t0");

        Assert.AreEqual(false, play.Result);
        Assert.AreEqual("OnGameLostMsg", play.Sent[^1]);
    }

    [TestMethod]
    public void Result_IsIgnored_BeforeTheData_OrSoonerThanTheRoundsTake()
    {
        var catalog = LoadCatalog();

        // A movie that never asked for its data has no game to win.
        var early = new Play(catalog[46]);
        early.Movie("OnConnectMsg");
        early.Now = 600_000;
        early.Movie("FRServer_GameWon\t9999\t0");

        Assert.IsNull(early.Result, "Nothing is paid.");
        Assert.IsFalse(early.Game.Finished);
        Assert.AreEqual("OnGameLostMsg", early.Sent[^1], "The movie is still told to end.");

        // Five rounds in half a second.
        var quick = new Play(catalog[46]);
        quick.Connect();

        for (var i = 0; i < 5; i++)
            quick.Movie("FRServer_RoundTransitionPause\ttrue");

        quick.Now += 500;
        quick.Movie("FRServer_GameWon\t9999\t0");

        Assert.IsNull(quick.Result);
        Assert.AreEqual("OnGameLostMsg", quick.Sent[^1]);

        // The first result stands: a second, later one gets the same answer and no pay.
        quick.Now += 600_000;
        quick.Movie("FRServer_GameWon\t9999\t0");

        Assert.IsNull(quick.Result);
        Assert.AreEqual("OnGameLostMsg", quick.Sent[^1]);
    }

    [TestMethod]
    public void Score_IsClamped_ToWhatTheRoundsCanScore()
    {
        var catalog = LoadCatalog();

        var high = new Play(catalog[77]);
        high.Connect();
        high.Now += 120_000;
        high.Movie("FRServer_GameLost\t999999999");

        Assert.AreEqual(4 * MimicGame.MaxScorePerRound, high.Game.Score);
        Assert.AreEqual(false, high.Result, "A loss pays its share.");
        Assert.AreEqual("OnGameLostMsg", high.Sent[^1]);

        var negative = new Play(catalog[77]);
        negative.Connect();
        negative.Now += 120_000;
        negative.Movie("FRServer_GameLost\t-50");

        Assert.AreEqual(0, negative.Game.Score);
    }

    [TestMethod]
    public void ReadyForGameData_IsAnsweredOnce()
    {
        var play = new Play(LoadCatalog()[79]);

        play.Connect();

        var sent = play.Sent.Count;

        Assert.IsTrue(play.Movie("FRServer_ReadyForGameData"));
        Assert.AreEqual(sent, play.Sent.Count, "The movie adds every row it gets; a second copy would play each round twice.");
    }

    [TestMethod]
    public void Smelting_BreaksItsOreFirst_AndPaysTheBar()
    {
        var catalog = LoadCatalog();
        var gold = new Play(catalog[137]);

        gold.Connect();

        var rounds = MovieRounds(gold.Sent);
        CollectionAssert.AreEqual(new[] { 101, 102, 103, 104 },
            rounds.Values.Select(x => int.Parse(x[MimicRow.StageTypeColumn])).ToArray());
        Assert.AreEqual("5", rounds[1][MimicRow.FirstDataColumn], "Gold ore.");

        var copper = new Play(catalog[77]);
        copper.Connect();

        // Each bar plays differently: gold's furnace runs hotter than copper's.
        Assert.IsTrue(int.Parse(rounds[3][MimicRow.FirstDataColumn]) > int.Parse(MovieRounds(copper.Sent)[3][MimicRow.FirstDataColumn]));

        foreach (var _ in rounds)
        {
            gold.Now += 30_000;
            gold.Movie("FRServer_RoundTransitionPause\tfalse");
            gold.Movie("FRServer_RoundTransitionPause\ttrue");
        }

        // SoeInterfaceFreeRealms.StopLevel leaves its quote open; the message still reads.
        Assert.IsTrue(gold.Movie("FRServer_StopLevel\t4\t1)"));
        Assert.IsTrue(gold.Movie("FRServer_GameWon\t40000\t10"));

        Assert.AreEqual(true, gold.Result);
        Assert.AreEqual(21789, catalog[137].Items.Single().ItemId, "A Gold Bar.");
        Assert.AreEqual("OnGameWonMsg", gold.Sent[^1]);
    }

    [TestMethod]
    public void Music_PlaysOneSong_AndTakesAWinOnlyOnceItCouldHaveBeenHeard()
    {
        var catalog = LoadCatalog();
        var hard = new Play(catalog[205]);

        hard.Connect();

        var rounds = MovieRounds(hard.Sent);
        Assert.AreEqual(1, rounds.Count);
        Assert.AreEqual("305", rounds[1][MimicRow.StageTypeColumn], "Very Hard plays the fifth song.");

        hard.Now += 20_000;
        hard.Movie("FRServer_GameWon\t50000\t0");

        Assert.IsNull(hard.Result, "The song is 58 seconds long.");

        // Booed off that soon is still a loss.
        var booed = new Play(catalog[205]);
        booed.Connect();
        booed.Now += 20_000;
        booed.Movie("FRServer_GameLost\t1200");

        Assert.AreEqual(false, booed.Result);

        var again = new Play(catalog[205]);
        again.Connect();
        again.Now += 70_000;
        again.Movie("FRServer_GameWon\t50000\t0");

        Assert.AreEqual(true, again.Result);
        Assert.AreEqual("OnGameWonMsg", again.Sent[^1]);

        // The first Feeling the Beat! (and its copy) plays one of the three easier songs.
        foreach (var id in new[] { 78, 206 })
        {
            var plain = new Play(catalog[id]);
            plain.Connect();

            var song = MovieRounds(plain.Sent)[1];

            Assert.AreEqual("78", song[MimicRow.GameColumn]);
            CollectionAssert.Contains(new[] { "301", "302", "303" }, song[MimicRow.StageTypeColumn]);
        }
    }

    [TestMethod]
    public void HackNSlash_SendsItsDummy_AndNeedsItsLastRoundWon()
    {
        var catalog = LoadCatalog();
        var play = new Play(catalog[79]);

        play.Connect();

        // The dummy after the rounds, before OnDataComplete: the movie reads it as it finishes the rounds.
        Assert.AreEqual("OnCustomDataMsg\t7901^79^3^3^5^5^7^3^4", play.Sent[^2]);

        var rounds = MovieRounds(play.Sent);
        Assert.AreEqual(6, rounds.Count);

        foreach (var (sequence, _) in rounds)
        {
            play.Now += 15_000;
            Assert.IsTrue(play.Movie("FRServer_CompleteLevel"));

            if (sequence == rounds.Count)
                Assert.IsTrue(play.Movie($"FRServer_StopLevel\t{sequence}\t1"));
        }

        play.Movie("FRServer_GameWon\t6000\t0");

        Assert.AreEqual(true, play.Result);
        Assert.AreEqual("OnGameWonMsg", play.Sent[^1]);

        // A win the movie never stopped its last round on is a loss.
        var unstopped = new Play(catalog[79]);
        unstopped.Connect();
        unstopped.Now += 120_000;
        unstopped.Movie("FRServer_GameWon\t6000\t0");

        Assert.AreEqual(false, unstopped.Result);
        Assert.AreEqual("OnGameLostMsg", unstopped.Sent[^1]);
    }
}
