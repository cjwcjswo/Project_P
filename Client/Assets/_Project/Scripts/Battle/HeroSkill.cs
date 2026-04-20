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
/// GDD v1.0 타겟 피아 식별.
/// </summary>
public enum TargetTeam
{
    Ally,
    Enemy
}

/// <summary>
/// GDD v1.0 타겟팅 알고리즘 8종.
/// </summary>
public enum TargetStrategy
{
    Front,
    Back,
    LowestHP,
    HighestAtk,
    Self,
    FixedIndex,
    All,
    Random
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
/// 스킬 내 개별 행동 단위. Actions[] 배열의 원소.
/// </summary>
public class SkillAction
{
    public ActionType ActionType { get; }
    public TargetTeam Team { get; }
    public TargetStrategy Strategy { get; }
    public int MaxCount { get; }
    public int TargetIndex { get; }
    public float BaseMultiplier { get; }
    public List<StatusEffectData> StatusEffects { get; }

    public SkillAction(ActionType actionType, TargetTeam team, TargetStrategy strategy,
                       int maxCount, int targetIndex, float baseMultiplier,
                       List<StatusEffectData> statusEffects = null)
    {
        ActionType = actionType;
        Team = team;
        Strategy = strategy;
        MaxCount = maxCount;
        TargetIndex = targetIndex;
        BaseMultiplier = baseMultiplier;
        StatusEffects = statusEffects ?? new List<StatusEffectData>();
    }
}

/// <summary>
/// 히어로 고유 스킬 정의. Actions 배열 기반 모듈형 구조.
/// </summary>
public class HeroSkill
{
    public string Name { get; }
    public List<SkillAction> Actions { get; }

    /// <summary>하위 호환 프로퍼티 — Actions[0] 기준 ActionType 반환.</summary>
    public ActionType ActionType => Actions.Count > 0 ? Actions[0].ActionType : ActionType.Attack;

    public HeroSkill(string name, List<SkillAction> actions)
    {
        Name = name;
        Actions = actions ?? new List<SkillAction>();
    }
}
