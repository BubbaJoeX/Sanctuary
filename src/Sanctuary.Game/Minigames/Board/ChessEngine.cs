using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Sanctuary.Game.Minigames.Board;

/// <summary>
/// The computer's chess: an alpha-beta search over material and piece placement, as deep as its skill allows.
/// The Free Realms client carries a native engine the live server could ask for moves (the movie's
/// ChessInit/ChessMoveRequest commands); its move format isn't known, so the server plays its own and the movie
/// only shows the moves. Lower skills look less far ahead and pick among nearly-best moves at random, so they
/// make the mistakes a beginner would.
/// </summary>
public static class ChessEngine
{
    public const int MateScore = 100000;

    private const int Infinity = 1000000;

    private static readonly int[] PieceValues = [0, 100, 500, 320, 330, 900, 0];

    #region Piece-square tables (white's view, square 0 = a8)

    private static readonly int[] PawnTable =
    [
        0, 0, 0, 0, 0, 0, 0, 0,
        50, 50, 50, 50, 50, 50, 50, 50,
        10, 10, 20, 30, 30, 20, 10, 10,
        5, 5, 10, 25, 25, 10, 5, 5,
        0, 0, 0, 20, 20, 0, 0, 0,
        5, -5, -10, 0, 0, -10, -5, 5,
        5, 10, 10, -20, -20, 10, 10, 5,
        0, 0, 0, 0, 0, 0, 0, 0
    ];

    private static readonly int[] KnightTable =
    [
        -50, -40, -30, -30, -30, -30, -40, -50,
        -40, -20, 0, 0, 0, 0, -20, -40,
        -30, 0, 10, 15, 15, 10, 0, -30,
        -30, 5, 15, 20, 20, 15, 5, -30,
        -30, 0, 15, 20, 20, 15, 0, -30,
        -30, 5, 10, 15, 15, 10, 5, -30,
        -40, -20, 0, 5, 5, 0, -20, -40,
        -50, -40, -30, -30, -30, -30, -40, -50
    ];

    private static readonly int[] BishopTable =
    [
        -20, -10, -10, -10, -10, -10, -10, -20,
        -10, 0, 0, 0, 0, 0, 0, -10,
        -10, 0, 5, 10, 10, 5, 0, -10,
        -10, 5, 5, 10, 10, 5, 5, -10,
        -10, 0, 10, 10, 10, 10, 0, -10,
        -10, 10, 10, 10, 10, 10, 10, -10,
        -10, 5, 0, 0, 0, 0, 5, -10,
        -20, -10, -10, -10, -10, -10, -10, -20
    ];

    private static readonly int[] RookTable =
    [
        0, 0, 0, 0, 0, 0, 0, 0,
        5, 10, 10, 10, 10, 10, 10, 5,
        -5, 0, 0, 0, 0, 0, 0, -5,
        -5, 0, 0, 0, 0, 0, 0, -5,
        -5, 0, 0, 0, 0, 0, 0, -5,
        -5, 0, 0, 0, 0, 0, 0, -5,
        -5, 0, 0, 0, 0, 0, 0, -5,
        0, 0, 0, 5, 5, 0, 0, 0
    ];

    private static readonly int[] QueenTable =
    [
        -20, -10, -10, -5, -5, -10, -10, -20,
        -10, 0, 0, 0, 0, 0, 0, -10,
        -10, 0, 5, 5, 5, 5, 0, -10,
        -5, 0, 5, 5, 5, 5, 0, -5,
        0, 0, 5, 5, 5, 5, 0, -5,
        -10, 5, 5, 5, 5, 5, 0, -10,
        -10, 0, 5, 0, 0, 0, 0, -10,
        -20, -10, -10, -5, -5, -10, -10, -20
    ];

    private static readonly int[] KingTable =
    [
        -30, -40, -40, -50, -50, -40, -40, -30,
        -30, -40, -40, -50, -50, -40, -40, -30,
        -30, -40, -40, -50, -50, -40, -40, -30,
        -30, -40, -40, -50, -50, -40, -40, -30,
        -20, -30, -30, -40, -40, -30, -30, -20,
        -10, -20, -20, -20, -20, -20, -20, -10,
        20, 20, 0, 0, 0, 0, 20, 20,
        20, 30, 10, 0, 0, 10, 30, 20
    ];

    /// <summary>
    /// The king's table once the queens are off: come to the middle.
    /// </summary>
    private static readonly int[] KingEndgameTable =
    [
        -50, -40, -30, -20, -20, -30, -40, -50,
        -30, -20, -10, 0, 0, -10, -20, -30,
        -30, -10, 20, 30, 30, 20, -10, -30,
        -30, -10, 30, 40, 40, 30, -10, -30,
        -30, -10, 30, 40, 40, 30, -10, -30,
        -30, -10, 20, 30, 30, 20, -10, -30,
        -30, -30, 0, 0, 0, 0, -30, -30,
        -50, -30, -30, -30, -30, -30, -30, -50
    ];

    #endregion

    /// <summary>
    /// How each skill (1 to 5) plays: how many moves ahead it looks, whether it follows captures to the end, and
    /// how many points of noise it adds to each candidate (so it sometimes picks a worse move).
    /// </summary>
    private static (int Depth, bool Quiescence, int Noise) Settings(int skill)
    {
        return Math.Clamp(skill, 1, 5) switch
        {
            1 => (1, false, 120),
            2 => (2, false, 60),
            3 => (2, true, 25),
            4 => (3, true, 10),
            _ => (4, true, 0)
        };
    }

    /// <summary>
    /// The computer's move for the side to move, or null when it has none. Runs on a copy: the position is
    /// left as it was.
    /// </summary>
    public static ChessMove? ChooseMove(ChessPosition position, int skill, Random random, int timeLimitMs = 2000)
    {
        // A search cut short by the clock leaves its moves made; it works on its own copy.
        position = position.Clone();

        var rootMoves = position.LegalMoves();

        if (rootMoves.Count == 0)
            return null;

        if (rootMoves.Count == 1)
            return rootMoves[0];

        var (depth, quiescence, noise) = Settings(skill);
        var search = new Search(quiescence, Stopwatch.StartNew(), timeLimitMs);
        var scores = new int[rootMoves.Count];

        OrderMoves(position, rootMoves);

        // Even the shallowest skill takes a mate in one and doesn't stalemate a beaten side, which the leaves of
        // a one-move search can't see.
        var endings = new int?[rootMoves.Count];

        for (var i = 0; i < rootMoves.Count; i++)
        {
            var undo = position.MakeMove(rootMoves[i]);

            if (position.LegalMoves().Count == 0)
                endings[i] = position.IsInCheck(position.WhiteToMove) ? MateScore - 1 : 0;

            position.UnmakeMove(rootMoves[i], undo);
        }

        // Deepen one move at a time, keeping the last depth that finished in time.
        for (var currentDepth = 1; currentDepth <= depth; currentDepth++)
        {
            var depthScores = new int[rootMoves.Count];

            try
            {
                for (var i = 0; i < rootMoves.Count; i++)
                {
                    var undo = position.MakeMove(rootMoves[i]);
                    depthScores[i] = -search.AlphaBeta(position, currentDepth - 1, -Infinity, Infinity, 1);
                    position.UnmakeMove(rootMoves[i], undo);
                }
            }
            catch (TimeoutException)
            {
                break;
            }

            scores = depthScores;
        }

        var best = 0;
        var bestScore = int.MinValue;

        for (var i = 0; i < rootMoves.Count; i++)
        {
            var score = endings[i] ?? scores[i];

            // Noise never hides a mate either way.
            if (noise > 0 && Math.Abs(score) < MateScore / 2)
                score += random.Next(-noise, noise + 1);

            if (score > bestScore)
            {
                bestScore = score;
                best = i;
            }
        }

        return rootMoves[best];
    }

    /// <summary>
    /// The position's worth to the side to move, in centipawns.
    /// </summary>
    public static int Evaluate(ChessPosition position)
    {
        var score = 0;
        var queens = 0;

        for (var square = 0; square < 64; square++)
        {
            if (ChessPosition.TypeOf(position[square]) == ChessPieceType.Queen)
                queens++;
        }

        for (var square = 0; square < 64; square++)
        {
            var piece = position[square];

            if (piece == 0)
                continue;

            var white = piece > 0;
            var type = ChessPosition.TypeOf(piece);

            // The tables are drawn for white; black reads them upside down.
            var tableSquare = white ? square : square ^ 56;

            var value = PieceValues[(int)type] + type switch
            {
                ChessPieceType.Pawn => PawnTable[tableSquare],
                ChessPieceType.Knight => KnightTable[tableSquare],
                ChessPieceType.Bishop => BishopTable[tableSquare],
                ChessPieceType.Rook => RookTable[tableSquare],
                ChessPieceType.Queen => QueenTable[tableSquare],
                _ => queens == 0 ? KingEndgameTable[tableSquare] : KingTable[tableSquare]
            };

            score += white ? value : -value;
        }

        return position.WhiteToMove ? score : -score;
    }

    /// <summary>
    /// Captures first, the most valuable victim by the cheapest attacker, then promotions.
    /// </summary>
    private static void OrderMoves(ChessPosition position, List<ChessMove> moves)
    {
        moves.Sort((a, b) => MoveOrder(position, b).CompareTo(MoveOrder(position, a)));
    }

    private static int MoveOrder(ChessPosition position, ChessMove move)
    {
        var order = 0;
        var victim = move.Kind == ChessMoveKind.EnPassant ? ChessPieceType.Pawn : ChessPosition.TypeOf(position[move.To]);

        if (victim != ChessPieceType.None)
            order += 10 * PieceValues[(int)victim] - PieceValues[(int)ChessPosition.TypeOf(position[move.From])] / 10;

        if (move.Kind == ChessMoveKind.Promotion)
            order += PieceValues[(int)move.Promotion];

        if (move.Kind == ChessMoveKind.Castle)
            order += 50;

        return order;
    }

    private sealed class Search(bool quiescence, Stopwatch clock, int timeLimitMs)
    {
        private const int MaxQuiescenceDepth = 6;

        private int _nodes;

        public int AlphaBeta(ChessPosition position, int depth, int alpha, int beta, int ply)
        {
            CheckTime();

            if (position.HalfmoveClock >= 100)
                return 0;

            if (depth <= 0)
                return quiescence ? Quiesce(position, alpha, beta, 0) : Evaluate(position);

            var moves = new List<ChessMove>(48);
            position.GenerateMoves(moves, capturesOnly: false);
            OrderMoves(position, moves);

            var white = position.WhiteToMove;
            var legal = 0;

            foreach (var move in moves)
            {
                var undo = position.MakeMove(move);

                if (position.IsInCheck(white))
                {
                    position.UnmakeMove(move, undo);
                    continue;
                }

                legal++;

                var score = -AlphaBeta(position, depth - 1, -beta, -alpha, ply + 1);
                position.UnmakeMove(move, undo);

                if (score >= beta)
                    return beta;

                if (score > alpha)
                    alpha = score;
            }

            if (legal == 0)
                return position.IsInCheck(white) ? -MateScore + ply : 0;

            return alpha;
        }

        /// <summary>
        /// Follows the captures out so the search doesn't stop in the middle of an exchange.
        /// </summary>
        private int Quiesce(ChessPosition position, int alpha, int beta, int depth)
        {
            CheckTime();

            var standPat = Evaluate(position);

            if (standPat >= beta)
                return beta;

            if (standPat > alpha)
                alpha = standPat;

            if (depth >= MaxQuiescenceDepth)
                return alpha;

            var moves = new List<ChessMove>(16);
            position.GenerateMoves(moves, capturesOnly: true);
            OrderMoves(position, moves);

            var white = position.WhiteToMove;

            foreach (var move in moves)
            {
                var undo = position.MakeMove(move);

                if (position.IsInCheck(white))
                {
                    position.UnmakeMove(move, undo);
                    continue;
                }

                var score = -Quiesce(position, -beta, -alpha, depth + 1);
                position.UnmakeMove(move, undo);

                if (score >= beta)
                    return beta;

                if (score > alpha)
                    alpha = score;
            }

            return alpha;
        }

        private void CheckTime()
        {
            if (++_nodes % 1024 == 0 && clock.ElapsedMilliseconds > timeLimitMs)
                throw new TimeoutException();
        }
    }
}
