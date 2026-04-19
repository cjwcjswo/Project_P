using System.Collections.Generic;
using System.Linq;

/// <summary>보드 전체를 스캔하여 3개 이상 연속된 동일 블록 그룹을 탐지.</summary>
public class Matcher
{
    /// <summary>보드 전체에서 모든 매치 그룹을 찾아 반환.</summary>
    public List<MatchResult> FindMatches(Board board)
    {
        // 가로/세로 스캔 결과를 HashSet 목록으로 수집
        var rawMatches = new List<HashSet<(int col, int row)>>();

        ScanDirection(board, rawMatches, horizontal: true);
        ScanDirection(board, rawMatches, horizontal: false);

        return MergeOverlapping(board, rawMatches);
    }

    /// <summary>
    /// 현재 보드에 유효한 스왑(스왑 후 매치가 발생하는 이동)이 하나라도 존재하는지 검사.
    /// Disabled·None 셀은 스왑 대상에서 제외한다.
    /// </summary>
    public bool HasValidMoves(Board board)
    {
        for (int col = 0; col < board.Width; col++)
        {
            for (int row = 0; row < board.Height; row++)
            {
                var type = board.GetBlock(col, row);
                if (type == BlockType.None || type == BlockType.Disabled) continue;

                // 오른쪽 인접 셀과 스왑
                if (col + 1 < board.Width && WouldSwapCauseMatch(board, col, row, col + 1, row))
                    return true;

                // 위쪽 인접 셀과 스왑
                if (row + 1 < board.Height && WouldSwapCauseMatch(board, col, row, col, row + 1))
                    return true;
            }
        }
        return false;
    }

    /// <summary>두 셀을 임시로 스왑했을 때 매치가 발생하는지 시뮬레이션.</summary>
    private bool WouldSwapCauseMatch(Board board, int c1, int r1, int c2, int r2)
    {
        var target = board.GetBlock(c2, r2);
        if (target == BlockType.None || target == BlockType.Disabled) return false;

        board.Swap(c1, r1, c2, r2);
        bool hasMatch = FindMatchesAround(board, c1, r1).Count > 0
                     || FindMatchesAround(board, c2, r2).Count > 0;
        board.Swap(c1, r1, c2, r2);
        return hasMatch;
    }

    /// <summary>스왑 후 해당 셀 주변만 검사 (성능 최적화용).</summary>
    public List<MatchResult> FindMatchesAround(Board board, int col, int row)
    {
        var rawMatches = new List<HashSet<(int, int)>>();

        ScanDirectionAround(board, rawMatches, col, row, horizontal: true);
        ScanDirectionAround(board, rawMatches, col, row, horizontal: false);

        return MergeOverlapping(board, rawMatches);
    }

    // ── 내부 스캔 로직 ─────────────────────────────────────────────────────

    /// <summary>매칭에 참여할 수 없는 블록 타입 (빈 칸, 비활성 장애물).</summary>
    private static bool IsUnmatchable(BlockType type) =>
        type == BlockType.None || type == BlockType.Disabled;

    private void ScanDirection(Board board, List<HashSet<(int, int)>> results, bool horizontal)
    {
        int outer = horizontal ? board.Height : board.Width;
        int inner = horizontal ? board.Width  : board.Height;

        for (int o = 0; o < outer; o++)
        {
            var streak = new List<(int col, int row)>();
            BlockType streakType = BlockType.None;

            for (int i = 0; i < inner; i++)
            {
                int col = horizontal ? i : o;
                int row = horizontal ? o : i;
                var type = board.GetBlock(col, row);

                if (!IsUnmatchable(type) && type == streakType)
                {
                    streak.Add((col, row));
                }
                else
                {
                    FlushStreak(streak, results);
                    streak.Clear();
                    if (!IsUnmatchable(type))
                        streak.Add((col, row));
                    streakType = type;
                }
            }
            FlushStreak(streak, results);
        }
    }

    private void ScanDirectionAround(Board board, List<HashSet<(int, int)>> results, int targetCol, int targetRow, bool horizontal)
    {
        // 해당 행/열 전체를 스캔
        if (horizontal)
            ScanSingleLine(board, results, targetRow, horizontal: true);
        else
            ScanSingleLine(board, results, targetCol, horizontal: false);
    }

    private void ScanSingleLine(Board board, List<HashSet<(int, int)>> results, int index, bool horizontal)
    {
        int length = horizontal ? board.Width : board.Height;
        var streak = new List<(int, int)>();
        BlockType streakType = BlockType.None;

        for (int i = 0; i < length; i++)
        {
            int col = horizontal ? i     : index;
            int row = horizontal ? index : i;
            var type = board.GetBlock(col, row);

            if (!IsUnmatchable(type) && type == streakType)
            {
                streak.Add((col, row));
            }
            else
            {
                FlushStreak(streak, results);
                streak.Clear();
                if (!IsUnmatchable(type))
                    streak.Add((col, row));
                streakType = type;
            }
        }
        FlushStreak(streak, results);
    }

    private static void FlushStreak(List<(int, int)> streak, List<HashSet<(int, int)>> results)
    {
        if (streak.Count >= Constants.MIN_MATCH)
            results.Add(new HashSet<(int, int)>(streak));
    }

    // ── 병합: 같은 BlockType이고 셀을 공유하는 매치를 하나로 합침 ─────────

    private List<MatchResult> MergeOverlapping(Board board, List<HashSet<(int, int)>> rawMatches)
    {
        // Union-Find 방식으로 겹치는 그룹 병합
        int count = rawMatches.Count;
        int[] parent = new int[count];
        for (int i = 0; i < count; i++) parent[i] = i;

        int Find(int x) { while (parent[x] != x) { parent[x] = parent[parent[x]]; x = parent[x]; } return x; }
        void Union(int a, int b) { parent[Find(a)] = Find(b); }

        for (int i = 0; i < count; i++)
        {
            for (int j = i + 1; j < count; j++)
            {
                if (Find(i) == Find(j)) continue;

                // 같은 타입이고 교집합이 있으면 병합
                var typeI = board.GetBlock(rawMatches[i].First().Item1, rawMatches[i].First().Item2);
                var typeJ = board.GetBlock(rawMatches[j].First().Item1, rawMatches[j].First().Item2);
                if (typeI == typeJ && rawMatches[i].Overlaps(rawMatches[j]))
                    Union(i, j);
            }
        }

        // 같은 root끼리 모아서 MatchResult 생성
        var groups = new Dictionary<int, (BlockType type, HashSet<(int, int)> cells)>();
        for (int i = 0; i < count; i++)
        {
            int root = Find(i);
            var type = board.GetBlock(rawMatches[i].First().Item1, rawMatches[i].First().Item2);

            if (!groups.ContainsKey(root))
                groups[root] = (type, new HashSet<(int, int)>());

            foreach (var cell in rawMatches[i])
                groups[root].cells.Add(cell);
        }

        var results = new List<MatchResult>();
        foreach (var (_, (type, cells)) in groups)
        {
            results.Add(new MatchResult
            {
                Type      = type,
                Positions = new List<(int col, int row)>(cells)
            });
        }
        return results;
    }
}

/// <summary>하나의 매치 그룹 결과.</summary>
public struct MatchResult
{
    public BlockType Type;
    public List<(int col, int row)> Positions;
}
