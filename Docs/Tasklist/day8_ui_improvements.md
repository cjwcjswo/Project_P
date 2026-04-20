# Day 8 - UI/UX 개선 및 스킬 블록 버그 수정

전투 HUD 정보 강화(스테이지/웨이브 표시, 체력 텍스트, 쿨다운 UI, 콤보 배율 표시) 및
2성 고유 스킬 블록의 **매칭 제거 시 십자 파괴 미발동 버그 수정**과 **탭 발동 애니메이션 개선**을 수행한다.

---

## 현재 상태 분석

| 항목 | 요구사항 | 현재 구현 | Gap |
|------|---------|----------|-----|
| 스테이지/웨이브 표시 | 화면에서 현재 진행 위치 확인 가능 | HUD에 없음 | BattleHUD + SetStageWave() 추가 필요 |
| 스킬 블록 매칭 파괴 | 매칭 제거 시 십자 파괴 발동 | 탭 시에만 발동, 매칭 제거 시 미발동 | BoardController 루프에 십자 파괴 로직 추가 |
| 스킬 블록 탭 애니메이션 | 자연스러운 십자 파괴 연출 | 어색한 즉시 제거 | 파문형 순차 AnimateDestroy 구현 |
| 히어로 HP 수치 | 'cur / max' 텍스트 표시 | HP 바 비율만 표시 | HeroHUDView에 TextMeshProUGUI 추가 |
| 적 스킬 쿨다운 | 잔여 쿨다운 시간 표시 | 캐스팅 진행 시에만 바 표시 | EnemyHUDView에 쿨다운 텍스트 추가 |
| 콤보 배율 표시 | 현재 콤보 데미지 배율 시각화 | 콤보 카운터만 표시 | BattleHUD 콤보 배율 텍스트 추가 |

---

## 태스크 목록

### T-D8-001: 스테이지/웨이브 현황 표시 UI 추가 (Medium)

유저가 현재 진행 중인 스테이지 번호와 웨이브 번호를 전투 화면에서 확인할 수 있도록 HUD를 확장한다.

- `BattleHUD.cs`: `[Header("Stage/Wave")]` + `_stageWaveText` (TextMeshProUGUI) SerializeField 추가
- `SetStageWave(int stage, int wave)` 공개 메서드 추가 → `"Stage N - Wave M"` 포맷 텍스트 갱신
- `BattleSceneManager`: 전투 초기화 시 `battleHUD.SetStageWave(stageId, waveIndex)` 호출
- 웨이브 전환 시점에 텍스트 갱신 연동
- 텍스트 위치: Canvas 상단 중앙 (앵커 top-center)
- 의존: 없음

---

### T-D8-002: 스킬 블록 매칭 제거 시 십자 파괴 미발동 버그 수정 (High)

일반 매칭으로 스킬 블록이 제거될 때 십자 범위 파괴가 발동하지 않는 버그를 수정한다.

- `BoardController.TrySwapAsync()`: 매칭 루프에서 clearPositions 산출 후 스킬 블록 위치 필터링
  ```
  var skillBlocksInMatch = clearPositions.Where(p => _board.IsSkillBlock(p.col, p.row)).ToList();
  ```
- 각 스킬 블록에 대해 `ClearCrossPattern` 실행 → 결과 좌표를 clearPositions에 합산(중복 제거)
- `GravityRefillEvent.PreGravityClearedCells`에 십자 파괴 좌표 포함하여 View에 전달
- `ProcessCascadeAsync()`에도 동일 로직 적용 (궁극기/DisableColor 후 캐스케이드 대응)
- `Events.cs`: 필요 시 `SkillBlockMatchedEvent` 신규 추가 또는 기존 이벤트 재활용
- 의존: 없음

---

### T-D8-003: 스킬 블록 탭 발동 십자 파괴 애니메이션 개선 (Medium)

유저가 스킬 블록을 탭하여 십자 범위가 파괴될 때 연출을 자연스럽게 개선한다.

- `Events.cs`: `SkillBlockCrossDestroyEvent { List<(int col, int row)> Positions }` 신규 추가
- `BattleManager`: 십자 파괴 좌표 목록을 `SkillBlockCrossDestroyEvent`로 발행
- `PuzzleBoardView`: `SkillBlockCrossDestroyEvent` 구독
  - 가운데 → 상하좌우 순서로 `0.05 s` 지연을 두는 **파문형 순차 파괴** 연출
- `BlockView`: `AnimateCrossDestroy(float delay)` 오버로드 추가 (delay 후 AnimateDestroy 실행)
- 스킬 블록 자체: 십자 파괴 시작 전 `DOPunchScale` 강조 애니메이션 → 소멸
- 의존: T-D8-002

---

### T-D8-004: HeroHUDView 체력 수치 텍스트 추가 (Medium)

HP 바 옆에 `cur / max` 형식의 수치 텍스트를 추가하여 정확한 체력 확인이 가능하게 한다.

- `HeroHUDView.cs`: `[SerializeField] private TextMeshProUGUI _hpText;` 필드 추가
- `Bind()`: 초기값 `_hpText.text = $"{hero.HP} / {hero.MaxHP}";` 설정
- `hero.OnHPChanged` 핸들러: HP 바 DOFillAmount + `_hpText.text = $"{cur} / {max}";` 동시 갱신
- 사망 시 `"0 / {max}"` 표시
- 텍스트 위치: HP 바 위 또는 아래 중앙 정렬 (인스펙터 조절 가능)
- `using TMPro;` 임포트 추가
- 의존: 없음

---

### T-D8-005: EnemyHUDView 스킬 쿨다운 UI 추가 (Medium)

적 스킬 쿨다운 잔여 시간을 숫자 텍스트로 표시하여 유저가 적 스킬 발동 타이밍을 미리 파악할 수 있게 한다.

- `EnemyHUDView.cs`: `[SerializeField] private TextMeshProUGUI _cooldownText;` 필드 추가
- 스킬 없는 적: `_cooldownText.gameObject.SetActive(false)`
- `EnemyState.cs`: `OnSkillCooldownChanged(float remaining)` 이벤트 추가 (기존 OnSkillCastProgress 확인 후 결정)
- `EnemyHUDView.Bind()`: `OnSkillCooldownChanged` 구독 → `_cooldownText.text = $"{remaining:F0}s"` 갱신
- 캐스팅 진행 중(`progress > 0`): 쿨다운 텍스트 숨기고 CastingBarGroup 표시
- 스킬 시전 완료(`OnSkillCast`): 쿨다운 텍스트 리셋 후 재표시
- `using TMPro;` 임포트 추가
- 의존: 없음

---

### T-D8-006: 콤보 데미지 증폭 배율 UI 표시 (Medium)

콤보 카운터 옆에 현재 콤보로 인한 데미지 증폭 배율을 직관적으로 표시한다.

- `BattleHUD.cs`: `[SerializeField] private TextMeshProUGUI _comboMultiplierText;` 필드 추가
- `BindCombo()`: `OnComboChanged` 핸들러에 배율 계산 및 텍스트 갱신 추가
  - 배율: `1f + (count - 1) * 0.2f` (`ComboCalculator.GetMultiplierAt(count)` 활용)
  - 포맷: `$"x{multiplier:F1}"` (예: x1.0, x1.2, x1.4)
- 콤보 0: `_comboGroup`과 함께 배율 텍스트 숨김
- DOTween 색상 그라데이션:
  - 콤보 1~2: 흰색
  - 콤보 3~4: 노란색
  - 콤보 5+: 주황색 (DOColor 트위닝)
- `_comboMultiplierText`는 `_comboGroup` 하위에 배치 → 콤보 종료 시 함께 숨김
- 의존: 없음

---

## 의존성 그래프

```
T-D8-001 (스테이지/웨이브 표시)   ← 독립
T-D8-002 (스킬 블록 매칭 파괴)    ← 독립
    └── T-D8-003 (탭 애니메이션 개선)
T-D8-004 (히어로 HP 텍스트)       ← 독립
T-D8-005 (적 쿨다운 UI)          ← 독립
T-D8-006 (콤보 배율 표시)         ← 독립
```

## 작업 순서 권장

1. **T-D8-002** (스킬 블록 매칭 파괴 버그 — 핵심 버그 우선)
2. **T-D8-003** (탭 애니메이션 개선 — T-D8-002 의존)
3. **T-D8-001 / T-D8-004 / T-D8-005 / T-D8-006** (독립 UI 작업, 병렬 진행 가능)
