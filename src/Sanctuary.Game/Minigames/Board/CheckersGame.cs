using System;
using System.Linq;

using Microsoft.Extensions.Logging;

namespace Sanctuary.Game.Minigames.Board;

/// <summary>
/// Checkers against the computer (game_checkers.swf). The movie checks the player's moves itself and sends each
/// one as OnMoveMsg (the start spot and the spots it lands on); the server checks it against its own board and
/// answers, like the movie's GameServer, with OnCheckersEventMsg events: every hop (MOVE), the piece it took
/// (MOVE_JUMPED), a crowning (MOVE_KING) and the turn passing. The computer's moves go out the same way, so the
/// movie animates them as a second player's; it has no ending of its own, so the server ends the game when a
/// side can't move.
/// </summary>
[FlashMinigame("Checkers")]
public sealed class CheckersGame : BoardGameServer
{
    #region CheckersNetworkEvent

    public const int InitEvent = 0;
    public const int TurnStartEvent = 1;
    public const int TurnEndEvent = 2;
    public const int MoveEvent = 10;
    public const int MoveJumpedEvent = 11;
    public const int MoveKingEvent = 12;
    public const int MoveInvalidEvent = 20;
    public const int BoardEvent = 30;

    #endregion

    /// <summary>
    /// The board's look (the backdrop's frame).
    /// </summary>
    private const int BoardFrame = 1;

    /// <summary>
    /// INIT's second argument: the id of the player who plays red. Red is the computer, which makes the player
    /// black, at the bottom of the board, with network spots the same as the movie's own.
    /// </summary>
    private const int RedPlayer = ComputerSide;

    private bool _boardSent;

    public CheckersPosition Position { get; private set; } = CheckersPosition.CreateStart();

    /// <summary>
    /// A position to play from instead of the opening one, the player to move. The movie always sets out the
    /// opening, so only tests use it.
    /// </summary>
    public CheckersPosition? StartPosition { get; set; }

    protected override long ComputerMoveDelay => 900;

    protected override void GameClientConnect()
    {
        SendActive(false);
    }

    protected override void OnStartGame()
    {
        Position = StartPosition?.Clone() ?? CheckersPosition.CreateStart();
        CurrentSide = PlayerSide;
        _boardSent = false;

        // The movie answers with OnReadyMsg once it has the board.
        Send("OnStartGameMsg");
        SendCheckersEvent(BoardEvent, BoardFrame);
    }

    protected override bool OnGameMessage(string name, FlashArgs args)
    {
        switch (name)
        {
            case "OnReadyMsg":
                if (IsInProgress && !_boardSent)
                {
                    _boardSent = true;

                    // No VS_AI: the movie would play the computer's moves itself.
                    SendCheckersEvent(InitEvent, PlayerSide, RedPlayer);
                    SendCheckersEvent(TurnStartEvent, PlayerSide);
                }

                return true;

            case "OnMoveMsg":
                PlayerMove(args.Int(0, -1), args.IntList(1));
                return true;
        }

        return false;
    }

    private void PlayerMove(int start, int[] path)
    {
        if (!IsPlayersTurn || !_boardSent)
        {
            SendCheckersEvent(MoveInvalidEvent, start);
            return;
        }

        var move = Position.LegalMoves().FirstOrDefault(candidate => candidate.Matches(start, path));

        if (move is null)
        {
            Logger.LogInformation("{player} tried an illegal checkers move ({start} -> {path}).", PlayerName, start, string.Join(',', path));

            // The movie has no way to take a move back; a new turn start lets the player move again.
            SendCheckersEvent(MoveInvalidEvent, start);
            SendCheckersEvent(TurnStartEvent, PlayerSide);
            return;
        }

        Play(move, PlayerSide);
    }

    private void Play(CheckersMove move, int side)
    {
        SendMoveEvents(move);
        Position.Play(move);

        var next = Position.SideToMove;

        SendCheckersEvent(TurnEndEvent, side);

        if (Position.LegalMoves().Count == 0)
        {
            // Out of pieces or blocked in: the side to move loses.
            Decide(next == ComputerSide);
            return;
        }

        if (Position.QuietPlies >= CheckersPosition.DrawPlies)
        {
            // Only kings moving, with nothing taken for forty moves each: a draw, which the movie can't show.
            Decide(false);
            return;
        }

        SendCheckersEvent(TurnStartEvent, next);

        if (next == PlayerSide)
        {
            CurrentSide = PlayerSide;
            return;
        }

        var snapshot = Position.Clone();
        var skill = Skill;

        StartComputerTurn(() =>
        {
            var reply = CheckersEngine.ChooseMove(snapshot, skill, new Random());
            return () => PlayComputerMove(reply);
        });
    }

    private void PlayComputerMove(CheckersMove? move)
    {
        if (IsDecided || !IsInProgress || CurrentSide != ComputerSide)
            return;

        var legal = move is null ? null : Position.LegalMoves().FirstOrDefault(candidate => candidate.Matches(move.Start, move.Path));

        if (legal is null)
        {
            Decide(true);
            return;
        }

        Play(legal, ComputerSide);
    }

    /// <summary>
    /// The move hop by hop, as the movie's GameServer.OnMove echoes it.
    /// </summary>
    private void SendMoveEvents(CheckersMove move)
    {
        var from = move.Start;

        for (var i = 0; i < move.Path.Length; i++)
        {
            var to = move.Path[i];

            SendCheckersEvent(MoveEvent, from, to);

            if (i < move.Jumped.Length)
                SendCheckersEvent(MoveJumpedEvent, move.Jumped[i]);

            from = to;
        }

        if (move.Crowns)
            SendCheckersEvent(MoveKingEvent, move.End);
    }

    private void SendCheckersEvent(int type, params object[] args)
    {
        Send("OnCheckersEventMsg", [type, .. args]);
    }
}
