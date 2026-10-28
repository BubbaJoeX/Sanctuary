using System;
using System.Diagnostics;

namespace Sanctuary.Game.Minigames.Board;

/// <summary>
/// The computer's checkers: an alpha-beta search over material, men's progress toward their crown and a guarded
/// back row. The movie's own computer (its VS_AI mode) plays random moves and gets chains of jumps wrong, so the
/// server plays instead and sends its moves like another player's.
/// </summary>
public static class CheckersEngine
{
    public const int WinScore = 100000;

    private const int Infinity = 1000000;

    /// <summary>
    /// How far each skill (1 to 5) looks ahead, and how many points of noise it adds to its candidates.
    /// </summary>
    private static (int Depth, int Noise) Settings(int skill)
    {
        return Math.Clamp(skill, 1, 5) switch
        {
            1 => (1, 60),
            2 => (3, 25),
            3 => (5, 10),
            4 => (7, 5),
            _ => (9, 0)
        };
    }

    /// <summary>
    /// The computer's move for the side to move, or null when it has none.
    /// </summary>
    public static CheckersMove? ChooseMove(CheckersPosition position, int skill, Random random, int timeLimitMs = 2000)
    {
        var moves = position.LegalMoves();

        if (moves.Count == 0)
            return null;

        if (moves.Count == 1)
            return moves[0];

        var (depth, noise) = Settings(skill);
        var clock = Stopwatch.StartNew();
        var scores = new int[moves.Count];

        for (var currentDepth = 1; currentDepth <= depth; currentDepth++)
        {
            var depthScores = new int[moves.Count];

            try
            {
                for (var i = 0; i < moves.Count; i++)
                {
                    var next = position.Clone();
                    next.Play(moves[i]);
                    depthScores[i] = -AlphaBeta(next, currentDepth - 1, -Infinity, Infinity, 1, clock, timeLimitMs);
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

        for (var i = 0; i < moves.Count; i++)
        {
            var score = scores[i];

            if (noise > 0 && Math.Abs(score) < WinScore / 2)
                score += random.Next(-noise, noise + 1);

            if (score > bestScore)
            {
                bestScore = score;
                best = i;
            }
        }

        return moves[best];
    }

    /// <summary>
    /// The position's worth to the side to move.
    /// </summary>
    public static int Evaluate(CheckersPosition position)
    {
        var score = 0;

        for (var spot = 0; spot < CheckersPosition.Spots; spot++)
        {
            var piece = position[spot];

            if (piece == 0)
                continue;

            var side = CheckersPosition.SideOf(piece);
            var row = spot / 4;
            int value;

            if (CheckersPosition.IsKing(piece))
            {
                // Kings are worth more in the middle, where they reach more.
                value = 160 + (row is >= 2 and <= 5 ? 5 : 0);
            }
            else
            {
                // A man's progress: rows covered toward its crown, and a bonus for holding the back row.
                var progress = side == BoardGameServer.PlayerSide ? 7 - row : row;
                value = 100 + progress * 4 + (progress == 0 ? 8 : 0);
            }

            var column = spot % 4;

            if (column is 1 or 2 && row is >= 2 and <= 5)
                value += 3;

            score += side == BoardGameServer.PlayerSide ? value : -value;
        }

        return position.SideToMove == BoardGameServer.PlayerSide ? score : -score;
    }

    private static int AlphaBeta(CheckersPosition position, int depth, int alpha, int beta, int ply, Stopwatch clock, int timeLimitMs)
    {
        if (clock.ElapsedMilliseconds > timeLimitMs)
            throw new TimeoutException();

        var moves = position.LegalMoves();

        // No move left loses.
        if (moves.Count == 0)
            return -WinScore + ply;

        if (position.QuietPlies >= CheckersPosition.DrawPlies)
            return 0;

        // Jumps are forced, so they're searched on past the horizon rather than cut off mid-exchange.
        if (depth <= 0 && (!moves[0].IsJump || ply > 24))
            return Evaluate(position);

        foreach (var move in moves)
        {
            var next = position.Clone();
            next.Play(move);

            var score = -AlphaBeta(next, depth - 1, -beta, -alpha, ply + 1, clock, timeLimitMs);

            if (score >= beta)
                return beta;

            if (score > alpha)
                alpha = score;
        }

        return alpha;
    }
}
