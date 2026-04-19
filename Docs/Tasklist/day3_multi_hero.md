# Day 3 - Multi-Hero & Multi-Enemy System 구현 가이드

> GDD v2.0 적용: 단일 히어로 → 다중 히어로(덱 빌딩), 단일 적 → 다중 적(웨이브) 전환

---

## T-D3-001: BlockType 확장 및 히어로-색상 인덱스 매핑

### 목표
블록 타입을 5종으로 확장하고, 히어로 배치 인덱스와 블록 색상 간 매핑 시스템을 구현한다.

### 작업 내용

#### 1. BlockType enum 수정
```
경로: Client/Assets/_Project/Scripts/Puzzle/BlockType.cs
```
```csharp
public enum BlockType
{
    None   = 0,
    Red    = 1,   // Index 0 — 최전방
    Yellow = 2,   // Index 1
    Green  = 3,   // Index 2
    Blue   = 4,   // Index 3
    Purple = 5    // Index 4 — 최후방
}
```

> **주의:** 기존 Blue=2, Yellow=4에서 값이 변경됨. GDD v2.0의 인덱스 순서(Red→Yellow→Green→Blue→Purple)에 맞춰 재정렬. 기존 코드에서 `BlockType.Blue`, `BlockType.Yellow`를 참조하는 곳은 이름 기반이므로 enum 값 변경에 의한 직접적 영향은 없지만, **직렬화된 데이터(ScriptableObject, PlayerPrefs 등)가 정수 값으로 저장된 경우** 마이그레이션 필요.

#### 2. Constants 수정
```
경로: Client/Assets/_Project/Scripts/Core/Constants.cs
```
```csharp
public static class Constants
{
    public const int BOARD_WIDTH = 7;
    public const int BOARD_HEIGHT = 6;
    public const int BLOCK_TYPE_COUNT = 5;  // 4 → 5
    public const int MIN_MATCH = 3;

    public const float SWAP_ANIM_DURATION = 0.2f;
    public const float DESTROY_ANIM_DURATION = 0.3f;
    public const float FALL_ANIM_DURATION = 0.3f;

    // 다중 히어로 제약
    public const int MIN_PARTY_SIZE = 3;
    public const int MAX_PARTY_SIZE = 5;
}
```

#### 3. HeroColorMap 신규 생성
```
경���: Client/Assets/_Project/Scripts/Battle/HeroColorMap.cs
```
```csharp
using System;
using System.Collections.Generic;

/// <summary>
/// 히어로 배치 인덱스(0~4)와 BlockType 간 양방향 매핑.
/// GDD v2.0: Index 0=Red, 1=Yellow, 2=Green, 3=Blue, 4=Purple
/// </summary>
public static class HeroColorMap
{
    private static readonly BlockType[] IndexToColor = new[]
    {
        BlockType.Red,     // Index 0
        BlockType.Yellow,  // Index 1
        BlockType.Green,   // Index 2
        BlockType.Blue,    // Index 3
        BlockType.Purple   // Index 4
    };

    private static readonly Dictionary<BlockType, int> ColorToIndex = new()
    {
        { BlockType.Red,    0 },
        { BlockType.Yellow, 1 },
        { BlockType.Green,  2 },
        { BlockType.Blue,   3 },
        { BlockType.Purple, 4 }
    };

    /// <summary>
    /// 배치 인덱스 → 블록 색상
    /// </summary>
    public static BlockType GetBlockType(int partyIndex)
    {
        if (partyIndex < 0 || partyIndex >= IndexToColor.Length)
            throw new ArgumentOutOfRangeException(nameof(partyIndex),
                $"Party index must be 0~{IndexToColor.Length - 1}, got {partyIndex}");
        return IndexToColor[partyIndex];
    }

    /// <summary>
    /// 블록 색상 → 배치 인덱스. 매핑 없으면 -1 반환.
    /// </summary>
    public static int GetHeroIndex(BlockType type)
    {
        return ColorToIndex.TryGetValue(type, out int index) ? index : -1;
    }

    /// <summary>
    /// 활성 히어로 수에 따른 드롭 가능 색상 목록 반환.
    /// </summary>
    public static List<BlockType> GetActiveColors(int partySize)
    {
        int count = Math.Clamp(partySize, Constants.MIN_PARTY_SIZE, Constants.MAX_PARTY_SIZE);
        var colors = new List<BlockType>(count);
        for (int i = 0; i < count; i++)
            colors.Add(IndexToColor[i]);
        return colors;
    }
}
```

### 완료 조건
- [ ] BlockType에 Purple(5) 추가, enum 값이 인덱스 순서와 일치
- [ ] Constants.BLOCK_TYPE_COUNT = 5
- [ ] HeroColorMap.GetBlockType(0) == Red, GetHeroIndex(Purple) == 4
- [ ] GetActiveColors(3) → [Red, Yellow, Green], GetActiveColors(5) → 5색 전부

---

## T-D3-002: HeroParty 데이터 ���델

### 목표
다중 히어로를 관리하는 파티 시스템을 구현하고, HeroState에 파티 인덱스를 추가한다.

### 작업 내용

#### 1. HeroState 수정
```
경로: Client/Assets/_Project/Scripts/Battle/HeroState.cs
```
기존 HeroState에 다음 프로퍼티 추가:
```csharp
// 기존 생성자 시그니처 유지하되, partyIndex 파라미터 추가
public int PartyIndex { get; private set; }
public BlockType MappedColor => HeroColorMap.GetBlockType(PartyIndex);

public HeroState(int maxHP, int attack, int defense, int level = 1,
                 int partyIndex = 0,
                 float autoAttackInterval = 1.5f, float autoAttackRatio = 0.1f)
{
    MaxHP = maxHP;
    CurrentHP = maxHP;
    Shield = 0;
    Attack = attack;
    Defense = defense;
    Level = level;
    PartyIndex = partyIndex;
    AutoAttackInterval = autoAttackInterval;
    AutoAttackRatio = autoAttackRatio;
}
```

#### 2. HeroParty 신규 생성
```
경로: Client/Assets/_Project/Scripts/Battle/HeroParty.cs
```
```csharp
using System;
using System.Collections.Generic;
using System.Linq;

public class HeroParty
{
    private readonly List<HeroState> _heroes;

    public IReadOnlyList<HeroState> Heroes => _heroes;
    public int Count => _heroes.Count;

    /// <summary>
    /// 히어로 사망 시 발행 (partyIndex)
    /// </summary>
    public event Action<int> OnHeroDied;

    /// <summary>
    /// 전원 사망 시 발행
    /// </summary>
    public event Action OnAllDead;

    public HeroParty(List<HeroState> heroes)
    {
        if (heroes.Count < Constants.MIN_PARTY_SIZE || heroes.Count > Constants.MAX_PARTY_SIZE)
            throw new InvalidDeckException(
                $"Party size must be {Constants.MIN_PARTY_SIZE}~{Constants.MAX_PARTY_SIZE}, got {heroes.Count}");

        _heroes = heroes;

        // 각 히어로 사망 이벤트 구독
        foreach (var hero in _heroes)
        {
            int idx = hero.PartyIndex;
            hero.OnDeath += () => HandleHeroDeath(idx);
        }
    }

    /// <summary>
    /// 색상으로 히어로 조회. 사망했거나 없으면 null.
    /// </summary>
    public HeroState GetHeroByColor(BlockType color)
    {
        int index = HeroColorMap.GetHeroIndex(color);
        if (index < 0 || index >= _heroes.Count) return null;
        var hero = _heroes[index];
        return hero.IsDead ? null : hero;
    }

    /// <summary>
    /// 파티 인덱스로 히어로 조회.
    /// </summary>
    public HeroState GetHeroByIndex(int partyIndex)
    {
        if (partyIndex < 0 || partyIndex >= _heroes.Count) return null;
        return _heroes[partyIndex];
    }

    /// <summary>
    /// 생존 히어로 목록.
    /// </summary>
    public List<HeroState> GetAliveHeroes()
    {
        return _heroes.Where(h => !h.IsDead).ToList();
    }

    /// <summary>
    /// 살아있는 최전방 히어로 (Index 0 우선). 전원 사망이면 null.
    /// </summary>
    public HeroState GetFrontHero()
    {
        return _heroes.FirstOrDefault(h => !h.IsDead);
    }

    /// <summary>
    /// 현재 활성(생존) 히어로의 색상 목록.
    /// 보드 리필 시 이 색상만 드롭.
    /// </summary>
    public List<BlockType> GetActiveColors()
    {
        return _heroes
            .Where(h => !h.IsDead)
            .Select(h => h.MappedColor)
            .ToList();
    }

    public bool AllDead => _heroes.All(h => h.IsDead);

    private void HandleHeroDeath(int partyIndex)
    {
        OnHeroDied?.Invoke(partyIndex);

        if (AllDead)
            OnAllDead?.Invoke();
    }
}

public class InvalidDeckException : Exception
{
    public InvalidDeckException(string message) : base(message) { }
}
```

### 완료 조건
- [ ] HeroState에 PartyIndex, MappedColor 프로퍼티 정상 동작
- [ ] HeroParty 생성 시 3명 미만 / 5명 초과 → InvalidDeckException
- [ ] GetHeroByColor(BlockType.Red) → Index 0 히어로 반환
- [ ] GetFrontHero() → 살아있는 최소 인덱스 히어로 반환
- [ ] GetActiveColors() → 생존 히어로의 색상만 반환
- [ ] 히어로 사망 시 OnHeroDied 발행, 전원 사망 시 OnAllDead 발행

---

## T-D3-003: 활성 블록 기반 보드 로직 수정

### ���표
보드의 블록 생성/리필이 활성 히어로 색상만 사용하도록 수정하고, 히어로 사망 시 블록 제거를 지원���다.

### 작업 내용

#### 1. Board 수정
```
경로: Client/Assets/_Project/Scripts/Puzzle/Board.cs
```
기존 `Initialize()`와 `Refill()`에 활성 색상 목록 파라미터 추가:

```csharp
private List<BlockType> _activeColors;

/// <summary>
/// 활성 색상으로 보드 초기화. 3연속 매치 없도록 랜덤 배치.
/// </summary>
public void Initialize(List<BlockType> activeColors)
{
    _activeColors = activeColors;
    var rand = new System.Random();

    for (int col = 0; col < Width; col++)
    {
        for (int row = 0; row < Height; row++)
        {
            BlockType type;
            do
            {
                type = _activeColors[rand.Next(_activeColors.Count)];
            } while (WouldCauseMatch(col, row, type));

            _grid[col, row] = type;
        }
    }
}

/// <summary>
/// 활성 색상 목록 갱신 (히어로 사망 시 호출)
/// </summary>
public void UpdateActiveColors(List<BlockType> activeColors)
{
    _activeColors = activeColors;
}

/// <summary>
/// 특정 색상의 블록을 모두 제거 (None으로 설정).
/// 제거된 위치 목록 반환.
/// </summary>
public List<(int col, int row)> RemoveBlocksOfType(BlockType type)
{
    var removed = new List<(int col, int row)>();
    for (int col = 0; col < Width; col++)
    {
        for (int row = 0; row < Height; row++)
        {
            if (_grid[col, row] == type)
            {
                _grid[col, row] = BlockType.None;
                removed.Add((col, row));
            }
        }
    }
    return removed;
}

/// <summary>
/// 리필 시 활성 색상만 사용
/// </summary>
public List<BlockMove> Refill()
{
    var moves = new List<BlockMove>();
    var rand = new System.Random();

    for (int col = 0; col < Width; col++)
    {
        int emptyCount = 0;
        for (int row = Height - 1; row >= 0; row--)
        {
            if (_grid[col, row] == BlockType.None)
            {
                emptyCount++;
                var type = _activeColors[rand.Next(_activeColors.Count)];
                _grid[col, row] = type;

                moves.Add(new BlockMove
                {
                    Col = col,
                    FromRow = Height + emptyCount - 1,
                    ToRow = row,
                    Type = type,
                    IsNew = true
                });
            }
        }
    }
    return moves;
}
```

> **기존 `Initialize()` (파라미터 없는 버전):** 하위 호환을 위해 유지하되, 내부에서 전체 5색을 활성 색상으로 사용하도록 수정. 또는 제거 후 모든 호출부를 `Initialize(activeColors)` 로 변경.

#### 2. BoardController 수정
```
경로: Client/Assets/_Project/Scripts/Puzzle/BoardController.cs
```
```csharp
// 필드 추가
private List<BlockType> _activeColors;

public BoardController(Board board, List<BlockType> activeColors)
{
    _board = board;
    _matcher = new Matcher();
    _activeColors = activeColors;
}

/// <summary>
/// 히어로 사망 시 호출: 해당 색상 블록 제거 → 중력 → 리필 → 캐스케이드
/// </summary>
public async UniTask RemoveColorAndCascadeAsync(BlockType deadHeroColor)
{
    // 활성 색상에서 제거
    _activeColors.Remove(deadHeroColor);
    _board.UpdateActiveColors(_activeColors);

    // 해당 색상 블록 제거
    var removed = _board.RemoveBlocksOfType(deadHeroColor);
    if (removed.Count == 0) return;

    // 제거 이벤트 발행 (뷰에서 파괴 애니메이션)
    EventBus.Publish(new HeroColorRemovedEvent { Color = deadHeroColor, Positions = removed });
    await UniTask.Delay(TimeSpan.FromSeconds(Constants.DESTROY_ANIM_DURATION));

    // 중력 + 리필 + 캐스케이드
    await ProcessCascadeAsync();
}
```

#### 3. 신규 이벤트 추가
```
경로: Client/Assets/_Project/Scripts/Puzzle/Events.cs
```
```csharp
public struct HeroColorRemovedEvent
{
    public BlockType Color;
    public List<(int col, int row)> Positions;
}
```

### 완료 조건
- [ ] Initialize(3색) 시 보드에 3종 블록만 배치
- [ ] Refill() 시 활성 색상만 드롭
- [ ] RemoveBlocksOfType(Red) → 보드 내 모든 Red 블록 제거
- [ ] UpdateActiveColors() 후 리필에 반영
- [ ] RemoveColorAndCascadeAsync() → 블록 제거 → 중력 → 리필 → 연쇄 매치 처리

---

## T-D3-004: 캐스케이드 개별 스킬 발동 ��팩터링

### 목표
v1의 "첫 매치 색상으로 전체 스킬 통일" 로직을 폐기하고, 캐스케이드 내 각 매치의 블록 색상별로 해당 히어로의 스킬을 개별 발동하도록 리팩터링한다.

### 작업 내용

#### 1. CascadeCompleteEvent 구조 변경
```
경로: Client/Assets/_Project/Scripts/Puzzle/Events.cs
```
```csharp
/// <summary>
/// 색상별 매치 데이터. 캐스케이드 내에서 특정 색상의 총 매치 정보.
/// </summary>
public struct ColorMatchData
{
    public BlockType Color;
    public int BlockCount;      // 해당 색상으로 매칭된 총 블록 수
    public int ComboAtTrigger;  // 해당 색상이 처음 매칭된 시점의 콤보 카운트
}

public struct CascadeCompleteEvent
{
    // PrimaryColor 제거 (v1 잔재)
    public int TotalCombo;
    public int TotalBlocksMatched;
    public List<MatchResult> AllMatches;
    public List<ColorMatchData> ColorBreakdown;  // 색상별 분류
}
```

#### 2. BoardController 캐스케이드 루프 수정
```
경로: Client/Assets/_Project/Scripts/Puzzle/BoardController.cs
```
`TrySwapAsync` 내 캐스케이드 루프에서 PrimaryColor 통일 제거:

```csharp
public async UniTask<CascadeResult> TrySwapAsync(int c1, int r1, int c2, int r2)
{
    // ... 스왑 및 초기 매치 확인은 동일 ...

    var allMatches = new List<MatchResult>();
    int combo = 0;
    int totalBlocks = 0;

    // 색상별 매치 집계
    var colorData = new Dictionary<BlockType, ColorMatchData>();

    while (matches.Count > 0)
    {
        combo++;
        allMatches.AddRange(matches);

        var clearPositions = matches
            .SelectMany(m => m.Positions)
            .Distinct()
            .ToList();

        int blocksInStep = clearPositions.Count;
        totalBlocks += blocksInStep;

        // 색상별 집계
        foreach (var match in matches)
        {
            if (!colorData.ContainsKey(match.Type))
            {
                colorData[match.Type] = new ColorMatchData
                {
                    Color = match.Type,
                    BlockCount = match.Positions.Count,
                    ComboAtTrigger = combo
                };
            }
            else
            {
                var existing = colorData[match.Type];
                existing.BlockCount += match.Positions.Count;
                colorData[match.Type] = existing;
            }
        }

        EventBus.Publish(new MatchFoundEvent
        {
            Matches = matches,
            ComboStep = combo,
            PrimaryColor = matches[0].Type, // 하위 호환용 (뷰 이펙트 등)
            BlocksMatchedInStep = blocksInStep
        });

        _board.ClearBlocks(clearPositions);
        await UniTask.Delay(TimeSpan.FromSeconds(Constants.DESTROY_ANIM_DURATION));

        var gravityMoves = _board.ApplyGravity();
        var refillMoves  = _board.Refill();
        EventBus.Publish(new GravityRefillEvent
        {
            GravityMoves = gravityMoves,
            RefillMoves  = refillMoves
        });
        await UniTask.Delay(TimeSpan.FromSeconds(Constants.FALL_ANIM_DURATION));

        matches = _matcher.FindMatches(_board);
    }

    EventBus.Publish(new CascadeCompleteEvent
    {
        TotalCombo = combo,
        TotalBlocksMatched = totalBlocks,
        AllMatches = allMatches,
        ColorBreakdown = new List<ColorMatchData>(colorData.Values)
    });

    EventBus.Publish(new BoardStabilizedEvent());

    return new CascadeResult
    {
        IsValid    = true,
        Combo      = combo,
        AllMatches = allMatches
    };
}
```

#### 3. ComboCalculator 콤보 캡 제거
```
경로: Client/Assets/_Project/Scripts/Puzzle/ComboCalculator.cs
```
```csharp
using System;

public class ComboCalculator
{
    private const float AMPLIFY_COEFFICIENT = 0.2f;
    // MAX_COMBO_CAP 제거 (GDD v2.0: 무제한 증폭)

    public int CurrentCombo { get; private set; }

    public event Action<int> OnComboChanged;

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
    /// GDD v2.0 공식: 1 + (콤보 - 1) * 0.2
    /// 캡 없음 — 콤보가 높을수록 계속 증폭.
    /// </summary>
    public float GetMultiplier()
    {
        return 1f + (CurrentCombo - 1) * AMPLIFY_COEFFICIENT;
    }

    /// <summary>
    /// 특정 콤보 시점의 배율 계산 (색상별 개별 발동용)
    /// </summary>
    public float GetMultiplierAt(int comboCount)
    {
        return 1f + (comboCount - 1) * AMPLIFY_COEFFICIENT;
    }
}
```

#### 4. SkillSystem 다중 히어로 리팩���링
```
경로: Client/Assets/_Project/Scripts/Battle/SkillSystem.cs
```
```csharp
using System;
using System.Collections.Generic;

public class SkillSystem
{
    private readonly HeroParty _party;
    private readonly EnemyWave _enemies;
    private readonly TargetingSystem _targeting;

    public event Action<SkillEffect> OnSkillExecuted;

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
            if (hero == null) continue; // 사망한 히어로 색상은 스킵

            float multiplier = combo.GetMultiplierAt(data.ComboAtTrigger);
            var effect = DamageCalculator.Calculate(data.Color, data.BlockCount, multiplier, hero.Attack);
            ExecuteSkill(effect, hero);
        }
    }

    public void ExecuteSkill(SkillEffect effect, HeroState sourceHero)
    {
        var target = _targeting.GetPriorityTarget(_enemies.AliveEnemies);

        switch (effect.Type)
        {
            case BlockType.Red:
                target?.TakeDamage(effect.Value);
                break;

            case BlockType.Yellow:
                // 광역: 모든 살아있는 적에게 데미지 + 기절
                foreach (var enemy in _enemies.AliveEnemies)
                {
                    enemy.TakeDamage(effect.Value);
                    if (effect.StunDuration > 0)
                        enemy.ApplyStun(effect.StunDuration);
                }
                break;

            case BlockType.Green:
                sourceHero.Heal(effect.Value);
                break;

            case BlockType.Blue:
                sourceHero.AddShield(effect.Value);
                break;

            case BlockType.Purple:
                // 확장용: 디버프 등 (추후 정의)
                target?.TakeDamage(effect.Value);
                break;
        }

        OnSkillExecuted?.Invoke(effect);
    }
}
```

#### 5. DamageCalculator Purple 지원 추가
```
경로: Client/Assets/_Project/Scripts/Battle/DamageCalculator.cs
```
```csharp
public static class DamageCalculator
{
    private const float BASE_DAMAGE_PER_BLOCK = 10f;
    private const float BASE_SHIELD_PER_BLOCK = 8f;
    private const float BASE_HEAL_PER_BLOCK = 7f;
    private const float BASE_AOE_PER_BLOCK = 6f;
    private const float BASE_PURPLE_PER_BLOCK = 9f;  // Purple 추가
    private const float STUN_DURATION = 1.5f;

    public static SkillEffect Calculate(BlockType color, int totalBlockCount,
                                        float comboMultiplier, int heroAttack)
    {
        float baseValue = totalBlockCount * GetBasePerBlock(color);
        float finalValue = baseValue * comboMultiplier;

        if (color == BlockType.Red || color == BlockType.Yellow || color == BlockType.Purple)
            finalValue += heroAttack * 0.5f;

        return new SkillEffect
        {
            Type = color,
            Value = (int)finalValue,
            StunDuration = color == BlockType.Yellow ? STUN_DURATION : 0f
        };
    }

    private static float GetBasePerBlock(BlockType type) => type switch
    {
        BlockType.Red    => BASE_DAMAGE_PER_BLOCK,
        BlockType.Blue   => BASE_SHIELD_PER_BLOCK,
        BlockType.Green  => BASE_HEAL_PER_BLOCK,
        BlockType.Yellow => BASE_AOE_PER_BLOCK,
        BlockType.Purple => BASE_PURPLE_PER_BLOCK,
        _ => 0f
    };
}
```

### 완료 조건
- [ ] CascadeCompleteEvent에 ColorBreakdown 포함, PrimaryColor 제거
- [ ] 캐스케이드 시 색상별 매치 데이터가 정확히 집계됨
- [ ] 콤보 10 이상에서도 증폭이 계속 상승 (캡 없음)
- [ ] SkillSystem.ExecuteFromCascade()가 색상별 해당 히어로 스킬 개별 발동
- [ ] 사망한 히어로 색상의 스킬은 발동하지 않음
- [ ] DamageCalculator에 Purple 타입 계산 정상 동��

---

## T-D3-005: 개별 궁극기 게이지 시스템

### 목표
단일 공유 궁극기 게이지를 히어로별 개별 게이지로 리팩터링한��.

### 작업 내용

#### UltimateGaugeManager 신규 생성 또는 UltimateGauge 리팩터링
```
경로: Client/Assets/_Project/Scripts/Battle/UltimateGauge.cs
```
```csharp
using System;
using System.Collections.Generic;

/// <summary>
/// 히어로별 개별 궁극기 게이지 관리.
/// GDD v2.0: 자기 색상 블록 매칭 시에만 해당 히어로 게이지 충전.
/// </summary>
public class UltimateGaugeManager
{
    public const int MAX_GAUGE = 100;

    private readonly Dictionary<int, int> _gauges = new(); // partyIndex → gauge

    public event Action<int, int, int> OnGaugeChanged;        // (heroIndex, current, max)
    public event Action<int> OnUltimateReady;                  // (heroIndex)
    public event Action<int, int, List<(int, int)>> OnUltimateActivated; // (heroIndex, damage, destroyedPositions)

    public void Initialize(HeroParty party)
    {
        _gauges.Clear();
        foreach (var hero in party.Heroes)
            _gauges[hero.PartyIndex] = 0;
    }

    public int GetGauge(int heroIndex) =>
        _gauges.TryGetValue(heroIndex, out int val) ? val : 0;

    public bool CanActivate(int heroIndex) =>
        GetGauge(heroIndex) >= MAX_GAUGE;

    /// <summary>
    /// 캐스케이드 결과로부터 색상별 게이지 충전.
    /// 자기 색상 블록만 해당 히어로 게이지에 반영.
    /// </summary>
    public void ChargeFromCascade(List<ColorMatchData> colorBreakdown)
    {
        foreach (var data in colorBreakdown)
        {
            int heroIndex = HeroColorMap.GetHeroIndex(data.Color);
            if (heroIndex < 0 || !_gauges.ContainsKey(heroIndex)) continue;

            int before = _gauges[heroIndex];
            _gauges[heroIndex] = Math.Min(MAX_GAUGE, before + data.BlockCount);

            OnGaugeChanged?.Invoke(heroIndex, _gauges[heroIndex], MAX_GAUGE);

            if (before < MAX_GAUGE && _gauges[heroIndex] >= MAX_GAUGE)
                OnUltimateReady?.Invoke(heroIndex);
        }
    }

    /// <summary>
    /// 특정 히어로의 궁극기 발동. 게이지 소모.
    /// </summary>
    public UltimateResult Activate(int heroIndex, int heroAttack, Board board)
    {
        if (!CanActivate(heroIndex))
            return new UltimateResult { Damage = 0, DestroyedPositions = null };

        _gauges[heroIndex] = 0;
        OnGaugeChanged?.Invoke(heroIndex, 0, MAX_GAUGE);

        // 고정 데미지: heroAttack * 3
        int damage = heroAttack * 3;

        // 보드 무작위 10블록 파괴 (기존 로직 유지)
        var positions = GetRandomBlockPositions(board, 10);

        OnUltimateActivated?.Invoke(heroIndex, damage, positions);

        return new UltimateResult
        {
            Damage = damage,
            DestroyedPositions = positions
        };
    }

    private List<(int col, int row)> GetRandomBlockPositions(Board board, int count)
    {
        var candidates = new List<(int, int)>();
        for (int col = 0; col < board.Width; col++)
            for (int row = 0; row < board.Height; row++)
                if (board.GetBlock(col, row) != BlockType.None)
                    candidates.Add((col, row));

        var rand = new System.Random();
        var result = new List<(int, int)>();
        int pick = Math.Min(count, candidates.Count);
        for (int i = 0; i < pick; i++)
        {
            int idx = rand.Next(candidates.Count);
            result.Add(candidates[idx]);
            candidates.RemoveAt(idx);
        }
        return result;
    }
}
```

### 완료 조건
- [ ] 초기화 시 히어로 수만큼 게이지(0) 생성
- [ ] ChargeFromCascade() → 자기 색상 블록 수만큼 해당 히어로 게이지 충전
- [ ] 다른 색상 블록은 해당 히어로 게이지에 영향 없음
- [ ] 게이지 100 도달 시 OnUltimateReady 발행
- [ ] Activate() → 게이지 0으로 리셋 + 데미지 + 10블록 파괴
- [ ] CanActivate()가 정확히 만충 여부 반환

---

## T-D3-006: 다중 적(웨이브) 시스템 및 타겟팅

### 목표
다수의 적을 관리하는 웨이브 시스템과, 아군/적군의 공격 대상 결정 로직을 구현한다.

### 작��� 내용

#### 1. EnemyState 수정
```
경로: Client/Assets/_Project/Scripts/Battle/EnemyState.cs
```
기존 EnemyState에 웨이브 내 인덱스 추가:
```csharp
public int WaveIndex { get; private set; }

// 생성자에 waveIndex 파라미터 추가
public EnemyState(int maxHP, int attack, float autoAttackInterval,
                  int waveIndex = 0, /* 기존 파라미터 유지 */)
{
    // ... 기존 초기화 ...
    WaveIndex = waveIndex;
}
```

#### 2. EnemyWave 신규 생성
```
경로: Client/Assets/_Project/Scripts/Battle/EnemyWave.cs
```
```csharp
using System;
using System.Collections.Generic;
using System.Linq;

public class EnemyWave
{
    private readonly List<EnemyState> _enemies;

    public IReadOnlyList<EnemyState> Enemies => _enemies;
    public List<EnemyState> AliveEnemies => _enemies.Where(e => !e.IsDead).ToList();
    public bool AllDead => _enemies.All(e => e.IsDead);

    public event Action OnAllEnemiesDead;

    public EnemyWave(List<EnemyState> enemies)
    {
        _enemies = enemies;

        foreach (var enemy in _enemies)
        {
            enemy.OnDeath += CheckAllDead;
        }
    }

    public EnemyState GetEnemy(int waveIndex)
    {
        return _enemies.FirstOrDefault(e => e.WaveIndex == waveIndex && !e.IsDead);
    }

    /// <summary>
    /// 가장 전방(최소 WaveIndex)의 살아있는 적
    /// </summary>
    public EnemyState GetFrontEnemy()
    {
        return _enemies
            .Where(e => !e.IsDead)
            .OrderBy(e => e.WaveIndex)
            .FirstOrDefault();
    }

    private void CheckAllDead()
    {
        if (AllDead)
            OnAllEnemiesDead?.Invoke();
    }
}
```

#### 3. TargetingSystem 신규 생���
```
경로: Client/Assets/_Project/Scripts/Battle/TargetingSystem.cs
```
```csharp
using System.Collections.Generic;
using System.Linq;

public class TargetingSystem
{
    private int _manualTargetIndex = -1; // 유저가 탭으로 지정한 적 인덱스

    /// <summary>
    /// 아군 → 적 타겟 결정.
    /// 수동 지정이 있으면 해당 적, 없으면 전방 우선.
    /// </summary>
    public EnemyState GetPriorityTarget(List<EnemyState> aliveEnemies)
    {
        if (aliveEnemies == null || aliveEnemies.Count == 0) return null;

        // 수동 타겟 지정 시
        if (_manualTargetIndex >= 0)
        {
            var manual = aliveEnemies.FirstOrDefault(e => e.WaveIndex == _manualTargetIndex);
            if (manual != null) return manual;
            // 수동 타겟이 사망했으면 자동으로 전방 우선
            _manualTargetIndex = -1;
        }

        // 기본: 가장 전방(최소 WaveIndex) 적
        return aliveEnemies.OrderBy(e => e.WaveIndex).First();
    }

    /// <summary>
    /// 유저 탭으로 일점사 타겟 지정
    /// </summary>
    public void SetManualTarget(int enemyWaveIndex)
    {
        _manualTargetIndex = enemyWaveIndex;
    }

    /// <summary>
    /// 수동 타겟 해제
    /// </summary>
    public void ClearManualTarget()
    {
        _manualTargetIndex = -1;
    }

    /// <summary>
    /// 적 → 아군 타겟 결정.
    /// Index 0 우선, 사망 시 다음 인덱스.
    /// GDD v2.0: 도발 스킬이 없을 경우 Index 0 우선 또는 랜덤.
    /// </summary>
    public HeroState GetHeroTarget(HeroParty party)
    {
        return party.GetFrontHero();
    }
}
```

### 완료 조건
- [ ] EnemyWave: 적 전원 사망 시 OnAllEnemiesDead 발행
- [ ] GetFrontEnemy() → 살아있는 최소 WaveIndex 적 반환
- [ ] TargetingSystem.GetPriorityTarget() → 전방 적 우선 반환
- [ ] SetManualTarget() → 유저 지정 적 타겟팅 동작
- [ ] 수동 타겟 사망 시 자동으로 전방 우선 복귀
- [ ] GetHeroTarget() → 아군 Index 0 우선 (사망 시 다음 인덱스)

---

## T-D3-007: BattleManager 멀티 히어로/적 오케스트레이션

### 목표
BattleManager를 HeroParty + EnemyWave 기반으로 전면 리팩터링하여 v2.0 전투 루프를 구현한다.

### 작업 내용

#### BattleManager 리팩터링
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
    private HeroParty _party;
    private EnemyWave _wave;
    private BoardController _boardController;
    private SkillSystem _skillSystem;
    private ComboCalculator _comboCalc;
    private UltimateGaugeManager _ultManager;
    private TargetingSystem _targeting;
    private CancellationTokenSource _cts;

    public event Action OnBattleWin;
    public event Action OnBattleLose;

    public UltimateGaugeManager UltManager => _ultManager;
    public TargetingSystem Targeting => _targeting;

    public void StartBattle(HeroParty party, EnemyWave wave, BoardController boardController)
    {
        _party = party;
        _wave = wave;
        _boardController = boardController;
        _targeting = new TargetingSystem();
        _comboCalc = new ComboCalculator();
        _ultManager = new UltimateGaugeManager();
        _skillSystem = new SkillSystem(party, wave, _targeting);

        _ultManager.Initialize(party);

        ServiceLocator.Register(_comboCalc);
        ServiceLocator.Register(_ultManager);
        ServiceLocator.Register(_skillSystem);
        ServiceLocator.Register(_targeting);

        // 이벤트 구독
        EventBus.Subscribe<CascadeCompleteEvent>(OnCascadeComplete);
        _party.OnHeroDied += OnHeroDied;
        _party.OnAllDead += HandleAllHeroesDead;
        _wave.OnAllEnemiesDead += HandleAllEnemiesDead;

        // 전투 루프 시작
        _cts = new CancellationTokenSource();

        // 히어로별 자동 공격 루프
        foreach (var hero in party.Heroes)
        {
            HeroAutoAttackLoop(hero, _cts.Token).Forget();
        }

        // 적별 자동 공격 + 스킬 루프
        foreach (var enemy in wave.Enemies)
        {
            EnemyAutoAttackLoop(enemy, _cts.Token).Forget();
            if (enemy.SkillCooldown > 0)
                EnemySkillLoop(enemy, _cts.Token).Forget();
        }
    }

    /// <summary>
    /// 캐스케이드 완료 시 색상별 개별 스킬 발동 + 궁극기 충전.
    /// </summary>
    private void OnCascadeComplete(CascadeCompleteEvent evt)
    {
        for (int i = 0; i < evt.TotalCombo; i++)
            _comboCalc.IncrementCombo();

        // 색상별 개별 스킬 발동
        _skillSystem.ExecuteFromCascade(evt.ColorBreakdown, _comboCalc);

        // 색상별 개별 궁극기 충전
        _ultManager.ChargeFromCascade(evt.ColorBreakdown);

        _comboCalc.Reset();
    }

    /// <summary>
    /// 히어로 사망 시 보드에서 해당 색상 블록 제거.
    /// </summary>
    private void OnHeroDied(int partyIndex)
    {
        var color = HeroColorMap.GetBlockType(partyIndex);
        _boardController.RemoveColorAndCascadeAsync(color).Forget();
    }

    /// <summary>
    /// 특정 히어로의 궁극기 발동 (UI에서 ��출).
    /// </summary>
    public async UniTask ActivateUltimateAsync(int heroIndex)
    {
        var hero = _party.GetHeroByIndex(heroIndex);
        if (hero == null || hero.IsDead) return;

        var result = _ultManager.Activate(heroIndex, hero.Attack, _boardController.Board);
        if (result.Damage <= 0) return;

        // 전방 적에게 궁극기 데미지
        var target = _targeting.GetPriorityTarget(_wave.AliveEnemies);
        target?.TakeDamage(result.Damage);

        // 보드 10블록 파괴 → 캐스케이드
        if (result.DestroyedPositions != null && result.DestroyedPositions.Count > 0)
        {
            _boardController.Board.ClearBlocks(result.DestroyedPositions);
            EventBus.Publish(new GravityRefillEvent
            {
                GravityMoves = new List<BlockMove>(),
                RefillMoves  = new List<BlockMove>()
            });
            await UniTask.Delay(TimeSpan.FromSeconds(0.3f));
            await _boardController.ProcessCascadeAsync();
        }
    }

    /// <summary>
    /// 히어로 자동 평타 루프. 타겟팅 시스템 사용.
    /// </summary>
    private async UniTaskVoid HeroAutoAttackLoop(HeroState hero, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await UniTask.Delay(
                TimeSpan.FromSeconds(hero.AutoAttackInterval),
                cancellationToken: ct);

            if (hero.IsDead) break;

            var target = _targeting.GetPriorityTarget(_wave.AliveEnemies);
            if (target == null) continue;

            int damage = hero.GetAutoAttackDamage();
            target.TakeDamage(damage);
            hero.NotifyAutoAttack(damage);
        }
    }

    /// <summary>
    /// 적 자동 평타 루프. 아군 Index 0 우선 타격.
    /// </summary>
    private async UniTaskVoid EnemyAutoAttackLoop(EnemyState enemy, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await UniTask.Delay(
                TimeSpan.FromSeconds(enemy.AutoAttackInterval),
                cancellationToken: ct);

            if (enemy.IsDead) break;
            if (enemy.IsStunned) continue;

            var heroTarget = _targeting.GetHeroTarget(_party);
            if (heroTarget == null) continue;

            int damage = enemy.GetAutoAttackDamage();
            heroTarget.TakeDamage(damage);
            enemy.NotifyAutoAttack(damage);
        }
    }

    /// <summary>
    /// 적 스킬 루프. 기존 로직 유지, 타겟만 변경.
    /// </summary>
    private async UniTaskVoid EnemySkillLoop(EnemyState enemy, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await UniTask.Delay(
                TimeSpan.FromSeconds(enemy.SkillCooldown),
                cancellationToken: ct);

            if (enemy.IsDead) break;
            if (enemy.IsStunned) continue;

            float castElapsed = 0f;
            bool interrupted = false;

            while (castElapsed < enemy.SkillCastTime)
            {
                await UniTask.Delay(
                    TimeSpan.FromSeconds(0.05f),
                    cancellationToken: ct);

                if (enemy.IsDead) { interrupted = true; break; }
                if (enemy.IsStunned)
                {
                    interrupted = true;
                    enemy.UpdateCastProgress(0f);
                    break;
                }

                castElapsed += 0.05f;
                enemy.UpdateCastProgress(castElapsed / enemy.SkillCastTime);
            }

            if (interrupted) continue;

            enemy.UpdateCastProgress(0f);
            enemy.NotifySkillCast();

            var heroTarget = _targeting.GetHeroTarget(_party);
            if (heroTarget != null)
                heroTarget.TakeDamage(enemy.SkillDamage);
        }
    }

    private void HandleAllEnemiesDead()
    {
        _cts?.Cancel();
        OnBattleWin?.Invoke();
        Cleanup();
    }

    private void HandleAllHeroesDead()
    {
        _cts?.Cancel();
        OnBattleLose?.Invoke();
        Cleanup();
    }

    private void Cleanup()
    {
        EventBus.Unsubscribe<CascadeCompleteEvent>(OnCascadeComplete);
        if (_party != null)
        {
            _party.OnHeroDied -= OnHeroDied;
            _party.OnAllDead -= HandleAllHeroesDead;
        }
        if (_wave != null)
            _wave.OnAllEnemiesDead -= HandleAllEnemiesDead;
    }
}
```

### 동작 시나리오
```
전투 시작: HeroParty(3명) + EnemyWave(2마리) + Board(3색)

1. 유저 Red 블록 매칭 → 캐스케이드 (Red 3개 + Yellow 연쇄 3개)
   → Index 0 히어로 Red 스킬 발동 (1콤보 100%)
   → Index 1 히어로 Yellow 스킬 발동 (2콤보 120%)
   → Red 게이지 +3, Yellow 게이지 +3

2. 적 자동 공격 → Index 0 히어로(최전방) 타격

3. Index 0 히어로 사망
   → Red 블록 즉시 제거 → 중력 + 리필 (2색만) → 캐스케이드
   → 보드에 Red 더 이상 드롭 안 됨

4. 적 자동 공격 → Index 1 히어로(다음 최전방) 타격

5. Index 2 히어로 궁극기 만충 → 터치 → 전방 적에게 대데미지 + 10블록 파괴
```

### 완료 조건
- [ ] StartBattle(HeroParty, EnemyWave, BoardController) 정상 초기화
- [ ] 캐스케이드 시 색상별 해당 히어로 스킬 개별 발동
- [ ] 캐스케이드 시 자기 색상만 궁극기 게이지 충전
- [ ] 히어로별 자동 공격이 타겟팅 시스템 사용
- [ ] 히어로 사망 → 해당 색상 블록 제거 → 리필 시 드롭 중단
- [ ] 적 공격이 아군 Index 0 우선 타격 (사망 시 다음)
- [ ] 전원 히어로 사망 → OnBattleLose
- [ ] 전원 적 사망 → OnBattleWin
- [ ] CancellationToken으로 모든 루프 정상 해제

---

## T-D3-008: 히어로 사망 시 비활성 블록(Disabled) 처리

### 목표
히어로 사망 시 해당 색상 블록을 완전 제거(None)하는 대신 **비활성 블록(Disabled, 회색)**으로 전환한다. Disabled 블록은 이동 불가, 매칭 불가, 중력 낙하 불가의 고정 장애물 역할을 한다.

### 작업 내용

#### 1. BlockType에 Disabled 추가
```
경로: Client/Assets/_Project/Scripts/Puzzle/BlockType.cs
```
```csharp
public enum BlockType
{
    None     = 0,
    Red      = 1,   // Index 0 — 최전방
    Yellow   = 2,   // Index 1
    Green    = 3,   // Index 2
    Blue     = 4,   // Index 3
    Purple   = 5,   // Index 4 — 최후방
    Disabled = 99   // 비활성 블록 (이동/매칭 불가, 고정 장애물)
}
```

#### 2. Board 수정 — 블록 비활성화 및 중력 예외 처리
```
경로: Client/Assets/_Project/Scripts/Puzzle/Board.cs
```

**RemoveBlocksOfType → ConvertBlocksToDisabled로 변경:**
```csharp
/// <summary>
/// 특정 색상의 블록을 Disabled(회색)로 전환.
/// 전환된 위치 목록 반환.
/// </summary>
public List<(int col, int row)> ConvertBlocksToDisabled(BlockType type)
{
    var converted = new List<(int col, int row)>();
    for (int col = 0; col < Width; col++)
    {
        for (int row = 0; row < Height; row++)
        {
            if (_grid[col, row] == type)
            {
                _grid[col, row] = BlockType.Disabled;
                converted.Add((col, row));
            }
        }
    }
    return converted;
}
```

**ApplyGravity() 수정 — Disabled 블록을 낙하 대상에서 제외:**
```csharp
public List<BlockMove> ApplyGravity()
{
    var moves = new List<BlockMove>();

    for (int col = 0; col < Width; col++)
    {
        int writeRow = 0;
        for (int readRow = 0; readRow < Height; readRow++)
        {
            var block = _grid[col, readRow];

            // Disabled 블록은 제자리에 고정 — 건너뜀
            if (block == BlockType.Disabled)
            {
                // writeRow가 Disabled 위치에 도달하면 건너뛰기
                if (writeRow == readRow)
                    writeRow++;
                continue;
            }

            if (block != BlockType.None)
            {
                // writeRow가 Disabled 블록 위치면 건너뛰기
                while (writeRow < Height && _grid[col, writeRow] == BlockType.Disabled)
                    writeRow++;

                if (readRow != writeRow && writeRow < Height)
                {
                    moves.Add(new BlockMove
                    {
                        Col     = col,
                        FromRow = readRow,
                        ToRow   = writeRow,
                        Type    = block,
                        IsNew   = false
                    });
                    _grid[col, writeRow] = block;
                    _grid[col, readRow]  = BlockType.None;
                }
                writeRow++;
                // 다시 Disabled 건너뛰기
                while (writeRow < Height && _grid[col, writeRow] == BlockType.Disabled)
                    writeRow++;
            }
        }
    }
    return moves;
}
```

**Refill() 수정 — None인 칸만 리필 (Disabled는 건너뛰기):**
```csharp
public List<BlockMove> Refill()
{
    var moves = new List<BlockMove>();

    for (int col = 0; col < Width; col++)
    {
        int spawnOffset = 0;
        for (int row = 0; row < Height; row++)
        {
            if (_grid[col, row] == BlockType.None)
            {
                var type = _activeColors[_rand.Next(_activeColors.Count)];
                _grid[col, row] = type;
                moves.Add(new BlockMove
                {
                    Col     = col,
                    FromRow = Height + spawnOffset,
                    ToRow   = row,
                    Type    = type,
                    IsNew   = true
                });
                spawnOffset++;
            }
            // Disabled는 그대로 유지 (리필 대상 아님)
        }
    }
    return moves;
}
```

#### 3. Matcher 수정 — Disabled를 None과 동일하게 매칭 불가 처리
```
경로: Client/Assets/_Project/Scripts/Puzzle/Matcher.cs
```
Matcher의 스캔 로직에서 `BlockType.None` 체크하는 곳에 `BlockType.Disabled`도 동일하게 streak 끊김 처리:
```csharp
// ScanDirection, ScanSingleLine 내부에서:
// 기존: if (type != BlockType.None && type == streakType)
// 변경:
bool isMatchable = type != BlockType.None && type != BlockType.Disabled;
if (isMatchable && type == streakType)
{
    streak.Add((col, row));
}
else
{
    FlushStreak(streak, results);
    streak.Clear();
    if (isMatchable)
        streak.Add((col, row));
    streakType = isMatchable ? type : BlockType.None;
}
```

#### 4. BoardController 수정 — 스왑 시 Disabled 블록 체크
```
경로: Client/Assets/_Project/Scripts/Puzzle/BoardController.cs
```
**TrySwapAsync()에서 Disabled 블록 스왑 차단:**
```csharp
public async UniTask<CascadeResult> TrySwapAsync(int c1, int r1, int c2, int r2)
{
    if (!_board.IsAdjacent(c1, r1, c2, r2))
        return CascadeResult.Invalid;

    // Disabled 블록은 스왑 불가
    if (_board.GetBlock(c1, r1) == BlockType.Disabled ||
        _board.GetBlock(c2, r2) == BlockType.Disabled)
    {
        return CascadeResult.Invalid;
    }

    // ... 이하 기존 로직 동일 ...
}
```

**RemoveColorAndCascadeAsync → DisableColorAsync로 리네이밍:**
```csharp
/// <summary>
/// 히어로 사망 시 호출: 해당 색상 블록을 Disabled(회색)로 전환 → 중력 → 리필 → 캐스케이드
/// </summary>
public async UniTask DisableColorAsync(BlockType deadHeroColor)
{
    _activeColors.Remove(deadHeroColor);
    _board.UpdateActiveColors(_activeColors);

    var converted = _board.ConvertBlocksToDisabled(deadHeroColor);
    if (converted.Count == 0) return;

    // 이벤트 발행 (뷰에서 회색 전환 애니메이션)
    EventBus.Publish(new HeroColorDisabledEvent { Color = deadHeroColor, Positions = converted });
    await UniTask.Delay(TimeSpan.FromSeconds(Constants.DESTROY_ANIM_DURATION));

    // 중력 + 리필 + 캐스케이드 (Disabled 블록은 고정, None 칸만 리필)
    await ProcessCascadeAsync();
}
```

#### 5. 이벤트 변경
```
경로: Client/Assets/_Project/Scripts/Puzzle/Events.cs
```
```csharp
// HeroColorRemovedEvent → HeroColorDisabledEvent로 교체
public struct HeroColorDisabledEvent
{
    public BlockType Color;
    public List<(int col, int row)> Positions;
}
```

#### 6. BlockView 수정 — Disabled 상태 렌더링
```
경로: Client/Assets/_Project/Scripts/UI/BlockView.cs
```
```csharp
// BlockColors 딕셔너리에 Disabled 추가
private static readonly Dictionary<BlockType, Color> BlockColors = new()
{
    { BlockType.Red,      Color.red },
    { BlockType.Yellow,   Color.yellow },
    { BlockType.Green,    Color.green },
    { BlockType.Blue,     new Color(0.2f, 0.5f, 1f) },
    { BlockType.Purple,   new Color(0.6f, 0.2f, 0.9f) },
    { BlockType.Disabled, new Color(0.4f, 0.4f, 0.4f) }  // 회색
};
```

#### 7. PuzzleBoardView 수정 — 이벤트 구독 변경
```
경로: Client/Assets/_Project/Scripts/UI/PuzzleBoardView.cs
```
```csharp
// OnEnable/OnDisable에서:
// HeroColorRemovedEvent → HeroColorDisabledEvent로 변경
EventBus.Subscribe<HeroColorDisabledEvent>(OnHeroColorDisabled);
EventBus.Unsubscribe<HeroColorDisabledEvent>(OnHeroColorDisabled);

/// <summary>
/// 히어로 사망으로 해당 색상 블록이 Disabled(회색)로 전환될 때 애니메이션.
/// 블록을 파괴하지 않고 회색으로 전환.
/// </summary>
private void OnHeroColorDisabled(HeroColorDisabledEvent evt)
{
    foreach (var (col, row) in evt.Positions)
    {
        var view = _blockViews[col, row];
        if (view == null) continue;

        // 회색으로 전환 + 약간의 흔들림 이펙트
        view.Setup(BlockType.Disabled, col, row);
        view.transform.DOShakeScale(0.3f, 0.2f);
    }
}
```

#### 8. BattleManager 수정 — OnHeroDied 호출 변경
```
경로: Client/Assets/_Project/Scripts/Battle/BattleManager.cs
```
```csharp
private void OnHeroDied(int partyIndex)
{
    var color = HeroColorMap.GetBlockType(partyIndex);
    // RemoveColorAndCascadeAsync → DisableColorAsync
    _boardController.DisableColorAsync(color).Forget();
}
```

### 동작 시나리오
```
1. Index 0 히어로(Red) 사망
   → 보드 내 모든 Red 블록이 회색(Disabled)으로 전환
   → Disabled 블록: 이동 불가, 매칭 불가, 중력 고정
   → Disabled 위의 블록은 낙하, None 칸만 새 블록 리필 (Red 제외)
   → 보드에 회색 블록이 장애물로 남아 전략적 난이도 상승
```

### 완료 조건
- [ ] BlockType.Disabled 추가, enum 값 = 99
- [ ] 히어로 사망 시 해당 색상 블록이 None이 아닌 Disabled로 전환
- [ ] Disabled 블록이 회색으로 렌더링
- [ ] Disabled 블록은 스왑 대상에서 제외 (터치해도 반응 없음)
- [ ] Disabled 블록은 Matcher에서 매칭 불가 (streak 끊김)
- [ ] Disabled 블록은 중력에 의해 낙하하지 않음 (제자리 고정)
- [ ] Disabled 블록 위의 일반 블록은 정상 낙하
- [ ] Refill 시 None 칸만 새 블록 생성, Disabled 칸은 건너뜀
- [ ] PuzzleBoardView에서 회색 전환 애니메이션 정상 동작

---

## T-D3-009: EnemyHUD 월드 스페이스 전환 — 초상화 제거, HP/캐스팅 바를 적 스프라이트 위에 배치

### 목표
EnemyHUDView에서 몬스터 초상화를 제거하고, HP 바와 캐스팅 바를 스크린 스페이스 Canvas가 아닌 적 스프라이트 바로 위에 월드 스페이스로 배치한다.

### 작업 내용

#### 1. EnemyEntityView 프리팹 구조 변경
```
경로: Client/Assets/_Project/Scripts/UI/EnemyEntityView.cs
```
EnemyEntityView 프리팹에 HP 바, 캐스팅 바, 타겟 버튼을 자식으로 포함하도록 확장:

**프리팹 계층 구조:**
```
EnemyEntity (EnemyEntityView)
├── SpriteRenderer (적 캐릭터)
├── Canvas (World Space, SortOrder=10)
│   ├── HPBarBackground (Image, 회색 배경)
│   │   └── HPBarFill (Image, Type=Filled, Method=Horizontal)
│   ├── CastingBarGroup (기본 비활성)
│   │   ├── CastingBarBackground (Image)
│   │   └── CastingBarFill (Image, Type=Filled)
│   └── DeadOverlay (기본 비활성)
└── Collider2D (BoxCollider2D, 터치 영역)
```

**Canvas 설정:**
- Render Mode: World Space
- Sorting Layer: UI (또는 전투 UI 전용 레이어)
- 위치: 스프라이트 상단에서 약간 위 (localPosition.y = 스프라이트 높이 + offset)
- 크기: 스프라이트 너비에 맞춤

```csharp
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class EnemyEntityView : MonoBehaviour
{
    [Header("Sprite")]
    [SerializeField] private SpriteRenderer _spriteRenderer;

    [Header("World Space HUD")]
    [SerializeField] private Image      _hpFill;
    [SerializeField] private GameObject _castingBarGroup;
    [SerializeField] private Image      _castingBarFill;
    [SerializeField] private GameObject _deadOverlay;

    public int     WaveIndex     { get; private set; }
    public Vector3 WorldPosition => transform.position;

    private Color _baseColor;

    public void Bind(EnemyState enemy)
    {
        WaveIndex  = enemy.WaveIndex;
        _baseColor = _spriteRenderer.color;

        _hpFill.fillAmount = 1f;
        _deadOverlay.SetActive(false);

        bool hasSkill = enemy.SkillCooldown > 0f;
        _castingBarGroup.SetActive(false);

        // HP
        enemy.OnHPChanged += (cur, max) =>
        {
            float ratio = (float)cur / max;
            _hpFill.DOFillAmount(ratio, 0.3f);
        };

        // 캐스팅 바
        if (hasSkill)
        {
            enemy.OnSkillCastProgress += progress =>
            {
                _castingBarGroup.SetActive(progress > 0f);
                _castingBarFill.fillAmount = progress;
            };
            enemy.OnSkillCast += () => _castingBarGroup.SetActive(false);
        }

        // 피격
        enemy.OnDamageTaken += _ => PlayHitFlash(new Color(1f, 0.5f, 0.5f));

        // 사망
        enemy.OnDeath += PlayDeathAnim;
    }

    /// <summary>
    /// 터치 → 일점사 타겟 지정 (Collider2D 기반, TargetingSystem 연동).
    /// PuzzleBoardView의 InputLoop과 별도로 BattleSceneView에서 Raycast 처리.
    /// </summary>
    public void SetupTargeting(EnemyState enemy, TargetingSystem targeting)
    {
        // Collider2D 기반 터치는 BattleSceneView에서 Raycast로 처리
        // 여기서는 참조만 보관
    }

    // ── 애니메이션 (기존 로직 유지) ──────────────────────────────────────

    public void PlayAttackAnim()
    {
        transform.DOPunchPosition(Vector3.left * 0.2f, 0.2f, 5, 0.5f);
    }

    public void PlayHitFlash(Color flashColor)
    {
        _spriteRenderer.DOKill(false);
        DOTween.Sequence()
            .Append(_spriteRenderer.DOColor(flashColor, 0.08f).SetEase(Ease.OutQuad))
            .Append(_spriteRenderer.DOColor(_baseColor,  0.12f).SetEase(Ease.InQuad));
    }

    public void PlayAoEHitAnim(Color flashColor)
    {
        transform.DOShakePosition(0.4f, 0.15f, 10, 90f);
        _spriteRenderer.DOKill(false);
        DOTween.Sequence()
            .Append(_spriteRenderer.DOColor(flashColor, 0.1f).SetEase(Ease.OutQuad))
            .Append(_spriteRenderer.DOColor(_baseColor,  0.15f).SetEase(Ease.InQuad));
    }

    public void PlayDeathAnim()
    {
        _deadOverlay.SetActive(true);
        _spriteRenderer.DOKill(false);
        DOTween.Sequence()
            .Append(_spriteRenderer.DOColor(Color.gray, 0.2f))
            .Append(_spriteRenderer.DOFade(0f, 0.4f))
            .OnComplete(() => gameObject.SetActive(false));
    }
}
```

#### 2. EnemyHUDView 제거/비활성화
```
경로: Client/Assets/_Project/Scripts/UI/EnemyHUDView.cs
```
- 기존 EnemyHUDView 클래스와 프리팹은 더 이상 사용하지 않음
- BattleHUD.BindWave()에서 EnemyHUDView 생성 로직 제거

#### 3. BattleHUD.BindWave() 수정
```
경로: Client/Assets/_Project/Scripts/UI/BattleHUD.cs
```
```csharp
/// <summary>
/// 적 HUD는 더 이상 스크린 스페이스에 생성하지 않음.
/// EnemyEntityView 내장 월드 스페이스 HUD로 대체됨.
/// 일점사 타겟팅은 BattleSceneView에서 Raycast로 처리.
/// </summary>
public void BindWave(EnemyWave wave, TargetingSystem targeting)
{
    // EnemyHUDView 프리팹 동적 생성 제거
    // 모든 HUD가 EnemyEntityView 내장으로 이전됨
    // 기존 _enemyHUDPrefab, _enemyHUDContainer 필드는 제거 또는 미사용
}
```

#### 4. BattleSceneView에서 적 터치 → 타겟팅 처리
```
경로: Client/Assets/_Project/Scripts/UI/BattleSceneView.cs
```
기존 EnemyHUDView의 TargetButton 대신 적 스프라이트의 Collider2D를 Raycast로 감지:
```csharp
// SpawnEnemies 내부에서 Collider2D 기반 터치 처리 설정
private void SpawnEnemies(EnemyWave wave)
{
    var enemies = wave.Enemies;
    for (int i = 0; i < enemies.Count; i++)
    {
        // ... 기존 스폰 로직 동일 ...

        // Collider2D가 있으면 터치 타겟팅 가능
        // (실제 터치 감지는 별도 InputAction Raycast로 처리)
    }
}
```

### 완료 조건
- [ ] EnemyHUDView의 PortraitImage(초상화) 제거
- [ ] HP 바가 적 스프라이트 바로 위에 월드 스페이스로 표시
- [ ] 캐스팅 바가 HP 바 하단(또는 스프라이트 위)에 월드 스페이스로 표시
- [ ] 스킬 없는 적은 캐스팅 바 미표시
- [ ] 적 사망 시 DeadOverlay 정상 표시
- [ ] 적 스프라이트 터치로 일점사 타겟 지정 동작
- [ ] 기존 BattleHUD.BindWave()에서 EnemyHUDView 생성 로직 제거

---

## T-D3-010: 유저 데이터 시스템 및 서버 통신 기반 히어로 스폰

### 목표
서버로부터 유저 데이터(보유 히어로, 배치 히어로)를 받아와 전투 시작 시 배치된 히어로를 스폰하는 시스템을 구현한다.

### 작업 내용

#### 1. DTO 정의 (Client + Shared)
```
경로: Client/Assets/_Project/Scripts/Data/UserDataDTO.cs
```
```csharp
using System;
using System.Collections.Generic;

[Serializable]
public class UserDataDTO
{
    public string userId;
    public List<HeroDataDTO> ownedHeroes;
    public List<string> deployedHeroIds;  // 배치된 히어로 ID 목록 (순서 = partyIndex)
}

[Serializable]
public class HeroDataDTO
{
    public string heroId;
    public string name;
    public int maxHP;
    public int attack;
    public int defense;
    public int level;
    public string skillType;          // "SingleAttack", "AoEAttack", "Heal", "Shield", "EnhancedAttack"
    public float baseValuePerBlock;
    public string skillName;
    public float stunDuration;        // AoE 전용, 0이면 미적용
}
```

#### 2. ApiClient 신규 생성
```
경로: Client/Assets/_Project/Scripts/Network/ApiClient.cs
```
```csharp
using System;
using System.Text;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

public class ApiClient
{
    private readonly string _baseUrl;

    public ApiClient(string baseUrl)
    {
        _baseUrl = baseUrl.TrimEnd('/');
    }

    /// <summary>
    /// GET 요청. 성공 시 JSON을 T로 역직렬화하여 반환.
    /// </summary>
    public async UniTask<T> GetAsync<T>(string path)
    {
        string url = $"{_baseUrl}/{path.TrimStart('/')}";
        using var request = UnityWebRequest.Get(url);
        request.SetRequestHeader("Content-Type", "application/json");

        await request.SendWebRequest().ToUniTask();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"[ApiClient] GET {url} failed: {request.error}");
            return default;
        }

        return JsonUtility.FromJson<T>(request.downloadHandler.text);
    }

    /// <summary>
    /// POST 요청.
    /// </summary>
    public async UniTask<TResponse> PostAsync<TRequest, TResponse>(string path, TRequest body)
    {
        string url = $"{_baseUrl}/{path.TrimStart('/')}";
        string json = JsonUtility.ToJson(body);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

        using var request = new UnityWebRequest(url, "POST");
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        await request.SendWebRequest().ToUniTask();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"[ApiClient] POST {url} failed: {request.error}");
            return default;
        }

        return JsonUtility.FromJson<TResponse>(request.downloadHandler.text);
    }
}
```

#### 3. UserDataService 신규 생성
```
경로: Client/Assets/_Project/Scripts/Network/UserDataService.cs
```
```csharp
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 서버에서 유저 데이터를 조회하고, 실패 시 로컬 폴백 JSON을 사용.
/// </summary>
public class UserDataService
{
    private readonly ApiClient _api;
    private const string FALLBACK_PATH = "UserData/default_user";  // Resources 폴백 경로

    public UserDataService(ApiClient api)
    {
        _api = api;
    }

    /// <summary>
    /// 유저 데이터 조회. 서버 실패 시 로컬 JSON 폴백.
    /// </summary>
    public async UniTask<UserDataDTO> FetchUserDataAsync(string userId)
    {
        // 서버 요청 시도
        var data = await _api.GetAsync<UserDataDTO>($"api/user/{userId}");
        if (data != null && data.deployedHeroIds != null && data.deployedHeroIds.Count > 0)
            return data;

        Debug.LogWarning("[UserDataService] 서버 응답 실패, 로컬 폴백 사용");
        return LoadFallback();
    }

    /// <summary>
    /// 로컬 Resources 폴백 JSON 로드.
    /// </summary>
    private UserDataDTO LoadFallback()
    {
        var textAsset = Resources.Load<TextAsset>(FALLBACK_PATH);
        if (textAsset == null)
        {
            Debug.LogError("[UserDataService] 폴백 JSON 없음, 기본 3인 파티 생성");
            return CreateDefaultUserData();
        }
        return JsonUtility.FromJson<UserDataDTO>(textAsset.text);
    }

    private UserDataDTO CreateDefaultUserData()
    {
        return new UserDataDTO
        {
            userId = "local_default",
            ownedHeroes = new List<HeroDataDTO>
            {
                new() { heroId = "hero_warrior",  name = "전사", maxHP = 500, attack = 100, defense = 10,
                         skillType = "SingleAttack", baseValuePerBlock = 10f, skillName = "베기" },
                new() { heroId = "hero_mage",     name = "마법사", maxHP = 450, attack = 90, defense = 15,
                         skillType = "AoEAttack", baseValuePerBlock = 6f, skillName = "번개", stunDuration = 1.5f },
                new() { heroId = "hero_healer",   name = "힐러", maxHP = 400, attack = 80, defense = 20,
                         skillType = "Heal", baseValuePerBlock = 7f, skillName = "치유" },
            },
            deployedHeroIds = new List<string> { "hero_warrior", "hero_mage", "hero_healer" }
        };
    }
}
```

#### 4. HeroFactory 신규 생성
```
경로: Client/Assets/_Project/Scripts/Battle/HeroFactory.cs
```
```csharp
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// UserDataDTO → HeroParty 변환.
/// deployedHeroIds 순서에 따라 partyIndex를 할당.
/// </summary>
public static class HeroFactory
{
    public static HeroParty CreateParty(UserDataDTO userData)
    {
        var heroes = new List<HeroState>();
        var ownedMap = userData.ownedHeroes.ToDictionary(h => h.heroId);

        for (int i = 0; i < userData.deployedHeroIds.Count; i++)
        {
            string heroId = userData.deployedHeroIds[i];
            if (!ownedMap.TryGetValue(heroId, out var dto))
            {
                throw new InvalidDeckException($"Hero '{heroId}' not found in owned heroes");
            }

            var skill = new HeroSkill(
                ParseSkillType(dto.skillType),
                dto.baseValuePerBlock,
                dto.skillName,
                dto.stunDuration
            );

            heroes.Add(new HeroState(
                maxHP: dto.maxHP,
                attack: dto.attack,
                defense: dto.defense,
                level: dto.level > 0 ? dto.level : 1,
                partyIndex: i,
                skill: skill
            ));
        }

        return new HeroParty(heroes);
    }

    private static SkillType ParseSkillType(string type) => type switch
    {
        "SingleAttack"   => SkillType.SingleAttack,
        "AoEAttack"      => SkillType.AoEAttack,
        "Heal"           => SkillType.Heal,
        "Shield"         => SkillType.Shield,
        "EnhancedAttack" => SkillType.EnhancedAttack,
        _ => SkillType.SingleAttack
    };
}
```

#### 5. GameManager.InitializeServices() 리팩터링
```
경로: Client/Assets/_Project/Scripts/Core/GameManager.cs
```
```csharp
private async UniTaskVoid Awake()
{
    if (Instance != null) { Destroy(gameObject); return; }
    Instance = this;
    DontDestroyOnLoad(gameObject);

    await InitializeServicesAsync();
}

private async UniTask InitializeServicesAsync()
{
    // 서버 API 클라이언트 초기화
    var apiClient = new ApiClient("http://localhost:5000");  // 개발 서버 URL
    var userDataService = new UserDataService(apiClient);

    // 유저 데이터 조회 (서버 실패 시 로컬 폴백)
    var userData = await userDataService.FetchUserDataAsync("test_user_001");

    // DTO → HeroParty 변환
    var party = HeroFactory.CreateParty(userData);

    // 적 웨이브 (추후 서버/스테이지 데이터로 교체)
    var enemies = new List<EnemyState>
    {
        new EnemyState(maxHP: 1000, attack: 80,
            skillCooldown: 8f, skillCastTime: 1.5f, skillDamageMultiplier: 3f,
            waveIndex: 0)
    };
    var wave = new EnemyWave(enemies);

    // 보드를 파티 활성 색상으로 초기화
    var activeColors = party.GetActiveColors();
    var board = new Board();
    board.Initialize(activeColors);

    var controller = new BoardController(board, activeColors);
    ServiceLocator.Register(controller);

    PuzzleBoardView.Initialize(controller);

    _battleManager = new BattleManager();
    _battleManager.StartBattle(party, wave, controller);

    _battleHUD.BindParty(party, _battleManager.UltManager, _battleManager);
    _battleHUD.BindWave(wave, _battleManager.Targeting);
    _battleHUD.BindCombo(ServiceLocator.Get<ComboCalculator>());

    _battleSceneView.Bind(party, wave,
        ServiceLocator.Get<SkillSystem>(),
        _battleManager.Targeting);
}
```

#### 6. 로컬 폴백 JSON 생성
```
경로: Client/Assets/_Project/Resources/UserData/default_user.json
```
```json
{
    "userId": "local_default",
    "ownedHeroes": [
        {
            "heroId": "hero_warrior",
            "name": "전사",
            "maxHP": 500,
            "attack": 100,
            "defense": 10,
            "level": 1,
            "skillType": "SingleAttack",
            "baseValuePerBlock": 10.0,
            "skillName": "베기",
            "stunDuration": 0
        },
        {
            "heroId": "hero_mage",
            "name": "마법사",
            "maxHP": 450,
            "attack": 90,
            "defense": 15,
            "level": 1,
            "skillType": "AoEAttack",
            "baseValuePerBlock": 6.0,
            "skillName": "번개",
            "stunDuration": 1.5
        },
        {
            "heroId": "hero_healer",
            "name": "힐러",
            "maxHP": 400,
            "attack": 80,
            "defense": 20,
            "level": 1,
            "skillType": "Heal",
            "baseValuePerBlock": 7.0,
            "skillName": "치유",
            "stunDuration": 0
        }
    ],
    "deployedHeroIds": ["hero_warrior", "hero_mage", "hero_healer"]
}
```

#### 7. 서버 프로젝트 스캐폴딩 (최소 MVP)
```
경로: Server/
```
.NET 8 Minimal API 프로젝트 생성:

```csharp
// Server/Program.cs (최소 MVP — 인메모리 데이터)
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// 인메모리 유저 데이터 (추후 Dapper + DB로 교체)
var userData = new
{
    userId = "test_user_001",
    ownedHeroes = new[]
    {
        new { heroId = "hero_warrior", name = "전사", maxHP = 500, attack = 100, defense = 10,
              level = 1, skillType = "SingleAttack", baseValuePerBlock = 10f, skillName = "베기", stunDuration = 0f },
        new { heroId = "hero_mage", name = "마법사", maxHP = 450, attack = 90, defense = 15,
              level = 1, skillType = "AoEAttack", baseValuePerBlock = 6f, skillName = "번개", stunDuration = 1.5f },
        new { heroId = "hero_healer", name = "힐러", maxHP = 400, attack = 80, defense = 20,
              level = 1, skillType = "Heal", baseValuePerBlock = 7f, skillName = "치유", stunDuration = 0f },
        new { heroId = "hero_knight", name = "기사", maxHP = 550, attack = 85, defense = 25,
              level = 1, skillType = "Shield", baseValuePerBlock = 8f, skillName = "방어막", stunDuration = 0f },
        new { heroId = "hero_assassin", name = "암살자", maxHP = 380, attack = 120, defense = 5,
              level = 1, skillType = "EnhancedAttack", baseValuePerBlock = 9f, skillName = "강타", stunDuration = 0f },
    },
    deployedHeroIds = new[] { "hero_warrior", "hero_mage", "hero_healer" }
};

app.MapGet("/api/user/{userId}", (string userId) =>
{
    return Results.Ok(userData);
});

app.Run();
```

### 동작 시나리오
```
1. 게임 시작 → GameManager.InitializeServicesAsync()
2. ApiClient → GET /api/user/test_user_001
3. 서버 응답 성공 → UserDataDTO 파싱
   (서버 미응답 → 로컬 default_user.json 폴백)
4. HeroFactory.CreateParty(userData) → deployedHeroIds 순서대로 HeroState 생성
   - "hero_warrior"  → PartyIndex 0 (Red)
   - "hero_mage"     → PartyIndex 1 (Yellow)
   - "hero_healer"   → PartyIndex 2 (Green)
5. HeroParty(3명) → Board 3색 초기화 → 전투 시작
```

### 완료 조건
- [ ] ApiClient GET/POST 요청 정상 동작 (async/await)
- [ ] UserDataService: 서버 응답 성공 시 서버 데이터 사용
- [ ] UserDataService: 서버 실패 시 로컬 JSON 폴백 정상 로드
- [ ] HeroFactory: deployedHeroIds 순서대로 partyIndex 할당
- [ ] HeroFactory: SkillType 문자열 → enum 파싱 정상
- [ ] GameManager: 하드코딩 히어로 → 서버 데이터 기반 히어로 스폰으로 전환
- [ ] 로컬 폴백 JSON으로 기존과 동일한 3인 파티 전투 정상 동작
- [ ] 서버 프로젝트: GET /api/user/{userId} 엔드포인트 응답 정상
