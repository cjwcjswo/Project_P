using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// HeroDataRepository + SkillDataRepository 기반으로 HeroState를 생성하는 팩토리.
/// 히어로 상세 스탯은 항상 HeroData.json(Repository)에서 읽는다.
/// </summary>
public static class HeroFactory
{
    /// <summary>
    /// HeroData + SkillData로 HeroState를 생성한다. GrowthStats를 레벨에 반영.
    /// </summary>
    public static HeroState Create(HeroData heroData, SkillData skillData, int level, int partyIndex)
    {
        level = Math.Max(1, level);
        var skill = BuildSkill(skillData);

        float attackInterval = heroData.AutoAttackInterval > 0f ? heroData.AutoAttackInterval : 1.5f;
        float attackRatio    = heroData.AutoAttackRatio    > 0f ? heroData.AutoAttackRatio    : 0.1f;

        return new HeroState(
            maxHP:          heroData.BaseStats.MaxHP,
            attack:         heroData.BaseStats.Attack,
            defense:        heroData.BaseStats.Defense,
            growthMaxHP:    heroData.GrowthStats.MaxHP,
            growthAttack:   heroData.GrowthStats.Attack,
            growthDefense:  heroData.GrowthStats.Defense,
            level:          level,
            partyIndex:     partyIndex,
            skill:          skill,
            autoAttackInterval: attackInterval,
            autoAttackRatio:    attackRatio
        );
    }

    private static HeroSkill BuildSkill(SkillData sd)
    {
        if (sd == null) return null;

        if (!Enum.TryParse<ActionType>(sd.ActionType, ignoreCase: true, out var actionType))
        {
            Debug.LogWarning($"[HeroFactory] Unknown ActionType '{sd.ActionType}', defaulting to Attack.");
            actionType = ActionType.Attack;
        }

        if (!Enum.TryParse<TargetScope>(sd.TargetScope, ignoreCase: true, out var targetScope))
        {
            Debug.LogWarning($"[HeroFactory] Unknown TargetScope '{sd.TargetScope}', defaulting to Single.");
            targetScope = TargetScope.Single;
        }

        return new HeroSkill(
            actionType:     actionType,
            targetScope:    targetScope,
            baseMultiplier: sd.BaseMultiplier,
            maxTargetCount: sd.MaxTargetCount,
            name:           sd.DisplayName,
            statusEffects:  sd.StatusEffects ?? new List<StatusEffectData>()
        );
    }
}
