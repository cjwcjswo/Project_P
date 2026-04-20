/// <summary>
/// 스킬 액션과 블록 매칭 수치로부터 스킬 효과 값을 계산.
/// GDD BaseMultiplier 기반: finalValue = blockCount * BaseMultiplier * comboMultiplier (+ heroAttack * 0.5 for Attack)
/// </summary>
public static class DamageCalculator
{
    /// <summary>
    /// GDD v1.0 — SkillAction 단위 계산.
    /// </summary>
    public static SkillEffect Calculate(SkillAction action, BlockType sourceColor,
                                        int totalBlockCount, float comboMultiplier, int heroAttack,
                                        string skillName = "")
    {
        float finalValue = totalBlockCount * action.BaseMultiplier * comboMultiplier;

        if (action.ActionType == ActionType.Attack)
            finalValue += heroAttack * 0.5f;

        return new SkillEffect
        {
            SourceColor   = sourceColor,
            ActionType    = action.ActionType,
            Team          = action.Team,
            Strategy      = action.Strategy,
            MaxCount      = action.MaxCount,
            TargetIndex   = action.TargetIndex,
            Value         = (int)finalValue,
            SourceAction  = action,
            SkillName     = skillName
        };
    }

    /// <summary>
    /// 하위 호환 브릿지 — HeroSkill.Actions[0] 기준으로 위임.
    /// </summary>
    public static SkillEffect Calculate(HeroSkill skill, BlockType sourceColor,
                                        int totalBlockCount, float comboMultiplier, int heroAttack)
    {
        if (skill.Actions.Count == 0)
        {
            return new SkillEffect
            {
                SourceColor = sourceColor,
                ActionType  = ActionType.Attack,
                Team        = TargetTeam.Enemy,
                Strategy    = TargetStrategy.Front,
                MaxCount    = 1,
                Value       = 0,
                SkillName   = skill.Name
            };
        }

        return Calculate(skill.Actions[0], sourceColor, totalBlockCount, comboMultiplier, heroAttack, skill.Name);
    }
}

public struct SkillEffect
{
    /// <summary>발동 히어로의 블록 색상 (뷰 이펙트 식별용)</summary>
    public BlockType SourceColor;
    public ActionType ActionType;
    public TargetTeam Team;
    public TargetStrategy Strategy;
    public int MaxCount;
    public int TargetIndex;
    public int Value;
    /// <summary>StatusEffects 접근 등 액션 원본 참조</summary>
    public SkillAction SourceAction;
    /// <summary>플로팅 텍스트에 표시할 스킬 이름</summary>
    public string SkillName;
}
