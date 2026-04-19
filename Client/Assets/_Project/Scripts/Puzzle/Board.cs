using System;
using System.Collections.Generic;

/// <summary>
/// 7x6 퍼즐 보드의 순수 데이터 모델.
/// 좌표계: col(0~6, 좌→우), row(0~5, 하→상). row 0 = 바닥.
/// 중력 방향: row 감소 방향.
/// </summary>
public class Board
{
    private readonly BlockType[,] _grid;
    private List<BlockType> _activeColors;
    private static readonly Random _rand = new();

    // ── 특수 블록(스킬 블록) 상태 관리 ────────────────────────────────────
    private readonly HashSet<(int col, int row)> _skillBlocks = new();

    public bool IsSkillBlock(int col, int row) => _skillBlocks.Contains((col, row));

    public void SetSkillBlock(int col, int row)
    {
        _skillBlocks.Add((col, row));
    }

    public void RemoveSkillBlock(int col, int row)
    {
        _skillBlocks.Remove((col, row));
    }

    public int Width  => Constants.BOARD_WIDTH;
    public int Height => Constants.BOARD_HEIGHT;

    public Board()
    {
        _grid = new BlockType[Width, Height];
        _activeColors = new List<BlockType>
        {
            BlockType.Red, BlockType.Yellow, BlockType.Green, BlockType.Blue, BlockType.Purple
        };
    }

    public BlockType GetBlock(int col, int row) => _grid[col, row];
    public void SetBlock(int col, int row, BlockType type) => _grid[col, row] = type;

    // ── 초기화 ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 활성 색상으로 보드 초기화. 3연속 매치 없도록 랜덤 배치.
    /// </summary>
    public void Initialize(List<BlockType> activeColors)
    {
        _activeColors = activeColors;

        for (int col = 0; col < Width; col++)
        {
            for (int row = 0; row < Height; row++)
            {
                BlockType selected;
                do
                {
                    selected = _activeColors[_rand.Next(_activeColors.Count)];
                }
                while (WouldCauseMatch(col, row, selected));

                _grid[col, row] = selected;
            }
        }
    }

    /// <summary>
    /// 활성 색상 목록 갱신 (히어로 사망 시 호출).
    /// </summary>
    public void UpdateActiveColors(List<BlockType> activeColors)
    {
        _activeColors = activeColors;
    }

    /// <summary>
    /// 특정 색상의 블록을 모두 제거 (None으로 설정).
    /// 제거된 위치 목록 반환.
    /// </summary>
    public List<(int col, int row)> RemoveBlocksOfType(BlockType type)
    {
        var removed = new List<(int col, int row)>();
        for (int col = 0; col < Width; col++)
        {
            for (int row = 0; row < Height; row++)
            {
                if (_grid[col, row] == type)
                {
                    _grid[col, row] = BlockType.None;
                    removed.Add((col, row));
                }
            }
        }
        return removed;
    }

    /// <summary>
    /// 특정 색상의 블록을 Disabled(고정 장애물)로 변환.
    /// 히어로 사망 시 호출. 변환된 위치 목록 반환.
    /// </summary>
    public List<(int col, int row)> ConvertBlocksToDisabled(BlockType type)
    {
        var converted = new List<(int col, int row)>();
        for (int col = 0; col < Width; col++)
        {
            for (int row = 0; row < Height; row++)
            {
                if (_grid[col, row] == type)
                {
                    _grid[col, row] = BlockType.Disabled;
                    converted.Add((col, row));
                }
            }
        }
        return converted;
    }

    /// <summary>해당 위치에 type을 놓으면 3연속 매치가 생기는지 확인.</summary>
    private bool WouldCauseMatch(int col, int row, BlockType type)
    {
        if (col >= 2 &&
            _grid[col - 1, row] == type &&
            _grid[col - 2, row] == type)
            return true;

        if (row >= 2 &&
            _grid[col, row - 1] == type &&
            _grid[col, row - 2] == type)
            return true;

        return false;
    }

    // ── 스왑 ──────────────────────────────────────────────────────────────

    /// <summary>두 셀이 상하좌우 인접한지 확인 (대각선 불가).</summary>
    public bool IsAdjacent(int c1, int r1, int c2, int r2)
    {
        int dc = Math.Abs(c1 - c2);
        int dr = Math.Abs(r1 - r2);
        return (dc + dr) == 1;
    }

    /// <summary>두 셀의 블록을 교환.</summary>
    public void Swap(int c1, int r1, int c2, int r2)
    {
        (_grid[c1, r1], _grid[c2, r2]) = (_grid[c2, r2], _grid[c1, r1]);
    }

    // ── 클리어 / 중력 / 리필 ───────────────────────────────────────────────

    /// <summary>지정된 셀들을 None으로 설정. 특수 블록 상태도 같이 제거.</summary>
    public void ClearBlocks(List<(int col, int row)> positions)
    {
        foreach (var (col, row) in positions)
        {
            _grid[col, row] = BlockType.None;
            _skillBlocks.Remove((col, row));
        }
    }

    /// <summary>
    /// 상하좌우 십자 모양 블록을 파괴한다 (2성 특수 블록 탭 발동 시).
    /// 특수 블록 자신의 위치도 포함하여 반환.
    /// </summary>
    public List<(int col, int row)> ClearCrossPattern(int col, int row)
    {
        var positions = new List<(int col, int row)>();
        // 자신
        if (_grid[col, row] != BlockType.None && _grid[col, row] != BlockType.Disabled)
            positions.Add((col, row));
        // 상
        if (row + 1 < Height && _grid[col, row + 1] != BlockType.Disabled)
            positions.Add((col, row + 1));
        // 하
        if (row - 1 >= 0 && _grid[col, row - 1] != BlockType.Disabled)
            positions.Add((col, row - 1));
        // 좌
        if (col - 1 >= 0 && _grid[col - 1, row] != BlockType.Disabled)
            positions.Add((col - 1, row));
        // 우
        if (col + 1 < Width && _grid[col + 1, row] != BlockType.Disabled)
            positions.Add((col + 1, row));

        ClearBlocks(positions);
        return positions;
    }

    /// <summary>
    /// 열 단위로 블록을 아래로 낙하 처리.
    /// Disabled 블록은 고정 장애물로 취급: 제자리 유지, 각 구간 내에서만 압축.
    /// 반환값: 이동한 블록들의 정보 (뷰 레이어 애니메이션용).
    /// </summary>
    public List<BlockMove> ApplyGravity()
    {
        var moves = new List<BlockMove>();

        for (int col = 0; col < Width; col++)
        {
            int segStart = 0;
            while (segStart < Height)
            {
                // Disabled 블록을 만나면 건너뛰기
                if (_grid[col, segStart] == BlockType.Disabled)
                {
                    segStart++;
                    continue;
                }

                // 현재 구간의 끝을 찾기 (Disabled 또는 열 끝까지)
                int segEnd = segStart;
                while (segEnd < Height && _grid[col, segEnd] != BlockType.Disabled)
                    segEnd++;

                // 구간 내에서 non-None 블록을 아래로 압축
                int writeRow = segStart;
                for (int readRow = segStart; readRow < segEnd; readRow++)
                {
                    if (_grid[col, readRow] != BlockType.None)
                    {
                        if (readRow != writeRow)
                        {
                            moves.Add(new BlockMove
                            {
                                Col     = col,
                                FromRow = readRow,
                                ToRow   = writeRow,
                                Type    = _grid[col, readRow],
                                IsNew   = false
                            });
                            _grid[col, writeRow] = _grid[col, readRow];
                            _grid[col, readRow]  = BlockType.None;

                            // 특수 블록 위치도 같이 이동
                            if (_skillBlocks.Remove((col, readRow)))
                                _skillBlocks.Add((col, writeRow));
                        }
                        writeRow++;
                    }
                }

                segStart = segEnd + 1;
            }
        }
        return moves;
    }

    /// <summary>
    /// Disabled·None 블록을 제외한 모든 활성 블록을 활성 색상으로 재무작위 배치.
    /// WouldCauseMatch를 통해 즉각적인 3연속 매치는 방지한다.
    /// 반환값: 타입이 변경된 셀 위치 목록 (뷰 레이어 갱신용).
    /// </summary>
    public List<(int col, int row)> Shuffle()
    {
        var changed = new List<(int col, int row)>();

        for (int col = 0; col < Width; col++)
        {
            for (int row = 0; row < Height; row++)
            {
                if (_grid[col, row] == BlockType.Disabled || _grid[col, row] == BlockType.None)
                    continue;

                BlockType selected;
                int attempts = 0;
                do
                {
                    selected = _activeColors[_rand.Next(_activeColors.Count)];
                    attempts++;
                }
                while (WouldCauseMatch(col, row, selected) && attempts < 100);

                _grid[col, row] = selected;
                changed.Add((col, row));
            }
        }

        return changed;
    }

    /// <summary>
    /// 빈 셀을 활성 색상의 랜덤 블록으로 채움.
    /// FromRow = Height + offset 으로 화면 위에서 낙하하는 연출 지원.
    /// 반환값: 새로 스폰된 블록들의 정보 (뷰 레이어 애니메이션용).
    /// </summary>
    public List<BlockMove> Refill()
    {
        var moves = new List<BlockMove>();

        for (int col = 0; col < Width; col++)
        {
            int spawnOffset = 0;
            for (int row = 0; row < Height; row++)
            {
                if (_grid[col, row] == BlockType.None)
                {
                    var type = _activeColors[_rand.Next(_activeColors.Count)];
                    _grid[col, row] = type;
                    moves.Add(new BlockMove
                    {
                        Col     = col,
                        FromRow = Height + spawnOffset,
                        ToRow   = row,
                        Type    = type,
                        IsNew   = true
                    });
                    spawnOffset++;
                }
            }
        }
        return moves;
    }
}

/// <summary>블록 이동 정보 (뷰 레이어에서 애니메이션 처리에 사용).</summary>
public struct BlockMove
{
    public int Col;
    public int FromRow;
    public int ToRow;
    public BlockType Type;
    public bool IsNew;
}
