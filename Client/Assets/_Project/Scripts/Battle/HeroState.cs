using System;
using System.Collections.Generic;

public class HeroState
{
    // ── 기본 스탯 ──────────────────────────────────────────────────────────
    public int MaxHP { get; private set; }
    public int CurrentHP { get; private set; }
    public int Shield { get; private set; }
    public int Defense { get; private set; }
    public int Level { get; private set; }
    public int PartyIndex { get; private set; }
    public BlockType MappedColor => HeroColorMap.GetBlockType(PartyIndex);
    public HeroSkill Skill { get; private set; }
    public bool IsDead => CurrentHP <= 0;

    public float AutoAttackInterval { get; private set; }
    public float AutoAttackRatio { get; private set; }

    // ── 성장 스탯 (레벨업 재계산용) ────────────────────────────────────────
    public int BaseMaxHP { get; private set; }
    public int BaseAttack { get; private set; }
    public int BaseDefense { get; private set; }
    private int _growthMaxHP;
    private int _growthAttack;
    private int _growthDefense;

    // ── Attack (AtkUp 버프 적용) ────────────────────────────────────────────
    private int _baseAttackCurrent;  // 레벨 기반 공격력 (버프 미적용)
    public int Attack
    {
        get
        {
            var atkUp = _statusEffects.GetEffect(StatusEffectType.AtkUp);
            if (atkUp != null)
                return (int)(_baseAttackCurrent * (1f + atkUp.Value));
            return _baseAttackCurrent;
        }
    }

    // ── EXP / 레벨업 ───────────────────────────────────────────────────────
    public const int MAX_LEVEL = 50;
    public int CurrentEXP { get; private set; }
    public int NextLevelEXP => Level >= MAX_LEVEL
        ? int.MaxValue
        : (int)(100 * Math.Pow(Level, 2) * Math.Pow(1.1, Level / 10.0));

    // ── StatusEffect ────────────────────────────────────────────────────────
    public IReadOnlyList<StatusEffectInstance> ActiveEffects => _statusEffects.ActiveEffects;
    private readonly StatusEffectHandler _statusEffects = new();

    // ── 이벤트 ─────────────────────────────────────────────────────────────
    public event Action<int, int> OnHPChanged;
    public event Action<int> OnShieldChanged;
    public event Action OnDeath;
    public event Action<int> OnAutoAttack;
    public event Action<int> OnDamageTaken;
    public event Action<int> OnLevelUp;   // arg: newLevel

    // ── 생성자 (growth 포함, Repository 기반) ──────────────────────────────
    public HeroState(int maxHP, int attack, int defense,
                     int growthMaxHP = 0, int growthAttack = 0, int growthDefense = 0,
                     int level = 1,
                     int partyIndex = 0,
                     HeroSkill skill = null,
                     float autoAttackInterval = 1.5f, float autoAttackRatio = 0.1f)
    {
        Level = Math.Max(1, level);
        PartyIndex = partyIndex;
        AutoAttackInterval = autoAttackInterval;
        AutoAttackRatio = autoAttackRatio;
        Skill = skill ?? DefaultSkill(partyIndex);

        BaseMaxHP = maxHP;
        BaseAttack = attack;
        BaseDefense = defense;
        _growthMaxHP = growthMaxHP;
        _growthAttack = growthAttack;
        _growthDefense = growthDefense;

        RecalculateStats();
        CurrentHP = MaxHP;
        Shield = 0;
    }

    // ── 스탯 재계산 ────────────────────────────────────────────────────────
    private void RecalculateStats()
    {
        MaxHP = BaseMaxHP + (Level - 1) * _growthMaxHP;
        _baseAttackCurrent = BaseAttack + (Level - 1) * _growthAttack;
        Defense = BaseDefense + (Level - 1) * _growthDefense;
    }

    // ── EXP / 레벨업 ───────────────────────────────────────────────────────
    public void AddEXP(int amount)
    {
        if (Level >= MAX_LEVEL) return;
        CurrentEXP += amount;
        while (Level < MAX_LEVEL && CurrentEXP >= NextLevelEXP)
        {
            CurrentEXP -= NextLevelEXP;
            LevelUp();
        }
    }

    private void LevelUp()
    {
        int prevMaxHP = MaxHP;
        Level++;
        RecalculateStats();

        // HP 비율 유지
        float ratio = prevMaxHP > 0 ? (float)CurrentHP / prevMaxHP : 1f;
        CurrentHP = Math.Max(1, (int)(MaxHP * ratio));

        OnHPChanged?.Invoke(CurrentHP, MaxHP);
        OnLevelUp?.Invoke(Level);
    }

    // ── StatusEffect ────────────────────────────────────────────────────────
    public bool ApplyStatusEffect(StatusEffectData data)
    {
        return _statusEffects.TryApply(data);
    }

    public void TickEffects(float deltaTime)
    {
        _statusEffects.Tick(deltaTime);
    }

    public bool HasEffect(StatusEffectType type) => _statusEffects.HasEffect(type);

    // ── 전투 액션 ──────────────────────────────────────────────────────────
    public int GetAutoAttackDamage()
    {
        return Math.Max(1, (int)(Attack * AutoAttackRatio));
    }

    public void NotifyAutoAttack(int damage)
    {
        OnAutoAttack?.Invoke(damage);
    }

    public void TakeDamage(int rawDamage)
    {
        int remaining = rawDamage;
        if (Shield > 0)
        {
            int absorbed = Math.Min(Shield, remaining);
            Shield -= absorbed;
            remaining -= absorbed;
            OnShieldChanged?.Invoke(Shield);
        }

        int finalDamage = Math.Max(1, remaining - Defense);
        CurrentHP = Math.Max(0, CurrentHP - finalDamage);
        OnDamageTaken?.Invoke(finalDamage);
        OnHPChanged?.Invoke(CurrentHP, MaxHP);

        if (IsDead) OnDeath?.Invoke();
    }

    public void Heal(int amount)
    {
        int before = CurrentHP;
        CurrentHP = Math.Min(MaxHP, CurrentHP + amount);
        if (CurrentHP != before)
            OnHPChanged?.Invoke(CurrentHP, MaxHP);
    }

    public void AddShield(int amount)
    {
        Shield += amount;
        OnShieldChanged?.Invoke(Shield);
    }

    // ── 기본 스킬 (Repository 미사용 시 fallback) ──────────────────────────
    private static HeroSkill DefaultSkill(int partyIndex) => partyIndex switch
    {
        0 => new HeroSkill(SkillType.SingleAttack,   10f, "공격"),
        1 => new HeroSkill(SkillType.AoEAttack,       6f, "광역", stunDuration: 1.5f),
        2 => new HeroSkill(SkillType.Heal,             7f, "회복"),
        3 => new HeroSkill(SkillType.Shield,           8f, "방어막"),
        4 => new HeroSkill(SkillType.EnhancedAttack,   9f, "강타"),
        _ => new HeroSkill(SkillType.SingleAttack,    10f, "공격")
    };
}
