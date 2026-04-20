# Day 10 - 히어로 애니메이션 & 웨이브 전환 연출

전투 내 히어로의 상태별 애니메이션(**Idle / Attack / Skill / Run / Death**)을 Animator Controller 기반으로 구현하고, 웨이브 클리어 시 히어로의 **Run 애니메이션 + 배경 스크롤**을 조합한 이동 연출을 추가한다.
기존 DOTween 위치 트윈 방식에서 Unity Animator State Machine으로 전환하며, `BattleManager`의 웨이브 전환 흐름에 비동기 연출 단계를 삽입한다.

---

## 현재 상태 분석

| 항목 | 현재 구현 | Day 10 목표 | Gap |
|------|----------|------------|-----|
| 평타 연출 | `DOPunchPosition` (위치 트윈) | `Animator.SetTrigger("Attack")` | Animator 전환 필요 |
| 스킬 연출 | `DOColor` 색상 플래시만 | `Animator.SetTrigger("Skill")` 클립 재생 | Skill 상태 추가 |
| Idle 애니메이션 | 없음 (정적 스프라이트) | Animator Idle 루프 | Animator Controller 신규 |
| 사망 연출 | DOTween 회색화 | `Animator.SetBool("IsDead", true)` | Death State 전환 |
| 웨이브 전환 | 즉시 적 교체 | Run 애님 + 배경 스크롤 후 교체 | 비동기 전환 단계 삽입 |
| 배경 스크롤 | 없음 | `BackgroundScrollView` 패럴랙스 스크롤 | 신규 컴포넌트 |

---

## 태스크 목록

### T-D10-001: Hero Animator Controller 에셋 생성 및 상태 머신 설계 (High)

에셋 경로: `Client/Assets/_Project/Animations/Hero/HeroAnimator.controller`

**State 정의**

| State | Loop | Has Exit Time | 진입 조건 |
|-------|------|--------------|----------|
| Idle | ✅ | ❌ | 기본값 |
| Attack | ❌ | ✅ | Trigger Attack |
| Skill | ❌ | ✅ | Trigger Skill |
| Run | ✅ | ❌ | Trigger Run |
| Death | ❌ | ✅ | Bool IsDead == true (Any State) |

**Parameter 목록**

- `Trigger Attack` — 평타 발동
- `Trigger Skill` — 스킬 발동
- `Trigger Run` — 이동 시작 (웨이브 전환)
- `Trigger BackToIdle` — Run → Idle 복귀
- `Bool IsDead` — 사망 고정 (Any State 전환)

**전환 규칙**

```
Any State ──(IsDead=true)──► Death
Idle      ──(Attack)──────► Attack ──(Exit Time)──► Idle
Idle      ──(Skill)───────► Skill  ──(Exit Time)──► Idle
Idle      ──(Run)─────────► Run    ──(BackToIdle)─► Idle
```

- 의존: 없음

---

### T-D10-002: Hero 스프라이트 시트 기반 Animation Clip 생성 (High)

클립 생성 위치: `Client/Assets/_Project/Animations/Hero/Clips/`

| 클립 파일 | FPS | 재생 방식 | 비고 |
|-----------|-----|---------|------|
| Hero_Idle.anim | 8 | Loop | 기본 대기 |
| Hero_Attack.anim | 12 | 1회 | ≈ 0.4s |
| Hero_Skill.anim | 12 | 1회 | ≈ 0.6s |
| Hero_Run.anim | 12 | Loop | 웨이브 이동용 |
| Hero_Death.anim | 8 | 1회 후 마지막 프레임 고정 | ≈ 0.6s |

**스프라이트 부재 시 최소 구현**

- Attack/Skill 클립: `SpriteRenderer.color` 노란 플래시 커브 (0.1s → 원복)
- Run 클립: `transform.position.x` ±0.02 흔들림 커브 루프
- 클립 내 커브만으로도 Animator 상태 전환은 동작하므로 스프라이트 준비 전에 선행 구현 가능

- 의존: T-D10-001

---

### T-D10-003: HeroEntityView — Animator 기반 재생 메서드 전환 (High)

파일: `Client/Assets/_Project/Scripts/UI/HeroEntityView.cs`

**변경 사항**

```csharp
// 추가 필드
[SerializeField] private Animator _animator;

// PlayAttackAnim: DOPunchPosition → Trigger
public void PlayAttackAnim()
    => _animator.SetTrigger("Attack");

// PlaySkillAnim [신규]
public void PlaySkillAnim()
    => _animator.SetTrigger("Skill");

// PlayRunAnim [신규]
public void PlayRunAnim()
    => _animator.SetTrigger("Run");

// StopRunAnim [신규]
public void StopRunAnim()
    => _animator.SetTrigger("BackToIdle");

// PlayDeathAnim: DOTween → Bool
public void PlayDeathAnim()
    => _animator.SetBool("IsDead", true);
```

- `PlayColorFlash` / `PlayDamageFlash`의 DOTween 색상 트윈은 그대로 유지
- `Reset()` 헬퍼에 `_animator = GetComponent<Animator>()` 추가
- 의존: T-D10-001

---

### T-D10-004: BattleSceneView — 스킬 애니메이션 트리거 연결 (Medium)

파일: `Client/Assets/_Project/Scripts/UI/BattleSceneView.cs`

`OnSkillExecuted` 분기 수정:

```csharp
bool isMassAttack = effect.ActionType == ActionType.Attack &&
                    (effect.Strategy == TargetStrategy.All ||
                     (effect.MaxCount > 1 && effect.MaxCount >= _enemyViews.Count));

if (isMassAttack || effect.ActionType != ActionType.Attack)
    srcHeroView?.PlaySkillAnim();   // 대규모 공격 포함 모든 스킬 계열
else
    srcHeroView?.PlayAttackAnim();  // 단일 평타 계열 Attack만
```

- AoE 타격 플래시 / 싱글 타격 플래시 / 스킬 텍스트 팝업 로직은 변경 없이 유지
- 의존: T-D10-003

---

### T-D10-005: BackgroundScrollView — 배경 스크롤 컴포넌트 구현 (Medium)

신규 파일: `Client/Assets/_Project/Scripts/UI/BackgroundScrollView.cs`

```csharp
[SerializeField] private Transform[] _bgLayers;
[SerializeField] private float[]     _layerSpeedMultipliers; // 패럴랙스 배수
[SerializeField] private float       _scrollDistance = 12f;
[SerializeField] private float       _scrollDuration = 1.2f;

public async UniTask ScrollAsync(CancellationToken ct)
{
    if (_bgLayers == null || _bgLayers.Length == 0)
    {
        await UniTask.Delay(TimeSpan.FromSeconds(_scrollDuration), cancellationToken: ct);
        return;
    }
    var tasks = new List<UniTask>();
    for (int i = 0; i < _bgLayers.Length; i++)
    {
        float dist = _scrollDistance * (_layerSpeedMultipliers?.Length > i
                     ? _layerSpeedMultipliers[i] : 1f);
        var tween = _bgLayers[i]
            .DOMoveX(_bgLayers[i].position.x - dist, _scrollDuration)
            .SetEase(Ease.InOutSine);
        tasks.Add(tween.ToUniTask(cancellationToken: ct));
    }
    await UniTask.WhenAll(tasks);
}

public void ResetPositions() { /* 각 레이어 원위치로 순간 이동 */ }
```

- `_bgLayers`가 비어있으면 `UniTask.Delay`만 실행 — 배경 없어도 웨이브 전환 타이밍 보장
- 의존: 없음

---

### T-D10-006: BattleManager — 웨이브 전환 전 비동기 콜백 주입 (High)

파일: `Client/Assets/_Project/Scripts/Battle/BattleManager.cs`

```csharp
// 신규 필드: 외부에서 주입하는 View 연출 콜백
public Func<int, UniTask> OnWaveTransitionRequested;

// HandleAllEnemiesDead → async 전환
private async UniTaskVoid HandleAllEnemiesDeadAsync()
{
    int nextIndex = _currentWaveIndex + 1;
    if (nextIndex < _setupData.Waves.Length)
    {
        if (OnWaveTransitionRequested != null)
            await OnWaveTransitionRequested.Invoke(nextIndex);

        _currentWaveIndex = nextIndex;
        ActivateWave(_setupData.Waves[_currentWaveIndex]);
    }
    else
    {
        _cts?.Cancel();
        var result = BuildBattleResult(isCleared: true);
        OnBattleWin?.Invoke();
        EventBus.Publish(new BattleCompleteEvent { Result = result });
        Cleanup();
    }
}
```

- `EnemyWave.OnAllEnemiesDead` 구독: `+= () => HandleAllEnemiesDeadAsync().Forget();`
- 기존 `void HandleAllEnemiesDead()` 제거
- 의존: T-D10-005

---

### T-D10-007: BattleSceneView — WaveTransitionAsync 구현 (High)

파일: `Client/Assets/_Project/Scripts/UI/BattleSceneView.cs`

```csharp
[SerializeField] private BackgroundScrollView _backgroundScroll;

public async UniTask WaveTransitionAsync(int nextWaveIndex)
{
    // 1. 히어로 Run 애니메이션 시작
    foreach (var heroView in _heroViews.Values)
        heroView.PlayRunAnim();

    // 2. 배경 스크롤 (없으면 기본 대기)
    if (_backgroundScroll != null)
        await _backgroundScroll.ScrollAsync(default);
    else
        await UniTask.Delay(TimeSpan.FromSeconds(1.2f));

    // 3. 히어로 Idle 복귀
    foreach (var heroView in _heroViews.Values)
        heroView.StopRunAnim();

    // 4. 적 스폰 전 짧은 여백
    await UniTask.Delay(TimeSpan.FromSeconds(0.3f));
}
```

순서 보장: `WaveTransitionAsync` 완료 → `ActivateWave` 내부에서 `OnWaveChanged` 발행 → `OnWaveChanged`에서 새 적 스폰

- 의존: T-D10-003, T-D10-005, T-D10-006

---

### T-D10-008: BattleSceneManager — 전환 콜백 연결 (High)

파일: `Client/Assets/_Project/Scripts/Core/BattleSceneManager.cs`

`InitializeBattleAsync`의 UI 바인딩 블록 내 `BindBattleManager` 직후에 추가:

```csharp
_battleManager.OnWaveTransitionRequested = _battleSceneView.WaveTransitionAsync;
```

- 의존: T-D10-006, T-D10-007

---

## 의존성 그래프

```
T-D10-001 (Animator Controller 설계)
    ├── T-D10-002 (Animation Clip 생성)
    └── T-D10-003 (HeroEntityView 전환)
            └── T-D10-004 (BattleSceneView 스킬 트리거)
            └── T-D10-007 (WaveTransitionAsync)

T-D10-005 (BackgroundScrollView) ← 독립
    └── T-D10-006 (BattleManager 콜백 주입)
            └── T-D10-007 (WaveTransitionAsync)
                    └── T-D10-008 (BattleSceneManager 연결)
```

## 작업 순서 권장

1. **Phase A — 에셋 준비 (병렬 가능)**
   - T-D10-001 (Animator Controller 설계) — Unity 에디터 작업
   - T-D10-005 (BackgroundScrollView 스크립트) — 코드 작업

2. **Phase B — 뷰 컴포넌트 전환**
   - T-D10-002 (Animation Clip 생성) — Unity 에디터 작업
   - T-D10-003 (HeroEntityView Animator 전환)

3. **Phase C — 시스템 연결**
   - T-D10-004 (BattleSceneView 스킬 트리거)
   - T-D10-006 (BattleManager 비동기 콜백)
   - T-D10-007 (BattleSceneView WaveTransitionAsync)

4. **Phase D — 마무리 연결 + 검증**
   - T-D10-008 (BattleSceneManager 콜백 등록)
   - Unity Play Mode에서 평타/스킬 애니메이션 확인
   - 웨이브 클리어 후 Run → 배경 스크롤 → 새 적 등장 순서 확인
