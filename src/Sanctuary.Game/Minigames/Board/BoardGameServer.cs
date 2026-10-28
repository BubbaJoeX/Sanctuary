using System;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using Sanctuary.Game.Minigames.Soe;

namespace Sanctuary.Game.Minigames.Board;

/// <summary>
/// A board game against the computer (chess, checkers). The movies were written for a server that relays two
/// players' moves, and treat the computer as the other player: the server holds the board, checks each of the
/// player's moves against it, answers with the computer's moves and decides when the game is over (the movies
/// don't). The computer thinks in the background, so a deep search never holds up the zone, and its move lands
/// on a later tick; the final position stays up a few seconds before the movie hears the result, because it
/// closes itself as soon as it does.
/// </summary>
public abstract class BoardGameServer : SoeGameServer
{
    /// <summary>
    /// The players as the movies number them in their turn events: the player is 0, the computer 1.
    /// </summary>
    public const int PlayerSide = 0;
    public const int ComputerSide = 1;

    private Task<Action>? _thinking;
    private long _playAt;

    private bool? _result;
    private long _resultAt;

    /// <summary>
    /// Whose turn it is.
    /// </summary>
    public int CurrentSide { get; protected set; } = PlayerSide;

    public bool IsPlayersTurn => IsInProgress && _result is null && CurrentSide == PlayerSide;

    /// <summary>
    /// True once the game is decided, while the final position is still up.
    /// </summary>
    public bool IsDecided => _result is not null || IsDone;

    /// <summary>
    /// The computer's search while it runs (tests wait on it).
    /// </summary>
    public Task? Thinking => _thinking;

    /// <summary>
    /// The player's name for logs (none when the game runs detached).
    /// </summary>
    protected string PlayerName => Player?.Name.FullName ?? "A player";

    /// <summary>
    /// The computer's strength, 1 to 5: the catalog entry's Level, else its Difficulty.
    /// </summary>
    protected int Skill => Math.Clamp(Definition.Level > 0 ? Definition.Level : Definition.Difficulty, 1, 5);

    /// <summary>
    /// The least time the computer takes over a move, so it doesn't answer while the player's move is still
    /// moving across the board.
    /// </summary>
    protected virtual long ComputerMoveDelay => 1000;

    /// <summary>
    /// How long the final position stays up before the movie is told who won.
    /// </summary>
    protected virtual long GameOverDelay => 4000;

    /// <summary>
    /// Hands the turn to the computer: <paramref name="think"/> runs in the background on a copy of the board and
    /// returns what to do with its move, which runs on a later tick.
    /// </summary>
    protected void StartComputerTurn(Func<Action> think)
    {
        CurrentSide = ComputerSide;
        _playAt = Clock + ComputerMoveDelay;
        _thinking = Task.Run(think);
    }

    /// <summary>
    /// The game is decided: the movie hears it after <see cref="GameOverDelay"/>, and the result pays out then
    /// (or when the movie closes first).
    /// </summary>
    protected void Decide(bool won)
    {
        _thinking = null;
        _result = won;
        _resultAt = Clock + GameOverDelay;
    }

    protected override void GameApplyTick(long delta)
    {
        if (_thinking is { IsCompleted: true } thinking && Clock >= _playAt)
        {
            _thinking = null;

            if (thinking.IsFaulted)
            {
                Logger.LogError(thinking.Exception, "The computer failed to find a move in minigame {id} ({name}).", Definition.Id,
                    Definition.Comment);
                OnComputerFailed();
            }
            else
            {
                thinking.Result();
            }
        }

        if (_result is { } won && Clock >= _resultAt)
        {
            _result = null;

            if (won)
                GameWon();
            else
                GameLost();
        }
    }

    /// <summary>
    /// The computer's search threw: the game can't go on, so the player wins it.
    /// </summary>
    protected virtual void OnComputerFailed()
    {
        Decide(true);
    }

    public override void OnClose()
    {
        // The movie closed on a decided game before it heard the result: it still pays.
        if (_result is { } won)
        {
            _result = null;
            Finish(won);
        }
    }
}
