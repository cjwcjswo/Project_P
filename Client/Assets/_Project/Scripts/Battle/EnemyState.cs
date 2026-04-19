using System;
using System.Collections.Generic;

public class EnemyState
{
    public int MonsterDataId { get; private set; }
    public string DisplayName { get; private set; }
    public string PrefabPath { get; private set; }

    public int MaxHP { get; private set; }
    public int CurrentHP { get; private set; }
    public int Attack { get; private set; }
    public bool IsDead => CurrentHP <= 0;

    // StatusEffect 시스템으로 통합 — IsStunned는 핸들러 조회로 동작
    public bool IsStunned => _statusEffects.HasEffect(StatusEffectType.Stun);
    public IReadOnlyList<StatusEffectInstance> ActiveEffects => _statusEffects.ActiveEffects;

    private readonly StatusEffectHandler _statusEffects = new();

    public float SkillCooldown { get; private set; }
    public float SkillCastTime { get; private set; }
    public int SkillDamage { get; private set; }
    public float SkillCastProgress { get; private set; }

    public float AutoAttackInterval { get; private set; }
    public float AutoAttackRatio    { get; private set; }
    public int WaveIndex { get; private set; }

    public event Action<int, int> OnHPChanged;
    public event Action OnDeath;
    public event Action<bool> OnStunChanged;
    public event Action<float> OnSkillCastProgress;
    public event Action OnSkillCast;
    public event Action<int> OnDamageTaken;
    public event Action<int> OnAutoAttack;

    public EnemyState(int maxHP, int attack, float skillCooldown = 8.0f,
                      float skillCastTime = 1.5f, float skillDamageMultiplier = 3.0f,
                      float autoAttackInterval = 3.0f, float autoAttackRatio = 0.1f,
                      int waveIndex = 0,
                      int monsterDataId = 0, string displayName = "", string prefabPath = "")
    {
        MonsterDataId = monsterDataId;
        DisplayName = displayName;
        PrefabPath = prefabPath;
        MaxHP = maxHP;
        CurrentHP = maxHP;
        Attack = attack;
        SkillCooldown = skillCooldown;
        SkillCastTime = skillCastTime;
        SkillDamage = (int)(attack * skillDamageMultiplier);
        AutoAttackInterval = autoAttackInterval;
        AutoAttackRatio    = autoAttackRatio;
        WaveIndex = waveIndex;
    }

    public int GetAutoAttackDamage() => Math.Max(1, (int)(Attack * AutoAttackRatio));

    public void NotifyAutoAttack(int damage) => OnAutoAttack?.Invoke(damage);

    public void TakeDamage(int damage)
    {
        int actual = Math.Max(0, damage);
        CurrentHP = Math.Max(0, CurrentHP - actual);
        OnDamageTaken?.Invoke(actual);
        OnHPChanged?.Invoke(CurrentHP, MaxHP);
        if (IsDead) OnDeath?.Invoke();
    }

    public void UpdateCastProgress(float progress)
    {
        SkillCastProgress = Math.Clamp(progress, 0f, 1f);
        OnSkillCastProgress?.Invoke(SkillCastProgress);
    }

    public void NotifySkillCast()
    {
        OnSkillCast?.Invoke();
    }

    /// <summary>StatusEffectData를 Probability 판정 후 적용.</summary>
    public bool ApplyStatusEffect(StatusEffectData data)
    {
        bool wasStunned = IsStunned;
        bool applied = _statusEffects.TryApply(data);
        if (applied && !wasStunned && IsStunned)
            OnStunChanged?.Invoke(true);
        return applied;
    }

    /// <summary>매 프레임 호출 — 지속 효과 Tick 처리.</summary>
    public void TickEffects(float deltaTime)
    {
        bool wasStunned = IsStunned;
        _statusEffects.Tick(deltaTime);
        bool nowStunned = IsStunned;
        if (wasStunned && !nowStunned)
            OnStunChanged?.Invoke(false);
    }
}
