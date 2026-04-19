using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;

/// <summary>
/// Board 데이터 모델과 Matcher를 조합하여 스왑 → 캐스케이드 루프를 실행.
/// 순수 C# 클래스 — MonoBehaviour 없음.
/// </summary>
public class BoardController
{
    private readonly Board _board;
    private readonly Matcher _matcher;
    private List<BlockType> _activeColors;

    public Board Board => _board;

    public BoardController(Board board, List<BlockType> activeColors)
    {
        _board = board;
        _matcher = new Matcher();
        _activeColors = activeColors;
    }

    /// <summary>
    /// 유저 스왑 요청 처리.
    /// 매치 없으면 원위치 후 Invalid 반환.
    /// 매치 있으면 캐스케이드 루프 실행 후 결과 반환.
    /// </summary>
    public async UniTask<CascadeResult> TrySwapAsync(int c1, int r1, int c2, int r2)
    {
        if (!_board.IsAdjacent(c1, r1, c2, r2))
            return CascadeResult.Invalid;

        // Disabled(비활성 장애물) 블록은 스왑 불가
        if (_board.GetBlock(c1, r1) == BlockType.Disabled ||
            _board.GetBlock(c2, r2) == BlockType.Disabled)
            return CascadeResult.Invalid;

        _board.Swap(c1, r1, c2, r2);
        EventBus.Publish(new SwapEvent { Col1 = c1, Row1 = r1, Col2 = c2, Row2 = r2, IsValid = true });
        await UniTask.Delay(TimeSpan.FromSeconds(Constants.SWAP_ANIM_DURATION));

        var matches = _matcher.FindMatches(_board);
        if (matches.Count == 0)
        {
            _board.Swap(c1, r1, c2, r2);
            EventBus.Publish(new SwapEvent { Col1 = c1, Row1 = r1, Col2 = c2, Row2 = r2, IsValid = false });
            await UniTask.Delay(TimeSpan.FromSeconds(Constants.SWAP_ANIM_DURATION));
            return CascadeResult.Invalid;
        }

        var allMatches = new List<MatchResult>();
        int combo = 0;
        int totalBlocks = 0;
        var colorData = new Dictionary<BlockType, ColorMatchData>();

        while (matches.Count > 0)
        {
            combo++;
            allMatches.AddRange(matches);

            var clearPositions = matches
                .SelectMany(m => m.Positions)
                .Distinct()
                .ToList();

            int blocksInStep = clearPositions.Count;
            totalBlocks += blocksInStep;

            // 이 스텝의 색상 데이터를 별도 수집 → 즉시 스킬 발동 이벤트
            var stepColorData = BuildStepColorData(matches, combo);
            EventBus.Publish(new MatchStepSkillTriggerEvent
            {
                ColorBreakdown = new List<ColorMatchData>(stepColorData.Values),
                ComboStep = combo
            });

            // CascadeCompleteEvent용 누적 데이터 갱신
            AccumulateColorData(matches, combo, colorData);

            EventBus.Publish(new MatchFoundEvent
            {
                Matches = matches,
                ComboStep = combo,
                BlocksMatchedInStep = blocksInStep
            });

            _board.ClearBlocks(clearPositions);
            await UniTask.Delay(TimeSpan.FromSeconds(Constants.DESTROY_ANIM_DURATION));

            var gravityMoves = _board.ApplyGravity();
            var refillMoves  = _board.Refill();
            EventBus.Publish(new GravityRefillEvent
            {
                GravityMoves = gravityMoves,
                RefillMoves  = refillMoves
            });
            await UniTask.Delay(TimeSpan.FromSeconds(Constants.FALL_ANIM_DURATION));

            matches = _matcher.FindMatches(_board);
        }

        EventBus.Publish(new CascadeCompleteEvent
        {
            TotalCombo = combo,
            TotalBlocksMatched = totalBlocks,
            AllMatches = allMatches,
            ColorBreakdown = new List<ColorMatchData>(colorData.Values)
        });

        await ResolveDeadlockAsync();
        EventBus.Publish(new BoardStabilizedEvent());

        return new CascadeResult
        {
            IsValid    = true,
            Combo      = combo,
            AllMatches = allMatches
        };
    }

    /// <summary>
    /// 히어로 사망 시 호출: 해당 색상 블록을 Disabled(고정 장애물)로 전환 → 중력 → 리필 → 캐스케이드
    /// </summary>
    public async UniTask DisableColorAsync(BlockType deadHeroColor)
    {
        _activeColors.Remove(deadHeroColor);
        _board.UpdateActiveColors(_activeColors);

        var disabled = _board.ConvertBlocksToDisabled(deadHeroColor);
        if (disabled.Count == 0) return;

        EventBus.Publish(new HeroColorDisabledEvent { OriginalColor = deadHeroColor, Positions = disabled });
        await UniTask.Delay(TimeSpan.FromSeconds(Constants.DESTROY_ANIM_DURATION));

        await ProcessCascadeAsync();
    }

    /// <summary>
    /// 궁극기 등 외부 블록 파괴 후 중력+리필+연쇄를 독립 실행.
    /// CascadeCompleteEvent를 발행한다.
    /// </summary>
    public async UniTask ProcessCascadeAsync()
    {
        var gravityMoves = _board.ApplyGravity();
        var refillMoves  = _board.Refill();
        EventBus.Publish(new GravityRefillEvent
        {
            GravityMoves = gravityMoves,
            RefillMoves  = refillMoves
        });
        await UniTask.Delay(TimeSpan.FromSeconds(Constants.FALL_ANIM_DURATION));

        var matches = _matcher.FindMatches(_board);
        if (matches.Count == 0) return;

        int combo = 0;
        int totalBlocks = 0;
        var allMatches = new List<MatchResult>();
        var colorData = new Dictionary<BlockType, ColorMatchData>();

        while (matches.Count > 0)
        {
            combo++;
            allMatches.AddRange(matches);

            var clearPositions = matches
                .SelectMany(m => m.Positions)
                .Distinct()
                .ToList();

            int blocksInStep = clearPositions.Count;
            totalBlocks += blocksInStep;

            // 이 스텝의 색상 데이터를 별도 수집 → 즉시 스킬 발동 이벤트
            var stepColorData = BuildStepColorData(matches, combo);
            EventBus.Publish(new MatchStepSkillTriggerEvent
            {
                ColorBreakdown = new List<ColorMatchData>(stepColorData.Values),
                ComboStep = combo
            });

            // CascadeCompleteEvent용 누적 데이터 갱신
            AccumulateColorData(matches, combo, colorData);

            EventBus.Publish(new MatchFoundEvent
            {
                Matches = matches,
                ComboStep = combo,
                BlocksMatchedInStep = blocksInStep
            });

            _board.ClearBlocks(clearPositions);
            await UniTask.Delay(TimeSpan.FromSeconds(Constants.DESTROY_ANIM_DURATION));

            gravityMoves = _board.ApplyGravity();
            refillMoves  = _board.Refill();
            EventBus.Publish(new GravityRefillEvent
            {
                GravityMoves = gravityMoves,
                RefillMoves  = refillMoves
            });
            await UniTask.Delay(TimeSpan.FromSeconds(Constants.FALL_ANIM_DURATION));

            matches = _matcher.FindMatches(_board);
        }

        EventBus.Publish(new CascadeCompleteEvent
        {
            TotalCombo = combo,
            TotalBlocksMatched = totalBlocks,
            AllMatches = allMatches,
            ColorBreakdown = new List<ColorMatchData>(colorData.Values)
        });

        await ResolveDeadlockAsync();
        EventBus.Publish(new BoardStabilizedEvent());
    }

    /// <summary>
    /// 유효한 스왑이 없는 교착 상태를 감지하고, 해소될 때까지 보드를 재배치한다.
    /// Disabled 블록은 제외하고 활성 블록만 재무작위 배치하며 BoardReshuffleEvent를 발행한다.
    /// </summary>
    private async UniTask ResolveDeadlockAsync()
    {
        const int maxAttempts = 10;
        int attempt = 0;
        while (!_matcher.HasValidMoves(_board) && attempt++ < maxAttempts)
        {
            var updated = _board.Shuffle();
            EventBus.Publish(new BoardReshuffleEvent { UpdatedPositions = updated });
            await UniTask.Delay(TimeSpan.FromSeconds(Constants.SWAP_ANIM_DURATION));
        }
    }

    /// <summary>
    /// 한 스텝의 매치 결과에서 색상별 ColorMatchData를 새 딕셔너리로 반환.
    /// MatchStepSkillTriggerEvent에 사용하기 위해 누적 colorData와 분리.
    /// </summary>
    private static Dictionary<BlockType, ColorMatchData> BuildStepColorData(
        List<MatchResult> matches, int combo)
    {
        var stepData = new Dictionary<BlockType, ColorMatchData>();
        foreach (var match in matches)
        {
            if (!stepData.ContainsKey(match.Type))
            {
                stepData[match.Type] = new ColorMatchData
                {
                    Color = match.Type,
                    BlockCount = match.Positions.Count,
                    ComboAtTrigger = combo
                };
            }
            else
            {
                var existing = stepData[match.Type];
                existing.BlockCount += match.Positions.Count;
                stepData[match.Type] = existing;
            }
        }
        return stepData;
    }

    private static void AccumulateColorData(List<MatchResult> matches, int combo,
                                            Dictionary<BlockType, ColorMatchData> colorData)
    {
        foreach (var match in matches)
        {
            if (!colorData.ContainsKey(match.Type))
            {
                colorData[match.Type] = new ColorMatchData
                {
                    Color = match.Type,
                    BlockCount = match.Positions.Count,
                    ComboAtTrigger = combo
                };
            }
            else
            {
                var existing = colorData[match.Type];
                existing.BlockCount += match.Positions.Count;
                colorData[match.Type] = existing;
            }
        }
    }
}
