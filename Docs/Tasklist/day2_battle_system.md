# Day 2 - Battle System & Skills 구현 가이드

## T-008: HeroState 및 EnemyState 데이터 모델

### 목표
전투 중 히어로와 적의 상태를 관리하는 순수 C# 데이터 모델을 구현한다.
GDD 반영: 용사 자동 평타(1.5초/공격력 10%), 몬스터 고유 스킬(쿨타임+캐스팅 바).

### 작업 내용

#### 1. HeroState
```
경로: Client/Assets/_Project/Scripts/Battle/HeroState.cs
```
```csharp
using System;

public class HeroState
{
    public int MaxHP { get; private set; }
    public int CurrentHP { get; private set; }
    public int Shield { get; private set; }
    public int Attack { get; private set; }
    public int Defense { get; private set; }
    public int Level { get; private set; }
    public bool IsDead => CurrentHP <= 0;

    // 자동 평타 설정 (GDD: 1.5초 간격, 공격력의 10%)
    public float AutoAttackInterval { get; private set; }
    public float AutoAttackRatio { get; private set; }

    public event Action<int, int> OnHPChanged;        // (current, max)
    public event Action<int> OnShieldChanged;          // (shieldAmount)
    public event Action OnDeath;
    public event Action<int> OnAutoAttack;             // (damage) — 평타 발생 시

    public HeroState(int maxHP, int attack, int defense, int level = 1,
                     float autoAttackInterval = 1.5f, float autoAttackRatio = 0.1f)
    {
        MaxHP = maxHP;
        CurrentHP = maxHP;
        Shield = 0;
        Attack = attack;
        Defense = defense;
        Level = level;
        AutoAttackInterval = autoAttackInterval;
        AutoAttackRatio = autoAttackRatio;
    }

    /// <summary>
    /// 자동 평타 데미지 계산: 공격력의 10%
    /// </summary>
    public int GetAutoAttackDamage()
    {
        return Math.Max(1, (int)(Attack * AutoAttackRatio));
    }

    public void TakeDamage(int rawDamage)
    {
        // 실드 우선 소모
        int remaining = rawDamage;
        if (Shield > 0)
        {
            int absorbed = Math.Min(Shield, remaining);
            Shield -= absorbed;
            remaining -= absorbed;
            OnShieldChanged?.Invoke(Shield);
        }

        // 남은 데미지를 HP에서 차감 (방어력 적용)
        int finalDamage = Math.Max(1, remaining - Defense);
        CurrentHP = Math.Max(0, CurrentHP - finalDamage);
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
```

#### 2. EnemyState
```
경로: Client/Assets/_Project/Scripts/Battle/EnemyState.cs
```
```csharp
using System;

public class EnemyState
{
    public int MaxHP { get; private set; }
    public int CurrentHP { get; private set; }
    public int Attack { get; private set; }
    public bool IsDead => CurrentHP <= 0;
    public bool IsStunned { get; private set; }

    // 몬스터 고유 스킬 (GDD: 쿨타임 기반 강력 공격)
    public float SkillCooldown { get; private set; }    // 스킬 쿨타임 (예: 8초)
    public float SkillCastTime { get; private set; }    // 캐스팅 시간 (예: 1.5초)
    public int SkillDamage { get; private set; }        // 스킬 데미지
    public float SkillCastProgress { get; private set; } // 0~1 캐스팅 진행도 (UI용)

    public event Action<int, int> OnHPChanged;           // (current, max)
    public event Action OnDeath;
    public event Action<bool> OnStunChanged;
    public event Action<float> OnSkillCastProgress;      // (0~1 진행도) 캐스팅 바 UI용
    public event Action OnSkillCast;                     // 스킬 시전 완료

    public EnemyState(int maxHP, int attack, float skillCooldown = 8.0f,
                      float skillCastTime = 1.5f, float skillDamageMultiplier = 3.0f)
    {
        MaxHP = maxHP;
        CurrentHP = maxHP;
        Attack = attack;
        SkillCooldown = skillCooldown;
        SkillCastTime = skillCastTime;
        SkillDamage = (int)(attack * skillDamageMultiplier);
    }

    public void TakeDamage(int damage)
    {
        CurrentHP = Math.Max(0, CurrentHP - damage);
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

    public void ApplyStun(float duration)
    {
        IsStunned = true;
        OnStunChanged?.Invoke(true);
    }

    public void RemoveStun()
    {
        IsStunned = false;
        OnStunChanged?.Invoke(false);
    }
}
```

### 기존 대비 변경점
- **HeroState**: 자동 평타 관련 필드/메서드 추가 (`AutoAttackInterval`, `AutoAttackRatio`, `GetAutoAttackDamage()`, `OnAutoAttack`)
- **EnemyState**: 단순 자동 공격 → 스킬 쿨타임/캐스팅 시스템으로 변경 (`SkillCooldown`, `SkillCastTime`, `SkillCastProgress`, `OnSkillCastProgress`, `OnSkillCast`)

### 완료 조건
- [ ] HeroState에 데미지 → 실드 우선 소모 → HP 차감 정상 동작
- [ ] Heal 시 MaxHP 초과 불가
- [ ] HP가 0 이하일 때 OnDeath 이벤트 발행
- [ ] EnemyState 기절 상태 on/off 정상 동작
- [ ] 용사 `GetAutoAttackDamage()`가 공격력의 10% 반환
- [ ] 몬스터 `OnSkillCastProgress`가 캐스팅 진행 중 0~1 값 발행

---

## T-009: 콤보 시스템 및 데미지 계산기

### 목표
캐스케이드 콤보 추적, **콤보 캡(MAX_COMBO_CAP=10)** 적용, **첫 매치 색상 고정 규칙** 반영.

### 작업 내용

#### 1. ComboCalculator
```
경로: Client/Assets/_Project/Scripts/Puzzle/ComboCalculator.cs
```
```csharp
using System;

public class ComboCalculator
{
    // GDD 밸런스 설정값
    private const float AMPLIFY_COEFFICIENT = 0.2f;  // 콤보당 증폭 20%
    private const int MAX_COMBO_CAP = 10;            // 데미지 증폭 한도

    public int CurrentCombo { get; private set; }    // UI 표시용 (캡 없음)
    public int EffectiveCombo => Math.Min(CurrentCombo, MAX_COMBO_CAP); // 데미지 계산용

    public event Action<int> OnComboChanged; // combo count (UI 표시용, 캡 없는 실제 값)

    public void Reset()
    {
        CurrentCombo = 0;
        OnComboChanged?.Invoke(0);
    }

    public void IncrementCombo()
    {
        CurrentCombo++;
        OnComboChanged?.Invoke(CurrentCombo);
    }

    /// <summary>
    /// GDD 공식: 1 + (적용콤보 - 1) * 0.2
    /// 1콤보: 100%, 5콤보: 180%, 10콤보: 280%, 15콤보: 280%(캡)
    /// </summary>
    public float GetMultiplier()
    {
        return 1f + (EffectiveCombo - 1) * AMPLIFY_COEFFICIENT;
    }
}
```

#### 2. 캐스케이드 첫 매치 색상 추적
GDD 핵심 규칙: **연쇄(Cascade)로 터지는 모든 블록은 "가장 처음 유저가 매칭한 블록의 색상"의 스킬을 따른다.**

이 로직은 `Board.TrySwapAsync()` 또는 `BattleManager`에서 관리:
```csharp
// CascadeContext — 캐스케이드 한 세션의 컨텍스트
public struct CascadeContext
{
    public BlockType PrimaryColor;   // 첫 매치의 색상
    public int TotalBlocksMatched;   // 전체 매칭된 블록 수
    public int ComboCount;           // 콤보 횟수
}
```

```csharp
// Board.TrySwapAsync() 내부 수정
// 첫 매치 색상 결정
BlockType primaryColor = matches[0].Type;  // 첫 번째 매치의 색상 고정

while (matches.Count > 0)
{
    combo++;
    // 모든 매치의 블록 수를 합산 (색상 무관)
    int blocksInStep = matches.SelectMany(m => m.Positions).Distinct().Count();
    totalBlocks += blocksInStep;

    // 이벤트에 primaryColor 포함
    EventBus.Publish(new MatchFoundEvent
    {
        Matches = matches,
        ComboStep = combo,
        PrimaryColor = primaryColor  // 항상 첫 매치 색상
    });

    // ... 클리어, 중력, 리필 ...
    matches = _matcher.FindMatches(this);
}
```

#### 3. MatchFoundEvent 수정
```csharp
public struct MatchFoundEvent
{
    public List<MatchResult> Matches;
    public int ComboStep;
    public BlockType PrimaryColor;     // 추가: 캐스케이드 내 스킬 적용 색상
    public int BlocksMatchedInStep;    // 추가: 이번 단계에서 매칭된 블록 수
}
```

#### 4. DamageCalculator
```
경로: Client/Assets/_Project/Scripts/Battle/DamageCalculator.cs
```
```csharp
public static class DamageCalculator
{
    // 기본 값 (BalanceConfig SO로 이동 가능)
    private const float BASE_DAMAGE_PER_BLOCK = 10f;
    private const float BASE_SHIELD_PER_BLOCK = 8f;
    private const float BASE_HEAL_PER_BLOCK = 7f;
    private const float BASE_AOE_PER_BLOCK = 6f;
    private const float STUN_DURATION = 1.5f; // 초

    /// <summary>
    /// 캐스케이드 전체 결과로 스킬 효과 계산.
    /// GDD 규칙: 모든 블록이 primaryColor 스킬로 적용됨.
    /// </summary>
    public static SkillEffect Calculate(BlockType primaryColor, int totalBlockCount,
                                         float comboMultiplier, int heroAttack)
    {
        float baseValue = totalBlockCount * GetBasePerBlock(primaryColor);
        float finalValue = baseValue * comboMultiplier;

        // 공격 스킬은 히어로 공격력 반영
        if (primaryColor == BlockType.Red || primaryColor == BlockType.Yellow)
            finalValue += heroAttack * 0.5f;

        return new SkillEffect
        {
            Type = primaryColor,
            Value = (int)finalValue,
            StunDuration = primaryColor == BlockType.Yellow ? STUN_DURATION : 0f
        };
    }

    private static float GetBasePerBlock(BlockType type) => type switch
    {
        BlockType.Red    => BASE_DAMAGE_PER_BLOCK,
        BlockType.Blue   => BASE_SHIELD_PER_BLOCK,
        BlockType.Green  => BASE_HEAL_PER_BLOCK,
        BlockType.Yellow => BASE_AOE_PER_BLOCK,
        _ => 0f
    };
}

public struct SkillEffect
{
    public BlockType Type;
    public int Value;          // 데미지/실드/회복량
    public float StunDuration; // Yellow 전용
}
```

### 수식 정리 (GDD 반영)

**콤보 증폭 공식:**
```
최종 효과량 = 기본 효과량 * (1 + (적용콤보 - 1) * 0.2)
적용콤보 = min(실제콤보, MAX_COMBO_CAP=10)
```

| 실제 콤보 | 적용 콤보 | 배율 | 비고 |
|-----------|-----------|------|------|
| 1 | 1 | 100% | 기본 |
| 3 | 3 | 140% | |
| 5 | 5 | 180% | |
| 10 | 10 | 280% | 캡 도달 |
| 15 | 10 | 280% | UI에 15 표시, 데미지는 10 기준 |

**캐스케이드 스킬 색상 규칙:**
```
유저가 Red 3매치 → 캐스케이드로 Blue 3개, Green 4개 추가 터짐
→ 총 10블록 모두 Red(공격) 스킬로 데미지 계산
→ Blue/Green 블록의 색상은 무시됨
```

### 완료 조건
- [ ] combo 1에서 시작, 캐스케이드 단계마다 +1 증가
- [ ] GetMultiplier()가 combo=5일 때 1.8 반환
- [ ] combo=15일 때 GetMultiplier()가 2.8 반환 (CAP=10 적용)
- [ ] UI에는 실제 콤보(15) 표시, 데미지 계산에는 EffectiveCombo(10) 사용
- [ ] 캐스케이드 내 모든 매치가 첫 매치 색상으로 통일되어 스킬 발동

---

## T-010: 스킬 시스템 (블록→액션 매핑)

### 목표
매치된 블록의 **첫 매치 색상(PrimaryColor)** 기준으로 전투 효과를 실행한다.

### 작업 내용

#### SkillSystem
```
경로: Client/Assets/_Project/Scripts/Battle/SkillSystem.cs
```
```csharp
public class SkillSystem
{
    private HeroState _hero;
    private EnemyState _enemy;

    public event Action<SkillEffect> OnSkillExecuted;

    public SkillSystem(HeroState hero, EnemyState enemy)
    {
        _hero = hero;
        _enemy = enemy;
    }

    public void ExecuteSkill(SkillEffect effect)
    {
        switch (effect.Type)
        {
            case BlockType.Red:
                _enemy.TakeDamage(effect.Value);
                break;

            case BlockType.Blue:
                _hero.AddShield(effect.Value);
                break;

            case BlockType.Green:
                _hero.Heal(effect.Value);
                break;

            case BlockType.Yellow:
                _enemy.TakeDamage(effect.Value);
                if (effect.StunDuration > 0)
                    _enemy.ApplyStun(effect.StunDuration);
                break;
        }

        OnSkillExecuted?.Invoke(effect);
    }
}
```

### 흐름 (GDD 반영)
```
유저가 블록 스왑
  → 첫 매치 색상(PrimaryColor) 결정
  → 캐스케이드 진행 (모든 연쇄 매치 누적)
  → CascadeCompleteEvent 발행 (primaryColor, totalBlocks, comboCount)
  → BattleManager에서:
      ComboCalc.GetMultiplier() (캡 적용)
      DamageCalc.Calculate(primaryColor, totalBlocks, multiplier, heroAttack)
      → SkillSystem.ExecuteSkill(effect)
          → HeroState/EnemyState 상태 변경
          → OnSkillExecuted 이벤트 → 뷰에서 이펙트 연출
```

### 기존 대비 변경점
- 매치마다 즉시 스킬 발동 → **캐스케이드 완료 후 한 번에 스킬 발동**으로 변경
- 각 매치의 개별 색상 → **PrimaryColor로 통일**

### 완료 조건
- [ ] Red 첫 매치 → 이후 연쇄 블록 색상 무관하게 모두 공격 스킬
- [ ] Blue 첫 매치 → 모든 연쇄 블록이 실드로 적용
- [ ] Green 첫 매치 → 모든 연쇄 블록이 회복으로 적용
- [ ] Yellow 첫 매치 → 모든 연쇄 블록이 광역+기절로 적용

---

## T-011: 궁극기 게이지 시스템

### 목표
블록 매칭으로 게이지를 충전하고, 유저가 초상화를 터치하여 궁극기를 발동한다.
GDD 반영: 블록당 +1, 만충 100, 발동 시 데미지 + 보드 10블록 무작위 파괴(연쇄 유도).

### 작업 내용

#### UltimateGauge
```
경로: Client/Assets/_Project/Scripts/Battle/UltimateGauge.cs
```
```csharp
using System;
using System.Collections.Generic;

public class UltimateGauge
{
    public const int MAX_GAUGE = 100;
    private const int CHARGE_PER_BLOCK = 1;  // GDD: 블록 1개당 1 충전

    public int Current { get; private set; }
    public bool CanActivate => Current >= MAX_GAUGE;

    public event Action<int, int> OnGaugeChanged;                    // (current, max)
    public event Action OnUltimateReady;                             // 만충 시 초상화 점등
    public event Action<int, List<(int col, int row)>> OnUltimateActivated;
    // (damage, 파괴될 블록 좌표 리스트)

    public void ChargeFromMatch(int matchedBlockCount)
    {
        if (CanActivate) return;

        int before = Current;
        Current = Math.Min(MAX_GAUGE, Current + matchedBlockCount * CHARGE_PER_BLOCK);
        OnGaugeChanged?.Invoke(Current, MAX_GAUGE);

        if (CanActivate && before < MAX_GAUGE)
            OnUltimateReady?.Invoke();
    }

    /// <summary>
    /// GDD 궁극기 효과:
    /// 1) 전체 적에게 강력한 고정 데미지
    /// 2) 보드판 내 무작위 블록 10개 파괴 → 연쇄(캐스케이드) 유도
    /// </summary>
    public UltimateResult Activate(int heroAttack, Board board)
    {
        if (!CanActivate) return default;

        // 고정 데미지 (공격력 * 3)
        int damage = (int)(heroAttack * 3.0f);

        // 보드에서 무작위 10블록 선택
        var destroyTargets = PickRandomBlocks(board, 10);

        Current = 0;
        OnUltimateActivated?.Invoke(damage, destroyTargets);
        OnGaugeChanged?.Invoke(Current, MAX_GAUGE);

        return new UltimateResult
        {
            Damage = damage,
            DestroyedPositions = destroyTargets
        };
    }

    private List<(int col, int row)> PickRandomBlocks(Board board, int count)
    {
        var candidates = new List<(int col, int row)>();
        for (int c = 0; c < board.Width; c++)
            for (int r = 0; r < board.Height; r++)
                if (board.GetBlock(c, r) != BlockType.None)
                    candidates.Add((c, r));

        var rand = new Random();
        var selected = new List<(int col, int row)>();
        int pickCount = Math.Min(count, candidates.Count);

        // Fisher-Yates 셔플로 N개 선택
        for (int i = candidates.Count - 1; i >= candidates.Count - pickCount; i--)
        {
            int j = rand.Next(i + 1);
            (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
            selected.Add(candidates[i]);
        }

        return selected;
    }

    public void Reset()
    {
        Current = 0;
        OnGaugeChanged?.Invoke(Current, MAX_GAUGE);
    }
}

public struct UltimateResult
{
    public int Damage;
    public List<(int col, int row)> DestroyedPositions;
}
```

### 기존 대비 변경점
| 항목 | 기존 | GDD 반영 |
|------|------|----------|
| 충전량 | 블록당 +5 (20개로 만충) | **블록당 +1 (100개로 만충)** |
| 효과 | 대량 데미지만 | **데미지 + 보드 10블록 파괴(연쇄 유도)** |
| `Activate()` 시그니처 | `(int heroAttack)` | `(int heroAttack, Board board)` |
| 반환값 | `int damage` | `UltimateResult (damage + positions)` |
| 발동 방식 | 버튼 탭 | **초상화 직접 터치** |

### 궁극기 발동 후 플로우
```
유저가 초상화 터치
  → UltGauge.Activate(heroAttack, board)
  → 1. 적에게 고정 데미지 적용
  → 2. 보드 10블록 좌표 반환
  → BattleManager에서:
       Board.ClearBlocks(positions)
       → Board.ApplyGravity() + Refill()
       → Matcher.FindMatches() → 연쇄 캐스케이드 시작
       (이 연쇄도 콤보 계산에 포함)
```

### 완료 조건
- [ ] 블록 매칭 시 블록당 +1 게이지 충전
- [ ] 100 도달 시 OnUltimateReady 발행
- [ ] Activate() 호출 시 데미지 + 무작위 10블록 좌표 반환
- [ ] 반환된 좌표의 블록 파괴 후 캐스케이드 정상 트리거
- [ ] 만충 상태에서 추가 충전 불가
- [ ] 발동 후 게이지 0 리셋

---

## T-012: BattleManager 오케스트레이션

### 목표
퍼즐 매치 결과를 전투 시스템에 연결하고, 용사 평타/적 스킬/궁극기 블록 파괴를 관리한다.

### 작업 내용

#### BattleManager
```
경로: Client/Assets/_Project/Scripts/Battle/BattleManager.cs
```
```csharp
using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;

public class BattleManager
{
    private HeroState _hero;
    private EnemyState _enemy;
    private Board _board;
    private SkillSystem _skillSystem;
    private ComboCalculator _comboCalc;
    private UltimateGauge _ultGauge;
    private CancellationTokenSource _cts;

    public event Action OnBattleWin;
    public event Action OnBattleLose;

    public void StartBattle(HeroState hero, EnemyState enemy, Board board)
    {
        _hero = hero;
        _enemy = enemy;
        _board = board;
        _comboCalc = new ComboCalculator();
        _ultGauge = new UltimateGauge();
        _skillSystem = new SkillSystem(hero, enemy);

        ServiceLocator.Register(_comboCalc);
        ServiceLocator.Register(_ultGauge);
        ServiceLocator.Register(_skillSystem);

        // 이벤트 구독
        EventBus.Subscribe<CascadeCompleteEvent>(OnCascadeComplete);
        _hero.OnDeath += HandleHeroDeath;
        _enemy.OnDeath += HandleEnemyDeath;

        // 비동기 루프 시작
        _cts = new CancellationTokenSource();
        HeroAutoAttackLoop(_cts.Token).Forget();
        EnemySkillLoop(_cts.Token).Forget();
    }

    /// <summary>
    /// GDD: 캐스케이드 완료 후 한 번에 스킬 발동 (첫 매치 색상으로 통일)
    /// </summary>
    private void OnCascadeComplete(CascadeCompleteEvent evt)
    {
        // 콤보 설정
        for (int i = 0; i < evt.TotalCombo; i++)
            _comboCalc.IncrementCombo();

        float multiplier = _comboCalc.GetMultiplier();

        // 첫 매치 색상 기준으로 전체 블록 효과 계산
        var effect = DamageCalculator.Calculate(
            evt.PrimaryColor, evt.TotalBlocksMatched, multiplier, _hero.Attack);
        _skillSystem.ExecuteSkill(effect);

        // 궁극기 게이지 충전 (전체 블록 수)
        _ultGauge.ChargeFromMatch(evt.TotalBlocksMatched);

        // 콤보 리셋 (다음 유저 입력 대기)
        _comboCalc.Reset();
    }

    /// <summary>
    /// GDD: 용사 자동 평타 (1.5초 간격, 공격력의 10%)
    /// 퍼즐 조작과 무관하게 독립 동작
    /// </summary>
    private async UniTaskVoid HeroAutoAttackLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await UniTask.Delay(
                TimeSpan.FromSeconds(_hero.AutoAttackInterval),
                cancellationToken: ct);

            if (_hero.IsDead || _enemy.IsDead) continue;

            int damage = _hero.GetAutoAttackDamage();
            _enemy.TakeDamage(damage);
            _hero.OnAutoAttack?.Invoke(damage);
        }
    }

    /// <summary>
    /// GDD: 몬스터 고유 스킬 (쿨타임 + 캐스팅)
    /// 쿨타임 대기 → 캐스팅 바 표시 → 시전 완료 → 데미지
    /// </summary>
    private async UniTaskVoid EnemySkillLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            // 쿨타임 대기
            await UniTask.Delay(
                TimeSpan.FromSeconds(_enemy.SkillCooldown),
                cancellationToken: ct);

            if (_enemy.IsDead) break;
            if (_enemy.IsStunned) continue;

            // 캐스팅 진행 (진행도를 UI에 전달)
            float castElapsed = 0f;
            bool interrupted = false;

            while (castElapsed < _enemy.SkillCastTime)
            {
                await UniTask.Delay(
                    TimeSpan.FromSeconds(0.05f), // 50ms 단위 업데이트
                    cancellationToken: ct);

                if (_enemy.IsDead) { interrupted = true; break; }
                if (_enemy.IsStunned)
                {
                    // 기절 시 캐스팅 중단, 쿨타임 처음부터
                    interrupted = true;
                    _enemy.UpdateCastProgress(0f);
                    break;
                }

                castElapsed += 0.05f;
                _enemy.UpdateCastProgress(castElapsed / _enemy.SkillCastTime);
            }

            if (interrupted) continue;

            // 스킬 시전 완료
            _enemy.UpdateCastProgress(0f);
            _enemy.NotifySkillCast();
            _hero.TakeDamage(_enemy.SkillDamage);
        }
    }

    /// <summary>
    /// 궁극기 발동: 데미지 + 보드 10블록 파괴 → 캐스케이드 유도
    /// </summary>
    public async UniTask ActivateUltimateAsync()
    {
        if (!_ultGauge.CanActivate) return;

        var result = _ultGauge.Activate(_hero.Attack, _board);

        // 1. 적에게 고정 데미지
        _enemy.TakeDamage(result.Damage);

        // 2. 보드 블록 파괴 → 중력 → 리필 → 연쇄 캐스케이드
        if (result.DestroyedPositions.Count > 0)
        {
            _board.ClearBlocks(result.DestroyedPositions);
            // 파괴 애니메이션 대기
            await UniTask.Delay(TimeSpan.FromSeconds(0.3f));

            // 중력 + 리필 + 연쇄 처리 (Board의 기존 캐스케이드 로직 재사용)
            await _board.ProcessCascadeAsync();
        }
    }

    private void HandleEnemyDeath()
    {
        _cts?.Cancel();
        OnBattleWin?.Invoke();
        Cleanup();
    }

    private void HandleHeroDeath()
    {
        _cts?.Cancel();
        OnBattleLose?.Invoke();
        Cleanup();
    }

    private void Cleanup()
    {
        EventBus.Unsubscribe<CascadeCompleteEvent>(OnCascadeComplete);
    }
}
```

#### CascadeCompleteEvent 수정
```csharp
public struct CascadeCompleteEvent
{
    public BlockType PrimaryColor;     // 첫 매치 색상
    public int TotalCombo;             // 총 콤보 수
    public int TotalBlocksMatched;     // 총 매칭된 블록 수
    public List<MatchResult> AllMatches;
}
```

#### Board에 추가 필요한 메서드
```csharp
// Board.cs에 추가
/// <summary>
/// 궁극기 등 외부 파괴 후 중력+리필+연쇄를 독립 실행
/// </summary>
public async UniTask ProcessCascadeAsync()
{
    var gravityMoves = ApplyGravity();
    var refillMoves = Refill();
    await UniTask.Delay(TimeSpan.FromSeconds(0.3f));

    var matches = _matcher.FindMatches(this);
    int combo = 0;
    int totalBlocks = 0;
    BlockType primaryColor = matches.Count > 0 ? matches[0].Type : BlockType.None;

    while (matches.Count > 0)
    {
        combo++;
        var clearPositions = matches.SelectMany(m => m.Positions).Distinct().ToList();
        totalBlocks += clearPositions.Count;

        EventBus.Publish(new MatchFoundEvent
        {
            Matches = matches,
            ComboStep = combo,
            PrimaryColor = primaryColor
        });

        ClearBlocks(clearPositions);
        await UniTask.Delay(TimeSpan.FromSeconds(0.3f));

        ApplyGravity();
        Refill();
        await UniTask.Delay(TimeSpan.FromSeconds(0.3f));

        matches = _matcher.FindMatches(this);
    }

    if (combo > 0)
    {
        EventBus.Publish(new CascadeCompleteEvent
        {
            PrimaryColor = primaryColor,
            TotalCombo = combo,
            TotalBlocksMatched = totalBlocks,
            AllMatches = new List<MatchResult>()
        });
    }
}
```

### 전투 흐름 다이어그램 (GDD 반영)
```
[병렬 루프 1] 용사 자동 평타 (1.5초 간격)
    → 공격력 * 10% 미미한 데미지 → Enemy.TakeDamage()

[병렬 루프 2] 몬스터 스킬 (쿨타임 8초)
    → 쿨타임 대기 → 캐스팅 바 진행 (기절 시 중단)
    → 시전 완료 → Hero.TakeDamage(스킬 데미지)

[유저 스왑] → Board.TrySwapAsync()
    → 첫 매치 색상(PrimaryColor) 결정
    → 캐스케이드 (매치→클리어→중력→리필 반복)
    → CascadeCompleteEvent 발행
    → BattleManager:
        ComboCalc (캡 10 적용) → DamageCalc → SkillSystem
        → UltGauge.Charge (블록당 +1)

[유저 초상화 터치] → BattleManager.ActivateUltimateAsync()
    → 1. Enemy.TakeDamage(고정 데미지)
    → 2. Board 무작위 10블록 파괴
    → 3. Board.ProcessCascadeAsync() → 추가 연쇄 → 추가 스킬 발동

[종료] Enemy.OnDeath → BattleWin / Hero.OnDeath → BattleLose
```

### 기존 대비 변경점
- `StartBattle` 시그니처에 `Board board` 파라미터 추가
- `OnMatchFound` 구독 제거 → `OnCascadeComplete`에서 한 번에 처리
- 용사 자동 평타 루프 `HeroAutoAttackLoop` 추가
- 적 자동 공격 → 스킬 쿨타임+캐스팅 루프 `EnemySkillLoop`으로 교체
- `ActivateUltimate()` → `ActivateUltimateAsync()` (보드 파괴 후 캐스케이드 대기)

### 완료 조건
- [ ] 캐스케이드 완료 시 첫 매치 색상으로 콤보 배율 적용된 스킬 발동
- [ ] 용사 1.5초 간격 자동 평타 (공격력 10%) 동작
- [ ] 몬스터 쿨타임 → 캐스팅 → 스킬 시전 동작
- [ ] 몬스터 캐스팅 중 기절 시 캐스팅 중단 + 쿨타임 초기화
- [ ] 궁극기 발동 → 데미지 + 보드 10블록 파괴 → 연쇄 캐스케이드 트리거
- [ ] 적 HP 0 → OnBattleWin, 히어로 HP 0 → OnBattleLose
- [ ] CancellationToken으로 전투 종료 시 모든 루프 정상 중단

---

## T-013: 배틀 씬 뷰 및 HUD

### 목표
전투 화면의 비주얼 요소를 구현한다.
GDD 반영: 초상화 원형 궁극기 게이지, 몬스터 캐스팅 바, 콤보 대형 표시.

### 작업 내용

#### 1. 화면 레이아웃 (GDD 기준)
```
┌──────────────────────────────────┐
│  [상단 전투 뷰]                    │
│                                  │
│  ┌────────┐          ┌────────┐  │
│  │ 용사    │          │ 몬스터  │  │
│  │ 초상화  │    VS    │ 초상화  │  │
│  │◯궁극기  │          │        │  │
│  │ 게이지  │          │        │  │
│  └────────┘          └────────┘  │
│  [■■■■■■■░░░] HP     [■■■■■░░░░] │
│                      [캐스팅 바]  │
│                                  │
│         ╔═══════════╗            │
│         ║  x5 COMBO ║            │  ← 콤보 대형 표시
│         ╚═══════════╝            │
├──────────────────────────────────┤
│  [하단 퍼즐 뷰]                    │
│  ┌─┬─┬─┬─┬─┬─┬─┐               │
│  │🔴│🔵│🟢│🟡│🔴│🔵│🟢│               │
│  ├─┼─┼─┼─┼─┼─┼─┤               │
│  │🟡│🔴│🔵│🟢│🟡│🔴│🔵│               │
│  ├─┼─┼─┼─┼─┼─┼─┤               │
│  │ ... 7x6 보드 ...│               │
│  └─┴─┴─┴─┴─┴─┴─┘               │
└──────────────────────────────────┘
```

#### 2. BattleSceneView
```
경로: Client/Assets/_Project/Scripts/UI/BattleSceneView.cs
```
```csharp
using UnityEngine;
using DG.Tweening;

public class BattleSceneView : MonoBehaviour
{
    [SerializeField] private SpriteRenderer _heroSprite;
    [SerializeField] private SpriteRenderer _enemySprite;

    private SkillSystem _skillSystem;
    private HeroState _hero;

    public void Bind(HeroState hero, SkillSystem skillSystem)
    {
        _hero = hero;
        _skillSystem = skillSystem;
        _skillSystem.OnSkillExecuted += OnSkillExecuted;
        _hero.OnAutoAttack += OnHeroAutoAttack;
    }

    /// <summary>
    /// 용사 평타 연출 (작은 타격 이펙트)
    /// </summary>
    private void OnHeroAutoAttack(int damage)
    {
        _heroSprite.transform.DOPunchPosition(Vector3.right * 0.1f, 0.15f);
        _enemySprite.DOColor(new Color(1f, 0.8f, 0.8f), 0.08f)
            .OnComplete(() => _enemySprite.DOColor(Color.white, 0.08f));
    }

    private void OnSkillExecuted(SkillEffect effect)
    {
        switch (effect.Type)
        {
            case BlockType.Red:
                // 강공격 이펙트
                _heroSprite.transform.DOPunchPosition(Vector3.right * 0.3f, 0.2f);
                _enemySprite.transform.DOPunchPosition(Vector3.left * 0.2f, 0.3f);
                _enemySprite.DOColor(Color.red, 0.1f)
                    .OnComplete(() => _enemySprite.DOColor(Color.white, 0.1f));
                break;

            case BlockType.Blue:
                _heroSprite.DOColor(Color.cyan, 0.15f)
                    .OnComplete(() => _heroSprite.DOColor(Color.white, 0.15f));
                break;

            case BlockType.Green:
                _heroSprite.DOColor(Color.green, 0.15f)
                    .OnComplete(() => _heroSprite.DOColor(Color.white, 0.15f));
                break;

            case BlockType.Yellow:
                _enemySprite.transform.DOShakePosition(0.4f, 0.15f);
                break;
        }
    }

    private void OnDestroy()
    {
        if (_skillSystem != null)
            _skillSystem.OnSkillExecuted -= OnSkillExecuted;
        if (_hero != null)
            _hero.OnAutoAttack -= OnHeroAutoAttack;
    }
}
```

#### 3. BattleHUD (GDD 반영)
```
경로: Client/Assets/_Project/Scripts/UI/BattleHUD.cs
```
```csharp
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

public class BattleHUD : MonoBehaviour
{
    [Header("Hero")]
    [SerializeField] private Image _heroHPFill;
    [SerializeField] private Image _heroShieldFill;
    [SerializeField] private Image _heroPortrait;

    [Header("Hero Ultimate - 원형 게이지")]
    [SerializeField] private Image _ultCircularGauge;    // Image Type=Filled, Fill Method=Radial360
    [SerializeField] private GameObject _ultFireEffect;   // 만충 시 불타오르는 이펙트
    [SerializeField] private Button _ultPortraitButton;   // 초상화 터치 버튼

    [Header("Enemy")]
    [SerializeField] private Image _enemyHPFill;
    [SerializeField] private Image _enemyPortrait;

    [Header("Enemy Skill Casting Bar")]
    [SerializeField] private GameObject _castingBarGroup;  // 캐스팅 중에만 표시
    [SerializeField] private Image _castingBarFill;        // 캐스팅 진행도

    [Header("Combo")]
    [SerializeField] private TextMeshProUGUI _comboText;
    [SerializeField] private GameObject _comboGroup;

    public void BindHero(HeroState hero)
    {
        hero.OnHPChanged += (cur, max) =>
        {
            float ratio = (float)cur / max;
            _heroHPFill.DOFillAmount(ratio, 0.3f);
        };
        hero.OnShieldChanged += shield =>
        {
            _heroShieldFill.gameObject.SetActive(shield > 0);
        };
    }

    public void BindEnemy(EnemyState enemy)
    {
        enemy.OnHPChanged += (cur, max) =>
        {
            float ratio = (float)cur / max;
            _enemyHPFill.DOFillAmount(ratio, 0.3f);
        };

        // 캐스팅 바 바인딩
        enemy.OnSkillCastProgress += progress =>
        {
            _castingBarGroup.SetActive(progress > 0);
            _castingBarFill.fillAmount = progress;
        };

        enemy.OnSkillCast += () =>
        {
            // 스킬 시전 완료 이펙트 (화면 흔들기 등)
            _castingBarGroup.SetActive(false);
            transform.DOShakePosition(0.3f, 10f);
        };
    }

    public void BindCombo(ComboCalculator combo)
    {
        combo.OnComboChanged += count =>
        {
            _comboGroup.SetActive(count > 0);
            _comboText.text = $"{count}";
            if (count > 0)
            {
                // 콤보 대형 표시 + 타격감 극대화
                _comboText.transform.localScale = Vector3.one * 1.5f;
                _comboText.transform.DOScale(Vector3.one, 0.3f).SetEase(Ease.OutBack);
                _comboText.transform.DOPunchScale(Vector3.one * 0.4f, 0.25f);
            }
        };
    }

    /// <summary>
    /// 궁극기 원형 게이지 바인딩 (GDD: 초상화 테두리 시계 방향)
    /// </summary>
    public void BindUltimate(UltimateGauge ult, System.Action onActivate)
    {
        // 원형 게이지 설정
        // _ultCircularGauge: Image Type=Filled, Fill Method=Radial 360,
        //                    Fill Origin=Top, Clockwise=true
        ult.OnGaugeChanged += (cur, max) =>
        {
            float ratio = (float)cur / max;
            _ultCircularGauge.DOFillAmount(ratio, 0.2f);
        };

        ult.OnUltimateReady += () =>
        {
            // 불타오르는 이펙트 활성화
            _ultFireEffect.SetActive(true);
            _ultPortraitButton.interactable = true;

            // 글로우 펄스 애니메이션
            _ultCircularGauge.DOColor(new Color(1f, 0.6f, 0f), 0.5f)
                .SetLoops(-1, LoopType.Yoyo);
        };

        _ultPortraitButton.onClick.AddListener(() =>
        {
            if (!ult.CanActivate) return;
            onActivate?.Invoke();
            _ultFireEffect.SetActive(false);
            _ultPortraitButton.interactable = false;
            _ultCircularGauge.DOKill();
            _ultCircularGauge.color = Color.white;
        });
    }
}
```

#### 4. Canvas 설정 (규칙 준수)
- Canvas Scaler: `Scale With Screen Size`
- Reference Resolution: `1080 x 1920`
- Screen Match Mode: `Match Width Or Height`, Match = `0` (Width 기준)
- Canvas 계층 구조:
  ```
  Canvas (Screen Space - Overlay)
  ├── BattleArea (top 40%)
  │   ├── HeroPanel (좌측)
  │   │   ├── HeroPortrait (Image)
  │   │   ├── UltCircularGauge (Image, Filled, Radial360, Clockwise)
  │   │   ├── UltFireEffect (ParticleSystem or Animated Sprite, 기본 비활성)
  │   │   ├── UltPortraitButton (Button, 초상화 위에 겹침)
  │   │   ├── HeroHPBar (Fill Image)
  │   │   └── HeroShieldBar (Fill Image, HP 바 위 겹침)
  │   │
  │   ├── VSText ("VS" 텍스트)
  │   │
  │   └── EnemyPanel (우측)
  │       ├── EnemyPortrait (Image)
  │       ├── EnemyHPBar (Fill Image)
  │       └── CastingBarGroup (기본 비활성)
  │           ├── CastingBarBG
  │           └── CastingBarFill (Fill Image)
  │
  ├── ComboGroup (화면 중앙, 기본 비활성)
  │   └── ComboText (TextMeshPro, 대형 폰트)
  │
  └── PuzzleArea (bottom 50%) ← PuzzleBoardView가 관리
  ```

#### 5. 원형 게이지 구현 상세
- Unity UI `Image` 컴포넌트 사용
- Image Type: `Filled`
- Fill Method: `Radial 360`
- Fill Origin: `Top` (12시 방향에서 시작)
- Clockwise: `true` (시계 방향으로 채워짐)
- 스프라이트: 초상화 테두리 모양의 원형 마스크 이미지

### 기존 대비 변경점
- 직선 게이지 바 → **초상화 테두리 원형 게이지 (Radial360)**
- 궁극기 버튼 → **초상화 직접 터치** + 불타오르는 이펙트
- 몬스터 단순 HP바 → **HP바 + 스킬 캐스팅 바** 추가
- 콤보 텍스트 → **대형 폰트 + 더 강한 스케일 애니메이션** (타격감 극대화)
- 용사 평타 이펙트 추가

### 완료 조건
- [ ] HP 바가 데미지/회복에 따라 부드럽게 변화
- [ ] 콤보 텍스트가 캐스케이드 시 대형으로 표시 + 펀치 애니메이션
- [ ] 궁극기 원형 게이지가 초상화 테두리를 시계 방향으로 채움
- [ ] 궁극기 만충 시 불타오르는 이펙트 + 초상화 터치 가능
- [ ] 초상화 터치 시 궁극기 발동 + 이펙트 해제 + 게이지 리셋
- [ ] 몬스터 스킬 캐스팅 바가 쿨타임 후 진행되며, 시전 완료 시 숨김
- [ ] 용사 평타 시 작은 타격 이펙트 연출
- [ ] 스킬 발동 시 히어로/적 스프라이트에 DOTween 이펙트
