using System.Collections.Generic;

public struct SwapEvent
{
    public int Col1, Row1, Col2, Row2;
    public bool IsValid;
}

public struct MatchFoundEvent
{
    public List<MatchResult> Matches;
    public int ComboStep;
    public BlockType PrimaryColor;  // 뷰 이펙트용 (하위 호환 유지)
    public int BlocksMatchedInStep;
}

public struct GravityRefillEvent
{
    public List<BlockMove> GravityMoves;
    public List<BlockMove> RefillMoves;
}

/// <summary>
/// 색상별 매치 데이터. 캐스케이드 내에서 특정 색상의 총 매치 정보.
/// </summary>
public struct ColorMatchData
{
    public BlockType Color;
    public int BlockCount;      // 해당 색상으로 매칭된 총 블록 수
    public int ComboAtTrigger;  // 해당 색상이 처음 매칭된 시점의 콤보 카운트
}

/// <summary>
/// 캐스케이드 루프의 각 스텝마다 발행. BattleManager가 즉시 스킬/콤보/궁극기를 처리한다.
/// </summary>
public struct MatchStepSkillTriggerEvent
{
    public List<ColorMatchData> ColorBreakdown;  // 이 스텝에서 매칭된 색상 데이터만 포함
    public int ComboStep;
}

public struct CascadeCompleteEvent
{
    public int TotalCombo;
    public int TotalBlocksMatched;
    public List<MatchResult> AllMatches;
    public List<ColorMatchData> ColorBreakdown;  // 색상별 분류
}

public struct BoardStabilizedEvent { }

public struct HeroColorRemovedEvent
{
    public BlockType Color;
    public List<(int col, int row)> Positions;
}

/// <summary>
/// 히어로 사망 시 해당 색상 블록이 Disabled(회색 장애물)로 전환될 때 발행.
/// PuzzleBoardView가 구독하여 BlockView를 회색으로 전환한다.
/// </summary>
public struct HeroColorDisabledEvent
{
    public BlockType OriginalColor;
    public List<(int col, int row)> Positions;
}

/// <summary>
/// 유효한 스왑이 없는 교착 상태가 감지되어 보드를 재배치했을 때 발행.
/// PuzzleBoardView가 구독하여 변경된 셀의 BlockView를 갱신하고 애니메이션을 재생한다.
/// </summary>
public struct BoardReshuffleEvent
{
    public List<(int col, int row)> UpdatedPositions;
}
