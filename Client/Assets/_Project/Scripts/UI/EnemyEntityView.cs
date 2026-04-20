using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// 적 1마리의 월드 스프라이트 + 월드 스페이스 HUD 컴포넌트.
/// BattleSceneView가 SpawnPoint 위치에 동적으로 Instantiate하여 EnemyState에 바인딩.
///
/// 프리팹 구성:
///   - MonsterModel (빈 Transform — 런타임 모델 인스턴스 부모)
///     └─ [런타임 로드] SpriteRenderer + Animator (MonsterAnimator.controller)
///   - Canvas (World Space) — 스프라이트 상단에 배치
///     ├─ HPFill (Image, Filled)
///     ├─ CastingBarGroup
///     │  └─ CastingBarFill (Image, Filled)
///     └─ CooldownGroup
///        └─ CooldownFill (Image, Filled)
///   - Collider2D (일점사 터치 영역)
///   - EnemyEntityView 스크립트 부착
///
/// 웨이브 전환 시 뷰가 Destroy되기 전에 반드시 UnbindAndKillTweens()를 호출해야
/// EnemyState 이벤트와 DOTween이 파괴된 컴포넌트를 건드리지 않는다.
/// </summary>
public class EnemyEntityView : MonoBehaviour
{
    [Header("World-Space HUD")]
    [SerializeField] private Image      _hpFill;
    [SerializeField] private GameObject _castingBarGroup;
    [SerializeField] private Image      _castingBarFill;

    [Tooltip("스킬 쿨다운 진행 Fill Image. fillAmount: 0=쿨다운 시작, 1=발동 가능. 없으면 무시.")]
    [SerializeField] private Image      _cooldownFill;
    [Tooltip("쿨다운 Fill을 감싸는 루트(항상 표시). 스킬 없는 몬스터는 숨긴다.")]
    [SerializeField] private GameObject _cooldownGroup;

    [Tooltip("Outline Shader Material. 런타임에 MaterialPropertyBlock으로 색상/활성화를 제어한다.")]
    [SerializeField] private Material   _outlineMaterial;

    public int     WaveIndex     { get; private set; }
    public Vector3 WorldPosition => transform.position;

    private Color           _baseColor;
    private TargetingSystem _targeting;
    private EnemyState      _boundEnemy;
    private bool            _skillHudRegistered;
    private bool            _cooldownRegistered;

    // ── 모델 동적 로드 ────────────────────────────────────────────────────
    private SpriteRenderer _spriteRenderer;
    private Animator       _animator;
    private Transform      _modelRoot;
    private GameObject     _spawnedModel;

    private static readonly int OutlineColorId     = Shader.PropertyToID("_OutlineColor");
    private static readonly int OutlineThicknessId = Shader.PropertyToID("_OutlineThickness");
    private static readonly int OutlineEnabledId   = Shader.PropertyToID("_OutlineEnabled");

    public void Bind(EnemyState enemy)
    {
        UnbindAndKillTweens();

        _boundEnemy = enemy;
        WaveIndex   = enemy.WaveIndex;

        AttachModelFromPrefabPath(enemy.PrefabPath);
        CacheBaseColor();

        enemy.OnDamageTaken += OnEnemyDamageTaken;
        enemy.OnDeath       += PlayDeathAnim;
    }

    /// <summary>
    /// 월드 스페이스 HUD 바인딩: HP 바, 캐스팅 바, 쿨다운 바, 일점사 터치.
    /// BattleSceneView가 Bind() 이후 호출.
    /// </summary>
    public void BindHUD(EnemyState enemy, TargetingSystem targeting)
    {
        _targeting = targeting;

        if (_hpFill != null && _boundEnemy != null)
        {
            _hpFill.fillAmount = 1f;
            _boundEnemy.OnHPChanged += OnEnemyHPChanged;
        }

        bool hasSkill = enemy.SkillCooldown > 0f;

        // 캐스팅 바
        if (_castingBarGroup != null)
        {
            _castingBarGroup.SetActive(false);

            if (hasSkill && _boundEnemy != null)
            {
                _boundEnemy.OnSkillCastProgress += OnEnemySkillCastProgress;
                _boundEnemy.OnSkillCast         += OnEnemySkillCastEnd;
                _skillHudRegistered = true;
            }
        }

        // 쿨다운 Fill (스킬 없는 몬스터는 숨김)
        if (_cooldownGroup != null)
            _cooldownGroup.SetActive(hasSkill);

        if (hasSkill && _boundEnemy != null)
        {
            if (_cooldownFill != null) _cooldownFill.fillAmount = 0f;
            _boundEnemy.OnCooldownChanged   += OnEnemyCooldownChanged;
            _boundEnemy.OnSkillCastProgress += OnEnemyCooldownDuringCast;
            _boundEnemy.OnSkillCast         += OnEnemyCooldownReset;
            _cooldownRegistered = true;
        }
    }

    /// <summary>
    /// EnemyState 구독 해제 및 이 뷰에 걸린 DOTween 전부 Kill.
    /// 웨이브 전환 시 BattleSceneView가 Destroy 직전에 호출한다.
    /// </summary>
    public void UnbindAndKillTweens()
    {
        if (_boundEnemy != null)
        {
            _boundEnemy.OnDamageTaken -= OnEnemyDamageTaken;
            _boundEnemy.OnDeath       -= PlayDeathAnim;
            _boundEnemy.OnHPChanged   -= OnEnemyHPChanged;

            if (_skillHudRegistered)
            {
                _boundEnemy.OnSkillCastProgress -= OnEnemySkillCastProgress;
                _boundEnemy.OnSkillCast         -= OnEnemySkillCastEnd;
                _skillHudRegistered = false;
            }

            if (_cooldownRegistered)
            {
                _boundEnemy.OnCooldownChanged   -= OnEnemyCooldownChanged;
                _boundEnemy.OnSkillCastProgress -= OnEnemyCooldownDuringCast;
                _boundEnemy.OnSkillCast         -= OnEnemyCooldownReset;
                _cooldownRegistered = false;
            }

            _boundEnemy = null;
        }

        if (_spriteRenderer != null)
            DOTween.Kill(_spriteRenderer, complete: false);
        if (_hpFill != null)
            DOTween.Kill(_hpFill, complete: false);
        if (_castingBarFill != null)
            DOTween.Kill(_castingBarFill, complete: false);

        transform.DOKill(complete: false);
        _targeting = null;

        // 동적으로 스폰된 모델 정리
        if (_spawnedModel != null)
        {
            Destroy(_spawnedModel);
            _spawnedModel   = null;
            _animator       = null;
            _spriteRenderer = null;
        }
    }

    private void OnDestroy()
    {
        UnbindAndKillTweens();
    }

    /// <summary>Collider2D 터치로 일점사 타겟 지정.</summary>
    private void OnMouseDown()
    {
        _targeting?.SetManualTarget(WaveIndex);
    }

    // ── 모델 동적 로드 ────────────────────────────────────────────────────

    private void AttachModelFromPrefabPath(string prefabPath)
    {
        if (_spawnedModel != null)
        {
            Destroy(_spawnedModel);
            _spawnedModel   = null;
            _animator       = null;
            _spriteRenderer = null;
        }

        if (_modelRoot == null)
            _modelRoot = transform.Find("MonsterModel");

        if (string.IsNullOrEmpty(prefabPath))
        {
            // PrefabPath 없음: 프리팹에 직접 배치된 SpriteRenderer 사용 (하위 호환)
            _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            _animator       = GetComponentInChildren<Animator>();
            return;
        }

        if (_modelRoot == null)
        {
            Debug.LogWarning($"[EnemyEntityView] 'MonsterModel' Transform을 찾을 수 없습니다. PrefabPath='{prefabPath}'");
            _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            return;
        }

        var prefab = Resources.Load<GameObject>(prefabPath);
        if (prefab == null)
        {
            Debug.LogWarning($"[EnemyEntityView] Resources 경로에 모델 프리팹이 없습니다: '{prefabPath}'");
            _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            return;
        }

        _spawnedModel = Instantiate(prefab, _modelRoot, worldPositionStays: false);
        _spawnedModel.transform.localPosition = Vector3.zero;
        _spawnedModel.transform.localRotation = Quaternion.identity;
        _spawnedModel.transform.localScale    = Vector3.one;

        _animator = _spawnedModel.GetComponentInChildren<Animator>(includeInactive: true);
        if (_animator == null)
            Debug.LogWarning($"[EnemyEntityView] Animator를 찾을 수 없습니다. PrefabPath='{prefabPath}'");

        _spriteRenderer = _spawnedModel.GetComponentInChildren<SpriteRenderer>(includeInactive: true);

        // 외곽선 Material 적용 (할당된 경우)
        if (_outlineMaterial != null && _spriteRenderer != null)
            _spriteRenderer.sharedMaterial = _outlineMaterial;
    }

    private void CacheBaseColor()
    {
        if (_spriteRenderer == null)
            _spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        _baseColor = _spriteRenderer != null ? _spriteRenderer.color : Color.white;
    }

    // ── 외곽선 제어 ───────────────────────────────────────────────────────

    /// <summary>
    /// 타겟 하이라이트 외곽선 ON/OFF.
    /// TargetingSystem에서 수동 타겟 변경 시 호출.
    /// </summary>
    public void SetOutline(Color color, bool enabled)
    {
        if (_spriteRenderer == null) return;
        var mpb = new MaterialPropertyBlock();
        _spriteRenderer.GetPropertyBlock(mpb);
        mpb.SetColor(OutlineColorId, color);
        mpb.SetFloat(OutlineThicknessId, 0.004f);  // UV 공간 오프셋 (0.002=얇음, 0.004=기본, 0.008=두꺼움)
        mpb.SetFloat(OutlineEnabledId, enabled ? 1f : 0f);
        _spriteRenderer.SetPropertyBlock(mpb);
    }

    // ── EnemyState 핸들러 ─────────────────────────────────────────────────

    private void OnEnemyDamageTaken(int _)
    {
        PlayHitFlash(new Color(1f, 0.5f, 0.5f));
    }

    private void OnEnemyHPChanged(int cur, int max)
    {
        if (_hpFill == null) return;
        float ratio = (float)cur / max;
        _hpFill.DOKill(complete: false);
        _hpFill.DOFillAmount(ratio, 0.3f).SetLink(gameObject);
    }

    private void OnEnemySkillCastProgress(float progress)
    {
        if (_castingBarGroup == null) return;
        _castingBarGroup.SetActive(progress > 0f);
        if (_castingBarFill != null)
            _castingBarFill.fillAmount = progress;
    }

    private void OnEnemySkillCastEnd()
    {
        if (_castingBarGroup != null)
            _castingBarGroup.SetActive(false);
    }

    // ── 쿨다운 Fill 핸들러 ────────────────────────────────────────────────

    private void OnEnemyCooldownChanged(float remaining, float total)
    {
        if (_cooldownFill == null || total <= 0f) return;
        float fill = 1f - remaining / total;
        _cooldownFill.DOKill(complete: false);
        _cooldownFill.DOFillAmount(fill, 0.4f).SetLink(gameObject);
    }

    private void OnEnemyCooldownDuringCast(float progress)
    {
        if (_cooldownGroup == null) return;
        _cooldownGroup.SetActive(progress <= 0f);
        if (progress > 0f && _cooldownFill != null)
        {
            _cooldownFill.DOKill(complete: false);
            _cooldownFill.fillAmount = 1f;
        }
    }

    private void OnEnemyCooldownReset()
    {
        if (_cooldownGroup != null) _cooldownGroup.SetActive(true);
        if (_cooldownFill  != null)
        {
            _cooldownFill.DOKill(complete: false);
            _cooldownFill.fillAmount = 0f;
        }
    }

    // ── 애니메이션 ────────────────────────────────────────────────────────

    /// <summary>평타 발동 시 Attack 트리거. Animator 없으면 DOTween 폴백.</summary>
    public void PlayAttackAnim()
    {
        if (_animator != null)
        {
            _animator.SetTrigger("Attack");
        }
        else
        {
            transform.DOPunchPosition(Vector3.left * 0.2f, 0.2f, 5, 0.5f).SetLink(gameObject);
        }
    }

    /// <summary>스킬 시전 시 Skill 트리거 재생.</summary>
    public void PlaySkillAnim()
    {
        _animator?.SetTrigger("Skill");
    }

    /// <summary>단일/강화 공격 피격 시 색상 플래시</summary>
    public void PlayHitFlash(Color flashColor)
    {
        if (_spriteRenderer == null) return;
        _spriteRenderer.DOKill(complete: false);
        DOTween.Sequence()
            .SetLink(gameObject)
            .Append(_spriteRenderer.DOColor(flashColor, 0.08f).SetEase(Ease.OutQuad))
            .Append(_spriteRenderer.DOColor(_baseColor,  0.12f).SetEase(Ease.InQuad));
    }

    /// <summary>광역 공격 피격 시 흔들림</summary>
    public void PlayAoEHitAnim(Color flashColor)
    {
        if (_spriteRenderer == null) return;
        transform.DOShakePosition(0.4f, 0.15f, 10, 90f).SetLink(gameObject);
        _spriteRenderer.DOKill(complete: false);
        DOTween.Sequence()
            .SetLink(gameObject)
            .Append(_spriteRenderer.DOColor(flashColor, 0.1f).SetEase(Ease.OutQuad))
            .Append(_spriteRenderer.DOColor(_baseColor,  0.15f).SetEase(Ease.InQuad));
    }

    /// <summary>사망 시 Animator Bool 세팅 + DOTween 페이드 폴백.</summary>
    public void PlayDeathAnim()
    {
        if (_animator != null)
        {
            _animator.SetBool("IsDead", true);
        }

        if (_spriteRenderer == null) return;
        _spriteRenderer.DOKill(complete: false);
        DOTween.Sequence()
            .SetLink(gameObject)
            .Append(_spriteRenderer.DOColor(Color.gray, 0.2f))
            .Append(_spriteRenderer.DOFade(0f, 0.4f))
            .OnComplete(() =>
            {
                if (this == null) return;
                gameObject.SetActive(false);
            });
    }

    private void Reset()
    {
        // SpriteRenderer는 동적 모델 로드 후 캐싱되므로 Reset에서는 설정하지 않음
    }
}
