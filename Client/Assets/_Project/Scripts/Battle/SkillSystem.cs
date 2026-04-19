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

    /// <summary>
    /// 캐스케이드 결과로부터 색상별 개별 스킬 발동.
    /// </summary>
    public void ExecuteFromCascade(List<ColorMatchData> colorBreakdown, ComboCalculator combo)
    {
        foreach (var data in colorBreakdown)
        {
            var hero = _party.GetHeroByColor(data.Color);
            if (hero == null) continue;

            float multiplier = combo.GetMultiplierAt(data.ComboAtTrigger);
            var effect = DamageCalculator.Calculate(hero.Skill, data.Color, data.BlockCount, multiplier, hero.Attack);
            ExecuteSkill(effect, hero);
        }
    }

    public void ExecuteSkill(SkillEffect effect, HeroState sourceHero)
    {
        switch (effect.ActionType)
        {
            case ActionType.Attack:
                ExecuteAttack(effect);
                break;

            case ActionType.Heal:
                ExecuteHeal(effect, sourceHero);
                break;

            case ActionType.Shield:
                ExecuteShield(effect, sourceHero);
                break;

            case ActionType.Buff:
                ExecuteBuff(effect);
                break;
        }

        // StatusEffects 부가 적용 (Attack/Heal/Shield 등 모든 타입)
        if (effect.Skill != null)
            ApplyStatusEffectsToTargets(effect);

        OnSkillExecuted?.Invoke(effect);
    }

    // ── Attack ──────────────────────────────────────────────────────────────
    private void ExecuteAttack(SkillEffect effect)
    {
        switch (effect.TargetScope)
        {
            case TargetScope.Single:
            {
                var target = _targeting.GetPriorityTarget(_enemies.AliveEnemies);
                target?.TakeDamage(effect.Value);
                break;
            }
            case TargetScope.Multi:
            {
                var alive = _enemies.AliveEnemies;
                int count = Math.Min(effect.MaxTargetCount, alive.Count);
                for (int i = 0; i < count; i++)
                    alive[i].TakeDamage(effect.Value);
                break;
            }
            case TargetScope.All:
            {
                foreach (var enemy in _enemies.AliveEnemies)
                    enemy.TakeDamage(effect.Value);
                break;
            }
        }
    }

    // ── Heal ────────────────────────────────────────────────────────────────
    private void ExecuteHeal(SkillEffect effect, HeroState sourceHero)
    {
        switch (effect.TargetScope)
        {
            case TargetScope.Single:
                sourceHero.Heal(effect.Value);
                break;
            case TargetScope.Multi:
            {
                // 최저 HP 순 N명 회복
                var alive = _party.GetAliveHeroes();
                alive.Sort((a, b) => a.CurrentHP.CompareTo(b.CurrentHP));
                int count = Math.Min(effect.MaxTargetCount, alive.Count);
                for (int i = 0; i < count; i++)
                    alive[i].Heal(effect.Value);
                break;
            }
            case TargetScope.All:
            {
                foreach (var hero in _party.GetAliveHeroes())
                    hero.Heal(effect.Value);
                break;
            }
        }
    }

    // ── Shield ──────────────────────────────────────────────────────────────
    private void ExecuteShield(SkillEffect effect, HeroState sourceHero)
    {
        switch (effect.TargetScope)
        {
            case TargetScope.Single:
                sourceHero.AddShield(effect.Value);
                break;
            case TargetScope.All:
            {
                foreach (var hero in _party.GetAliveHeroes())
                    hero.AddShield(effect.Value);
                break;
            }
            default:
                sourceHero.AddShield(effect.Value);
                break;
        }
    }

    // ── Buff ────────────────────────────────────────────────────────────────
    private void ExecuteBuff(SkillEffect effect)
    {
        if (effect.Skill == null) return;

        var targets = effect.TargetScope == TargetScope.All
            ? _party.GetAliveHeroes()
            : new List<HeroState> { GetBuffTarget(effect) };

        foreach (var hero in targets)
        {
            foreach (var se in effect.Skill.StatusEffects)
                hero.ApplyStatusEffect(se);
        }
    }

    private HeroState GetBuffTarget(SkillEffect effect)
    {
        // Single buff → 시전자(partyIndex 0 fallback)
        return _party.GetHeroByIndex(0);
    }

    // ── StatusEffects 부가 적용 ─────────────────────────────────────────────
    private void ApplyStatusEffectsToTargets(SkillEffect effect)
    {
        if (effect.Skill.StatusEffects == null || effect.Skill.StatusEffects.Count == 0) return;
        // Buff 스킬은 ExecuteBuff에서 이미 처리
        if (effect.ActionType == ActionType.Buff) return;

        // Attack 계열 → 적에게 상태이상 부여
        if (effect.ActionType == ActionType.Attack)
        {
            var targets = GetAttackTargets(effect);
            foreach (var enemy in targets)
            {
                foreach (var se in effect.Skill.StatusEffects)
                    enemy.ApplyStatusEffect(se);
            }
        }
    }

    private List<EnemyState> GetAttackTargets(SkillEffect effect)
    {
        var result = new List<EnemyState>();
        var alive = _enemies.AliveEnemies;
        switch (effect.TargetScope)
        {
            case TargetScope.Single:
                var t = _targeting.GetPriorityTarget(alive);
                if (t != null) result.Add(t);
                break;
            case TargetScope.Multi:
                int count = Math.Min(effect.MaxTargetCount, alive.Count);
                for (int i = 0; i < count; i++) result.Add(alive[i]);
                break;
            case TargetScope.All:
                result.AddRange(alive);
                break;
        }
        return result;
    }
}
