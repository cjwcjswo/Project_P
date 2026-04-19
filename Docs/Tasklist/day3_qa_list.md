# Day 3 QA & Improvements

## 개요

Day 3 멀티 히어로/적 시스템 구현 이후 발견된 치명적 버그 2건과 게임플레이 개선사항 1건을 처리한다.

---

## T-D3-001: 버그 수정 — HeroHUDView / EnemyHUDView Disabled 상태

### 증상

게임 시작 시 HUD 슬롯 프리팹이 비활성(disabled) 상태로 스폰되어 초상화와 HP 바가 화면에 보이지 않음.

### 원인

Unity의 `Instantiate(prefab, parent)`는 프리팹 루트 오브젝트의 `activeSelf` 상태를 그대로 복사한다.
프리팹이 비활성 상태로 저장되어 있으면 인스턴스도 비활성화된 채로 생성된다.

```csharp
// BattleHUD.cs — 문제 코드
var view = Instantiate(_heroHUDPrefab, _heroHUDContainer);
// 프리팹이 inactive이면 view도 inactive 상태로 남음
view.Bind(hero, ultManager, ...);
```

### 수정 내용

**`BattleHUD.cs`**

`BindParty()`와 `BindWave()` 각각에서 `Instantiate` 직후 `view.gameObject.SetActive(true)` 명시적 호출 추가.

```csharp
var view = Instantiate(_heroHUDPrefab, _heroHUDContainer);
view.gameObject.SetActive(true);  // 추가
view.Bind(hero, ultManager, ...);
```

---

## T-D3-002: 버그 수정 — 피격 데미지 플로팅 텍스트 미출력

### 증상

히어로와 적 모두 피격 시 데미지 숫자 텍스트(`FloatingTextView`)가 표시되지 않음.

### 원인

`BattleSceneView`를 단일 스프라이트 구조(`_heroSprite`, `_enemySprite`)에서
다중 `EntityView` 구조로 리팩터링할 때 기존의 `OnDamageTaken` 구독이 제거되었으나
새 `SpawnHeroes()` / `SpawnEnemies()` 메서드에 재추가되지 않음.
`SpawnDamageText()` 메서드는 존재하지만 실제로 호출되는 경로가 없는 상태.

```csharp
// 이전 BattleSceneView.Bind() — 삭제된 구독
hero.OnDamageTaken  += dmg => SpawnDamageText(dmg, _heroSprite.transform.position);
enemy.OnDamageTaken += dmg => SpawnDamageText(dmg, _enemySprite.transform.position);
```

### 수정 내용

**`BattleSceneView.cs`**

`SpawnHeroes()` 내에서 각 `HeroEntityView`의 `WorldPosition` 기준으로 구독 복구:

```csharp
heroes[i].OnDamageTaken += dmg =>
    SpawnDamageText(dmg, view.WorldPosition + Vector3.up * 0.5f);
```

`SpawnEnemies()` 내에서 각 `EnemyEntityView`의 `WorldPosition` 기준으로 구독 복구:

```csharp
enemies[i].OnDamageTaken += dmg =>
    SpawnDamageText(dmg, view.WorldPosition + Vector3.up * 0.5f);
```

---

## T-D3-003: 개선 — 매칭 즉시 스킬 발동

### 증상

블록을 매칭하면 이어지는 연쇄 콤보가 모두 끝난 뒤에야 스킬이 발동됨.
예: 3연쇄 콤보 시 3번째 콤보 낙하가 끝난 후 모든 스킬이 한꺼번에 실행됨.

### 원인

`BoardController.TrySwapAsync`는 캐스케이드 while 루프 전체가 끝난 뒤 `CascadeCompleteEvent`를 한 번만 발행하고,
`BattleManager.OnCascadeComplete`가 그 시점에 모든 스킬을 일괄 실행한다.

```
while (matches.Count > 0):
    AccumulateColorData → colorData (누적)
    ...
// 루프 종료 후
Publish CascadeCompleteEvent  ← 스킬 발동이 여기서 발생
```

### 수정 내용

**`Events.cs`** — 신규 이벤트 추가

```csharp
public struct MatchStepSkillTriggerEvent
{
    public List<ColorMatchData> ColorBreakdown;  // 이 스텝의 색상 데이터만 포함
    public int ComboStep;
}
```

**`BoardController.cs`**

`TrySwapAsync`와 `ProcessCascadeAsync` 두 루프 모두에서 매 스텝마다 스텝별 `ColorMatchData`를 별도로 수집하여 `MatchStepSkillTriggerEvent` 발행:

```
while (matches.Count > 0):
    stepColorData = BuildStepColorData(matches, combo)
    Publish MatchStepSkillTriggerEvent { ColorBreakdown = stepColorData }  ← 즉시 발행
    AccumulateColorData → colorData (CascadeCompleteEvent용 누적 유지)
    ...
```

**`BattleManager.cs`**

- `StartBattle`에서 `MatchStepSkillTriggerEvent` 구독 추가
- 새 핸들러 `OnMatchStepSkillTrigger`: 콤보 증가 + 스킬 실행 + 궁극기 충전을 스텝마다 즉시 처리
- `OnCascadeComplete`: 스킬 실행 코드 제거, `_comboCalc.Reset()`만 유지
- `Cleanup()`에서 `MatchStepSkillTriggerEvent` 구독 해제 추가

---

## 공통 변경 요약

| 파일 | 변경 유형 | 태스크 |
|------|-----------|--------|
| `BattleHUD.cs` | 수정 | T-D3-001 |
| `BattleSceneView.cs` | 수정 | T-D3-002 |
| `Events.cs` | 수정 (이벤트 추가) | T-D3-003 |
| `BoardController.cs` | 수정 | T-D3-003 |
| `BattleManager.cs` | 수정 | T-D3-003 |
