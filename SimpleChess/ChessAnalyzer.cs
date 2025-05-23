﻿using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace SimpleChess.Chess;

public class ChessAnalyzer
{
    public const float MateValue = 99999;
    public const float MateCutoff = MateValue - 100;

    private int _maxDepth;

    private readonly ConcurrentDictionary<ChessBoard, ChessResults> _tranpositions =
        new ConcurrentDictionary<ChessBoard, ChessResults>();

    public ChessResults FindBestMove(ChessBoard board, int depth)
    {
        _tranpositions.Clear();
        _maxDepth = depth;
        var result = NegaMaxFast(board, 0, float.MinValue, float.MaxValue);
        _tranpositions.Clear();
        return result;
    }

    private ChessResults NegaMaxFast(ChessBoard board,
        int depth,
        float alpha,
        float beta)
    {
        ChessResults data;
        var best = default(ChessMove);

        if (_tranpositions.TryGetValue(board, out data))
        {
            best = data.Move;
            if (ObtainScore(data, depth, ref alpha))
                return data;
        }

        var moves = board.LegalMoves();
        if (moves.Count == 0)
        {
            alpha = EvaluateEndOfGame(board, depth);
            goto Finish;
        }

        if (depth >= _maxDepth)
        {
            alpha = Evaluate(board);
            goto Finish;
        }

        var childDepth = depth + 1;
        Parallel.ForEach(moves, m =>
        {
            var b = board;
            b.Move(m);
            // if position has been visited before,
            // then return draw

            var results = NegaMax(b, childDepth, -beta, -alpha);
            var newScore = -results.Score;
            if (newScore > alpha)
            {
                alpha = newScore;
                best = m;
            }
        });

        Finish:
        return Memoize(ref board, best, alpha, depth);
    }

    private ChessResults NegaMax(ChessBoard board,
        int depth,
        float alpha,
        float beta)
    {
        ChessResults data;
        var best = default(ChessMove);

        if (_tranpositions.TryGetValue(board, out data))
        {
            best = data.Move;
            if (ObtainScore(data, depth, ref alpha))
                return data;
        }

        var moves = board.LegalMoves();
        if (moves.Count == 0)
        {
            alpha = EvaluateEndOfGame(board, depth);
            goto Finish;
        }

        if (depth >= _maxDepth)
        {
            alpha = Evaluate(board);
            goto Finish;
        }

        var childDepth = depth + 1;
        foreach (var m in moves)
        {
            var b = board;
            b.Move(m);
            // if position has been visited before,
            // then return draw

            var results = NegaMax(b, childDepth, -beta, -alpha);
            var newScore = -results.Score;
            if (newScore > alpha)
            {
                alpha = newScore;
                best = m;
                if (alpha >= beta)
                    break;
            }
        }

        Finish:
        return Memoize(ref board, best, alpha, depth);
    }

    private float EvaluateEndOfGame(ChessBoard board, int depth)
    {
        var score = board.InCheck ? depth - MateValue : 0;
        return score;
    }

    private int Evaluate(ChessBoard board)
    {
        var score = board.Score;
        return board.WhiteToPlay ? score : -score;
    }

    private ChessResults Memoize(ref ChessBoard board, ChessMove move, float score, int depth)
    {
        ChessResults data;
        var added = !_tranpositions.TryGetValue(board, out data);
        if (added)
            data = new ChessResults();
        data.Score = score;
        data.Move = move;
        data.Depth = depth;
        if (added)
            _tranpositions[board] = data;
        return data;
    }

    private static bool IsMate(float score)
    {
        return score < -MateCutoff || score > MateCutoff;
    }

    private IEnumerable<ChessMove> QuiescentMove(ChessBoard board)
    {
        var list = board.LegalMoves();

        if (!board.InCheck)
            list.RemoveAll(m =>
            {
                if ((m.Flags & ChessMove.MoveFlags.Capture) != 0
                    || m.Promotion != 0)
                    return false;

                var t = board.Move(m);
                if (board.InCheck)
                    return false;

                return true;
            });

        return list;
    }

    private static bool ObtainScore(ChessResults results, int depth, ref float score)
    {
        if (IsMate(results.Score))
        {
            score = Math.Abs(results.Score);
            score = score - depth + results.Depth;
            if (results.Score < 0)
                score = -score;
            return true;
        }
        if (results.Depth <= depth)
        {
            score = results.Score;
            return true;
        }
        return false;
    }
}

public class ChessResults
{
    public int Depth;
    public ChessMove Move;
    public float Score;
}