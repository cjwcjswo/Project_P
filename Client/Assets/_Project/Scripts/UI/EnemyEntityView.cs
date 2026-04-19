using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// 적 1마리의 월드 스프라이트 + 월드 스페이스 HUD 컴포넌트.
/// BattleSceneView가 SpawnPoint 위치에 동적으로 Instantiate하여 EnemyState에 바인딩.
///
/// 웨이브 전환 시 뷰가 Destroy되기 전에 반드시 UnbindAndKillTweens()를 호출해야
/// EnemyState 이벤트와 DOTween이 파괴된 SpriteRenderer/Image를 건드리지 않는다.
///
/// 프리팹 구성:
///   - SpriteRenderer (적 캐릭터 스프라이트)
///   - Canvas (World Space) — 스프라이트 상단에 배치
///     ├─ HPFill (Image, Filled)
///     ├─ CastingBarGroup
///     │  └─ CastingBarFill (Image, Filled)
///   - Collider2D (일점사 터치 영역)
///   - EnemyEntityView 스크립트 부착
/// </summary>
public class EnemyEntityView : MonoBehaviour
{
    [SerializeField] private SpriteRenderer _spriteRenderer;

    [Header("World-Space HUD")]
    [SerializeField] private Image      _hpFill;
    [SerializeField] private GameObject _castingBarGroup;
    [SerializeField] private Image      _castingBarFill;

    public int     WaveIndex     { get; private set; }
    public Vector3 WorldPosition => transform.position;

    private Color _baseColor;
    private TargetingSystem _targeting;
    private EnemyState _boundEnemy;
    private bool _skillHudRegistered;

    public void Bind(EnemyState enemy)
    {
        UnbindAndKillTweens();

        _boundEnemy = enemy;
        WaveIndex   = enemy.WaveIndex;
        _baseColor  = _spriteRenderer.color;

        enemy.OnDamageTaken += OnEnemyDamageTaken;
        enemy.OnDeath       += PlayDeathAnim;
    }

    /// <summary>
    /// 월드 스페이스 HUD 바인딩: HP 바, 캐스팅 바, 사망 오버레이, 일점사 터치.
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
        if (_castingBarGroup != null)
        {
            _castingBarGroup.SetActive(false);

            if (hasSkill && _boundEnemy != null)
            {
                _boundEnemy.OnSkillCastProgress += OnEnemySkillCastProgress;
                _boundEnemy.OnSkillCast        += OnEnemySkillCastEnd;
                _skillHudRegistered = true;
            }
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

    // ── EnemyState 핸들러 (이름 있어야 Unbind에서 -= 가능) ───────────────

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

    // ── 애니메이션 ────────────────────────────────────────────────────────

    /// <summary>평타 발동 시 좌측으로 펀치</summary>
    public void PlayAttackAnim()
    {
        transform.DOPunchPosition(Vector3.left * 0.2f, 0.2f, 5, 0.5f).SetLink(gameObject);
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

    /// <summary>사망 시 페이드 아웃</summary>
    public void PlayDeathAnim()
    {
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
        _spriteRenderer = GetComponent<SpriteRenderer>();
    }
}
