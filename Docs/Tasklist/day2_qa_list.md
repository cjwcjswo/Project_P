# Day 2 QA & Visual Improvements

## 개요

Day 1 구현 이후 발견된 치명적 버그 1건과 전투 경험 개선을 위한 플로팅 텍스트 기능 2건을 처리한다.

---

## T-D2-001: 버그 수정 — 블록 스왑 실패 시 원위치 복귀 미작동

### 증상

블록을 드래그하여 이동시킨 뒤 매칭이 성립되지 않으면 원래 위치로 돌아와야 하지만,
비주얼상 이동된 채로 남아있어 보드 상태와 화면이 불일치한다.

### 원인

`BoardController.TrySwapAsync`는 매치가 없을 때 **두 번째 SwapEvent(`IsValid = false`)** 를 발행하여 데이터 모델을 원위치로 되돌린다.
그러나 `PuzzleBoardView.OnSwap`의 첫 줄에서 `IsValid = false`이면 즉시 반환하므로 **뷰 레이어에서 역방향 애니메이션이 실행되지 않는다**.

```csharp
// PuzzleBoardView.cs — 문제 코드
private void OnSwap(SwapEvent evt)
{
    if (!evt.IsValid) return; // ← 이 줄이 원위치 처리를 막음
    ...
}
```

BoardController가 두 번째 이벤트에서 같은 (col1, row1, col2, row2)를 사용하기 때문에,
`OnSwap`에서 IsValid 체크를 제거하고 동일 로직을 실행하면 자연스럽게 원위치로 복귀된다.

### 수정 내용

**`PuzzleBoardView.cs`**
- `OnSwap`에서 `if (!evt.IsValid) return;` 제거
- `IsValid`에 따라 사용할 Ease를 분기: 정방향은 `OutBounce`, 복귀는 `InOutSine`

**`BlockView.cs`**
- `AnimateMoveTo(Vector3 targetPos, float duration, Ease ease)` — Ease 파라미터 추가 (기본값 `Ease.OutBounce` 유지)

---

## T-D2-002: 개선 — Player/Enemy 데미지 플로팅 텍스트

### 목표

플레이어 또는 적이 피해를 입을 때, 해당 오브젝트 위쪽으로 데미지 숫자가 떠올라 페이드 아웃된다.

### 구현

#### 신규 파일: `FloatingTextView.cs`

`TextMeshPro`(월드 스페이스 메시 텍스트) 기반 MonoBehaviour.

```
Show(string text, Color color, Vector3 worldPos)
  └─ 지정 위치에 이동
  └─ DOTween: 1초간 위로 0.8유닛 이동 (Ease.OutCubic)
  └─ DOTween: 마지막 0.4초 동안 알파 0으로 페이드
  └─ 애니메이션 완료 시 Destroy(gameObject)
```

#### 수정: `HeroState.cs`

```csharp
public event Action<int> OnDamageTaken;

public void TakeDamage(int rawDamage)
{
    // ... 기존 방어/실드 계산 ...
    int finalDamage = Math.Max(1, remaining - Defense);
    CurrentHP = Math.Max(0, CurrentHP - finalDamage);
    OnDamageTaken?.Invoke(finalDamage); // 추가
    OnHPChanged?.Invoke(CurrentHP, MaxHP);
    if (IsDead) OnDeath?.Invoke();
}
```

#### 수정: `EnemyState.cs`

```csharp
public event Action<int> OnDamageTaken;

public void TakeDamage(int damage)
{
    int actual = Math.Max(0, damage);
    CurrentHP = Math.Max(0, CurrentHP - actual);
    OnDamageTaken?.Invoke(actual); // 추가
    OnHPChanged?.Invoke(CurrentHP, MaxHP);
    if (IsDead) OnDeath?.Invoke();
}
```

#### 수정: `BattleSceneView.cs`

```csharp
[SerializeField] private FloatingTextView _floatingTextPrefab;

// Bind() 내부
_hero.OnDamageTaken  += dmg => SpawnDamageText(dmg, _heroSprite.transform.position);
_enemy.OnDamageTaken += dmg => SpawnDamageText(dmg, _enemySprite.transform.position);
```

---

## T-D2-003: 개선 — 스킬 발동 시 Player 위 스킬명 텍스트

### 목표

블록 매칭으로 스킬이 발동될 때 Player 오브젝트 위에 스킬 종류를 나타내는 텍스트가 표시된다.

| BlockType | 스킬명   | 색상  |
|-----------|----------|-------|
| Red       | 공격!    | 빨강  |
| Blue      | 방어!    | 파랑  |
| Green     | 회복!    | 초록  |
| Yellow    | 기절!    | 노랑  |

### 구현

`BattleSceneView.OnSkillExecuted` 핸들러 확장:

```csharp
private void OnSkillExecuted(SkillEffect effect)
{
    // 기존 punch/shake 애니메이션 유지
    // 추가: 스킬명 플로팅 텍스트
    SpawnSkillText(effect.Type, _heroSprite.transform.position);
}
```

---

## 공통 변경 요약

| 파일 | 변경 유형 | 설명 |
|------|-----------|------|
| `PuzzleBoardView.cs` | 수정 | OnSwap IsValid 조건 제거, 복귀 시 Ease.InOutSine |
| `BlockView.cs` | 수정 | AnimateMoveTo에 Ease 파라미터 추가 |
| `HeroState.cs` | 수정 | OnDamageTaken 이벤트 추가 |
| `EnemyState.cs` | 수정 | OnDamageTaken 이벤트 추가 |
| `FloatingTextView.cs` | 신규 | 월드 스페이스 플로팅 텍스트 컴포넌트 |
| `BattleSceneView.cs` | 수정 | 데미지/스킬 텍스트 바인딩 및 스폰 로직 |
