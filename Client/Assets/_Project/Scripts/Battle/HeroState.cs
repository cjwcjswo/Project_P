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
    public bool IsDead => CurrentHP <= 0;

    // ── 모델/리소스 ─────────────────────────────────────────────────────────
    public string PrefabPath { get; private set; }

    // ── 직업 / 등급 / 스킬 ────────────────────────────────────────────────
    public HeroClass HeroClass { get; private set; }
    public int Grade { get; private set; }
    public string IllustrationPath { get; private set; }

    /// <summary>1성~3성 공통: 3매칭 시 발동 스킬.</summary>
    public HeroSkill MatchSkill { get; private set; }
    /// <summary>2성 이상: 특수 블록 탭 시 발동. null이면 미보유.</summary>
    public HeroSkill UniqueSkill { get; private set; }
    /// <summary>3성 전용: 초상화 게이지 만충 후 탭 시 발동. null이면 미보유.</summary>
    public HeroSkill UltimateSkill { get; private set; }

    /// <summary>하위호환 단일 스킬 참조 (MatchSkill 동의어).</summary>
    public HeroSkill Skill => MatchSkill;

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

    // ── 생성자 ─────────────────────────────────────────────────────────────
    public HeroState(int maxHP, int attack, int defense,
                     int growthMaxHP = 0, int growthAttack = 0, int growthDefense = 0,
                     int level = 1,
                     int partyIndex = 0,
                     string prefabPath = "",
                     HeroSkill matchSkill = null,
                     HeroSkill uniqueSkill = null,
                     HeroSkill ultimateSkill = null,
                     float autoAttackInterval = 1.5f, float autoAttackRatio = 0.1f,
                     HeroClass heroClass = HeroClass.None,
                     int grade = 1,
                     string illustrationPath = "")
    {
        Level = Math.Max(1, level);
        PartyIndex = partyIndex;
        PrefabPath = prefabPath ?? "";
        AutoAttackInterval = autoAttackInterval;
        AutoAttackRatio = autoAttackRatio;
        HeroClass = heroClass;
        Grade = grade;
        IllustrationPath = illustrationPath ?? "";

        if (matchSkill == null)
            UnityEngine.Debug.LogWarning($"[HeroState] partyIndex={partyIndex} has no MatchSkill assigned.");
        MatchSkill = matchSkill;
        UniqueSkill = uniqueSkill;
        UltimateSkill = ultimateSkill;

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
}
