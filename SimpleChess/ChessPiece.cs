namespace SimpleChess.Chess;

public enum ChessPiece : byte
{
    Space,
    King,
    Queen,
    Rook,
    Bishop,
    Knight,
    Pawn,
    Black = 0x8,
    BlackKing = Black | King,
    BlackQueen = Black | Queen,
    BlackRook = Black | Rook,
    BlackBishop = Black | Bishop,
    BlackKnight = Black | Knight,
    BlackPawn = Black | Pawn
}

public static class ChessPieceTools
{
    public static int PieceScore(this ChessPiece piece)
    {
        switch (piece & ~ChessPiece.Black)
        {
            case ChessPiece.Knight:
            case ChessPiece.Bishop:
                return 3;
            case ChessPiece.Rook:
                return 5;
            case ChessPiece.Queen:
                return 9;
            case ChessPiece.Pawn:
                return 1;
        }
        return 0;
    }

    public static bool IsWhite(this ChessPiece p)
    {
        return p != 0 && (p & ChessPiece.Black) == 0;
    }

    public static char GetPieceCharacter(this ChessPiece piece)
    {
        const string chars1 = " KQRBNP";
        var i = (int) (piece & ~ChessPiece.Black);
        var s = chars1;
        return i < s.Length ? s[i] : ' ';
    }

    public static ChessPiece GetPieceFromCharacter(char ch, bool color = false)
    {
        ChessPiece p;
        switch (char.ToLower(ch))
        {
            case 'k':
                p = ChessPiece.King;
                break;
            case 'q':
                p = ChessPiece.Queen;
                break;
            case 'b':
                p = ChessPiece.Bishop;
                break;
            case 'r':
                p = ChessPiece.Rook;
                break;
            case 'n':
                p = ChessPiece.Knight;
                break;
            case 'p':
                p = ChessPiece.Pawn;
                break;
            default:
                return 0;
        }

        if (color && char.IsLower(ch))
            p |= ChessPiece.Black;
        return p;
    }

    public static char GetPieceSymbol(this ChessPiece piece)
    {
        const string syms1 = " ♔♕♖♗♘♙";
        const string syms2 = " ♚♛♜♝♞♟";
        var i = (int) (piece & ~ChessPiece.Black);
        var white = piece.IsWhite();

        var s = white ? syms1 : syms2;
        return i < s.Length ? s[i] : ' ';
    }
}