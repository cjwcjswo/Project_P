using System.Collections.Generic;

/// <summary>
/// GDD 3.1 기준 스킬의 주 목적.
/// </summary>
public enum ActionType
{
    Attack,
    Heal,
    Shield,
    Buff
}

/// <summary>
/// GDD 3.1 기준 스킬 적용 범위.
/// </summary>
public enum TargetScope
{
    Single,
    Multi,
    All
}

/// <summary>
/// GDD 1절 직업군. HeroData.json Class 필드와 대응.
/// </summary>
public enum HeroClass
{
    None,
    Warrior,
    Mage,
    Assassin,
    Healer
}

/// <summary>
/// 히어로 고유 스킬 정의. SkillData.json 스키마에 대응.
/// </summary>
public class HeroSkill
{
    public ActionType ActionType { get; }
    public TargetScope TargetScope { get; }
    public float BaseMultiplier { get; }
    public int MaxTargetCount { get; }
    public string Name { get; }
    public List<StatusEffectData> StatusEffects { get; }

    public HeroSkill(ActionType actionType, TargetScope targetScope,
                     float baseMultiplier, int maxTargetCount,
                     string name,
                     List<StatusEffectData> statusEffects = null)
    {
        ActionType = actionType;
        TargetScope = targetScope;
        BaseMultiplier = baseMultiplier;
        MaxTargetCount = maxTargetCount;
        Name = name;
        StatusEffects = statusEffects ?? new List<StatusEffectData>();
    }
}
