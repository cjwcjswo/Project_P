using UnityEngine;
using DG.Tweening;

/// <summary>
/// 히어로 1명의 월드 스프라이트 컴포넌트.
/// BattleSceneView가 SpawnPoint 위치에 동적으로 Instantiate하여 HeroState에 바인딩.
///
/// 프리팹 구성:
///   - HeroModel (빈 Transform — 런타임 모델 인스턴스 부모)
///     └─ [런타임 로드] SpriteRenderer + Animator (HeroAnimator.controller)
///   - HeroEntityView 스크립트 부착
/// </summary>
public class HeroEntityView : MonoBehaviour
{
    [Tooltip("Outline Shader Material. 런타임에 MaterialPropertyBlock으로 색상/활성화를 제어한다.")]
    [SerializeField] private Material _outlineMaterial;

    private SpriteRenderer _spriteRenderer;
    private Transform      _modelRoot;
    private GameObject     _spawnedModel;
    private Animator       _animator;

    public int     PartyIndex    { get; private set; }
    public Vector3 WorldPosition => transform.position;

    private Color _baseColor;

    private static readonly int OutlineColorId     = Shader.PropertyToID("_OutlineColor");
    private static readonly int OutlineThicknessId = Shader.PropertyToID("_OutlineThickness");
    private static readonly int OutlineEnabledId   = Shader.PropertyToID("_OutlineEnabled");

    public void Bind(HeroState hero)
    {
        PartyIndex = hero.PartyIndex;
        AttachModelFromPrefabPath(hero.PrefabPath);
        CacheBaseColor();
        ApplyOutline(hero.MappedColor);

        hero.OnDamageTaken += _ => PlayDamageFlash(new Color(1f, 0.3f, 0.3f));
        hero.OnDeath       += PlayDeathAnim;
    }

    // ── 애니메이션 ────────────────────────────────────────────────────────

    /// <summary>평타 발동 시 Attack 상태 재생</summary>
    public void PlayAttackAnim()
    {
        _animator?.SetTrigger("Attack");
    }

    /// <summary>스킬 발동 시 Skill 상태 재생</summary>
    public void PlaySkillAnim()
    {
        _animator?.SetTrigger("Skill");
    }

    /// <summary>웨이브 이동 시 Run 상태 진입</summary>
    public void PlayRunAnim()
    {
        _animator?.SetTrigger("Run");
    }

    /// <summary>웨이브 이동 완료 후 Idle 복귀</summary>
    public void StopRunAnim()
    {
        _animator?.SetTrigger("BackToIdle");
    }

    /// <summary>사망 시 Death 상태로 고정 전환 (IsDead Bool)</summary>
    public void PlayDeathAnim()
    {
        _animator?.SetBool("IsDead", true);
    }

    /// <summary>스킬 효과 발동 시 색상 강조 (회복, 방어막 등)</summary>
    public void PlayColorFlash(Color flashColor)
    {
        FlashColor(flashColor, _baseColor, 0.3f);
    }

    /// <summary>피격 시 붉은 플래시</summary>
    public void PlayDamageFlash(Color flashColor)
    {
        FlashColor(flashColor, _baseColor, 0.2f);
    }

    // ── 외곽선 제어 ───────────────────────────────────────────────────────

    /// <summary>블록 타입에 맞는 외곽선 색상을 MaterialPropertyBlock으로 적용.</summary>
    private void ApplyOutline(BlockType blockType)
    {
        if (_spriteRenderer == null) return;
        if (_outlineMaterial != null)
            _spriteRenderer.sharedMaterial = _outlineMaterial;

        var mpb = new MaterialPropertyBlock();
        _spriteRenderer.GetPropertyBlock(mpb);
        mpb.SetColor(OutlineColorId, BlockTypeColors.Get(blockType));
        mpb.SetFloat(OutlineThicknessId, 0.002f);  // UV 공간 오프셋 (0.002=얇음, 0.004=기본, 0.008=두꺼움)
        mpb.SetFloat(OutlineEnabledId, 1f);
        _spriteRenderer.SetPropertyBlock(mpb);
    }

    // ── 헬퍼 ──────────────────────────────────────────────────────────────

    private void FlashColor(Color flashColor, Color baseColor, float duration)
    {
        if (_spriteRenderer == null) return;
        _spriteRenderer.DOKill(false);
        DOTween.Sequence()
            .Append(_spriteRenderer.DOColor(flashColor, duration * 0.4f).SetEase(Ease.OutQuad))
            .Append(_spriteRenderer.DOColor(baseColor,  duration * 0.6f).SetEase(Ease.InQuad));
    }

    private void Reset()
    {
        _modelRoot = transform.Find("HeroModel");
    }

    private void AttachModelFromPrefabPath(string prefabPath)
    {
        if (_spawnedModel != null)
        {
            Destroy(_spawnedModel);
            _spawnedModel   = null;
            _animator       = null;
            _spriteRenderer = null;
        }

        if (_modelRoot == null) _modelRoot = transform.Find("HeroModel");

        if (string.IsNullOrEmpty(prefabPath))
        {
            // PrefabPath 없음: 자식에 직접 배치된 컴포넌트 사용
            _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            _animator       = GetComponentInChildren<Animator>();
            return;
        }

        if (_modelRoot == null)
        {
            Debug.LogWarning($"[HeroEntityView] HeroModel Transform을 찾을 수 없습니다. PrefabPath='{prefabPath}'");
            _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            return;
        }

        var prefab = Resources.Load<GameObject>(prefabPath);
        if (prefab == null)
        {
            Debug.LogWarning($"[HeroEntityView] Resources 경로에 모델 프리팹이 없습니다: '{prefabPath}'");
            _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            return;
        }

        _spawnedModel = Instantiate(prefab, _modelRoot, worldPositionStays: false);
        _spawnedModel.transform.localPosition = Vector3.zero;
        _spawnedModel.transform.localRotation = Quaternion.identity;
        _spawnedModel.transform.localScale    = Vector3.one;

        _animator = _spawnedModel.GetComponentInChildren<Animator>(includeInactive: true);
        if (_animator == null)
            Debug.LogWarning($"[HeroEntityView] Animator를 찾을 수 없습니다. PrefabPath='{prefabPath}'");

        _spriteRenderer = _spawnedModel.GetComponentInChildren<SpriteRenderer>(includeInactive: true)
                          ?? GetComponentInChildren<SpriteRenderer>();
    }

    private void CacheBaseColor()
    {
        if (_spriteRenderer == null)
            _spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        _baseColor = _spriteRenderer != null ? _spriteRenderer.color : Color.white;
    }
}
