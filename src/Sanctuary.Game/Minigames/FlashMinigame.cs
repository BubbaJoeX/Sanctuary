using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Resources.Definitions;
using Sanctuary.Packet;

namespace Sanctuary.Game.Minigames;

/// <summary>
/// Names the <see cref="FlashMinigame"/> that runs the games whose <see cref="MinigameDefinition.Game"/> matches.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class FlashMinigameAttribute(string name) : Attribute
{
    public string Name { get; } = name;
}

/// <summary>
/// One player's game of a Flash minigame, from the movie loading to it closing. The movie talks to the server
/// with tab-separated messages (its SoeNetworkTypeFreeRealms): <see cref="MinigameManager"/> takes the
/// FRServer_* messages every movie sends and hands the rest to <see cref="OnMessage"/>.
/// Calls into a game are serialized, so a game needs no locking of its own.
/// </summary>
public abstract class FlashMinigame
{
    internal readonly object SyncRoot = new();

    private long _startedAt;

    private Func<long>? _clock;
    private Action<string>? _outbox;
    private Action<bool>? _onFinish;

    public Player Player { get; private set; } = null!;
    public MinigameDefinition Definition { get; private set; } = null!;

    protected IRewardManager RewardManager { get; private set; } = null!;
    protected IResourceManager ResourceManager { get; private set; } = null!;
    protected ILogger Logger { get; private set; } = null!;

    public int StageId => Definition.Id;

    /// <summary>
    /// The score the game ends on. The movie's own score reports (FRServer_ScoreSet, FRServer_GameWon, ...) set it,
    /// unless <see cref="KeepsScore"/>.
    /// </summary>
    public int Score { get; set; }

    /// <summary>
    /// True for games the server plays itself (it holds the board, the timer and the score), so the movie's
    /// reports of a score or a result are only for show.
    /// </summary>
    public virtual bool KeepsScore => false;

    /// <summary>
    /// Set once the game has paid out; the movie may still show its ending.
    /// </summary>
    public bool Finished { get; private set; }

    public bool Won { get; private set; }

    /// <summary>
    /// Lines for the end score screen (FRServer_ScoreInfo).
    /// </summary>
    public List<MinigameScoreEntry> ScoreEntries { get; } = [];

    /// <summary>
    /// Milliseconds since the movie was started; the clock everything in a game runs on (the movie's getTimer).
    /// </summary>
    protected long Now => _clock?.Invoke() ?? Environment.TickCount64 - _startedAt;

    /// <summary>
    /// The game's own data, when its movie's server data never shipped with the client: Resources/Minigames/&lt;Game&gt;.
    /// </summary>
    public string DataDirectory { get; private set; } = string.Empty;

    internal void Initialize(Player player, MinigameDefinition definition, IRewardManager rewardManager,
        IResourceManager resourceManager, ILogger logger)
    {
        Player = player;
        Definition = definition;
        RewardManager = rewardManager;
        ResourceManager = resourceManager;
        Logger = logger;
        DataDirectory = System.IO.Path.Combine(Game.ResourceManager.MinigamesDirectory, definition.Game);

        _startedAt = Environment.TickCount64;
    }

    /// <summary>
    /// A game outside the server, for tests that play it through its messages: what it sends the movie goes to
    /// <paramref name="outbox"/>, its result to <paramref name="onFinish"/> (nothing is paid), and its clock is
    /// <paramref name="clock"/> (milliseconds) when given.
    /// </summary>
    public static T CreateDetached<T>(MinigameDefinition definition, string dataDirectory, Action<string> outbox,
        Action<bool>? onFinish = null, Func<long>? clock = null) where T : FlashMinigame, new()
    {
        var game = new T
        {
            Definition = definition,
            DataDirectory = dataDirectory,
            Logger = NullLogger.Instance,
            _outbox = outbox,
            _onFinish = onFinish,
            _clock = clock,
            _startedAt = Environment.TickCount64
        };

        return game;
    }

    /// <summary>
    /// The movie is loading; it sends OnConnectMsg (or FRServer_Connect) once it's up.
    /// </summary>
    public virtual void OnStart()
    {
    }

    /// <summary>
    /// A message from the movie. Returns false for messages the game doesn't know, which are logged.
    /// </summary>
    public abstract bool OnMessage(string name, FlashArgs args);

    /// <summary>
    /// The movie paused or resumed (its menu, a tutorial, a round transition): FRServer_Pause.
    /// </summary>
    public virtual void OnPause(bool paused)
    {
    }

    /// <summary>
    /// Every zone tick while the game is open.
    /// </summary>
    public virtual void Tick()
    {
    }

    /// <summary>
    /// The movie closed or the player left. Nothing more is sent after this.
    /// </summary>
    public virtual void OnClose()
    {
    }

    /// <summary>
    /// Answers FRServer_RequestStageId: which stage of the game to play (the movie's VOnServerSetStageIdMsg).
    /// </summary>
    public virtual void SendStageId()
    {
        Send("VOnServerSetStageIdMsg", Definition.Level);
    }

    /// <summary>
    /// Sends a message to the movie: the name and its arguments, tab-separated. Like the movie's own AddMessage,
    /// lists are comma-separated and booleans are true/false.
    /// </summary>
    protected void Send(string name, params object?[] args)
    {
        SendRaw(FormatMessage(name, args));
    }

    protected void SendRaw(string payload)
    {
        if (_outbox is not null)
        {
            _outbox(payload);
            return;
        }

        MinigameManager.Trace(this, "<", payload);

        Player.SendTunneled(new MiniGamePayloadPacket
        {
            StageId = StageId,
            Payload = payload
        });
    }

    /// <summary>
    /// Ends the game with a result and pays out (<see cref="MinigameManager.Finish"/>). The movie stays up for its
    /// ending and closes itself (FRServer_GameOver or FRServer_GameClose).
    /// </summary>
    protected void Finish(bool won)
    {
        if (_outbox is not null)
        {
            if (!Finished)
            {
                MarkFinished(won);
                _onFinish?.Invoke(won);
            }

            return;
        }

        MinigameManager.Finish(this, won);
    }

    internal void MarkFinished(bool won)
    {
        Finished = true;
        Won = won;
    }

    public static string FormatMessage(string name, params object?[] args)
    {
        if (args.Length == 0)
            return name;

        return name + "\t" + string.Join('\t', args.Select(FormatArgument));
    }

    public static string FormatArgument(object? arg)
    {
        return arg switch
        {
            null => "undefined",
            string text => text,
            bool value => value ? "true" : "false",
            float value => value.ToString(CultureInfo.InvariantCulture),
            double value => value.ToString(CultureInfo.InvariantCulture),
            IFormattable value => value.ToString(null, CultureInfo.InvariantCulture),
            IEnumerable values => string.Join(',', values.Cast<object?>().Select(FormatArgument)),
            _ => arg.ToString() ?? string.Empty
        };
    }
}

/// <summary>
/// A line the movie reports for its score screen (FRServer_ScoreInfo): a label, an icon, how to show it
/// (0 a count, 2 a time, 4 the total), the count and its maximum, and the points it's worth.
/// </summary>
public sealed record MinigameScoreEntry(string Label, int IconId, int Type, int Count, int Max, int Points);

/// <summary>
/// A Flash message's arguments. The movie converts its own the same way (SoeNetworkTypeFreeRealms.ConvertArg):
/// numbers, true/false and "undefined".
/// </summary>
public readonly struct FlashArgs(string[] values)
{
    private readonly string[] _values = values;

    public int Count => _values?.Length ?? 0;

    public string this[int index] => index < Count ? _values[index] : string.Empty;

    public bool Has(int index) => index < Count && _values[index] != "undefined" && _values[index].Length > 0;

    public string String(int index, string fallback = "") => Has(index) ? _values[index] : fallback;

    public int Int(int index, int fallback = 0)
    {
        if (!Has(index))
            return fallback;

        if (int.TryParse(_values[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            return value;

        return double.TryParse(_values[index], NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
            && number is >= int.MinValue and <= int.MaxValue ? (int)number : fallback;
    }

    public double Double(int index, double fallback = 0)
    {
        return Has(index) && double.TryParse(_values[index], NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : fallback;
    }

    /// <summary>
    /// true/false, or a number (non-zero is true), as the movie sends either.
    /// </summary>
    public bool Bool(int index, bool fallback = false)
    {
        if (!Has(index))
            return fallback;

        var value = _values[index];

        if (value.Equals("true", StringComparison.OrdinalIgnoreCase))
            return true;

        if (value.Equals("false", StringComparison.OrdinalIgnoreCase))
            return false;

        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number != 0 : fallback;
    }

    /// <summary>
    /// A list argument the movie joined with commas (an ActionScript array), as ints.
    /// </summary>
    public int[] IntList(int index, char separator = ',')
    {
        if (!Has(index))
            return [];

        return _values[index].Split(separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => int.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0)
            .ToArray();
    }

    public string[] All => _values ?? [];
}
