using System;
using System.IO;
using System.Linq;
using System.Text.Json;

using Microsoft.Extensions.Logging;

using Sanctuary.Game.Minigames.Soe;

namespace Sanctuary.Game.Minigames.SpotTheDifference;

/// <summary>
/// Spot the Difference (game_hidden.swf, SOE's hidden-object game): rounds of pictures, each with differences to
/// click before its time runs out. The movie shows the picture its OnLevelInitMsg names (a frame of its level
/// view) and sends every click (OnReleaseMsg, in the picture's pixels); the server holds where the differences
/// are, decides each click (found, missed, or a run of misses that costs time) and runs each round's timer, as
/// the movie's GameServer did. The pictures and differences are in <see cref="SpotTheDifferencePuzzles"/>.
/// </summary>
[FlashMinigame("SpotTheDifference")]
public sealed class SpotTheDifferenceGame : SoeGameServer
{
    #region HiddenNetworkEvent

    public const int ClickFoundEvent = 11;
    public const int ClickNoneEvent = 12;
    public const int ClickWrongEvent = 13;
    public const int ScoreIncrementEvent = 14;

    #endregion

    /// <summary>
    /// Finds this close together build a chain, each worth one more times the points (the movie's ScoreModifier).
    /// </summary>
    private const long ChainWindow = 5000;
    private const int MaxChain = 5;

    private SpotTheDifferenceLevel? _level;

    private int _hints;
    private int _points;
    private int _chain;
    private long _lastFoundAt;

    /// <summary>
    /// The difficulty being played, once the puzzles are loaded.
    /// </summary>
    public SpotTheDifferenceLevel? Settings => _level;

    public int HintsLeft => _hints;

    public SpotTheDifferenceRound? Round => CurrentLevel as SpotTheDifferenceRound;

    protected override void GameInitialize()
    {
        var path = Path.Combine(DataDirectory, SpotTheDifferencePuzzles.FileName);
        SpotTheDifferencePuzzles puzzles;

        try
        {
            puzzles = SpotTheDifferencePuzzles.Load(path);
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            Logger.LogError(ex, "Failed to load Spot the Difference's puzzles from \"{path}\".", path);
            return;
        }

        _level = puzzles.Levels.Find(x => x.Level == Definition.Level) ?? puzzles.Levels.FirstOrDefault();

        if (_level is null)
        {
            Logger.LogError("\"{path}\" has no levels.", path);
            return;
        }

        var boards = _level.Frames.Count > 0
            ? _level.Frames.Select(frame => puzzles.Boards.Find(board => board.Frame == frame)).OfType<SpotTheDifferenceBoard>().ToList()
            : puzzles.Boards.OrderBy(_ => Random.Shared.Next()).Take(_level.Rounds).ToList();

        boards.RemoveAll(board => board.Differences.Count == 0);

        for (var i = 0; i < boards.Count; i++)
        {
            AddLevel(i + 1, new SpotTheDifferenceRound(this, boards[i], _level)
            {
                Level = i + 1
            });
        }
    }

    /// <summary>
    /// The movie waits for OnServerReadyMsg; without puzzles there's nothing to play.
    /// </summary>
    protected override bool IsGameReady() => LevelCount > 0;

    protected override void GameClientConnect()
    {
        SendActive(false);
    }

    protected override void OnStartGame()
    {
        _hints = Math.Min(_level!.HintsAtStart, _level.HintsMax);
        _points = 0;
        _chain = 0;

        NextLevel();
    }

    protected override void OnNextLevel()
    {
        // The movie answers with OnStartLevelMsg once its round-start screen is done.
        SendNextLevel(Level, CurrentLevel.Goal, 0);
    }

    protected override void OnStartLevel()
    {
        var round = Round!;

        if (Level > 1)
            _hints = Math.Min(_hints + _level!.HintsPerRound, _level.HintsMax);

        // Starting the level zeroed the framework's score; the game's runs on.
        Score = _points;

        Send("OnLevelInitMsg", round.Board.Frame, (int)round.EndDuration, round.Goal, _hints, _level!.HintsMax);

        // No hourglasses: the movie's only ones belong to its hidden-object scene.
        Send("OnLevelHourglassInitMsg", 0, 0);

        for (var i = 0; i < round.Spots.Count; i++)
            Send("OnAddItemMsg", i, round.Spots[i].NameId, 0);

        SetActive(true);
        SendActive(true);
    }

    protected override void GameApplyTick(long delta)
    {
        if (IsInProgress && !IsPaused && CheckLevelDone())
            StopLevel();
    }

    protected override void OnStopLevel()
    {
        var round = Round!;

        SetActive(false);
        SendActive(false);

        // The movie shows its round-end screen and asks for the next round (OnNextLevelMsg) from it.
        Send("OnStopLevelMsg", Level, round.Goal, round.Score, round.Goal > 0 ? round.FoundCount * 100 / round.Goal : 100);
    }

    protected override void OnNextLevelMsg()
    {
        // Only from the round-end screen: a round still running isn't lost yet.
        if (CurrentLevel.IsInProgress)
            return;

        base.OnNextLevelMsg();
    }

    protected override bool OnGameMessage(string name, FlashArgs args)
    {
        switch (name)
        {
            case "OnReleaseMsg":
                Release(args.Int(0, int.MinValue), args.Int(1, int.MinValue));
                return true;

            case "OnHintRequestMsg":
                UseHint();
                return true;

            case "OnHourglassRequestMsg":
            case "OnMagnifyingGlassRequestMsg":
                // The game hands out neither.
                return true;
        }

        return false;
    }

    /// <summary>
    /// A click on the picture (the movie's GameLevel.OnRelease).
    /// </summary>
    private void Release(int x, int y)
    {
        if (!IsActive || Round is not { IsInProgress: true } round)
            return;

        var index = round.FindAt(x, y);

        if (index >= 0)
        {
            var spot = round.Spots[index];

            round.MarkFound(index);
            round.WrongCount = 0;

            _chain = _chain > 0 && Clock - _lastFoundAt <= ChainWindow ? Math.Min(_chain + 1, MaxChain) : 1;
            _lastFoundAt = Clock;

            SendLevelEvent(ClickFoundEvent, index, spot.Clip, spot.NameId);
            round.DeltaScore(1);
            AddPoints(_level!.PointsPerDifference * _chain);

            if (round.IsCleared)
            {
                AddPoints((int)(round.GetTimeLeft() / 1000) * _level.PointsPerSecondLeft);
                round.SetDoneFlag();
            }

            return;
        }

        round.WrongCount++;

        if (round.WrongCount >= _level!.WrongClicks)
        {
            round.WrongCount = 0;
            round.ChangeTimerDuration(-_level.TimePenalty);
            SendLevelEvent(ClickWrongEvent, _level.TimePenalty, x, y);
        }
        else
        {
            SendLevelEvent(ClickNoneEvent);
        }
    }

    /// <summary>
    /// The hint button: points out a difference (the movie sparkles its clip).
    /// </summary>
    private void UseHint()
    {
        if (!IsActive || Round is not { IsInProgress: true } round || _hints <= 0)
            return;

        var index = round.PickHint(Random.Shared);

        if (index < 0)
            return;

        _hints--;
        Send("OnHintUsedMsg", _hints, round.Spots[index].Clip);
    }

    /// <summary>
    /// Adds to the game's score and the score counter the client shows above the movie.
    /// </summary>
    private void AddPoints(int points)
    {
        // The round's score (differences found) moved the framework's total; the game keeps its own.
        Score = _points;

        if (points <= 0)
            return;

        _points += points;
        Score = _points;

        SendLevelEvent(ScoreIncrementEvent, points);
    }

    private void SendLevelEvent(int type, params object[] args)
    {
        Send("OnLevelEventMsg", [type, .. args]);
    }
}
