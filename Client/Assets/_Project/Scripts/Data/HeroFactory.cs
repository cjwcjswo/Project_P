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
    /// HeroData + SkillDataRepository로 HeroState를 생성한다.
    /// Grade에 따라 MatchSkill/UniqueSkill/UltimateSkill을 각각 조회한다.
    /// </summary>
    public static HeroState Create(HeroData heroData, SkillDataRepository skillRepo, int level, int partyIndex)
    {
        level = Math.Max(1, level);

        var matchSkill    = BuildSkill(skillRepo, heroData.MatchSkillId);
        var uniqueSkill   = heroData.UniqueSkillId   > 0 ? BuildSkill(skillRepo, heroData.UniqueSkillId)   : null;
        var ultimateSkill = heroData.UltimateSkillId > 0 ? BuildSkill(skillRepo, heroData.UltimateSkillId) : null;

        if (!Enum.TryParse<HeroClass>(heroData.Class, ignoreCase: true, out var heroClass))
        {
            Debug.LogWarning($"[HeroFactory] Unknown Class '{heroData.Class}' for hero {heroData.Id}, defaulting to None.");
            heroClass = HeroClass.None;
        }

        float attackInterval = heroData.AutoAttackInterval > 0f ? heroData.AutoAttackInterval : 1.5f;
        float attackRatio    = heroData.AutoAttackRatio    > 0f ? heroData.AutoAttackRatio    : 0.1f;

        return new HeroState(
            maxHP:               heroData.BaseStats.MaxHP,
            attack:              heroData.BaseStats.Attack,
            defense:             heroData.BaseStats.Defense,
            growthMaxHP:         heroData.GrowthStats.MaxHP,
            growthAttack:        heroData.GrowthStats.Attack,
            growthDefense:       heroData.GrowthStats.Defense,
            level:               level,
            partyIndex:          partyIndex,
            matchSkill:          matchSkill,
            uniqueSkill:         uniqueSkill,
            ultimateSkill:       ultimateSkill,
            autoAttackInterval:  attackInterval,
            autoAttackRatio:     attackRatio,
            heroClass:           heroClass,
            grade:               heroData.Grade,
            illustrationPath:    heroData.IllustrationPath ?? ""
        );
    }

    /// <summary>
    /// skillId가 0이 아닌 경우 Repository에서 조회하여 HeroSkill을 생성한다.
    /// </summary>
    private static HeroSkill BuildSkill(SkillDataRepository skillRepo, int skillId)
    {
        if (skillId <= 0) return null;

        var sd = skillRepo.GetById(skillId);
        if (sd == null)
        {
            Debug.LogWarning($"[HeroFactory] SkillData not found for skillId={skillId}.");
            return null;
        }

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
