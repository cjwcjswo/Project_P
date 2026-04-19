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
/// 하위 호환용 — HeroDataDTO.skillType 파싱 및 DefaultSkill 매핑에 사용.
/// </summary>
public enum SkillType
{
    SingleAttack,
    AoEAttack,
    Heal,
    Shield,
    EnhancedAttack
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

    /// <summary>
    /// 하위 호환 생성자 — 기존 SkillType 기반 코드(DefaultSkill, HeroFactory legacy)에서 사용.
    /// </summary>
    public HeroSkill(SkillType legacyType, float baseValuePerBlock, string name, float stunDuration = 0f)
    {
        Name = name;
        BaseMultiplier = baseValuePerBlock;
        StatusEffects = new List<StatusEffectData>();

        switch (legacyType)
        {
            case SkillType.SingleAttack:
            case SkillType.EnhancedAttack:
                ActionType = ActionType.Attack;
                TargetScope = TargetScope.Single;
                MaxTargetCount = 1;
                break;
            case SkillType.AoEAttack:
                ActionType = ActionType.Attack;
                TargetScope = TargetScope.Multi;
                MaxTargetCount = 99;
                if (stunDuration > 0f)
                    StatusEffects.Add(new StatusEffectData
                    {
                        EffectType = "Stun",
                        Value = 0f,
                        Duration = stunDuration,
                        Probability = 1.0f
                    });
                break;
            case SkillType.Heal:
                ActionType = ActionType.Heal;
                TargetScope = TargetScope.Single;
                MaxTargetCount = 1;
                break;
            case SkillType.Shield:
                ActionType = ActionType.Shield;
                TargetScope = TargetScope.Single;
                MaxTargetCount = 1;
                break;
            default:
                ActionType = ActionType.Attack;
                TargetScope = TargetScope.Single;
                MaxTargetCount = 1;
                break;
        }
    }
}
