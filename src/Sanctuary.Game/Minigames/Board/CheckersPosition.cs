using System;
using System.Collections.Generic;
using System.Linq;

namespace Sanctuary.Game.Minigames.Board;

/// <summary>
/// A checkers move: the spot it starts from and each spot it lands on (several for a chain of jumps), with the
/// pieces it takes. Spots are the movie's 32 dark squares, four to a row (<see cref="CheckersPosition"/>).
/// </summary>
public sealed class CheckersMove(int start, int[] path, int[] jumped, bool crowns)
{
    public int Start { get; } = start;

    public int[] Path { get; } = path;

    public int[] Jumped { get; } = jumped;

    /// <summary>
    /// True when a man ends on the far row and becomes a king.
    /// </summary>
    public bool Crowns { get; } = crowns;

    public int End => Path[^1];

    public bool IsJump => Jumped.Length > 0;

    public bool Matches(int start, IReadOnlyList<int> path) => Start == start && Path.SequenceEqual(path);

    public override string ToString() => $"{Start}-{string.Join('-', Path)}";
}

/// <summary>
/// A checkers board with American rules as the movie plays them (BoardProxy): men move and take forward only,
/// kings one square any way; a capture must be taken, and a chain of jumps carried on to its end (any end: not
/// necessarily the longest); taken pieces leave the board as they're jumped, and a man that reaches the far row
/// is crowned and stops. The player's pieces start on spots 20-31 and move up (to row 0) with the first move;
/// the computer's start on 0-11 and move down.
/// </summary>
public sealed class CheckersPosition
{
    public const int Spots = 32;

    public const sbyte PlayerMan = 1;
    public const sbyte PlayerKing = 2;
    public const sbyte ComputerMan = -1;
    public const sbyte ComputerKing = -2;

    /// <summary>
    /// Moves (half moves) since a piece was taken or a man moved, for the draw once only kings shuffle about.
    /// </summary>
    public const int DrawPlies = 80;

    private readonly sbyte[] _spots = new sbyte[Spots];

    /// <summary>
    /// <see cref="BoardGameServer.PlayerSide"/> or <see cref="BoardGameServer.ComputerSide"/>.
    /// </summary>
    public int SideToMove { get; private set; } = BoardGameServer.PlayerSide;

    public int QuietPlies { get; private set; }

    public sbyte this[int spot] => _spots[spot];

    public static CheckersPosition CreateStart()
    {
        var position = new CheckersPosition();

        for (var spot = 0; spot < 12; spot++)
        {
            position._spots[spot] = ComputerMan;
            position._spots[Spots - 1 - spot] = PlayerMan;
        }

        return position;
    }

    /// <summary>
    /// A position from its pieces, for tests and puzzles.
    /// </summary>
    public static CheckersPosition FromPieces(IReadOnlyDictionary<int, sbyte> pieces, int sideToMove = BoardGameServer.PlayerSide)
    {
        var position = new CheckersPosition { SideToMove = sideToMove };

        foreach (var (spot, piece) in pieces)
            position._spots[spot] = piece;

        return position;
    }

    public CheckersPosition Clone()
    {
        var clone = new CheckersPosition
        {
            SideToMove = SideToMove,
            QuietPlies = QuietPlies
        };

        Array.Copy(_spots, clone._spots, Spots);

        return clone;
    }

    public static int SideOf(sbyte piece) => piece > 0 ? BoardGameServer.PlayerSide : BoardGameServer.ComputerSide;

    public static bool IsKing(sbyte piece) => Math.Abs(piece) == PlayerKing;

    /// <summary>
    /// The spot one diagonal step away, or -1 off the board: the movie's BoardProxy.GetSpotFromOffset. Rows
    /// alternate between starting on a light and a dark square, so <paramref name="dx"/> 0 and 1 are the two
    /// diagonals (left and right) and <paramref name="dy"/> -1 and 1 up and down; the same step twice is a jump.
    /// </summary>
    public static int Step(int spot, int dx, int dy)
    {
        if (spot is < 0 or >= Spots)
            return -1;

        var row = spot / 4;
        var column = spot % 4 + dx + (row % 2 != 0 ? -1 : 0);

        row += dy;

        if (column is < 0 or > 3 || row is < 0 or > 7)
            return -1;

        return row * 4 + column;
    }

    /// <summary>
    /// The row a side's men are crowned on.
    /// </summary>
    public static int CrownRow(int side) => side == BoardGameServer.PlayerSide ? 0 : 7;

    private static int Forward(int side) => side == BoardGameServer.PlayerSide ? -1 : 1;

    public int Count(int side) => _spots.Count(piece => piece != 0 && SideOf(piece) == side);

    /// <summary>
    /// The side to move's moves: its jumps when it has any, else its steps.
    /// </summary>
    public List<CheckersMove> LegalMoves()
    {
        var moves = new List<CheckersMove>();
        var board = (sbyte[])_spots.Clone();

        for (var spot = 0; spot < Spots; spot++)
        {
            var piece = board[spot];

            if (piece != 0 && SideOf(piece) == SideToMove)
                FindJumps(board, spot, spot, piece, [], [], moves);
        }

        if (moves.Count > 0)
            return moves;

        for (var spot = 0; spot < Spots; spot++)
        {
            var piece = _spots[spot];

            if (piece == 0 || SideOf(piece) != SideToMove)
                continue;

            foreach (var dy in Directions(piece))
            {
                for (var dx = 0; dx <= 1; dx++)
                {
                    var to = Step(spot, dx, dy);

                    if (to >= 0 && _spots[to] == 0)
                        moves.Add(new CheckersMove(spot, [to], [], !IsKing(piece) && to / 4 == CrownRow(SideToMove)));
                }
            }
        }

        return moves;
    }

    private IEnumerable<int> Directions(sbyte piece)
    {
        if (IsKing(piece))
            return [-1, 1];

        return [Forward(SideOf(piece))];
    }

    /// <summary>
    /// Every chain of jumps from <paramref name="spot"/>, taking pieces off <paramref name="board"/> as it goes
    /// and putting them back after.
    /// </summary>
    private void FindJumps(sbyte[] board, int start, int spot, sbyte piece, List<int> path, List<int> jumped, List<CheckersMove> moves)
    {
        var found = false;

        foreach (var dy in Directions(piece))
        {
            for (var dx = 0; dx <= 1; dx++)
            {
                var over = Step(spot, dx, dy);

                if (over < 0 || board[over] == 0 || SideOf(board[over]) == SideOf(piece))
                    continue;

                var land = Step(over, dx, dy);

                if (land < 0 || board[land] != 0)
                    continue;

                found = true;

                var taken = board[over];

                board[over] = 0;
                board[spot] = 0;
                board[land] = piece;
                path.Add(land);
                jumped.Add(over);

                if (!IsKing(piece) && land / 4 == CrownRow(SideOf(piece)))
                    moves.Add(new CheckersMove(start, [.. path], [.. jumped], true));
                else
                    FindJumps(board, start, land, piece, path, jumped, moves);

                path.RemoveAt(path.Count - 1);
                jumped.RemoveAt(jumped.Count - 1);
                board[land] = 0;
                board[spot] = piece;
                board[over] = taken;
            }
        }

        if (!found && path.Count > 0)
            moves.Add(new CheckersMove(start, [.. path], [.. jumped], false));
    }

    /// <summary>
    /// Plays a legal move and passes the turn.
    /// </summary>
    public void Play(CheckersMove move)
    {
        var piece = _spots[move.Start];

        _spots[move.Start] = 0;

        foreach (var spot in move.Jumped)
            _spots[spot] = 0;

        _spots[move.End] = move.Crowns ? (sbyte)(piece * 2) : piece;

        QuietPlies = move.IsJump || !IsKing(piece) ? 0 : QuietPlies + 1;
        SideToMove = SideToMove == BoardGameServer.PlayerSide ? BoardGameServer.ComputerSide : BoardGameServer.PlayerSide;
    }
}
