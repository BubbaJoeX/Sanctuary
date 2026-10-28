using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sanctuary.Game.Minigames;
using Sanctuary.Game.Minigames.Board;
using Sanctuary.Game.Resources.Definitions;

namespace Sanctuary.Game.Tests;

[TestClass]
public sealed class BoardMinigameTests
{
    private const sbyte WhitePawn = (sbyte)ChessPieceType.Pawn;
    private const sbyte WhiteRook = (sbyte)ChessPieceType.Rook;
    private const sbyte WhiteKing = (sbyte)ChessPieceType.King;
    private const sbyte BlackPawn = -(sbyte)ChessPieceType.Pawn;
    private const sbyte BlackRook = -(sbyte)ChessPieceType.Rook;
    private const sbyte BlackKing = -(sbyte)ChessPieceType.King;

    /// <summary>
    /// A game run through its messages, on a clock of the test's own.
    /// </summary>
    private sealed class Session<T> where T : BoardGameServer, new()
    {
        public long Now;
        public readonly List<string> Sent = [];
        public bool? Result;
        public readonly T Game;

        public Session(MinigameDefinition definition, Action<T>? setup = null)
        {
            Game = FlashMinigame.CreateDetached<T>(definition, "", Sent.Add, won => Result = won, () => Now);
            setup?.Invoke(Game);
            Game.OnStart();
        }

        public void Message(string name, params string[] args) => Game.OnMessage(name, new FlashArgs(args));

        /// <summary>
        /// Lets the computer finish thinking and play its move.
        /// </summary>
        public void ComputerMoves()
        {
            Assert.IsNotNull(Game.Thinking, "The computer isn't thinking.");
            Assert.IsTrue(Game.Thinking.Wait(TimeSpan.FromSeconds(30)), "The computer took too long.");

            Now += 2000;
            Game.Tick();
        }

        /// <summary>
        /// Lets the final position's delay run out.
        /// </summary>
        public void Wait(long milliseconds)
        {
            Now += milliseconds;
            Game.Tick();
        }
    }

    private static MinigameDefinition Chess(int level = 1) => new() { Id = 20, Comment = "Chess", Game = "Chess", Level = level };

    private static MinigameDefinition Checkers(int level = 3) => new() { Id = 21, Comment = "Checkers", Game = "Checkers", Level = level };

    /// <summary>
    /// The movie's network square of a square's name: a8 is 0, h1 is 63.
    /// </summary>
    private static int Square(string name) => (8 - (name[1] - '0')) * 8 + (name[0] - 'a');

    private static string S(string name) => Square(name).ToString();

    private static string ChessEvent(params int[] args) => FlashMinigame.FormatMessage("OnChessEventMsg", args.Cast<object>().ToArray());

    private static string CheckersEvent(params int[] args) => FlashMinigame.FormatMessage("OnCheckersEventMsg", args.Cast<object>().ToArray());

    private static Session<ChessGame> StartChess(ChessPosition? position = null, int level = 1)
    {
        var session = new Session<ChessGame>(Chess(level), game => game.StartPosition = position);

        session.Message("OnConnectMsg");
        session.Message("OnStartGameMsg", "0");
        session.Sent.Clear();

        return session;
    }

    #region Chess

    [TestMethod]
    public void Chess_StartsWithThePlayerAsWhite()
    {
        var session = new Session<ChessGame>(Chess());

        session.Message("OnConnectMsg");
        CollectionAssert.AreEqual(new[] { "OnActiveMsg\tfalse", "OnServerReadyMsg" }, session.Sent);

        session.Sent.Clear();
        session.Message("OnStartGameMsg", "0");

        // INIT with equal arguments: the player is white (ChessMachine.GameStart), and moves first.
        CollectionAssert.AreEqual(new[] { ChessEvent(ChessGame.InitEvent, 0, 0), ChessEvent(ChessGame.TurnStartEvent, 0) }, session.Sent);
        Assert.IsTrue(session.Game.IsPlayersTurn);
    }

    [TestMethod]
    public void Chess_EchoesThePlayersMove_ThenTheComputerAnswers()
    {
        var session = StartChess();

        session.Message("OnMoveMsg", S("e2"), S("e4"));

        CollectionAssert.AreEqual(new[]
        {
            ChessEvent(ChessGame.MoveEvent, Square("e2"), Square("e4")),
            ChessEvent(ChessGame.TurnEndEvent, 0),
            ChessEvent(ChessGame.TurnStartEvent, 1)
        }, session.Sent);

        Assert.IsFalse(session.Game.IsPlayersTurn);

        // Not the player's turn: rejected, and nothing moves.
        session.Sent.Clear();
        session.Message("OnMoveMsg", S("d2"), S("d4"));
        CollectionAssert.AreEqual(new[] { ChessEvent(ChessGame.InvalidMoveEvent, Square("d2"), Square("d4")) }, session.Sent);
        Assert.AreEqual(WhitePawn, session.Game.Position[Square("d2")]);

        session.Sent.Clear();
        session.ComputerMoves();

        Assert.AreEqual(3, session.Sent.Count, string.Join(" | ", session.Sent));

        // Black's first move is a pawn's or a knight's: a plain move, on the board.
        var move = session.Sent[0].Split('\t');
        Assert.AreEqual("OnChessEventMsg", move[0]);
        Assert.AreEqual(ChessGame.MoveEvent.ToString(), move[1]);
        Assert.IsTrue(session.Game.Position[int.Parse(move[3])] < 0);
        Assert.AreEqual(0, session.Game.Position[int.Parse(move[2])]);

        Assert.AreEqual(ChessEvent(ChessGame.TurnEndEvent, 1), session.Sent[1]);
        Assert.AreEqual(ChessEvent(ChessGame.TurnStartEvent, 0), session.Sent[2]);
        Assert.IsTrue(session.Game.IsPlayersTurn);
    }

    [TestMethod]
    public void Chess_RejectsIllegalMoves_AndLetsThePlayerMoveAgain()
    {
        var session = StartChess();

        // Three squares up, castling through its own pieces, a promotion that isn't one.
        session.Message("OnMoveMsg", S("e2"), S("e5"));
        session.Message("OnCastleMsg", S("g1"));
        session.Message("OnPromoteMsg", S("e2"), S("e3"), "5");

        CollectionAssert.AreEqual(new[]
        {
            ChessEvent(ChessGame.InvalidMoveEvent, Square("e2"), Square("e5")),
            ChessEvent(ChessGame.TurnStartEvent, 0),
            ChessEvent(ChessGame.InvalidMoveEvent, -1, Square("g1")),
            ChessEvent(ChessGame.TurnStartEvent, 0),
            ChessEvent(ChessGame.InvalidMoveEvent, Square("e2"), Square("e3")),
            ChessEvent(ChessGame.TurnStartEvent, 0)
        }, session.Sent);

        Assert.AreEqual(WhitePawn, session.Game.Position[Square("e2")]);
        Assert.IsTrue(session.Game.IsPlayersTurn);
        Assert.IsNull(session.Game.Thinking);
    }

    [TestMethod]
    public void Chess_RejectsCastlingThroughCheck()
    {
        // A black rook watches f1.
        var position = ChessPosition.FromPieces(new Dictionary<int, sbyte>
        {
            [Square("e1")] = WhiteKing,
            [Square("h1")] = WhiteRook,
            [Square("e8")] = BlackKing,
            [Square("f8")] = BlackRook
        }, castling: ChessPosition.WhiteKingSide);

        var session = StartChess(position);
        session.Message("OnCastleMsg", S("g1"));

        Assert.AreEqual(ChessEvent(ChessGame.InvalidMoveEvent, -1, Square("g1")), session.Sent[0]);
        Assert.AreEqual(WhiteKing, session.Game.Position[Square("e1")]);
    }

    [TestMethod]
    public void Chess_SendsCastlingEnPassantAndPromotion_AsTheMovieShowsThem()
    {
        var castle = StartChess(ChessPosition.FromPieces(new Dictionary<int, sbyte>
        {
            [Square("e1")] = WhiteKing,
            [Square("h1")] = WhiteRook,
            [Square("a8")] = BlackKing,
            [Square("a7")] = BlackPawn
        }, castling: ChessPosition.WhiteKingSide));

        castle.Message("OnCastleMsg", S("g1"));
        Assert.AreEqual(ChessEvent(ChessGame.CastleEvent, Square("g1")), castle.Sent[0]);
        Assert.AreEqual(WhiteKing, castle.Game.Position[Square("g1")]);
        Assert.AreEqual(WhiteRook, castle.Game.Position[Square("f1")]);

        // Black's pawn just came d7-d5, past e5's.
        var enPassant = StartChess(ChessPosition.FromPieces(new Dictionary<int, sbyte>
        {
            [Square("e1")] = WhiteKing,
            [Square("e5")] = WhitePawn,
            [Square("a8")] = BlackKing,
            [Square("d5")] = BlackPawn,
            [Square("h7")] = BlackPawn
        }, enPassant: Square("d6")));

        enPassant.Message("OnEnPassantMsg", S("e5"), S("d6"), S("d5"));
        Assert.AreEqual(ChessEvent(ChessGame.EnPassantEvent, Square("e5"), Square("d6"), Square("d5")), enPassant.Sent[0]);
        Assert.AreEqual(0, enPassant.Game.Position[Square("d5")]);

        var promotion = StartChess(ChessPosition.FromPieces(new Dictionary<int, sbyte>
        {
            [Square("e1")] = WhiteKing,
            [Square("b7")] = WhitePawn,
            [Square("h8")] = BlackKing,
            [Square("h7")] = BlackPawn
        }));

        // The movie always makes a queen.
        promotion.Message("OnPromoteMsg", S("b7"), S("b8"), "5");
        Assert.AreEqual(ChessEvent(ChessGame.PromoteEvent, Square("b7"), Square("b8"), 5), promotion.Sent[0]);
        Assert.AreEqual((sbyte)ChessPieceType.Queen, promotion.Game.Position[Square("b8")]);
    }

    [TestMethod]
    public void Chess_PlayerMates_ShowsTheMate_ThenWins()
    {
        // A back-rank mate: Ra1-a8.
        var session = StartChess(ChessPosition.FromPieces(new Dictionary<int, sbyte>
        {
            [Square("g1")] = WhiteKing,
            [Square("a1")] = WhiteRook,
            [Square("g8")] = BlackKing,
            [Square("f7")] = BlackPawn,
            [Square("g7")] = BlackPawn,
            [Square("h7")] = BlackPawn
        }));

        session.Message("OnMoveMsg", S("a1"), S("a8"));

        CollectionAssert.AreEqual(new[]
        {
            ChessEvent(ChessGame.MoveEvent, Square("a1"), Square("a8")),
            ChessEvent(ChessGame.CheckmateEvent),
            ChessEvent(ChessGame.TurnEndEvent, 0)
        }, session.Sent);

        Assert.IsTrue(session.Game.IsDecided);
        Assert.IsNull(session.Result);

        // The movie hears the result once the mate has been up a while; it closes itself then.
        session.Sent.Clear();
        session.Wait(1000);
        Assert.AreEqual(0, session.Sent.Count);

        session.Wait(3000);
        CollectionAssert.AreEqual(new[] { "OnGameWonMsg" }, session.Sent);
        Assert.AreEqual(true, session.Result);
    }

    [TestMethod]
    public void Chess_ComputerMates_PlayerLoses()
    {
        // The player's back rank is weak and a2-a3 does nothing about it: Re8-e1 mates, even at the lowest skill.
        var session = StartChess(ChessPosition.FromPieces(new Dictionary<int, sbyte>
        {
            [Square("h1")] = WhiteKing,
            [Square("g2")] = WhitePawn,
            [Square("h2")] = WhitePawn,
            [Square("a2")] = WhitePawn,
            [Square("a8")] = BlackKing,
            [Square("e8")] = BlackRook
        }));

        session.Message("OnMoveMsg", S("a2"), S("a3"));
        session.Sent.Clear();
        session.ComputerMoves();

        CollectionAssert.AreEqual(new[]
        {
            ChessEvent(ChessGame.MoveEvent, Square("e8"), Square("e1")),
            ChessEvent(ChessGame.CheckmateEvent),
            ChessEvent(ChessGame.TurnEndEvent, 1)
        }, session.Sent);

        session.Wait(4000);
        Assert.AreEqual("OnGameLostMsg", session.Sent[^1]);
        Assert.AreEqual(false, session.Result);
    }

    [TestMethod]
    public void Chess_ClosingADecidedGame_StillPays()
    {
        var session = StartChess(ChessPosition.FromPieces(new Dictionary<int, sbyte>
        {
            [Square("g1")] = WhiteKing,
            [Square("a1")] = WhiteRook,
            [Square("g8")] = BlackKing,
            [Square("f7")] = BlackPawn,
            [Square("g7")] = BlackPawn,
            [Square("h7")] = BlackPawn
        }));

        session.Message("OnMoveMsg", S("a1"), S("a8"));
        session.Game.OnClose();

        Assert.AreEqual(true, session.Result);
    }

    [TestMethod]
    public void ChessPosition_GeneratesEveryLegalMove()
    {
        // Perft counts: the opening, and "Kiwipete", which has castling, en passant and promotions in reach.
        var start = ChessPosition.CreateStart();
        Assert.AreEqual(20, Perft(start, 1));
        Assert.AreEqual(400, Perft(start, 2));
        Assert.AreEqual(8902, Perft(start, 3));

        var kiwipete = FromFen("r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq -");
        Assert.AreEqual(48, Perft(kiwipete, 1));
        Assert.AreEqual(2039, Perft(kiwipete, 2));
        Assert.AreEqual(97862, Perft(kiwipete, 3));

        // En passant and promotions (CPW position 3 and 4).
        Assert.AreEqual(2812, Perft(FromFen("8/2p5/3p4/KP5r/1R3p1k/8/4P1P1/8 w - -"), 3));
        Assert.AreEqual(9467, Perft(FromFen("r3k2r/Pppp1ppp/1b3nbN/nP6/BBP1P3/q4N2/Pp1P2PP/R2Q1RK1 w kq -"), 3));
    }

    [TestMethod]
    public void ChessPosition_KnowsTheDraws()
    {
        var bare = ChessPosition.FromPieces(new Dictionary<int, sbyte>
        {
            [Square("e1")] = WhiteKing,
            [Square("e8")] = BlackKing,
            [Square("c3")] = (sbyte)ChessPieceType.Knight
        });

        Assert.AreEqual(ChessStatus.InsufficientMaterial, bare.GetStatus());

        // Black to move, not in check, and nowhere to go.
        var stalemate = ChessPosition.FromPieces(new Dictionary<int, sbyte>
        {
            [Square("c7")] = (sbyte)ChessPieceType.Queen,
            [Square("e1")] = WhiteKing,
            [Square("a8")] = BlackKing
        }, whiteToMove: false);

        Assert.AreEqual(ChessStatus.Stalemate, stalemate.GetStatus());

        // Knights out and back twice: the opening comes up a third time.
        var repetition = ChessPosition.CreateStart();
        string[] shuffle = ["g1", "f3", "g8", "f6", "f3", "g1", "f6", "g8"];

        for (var round = 0; round < 2; round++)
        {
            for (var i = 0; i < shuffle.Length; i += 2)
                repetition.Play(repetition.LegalMoves().Single(move => move.From == Square(shuffle[i]) && move.To == Square(shuffle[i + 1])));
        }

        Assert.AreEqual(ChessStatus.Repetition, repetition.GetStatus());
    }

    private static long Perft(ChessPosition position, int depth)
    {
        var moves = position.LegalMoves();

        if (depth == 1)
            return moves.Count;

        long nodes = 0;

        foreach (var move in moves)
        {
            var undo = position.MakeMove(move);
            nodes += Perft(position, depth - 1);
            position.UnmakeMove(move, undo);
        }

        return nodes;
    }

    private static ChessPosition FromFen(string fen)
    {
        var parts = fen.Split(' ');
        var pieces = new Dictionary<int, sbyte>();
        var square = 0;

        foreach (var c in parts[0])
        {
            if (c == '/')
                continue;

            if (char.IsDigit(c))
            {
                square += c - '0';
                continue;
            }

            var type = char.ToLowerInvariant(c) switch
            {
                'p' => ChessPieceType.Pawn,
                'r' => ChessPieceType.Rook,
                'n' => ChessPieceType.Knight,
                'b' => ChessPieceType.Bishop,
                'q' => ChessPieceType.Queen,
                _ => ChessPieceType.King
            };

            pieces[square++] = (sbyte)(char.IsUpper(c) ? (sbyte)type : -(sbyte)type);
        }

        var castling = 0;

        foreach (var c in parts[2])
        {
            castling |= c switch
            {
                'K' => ChessPosition.WhiteKingSide,
                'Q' => ChessPosition.WhiteQueenSide,
                'k' => ChessPosition.BlackKingSide,
                'q' => ChessPosition.BlackQueenSide,
                _ => 0
            };
        }

        return ChessPosition.FromPieces(pieces, parts[1] == "w", castling, parts[3] == "-" ? -1 : Square(parts[3]));
    }

    #endregion

    #region Checkers

    private static Session<CheckersGame> StartCheckers(CheckersPosition? position = null, int level = 3)
    {
        var session = new Session<CheckersGame>(Checkers(level), game => game.StartPosition = position);

        session.Message("OnConnectMsg");
        session.Message("OnStartGameMsg", "0");
        session.Message("OnReadyMsg");
        session.Sent.Clear();

        return session;
    }

    [TestMethod]
    public void Checkers_SetsUpTheBoard_WithThePlayerMovingFirst()
    {
        var session = new Session<CheckersGame>(Checkers());

        session.Message("OnConnectMsg");
        CollectionAssert.AreEqual(new[] { "OnActiveMsg\tfalse", "OnServerReadyMsg" }, session.Sent);

        session.Sent.Clear();
        session.Message("OnStartGameMsg", "0");
        CollectionAssert.AreEqual(new[] { "OnStartGameMsg", CheckersEvent(CheckersGame.BoardEvent, 1) }, session.Sent);

        // The movie's OnReadyMsg: the player is 0, red (the computer) is 1, and no VS_AI, which would have the
        // movie play the computer itself.
        session.Sent.Clear();
        session.Message("OnReadyMsg");
        CollectionAssert.AreEqual(new[] { CheckersEvent(CheckersGame.InitEvent, 0, 1), CheckersEvent(CheckersGame.TurnStartEvent, 0) }, session.Sent);
    }

    [TestMethod]
    public void Checkers_EchoesThePlayersMove_ThenTheComputerAnswers()
    {
        var session = StartCheckers();

        session.Message("OnMoveMsg", "21", "17");

        CollectionAssert.AreEqual(new[]
        {
            CheckersEvent(CheckersGame.MoveEvent, 21, 17),
            CheckersEvent(CheckersGame.TurnEndEvent, 0),
            CheckersEvent(CheckersGame.TurnStartEvent, 1)
        }, session.Sent);

        session.Sent.Clear();
        session.ComputerMoves();

        // One of the computer's men steps down a row.
        var move = session.Sent[0].Split('\t');
        Assert.AreEqual(CheckersGame.MoveEvent.ToString(), move[1]);
        Assert.AreEqual(CheckersPosition.ComputerMan, session.Game.Position[int.Parse(move[3])]);
        Assert.AreEqual(int.Parse(move[2]) / 4 + 1, int.Parse(move[3]) / 4);

        Assert.AreEqual(CheckersEvent(CheckersGame.TurnEndEvent, 1), session.Sent[^2]);
        Assert.AreEqual(CheckersEvent(CheckersGame.TurnStartEvent, 0), session.Sent[^1]);
        Assert.IsTrue(session.Game.IsPlayersTurn);
    }

    [TestMethod]
    public void Checkers_RejectsIllegalMoves_AndForcesTheJump()
    {
        var session = StartCheckers(CheckersPosition.FromPieces(new Dictionary<int, sbyte>
        {
            [21] = CheckersPosition.PlayerMan,
            [30] = CheckersPosition.PlayerMan,
            [17] = CheckersPosition.ComputerMan,
            [0] = CheckersPosition.ComputerMan
        }));

        // Two rows at once; a step backwards; a step while a jump is there to take.
        session.Message("OnMoveMsg", "30", "22,13");
        session.Message("OnMoveMsg", "21", "25");
        session.Message("OnMoveMsg", "21", "16");

        CollectionAssert.AreEqual(new[]
        {
            CheckersEvent(CheckersGame.MoveInvalidEvent, 30),
            CheckersEvent(CheckersGame.TurnStartEvent, 0),
            CheckersEvent(CheckersGame.MoveInvalidEvent, 21),
            CheckersEvent(CheckersGame.TurnStartEvent, 0),
            CheckersEvent(CheckersGame.MoveInvalidEvent, 21),
            CheckersEvent(CheckersGame.TurnStartEvent, 0)
        }, session.Sent);

        session.Sent.Clear();
        session.Message("OnMoveMsg", "21", "14");

        CollectionAssert.AreEqual(new[]
        {
            CheckersEvent(CheckersGame.MoveEvent, 21, 14),
            CheckersEvent(CheckersGame.MoveJumpedEvent, 17),
            CheckersEvent(CheckersGame.TurnEndEvent, 0),
            CheckersEvent(CheckersGame.TurnStartEvent, 1)
        }, session.Sent);

        Assert.AreEqual(0, session.Game.Position[17]);
    }

    [TestMethod]
    public void Checkers_ChainsOfJumps_GoToTheirEnd()
    {
        var session = StartCheckers(CheckersPosition.FromPieces(new Dictionary<int, sbyte>
        {
            [25] = CheckersPosition.PlayerMan,
            [21] = CheckersPosition.ComputerMan,
            [13] = CheckersPosition.ComputerMan,
            [3] = CheckersPosition.ComputerMan
        }));

        // Stopping halfway is not allowed.
        session.Message("OnMoveMsg", "25", "16");
        Assert.AreEqual(CheckersEvent(CheckersGame.MoveInvalidEvent, 25), session.Sent[0]);

        session.Sent.Clear();
        session.Message("OnMoveMsg", "25", "16,9");

        CollectionAssert.AreEqual(new[]
        {
            CheckersEvent(CheckersGame.MoveEvent, 25, 16),
            CheckersEvent(CheckersGame.MoveJumpedEvent, 21),
            CheckersEvent(CheckersGame.MoveEvent, 16, 9),
            CheckersEvent(CheckersGame.MoveJumpedEvent, 13),
            CheckersEvent(CheckersGame.TurnEndEvent, 0),
            CheckersEvent(CheckersGame.TurnStartEvent, 1)
        }, session.Sent);
    }

    [TestMethod]
    public void Checkers_ManReachingTheFarRow_IsCrowned()
    {
        var session = StartCheckers(CheckersPosition.FromPieces(new Dictionary<int, sbyte>
        {
            [5] = CheckersPosition.PlayerMan,
            [27] = CheckersPosition.ComputerMan
        }));

        session.Message("OnMoveMsg", "5", "1");

        CollectionAssert.AreEqual(new[]
        {
            CheckersEvent(CheckersGame.MoveEvent, 5, 1),
            CheckersEvent(CheckersGame.MoveKingEvent, 1),
            CheckersEvent(CheckersGame.TurnEndEvent, 0),
            CheckersEvent(CheckersGame.TurnStartEvent, 1)
        }, session.Sent);

        Assert.AreEqual(CheckersPosition.PlayerKing, session.Game.Position[1]);
    }

    [TestMethod]
    public void Checkers_TakingTheLastPiece_Wins()
    {
        var session = StartCheckers(CheckersPosition.FromPieces(new Dictionary<int, sbyte>
        {
            [21] = CheckersPosition.PlayerMan,
            [17] = CheckersPosition.ComputerMan
        }));

        session.Message("OnMoveMsg", "21", "14");

        // No turn start for a side that can't move.
        Assert.AreEqual(CheckersEvent(CheckersGame.TurnEndEvent, 0), session.Sent[^1]);
        Assert.IsTrue(session.Game.IsDecided);

        session.Wait(4000);
        Assert.AreEqual("OnGameWonMsg", session.Sent[^1]);
        Assert.AreEqual(true, session.Result);
    }

    [TestMethod]
    public void Checkers_LosingTheLastPiece_Loses()
    {
        // 22-17 walks into the computer's only move, a jump that takes the player's last man.
        var session = StartCheckers(CheckersPosition.FromPieces(new Dictionary<int, sbyte>
        {
            [22] = CheckersPosition.PlayerMan,
            [13] = CheckersPosition.ComputerMan
        }));

        session.Message("OnMoveMsg", "22", "17");
        session.Sent.Clear();
        session.ComputerMoves();

        CollectionAssert.AreEqual(new[]
        {
            CheckersEvent(CheckersGame.MoveEvent, 13, 22),
            CheckersEvent(CheckersGame.MoveJumpedEvent, 17),
            CheckersEvent(CheckersGame.TurnEndEvent, 1)
        }, session.Sent);

        session.Wait(4000);
        Assert.AreEqual("OnGameLostMsg", session.Sent[^1]);
        Assert.AreEqual(false, session.Result);
    }

    [TestMethod]
    public void CheckersPosition_StepsLikeTheMovie()
    {
        // BoardProxy.GetSpotFromOffset: rows alternate, so the same (dx, dy) twice is a straight jump.
        Assert.AreEqual(16, CheckersPosition.Step(21, 0, -1));
        Assert.AreEqual(17, CheckersPosition.Step(21, 1, -1));
        Assert.AreEqual(5, CheckersPosition.Step(0, 1, 1));
        Assert.AreEqual(9, CheckersPosition.Step(5, 1, 1));
        Assert.AreEqual(-1, CheckersPosition.Step(3, 1, 1));
        Assert.AreEqual(-1, CheckersPosition.Step(28, 0, 1));

        Assert.AreEqual(7, CheckersPosition.CreateStart().LegalMoves().Count);
    }

    #endregion
}
