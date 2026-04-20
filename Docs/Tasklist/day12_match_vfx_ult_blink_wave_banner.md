# Day 12 - 매칭 VFX(파괴/흡수), 궁극기 테두리 반짝임, 웨이브 배너 연출

퍼즐 매칭의 피드백을 강화하기 위해 **블록 파괴 파티클**과 **블록 색상 에너지 흡수(히어로로 이동)** 연출을 추가합니다. 또한 **궁극기 사용 가능 상태**를 HUD에서 더 명확히 보여주고, **웨이브 시작/전환** 시 "Wave N" 배너가 자연스럽게 지나가는 연출을 추가합니다.

---

## 현재 상태 분석 (근거)

- `BoardController.cs`는 매치 스텝마다 `MatchStepSkillTriggerEvent`를 발행하고(`ColorBreakdown` 포함), `BattleManager.cs`는 이를 받아 실제 스킬 처리(`SkillSystem.ExecuteFromCascade`)를 수행합니다.
- 블록 타입 팔레트는 `BlockTypeColors.cs`에 이미 존재합니다.
  - Red `#FC8181`, Yellow `#F6E05E`, Green `#68D391`, Blue `#63B3ED`, Purple `#B794F4`
- 웨이브 전환 훅은 이미 존재하며, `BattleManager.cs`는 수정 없이 이벤트를 구독하여 사용합니다.
  - `BattleManager.OnWaveChanged`(새 웨이브 활성화 시점)
- `HeroHUDView.cs`는 궁극기 만충 시 `_ultGaugeFill`에 DOColor 루프를 주지만, `PortraitBorder`는 색만 유지합니다.

---

## 태스크 목록

### T-D12-001: 블록 매칭 파괴 파티클(블록 조각) 추가 (High)

블록이 매칭으로 제거될 때, 작은 조각이 여러 개로 부숴지는 듯한 파티클 연출을 추가합니다.

- **대상 파일**
  - `Client/Assets/_Project/Scripts/UI/PuzzleBoardView.cs`
  - `Client/Assets/_Project/Scripts/UI/BlockView.cs`
- **구현 포인트**
  - **생명주기 분리:** `BlockView`가 비활성화되더라도 파티클이 끝까지 재생되도록 독립적인 **VFX Object Pool**을 구축합니다.
  - 파괴될 블록의 좌표(`GridToWorld`)에 파티클 프리팹을 스폰하며, `BlockView` 내부에서 자식으로 생성하지 않습니다.
  - 파티클은 `BlockTypeColors.Get(type)`을 통해 해당 블록의 색상을 실시간으로 주입합니다.

### T-D12-002: 블록 색상 빛(orb) 흡수 연출 + HeroEntityView 스킬 발동 연동 (High)

터진 블록의 기운이 해당 속성 히어로(`HeroEntityView`)에게 날아가 스킬 발동을 예고하는 연출을 추가합니다.

- **대상 파일**
  - `Client/Assets/_Project/Scripts/UI/HeroEntityView.cs`
  - `Client/Assets/_Project/Scripts/UI/PuzzleBoardView.cs`
- **구현 포인트**
  - `DOTween`의 `DOMove`를 이용해 블록 위치에서 영웅 위치로 이동시킵니다.
  - **동기화 전략:** 실제 데미지 로직과의 시간차를 줄이기 위해 이동 시간은 **0.15초 이내**의 매우 빠른 속도로 설정합니다.
  - **연쇄 반응:** Orb가 영웅에게 도달하는 순간(`OnComplete`), `HeroEntityView.PlaySkillAnim()`을 호출하여 시각적 인과관계를 완성합니다.
  - 색상 팔레트: Red `#FC8181`, Yellow `#F6E05E`, Green `#68D391`, Blue `#63B3ED`, Purple `#B794F4`

### T-D12-003: HeroHUDView — 궁극기 사용 가능 시 PortraitBorder 주기적 반짝임 (Medium)

궁극기 사용 가능 상태일 때, 초상화 테두리(`_portraitBorder`)가 주기적으로 반짝여 유저에게 명확히 알려줍니다.

- **대상 파일**
  - `Client/Assets/_Project/Scripts/UI/HeroHUDView.cs`
- **구현 포인트**
  - `UltimateGaugeManager.OnUltimateReady` 호출 시 `_portraitBorder`에 반짝임 효과를 추가합니다.
  - **색상 보존:** 고유 속성 색상을 유지하기 위해 `DOColor` 대신 **`DOFade(0.3f, 0.5f)`**를 사용하거나 알파값을 조절하는 방식을 사용합니다.
  - 궁극기 발동(초상화 클릭) 또는 사망(`OnDeath`) 시 `DOKill()`을 호출하여 연출을 즉시 초기화합니다.

### T-D12-004: 웨이브 시작/전환 시 "Wave N" 배너 텍스트 연출 추가 (Medium)

웨이브가 시작되거나 전환될 때, `"Wave N"` 텍스트가 화면을 자연스럽게 지나가는 연출을 추가합니다.

- **대상 파일**
  - `Client/Assets/_Project/Scripts/UI/BattleHUD.cs` (또는 독립적인 `WaveBannerView.cs`)
- **구현 포인트**
  - `BattleManager.cs` 수정 없이 `OnWaveChanged` 이벤트를 구독하여 UI 레이어에서만 처리합니다.
  - 텍스트가 화면 왼쪽 밖에서 등장하여 중앙에서 잠시 감속(`Ease.OutCubic`) 후 오른쪽 밖으로 이탈하는 시퀀스를 구성합니다.
  - 표시 문자열: 0-based 인덱스 보정을 적용한 `"Wave {waveIndex + 1}"`

---

## 의존성 및 작업 순서

1. **VFX 시스템 구축:** 파티클 풀링 및 Orb 이동 로직 구현
2. **View 연동:** `PuzzleBoardView`와 `HeroEntityView` 사이의 타이밍 조율
3. **UI 폴리싱:** HUD 테두리 반짝임 및 웨이브 배너 추가