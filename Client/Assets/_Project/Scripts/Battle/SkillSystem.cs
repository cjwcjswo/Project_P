using System;
using System.Collections.Generic;

public class SkillSystem
{
    private readonly HeroParty _party;
    private EnemyWave _enemies;
    private readonly TargetingSystem _targeting;

    public event Action<SkillEffect> OnSkillExecuted;

    public void SetWave(EnemyWave wave) => _enemies = wave;

    public SkillSystem(HeroParty party, EnemyWave enemies, TargetingSystem targeting)
    {
        _party = party;
        _enemies = enemies;
        _targeting = targeting;
    }

    // ── 공개 진입점 ──────────────────────────────────────────────────────────

    /// <summary>
    /// 2성 특수 블록 탭 시 해당 히어로의 UniqueSkill을 즉시 발동.
    /// </summary>
    public void ExecuteUniqueSkill(HeroState hero)
    {
        if (hero == null || hero.UniqueSkill == null) return;
        ExecuteSkillActions(hero.UniqueSkill, hero, hero.MappedColor, blockCount: 1, comboMultiplier: 1f);
    }

    /// <summary>
    /// 3성 궁극기 발동 (UltimateSkill 기반).
    /// </summary>
    public void ExecuteUltimateSkill(HeroState hero)
    {
        if (hero == null || hero.UltimateSkill == null) return;
        ExecuteSkillActions(hero.UltimateSkill, hero, hero.MappedColor, blockCount: 1, comboMultiplier: 1f);
    }

    /// <summary>
    /// 캐스케이드 결과로부터 색상별 MatchSkill 발동.
    /// </summary>
    public void ExecuteFromCascade(List<ColorMatchData> colorBreakdown, ComboCalculator combo)
    {
        foreach (var data in colorBreakdown)
        {
            var hero = _party.GetHeroByColor(data.Color);
            if (hero == null) continue;

            float multiplier = combo.GetMultiplierAt(data.ComboAtTrigger);
            ExecuteSkillActions(hero.MatchSkill, hero, data.Color, data.BlockCount, multiplier);
        }
    }

    // ── 핵심 실행 엔진 ───────────────────────────────────────────────────────

    /// <summary>
    /// GDD v1.0 순차 멀티 액션 실행.
    /// skill.Actions 배열을 순서대로 순회하여 각 Action을 독립적으로 실행한다.
    /// </summary>
    public void ExecuteSkillActions(HeroSkill skill, HeroState sourceHero,
                                    BlockType sourceColor, int blockCount, float comboMultiplier)
    {
        if (skill == null || skill.Actions.Count == 0) return;

        foreach (var action in skill.Actions)
        {
            // GDD 5절: 각 Action 직전 타겟 유효성 재검증
            ResolveTargets(action, sourceHero,
                out List<EnemyState> enemyTargets,
                out List<HeroState> heroTargets);

            var effect = DamageCalculator.Calculate(
                action, sourceColor, blockCount, comboMultiplier, sourceHero.Attack, skill.Name);

            // ActionType별 효과 적용
            ApplyActionEffect(action, effect, enemyTargets, heroTargets, sourceHero);

            // action.StatusEffects를 해당 액션의 타겟에게 적용
            ApplyActionStatusEffects(action, enemyTargets, heroTargets);

            // 뷰 이벤트 발행 (각 액션마다 발행)
            OnSkillExecuted?.Invoke(effect);
        }
    }

    // ── 타겟 결정 ────────────────────────────────────────────────────────────

    private void ResolveTargets(SkillAction action, HeroState sourceHero,
                                out List<EnemyState> enemyTargets,
                                out List<HeroState> heroTargets)
    {
        if (action.Team == TargetTeam.Enemy)
        {
            enemyTargets = _targeting.Resolve(
                _enemies.AliveEnemies, action.Strategy, action.MaxCount, action.TargetIndex);
            heroTargets = null;
        }
        else
        {
            enemyTargets = null;
            heroTargets = _targeting.Resolve(
                _party.GetAliveHeroes(), action.Strategy, action.MaxCount,
                self: sourceHero, fixedIndex: action.TargetIndex);
        }
    }

    // ── 효과 적용 ────────────────────────────────────────────────────────────

    private void ApplyActionEffect(SkillAction action, SkillEffect effect,
                                   List<EnemyState> enemyTargets,
                                   List<HeroState> heroTargets,
                                   HeroState sourceHero)
    {
        switch (action.ActionType)
        {
            case ActionType.Attack:
                if (enemyTargets == null) break;
                foreach (var enemy in enemyTargets)
                    enemy.TakeDamage(effect.Value);
                break;

            case ActionType.Heal:
                if (heroTargets == null) break;
                foreach (var hero in heroTargets)
                    hero.Heal(effect.Value);
                break;

            case ActionType.Shield:
                if (heroTargets == null) break;
                foreach (var hero in heroTargets)
                    hero.AddShield(effect.Value);
                break;

            case ActionType.Buff:
                // Buff 수치 효과는 StatusEffects로만 처리. 여기서는 추가 처리 없음.
                break;
        }
    }

    private static void ApplyActionStatusEffects(SkillAction action,
                                                  List<EnemyState> enemyTargets,
                                                  List<HeroState> heroTargets)
    {
        if (action.StatusEffects == null || action.StatusEffects.Count == 0) return;

        if (action.Team == TargetTeam.Enemy && enemyTargets != null)
        {
            foreach (var enemy in enemyTargets)
            {
                foreach (var se in action.StatusEffects)
                    enemy.ApplyStatusEffect(se);
            }
        }
        else if (action.Team == TargetTeam.Ally && heroTargets != null)
        {
            foreach (var hero in heroTargets)
            {
                foreach (var se in action.StatusEffects)
                    hero.ApplyStatusEffect(se);
            }
        }
    }
}
