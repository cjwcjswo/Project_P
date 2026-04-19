# Day 7 - 히어로 직업 및 등급별 고유 스킬 시스템

GDD **'히어로 직업 및 고유 스킬 1_0'** 기반으로 4종 직업(Class) 시스템, 등급별(1~3성) 멀티 스킬 트리거 구조,
2성 특수 블록(고유 스킬 블록) 생성/탭 발동, 3성 궁극기 컷인 연출을 구현한다.
또한 Day 6에서 하위호환용으로 유지했던 **레거시 코드를 전면 제거**한다.

---

## 현재 상태 분석

| 항목 | GDD 요구사항 | 현재 구현 | Gap |
|------|-------------|----------|-----|
| 직업(Class) | Warrior/Mage/Assassin/Healer 4종 | HeroData에 Class 필드 없음 | HeroClass enum + 데이터 확장 필요 |
| 블록 직업 아이콘 | BlockView 중앙에 직업 아이콘 오버레이 | BlockView에 아이콘 없음 | SpriteRenderer 자식 + 아이콘 매핑 |
| 등급별 스킬 | MatchSkillId / UniqueSkillId / UltimateSkillId 3종 | 단일 SkillId만 존재 | HeroData 스키마 변경 + HeroState 멀티 스킬 |
| Grade 범위 | 1~3성 | 현재 2~4 (에이다4, 레이4) | Grade 값 재조정 필요 |
| 2성 특수 블록 | 4매칭 시 고유 스킬 블록 생성, 탭 발동 → 십자 파괴 + UniqueSkill | 미구현 | Board/Matcher/BlockView 확장 필요 |
| 3성 궁극기 | UltimateSkill 기반 발동 + 일러스트 컷인 | UltGauge 존재, 고정 데미지(attack*3) | SkillData 연동 + 컷인 연출 |
| 레거시 코드 | 제거 대상 | SkillType enum, legacy 생성자, DefaultSkill, FormerlySerializedAs 잔존 | 전면 삭제 |
| IllustrationPath | 3성 컷인 일러스트 경로 | HeroData에 없음 | 필드 추가 |

---

## 태스크 목록

### T-D7-001: 레거시 코드 제거 — SkillType enum 및 하위호환 생성자 삭제 (High)

Day 6에서 하위호환용으로 유지했던 레거시 코드를 전면 제거:

- `HeroSkill.cs`: `SkillType` enum 삭제, SkillType 기반 하위호환 생성자 삭제
- `HeroState.cs`: `DefaultSkill(int partyIndex)` 메서드 삭제, 생성자에서 `skill ?? DefaultSkill()` → `skill` 직접 할당
- `BattleSceneView.cs`: `[FormerlySerializedAs("_floatingTextPrefab")]` 어트리뷰트 삭제
- 전체 Grep으로 `SkillType` 참조 잔재 검증
- 의존: 없음

### T-D7-011: HeroData.json Grade 값 정합성 수정 (High)

현재 HeroData.json에 Grade 4가 존재하나 GDD 기준 1~3 범위로 재조정:

- 10002 에이다: 4 → 2
- 10004 크롬: 2 → 1
- 10005 레이: 4 → 3
- 의존: 없음

### T-D7-002: 직업(Class) 시스템 — HeroClass enum 및 데이터 확장 (High)

- `HeroClass` enum 신규: Warrior, Mage, Assassin, Healer
- `HeroData` 클래스에 `string Class` 필드 추가
- `HeroData.json`: 각 히어로별 Class 값 추가
- `HeroState`에 `HeroClass Class` 프로퍼티 추가
- `HeroFactory.Create()`: Class 파싱 및 전달
- 의존: T-D7-001

### T-D7-003: 등급별 멀티 스킬 데이터 구조 전환 (High)

- `HeroData`: `SkillId` → `MatchSkillId`, `UniqueSkillId`, `UltimateSkillId` 교체
- `HeroData.json`: 히어로별 3종 스킬 ID 할당 (미사용 스킬은 0)
- `SkillData.json`: UniqueSkill·UltimateSkill 데이터 추가 (5006~5010)
- `HeroData`에 `IllustrationPath` 필드 추가
- `HeroState`: `Skill` 단일 → `MatchSkill`, `UniqueSkill`, `UltimateSkill` 3종
- `HeroFactory.Create()`: 3종 스킬 각각 빌드 및 전달
- 의존: T-D7-002

### T-D7-004: SkillSystem/BattleSceneManager 멀티 스킬 대응 (High)

- `SkillSystem.ExecuteFromCascade()`: `hero.Skill` → `hero.MatchSkill` 참조 변경
- `BattleSceneManager`: `heroData.SkillId` → `heroData.MatchSkillId` 변경
- 컴파일 오류 전체 점검
- 의존: T-D7-003

### T-D7-005: 2성 특수 블록(고유 스킬 블록) 생성 시스템 (High)

- Board 모델: 특수 블록 상태 관리 (IsSkillBlock 플래그 또는 별도 BlockType)
- Matcher/BoardController: 4개 이상 매칭 감지 → 중심 위치에 특수 블록 생성
- Grade >= 2 && UniqueSkillId > 0 조건 검증
- 특수 블록 생성 이벤트 발행
- 의존: T-D7-004

### T-D7-006: 2성 특수 블록 시각화 및 탭 발동 (High)

- `BlockView`: 특수 블록용 파티클/아우라 이펙트 활성화
- `PuzzleBoardView`: 특수 블록 탭 입력 감지 (스왑 대신 탭 처리)
- 탭 발동: 십자 모양 블록 파괴 + UniqueSkill 즉시 발동
- `SkillSystem`: `ExecuteUniqueSkill(HeroState hero)` 메서드 추가
- 의존: T-D7-005

### T-D7-007: 3성 궁극기 시스템 리팩터링 — UltimateSkill 연동 (High)

- `UltimateGaugeManager`: Grade >= 3만 게이지 충전 가능
- `BattleManager.ActivateUltimateAsync()`: `heroAttack * 3` → UltimateSkill 기반 SkillSystem 경유
- `HeroHUDView`: Grade < 3 히어로는 궁극기 게이지 UI 숨김
- 의존: T-D7-004

### T-D7-008: 3성 궁극기 컷인(Cut-in) 연출 (Medium)

- `CutInView.cs` 신규: 일러스트 좌→우 Fade in-out 연출 컴포넌트
- `HeroData.IllustrationPath` → Resources 일러스트 Sprite 로드
- DOTween 기반: CanvasGroup alpha 0→0.8→0, 위치 이동
- `BattleManager.ActivateUltimateAsync()` 내 컷인 재생
- 의존: T-D7-007

### T-D7-009: BlockView 직업 아이콘 오버레이 (Medium)

- `BlockView`: `_classIconRenderer` SpriteRenderer 자식 오브젝트 SerializeField
- HeroColorMap으로 블록-히어로 매핑 → 히어로 직업 조회
- HeroClass별 아이콘 Sprite 매핑 (인스펙터)
- 매핑된 히어로 없는 블록은 아이콘 비활성화
- 의존: T-D7-002

### T-D7-010: 서버 API 업데이트 — Class/멀티 스킬 대응 (Low)

- heroDataDb: Class, MatchSkillId/UniqueSkillId/UltimateSkillId, IllustrationPath
- HeroMasterDto 스키마 갱신
- GET /api/heroes, GET /api/skills 응답 갱신
- 의존: T-D7-003

---

## 의존성 그래프

```
T-D7-001 (레거시 제거)      T-D7-011 (Grade 정합성)
    │                            │
    ├── T-D7-002 (Class 시스템) ──┘
    │        │
    │        ├── T-D7-009 (블록 직업 아이콘)
    │        │
    │        └── T-D7-003 (멀티 스킬 데이터)
    │                 │
    │                 ├── T-D7-010 (서버 API)
    │                 │
    │                 └── T-D7-004 (SkillSystem 연동)
    │                          │
    │                 ┌────────┼────────┐
    │                 │        │        │
    │           T-D7-005    T-D7-007   │
    │          (특수블록)    (궁극기)    │
    │              │           │        │
    │           T-D7-006    T-D7-008   │
    │          (탭발동)     (컷인)      │
    │                                   │
    └───────────────────────────────────┘
```

## 작업 순서 권장

1. **T-D7-001** + **T-D7-011** (병렬 — 레거시 제거 + Grade 정합성)
2. **T-D7-002** (직업 시스템)
3. **T-D7-003** (멀티 스킬 데이터 구조)
4. **T-D7-004** (SkillSystem 연동)
5. **T-D7-005** → **T-D7-006** (2성 특수 블록 생성 → 탭 발동)
6. **T-D7-007** → **T-D7-008** (3성 궁극기 리팩터링 → 컷인 연출)
7. **T-D7-009** (블록 직업 아이콘)
8. **T-D7-010** (서버 스텁)
