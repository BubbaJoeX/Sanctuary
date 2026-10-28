using System;
using System.Collections.Generic;
using System.Text;

namespace Sanctuary.Game.Minigames.Board;

/// <summary>
/// The pieces, numbered as the movie's ChessNetworkEvent numbers them (PIECE_PAWN ... PIECE_KING), which is also
/// how a promotion names the piece it makes.
/// </summary>
public enum ChessPieceType : sbyte
{
    None = 0,
    Pawn = 1,
    Rook = 2,
    Knight = 3,
    Bishop = 4,
    Queen = 5,
    King = 6
}

public enum ChessMoveKind : byte
{
    Normal,
    DoublePush,
    EnPassant,
    Castle,
    Promotion
}

/// <summary>
/// A move, on the movie's network squares: 0 is a8, 7 is h8, 56 is a1 and 63 is h1 (row by row from black's
/// side). A castle is the king's move; the rook goes with it.
/// </summary>
public readonly record struct ChessMove(int From, int To, ChessMoveKind Kind = ChessMoveKind.Normal,
    ChessPieceType Promotion = ChessPieceType.None)
{
    /// <summary>
    /// The pawn an en passant capture takes: beside the capturing pawn, on the file it moves to.
    /// </summary>
    public int EnPassantCapture => (From & ~7) | (To & 7);
}

public enum ChessStatus
{
    Ongoing,
    Check,
    Checkmate,
    Stalemate,
    FiftyMoves,
    Repetition,
    InsufficientMaterial
}

/// <summary>
/// A chess position with the full rules: castling (not out of, through or into check), en passant, promotion,
/// and the draws (stalemate, fifty moves, threefold repetition, too little material to mate). White's pieces
/// are positive, black's negative. White is the player in the Flash game, so white moves up the board (toward
/// square 0).
/// </summary>
public sealed class ChessPosition
{
    public const int WhiteKingSide = 1;
    public const int WhiteQueenSide = 2;
    public const int BlackKingSide = 4;
    public const int BlackQueenSide = 8;

    private static readonly int[] KnightRows = [-2, -2, -1, -1, 1, 1, 2, 2];
    private static readonly int[] KnightColumns = [-1, 1, -2, 2, -2, 2, -1, 1];
    private static readonly int[] KingRows = [-1, -1, -1, 0, 0, 1, 1, 1];
    private static readonly int[] KingColumns = [-1, 0, 1, -1, 1, -1, 0, 1];
    private static readonly (int Row, int Column)[] Straight = [(-1, 0), (1, 0), (0, -1), (0, 1)];
    private static readonly (int Row, int Column)[] Diagonal = [(-1, -1), (-1, 1), (1, -1), (1, 1)];

    private static readonly ChessPieceType[] Promotions =
        [ChessPieceType.Queen, ChessPieceType.Rook, ChessPieceType.Bishop, ChessPieceType.Knight];

    private readonly sbyte[] _squares = new sbyte[64];

    /// <summary>
    /// How often each position has come up in the game, for the threefold repetition.
    /// </summary>
    private readonly Dictionary<string, int> _seen = [];

    public bool WhiteToMove { get; private set; } = true;

    /// <summary>
    /// The castling still allowed: <see cref="WhiteKingSide"/> and the like.
    /// </summary>
    public int Castling { get; private set; }

    /// <summary>
    /// The square a pawn just skipped with its double step, which an enemy pawn may take en passant; -1 if none.
    /// </summary>
    public int EnPassant { get; private set; } = -1;

    /// <summary>
    /// Moves (half moves) since a pawn moved or a piece was taken, for the fifty-move draw.
    /// </summary>
    public int HalfmoveClock { get; private set; }

    public sbyte this[int square] => _squares[square];

    public static ChessPosition CreateStart()
    {
        var position = new ChessPosition();

        ChessPieceType[] backRank =
        [
            ChessPieceType.Rook, ChessPieceType.Knight, ChessPieceType.Bishop, ChessPieceType.Queen,
            ChessPieceType.King, ChessPieceType.Bishop, ChessPieceType.Knight, ChessPieceType.Rook
        ];

        for (var column = 0; column < 8; column++)
        {
            position._squares[column] = (sbyte)-(sbyte)backRank[column];
            position._squares[8 + column] = -(sbyte)ChessPieceType.Pawn;
            position._squares[48 + column] = (sbyte)ChessPieceType.Pawn;
            position._squares[56 + column] = (sbyte)backRank[column];
        }

        position.Castling = WhiteKingSide | WhiteQueenSide | BlackKingSide | BlackQueenSide;
        position.Remember();

        return position;
    }

    /// <summary>
    /// A position from its pieces (white positive), for tests and puzzles: no castling unless given.
    /// </summary>
    public static ChessPosition FromPieces(IReadOnlyDictionary<int, sbyte> pieces, bool whiteToMove = true, int castling = 0,
        int enPassant = -1)
    {
        var position = new ChessPosition
        {
            WhiteToMove = whiteToMove,
            Castling = castling,
            EnPassant = enPassant
        };

        foreach (var (square, piece) in pieces)
            position._squares[square] = piece;

        position.Remember();

        return position;
    }

    public ChessPosition Clone()
    {
        var clone = new ChessPosition
        {
            WhiteToMove = WhiteToMove,
            Castling = Castling,
            EnPassant = EnPassant,
            HalfmoveClock = HalfmoveClock
        };

        Array.Copy(_squares, clone._squares, _squares.Length);

        foreach (var (key, count) in _seen)
            clone._seen[key] = count;

        return clone;
    }

    public static int Row(int square) => square >> 3;

    public static int Column(int square) => square & 7;

    public static ChessPieceType TypeOf(sbyte piece) => (ChessPieceType)Math.Abs(piece);

    #region Moves

    /// <summary>
    /// The moves the side to move may make.
    /// </summary>
    public List<ChessMove> LegalMoves()
    {
        var moves = new List<ChessMove>(48);
        GenerateMoves(moves, capturesOnly: false);

        var white = WhiteToMove;

        moves.RemoveAll(move =>
        {
            var undo = MakeMove(move);
            var illegal = IsInCheck(white);
            UnmakeMove(move, undo);

            return illegal;
        });

        return moves;
    }

    /// <summary>
    /// Every move the side to move's pieces can make, some of which may leave its own king in check (the search
    /// tries them and drops those). With <paramref name="capturesOnly"/>, only captures and promotions.
    /// </summary>
    public void GenerateMoves(List<ChessMove> moves, bool capturesOnly)
    {
        for (var square = 0; square < 64; square++)
        {
            var piece = _squares[square];

            if (piece == 0 || piece > 0 != WhiteToMove)
                continue;

            switch (TypeOf(piece))
            {
                case ChessPieceType.Pawn:
                    GeneratePawnMoves(moves, square, capturesOnly);
                    break;

                case ChessPieceType.Knight:
                    GenerateSteps(moves, square, KnightRows, KnightColumns, capturesOnly);
                    break;

                case ChessPieceType.Bishop:
                    GenerateSlides(moves, square, Diagonal, capturesOnly);
                    break;

                case ChessPieceType.Rook:
                    GenerateSlides(moves, square, Straight, capturesOnly);
                    break;

                case ChessPieceType.Queen:
                    GenerateSlides(moves, square, Straight, capturesOnly);
                    GenerateSlides(moves, square, Diagonal, capturesOnly);
                    break;

                case ChessPieceType.King:
                    GenerateSteps(moves, square, KingRows, KingColumns, capturesOnly);

                    if (!capturesOnly)
                        GenerateCastles(moves, square);
                    break;
            }
        }
    }

    private void GeneratePawnMoves(List<ChessMove> moves, int square, bool capturesOnly)
    {
        var white = WhiteToMove;
        var forward = white ? -1 : 1;
        var row = Row(square);
        var column = Column(square);
        var nextRow = row + forward;

        if (nextRow is < 0 or > 7)
            return;

        var lastRow = white ? 0 : 7;
        var one = nextRow * 8 + column;

        if (_squares[one] == 0)
        {
            if (nextRow == lastRow)
                AddPromotions(moves, square, one, capturesOnly);
            else if (!capturesOnly)
            {
                moves.Add(new ChessMove(square, one));

                var startRow = white ? 6 : 1;
                var two = one + forward * 8;

                if (row == startRow && _squares[two] == 0)
                    moves.Add(new ChessMove(square, two, ChessMoveKind.DoublePush));
            }
        }

        for (var side = -1; side <= 1; side += 2)
        {
            var targetColumn = column + side;

            if (targetColumn is < 0 or > 7)
                continue;

            var target = nextRow * 8 + targetColumn;
            var victim = _squares[target];

            if (victim != 0 && victim > 0 != white)
            {
                if (nextRow == lastRow)
                    AddPromotions(moves, square, target, capturesOnly);
                else
                    moves.Add(new ChessMove(square, target));
            }
            else if (target == EnPassant)
            {
                moves.Add(new ChessMove(square, target, ChessMoveKind.EnPassant));
            }
        }
    }

    private static void AddPromotions(List<ChessMove> moves, int from, int to, bool queenOnly)
    {
        foreach (var piece in Promotions)
        {
            moves.Add(new ChessMove(from, to, ChessMoveKind.Promotion, piece));

            if (queenOnly)
                return;
        }
    }

    private void GenerateSteps(List<ChessMove> moves, int square, int[] rows, int[] columns, bool capturesOnly)
    {
        var row = Row(square);
        var column = Column(square);

        for (var i = 0; i < rows.Length; i++)
        {
            var targetRow = row + rows[i];
            var targetColumn = column + columns[i];

            if (targetRow is < 0 or > 7 || targetColumn is < 0 or > 7)
                continue;

            var target = targetRow * 8 + targetColumn;
            var victim = _squares[target];

            if (victim == 0 ? !capturesOnly : victim > 0 != WhiteToMove)
                moves.Add(new ChessMove(square, target));
        }
    }

    private void GenerateSlides(List<ChessMove> moves, int square, (int Row, int Column)[] directions, bool capturesOnly)
    {
        var row = Row(square);
        var column = Column(square);

        foreach (var (rowStep, columnStep) in directions)
        {
            var targetRow = row + rowStep;
            var targetColumn = column + columnStep;

            while (targetRow is >= 0 and <= 7 && targetColumn is >= 0 and <= 7)
            {
                var target = targetRow * 8 + targetColumn;
                var victim = _squares[target];

                if (victim == 0)
                {
                    if (!capturesOnly)
                        moves.Add(new ChessMove(square, target));
                }
                else
                {
                    if (victim > 0 != WhiteToMove)
                        moves.Add(new ChessMove(square, target));

                    break;
                }

                targetRow += rowStep;
                targetColumn += columnStep;
            }
        }
    }

    private void GenerateCastles(List<ChessMove> moves, int square)
    {
        var white = WhiteToMove;
        var home = white ? 60 : 4;

        if (square != home)
            return;

        var kingSide = white ? WhiteKingSide : BlackKingSide;
        var queenSide = white ? WhiteQueenSide : BlackQueenSide;
        var rook = (sbyte)(white ? (sbyte)ChessPieceType.Rook : -(sbyte)ChessPieceType.Rook);

        if ((Castling & (kingSide | queenSide)) == 0 || IsAttacked(home, !white))
            return;

        if ((Castling & kingSide) != 0 && _squares[home + 3] == rook && _squares[home + 1] == 0 && _squares[home + 2] == 0
            && !IsAttacked(home + 1, !white) && !IsAttacked(home + 2, !white))
        {
            moves.Add(new ChessMove(home, home + 2, ChessMoveKind.Castle));
        }

        if ((Castling & queenSide) != 0 && _squares[home - 4] == rook && _squares[home - 1] == 0 && _squares[home - 2] == 0
            && _squares[home - 3] == 0 && !IsAttacked(home - 1, !white) && !IsAttacked(home - 2, !white))
        {
            moves.Add(new ChessMove(home, home - 2, ChessMoveKind.Castle));
        }
    }

    #endregion

    #region Attacks

    public bool IsInCheck(bool white)
    {
        var king = (sbyte)(white ? (sbyte)ChessPieceType.King : -(sbyte)ChessPieceType.King);

        for (var square = 0; square < 64; square++)
        {
            if (_squares[square] == king)
                return IsAttacked(square, !white);
        }

        return false;
    }

    /// <summary>
    /// Whether a piece of the given side attacks the square.
    /// </summary>
    public bool IsAttacked(int square, bool byWhite)
    {
        var row = Row(square);
        var column = Column(square);
        var sign = byWhite ? 1 : -1;

        // A white pawn attacks the squares diagonally above it, so it sits a row below the square it attacks.
        var pawnRow = row + (byWhite ? 1 : -1);

        if (pawnRow is >= 0 and <= 7)
        {
            for (var side = -1; side <= 1; side += 2)
            {
                var pawnColumn = column + side;

                if (pawnColumn is >= 0 and <= 7 && _squares[pawnRow * 8 + pawnColumn] == sign * (int)ChessPieceType.Pawn)
                    return true;
            }
        }

        if (AttackedByStep(row, column, KnightRows, KnightColumns, (sbyte)(sign * (int)ChessPieceType.Knight))
            || AttackedByStep(row, column, KingRows, KingColumns, (sbyte)(sign * (int)ChessPieceType.King)))
        {
            return true;
        }

        return AttackedBySlide(row, column, Straight, (sbyte)(sign * (int)ChessPieceType.Rook), (sbyte)(sign * (int)ChessPieceType.Queen))
            || AttackedBySlide(row, column, Diagonal, (sbyte)(sign * (int)ChessPieceType.Bishop), (sbyte)(sign * (int)ChessPieceType.Queen));
    }

    private bool AttackedByStep(int row, int column, int[] rows, int[] columns, sbyte attacker)
    {
        for (var i = 0; i < rows.Length; i++)
        {
            var targetRow = row + rows[i];
            var targetColumn = column + columns[i];

            if (targetRow is >= 0 and <= 7 && targetColumn is >= 0 and <= 7 && _squares[targetRow * 8 + targetColumn] == attacker)
                return true;
        }

        return false;
    }

    private bool AttackedBySlide(int row, int column, (int Row, int Column)[] directions, sbyte slider, sbyte queen)
    {
        foreach (var (rowStep, columnStep) in directions)
        {
            var targetRow = row + rowStep;
            var targetColumn = column + columnStep;

            while (targetRow is >= 0 and <= 7 && targetColumn is >= 0 and <= 7)
            {
                var piece = _squares[targetRow * 8 + targetColumn];

                if (piece != 0)
                {
                    if (piece == slider || piece == queen)
                        return true;

                    break;
                }

                targetRow += rowStep;
                targetColumn += columnStep;
            }
        }

        return false;
    }

    #endregion

    #region Making moves

    public readonly record struct Undo(sbyte Moved, sbyte Captured, int CapturedSquare, int Castling, int EnPassant, int HalfmoveClock);

    /// <summary>
    /// Plays a move in the game: it counts toward the repetition draw. The move must be legal.
    /// </summary>
    public void Play(ChessMove move)
    {
        MakeMove(move);
        Remember();
    }

    /// <summary>
    /// Plays a move for the search; <see cref="UnmakeMove"/> takes it back.
    /// </summary>
    public Undo MakeMove(ChessMove move)
    {
        var moved = _squares[move.From];
        var white = moved > 0;
        var capturedSquare = move.Kind == ChessMoveKind.EnPassant ? move.EnPassantCapture : move.To;
        var captured = _squares[capturedSquare];
        var undo = new Undo(moved, captured, capturedSquare, Castling, EnPassant, HalfmoveClock);

        _squares[capturedSquare] = 0;
        _squares[move.From] = 0;
        _squares[move.To] = move.Kind == ChessMoveKind.Promotion ? (sbyte)((white ? 1 : -1) * (int)move.Promotion) : moved;

        if (move.Kind == ChessMoveKind.Castle)
        {
            var (rookFrom, rookTo) = CastleRook(move.To);
            _squares[rookTo] = _squares[rookFrom];
            _squares[rookFrom] = 0;
        }

        if (TypeOf(moved) == ChessPieceType.King)
            Castling &= white ? ~(WhiteKingSide | WhiteQueenSide) : ~(BlackKingSide | BlackQueenSide);

        Castling &= ~(CornerRight(move.From) | CornerRight(move.To));

        EnPassant = move.Kind == ChessMoveKind.DoublePush ? (move.From + move.To) / 2 : -1;
        HalfmoveClock = TypeOf(moved) == ChessPieceType.Pawn || captured != 0 ? 0 : HalfmoveClock + 1;
        WhiteToMove = !WhiteToMove;

        return undo;
    }

    public void UnmakeMove(ChessMove move, Undo undo)
    {
        WhiteToMove = !WhiteToMove;

        if (move.Kind == ChessMoveKind.Castle)
        {
            var (rookFrom, rookTo) = CastleRook(move.To);
            _squares[rookFrom] = _squares[rookTo];
            _squares[rookTo] = 0;
        }

        _squares[move.To] = 0;
        _squares[move.From] = undo.Moved;
        _squares[undo.CapturedSquare] = undo.Captured;

        Castling = undo.Castling;
        EnPassant = undo.EnPassant;
        HalfmoveClock = undo.HalfmoveClock;
    }

    /// <summary>
    /// Where the rook comes from and goes to when the king castles to <paramref name="kingTo"/>; the movie's
    /// WaitingForNetwork.OnCastleMsg moves the same ones.
    /// </summary>
    public static (int From, int To) CastleRook(int kingTo)
    {
        return kingTo switch
        {
            62 => (63, 61),
            58 => (56, 59),
            6 => (7, 5),
            _ => (0, 3)
        };
    }

    /// <summary>
    /// The castling a rook's corner holds: lost once anything moves from or to it.
    /// </summary>
    private static int CornerRight(int square)
    {
        return square switch
        {
            63 => WhiteKingSide,
            56 => WhiteQueenSide,
            7 => BlackKingSide,
            0 => BlackQueenSide,
            _ => 0
        };
    }

    #endregion

    #region Status

    /// <summary>
    /// How things stand for the side to move.
    /// </summary>
    public ChessStatus GetStatus()
    {
        var inCheck = IsInCheck(WhiteToMove);

        if (LegalMoves().Count == 0)
            return inCheck ? ChessStatus.Checkmate : ChessStatus.Stalemate;

        if (HalfmoveClock >= 100)
            return ChessStatus.FiftyMoves;

        if (_seen.TryGetValue(Key(), out var count) && count >= 3)
            return ChessStatus.Repetition;

        if (IsInsufficientMaterial())
            return ChessStatus.InsufficientMaterial;

        return inCheck ? ChessStatus.Check : ChessStatus.Ongoing;
    }

    /// <summary>
    /// Kings with at most one bishop or knight between them: nobody can mate.
    /// </summary>
    public bool IsInsufficientMaterial()
    {
        var minors = 0;

        foreach (var piece in _squares)
        {
            switch (TypeOf(piece))
            {
                case ChessPieceType.Pawn:
                case ChessPieceType.Rook:
                case ChessPieceType.Queen:
                    return false;

                case ChessPieceType.Bishop:
                case ChessPieceType.Knight:
                    minors++;
                    break;
            }
        }

        return minors <= 1;
    }

    private void Remember()
    {
        var key = Key();
        _seen[key] = _seen.GetValueOrDefault(key) + 1;
    }

    private string Key()
    {
        var builder = new StringBuilder(72);

        foreach (var piece in _squares)
            builder.Append((char)('a' + piece + 6));

        builder.Append(WhiteToMove ? 'w' : 'b').Append(Castling).Append(':').Append(EnPassant);

        return builder.ToString();
    }

    #endregion
}
