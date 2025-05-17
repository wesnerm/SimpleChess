using System.Collections.Concurrent;

namespace SimpleChess.Chess;

[TestFixture]
public class ChessTest 
{
    private readonly string initFen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

    [Test]
    [Explicit]
    public void LegalMoves()
    {
        var mated = new ChessBoard("r1b1R1k1/ppp2p1p/1b3p1B/3q4/3p4/5N2/PPP2KPP/R7 b - - 0 1");
        Assert.AreEqual(0, CountMoves(mated, 4));

        var board = ChessBoard.NewBoard();
        Assert.AreEqual(1, CountMoves(board, 0));
        Assert.AreEqual(20, CountMoves(board, 1));
        Assert.AreEqual(400, CountMoves(board, 2));
        Assert.AreEqual(8902, CountMoves(board, 3));
        Assert.AreEqual(197281, CountMoves(board, 4)); // 4.83 secs
        //Assert.AreEqual(4865609, CountMoves(board, 5));  // 14 secs
        //Assert.AreEqual(119060324, CountMoves(board, 6)); // 10 minutes
        //Assert.AreEqual(3195901860, CountMoves(board, 7));
        //8  84998978956
        //9  2439530234167
        //10 69352859712417
    }

    [Test]
    [Explicit]
    public void LegalMoves2()
    {
        var mated = new ChessBoard("r1b1R1k1/ppp2p1p/1b3p1B/3q4/3p4/5N2/PPP2KPP/R7 b - - 0 1");
        Assert.AreEqual(0, CountMoves(mated, 4));

        var board = ChessBoard.NewBoard();
        set2.Clear();
        Assert.AreEqual(1, CountMoves2(board, 0));

        set2.Clear();
        Assert.AreEqual(20, CountMoves2(board, 1));

        set2.Clear();
        Assert.AreEqual(400, CountMoves2(board, 2));

        set2.Clear();
        Assert.AreEqual(5362, CountMoves2(board, 3));

        set2.Clear();
        Assert.AreEqual(72078, CountMoves2(board, 4)); //71852 w ep

        set2.Clear();
        Assert.AreEqual(822518, CountMoves2(board, 5)); //809896 hmm

        set2.Clear();
        Assert.AreEqual(9344875, CountMoves2(board, 6)); // 14.54 seconds --> 2.7 seconds

        set2.Clear();
        //Assert.AreEqual(3195901860, CountMoves2(board, 7));
    }

    private int CountMoves(ChessBoard board, int depth)
    {
        if (depth == 0)
            return 1;

        var count = 0;
        foreach (var m in board.LegalMoves())
        {
            var b = board;
            b.Move(m);
            count += CountMoves(b, depth - 1);
        }
        return count;
    }

    private readonly HashSet<ChessBoard> set = new HashSet<ChessBoard>();
    private readonly ConcurrentDictionary<ChessBoard, object> set2 = new ConcurrentDictionary<ChessBoard, object>();

    private int CountMoves2(ChessBoard board, int depth)
    {
        if (depth == 0)
            return 1;

        set2.Clear();
        depth--;

        var count = board.LegalMoves().Sum(m =>
        {
            var b = board;
            b.Move(m);
            return CountMoves2Slow(b, depth);
        });

        Assert.AreEqual(set.Count, count);
        set.Clear();
        return count;
    }

    private int CountMoves2Slow(ChessBoard board, int depth)
    {
        if (depth <= 0)
        {
            do
            {
                if (set2.ContainsKey(board))
                    return 0;
            } while (!set2.TryAdd(board, null));
            return 1;
        }

        depth--;
        var count = 0;
        foreach (var m in board.LegalMoves())
        {
            var b = board;
            b.Move(m);
            count += CountMoves2Slow(b, depth);
        }
        return count;
    }

    private ChessGame CheckGame(string game, string fen)
    {
        var game1 = new ChessGame(game);

        var finalBoard = game1.FinalBoard;
        finalBoard.DrawHalfMoves = 0;
        finalBoard.MoveCount = 1;

        Assert.AreEqual(fen, finalBoard.Fen);
        if (!string.IsNullOrWhiteSpace(game1["FEN"]))
            Assert.AreEqual(game1.Fen, game1.InitialBoard.Fen);
        return game1;
    }

    [Test]
    [Explicit]
    public void Analyze()
    {
        var g4 = @"[FEN ""4N3/p5R1/1p3k2/2p2P2/2PpK3/1P6/1r6/8 b - - 0 1""]";
        CheckScore(g4, -ChessAnalyzer.MateValue, 0);

        var g3 = @"[FEN ""8/p3P1R1/1p3k2/2p2P2/2PpK3/1P6/1r6/8 w - - 0 1""]
1.e8=N#";
        CheckScore(g3, ChessAnalyzer.MateValue - 1, 1);

        var g2 = @"[FEN ""N1k5/p6R/1p4p1/6p1/1n2r1P1/5N2/PPPR2P1/2K5 w - - 0 1""]
1.Rc7+ Kb8 2.Rd8#";

        CheckScore(g2, ChessAnalyzer.MateValue - 3, 3);

        var g1 = @"[FEN ""rn2r1k1/pp1b1pp1/1bp3q1/3p1NBQ/1P6/P1P5/5PPP/4RRK1 w - - 0 1""]
1.Rxe8+ Bxe8 2.Ne7+ Kf8 3.Qh8# *";

        CheckScore(g1, ChessAnalyzer.MateValue - 5, 5);
    }

    private void CheckScore(string g, float score, int depth)
    {
        var game = new ChessGame(g);

        var analyzer = new ChessAnalyzer();
        var board = game.InitialBoard;
        var result = analyzer.FindBestMove(board, depth);
        Assert.AreEqual(score, result.Score);

        if (game.PlyCount > 0)
        {
            var m = board.GetMoves(game[0]).SingleOrDefault();
            Assert.AreEqual(m, result.Move);
        }
    }

    [Test]
    public void Castling()
    {
        // Queenside castling
        var g1 = @"1.e4 e5 2.Nf3 d6 3.c3 f5 4.Bc4 Nf6 5.d4 fxe4 6.dxe5 exf3
7.exf6 Qxf6 8.gxf3 Nc6 9.f4 Bd7 10.Be3 O-O-O 11.Nd2 Re8 12.Qf3
Bf5 13.O-O-O d5 14.Bxd5 Qxc3+ 15.bxc3 Ba3# 0-1";
        var f1 = "2k1r2r/ppp3pp/2n5/3B1b2/5P2/b1P1BQ2/P2N1P1P/2KR3R w - - 0 1";
        CheckGame(g1, f1);
    }

    [Test]
    public void ChessGameTest()
    {
        var t1 =
            @"[Event ""EST-ch""]
[Site ""Tallinn""]
[Date ""2003.05.19""]
[EventDate ""2003.05.17""]
[Round ""3.1""]
[Result ""1-0""]
[White ""Meelis Kanep""]
[Black ""Margus Soot""]
[ECO ""D35""]
[WhiteElo ""2446""]
[BlackElo ""2072""]
[PlyCount ""46""]

";
        var g1 =
            @"1. d4 e6 2. c4 Nf6 3. Nc3 d5 4. cxd5 Nxd5 5. e4 Nxc3 6. bxc3
c5 7. Nf3 cxd4 8. cxd4 Bb4+ 9. Bd2 Bxd2+ 10. Qxd2 O-O 11. Bc4
Nc6 12. O-O b6 13. Rad1 Bb7 14. Rfe1 Rc8 15. d5 Na5 16. Bd3
exd5 17. e5 d4 18. Nxd4 Kh8 19. Qf4 Rc3 20. Re3 Qd5 21. Rg3
Rfc8 22. Qg5 Rg8 23. Rh3 Rxd3 {Black resigned on the spot, not
waiting for 24.Rxh7+} 1-0";
        var f1 = "6rk/pb3ppp/1p6/n2qP1Q1/3N4/3r3R/P4PPP/3R2K1 w - - 0 1";

        CheckGame(t1 + g1, f1);

        var game = new ChessGame(t1 + g1);
        Assert.AreEqual(initFen, game.InitialBoard.Fen);
        Assert.AreEqual(47, game.PlyCount);
        Assert.AreEqual(game.White, "Meelis Kanep");
        Assert.AreEqual(game.Black, "Margus Soot");
        Assert.AreEqual(game.Date, "2003.05.19");
        Assert.AreEqual(game.Result, "1-0");
        Assert.AreEqual(game.Site, "Tallinn");
        Assert.AreEqual(game.Event, "EST-ch");
        Assert.AreEqual("", game["Misc"]);

        game["Misc"] = "X";
        Assert.AreEqual("X", game["Misc"]);

        var game2 = new ChessGame();
        game2.Pgn = g1;
        Assert.AreEqual(initFen, game2.InitialBoard.Fen);
        var fb = game2.FinalBoard;
        fb.MoveCount = 1;
        Assert.AreEqual(f1, fb.Fen);
        Assert.AreEqual(47, game2.PlyCount);

        var game3 = new ChessGame();
        game3.Clear(f1);
        Assert.AreEqual(f1, game3.Fen);
        Assert.AreEqual(f1, game3["FEN"]);
    }

    [Test]
    public void EmptyChessBoard()
    {
        var initial = ChessBoard.NewBoard();
        for (var i = 3; i <= 6; i++)
            for (var c = 1; c <= 8; c++)
                Assert.IsTrue(initial[c, i] == 0);

        Assert.IsTrue(initial[0, 0] == ChessBoard.OutsideBoard);
        Assert.IsTrue(initial[-1, -1] == ChessBoard.OutsideBoard);

        for (var c = 1; c <= 8; c++)
        {
            Assert.IsTrue(initial[c, 2] == ChessPiece.Pawn);
            Assert.IsTrue(initial[c, 7] == (ChessPiece.Pawn | ChessPiece.Black));
            Assert.IsTrue(initial[c, 8] == (initial[c, 1] ^ ChessPiece.Black));
        }

        Assert.IsTrue(initial[1, 1] == ChessPiece.Rook);
        Assert.IsTrue(initial[8, 1] == ChessPiece.Rook);
        Assert.IsTrue(initial[2, 1] == ChessPiece.Knight);
        Assert.IsTrue(initial[7, 1] == ChessPiece.Knight);
        Assert.IsTrue(initial[3, 1] == ChessPiece.Bishop);
        Assert.IsTrue(initial[6, 1] == ChessPiece.Bishop);
        Assert.IsTrue(initial[4, 1] == ChessPiece.Queen);
        Assert.IsTrue(initial[5, 1] == ChessPiece.King);
        Assert.IsTrue(initial.MoveCount == 1);
        Assert.IsTrue(initial.DrawHalfMoves == 0);
        Assert.IsTrue(initial.WhiteToPlay);
    }

    [Test]
    public void EnPassant()
    {
        var g1 = @"1.d4 Nf6 2.c4 g6 3.Nc3 Bg7 4.e4 d6 5.f3 O-O 6.Be3 Nc6 7.Nge2
a6 8.Qd2 Rb8 9.h4 b5 10.h5 e5 11.d5 Na5 12.Ng3 bxc4 13.O-O-O
Nd7 14.hxg6 fxg6 15.Nb1 Rb5 16.b4 cxb3 17.Bxb5 c5 18.dxc6 axb5
19.Qd5+ Rf7 20.axb3 Nf8 21.Qxd6 Qe8 22.Qd8 Qxc6+ 23.Kb2 Qa8
24.Rc1 Nc4+ 25.bxc4 Rd7 26.Qe8 bxc4 27.Nc3 Qc6 28.Kc2 Rd2+
1/2-1/2";
        var f1 = "2b1Qnk1/6bp/2q3p1/4p3/2p1P3/2N1BPN1/2Kr2P1/2R4R w - - 0 1";
        CheckGame(g1, f1);
    }

    [Test]
    public void Fen()
    {
        var blank = new ChessBoard();
        Assert.AreEqual("8/8/8/8/8/8/8/8 w - - 0 1", blank.Fen);

        var empty = ChessBoard.NewBoard();
        Assert.AreEqual(empty.Fen, initFen);
        Assert.AreEqual(empty, new ChessBoard(initFen));

        var game = empty;
        Assert.AreEqual(game, empty);
        Assert.IsTrue(game.WhiteToPlay);

        var p = game.Move("e2", "e4");
        Assert.AreEqual(p, ChessPiece.Space);
        Assert.AreEqual(game["e4"], ChessPiece.Pawn);
        Assert.AreEqual(game.Fen, "rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq - 0 1");
        Assert.IsFalse(game.WhiteToPlay);

        p = game.Move("c7", "c5");
        Assert.AreEqual(p, ChessPiece.Space);
        Assert.AreEqual(game["c5"], ChessPiece.Pawn | ChessPiece.Black);
        Assert.AreEqual(game.Fen, "rnbqkbnr/pp1ppppp/8/2p5/4P3/8/PPPP1PPP/RNBQKBNR w KQkq - 0 2");
        Assert.IsTrue(game.WhiteToPlay);

        p = game.Move("g1", "f3");
        Assert.AreEqual(p, ChessPiece.Space);
        Assert.AreEqual(game["f3"], ChessPiece.Knight);
        Assert.AreEqual(game.Fen, "rnbqkbnr/pp1ppppp/8/2p5/4P3/5N2/PPPP1PPP/RNBQKB1R b KQkq - 1 2");
        Assert.AreEqual(0, game.Score);

        Assert.AreNotEqual(game, empty);
        Assert.AreNotEqual(game.GetHashCode(), empty.GetHashCode());

        game.Fen = initFen;
        Assert.AreEqual(game, empty);
        Assert.AreEqual(game.GetHashCode(), empty.GetHashCode());
        Assert.AreEqual(game.Fen, game.ToString());
    }

    [Test]
    public void FindPiece()
    {
        int rank, file;
        var game = ChessBoard.NewBoard();
        Assert.IsTrue(game.FindKing(true, out file, out rank));
        Assert.AreEqual(rank, 1);
        Assert.AreEqual(file, 5);

        Assert.IsTrue(game.FindKing(false, out file, out rank));
        Assert.AreEqual(rank, 8);
        Assert.AreEqual(file, 5);
    }

    [Test]
    public void InCheck()
    {
        var game = ChessBoard.NewBoard();
        Assert.IsFalse(game.InCheck);
        game.Move("f2", "f4");
        Assert.IsFalse(game.InCheck);
        game.Move("e7", "e5");
        Assert.IsFalse(game.InCheck);

        var game2 = game;
        game2.Move("e2", "e3");
        Assert.IsFalse(game2.InCheck);
        Assert.IsFalse(game2.Checkmated);
        game2.Move("d8", "h4");
        Assert.IsTrue(game2.InCheck);
        Assert.IsFalse(game2.Checkmated);

        // Fool's mate
        game.Move("g2", "g3");
        game.Move("e5", "f4");
        game.Move("g3", "f4");
        game.Move("d8", "h4");
        Assert.IsTrue(game.InCheck);
        Assert.IsTrue(game.Checkmated);
    }

    [Test]
    public void IsAttacked()
    {
        var game = ChessBoard.NewBoard();
        for (var rank = 1; rank < 5; rank++)
        {
            for (var file = 1; file < 9; file++)
            {
                Assert.IsFalse(game.IsAttacked(file, 1, true));
                Assert.IsFalse(game.IsAttacked(file, 9 - rank, false));
            }
        }

        for (var c = 1; c < 9; c++)
        {
            Assert.IsTrue(game.IsAttacked(c, 2, false));
            Assert.IsTrue(game.IsAttacked(c, 7, true));
            Assert.IsTrue(game.IsAttacked(c, 3, false));
            Assert.IsTrue(game.IsAttacked(c, 6, true));
        }

        for (var c = 2; c < 8; c++)
        {
            Assert.IsTrue(game.IsAttacked(c, 1, false));
            Assert.IsTrue(game.IsAttacked(c, 8, true));
        }

        Assert.IsFalse(game.IsAttacked(1, 1, false));
        Assert.IsFalse(game.IsAttacked(8, 8, true));
    }

    [Test]
    public void Move2()
    {
        var game = ChessBoard.NewBoard();
        game.Move("e4");
        Assert.AreEqual(game.Fen, "rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq - 0 1");
        game.Move("c5");
        Assert.AreEqual(game.Fen, "rnbqkbnr/pp1ppppp/8/2p5/4P3/8/PPPP1PPP/RNBQKBNR w KQkq - 0 2");
        game.Move("Nf3");
        Assert.AreEqual(game.Fen, "rnbqkbnr/pp1ppppp/8/2p5/4P3/5N2/PPPP1PPP/RNBQKB1R b KQkq - 1 2");
    }

    [Test]
    public void PartialGame()
    {
        var g1 = @"[FEN ""r1b2rk1/ppp2ppp/1b3N2/3q4/3p4/Q4N2/PPP2KPP/R1B1R3 b - - 0 12""]

1. ... gxf6 2. Qxf8+ Kxf8 3. Bh6+ Kg8 4. Re8#";

        var f1 = "r1b1R1k1/ppp2p1p/1b3p1B/3q4/3p4/5N2/PPP2KPP/R7 b - - 0 1";
        var f1Init = "r1b2rk1/ppp2ppp/1b3N2/3q4/3p4/Q4N2/PPP2KPP/R1B1R3 b - - 0 12";

        CheckGame(g1, f1);

        var game1 = new ChessGame(g1);
        Assert.AreEqual(game1.Fen, game1.InitialBoard.Fen);
        Assert.AreEqual(f1Init, game1.Fen);
        Assert.AreEqual(f1Init, game1["FEN"]);
    }

    [Test]
    public void PawnPromotion()
    {
        // Queen promo with capture
        var g5 = @"1.e4 d5 2.exd5 Nf6 3.Bb5+ c6 4.dxc6 Qb6 5.cxb7+ Qxb5 6.bxc8=Q# 
1-0";

        var f5 = "rnQ1kb1r/p3pppp/5n2/1q6/8/8/PPPP1PPP/RNBQK1NR b KQkq - 0 1";
        CheckGame(g5, f5);

        // Queen promo with capture -- missing =Q
        var g5_2 = @"1.e4 d5 2.exd5 Nf6 3.Bb5+ c6 4.dxc6 Qb6 5.cxb7+ Qxb5 6.bxc8# 
1-0";
        CheckGame(g5_2, f5);

        // Queen promo without capture
        var g6 =
            @"1. e4 e5 2. Nf3 Nc6 3. c3 d6 4. d4 Qe7 5. Bd3 g6 6. O-O Bg7 7. Nbd2 Nf6 8. Nc4 O-O 9. dxe5 dxe5 10. Bg5 h6 11. Bh4 Rd8 12. Qc2 g5 13. Bg3 Nh5 14. Ne3 Nf4 15. Rfd1 Be6 16. Bc4 a6 17. a4 Rab8 18. b4 Qe8 19. h4 g4 20. Nh2 h5 21. Nd5 Bxd5 22. exd5 Ne7 23. Qe4 Nc8 24. Ba2 Nd6 25. Qc2 b5 26. Nf1 Ng6 27. Ne3 Qd7 28. Bb1 Ra8 29. Qa2 Qe8 30. Bxg6 fxg6 31. c4 Qf7 32. axb5 Ne4 33. bxa6 Nc3 34. Qa3 Nxd1 35. Rxd1 Qf6 36. b5 g5 37. hxg5 Qxg5 38. d6 h4 39. Bxh4 Qxh4 40. dxc7 Rf8 41. g3 Qf6 42. Rd2 Kh8 43. Qd6 Qf3 44. Qd5 Qf6 45. f3 e4 46. Rh2 Bh6 47. f4 Qa1 48. Qd1 Qc3 49. Qd2 Qxd2 50. Rxd2 Bg7 51. b6 Bc3 52. Rd7 Bb4 53. b7 Rae8 54. a7 Bc5 55. b8=Q Bxe3 56. Kf1";
        var f6 = "1Q2rr1k/P1PR4/8/8/2P1pPp1/4b1P1/8/5K2 b - - 0 1";
        CheckGame(g6, f6);

        var g6_2 =
            @"1. e4 e5 2. Nf3 Nc6 3. c3 d6 4. d4 Qe7 5. Bd3 g6 6. O-O Bg7 7. Nbd2 Nf6 8. Nc4 O-O 9. dxe5 dxe5 10. Bg5 h6 11. Bh4 Rd8 12. Qc2 g5 13. Bg3 Nh5 14. Ne3 Nf4 15. Rfd1 Be6 16. Bc4 a6 17. a4 Rab8 18. b4 Qe8 19. h4 g4 20. Nh2 h5 21. Nd5 Bxd5 22. exd5 Ne7 23. Qe4 Nc8 24. Ba2 Nd6 25. Qc2 b5 26. Nf1 Ng6 27. Ne3 Qd7 28. Bb1 Ra8 29. Qa2 Qe8 30. Bxg6 fxg6 31. c4 Qf7 32. axb5 Ne4 33. bxa6 Nc3 34. Qa3 Nxd1 35. Rxd1 Qf6 36. b5 g5 37. hxg5 Qxg5 38. d6 h4 39. Bxh4 Qxh4 40. dxc7 Rf8 41. g3 Qf6 42. Rd2 Kh8 43. Qd6 Qf3 44. Qd5 Qf6 45. f3 e4 46. Rh2 Bh6 47. f4 Qa1 48. Qd1 Qc3 49. Qd2 Qxd2 50. Rxd2 Bg7 51. b6 Bc3 52. Rd7 Bb4 53. b7 Rae8 54. a7 Bc5 55. b8 Bxe3 56. Kf1";

        CheckGame(g6_2, f6);
    }

    [Test]
    public void Pgn()
    {
        var g1 = @"[FEN ""r1b2rk1/ppp2ppp/1b3N2/3q4/3p4/Q4N2/PPP2KPP/R1B1R3 b - - 0 12""]

1. ... gxf6 2. Qxf8+ Kxf8 3. Bh6+ Kg8 4. Re8#";

        var f1 = "r1b1R1k1/ppp2p1p/1b3p1B/3q4/3p4/5N2/PPP2KPP/R7 b - - 0 1";
        var f1Init = "r1b2rk1/ppp2ppp/1b3N2/3q4/3p4/Q4N2/PPP2KPP/R1B1R3 b - - 0 12";

        CheckGame(g1, f1);

        var r1 =
            @"[Event """"]
[Site """"]
[Date ""2013.04.01""]
[Round ""1""]
[White ""Human""]
[Black ""Human""]
[Result ""*""]
[FEN ""r1b2rk1/ppp2ppp/1b3N2/3q4/3p4/Q4N2/PPP2KPP/R1B1R3 b - - 0 12""]]

1. ... gxf6 2. Qxf8+ Kxf8 3. Bh6+ Kg8 4. Re8#
".Replace("\r", "");

        var game1 = new ChessGame(g1);
        Assert.AreEqual(game1.Fen, game1.InitialBoard.Fen);
        Assert.AreEqual(f1Init, game1.Fen);
        Assert.AreEqual(f1Init, game1["FEN"]);
        game1.DateValue = new DateTime(2013, 4, 1);
        Assert.AreEqual(r1, game1.Pgn);
    }

    [Test]
    public void PieceScore()
    {
        Assert.AreEqual(3, ChessPiece.Bishop.PieceScore());
        Assert.AreEqual(3, ChessPiece.Knight.PieceScore());
        Assert.AreEqual(1, ChessPiece.Pawn.PieceScore());
        Assert.AreEqual(9, ChessPiece.Queen.PieceScore());
        Assert.AreEqual(5, ChessPiece.Rook.PieceScore());
        Assert.AreEqual(0, ChessPiece.Space.PieceScore());
        Assert.AreEqual(0, ChessPiece.King.PieceScore());
        Assert.AreEqual(3, (ChessPiece.Bishop | ChessPiece.Black).PieceScore());
    }

    [Test]
    public void PieceSymbols()
    {
        Assert.AreEqual("K"[0], ChessPiece.King.GetPieceCharacter());
        Assert.AreEqual("K"[0], ChessPiece.BlackKing.GetPieceCharacter());
        Assert.AreEqual("Q"[0], ChessPiece.Queen.GetPieceCharacter());
        Assert.AreEqual("Q"[0], ChessPiece.BlackQueen.GetPieceCharacter());
        Assert.AreEqual("R"[0], ChessPiece.Rook.GetPieceCharacter());
        Assert.AreEqual("R"[0], ChessPiece.BlackRook.GetPieceCharacter());
        Assert.AreEqual("B"[0], ChessPiece.Bishop.GetPieceCharacter());
        Assert.AreEqual("B"[0], ChessPiece.BlackBishop.GetPieceCharacter());
        Assert.AreEqual("N"[0], ChessPiece.Knight.GetPieceCharacter());
        Assert.AreEqual("N"[0], ChessPiece.BlackKnight.GetPieceCharacter());
        Assert.AreEqual("P"[0], ChessPiece.Pawn.GetPieceCharacter());
        Assert.AreEqual("P"[0], ChessPiece.BlackPawn.GetPieceCharacter());
        Assert.AreEqual(" "[0], ChessPiece.Space.GetPieceCharacter());
        Assert.AreEqual(" "[0], ChessPiece.Black.GetPieceCharacter());

        Assert.AreEqual("♔"[0], ChessPiece.King.GetPieceSymbol());
        Assert.AreEqual("♚"[0], ChessPiece.BlackKing.GetPieceSymbol());
        Assert.AreEqual("♕"[0], ChessPiece.Queen.GetPieceSymbol());
        Assert.AreEqual("♛"[0], ChessPiece.BlackQueen.GetPieceSymbol());
        Assert.AreEqual("♖"[0], ChessPiece.Rook.GetPieceSymbol());
        Assert.AreEqual("♜"[0], ChessPiece.BlackRook.GetPieceSymbol());
        Assert.AreEqual("♗"[0], ChessPiece.Bishop.GetPieceSymbol());
        Assert.AreEqual("♝"[0], ChessPiece.BlackBishop.GetPieceSymbol());
        Assert.AreEqual("♘"[0], ChessPiece.Knight.GetPieceSymbol());
        Assert.AreEqual("♞"[0], ChessPiece.BlackKnight.GetPieceSymbol());
        Assert.AreEqual("♙"[0], ChessPiece.Pawn.GetPieceSymbol());
        Assert.AreEqual("♟"[0], ChessPiece.BlackPawn.GetPieceSymbol());
        Assert.AreEqual(" "[0], ChessPiece.Space.GetPieceSymbol());
        Assert.AreEqual(" "[0], ChessPiece.Black.GetPieceSymbol());

        Assert.AreEqual('a', ChessBoard.GetFileCharacter(1));
        Assert.AreEqual('g', ChessBoard.GetFileCharacter(7));
    }

    [Test]
    public void UnderPromotion()
    {
        // Knight Promo
        var g1 = @"1.d4 Nf6 2.Nc3 d5 3.Bg5 c5 4.Bxf6 gxf6 5.e4 dxe4 6.dxc5 Qa5
7.Qh5 Bg7 8.Bb5+ Nc6 9.Ne2 O-O 10.a3 f5 11.O-O Qc7 12.b4 Be6
13.Rad1 Rad8 14.Ba4 a5 15.Nb5 Qe5 16.c3 axb4 17.axb4 Bc4
18.Rxd8 Rxd8 19.Nbd4 Nxd4 20.cxd4 Qf6 21.Rc1 Qa6 22.Bd1 Qa2
23.h3 Bd3 24.Ng3 Qd2 25.Nxf5 e3 26.Nxe7+ Kh8 27.Qh4 exf2
28.Kh2 Rxd4 29.Qg3 f1=N+ 0-1";
        var f1 = "7k/1p2Npbp/8/2P5/1P1r4/3b2QP/3q2PK/2RB1n2 w - - 0 1";
        CheckGame(g1, f1);

        var g2 = @"1.d4 d5 2.c4 c6 3.Nc3 Nf6 4.Nf3 e6 5.Bg5 dxc4 6.e4 b5 7.a4 Bb7
8.axb5 cxb5 9.Nxb5 Qb6 10.Qa4 Nc6 11.Bxf6 gxf6 12.Bxc4 a6
13.Nc3 Qxb2 14.O-O Qxc3 15.d5 Qb4 16.dxc6 Bc8 17.Qa2 Bh6
18.Qe2 a5 19.Rfb1 Qc3 20.Ra4 O-O 21.h3 Bf4 22.g3 Rb8 23.Rxb8
Bxb8 24.Qd3 Qc1+ 25.Kg2 Bc7 26.Qd2 Qxd2 27.Nxd2 e5 28.Nb3 Be6
29.Nc5 Bxc4 30.Rxc4 Ra8 31.Kf3 Kf8 32.Kg4 Ke7 33.Kf5 Bb6
34.Nd7 Bxf2 35.Nxf6 Bxg3 36.Nd5+ Kd6 37.Nb6 Rg8 38.c7 Bh4
39.c8=N+ 1-0";
        var f2 = "2N3r1/5p1p/1N1k4/p3pK2/2R1P2b/7P/8/8 b - - 0 1";
        CheckGame(g2, f2);

        // Rook underpromotion
        var g3 = @"1. e4 e5 2. Nf3 Nf6 3. Nxe5 d6 4. Nf3 Nxe4 5. d4 d5 6. Bd3 Nc6
7. O-O Be7 8. c4 Nb4 9. Be2 O-O 10. Nc3 Be6 11. Ne5 f6 12. Nf3
c5 13. Be3 Rc8 14. dxc5 Bxc5 15. Bxc5 Rxc5 16. Qb3 Nxc3
17. Qxc3 Nc6 18. b4 d4 19. Qd2 Rxc4 20. Bxc4 Bxc4 21. Rfe1 Qd5
22. a4 a6 23. Qf4 Rd8 24. h4 Qd6 25. Qxd6 Rxd6 26. Nd2 Bd3
27. Re8+ Kf7 28. Rc8 Kg6 29. Rc7 Nxb4 30. Rxb7 Nd5 31. Rc1 Nc3
32. Kh2 Ne4 33. Nb3 Nxf2 34. Rcc7 Ng4+ 35. Kg3 Ne5 36. Rxg7+
Kf5 37. Rxh7 Bc2 38. Nd2 Bxa4 39. h5 Rd8 40. Rhg7 Bc6 41. Rb1
a5 42. Nb3 Ke4 43. Nxa5 Bd5 44. h6 d3 45. Kf2 Kd4 46. h7 Rh8
47. Rb4+ Kc5 48. Rh4 Kb6 49. Nb3 Bxb3 50. Rb4+ Kc6 51. Rxb3 d2
52. Rc3+ Kd5 53. Ke2 Nc4 54. Rg8 Rxh7 55. Rd8+ Kc5 56. Rc8+
Kd5 57. R8xc4 Rh2 58. Rg4 d1=R 59. Kxd1 Rh1+ 60. Ke2 Ra1
61. Rg6 Ke4 62. Re3+ Kf5 63. Rg8 Kf4 64. Rf3+ Ke5 65. Re8+ Kd6
66. Rd3+ Kc6 67. Re6+ Kc5 68. Rxf6 Ra2+ 69. Kf3 Ra8 70. Rc6+
Kb5 71. Rc5+ Kb4 72. Rb5+ Kc4 73. Rd4+ Kc3 74. Rc5+ Kxd4
75. Rf5 Ra1 76. Kf4 Rf1+ 77. Kg5 Ra1 78. g4 Ke4 79. Rb5 Kd3
80. Kg6 Ra6+ 81. Kh5 Kc4 82. Rf5 Ra8 83. g5 Rh8+ 84. Kg4 Rg8
85. Rf6 Rd8 86. g6 Rd7 87. Rf5 Kd3 88. Rg5 Rd4+ 89. Kh5 Rd8
90. g7 Rg8 91. Kh6 Ke3 92. Kh7 Re8 93. g8=Q Rxg8 94. Kxg8 Kf4
95. Rd5 Ke4 96. Rd7 Ke3 97. Kf7 Kf4 98. Re7 Kf5 99. Re6 Kg4
100. Rf6 Kg5 101. Kg7 Kg4 102. Kg6 Kh3 103. Kf5 Kg3 104. Kg5
Kh2 105. Kf4 Kg2 106. Kg4 Kh1 107. Rf2 Kg1 108. Kg3 Kh1
109. Rf1# 1-0";
        var f3 = "8/8/8/8/8/6K1/8/5R1k b - - 0 1";
        CheckGame(g3, f3);

        var g4 = @"1.e4 c5 2.Nf3 d6 3.d4 cxd4 4.Nxd4 Nf6 5.Nc3 Nc6 6.Be2 e6 7.Be3
Be7 8.Qd2 a6 9.O-O O-O 10.Rad1 Qc7 11.f4 Bd7 12.Nxc6 Bxc6
13.Bf3 Rad8 14.Qf2 Rd7 15.Bb6 Qb8 16.g4 d5 17.g5 Nxe4 18.Nxe4
dxe4 19.Rxd7 Bxd7 20.Bxe4 f5 21.Bd3 e5 22.Bc5 Qd8 23.h4 e4
24.Bc4+ Kh8 25.Bd4 Bc6 26.Rd1 Bc5 27.h5 Bxd4 28.Qxd4 Qxd4+
29.Rxd4 g6 30.h6 a5 31.Kf2 Rc8 32.Ke3 Re8 33.Rd6 Rc8 34.Be6
Re8 35.c4 Rb8 36.b3 Be8 37.a3 b6 38.Bd5 b5 39.c5 b4 40.a4 Rc8
41.c6 Rd8 42.c7 Rc8 43.Rd8 Rxd8 44.cxd8=B 1-0";
        var f4 = "3Bb2k/7p/6pP/p2B1pP1/Pp2pP2/1P2K3/8/8 b - - 0 1";
        CheckGame(g4, f4);
    }
}