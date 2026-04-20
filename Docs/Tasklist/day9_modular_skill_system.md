# Day 9 - 확장형 모듈식 스킬 시스템

기존 단일 ActionType/TargetScope 기반 스킬 구조를 GDD v1.0 **'확장형 모듈식 스킬 시스템'**에 맞춰
**Actions[] 배열 + Target 객체(Team/Strategy/MaxCount)** 기반 모듈형으로 전환한다.
하나의 스킬이 여러 개의 독립 행동을 순차 실행하며, 8종 타겟팅 알고리즘을 통해
**코드 수정 없이 데이터만으로** 복합 기믹(딜탱 복합기, 조건부 힐링 등)을 구현할 수 있도록 한다.

---

## 현재 상태 분석

| 항목 | GDD 목표 | 현재 구현 | Gap |
|------|---------|----------|-----|
| 스킬 데이터 구조 | Actions[] 배열 (멀티 액션) | 스킬당 단일 ActionType + TargetScope | 플랫 → 모듈형 전환 필요 |
| 타겟 객체 | Target { Team, Strategy, MaxCount, TargetIndex } | TargetScope enum (Single/Multi/All) | Target 객체 도입 필요 |
| 타겟팅 알고리즘 | 8종 (Front/Back/LowestHP/HighestAtk/Self/FixedIndex/All/Random) | Front 우선순위만 존재 | 7종 알고리즘 신규 구현 |
| 스킬 실행 | Actions 배열 순차 실행 + 액션별 타겟팅 | 단일 ActionType switch 분기 | 순차 실행 엔진 재설계 |
| 타겟 유효성 | 각 Action 직전 재검증 | 없음 | 실행 시점 유효성 검증 추가 |
| 복합 스킬 | 딜+힐, 탱+버프 등 조합 | 불가능 (1스킬 = 1액션) | 멀티 액션 구조로 해결 |

---

## 태스크 목록

### T-D9-001: SkillData.json 스키마 전환 — Actions 배열 + Target 객체 (High)

기존 플랫 구조를 GDD v1.0 모듈형 스키마로 전환한다.

- 기존 각 스킬의 `ActionType, TargetScope, MaxTargetCount, BaseMultiplier, StatusEffects`를
  `Actions[]` 배열 내 개별 Action 객체 + `Target` 객체로 이동
- TargetScope → Target 매핑 규칙:
  | 기존 | 변환 후 Team | 변환 후 Strategy | MaxCount |
  |------|-------------|-----------------|----------|
  | Attack + Single | Enemy | Front | 1 |
  | Attack + Multi | Enemy | Front | N |
  | Attack + All | Enemy | All | 0 |
  | Heal + Single | Ally | Self | 1 |
  | Heal + Multi | Ally | LowestHP | N |
  | Heal + All | Ally | All | 0 |
  | Shield + Single | Ally | Self | 1 |
  | Shield + All | Ally | All | 0 |
  | Buff + All | Ally | All | 0 |
- 기존 10종 스킬(5001~5010) 전부 변환
- `EffectPrefabPath`는 스킬 루트에 유지
- 의존: 없음

---

### T-D9-002: SkillDataRepository 모듈형 데이터 클래스 재설계 (High)

직렬화 클래스를 신규 스키마에 맞춰 재설계한다.

- 기존 `SkillData`에서 `ActionType, TargetScope, MaxTargetCount, BaseMultiplier, StatusEffects` 필드 제거
- 신규 직렬화 클래스 추가:
  ```
  [Serializable] TargetData { string Team; string Strategy; int MaxCount; int TargetIndex; }
  [Serializable] ActionData { string ActionType; TargetData Target; float BaseMultiplier; List<StatusEffectData> StatusEffects; }
  ```
- `SkillData`에 `List<ActionData> Actions` 필드 추가
- 기존 `StatusEffectData` 변경 없이 유지
- Repository Load/GetById/GetAll 로직 변경 불필요 (JsonUtility 자동 역직렬화)
- 의존: 없음

---

### T-D9-003: TargetStrategy enum + TargetingSystem 8종 알고리즘 구현 (High)

GDD 3절의 타겟팅 알고리즘 8종을 구현한다.

- `HeroSkill.cs`에 enum 추가:
  ```csharp
  public enum TargetTeam { Ally, Enemy }
  public enum TargetStrategy { Front, Back, LowestHP, HighestAtk, Self, FixedIndex, All, Random }
  ```
- `TargetingSystem.cs`에 통합 Resolve 메서드 추가:
  - **Front**: Index 오름차순 → maxCount개
  - **Back**: Index 내림차순 → maxCount개
  - **LowestHP**: HP 비율 오름차순 → maxCount개
  - **HighestAtk**: Attack 내림차순 → maxCount개
  - **Self**: 시전자 본인 (호출측 주입)
  - **FixedIndex**: 지정 인덱스 대상, 사망 시 빈 리스트
  - **All**: 전체 생존 대상
  - **Random**: UnityEngine.Random 기반 maxCount개 셔플 선택
- 기존 `GetPriorityTarget` / `GetHeroTarget` 유지 (내부적으로 Resolve 호출하도록 리팩터링)
- 의존: 없음

---

### T-D9-004: HeroSkill 멀티 액션 구조 전환 (High)

HeroSkill을 단일 액션에서 Actions 리스트 기반으로 전환한다.

- 신규 `SkillAction` 클래스:
  ```csharp
  public class SkillAction {
      public ActionType ActionType;
      public TargetTeam Team;
      public TargetStrategy Strategy;
      public int MaxCount;
      public int TargetIndex;
      public float BaseMultiplier;
      public List<StatusEffectData> StatusEffects;
  }
  ```
- `HeroSkill` 변경:
  - 기존 `ActionType, TargetScope, BaseMultiplier, MaxTargetCount, StatusEffects` 제거
  - `List<SkillAction> Actions` 추가
  - 하위 호환 프로퍼티: `ActionType => Actions[0].ActionType` (전환기 최소 수정용)
- 의존: T-D9-003

---

### T-D9-005: DamageCalculator / SkillEffect 액션 단위 계산 전환 (High)

계산 단위를 스킬 → 액션으로 변경한다.

- `SkillEffect` 변경:
  - `TargetScope` 제거 → `TargetTeam Team`, `TargetStrategy Strategy`, `int MaxCount`, `int TargetIndex` 추가
  - `SkillAction SourceAction` 참조 추가 (StatusEffects 접근용)
- `DamageCalculator.Calculate` 시그니처 변경:
  ```csharp
  public static SkillEffect Calculate(SkillAction action, BlockType sourceColor,
      int totalBlockCount, float comboMultiplier, int heroAttack)
  ```
- 기존 HeroSkill 기반 Calculate는 `Actions[0]` 위임 브릿지 유지 (전환기)
- `BattleSceneView.OnSkillExecuted` TargetScope → Strategy 기반 분기로 수정
- 의존: T-D9-004

---

### T-D9-006: SkillSystem 순차 멀티 액션 실행 엔진 (High)

SkillSystem을 Actions 배열 순차 실행 방식으로 전면 재설계한다.

- 핵심 실행 흐름:
  ```
  foreach (action in skill.Actions)
      1. 타겟 후보 결정 (Team → Ally/Enemy 리스트)
      2. TargetingSystem.Resolve(candidates, strategy, maxCount, fixedIndex)
      3. 각 Action 직전 타겟 유효성 재검증 (GDD 5절)
      4. DamageCalculator.Calculate(action, ...)
      5. ActionType별 효과 적용 (TakeDamage/Heal/AddShield/ApplyStatusEffect)
      6. StatusEffects 해당 타겟에 적용
      7. OnSkillExecuted 이벤트 발행
  ```
- 기존 `ExecuteAttack/ExecuteHeal/ExecuteShield/ExecuteBuff` → 새 구조에 맞게 통합
- `ExecuteFromCascade` / `ExecuteUniqueSkill` / `ExecuteUltimateSkill` 동일 패턴 적용
- 의존: T-D9-003, T-D9-004, T-D9-005

---

### T-D9-007: HeroFactory 신규 스키마 파싱 (High)

BuildSkill 메서드를 신규 모듈형 스키마에 맞춰 수정한다.

- `SkillData.Actions` 배열 순회 → 각 `ActionData` → `SkillAction` 변환:
  - `ActionData.ActionType` → `Enum.TryParse<ActionType>`
  - `ActionData.Target.Team` → `Enum.TryParse<TargetTeam>`
  - `ActionData.Target.Strategy` → `Enum.TryParse<TargetStrategy>`
  - `MaxCount`, `TargetIndex`, `BaseMultiplier`, `StatusEffects` 복사
- `HeroSkill` 생성자에 `List<SkillAction>` + `Name` 전달
- 기존 ActionType/TargetScope 파싱 로직 제거
- 의존: T-D9-002, T-D9-004

---

### T-D9-008: 기존 참조 코드 호환성 수정 (High)

외부에서 기존 필드를 참조하는 코드를 전수 수정한다.

- `BattleSceneView.OnSkillExecuted`: `SkillEffect.TargetScope` → `Strategy/Team` 기반 분기
- `BattleManager`: ExecuteUniqueSkill/ExecuteUltimateSkill 호출부 대응
- `SkillSystem.ExecuteFromCascade`: 신규 ExecuteSkill 패턴 적용
- 기존 `TargetScope` enum 제거 또는 `[Obsolete]` 마킹
- 컴파일 오류 전수 확인 및 수정
- 의존: T-D9-006

---

### T-D9-009: GDD 예시 복합 스킬 데이터 추가 (Medium)

GDD 4절의 예시 스킬 2종을 추가하여 멀티 액션을 검증한다.

- **6001 '성녀의 가호'** (복합 지원):
  - Action 0: Heal / Ally / FixedIndex(0) / MaxCount 1 / Multiplier 2.5
  - Action 1: Buff / Ally / Self / StatusEffects [DefUp 0.5, 10s, 100%]
- **6002 '흡혈의 일격'** (공방 일체):
  - Action 0: Attack / Enemy / Front / MaxCount 2 / Multiplier 2.0
  - Action 1: Heal / Ally / LowestHP / MaxCount 1 / Multiplier 1.5
- HeroData.json에서 테스트용 히어로 스킬 ID를 6001/6002로 교체하여 실전 검증
- 의존: T-D9-006, T-D9-007

---

### T-D9-010: 서버 API 스키마 동기화 (Medium)

서버 인메모리 스킬 데이터를 클라이언트 신규 스키마와 동기화한다.

- Program.cs 스킬 DTO 변경:
  - 기존 `SkillMasterDto`에서 `ActionType, TargetScope, MaxTargetCount, BaseMultiplier` 제거
  - 신규 `TargetDto`, `ActionDto` record 추가
  - `SkillMasterDto`에 `List<ActionDto> Actions` 추가
- `skillDataDb` 인메모리 리스트를 신규 스키마로 전환 (기존 10종 + 예시 2종)
- `GET /api/skills` 응답 검증
- 의존: T-D9-001

---

## 의존성 그래프

```
T-D9-001 (SkillData.json 스키마 전환)    ← 독립
    └── T-D9-010 (서버 API 동기화)

T-D9-002 (SkillDataRepository 재설계)    ← 독립
    └── T-D9-007 (HeroFactory 파싱)

T-D9-003 (TargetStrategy + TargetingSystem) ← 독립
    ├── T-D9-004 (HeroSkill 멀티 액션)
    │       ├── T-D9-005 (DamageCalculator 전환)
    │       │       └── T-D9-006 (SkillSystem 실행 엔진)
    │       │               ├── T-D9-008 (호환성 수정)
    │       │               └── T-D9-009 (복합 스킬 검증)
    │       └── T-D9-007 (HeroFactory 파싱)
    └── T-D9-006 (SkillSystem 실행 엔진)
```

## 작업 순서 권장

1. **Phase A — 기반 작업 (병렬 가능)**
   - T-D9-001 (SkillData.json 스키마 전환)
   - T-D9-002 (SkillDataRepository 재설계)
   - T-D9-003 (TargetStrategy + TargetingSystem 8종)

2. **Phase B — 코어 모델 전환**
   - T-D9-004 (HeroSkill 멀티 액션)
   - T-D9-005 (DamageCalculator 액션 단위)

3. **Phase C — 실행 엔진 + 연결**
   - T-D9-006 (SkillSystem 순차 실행)
   - T-D9-007 (HeroFactory 파싱)
   - T-D9-008 (호환성 수정)

4. **Phase D — 검증 + 서버**
   - T-D9-009 (복합 스킬 검증)
   - T-D9-010 (서버 API 동기화)
