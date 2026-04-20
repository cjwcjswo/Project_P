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
            prefabPath:          heroData.PrefabPath ?? "",
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
    /// GDD v1.0 모듈형 스키마로 HeroSkill을 생성한다.
    /// SkillData.Actions 배열을 순회하여 SkillAction 리스트를 구성한다.
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

        if (sd.Actions == null || sd.Actions.Count == 0)
        {
            Debug.LogWarning($"[HeroFactory] Skill id={skillId} has no Actions defined.");
            return new HeroSkill(sd.DisplayName, new List<SkillAction>());
        }

        var actions = new List<SkillAction>(sd.Actions.Count);
        foreach (var ad in sd.Actions)
        {
            if (!Enum.TryParse<ActionType>(ad.ActionType, ignoreCase: true, out var actionType))
            {
                Debug.LogWarning($"[HeroFactory] Skill id={skillId}: Unknown ActionType '{ad.ActionType}', defaulting to Attack.");
                actionType = ActionType.Attack;
            }

            var target = ad.Target ?? new TargetData();

            if (!Enum.TryParse<TargetTeam>(target.Team, ignoreCase: true, out var team))
            {
                Debug.LogWarning($"[HeroFactory] Skill id={skillId}: Unknown TargetTeam '{target.Team}', defaulting to Enemy.");
                team = TargetTeam.Enemy;
            }

            if (!Enum.TryParse<TargetStrategy>(target.Strategy, ignoreCase: true, out var strategy))
            {
                Debug.LogWarning($"[HeroFactory] Skill id={skillId}: Unknown TargetStrategy '{target.Strategy}', defaulting to Front.");
                strategy = TargetStrategy.Front;
            }

            actions.Add(new SkillAction(
                actionType:    actionType,
                team:          team,
                strategy:      strategy,
                maxCount:      target.MaxCount,
                targetIndex:   target.TargetIndex,
                baseMultiplier: ad.BaseMultiplier,
                statusEffects:  ad.StatusEffects ?? new List<StatusEffectData>()
            ));
        }

        return new HeroSkill(sd.DisplayName, actions);
    }
}
