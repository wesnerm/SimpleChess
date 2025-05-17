using System.Text;

namespace SimpleChess.Chess;

public class ChessGame
{
    public enum PgnToken
    {
        Symbol,
        Number,
        LeftBracket,
        LeftBrace,
        LeftParanthesis,
        RightBracket,
        RightBrace,
        RightParenthesis,
        Annotation,
        String,
        Comment,
        Escape,
        Dot
    }

    public class GameMoves
    {
        public string Annotation;
        public ChessMove Move;
    }

    #region Variables

    private readonly Dictionary<string, string> _tags = new Dictionary<string, string>();
    private readonly List<string> _plies = new List<string>();
    public const string StandardFen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

    #endregion

    #region Constructor

    public ChessGame()
    {
        Clear();
    }

    public ChessGame(string pgn)
    {
        Pgn = pgn;
    }

    #endregion

    #region Properties

    public string Event
    {
        get { return this["Event"]; }
        set { this["Event"] = value; }
    }

    public string Site
    {
        get { return this["Site"]; }
        set { this["Site"] = value; }
    }

    public string Date
    {
        get { return this["Date"] ?? ""; }
        set { this["Date"] = value; }
    }

    public DateTime? DateValue
    {
        get { return ParseDate(Date); }
        set
        {
            if (value == null)
                _tags.Remove("Date");
            else
                Date = ((DateTime) value).ToString("yyyy.MM.dd");
        }
    }

    public DateTime? ParseDate(string dateString)
    {
        if (string.IsNullOrWhiteSpace(dateString))
            return null;
        var array = dateString.Split('.');

        var yearString = array[0];
        var monthString = array.Length > 1 ? array[1] : "01";
        var dayString = array.Length > 2 ? array[2] : "01";
        int year, month, day;

        if (!int.TryParse(yearString, out year) || year <= 1 || year >= 2100)
            return null;

        if (!int.TryParse(monthString, out month))
            month = 1;
        if (!int.TryParse(dayString, out day))
            day = 1;

        return new DateTime(year, month, day);
    }

    public string Round
    {
        get { return this["Round"]; }
        set { this["Round"] = value; }
    }

    public string White
    {
        get { return this["White"]; }
        set { this["White"] = value; }
    }

    public string Black
    {
        get { return this["Black"]; }
        set { this["Black"] = value; }
    }

    public string Result
    {
        get { return this["Result"]; }
        set { this["Result"] = value; }
    }

    public string Fen
    {
        get { return this["FEN"] ?? StandardFen; }
        set { this["FEN"] = value; }
    }

    public string this[int index]
    {
        get { return _plies[index]; }
    }

    public string this[string name]
    {
        get { return _tags.GetValueOrDefault(name) ?? ""; }
        set { _tags[name] = value ?? ""; }
    }

    public string Pgn
    {
        get { return SavePgn(); }
        set { LoadPgn(value); }
    }

    public override string ToString()
    {
        return SavePgn(false);
    }

    #endregion

    #region Methods

    public void Clear(string fen = null)
    {
        _tags.Clear();
        _plies.Clear();
        Date = DateTime.Now.ToString("yyyy.MM.dd");
        Result = "*";
        White = Black = "Human";
        Event = "";
        Site = "";
        Round = "1";

        if (fen != null)
            Fen = fen;
    }

    private void LoadPgn(string pgn)
    {
        Clear();
        var tagMode = false;
        string tag = null;
        var paren = 0;

        foreach (var token in Parse(pgn))
        {
            switch (token[0])
            {
                case '"':
                    if (tag != null && tagMode)
                    {
                        var s = token.Substring(1, Math.Max(0, token.Length - 2));
                        this[tag] = s;
                        tag = null;
                    }
                    break;
                case '[':
                    tagMode = true;
                    tag = null;
                    break;
                case ']':
                    tagMode = false;
                    break;
                case '(':
                    paren++;
                    break;
                case ')':
                    paren--;
                    break;
                default:
                    if (tagMode)
                        tag = token;
                    else if (paren == 0)
                    {
                        _plies.Add(token);
                        if (!IsMove(token))
                            Result = token;
                    }
                    break;
            }
        }
    }

    private static bool IsPunc(char ch)
    {
        switch (ch)
        {
            case ';':
            case '{':
            case '"':
            case '[':
            case ']':
            case '(':
            case ')':
            case '.':
            case '*':
                return true;
        }

        return false;
    }

    private IEnumerable<string> Parse(string text)
    {
        var i = 0;
        while (i < text.Length)
        {
            var start = i;
            var ch = text[i];

            if (char.IsWhiteSpace(ch))
            {
                i++;
                continue;
            }

            switch (ch)
            {
                case ';':
                case '%':
                    while (i < text.Length)
                    {
                        ch = text[i];
                        if (ch == '\n' || ch == '\r')
                            break;
                        i++;
                    }
                    continue;

                case '{':
                    while (i < text.Length)
                    {
                        if (text[i++] == '}')
                            break;
                    }
                    continue;

                case '"':
                    for (i++; i < text.Length; i++)
                    {
                        ch = text[i];
                        if (ch == '"')
                        {
                            i++;
                            yield return text.Substring(start, i - start);
                            break;
                        }
                        if (ch == '\\')
                        {
                            if (i + 1 < text.Length)
                                i++;
                        }
                    }
                    continue;

                case '[':
                case ']':
                case '(':
                case ')':
                    i++;
                    break;

                case '<':
                case '>':
                case '.':
                case '*':
                case '$':
                    // ignore dots
                    i++;
                    continue;

                default:
                    for (i++; i < text.Length; i++)
                    {
                        ch = text[i];
                        if (IsPunc(ch) || char.IsWhiteSpace(ch))
                            break;
                    }

                    var isNumber = true;
                    for (var j = start; j < i; j++)
                    {
                        ch = text[j];
                        if (ch < '0' || ch > '9')
                        {
                            isNumber = false;
                            break;
                        }
                    }

                    if (isNumber)
                        continue;
                    break;
            }

            yield return text.Substring(start, i - start);
        }
    }

    public string SavePgn(bool emitTags = true)
    {
        var sb = new StringBuilder();
        if (emitTags)
        {
            var set = new HashSet<string> {"Event", "Site", "Date", "Round", "White", "Black", "Result"};

            foreach (var item in set)
            {
                sb.Append('[');
                sb.Append(item);
                sb.Append(' ');
                sb.Append(this[item]);
                sb.Append("]\n");
            }

            foreach (var item in _tags.Keys)
            {
                if (set.Contains(item))
                    continue;
                sb.Append('[');
                sb.Append(item);
                sb.Append(' ');
                sb.Append(this[item]);
                sb.Append(']');
                sb.Append("]\n");
            }

            sb.Append("\n");
        }

        var moveNo = 0;
        var board = InitialBoard;

        if (!board.WhiteToPlay)
        {
            sb.Append("1. ...");
            moveNo++;
        }

        foreach (var move in _plies)
        {
            if (moveNo != 0)
                sb.Append(' ');

            if (moveNo%2 == 0)
            {
                if (IsMove(move))
                {
                    sb.Append(moveNo/2 + 1);
                    sb.Append(". ");
                }
            }

            sb.Append(move);
            moveNo++;
        }

        if (emitTags)
            sb.Append('\n');

        return sb.ToString();
    }

    public bool Move(string move)
    {
        _plies.Add(move);
        return true;
    }

    private bool IsMove(string move)
    {
        if (string.IsNullOrWhiteSpace(move))
            return false;
        return char.IsLetter(move[0]);
    }

    #endregion;

    #region Board

    public ChessBoard InitialBoard
    {
        get
        {
            var fen = Fen;
            if (string.IsNullOrWhiteSpace(fen))
                return ChessBoard.NewBoard();
            return new ChessBoard(fen);
        }
    }

    public ChessBoard GetBoard(int plyIndex)
    {
        var board = InitialBoard;
        for (var i = 0; i < plyIndex; i++)
        {
            var ply = _plies[i];
#if DEBUG
            var prevBoard = board;
#endif
            board.Move(ply);
        }
        return board;
    }

    public ChessBoard FinalBoard
    {
        get { return GetBoard(_plies.Count); }
    }

    public int PlyCount
    {
        get { return _plies.Count; }
    }

    #endregion
}