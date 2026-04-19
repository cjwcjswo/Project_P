# Day 6 - Data-Driven Hero & Skill System

GDD **'히어로 및 스킬 1_0'** 기반으로 데이터 주도 히어로/스킬 시스템을 구현한다.
기존 하드코딩된 SkillType enum 체계를 GDD의 **ActionType + TargetScope 조합형**으로 전환하고,
**StatusEffect 런타임 시스템**과 **레벨/EXP 성장 공식**을 적용한다.

---

## 현재 상태 분석

| 항목 | GDD 요구사항 | 현재 구현 | Gap |
|------|-------------|----------|-----|
| 히어로 데이터 | HeroData.json + Repository | 서버 DTO(HeroDataDTO) 기반, 로컬 JSON 없음 | HeroData.json + HeroDataRepository 신설 필요 |
| 스킬 데이터 | SkillData.json + Repository | HeroSkill 클래스 하드코딩 | SkillData.json + SkillDataRepository 신설 필요 |
| 스킬 분류 | ActionType + TargetScope 조합 | SkillType enum 5종 고정 | 조합형 모델로 전환 필요 |
| 상태 이상 | StatusEffects 배열 (Stun/Burn/AtkUp/DefDown) | AoE에 Stun만 하드코딩 | StatusEffect 시스템 전체 신설 필요 |
| 레벨 성장 | BaseStat + (Lv-1)*GrowthStat, EXP 공식 | HeroState.Level 존재하나 성장 로직 없음 | EXP 누적/레벨업/스탯 재계산 구현 필요 |
| 만렙 | 50Lv | 없음 | 만렙 캡 적용 필요 |

---

## 태스크 목록

### T-D6-001: SkillData.json + SkillDataRepository (High)
- `Resources/SkillData.json` 생성 (GDD 3.1 스키마)
  - 필드: Id, DisplayName, EffectPrefabPath, ActionType, TargetScope, MaxTargetCount, BaseMultiplier, StatusEffects[]
  - 샘플 5종: 회전베기(Attack/Multi), 힐링라이트(Heal/Single), 수호의방패(Shield/Single), 전투함성(Buff/All), 정의의일격(Attack/Single)
- `SkillDataRepository.cs` 신규 (MonsterDataRepository 패턴)
- 의존: 없음

### T-D6-002: HeroData.json + HeroDataRepository (High)
- `Resources/HeroData.json` 생성 (GDD 2.1 스키마)
  - 필드: Id, DisplayName, PrefabPath, Grade, SkillId, BaseStats, GrowthStats, AutoAttackInterval, AutoAttackRatio
  - 샘플 5종 (파티 슬롯 0~4 매핑)
- `HeroDataRepository.cs` 신규 (Dictionary 캐시, GetById/GetAll)
- 의존: T-D6-001 (SkillId 참조)

### T-D6-003: 스킬 모델 리팩터링 - ActionType + TargetScope (High)
- 기존 `SkillType` enum → `ActionType` enum (Attack/Heal/Shield/Buff) + `TargetScope` enum (Single/Multi/All) 분리
- `HeroSkill` 클래스 재설계: BaseMultiplier, MaxTargetCount, StatusEffects 포함
- `SkillEffect` struct 업데이트
- 의존: T-D6-001

### T-D6-004: StatusEffect 런타임 시스템 (High)
- `StatusEffectType` enum: Stun, Burn, AtkUp, DefDown 등
- `StatusEffectInstance` 클래스: type, value, remainingDuration
- `StatusEffectHandler` 클래스: Apply/Tick/Remove 로직
- EnemyState/HeroState에 ActiveEffects 리스트, ApplyStatusEffect(), TickEffects(dt), RemoveExpiredEffects()
- Probability 기반 발동 판정
- 의존: T-D6-003

### T-D6-005: SkillSystem / DamageCalculator 리팩터링 (High)
- `SkillSystem.ExecuteSkill()`: ActionType + TargetScope 조합 분기
  - Attack-Single: 우선 타겟 1체 / Attack-Multi: MaxTargetCount체 / Attack-All: 전체
  - Heal-Single: 시전자 / Heal-All: 전체 아군
  - Shield-Single: 시전자 / Buff-All: 전체 아군 버프
- StatusEffects 배열 순회 → Probability 판정 → ApplyStatusEffect
- `DamageCalculator`: BaseMultiplier 기반 계산
- 의존: T-D6-003, T-D6-004

### T-D6-006: 히어로 레벨/EXP 성장 시스템 (High)
- HeroState에 CurrentEXP, TotalEXP 필드 추가
- 레벨업 필요 EXP = `100 * Level^2 * 1.1^(Level/10)`
- 스탯 성장: `현재스탯 = BaseStat + (Level - 1) * GrowthStat`
- `AddEXP(int amount)`: 경험치 누적 → 자동 레벨업 (만렙 50) → 스탯 재계산
- `OnLevelUp` 이벤트
- 의존: T-D6-002

### T-D6-007: HeroFactory 리팩터링 - Repository 기반 생성 (High)
- `HeroFactory.Create(int heroDataId, int level, int partyIndex)` 신규 시그니처
  - HeroDataRepository → HeroData 조회
  - SkillId → SkillDataRepository → SkillData 조회
  - GrowthStats 적용 스탯 계산 → HeroState 생성
- 기존 DTO 기반 `Create(HeroDataDTO, int)` 하위호환 유지
- BattleSceneManager 히어로 파티 구성 Repository 기반 전환
- 의존: T-D6-002, T-D6-003, T-D6-006

### T-D6-008: BattleManager StatusEffect Tick 루프 통합 (Medium)
- 전투 루프에 `TickEffects(deltaTime)` 호출 통합
- Burn 도트 데미지, 버프 만료 등 지속 효과 처리
- 기존 ApplyStun/RemoveStun → StatusEffect 시스템 통합 대체
- 의존: T-D6-004, T-D6-005

### T-D6-009: GameManager 초기화 등록 (Medium)
- `InitializeCoreServicesAsync()`에서 HeroDataRepository, SkillDataRepository 생성
- ServiceLocator 등록 (MonsterDataRepository/StageDataRepository 동일 패턴)
- 의존: T-D6-001, T-D6-002

### T-D6-010: 서버 히어로/스킬 API 스텁 (Low)
- `GET /api/heroes` — 전체 히어로 목록
- `GET /api/skills` — 전체 스킬 목록
- `POST /api/hero/levelup` — 레벨업 스텁 (userId, heroId, expAmount → newLevel, newStats)
- 의존: T-D6-001, T-D6-002

---

## 의존성 그래프

```
T-D6-001 (SkillData)
  ├── T-D6-002 (HeroData) ── T-D6-006 (Level/EXP)
  │                                  │
  ├── T-D6-003 (스킬 모델) ─────────┤
  │        │                         │
  │        ├── T-D6-004 (StatusEffect)
  │        │        │                │
  │        └── T-D6-005 (SkillSystem 리팩터링)
  │                 │                │
  │                 └── T-D6-008 (Tick 루프)
  │                                  │
  ├── T-D6-009 (GameManager 등록)    │
  ├── T-D6-010 (서버 스텁)           │
  └────────────── T-D6-007 (HeroFactory 리팩터링)
```

## 작업 순서 권장

1. **T-D6-001** → **T-D6-002** (데이터 레이어 우선)
2. **T-D6-003** (스킬 모델 전환)
3. **T-D6-004** (StatusEffect 시스템)
4. **T-D6-006** (레벨/EXP 성장)
5. **T-D6-005** (SkillSystem 리팩터링)
6. **T-D6-007** (HeroFactory 리팩터링)
7. **T-D6-009** → **T-D6-008** → **T-D6-010** (통합/서버)
