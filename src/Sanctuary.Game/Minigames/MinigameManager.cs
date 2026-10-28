using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;
using Sanctuary.Game.Resources.Definitions;
using Sanctuary.Game.Resources.Definitions.Rewards;
using Sanctuary.Packet;

namespace Sanctuary.Game.Minigames;

/// <summary>
/// The Flash minigames (Resources/Minigames.json): lists them in the Games window, starts one when a player picks
/// it, carries the movie's messages to its <see cref="FlashMinigame"/>, pays out the result and closes it.
/// <para>
/// The flow, as the client runs it: PLAY in the Games window joins the activity (its id is the game's), which is
/// answered with the game's Info and opens its start screen; the start screen's Play asks to start
/// (<see cref="BaseMiniGamePacket.RequestStartGame"/>), which loads the movie and starts it. The movie then talks
/// to the server with Flash messages until it's done (FRServer_GameOver) or closed (FRServer_GameClose,
/// FRServer_GameQuit, or the client cancelling).
/// </para>
/// </summary>
public static class MinigameManager
{
    private static ILogger _logger = null!;

    /// <summary>
    /// Every message to and from a movie, in Logs/Minigames-&lt;date&gt;.log (NLog's MinigameTrace logger).
    /// </summary>
    private static ILogger _traceLogger = null!;
    private static IRewardManager _rewardManager = null!;
    private static IResourceManager _resourceManager = null!;

    private static readonly Dictionary<string, Func<FlashMinigame>> Games = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The game each player has open.
    /// </summary>
    private static readonly ConcurrentDictionary<ulong, FlashMinigame> Sessions = new();

    /// <summary>
    /// The game each player picked in the Games window, until they start it.
    /// </summary>
    private static readonly ConcurrentDictionary<ulong, int> Picked = new();

    private static bool _configured;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        if (_configured)
            return;

        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(MinigameManager));
        _traceLogger = loggerFactory.CreateLogger("MinigameTrace");

        _rewardManager = serviceProvider.GetRequiredService<IRewardManager>();
        _resourceManager = serviceProvider.GetRequiredService<IResourceManager>();

        foreach (var type in typeof(FlashMinigame).Assembly.GetTypes())
        {
            if (type.IsAbstract || !typeof(FlashMinigame).IsAssignableFrom(type))
                continue;

            var attribute = type.GetCustomAttribute<FlashMinigameAttribute>();

            if (attribute is null || type.GetConstructor(Type.EmptyTypes) is null)
                continue;

            Games[attribute.Name] = () => (FlashMinigame)Activator.CreateInstance(type)!;
        }

        _configured = true;
    }

    /// <summary>
    /// The names of the games the server can run.
    /// </summary>
    public static IEnumerable<string> GameNames => Games.Keys;

    public static bool CanRun(MinigameDefinition definition) => Games.ContainsKey(definition.Game);

    public static FlashMinigame? GetSession(Player player) => Sessions.GetValueOrDefault(player.Guid);

    /// <summary>
    /// The Games window (Main Menu > Games) lists the activities of the tile's category; the client has no list of
    /// its own. Every game with a category and a server to run it.
    /// </summary>
    public static void SendActivities(Player player)
    {
        var activities = _resourceManager.Minigames.Values
            .Where(x => x.Category > 0 && CanRun(x))
            .OrderBy(x => x.Category)
            .ThenBy(x => x.Difficulty)
            .ThenBy(x => x.Id)
            .Select(x => new ActivityDefinition
            {
                // A minigame activity: PLAY hands it to the client's minigame controller.
                Id = x.Id,
                Type = BaseActivityServicePacket.MinigameActivityType,
                Category = x.Category,
                ImageSetId = x.IconId,
                DisplayNameId = x.NameId,
                DisplayDescriptionId = x.DescriptionId,
                NameId = x.NameId,
                DescriptionId = x.DescriptionId,
                CanPlayerJoin = true,
                DetailImageFilename = x.DetailImage,
                ThumbnailImageFilename = x.ThumbnailImage,
                Difficulty = x.Difficulty
            })
            .ToList();

        player.SendTunneled(new ActivityPacketListOfActivities
        {
            Type = BaseActivityServicePacket.MinigameActivityType,
            Activities = activities
        });
    }

    /// <summary>
    /// Answers PLAY in the Games window (or a world object or script starting a game): the game's Info opens its
    /// start screen.
    /// </summary>
    public static bool Join(Player player, int minigameId)
    {
        if (!_resourceManager.Minigames.TryGetValue(minigameId, out var definition) || !CanRun(definition))
        {
            _logger.LogWarning("{player} asked for minigame {id}, which the server doesn't run.", player.Name.FullName, minigameId);
            return false;
        }

        // A game left open (the movie never said it closed) gives way to the new one.
        Close(player, notifyClient: false);

        Picked[player.Guid] = minigameId;

        player.SendTunneled(new MiniGameInfoPacket
        {
            StageId = definition.Id,
            NameId = definition.NameId,
            IconId = definition.IconId,
            DescriptionId = definition.DescriptionId,
            Difficulty = definition.Difficulty,
            StageDefinitionId = definition.Id
        });

        return true;
    }

    /// <summary>
    /// The start screen's Play: loads the movie and starts the game.
    /// </summary>
    public static void RequestStart(Player player, int stageId)
    {
        var minigameId = Picked.TryGetValue(player.Guid, out var picked) ? picked : stageId;

        if (!_resourceManager.Minigames.TryGetValue(minigameId, out var definition)
            || !Games.TryGetValue(definition.Game, out var create))
        {
            _logger.LogWarning("{player} asked to start minigame {id}, which the server doesn't run.", player.Name.FullName, minigameId);
            return;
        }

        Close(player, notifyClient: false);

        var game = create();
        game.Initialize(player, definition, _rewardManager, _resourceManager, _logger);

        Sessions[player.Guid] = game;

        _logger.LogInformation("{player} started minigame {id} ({name}).", player.Name.FullName, definition.Id, definition.Comment);

        player.SendTunneled(new CommandPacketStartFlashGame
        {
            GameSwfName = definition.Swf,
            ReturnToPortal = true
        });

        player.SendTunneled(new MiniGameHeaderPacket
        {
            SubOpCode = BaseMiniGamePacket.StartGame,
            StageId = definition.Id
        });

        Run(game, () => game.OnStart());
    }

    /// <summary>
    /// A Flash message from the movie.
    /// </summary>
    public static void HandlePayload(Player player, int stageId, string payload)
    {
        if (!Sessions.TryGetValue(player.Guid, out var game) || game.StageId != stageId)
        {
            _logger.LogInformation("{player} sent a message for minigame {stage}, which isn't open. ( Payload: {payload} )",
                player.Name.FullName, stageId, payload.Replace('\t', '|'));
            return;
        }

        var (name, args) = ParseMessage(payload);

        Trace(game, ">", string.Join('|', [name, .. args.All]));

        if (name.Length == 0)
            return;

        Run(game, () =>
        {
            if (!HandleCommonMessage(game, name, args) && !game.OnMessage(name, args))
            {
                _logger.LogInformation("{player} sent minigame {stage} a message it doesn't handle. ( Payload: {payload} )",
                    player.Name.FullName, stageId, string.Join('|', [name, .. args.All]));
            }
        });
    }

    /// <summary>
    /// Splits a Flash message into its name and arguments. The movie's messages end with padding (control
    /// characters, spaces) after the last field.
    /// </summary>
    public static (string Name, FlashArgs Args) ParseMessage(string payload)
    {
        var cleaned = new string(payload.Where(c => c == '\t' || !char.IsControl(c)).ToArray()).TrimEnd();
        var parts = cleaned.Split('\t');

        return (parts[0].Trim(), new FlashArgs(parts[1..]));
    }

    /// <summary>
    /// The FRServer_* messages every movie sends (SoeInterfaceFreeRealms, SoeNetworkClient). The game sees them too
    /// when it wants them (FRServer_Connect, FRServer_ReadyForGameData, FRServer_StopLevel, ...).
    /// </summary>
    private static bool HandleCommonMessage(FlashMinigame game, string name, FlashArgs args)
    {
        switch (name)
        {
            case "FRServer_ScoreSet":
                if (!game.KeepsScore)
                    game.Score = args.Int(0);
                return true;

            case "FRServer_ScoreIncrement":
                if (!game.KeepsScore)
                    game.Score += args.Int(0);
                return true;

            case "FRServer_ScoreInfo":
                game.ScoreEntries.Add(new MinigameScoreEntry(args.String(0), args.Int(1), args.Int(2), args.Int(3), args.Int(4), args.Int(5)));
                return true;

            case "FRServer_Pause":
                game.OnPause(args.Bool(0));
                return true;

            case "FRServer_RequestStageId":
                // The movie's VOnServerSetStageIdMsg: which stage (level set) of the game to play.
                game.SendStageId();
                return true;

            case "FRServer_GameWon":
            case "FRServer_GameLost":
            {
                // A game the server plays has decided already; the movie only echoes it.
                if (game.KeepsScore || game.Finished)
                {
                    game.OnMessage(name, args);
                    return true;
                }

                game.Score = args.Int(0);

                if (!game.OnMessage(name, args))
                    Finish(game, name == "FRServer_GameWon");

                return true;
            }

            case "FRServer_GameOver":
                // The movie has shown its ending.
                game.OnMessage(name, args);
                Close(game.Player);
                return true;

            case "FRServer_GameClose":
            case "FRServer_GameQuit":
                game.OnMessage(name, args);
                Close(game.Player);
                return true;
        }

        return false;
    }

    /// <summary>
    /// Pays out a game's result once: coins and stars (a share of them for a loss), and for a win its items and a
    /// roll on its reward table.
    /// </summary>
    public static void Finish(FlashMinigame game, bool won)
    {
        if (game.Finished)
            return;

        game.MarkFinished(won);

        var player = game.Player;
        var definition = game.Definition;
        var share = won ? 1f : definition.LossShare;

        var coins = (int)MathF.Round(definition.Coins * share);
        var stars = (int)MathF.Round(definition.Stars * share);

        if (coins > 0)
            _rewardManager.TryGrantCurrency(player, CurrencyType.Coins, coins);

        if (stars > 0)
            _rewardManager.TryGrantExperience(player, stars, definition.ProfileId);

        var items = new List<string>();

        if (won)
        {
            foreach (var item in definition.Items)
            {
                if (_rewardManager.TryGrantItem(player, item.ItemId, 0, item.Quantity))
                    items.Add(item.Quantity > 1 ? $"{item.Quantity} {ItemName(item.ItemId)}" : ItemName(item.ItemId));
            }

            if (!string.IsNullOrEmpty(definition.RewardTable)
                && _rewardManager.TryRollReward(definition.RewardTable, out var drop) && drop is not null)
            {
                _rewardManager.TryGrantReward(player, drop);
            }
        }

        _logger.LogInformation("{player} {result} minigame {id} ({name}) with {score}: {coins} coins, {stars} stars, items {items}.",
            player.Name.FullName, won ? "won" : "lost", definition.Id, definition.Comment, game.Score, coins, stars,
            items.Count > 0 ? string.Join(", ", items) : "none");

        var earned = new List<string>();

        if (coins > 0)
            earned.Add($"{coins} coins");

        if (stars > 0)
            earned.Add($"{stars} stars");

        earned.AddRange(items);

        var title = string.IsNullOrEmpty(definition.Comment) ? "Minigame" : definition.Comment;

        ChatHelper.SendSystemMessage(player, earned.Count > 0
            ? $"{title}: {(won ? "you won" : "better luck next time")}! You earned {string.Join(", ", earned)}."
            : $"{title}: {(won ? "you won" : "better luck next time")}!");
    }

    /// <summary>
    /// Closes the player's game: the movie's window and the client's minigame state. Nothing is paid that the game
    /// hadn't paid already.
    /// </summary>
    public static void Close(Player player, bool notifyClient = true)
    {
        Picked.TryRemove(player.Guid, out _);

        if (!Sessions.TryRemove(player.Guid, out var game))
            return;

        Run(game, game.OnClose);

        if (!notifyClient)
            return;

        player.SendTunneled(new ExecuteScriptPacket { Script = "MiniGameFlash.StopAllSounds" });

        // Game over and leave return the client to the world, but leave the movie's window up; the client's
        // MiniGameFlash object closes it.
        // A game closed before it ended goes out the way the daily wheel always has (won), which the client takes.
        player.SendTunneled(new MiniGameGameOverPacket
        {
            StageId = game.StageId,
            Won = !game.Finished || game.Won
        });

        player.SendTunneled(new MiniGameHeaderPacket
        {
            SubOpCode = BaseMiniGamePacket.LeaveGame,
            StageId = game.StageId
        });

        player.SendTunneled(new ExecuteScriptPacket { Script = "MiniGameFlash.Hide" });
        player.SendTunneled(new ExecuteScriptPacket { Script = "MiniGameFlash.Unload" });
    }

    /// <summary>
    /// The client closed the start screen or the game (cancel or leave).
    /// </summary>
    public static void Cancel(Player player)
    {
        if (Sessions.ContainsKey(player.Guid))
            Close(player);
        else
            Picked.TryRemove(player.Guid, out _);
    }

    /// <summary>
    /// The player left the zone or the server: drop the game without telling the client.
    /// </summary>
    public static void Abandon(Player player)
    {
        Close(player, notifyClient: false);
    }

    /// <summary>
    /// Every zone tick, from the player's update.
    /// </summary>
    public static void Tick(Player player)
    {
        if (Sessions.TryGetValue(player.Guid, out var game))
            Run(game, game.Tick);
    }

    internal static void Trace(FlashMinigame game, string direction, string message)
    {
        _traceLogger.LogInformation("{player} {stage} {direction} {message}", game.Player.Name.FullName, game.StageId, direction,
            message.Replace('	', '|'));
    }

    private static void Run(FlashMinigame game, Action action)
    {
        lock (game.SyncRoot)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Minigame {id} ({name}) failed for {player}.", game.Definition.Id, game.Definition.Comment,
                    game.Player.Name.FullName);
            }
        }
    }

    private static string ItemName(int itemId)
    {
        return _resourceManager.ClientItemDefinitions.TryGetValue(itemId, out var item)
            ? GameNameHelper.Get(item.NameId, $"item {itemId}")
            : $"item {itemId}";
    }
}
