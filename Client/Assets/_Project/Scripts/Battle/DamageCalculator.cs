/// <summary>
/// 히어로 스킬과 블록 매칭 수치로부터 스킬 효과 값을 계산.
/// GDD BaseMultiplier 기반: finalValue = blockCount * BaseMultiplier * comboMultiplier (+ heroAttack * 0.5 for Attack)
/// </summary>
public static class DamageCalculator
{
    public static SkillEffect Calculate(HeroSkill skill, BlockType sourceColor,
                                        int totalBlockCount, float comboMultiplier, int heroAttack)
    {
        float finalValue = totalBlockCount * skill.BaseMultiplier * comboMultiplier;

        if (skill.ActionType == ActionType.Attack)
            finalValue += heroAttack * 0.5f;

        return new SkillEffect
        {
            SourceColor  = sourceColor,
            ActionType   = skill.ActionType,
            TargetScope  = skill.TargetScope,
            MaxTargetCount = skill.MaxTargetCount,
            Value        = (int)finalValue,
            Skill        = skill,
            SkillName    = skill.Name
        };
    }
}

public struct SkillEffect
{
    /// <summary>발동 히어로의 블록 색상 (뷰 이펙트 식별용)</summary>
    public BlockType SourceColor;
    public ActionType ActionType;
    public TargetScope TargetScope;
    public int MaxTargetCount;
    public int Value;
    /// <summary>StatusEffects 접근 등 스킬 원본 참조</summary>
    public HeroSkill Skill;
    /// <summary>플로팅 텍스트에 표시할 스킬 이름</summary>
    public string SkillName;
}
