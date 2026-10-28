using System;

using Microsoft.Extensions.Logging;

namespace Sanctuary.Game.Minigames.Board;

/// <summary>
/// Chess against the computer (game_chess.swf). The movie checks the player's moves itself and sends them as
/// OnMoveMsg, OnCastleMsg, OnPromoteMsg or OnEnPassantMsg; the server checks them again against its own board,
/// echoes them and the computer's moves as OnChessEventMsg events (its GameServer), and tells the movie about
/// check and mate, which it only shows. The player is white and moves first; squares are the movie's network
/// squares (<see cref="ChessMove"/>).
/// </summary>
[FlashMinigame("Chess")]
public sealed class ChessGame : BoardGameServer
{
    #region ChessNetworkEvent

    public const int InitEvent = 0;
    public const int TurnStartEvent = 1;
    public const int TurnEndEvent = 2;
    public const int MoveEvent = 10;
    public const int CastleEvent = 11;
    public const int PromoteEvent = 12;
    public const int EnPassantEvent = 13;
    public const int CheckEvent = 14;
    public const int CheckmateEvent = 15;
    public const int DrawEvent = 16;
    public const int StalemateEvent = 17;
    public const int InvalidMoveEvent = 20;

    #endregion

    public ChessPosition Position { get; private set; } = ChessPosition.CreateStart();

    /// <summary>
    /// A position to play from instead of the opening one, white (the player) to move. The movie always sets out
    /// the opening, so only tests use it.
    /// </summary>
    public ChessPosition? StartPosition { get; set; }

    protected override long ComputerMoveDelay => 1200;

    protected override void GameClientConnect()
    {
        SendActive(false);
    }

    protected override void OnStartGame()
    {
        Position = StartPosition?.Clone() ?? ChessPosition.CreateStart();
        CurrentSide = PlayerSide;

        // INIT with both arguments equal makes the player white, with the first move (ChessMachine.GameStart).
        SendChessEvent(InitEvent, PlayerSide, PlayerSide);
        SendChessEvent(TurnStartEvent, PlayerSide);
    }

    protected override bool OnGameMessage(string name, FlashArgs args)
    {
        switch (name)
        {
            case "OnMoveMsg":
            {
                int from = args.Int(0, -1), to = args.Int(1, -1);
                PlayerMove(from, to, move => move.From == from && move.To == to
                    && move.Kind is ChessMoveKind.Normal or ChessMoveKind.DoublePush);
                return true;
            }

            case "OnCastleMsg":
            {
                var to = args.Int(0, -1);
                PlayerMove(-1, to, move => move.Kind == ChessMoveKind.Castle && move.To == to);
                return true;
            }

            case "OnPromoteMsg":
            {
                int from = args.Int(0, -1), to = args.Int(1, -1);

                // The movie always makes a queen; an unknown piece is one too.
                var piece = (ChessPieceType)args.Int(2, (int)ChessPieceType.Queen);

                if (piece is < ChessPieceType.Rook or > ChessPieceType.Queen)
                    piece = ChessPieceType.Queen;

                PlayerMove(from, to, move => move.From == from && move.To == to && move.Kind == ChessMoveKind.Promotion
                    && move.Promotion == piece);
                return true;
            }

            case "OnEnPassantMsg":
            {
                int from = args.Int(0, -1), to = args.Int(1, -1), capture = args.Int(2, -1);
                PlayerMove(from, to, move => move.From == from && move.To == to && move.Kind == ChessMoveKind.EnPassant
                    && move.EnPassantCapture == capture);
                return true;
            }

            case "OnMoveComputerMsg":
                // The client engine's answer to MOVE_REQUEST, which this server never sends.
                return true;
        }

        return false;
    }

    private void PlayerMove(int from, int to, Predicate<ChessMove> matches)
    {
        if (!IsPlayersTurn)
        {
            SendChessEvent(InvalidMoveEvent, from, to);
            return;
        }

        var moves = Position.LegalMoves();
        var index = moves.FindIndex(matches);

        if (index < 0)
        {
            Logger.LogInformation("{player} tried an illegal chess move ({from} -> {to}).", PlayerName, from, to);

            // The movie puts the piece back (WaitingForUser.OnInvalidMoveMsg); a new turn start lets the player
            // move again.
            SendChessEvent(InvalidMoveEvent, from, to);
            SendChessEvent(TurnStartEvent, PlayerSide);
            return;
        }

        Play(moves[index], PlayerSide);
    }

    private void Play(ChessMove move, int side)
    {
        Position.Play(move);
        SendMoveEvent(move);

        var status = Position.GetStatus();

        switch (status)
        {
            case ChessStatus.Checkmate:
                SendChessEvent(CheckmateEvent);
                SendChessEvent(TurnEndEvent, side);
                Decide(side == PlayerSide);
                return;

            case ChessStatus.Stalemate:
            case ChessStatus.FiftyMoves:
            case ChessStatus.Repetition:
            case ChessStatus.InsufficientMaterial:
                // The movie has no draw ending (it doesn't know these events); a draw ends it as a loss, which
                // still pays the loss share.
                SendChessEvent(status == ChessStatus.Stalemate ? StalemateEvent : DrawEvent);
                SendChessEvent(TurnEndEvent, side);
                Decide(false);
                return;

            case ChessStatus.Check:
                SendChessEvent(CheckEvent);
                break;
        }

        var next = side == PlayerSide ? ComputerSide : PlayerSide;

        SendChessEvent(TurnEndEvent, side);
        SendChessEvent(TurnStartEvent, next);

        if (next == PlayerSide)
        {
            CurrentSide = PlayerSide;
            return;
        }

        var snapshot = Position.Clone();
        var skill = Skill;

        StartComputerTurn(() =>
        {
            var reply = ChessEngine.ChooseMove(snapshot, skill, new Random());
            return () => PlayComputerMove(reply);
        });
    }

    private void PlayComputerMove(ChessMove? move)
    {
        if (IsDecided || !IsInProgress || CurrentSide != ComputerSide)
            return;

        if (move is not { } reply || !Position.LegalMoves().Contains(reply))
        {
            // Out of moves (the status would have ended the game) or a stale answer.
            Decide(true);
            return;
        }

        Play(reply, ComputerSide);
    }

    private void SendMoveEvent(ChessMove move)
    {
        switch (move.Kind)
        {
            case ChessMoveKind.Castle:
                SendChessEvent(CastleEvent, move.To);
                break;

            case ChessMoveKind.Promotion:
                SendChessEvent(PromoteEvent, move.From, move.To, (int)move.Promotion);
                break;

            case ChessMoveKind.EnPassant:
                SendChessEvent(EnPassantEvent, move.From, move.To, move.EnPassantCapture);
                break;

            default:
                SendChessEvent(MoveEvent, move.From, move.To);
                break;
        }
    }

    private void SendChessEvent(int type, params object[] args)
    {
        Send("OnChessEventMsg", [type, .. args]);
    }
}
