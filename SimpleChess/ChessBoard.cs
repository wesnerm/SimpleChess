using System.Text;

namespace SimpleChess.Chess;

public unsafe struct ChessBoard : IEquatable<ChessBoard>
{
    #region Variables

#pragma warning disable 649
    private fixed uint _rank[8];
#pragma warning restore 649
    private ChessFlags _chessFlags;
    private byte _enPassant;
    private byte _drawPlies;
    private byte _fullMoves;
    private byte _whiteKing;
    private byte _blackKing;

    #endregion

    #region Constructor

    public ChessBoard(string fen)
        : this()
    {
        SetFen(fen);
    }

    public static ChessBoard NewBoard()
    {
        const uint black = 0x88888888;
        const uint pieces =
            (uint)ChessPiece.Rook << 0 | (uint)ChessPiece.Knight << 4 | (uint)ChessPiece.Bishop << 8 |
            (uint)ChessPiece.Queen << 12 | (uint)ChessPiece.King << 16 | (uint)ChessPiece.Bishop << 20 |
            (uint)ChessPiece.Knight << 24 | (uint)ChessPiece.Rook << 28;
        const uint pawns =
            (uint)ChessPiece.Pawn << 0 | (uint)ChessPiece.Pawn << 4 | (uint)ChessPiece.Pawn << 8 |
            (uint)ChessPiece.Pawn << 12 | (uint)ChessPiece.Pawn << 16 | (uint)ChessPiece.Pawn << 20 |
            (uint)ChessPiece.Pawn << 24 | (uint)ChessPiece.Pawn << 28;

        var board = new ChessBoard
        {
            _chessFlags =
                ChessFlags.WhiteCastleShort | ChessFlags.BlackCastleShort | ChessFlags.WhiteCastleLong | ChessFlags.BlackCastleLong
        };

        board._rank[0] = pieces;
        board._rank[1] = pawns;
        board._rank[6] = pawns | black;
        board._rank[7] = pieces | black;
        return board;
    }

    #endregion

    #region Properties

    public string Fen
    {
        get { return GetFen(); }
        set { SetFen(value); }
    }

    public bool WhiteToPlay
    {
        get { return (_chessFlags & ChessFlags.BlackToPlay) == 0; }
        set
        {
            _chessFlags = (_chessFlags & ~ChessFlags.BlackToPlay) // 
                          | (value ? 0 : ChessFlags.BlackToPlay);
        }
    }

    public int MoveCount
    {
        get { return _fullMoves + 1; }
        set { _fullMoves = (byte)(value - 1); }
    }

    public int DrawHalfMoves
    {
        get { return _drawPlies; }
        set { _drawPlies = (byte)Math.Min(value, 255); }
    }

    public ChessPiece this[string loc]
    {
        get
        {
            int file, rank;
            GetCell(loc, out file, out rank);
            return this[file, rank];
        }
    }

    public ChessPiece this[int col, int row]
    {
        get
        {
            unchecked
            {
                var icol = col - 1;
                if ((uint)icol >= 8)
                    return OutsideBoard;

                var shift = icol << 2;
                var value = (GetRank(row) >> shift) & 0xfu;
                return (ChessPiece)value;
            }
        }
        set
        {
            unchecked
            {
                Debug.Assert(value != ChessPiece.Black);
                if ((uint)value >= 16)
                    throw new ArgumentOutOfRangeException();

                var icol = col - 1;
                if ((uint)icol >= 8)
                    throw new ArgumentOutOfRangeException();

                var shift = icol << 2;
                var mask = 0xfu << shift;
                SetRank(row, (uint)value << shift, ~mask);
            }
        }
    }

    public bool InCheck
    {
        get
        {
            int rank, file;
            var white = WhiteToPlay;
            if (!FindKing(white, out file, out rank))
            {
                Debug.Assert(false);
                return false;
            }
            return IsAttacked(file, rank, white);
        }
    }

    public bool Checkmated
    {
        get { return InCheck && GameEnded; }
    }

    public bool GameEnded
    {
        get
        {
            var gameEnded = true;
            LegalMoves(x => { gameEnded = false; });
            return gameEnded;
        }
    }

    public int Score
    {
        get
        {
            var count = 0;
            for (var r = 1; r <= 8; r++)
                for (var f = 1; f <= 8; f++)
                {
                    var p = this[f, r];
                    if (p != 0)
                    {
                        var score = p.PieceScore();
                        if (p.IsWhite())
                            count += score;
                        else
                            count -= score;
                    }
                }
            return count;
        }
    }

    #endregion

    #region Methods

    public const ChessPiece OutsideBoard = ChessPiece.Black;

    public bool GetCell(string loc, out int file, out int rank)
    {
        if (loc != null && loc.Length == 2)
        {
            file = char.ToLower(loc[0]) - 'a' + 1;
            rank = loc[1] - '0';
            if (file >= 1 && file <= 8
                && rank >= 1 && rank <= 8)
                return true;
        }

        file = 0;
        rank = 0;
        return false;
    }

    #endregion

    #region Object Overrides

    public override bool Equals(object obj)
    {
        return Equals((ChessBoard)obj);
    }

    public bool Equals(ChessBoard board)
    {
        fixed (uint* r1 = _rank)
        {
            var r2 = board._rank;
            return r1[0] == r2[0] && r1[1] == r2[1] && r1[2] == r2[2] && r1[3] == r2[3] && r1[4] == r2[4] && r1[5] == r2[5] &&
                   r1[6] == r2[6] && r1[7] == r2[7] && _chessFlags == board._chessFlags && _enPassant == board._enPassant;
        }
    }

    public override int GetHashCode()
    {
        fixed (uint* r = _rank)
            unchecked
            {
                var hash = HashCode.Combine((int)r[0], (int)r[1]);
                hash = HashCode.Combine(hash, (int)r[2], (int)r[3]);
                hash = HashCode.Combine(hash, (int)r[4], (int)r[5]);
                hash = HashCode.Combine(hash, (int)r[6], (int)r[7]);
                hash = HashCode.Combine(hash, (int)_chessFlags, _enPassant);
                return hash;
            }
    }

    #endregion

    #region Methods

    private uint GetRank(int rank)
    {
        var index = unchecked((uint)(rank - 1));
        fixed (uint* r = _rank)
            if ((index & ~7) == 0)
                return r[index];
        return 0x88888888;
    }

    private void SetRank(int rank, uint value, uint keepmask = 0)
    {
        var index = unchecked((uint)(rank - 1));
        if ((index & ~7) == 0)
        {
            fixed (uint* p = _rank)
            {
                var r = &p[index];
                *r = (*r & keepmask) | value;
            }
        }
        else
        {
            throw new ArgumentOutOfRangeException();
        }
    }

    public IEnumerable<ChessMove> GetMoves(string text)
    {
        var white = WhiteToPlay;
        var move = new ChessMove(text, white);

        if (move.StartFile != 0 && move.StartRank != 0)
        {
            if (move.Piece != 0)
                move.Piece = this[move.StartFile, move.StartRank] & ~ChessPiece.Black;

            if (move.Piece == ChessPiece.Pawn && (move.Rank == (WhiteToPlay ? 8 : 1)) && move.Promotion == 0)
                move.Promotion = ChessPiece.Queen;

            yield return move;
            yield break;
        }

        foreach (var m in LegalMoves())
        {
            if (move.Match(m))
                yield return m;
        }
    }

    private ChessMove SimplifyMove(ChessMove chessMove)
    {
        var move = chessMove;
        var excludeRank = true;
        var excludeFile = true;
        var search = true;

        if ((move.Piece & ~ChessPiece.Black) == ChessPiece.Pawn
            && move.StartFile != 0
            && move.StartFile != move.File)
        {
            search = false;
            excludeFile = false;
            move.Flags |= ChessMove.MoveFlags.Capture;
        }

        if (search)
        {
            var noStart = move;
            move.StartRank = 0;
            move.StartFile = 0;

            LegalMoves(m =>
            {
                if (noStart.Match(m) && !move.Match(m))
                {
                    if (move.StartFile != 0 && move.StartFile != m.StartFile)
                        excludeFile = false;
                    else
                        excludeRank = false;
                }
            });
        }

        if (excludeFile)
            move.StartFile = 0;
        if (excludeRank)
            move.StartRank = 0;

        if (this[move.File, move.Rank] != 0)
            move.Flags |= ChessMove.MoveFlags.Capture;

        var board = this;
        board.Move(move);
        if (board.InCheck)
        {
            move.Flags |= ~ChessMove.MoveFlags.Check;
            if (board.GameEnded)
                move.Flags |= ChessMove.MoveFlags.Checkmate;
        }

        return move;
    }

    public string RenderMove(ChessMove move)
    {
        var move2 = SimplifyMove(move);
        return move2.ToString();
    }

    public ChessPiece Move(string move)
    {
        if (!char.IsLetter(move[0]))
            return 0;

        return Move(GetMoves(move).Single());
    }

    public ChessPiece Move(int startFile, int startRank, int endFile, int endRank)
    {
        return Move(new ChessMove
        {
            StartFile = (byte)startFile,
            StartRank = (byte)startRank,
            File = (byte)endFile,
            Rank = (byte)endRank
        });
    }

    public ChessPiece Move(string startloc, string endloc)
    {
        int startFile, startRank;
        int file, rank;
        GetCell(startloc, out startFile, out startRank);
        GetCell(endloc, out file, out rank);
        return Move(startFile, startRank, file, rank);
    }

    public ChessPiece Move(ChessMove move)
    {
        var startFile = move.StartFile;
        var startRank = move.StartRank;
        var file = move.File;
        var rank = move.Rank;

        var startPiece = this[startFile, startRank];
        var endPiece = this[file, rank];
        var white = startPiece < ChessPiece.Black;
        Debug.Assert(move.Piece == 0 || (startPiece & ~ChessPiece.Black) == move.Piece);
        Debug.Assert(startPiece != 0);
        byte enPassant = 0;

        switch (startPiece & ~ChessPiece.Black)
        {
            case ChessPiece.King:
                // Check if start piece is king and castling
                if (startFile == 5 && startRank == rank)
                {
                    if (file == 7)
                    {
                        this[6, startRank] = this[8, startRank];
                        this[8, startRank] = ChessPiece.Space;
                    }
                    else if (file == 3)
                    {
                        this[4, startRank] = this[1, startRank];
                        this[1, startRank] = ChessPiece.Space;
                    }
                }
                _chessFlags &= white
                    ? ~(ChessFlags.WhiteCastleShort | ChessFlags.WhiteCastleLong)
                    : ~(ChessFlags.BlackCastleShort | ChessFlags.BlackCastleLong);
                goto default;

            case ChessPiece.Rook:
                if (white)
                {
                    if (startRank == 1)
                    {
                        if (startFile == 1) _chessFlags &= ~ChessFlags.WhiteCastleLong;
                        if (startFile == 8) _chessFlags &= ~ChessFlags.WhiteCastleShort;
                    }
                }
                else
                {
                    if (startRank == 8)
                    {
                        if (startFile == 1) _chessFlags &= ~ChessFlags.BlackCastleLong;
                        if (startFile == 8) _chessFlags &= ~ChessFlags.BlackCastleShort;
                    }
                }
                // If moving rook, check for castling
                goto default;

            case ChessPiece.Pawn:
                if (_enPassant == file)
                {
                    // Check if piece is pawn and enpassant
                    if (rank == (white ? 6 : 3))
                        this[file, startRank] = ChessPiece.Space;
                }
                else
                {
                    if (file == startFile)
                    {
                        if (white ? rank == 4 && startRank == 2 : rank == 5 && startRank == 7)
                        {
                            // Cuts down on the number of distinguishable positions
                            var enP = white ? ChessPiece.BlackPawn : ChessPiece.Pawn;
                            if (this[file - 1, rank] == enP || this[file + 1, rank] == enP)
                                enPassant = file;
                        }
                    }

                    // Check if piece is pawn and promoting
                    if (rank == 1 || rank == 8)
                    {
                        var promo = move.Promotion == 0 ? ChessPiece.Queen : move.Promotion;
                        startPiece ^= (promo & ~ChessPiece.Black) ^ ChessPiece.Pawn;
                    }
                }
                _drawPlies = 0;
                break;

            default:
                if (endPiece == 0)
                    _drawPlies++;
                else
                    _drawPlies = 0;
                break;
        }

        if ((endPiece & ~ChessPiece.Black) == ChessPiece.Rook)
        {
            if (rank == 1)
            {
                if (file == 1) _chessFlags &= ~ChessFlags.WhiteCastleLong;
                else if (file == 8) _chessFlags &= ~ChessFlags.WhiteCastleShort;
            }
            else if (rank == 8)
            {
                if (file == 1) _chessFlags &= ~ChessFlags.BlackCastleLong;
                else if (file == 8) _chessFlags &= ~ChessFlags.BlackCastleShort;
            }
        }

        this[startFile, startRank] = ChessPiece.Space;
        this[file, rank] = startPiece;
        if (!white) _fullMoves++;
        WhiteToPlay = !white;
        _enPassant = enPassant;
        return endPiece;
    }

    public bool FindKing(bool white, out int file, out int rank)
    {
        var pos = white ? _whiteKing : _blackKing;
        file = pos & 0xf;
        rank = pos >> 4;
        var p = white ? ChessPiece.King : ChessPiece.BlackKing;
        if (this[file, rank] == p)
            return true;
        if (!FindPiece(p, out file, out rank))
            return false;
        pos = (byte)(file | (rank << 4));
        if (white) _whiteKing = pos;
        else _blackKing = pos;
        return true;
    }

    public bool FindPiece(ChessPiece piece, out int file, out int rank)
    {
        for (var r = 1; r <= 8; r++)
        {
            if (GetRank(r) == 0)
                continue;

            for (var f = 1; f <= 8; f++)
            {
                var p = this[f, r];
                if (p == piece)
                {
                    rank = r;
                    file = f;
                    return true;
                }
            }
        }

        rank = 0;
        file = 0;
        return false;
    }

    public bool IsAttacked(int file, int rank, bool white)
    {
        // Check knight
        var c = white ? 0 : ChessPiece.Black;
        var en = ChessPiece.Knight ^ ChessPiece.Black ^ c;
        if (this[file - 2, rank - 1] == en
            || this[file + 2, rank + 1] == en
            || this[file + 2, rank - 1] == en
            || this[file - 2, rank + 1] == en
            || this[file - 1, rank - 2] == en
            || this[file + 1, rank + 2] == en
            || this[file + 1, rank - 2] == en
            || this[file - 1, rank + 2] == en)
            return true;

        var p = FindAttacker(file, rank, -1, -1);
        if (p != 0 && p.IsWhite() != white)
            return true;

        p = FindAttacker(file, rank, -1, 0);
        if (p != 0 && p.IsWhite() != white)
            return true;

        p = FindAttacker(file, rank, -1, 1);
        if (p != 0 && p.IsWhite() != white)
            return true;

        p = FindAttacker(file, rank, 0, 1);
        if (p != 0 && p.IsWhite() != white)
            return true;

        p = FindAttacker(file, rank, 0, -1);
        if (p != 0 && p.IsWhite() != white)
            return true;

        p = FindAttacker(file, rank, 1, -1);
        if (p != 0 && p.IsWhite() != white)
            return true;

        p = FindAttacker(file, rank, 1, 0);
        if (p != 0 && p.IsWhite() != white)
            return true;

        p = FindAttacker(file, rank, 1, 1);
        if (p != 0 && p.IsWhite() != white)
            return true;

        return false;
    }

    private ChessPiece FindAttacker(int file, int rank, int dr, int df)
    {
        var rcount = dr > 0
            ? 8 - rank
            : dr < 0
                ? rank - 1
                : 7;

        var fcount = df > 0
            ? 8 - file
            : df < 0
                ? file - 1
                : 7;

        var count = Math.Min(rcount, fcount);
        Debug.Assert(dr != 0 || df != 0);
        var r = rank;
        var f = file;
        for (var i = 1; i <= count; i++)
        {
            r += dr;
            f += df;
            var p = this[f, r];
            if (p != 0)
            {
                var p2 = p & ~ChessPiece.Black;
                if (i > 2 && (p2 == ChessPiece.Pawn || p2 == ChessPiece.King))
                    break;
                switch (p2)
                {
                    case ChessPiece.Pawn:
                        if (i >= 2
                            || df == 0
                            || dr != (p == ChessPiece.Pawn ? -1 : 1))
                            return 0;
                        break;
                    case ChessPiece.King:
                        if (i >= 2)
                            return 0;
                        break;
                    case ChessPiece.Rook:
                        if (dr != 0 && df != 0)
                            return 0;
                        break;
                    case ChessPiece.Bishop:
                        if (dr == 0 || df == 0)
                            return 0;
                        break;
                    case ChessPiece.Knight:
                        return 0;
                }
                return p;
            }
        }

        return 0;
    }

    private void SetFen(string fen)
    {
        this = default(ChessBoard);

        var array = fen.Split();
        var position = array.Length > 0 ? array[0] : "";
        var color = array.Length > 1 ? array[1] : "";
        var castling = array.Length > 2 ? array[2] : "";
        var ep = array.Length > 3 ? array[3] : "";
        var hf = array.Length > 4 ? array[4] : "";
        var mv = array.Length > 5 ? array[5] : "";

        if (ep.Length == 2 && ep[0] >= 'a' && ep[0] <= 'h')
            _enPassant = (byte)(ep[0] - 'a' + 1);
        WhiteToPlay = color != "b";

        int fullMoves, halfmoves;
        int.TryParse(hf, out halfmoves);
        DrawHalfMoves = halfmoves;

        int.TryParse(mv, out fullMoves);
        MoveCount = Math.Max(fullMoves, 1);

        foreach (var ch in castling)
        {
            switch (ch)
            {
                case 'K':
                    _chessFlags |= ChessFlags.BlackCastleShort;
                    break;
                case 'Q':
                    _chessFlags |= ChessFlags.BlackCastleLong;
                    break;
                case 'k':
                    _chessFlags |= ChessFlags.WhiteCastleShort;
                    break;
                case 'q':
                    _chessFlags |= ChessFlags.WhiteCastleLong;
                    break;
            }
        }

        var c = 1;
        var r = 8;
        foreach (var ch in position)
        {
            var p = ChessPiece.Space;
            switch (char.ToLower(ch))
            {
                case 'k':
                case 'q':
                case 'p':
                case 'r':
                case 'b':
                case 'n':
                    p = ChessPieceTools.GetPieceFromCharacter(ch, true);
                    break;
                case '/':
                    r--;
                    c = 1;
                    break;
                default:
                    if (ch >= '1' && ch <= '8')
                        c += ch - '0';
                    break;
            }

            if (p != ChessPiece.Space)
            {
                if (r < 1)
                    break;
                if (c >= 1)
                    this[c++, r] = p;
            }
        }
    }

    private string GetFen()
    {
        var sb = new StringBuilder();

        for (var r = 8; r >= 1; r--)
        {
            if (r != 8)
                sb.Append('/');

            for (var c = 1; c <= 8; c++)
            {
                var p = this[c, r];
                var white = p < ChessPiece.Black;
                switch (p & ~ChessPiece.Black)
                {
                    case ChessPiece.King:
                        sb.Append(white ? 'K' : 'k');
                        break;
                    case ChessPiece.Queen:
                        sb.Append(white ? 'Q' : 'q');
                        break;
                    case ChessPiece.Rook:
                        sb.Append(white ? 'R' : 'r');
                        break;
                    case ChessPiece.Pawn:
                        sb.Append(white ? 'P' : 'p');
                        break;
                    case ChessPiece.Knight:
                        sb.Append(white ? 'N' : 'n');
                        break;
                    case ChessPiece.Bishop:
                        sb.Append(white ? 'B' : 'b');
                        break;
                    case ChessPiece.Space:
                        var space = '1';
                        for (; c < 8 && this[c + 1, r] == ChessPiece.Space; space++)
                            c++;
                        sb.Append(space);
                        break;
                }
            }
        }

        sb.Append(' ');
        // Active color
        sb.Append(WhiteToPlay ? 'w' : 'b');
        sb.Append(' ');
        // Castling
        var len = sb.Length;
        if ((_chessFlags & ChessFlags.WhiteCastleShort) != 0)
            sb.Append('K');
        if ((_chessFlags & ChessFlags.WhiteCastleLong) != 0)
            sb.Append('Q');
        if ((_chessFlags & ChessFlags.BlackCastleShort) != 0)
            sb.Append('k');
        if ((_chessFlags & ChessFlags.BlackCastleLong) != 0)
            sb.Append('q');
        if (len == sb.Length)
            sb.Append('-');

        sb.Append(' ');
        // Enpassant target square
        if (_enPassant == 0)
            sb.Append('-');
        else
        {
            sb.Append((char)('a' + _enPassant - 1));
            sb.Append(WhiteToPlay ? '6' : '3');
        }
        sb.Append(' ');
        // Halfmove clock
        sb.Append(_drawPlies.ToString());
        sb.Append(' ');
        // Fullmove number
        sb.Append((_fullMoves + 1).ToString());
        return sb.ToString();
    }

    public List<ChessMove> LegalMoves()
    {
        var list = new List<ChessMove>();
        LegalMoves(list.Add);
        return list;
    }

    public void LegalMoves(Action<ChessMove> action)
    {
        var white = WhiteToPlay;
        for (var r = 1; r <= 8; r++)
            for (var c = 1; c <= 8; c++)
            {
                var p = this[c, r];
                if (p == 0 || p.IsWhite() != white)
                    continue;

                var move = new ChessMove
                {
                    Piece = p & ~ChessPiece.Black,
                    StartFile = (byte)c,
                    StartRank = (byte)r
                };

                switch (move.Piece)
                {
                    case ChessPiece.Pawn:
                        var dr = white ? 1 : -1;
                        ChessPiece promoStart = 0, promoEnd = 0;
                        if (move.StartRank == (white ? 7 : 2))
                        {
                            promoStart = ChessPiece.Queen;
                            promoEnd = ChessPiece.Knight;
                        }
                        else if (move.StartRank == (white ? 2 : 7)
                                 && this[c, r + dr] == ChessPiece.Space
                                 && this[c, r + dr + dr] == ChessPiece.Space)
                        {
                            AttemptMove(ref move, 0, dr + dr, action);
                        }

                        for (var promo = promoStart; promo <= promoEnd; promo++)
                        {
                            move.Promotion = promo;
                            if (this[c, r + dr] == ChessPiece.Space)
                                AttemptMove(ref move, 0, dr, action);
                            if (this[c + 1, r + dr] != ChessPiece.Space
                                || _enPassant == c + 1 && r == (white ? 5 : 4))
                                AttemptMove(ref move, +1, dr, action);
                            if (this[c - 1, r + dr] != ChessPiece.Space
                                || _enPassant == c - 1 && r == (white ? 5 : 4))
                                AttemptMove(ref move, -1, dr, action);
                        }
                        continue;

                    case ChessPiece.King:
                        Attempt4Move(ref move, 0, 1, action);
                        Attempt4Move(ref move, 1, 1, action);

                        if (c == 5 && r == (white ? 1 : 8))
                        {
                            move.Flags |= ChessMove.MoveFlags.Capture;
                            if ((_chessFlags & (white ? ChessFlags.WhiteCastleShort : ChessFlags.BlackCastleShort)) != 0
                                && this[7, r] == ChessPiece.Space
                                && this[6, r] == ChessPiece.Space
                                && !IsAttacked(6, r, white))
                                AttemptMove(ref move, +2, 0, action);
                            if ((_chessFlags & (white ? ChessFlags.WhiteCastleLong : ChessFlags.BlackCastleLong)) != 0
                                && this[4, r] == ChessPiece.Space
                                && this[3, r] == ChessPiece.Space
                                && this[2, r] == ChessPiece.Space
                                && !IsAttacked(4, r, white))
                                AttemptMove(ref move, -2, 0, action);
                        }
                        continue;

                    case ChessPiece.Queen:
                        Attempt4Move(ref move, 1, 1, action, true);
                        goto case ChessPiece.Rook;

                    case ChessPiece.Rook:
                        Attempt4Move(ref move, 0, 1, action, true);
                        continue;

                    case ChessPiece.Knight:
                        Attempt4Move(ref move, 2, 1, action);
                        Attempt4Move(ref move, 1, 2, action);
                        continue;

                    case ChessPiece.Bishop:
                        Attempt4Move(ref move, 1, 1, action, true);
                        continue;
                }
            }
    }

    private void AttemptMove(ref ChessMove m, int df, int dr, Action<ChessMove> action, bool repeat = false)
    {
        unchecked
        {
            m.Rank = m.StartRank;
            m.File = m.StartFile;
            var white = WhiteToPlay;

            do
            {
                m.Rank = (byte)(m.Rank + dr);
                m.File = (byte)(m.File + df);

                if (m.Rank < 1 || m.Rank > 8 || m.File < 1 || m.File > 8)
                    return;

                var hit = this[m.File, m.Rank];
                if (hit != ChessPiece.Space)
                {
                    if (hit.IsWhite() == white)
                        return;
                    m.Flags |= ChessMove.MoveFlags.Capture;
                    repeat = false;
                }

                // Check legality
                var board = this;
                board.Move(m);
                board.WhiteToPlay = WhiteToPlay;
                if (!board.InCheck)
                {
                    board.WhiteToPlay = !WhiteToPlay;
                    if (board.InCheck)
                        m.Flags |= ChessMove.MoveFlags.Check;
                    action(m);
                }
            } while (repeat);
        }
    }

    private void Attempt4Move(ref ChessMove move, int df, int dr, Action<ChessMove> action, bool repeat = false)
    {
        AttemptMove(ref move, +df, +dr, action, repeat);
        AttemptMove(ref move, +dr, -df, action, repeat);
        AttemptMove(ref move, -df, -dr, action, repeat);
        AttemptMove(ref move, -dr, +df, action, repeat);
    }

    public override string ToString()
    {
        return GetFen();
    }

    public static char GetFileCharacter(int column)
    {
        if (column < 1 || column > 8)
            return ' ';
        return (char)('a' + column - 1);
    }

    #endregion
}

public enum ChessFormat
{
    Symbol,
    Character,
    Text
}

[Flags]
public enum ChessFlags : byte
{
    WhiteCastleShort = 1,
    WhiteCastleLong = 2,
    BlackCastleShort = 4,
    BlackCastleLong = 8,
    BlackToPlay = 16
}