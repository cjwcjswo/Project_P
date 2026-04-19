/// <summary>
/// 전투 완료(클리어 또는 패배) 시 EventBus를 통해 발행되는 이벤트.
/// EventBus는 struct 타입만 지원하므로 struct로 정의.
/// </summary>
public struct BattleCompleteEvent
{
    public BattleResult Result;
}
