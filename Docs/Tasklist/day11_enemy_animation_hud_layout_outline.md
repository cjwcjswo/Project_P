# Day 11 - 적 애니메이션 시스템, HUD 배치 반전, 블록 타입 외곽선

적(Enemy) 엔티티에 히어로와 동일한 Animator 기반 애니메이션(Idle/Attack/Skill)을 추가하고, MonsterData.PrefabPath에서 모델을 동적 로드하는 구조로 전환한다. HeroHUDView 배치를 우→좌로 반전하여 월드 공간의 히어로 위치와 HUD 슬롯이 시각적으로 대응되도록 수정한다. 히어로 모델 스프라이트에 BlockType별 색상 외곽선을 Outline Shader로 렌더링한다.

---

## 현재 상태 분석

| 항목 | 현재 구현 | Day 11 목표 | Gap |
|------|----------|------------|-----|
| 적 평타 연출 | `DOPunchPosition` (위치 트윈) | `Animator.SetTrigger("Attack")` | Animator 전환 필요 |
| 적 스킬 연출 | 캐스팅 바 Fill만 | `Animator.SetTrigger("Skill")` 클립 재생 | Skill 상태 추가 |
| 적 Idle 애니메이션 | 없음 (정적 스프라이트) | Animator Idle 루프 | Animator Controller 신규 |
| 적 모델 로드 | 프리팹 직접 배치 (SpriteRenderer 고정) | PrefabPath 기반 동적 로드 | HeroEntityView 패턴 적용 |
| HeroHUD 배치 | 좌→우 (0번=왼쪽) | 우→좌 (0번=오른쪽) | 배치 순서 반전 |
| 히어로 외곽선 | 없음 | BlockType별 색상 외곽선 | Outline Shader 신규 |
| HUD 초상화 외곽선 | 없음 | BlockType별 색상 테두리 | 테두리 Image 추가 |

---

## 태스크 목록

### T-D11-001: EnemyEntityView — 동적 모델 로드 및 Animator 통합 (High)

파일: `Client/Assets/_Project/Scripts/UI/EnemyEntityView.cs`

HeroEntityView와 동일한 패턴으로 `MonsterData.PrefabPath`를 이용해 모델 프리팹을 동적 로드/인스턴스화한다.

**구조 변경**

```
EnemyEntity (프리팹 루트)
├─ MonsterModel (빈 Transform — 모델 인스턴스 부모)
│  └─ [런타임: Resources에서 로드한 모델 프리팹 인스턴스]
│     ├─ SpriteRenderer
│     └─ Animator (MonsterAnimator.controller)
├─ Canvas (World Space) — HUD
│  ├─ HPFill
│  ├─ CastingBarGroup
│  └─ CooldownGroup
└─ Collider2D
```

**핵심 코드**

```csharp
private GameObject _spawnedModel;
private Animator   _animator;
private Transform  _modelRoot;

private void AttachModelFromPrefabPath(string prefabPath)
{
    if (_spawnedModel != null) { Destroy(_spawnedModel); _spawnedModel = null; }
    _animator = null;
    if (string.IsNullOrEmpty(prefabPath)) return;
    if (_modelRoot == null) _modelRoot = transform.Find("MonsterModel");
    if (_modelRoot == null) return;

    var prefab = Resources.Load<GameObject>(prefabPath);
    if (prefab == null) return;

    _spawnedModel = Instantiate(prefab, _modelRoot, false);
    _animator       = _spawnedModel.GetComponentInChildren<Animator>(true);
    _spriteRenderer = _spawnedModel.GetComponentInChildren<SpriteRenderer>(true) ?? _spriteRenderer;
}
```

**Animator 재생 메서드 변경**

```csharp
public void PlayAttackAnim()
{
    if (_animator != null)
        _animator.SetTrigger("Attack");
    else
        transform.DOPunchPosition(Vector3.left * 0.2f, 0.2f, 5, 0.5f).SetLink(gameObject);
}

public void PlaySkillAnim()
{
    _animator?.SetTrigger("Skill");
}
```

- 의존: 없음

---

### T-D11-002: HeroEntityView — 모델 로드 방식 효율화 검토 및 통일 (Medium)

파일: `Client/Assets/_Project/Scripts/UI/HeroEntityView.cs`

- `Resources.Load`는 Unity 내부적으로 이미 캐시하므로 추가 캐시 레이어는 불필요.
- HeroEntityView와 EnemyEntityView의 `AttachModelFromPrefabPath()` 시그니처 및 패턴을 통일.
- `CacheBaseColor()` 호출이 모델 교체 후에 수행되는지 확인.
- 의존: T-D11-001

---

### T-D11-003: Monster Animator Controller 에셋 생성 (High)

에셋 경로: `Client/Assets/_Project/Animations/Monster/MonsterAnimator.controller`

**State 정의**

| State | Loop | Has Exit Time | 진입 조건 |
|-------|------|--------------|----------|
| Idle | ✅ | ❌ | 기본값 |
| Attack | ❌ | ✅ | Trigger Attack |
| Skill | ❌ | ✅ | Trigger Skill |
| Death | ❌ | ✅ | Bool IsDead == true (Any State) |

**Parameter 목록**

- `Trigger Attack` — 평타 발동
- `Trigger Skill` — 스킬 시전
- `Bool IsDead` — 사망 고정

**전환 규칙**

```
Any State ──(IsDead=true)──► Death
Idle      ──(Attack)──────► Attack ──(Exit Time)──► Idle
Idle      ──(Skill)───────► Skill  ──(Exit Time)──► Idle
```

**임시 Animation Clip** (스프라이트 부재 시)

| 클립 파일 | FPS | 재생 방식 | 비고 |
|-----------|-----|---------|------|
| Monster_Idle.anim | 8 | Loop | 기본 대기 |
| Monster_Attack.anim | 12 | 1회 (0.4s) | color 빨간 플래시 커브 |
| Monster_Skill.anim | 12 | 1회 (0.6s) | color 보라 플래시 커브 |
| Monster_Death.anim | 8 | 1회 (0.6s) | 페이드아웃 커브 |

- 의존: 없음

---

### T-D11-004: BattleSceneView — 적 스킬/공격 애니메이션 트리거 연결 (Medium)

파일: `Client/Assets/_Project/Scripts/UI/BattleSceneView.cs`

- `OnEnemyAutoAttack`: 기존 `PlayAttackAnim()` 호출 유지 (내부가 Animator 기반으로 전환됨).
- `SpawnEnemies()`에서 `OnSkillCast` 이벤트 구독 추가 → `enemyView.PlaySkillAnim()` 호출.
- 적 사망 시 Animator가 있으면 `SetBool("IsDead", true)` 추가 호출 (EnemyEntityView 내부 처리).
- 의존: T-D11-001

---

### T-D11-005: HeroHUDView 배치 순서 반전 (우→좌) (High)

파일: `Client/Assets/_Project/Scripts/UI/BattleHUD.cs`

**현재 문제**

```
월드 공간 히어로 위치: [Hero0] [Hero1] [Hero2] [Hero3]  →  적
HUD 배치 (현재):      [HUD0] [HUD1] [HUD2] [HUD3]
                       ← 좌측 기준 우측으로 배치

원하는 HUD 배치:      [HUD3] [HUD2] [HUD1] [HUD0]
                       ← 최후방이 좌측, 최전방(0번)이 우측
```

**수정 방법** — `BattleHUD.BindParty()`에서 Instantiate 후 `SetAsFirstSibling()`:

```csharp
public void BindParty(HeroParty party, UltimateGaugeManager ultManager, BattleManager battleManager)
{
    foreach (var hero in party.Heroes)
    {
        var view = Instantiate(_heroHUDPrefab, _heroHUDContainer);
        view.transform.SetAsFirstSibling();  // ← 추가: 매번 맨 앞에 삽입 → 결과적으로 역순 배치
        view.gameObject.SetActive(true);
        int idx  = hero.PartyIndex;
        view.Bind(hero, ultManager, () => battleManager.ActivateUltimateAsync(idx).Forget());
    }
}
```

**검증 항목**

- 3~5명 파티에서 0번 히어로 HUD가 가장 우측에 표시
- 궁극기 버튼 클릭 시 올바른 heroIndex에 매핑
- 의존: 없음

---

### T-D11-006: 2D Outline Shader Graph 생성 (High)

에셋 경로: `Client/Assets/_Project/Shaders/Sprite-Outline.shadergraph`

**셰이더 요구사항**

- URP 2D Sprite 기반 (Sprite Unlit 또는 Sprite Lit Master Node).
- 스프라이트의 알파 채널 기반 외곽선 감지 — **모델에 fit한 외곽선** (여백 포함 X).
- 8방향 인접 픽셀 알파 샘플링으로 매끄러운 윤곽선.
- 외곽선은 스프라이트 **외부**에만 렌더링.

**프로퍼티**

| 이름 | 타입 | 기본값 | 설명 |
|------|------|--------|------|
| _OutlineColor | Color | White | 외곽선 색상 |
| _OutlineThickness | Float | 1.0 | 외곽선 두께 (픽셀 단위) |
| _OutlineEnabled | Float | 0 | 외곽선 ON/OFF |

**알고리즘 (Alpha-edge detection, 8방향)**

```hlsl
float2 offsets[8] = {
    float2(0,1), float2(0,-1), float2(1,0), float2(-1,0),
    float2(1,1), float2(1,-1), float2(-1,1), float2(-1,-1)
};
float maxAlpha = 0;
for (int i = 0; i < 8; i++)
    maxAlpha = max(maxAlpha, tex2D(_MainTex, uv + offsets[i] * texelSize * thickness).a);

if (mainAlpha < 0.1 && maxAlpha > 0.1 && outlineEnabled > 0.5)
    return float4(outlineColor.rgb, maxAlpha);
else
    return mainColor;
```

**Material**

- `Client/Assets/_Project/Materials/Sprite-Outline.mat` — 기본 Material (_OutlineEnabled=0)
- 런타임에 `MaterialPropertyBlock`으로 색상/두께/활성화 제어 → Material Instance 증가 방지
- 의존: 없음

---

### T-D11-007: HeroEntityView — 블록 타입별 외곽선 색상 적용 (High)

파일: `Client/Assets/_Project/Scripts/UI/HeroEntityView.cs`

**색상 매핑**

| BlockType | Hex | 시각 |
|-----------|-----|------|
| Red | #FC8181 | 빨강 |
| Yellow | #F6E05E | 노랑 |
| Green | #68D391 | 초록 |
| Blue | #63B3ED | 파랑 |
| Purple | #B794F4 | 보라 |

**구현**

```csharp
// BlockTypeColors.cs (신규)
public static class BlockTypeColors
{
    public static Color Get(BlockType type) => type switch
    {
        BlockType.Red    => ColorUtility.TryParseHtmlString("#FC8181", out var c) ? c : Color.red,
        BlockType.Yellow => ColorUtility.TryParseHtmlString("#F6E05E", out c) ? c : Color.yellow,
        BlockType.Green  => ColorUtility.TryParseHtmlString("#68D391", out c) ? c : Color.green,
        BlockType.Blue   => ColorUtility.TryParseHtmlString("#63B3ED", out c) ? c : Color.blue,
        BlockType.Purple => ColorUtility.TryParseHtmlString("#B794F4", out c) ? c : Color.magenta,
        _ => Color.white
    };
}
```

```csharp
// HeroEntityView.Bind() 내
private void ApplyOutline(BlockType blockType)
{
    if (_spriteRenderer == null) return;
    var mpb = new MaterialPropertyBlock();
    _spriteRenderer.GetPropertyBlock(mpb);
    mpb.SetColor("_OutlineColor", BlockTypeColors.Get(blockType));
    mpb.SetFloat("_OutlineThickness", 1.5f);
    mpb.SetFloat("_OutlineEnabled", 1f);
    _spriteRenderer.SetPropertyBlock(mpb);
}
```

- 모델 프리팹의 SpriteRenderer Material을 Sprite-Outline.mat으로 교체 필요
- 또는 AttachModelFromPrefabPath() 후 `_spriteRenderer.material = _outlineMaterial` 런타임 할당
- 의존: T-D11-006

---

### T-D11-008: HeroHUDView — 초상화에 블록 타입 테두리 적용 (Medium)

파일: `Client/Assets/_Project/Scripts/UI/HeroHUDView.cs`

UI Image에는 SpriteRenderer용 Shader를 직접 사용할 수 없으므로, 초상화 위에 테두리 전용 Image를 겹쳐 배치한다.

**프리팹 변경**

```
HeroHUDView
├─ PortraitBorder (Image, 9-Slice 테두리 스프라이트) ← 신규
├─ PortraitImage (Image, 초상화)
├─ UltGaugeFill (Image, Radial 360)
├─ PortraitButton (Button)
...
```

**코드 변경**

```csharp
[SerializeField] private Image _portraitBorder;

// Bind() 내
if (_portraitBorder != null)
    _portraitBorder.color = BlockTypeColors.Get(hero.MappedColor);
```

- 9-Slice 테두리 스프라이트 에셋 필요 (사각형 테두리, 내부 투명)
- 의존: T-D11-007

---

### T-D11-009: EnemyEntityView — 타겟 하이라이트 외곽선 (Low, 선택)

파일: `Client/Assets/_Project/Scripts/UI/EnemyEntityView.cs`

적은 BlockType이 없으므로 기본적으로 외곽선 비활성화. 일점사 타겟으로 선택된 적에만 하이라이트 외곽선(노란색 #F6E05E)을 표시.

```csharp
public void SetOutline(Color color, bool enabled)
{
    if (_spriteRenderer == null) return;
    var mpb = new MaterialPropertyBlock();
    _spriteRenderer.GetPropertyBlock(mpb);
    mpb.SetColor("_OutlineColor", color);
    mpb.SetFloat("_OutlineEnabled", enabled ? 1f : 0f);
    _spriteRenderer.SetPropertyBlock(mpb);
}
```

- 의존: T-D11-006, T-D11-001

---

## 의존성 그래프

```
T-D11-001 (Enemy 동적 모델 로드 + Animator)
    ├── T-D11-002 (Hero 모델 로드 패턴 통일)
    ├── T-D11-004 (BattleSceneView 적 애니메이션 연결)
    └── T-D11-009 (적 타겟 하이라이트, 선택적)

T-D11-003 (Monster Animator Controller) ← 독립, 에디터 작업

T-D11-005 (HeroHUD 배치 반전) ← 독립

T-D11-006 (Outline Shader Graph) ← 독립
    ├── T-D11-007 (HeroEntityView 외곽선)
    │       └── T-D11-008 (HeroHUDView 초상화 테두리)
    └── T-D11-009 (적 타겟 하이라이트, 선택적)
```

## 작업 순서 권장

1. **Phase A — 병렬 기반 작업** (독립)
   - T-D11-001 (EnemyEntityView 동적 모델 로드 + Animator)
   - T-D11-003 (Monster Animator Controller 에셋) — Unity 에디터
   - T-D11-005 (HeroHUD 배치 반전)
   - T-D11-006 (Outline Shader Graph)

2. **Phase B — 연결 및 적용**
   - T-D11-002 (Hero/Enemy 모델 로드 패턴 통일)
   - T-D11-004 (BattleSceneView 적 애니메이션 연결)
   - T-D11-007 (HeroEntityView 외곽선 적용)

3. **Phase C — 마무리**
   - T-D11-008 (HeroHUDView 초상화 테두리)
   - T-D11-009 (적 타겟 하이라이트, 선택적)

4. **검증**
   - 적 모델이 PrefabPath에서 동적 로드되어 Idle 애니메이션 재생 확인
   - 적 평타/스킬 시 Attack/Skill 애니메이션 트리거 확인
   - HeroHUD 0번 슬롯이 가장 우측에 배치되는지 확인
   - 히어로 모델에 BlockType 색상 외곽선이 스프라이트 윤곽에 fit하게 렌더링되는지 확인
   - HUD 초상화에 블록 타입 색상 테두리가 표시되는지 확인
