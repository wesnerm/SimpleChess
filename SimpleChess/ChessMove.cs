using System.Text;

namespace SimpleChess.Chess;

public struct ChessMove
{
    public byte StartRank;
    public byte StartFile;
    public byte Rank;
    public byte File;
    public ChessPiece Piece;
    public ChessPiece Promotion;
    public MoveFlags Flags;

    public ChessMove(string text, bool white = true)
        : this()
    {
        var o = 0;
        var capture = false;

        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (char.IsUpper(ch))
            {
                if (ch == 'O')
                    o++;
                else
                    Piece = ChessPieceTools.GetPieceFromCharacter(ch);
                continue;
            }

            if (char.IsLower(ch))
            {
                if (ch >= 'a' && ch <= 'h')
                {
                    StartFile = File;
                    File = (byte) (ch - 'a' + 1);
                }
                else if (ch == 'x')
                {
                    capture = true;
                    Flags |= MoveFlags.Capture;
                }
                continue;
            }

            if (ch >= '1' && ch <= '8')
            {
                StartRank = Rank;
                Rank = (byte) (ch - '0');
                continue;
            }

            switch (ch)
            {
                case '#':
                    Flags |= MoveFlags.Checkmate;
                    break;
                case '+':
                    Flags |= MoveFlags.Check;
                    break;
                case '!':
                    Flags |= MoveFlags.Exclamation;
                    break;
                case '?':
                    Flags |= MoveFlags.Question;
                    break;
                case '=':
                    i++;
                    if (i < text.Length)
                    {
                        Promotion = ChessPieceTools.GetPieceFromCharacter(text[i]);
                        if (Promotion != 0)
                            Piece = ChessPiece.Pawn;
                    }
                    break;
            }
        }

        if (o > 1)
        {
            Piece = ChessPiece.King;
            StartFile = 5;
            File = (byte) (o == 2 ? 7 : 3);
            StartRank = Rank = (byte) (white ? 1 : 8);
        }

        if (Piece == 0 && StartRank == 0)
        {
            Piece = ChessPiece.Pawn;
            if (Rank != 0 && (Rank != (white ? 4 : 5) || capture))
                StartRank = unchecked((byte) (Rank + (white ? -1 : 1)));
            if (StartFile == 0 && !capture)
                StartFile = File;
        }
    }

    public bool Match(ChessMove move)
    {
        if (StartRank != 0 &&
            move.StartRank != StartRank
            && move.StartRank != 0)
            return false;

        if (Rank != 0 &&
            move.Rank != Rank
            && move.Rank != 0)
            return false;

        if (move.StartFile != StartFile
            && move.StartFile != 0
            && StartFile != 0)
            return false;

        if (File != 0
            && move.File != File
            && move.File != 0)
            return false;

        if (Piece != 0
            && move.Piece != Piece
            && move.Piece != 0)
            return false;

        if (move.Promotion != Promotion)
            return false;

        return true;
    }

    public override string ToString()
    {
        var sb = new StringBuilder();
        string dash = null;

        if ((Piece & ChessPiece.Black) == ChessPiece.King
            && StartFile != 0 && Math.Abs(StartFile - File) == 2)
        {
            sb.Append(StartFile < File ? "O-O" : "O-O-O");
        }
        else
        {
            if (Piece == ChessPiece.Pawn)
            {
                if (StartFile != 0 && StartFile != File)
                    sb.Append(ChessBoard.GetFileCharacter(StartFile));
            }
            else if (Piece != 0)
                sb.Append(Piece.GetPieceCharacter());
            else
            {
                if (StartFile != 0)
                    sb.Append(ChessBoard.GetFileCharacter(StartFile));
                if (StartRank != 0)
                    sb.Append(StartRank);
                dash = "-";
            }
        }

        if ((Flags & MoveFlags.Capture) != 0)
            dash = "x";

        if (dash != null)
            sb.Append(dash);
        sb.Append(ChessBoard.GetFileCharacter(File));
        sb.Append(Rank);

        if ((Flags & MoveFlags.Checkmate) != 0)
            sb.Append('#');
        else if ((Flags & MoveFlags.Check) != 0)
            sb.Append('+');

        else if (Promotion != 0)
        {
            sb.Append('=');
            sb.Append(Promotion.GetPieceCharacter());
        }

        if ((Flags & MoveFlags.Exclamation) != 0)
            sb.Append('!');
        if ((Flags & MoveFlags.Question) != 0)
            sb.Append('?');
        if ((Flags & MoveFlags.Exclamation2) != 0)
            sb.Append('!');
        if ((Flags & MoveFlags.Question2) != 0)
            sb.Append('?');

        return sb.ToString();
    }

    [Flags]
    public enum MoveFlags : byte
    {
        None,
        Exclamation = 0x1,
        Question = 0x2,
        Exclamation2 = 0x4,
        Question2 = 0x8,
        Check = 0x10,
        Checkmate = 0x20,
        Capture = 0x40
    }
}