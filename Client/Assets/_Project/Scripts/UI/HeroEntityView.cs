using UnityEngine;
using DG.Tweening;

/// <summary>
/// 히어로 1명의 월드 스프라이트 컴포넌트.
/// BattleSceneView가 SpawnPoint 위치에 동적으로 Instantiate하여 HeroState에 바인딩.
///
/// 프리팹 구성:
///   - SpriteRenderer (캐릭터 스프라이트)
///   - HeroEntityView 스크립트 부착
/// </summary>
public class HeroEntityView : MonoBehaviour
{
    [SerializeField] private SpriteRenderer _spriteRenderer;

    public int     PartyIndex    { get; private set; }
    public Vector3 WorldPosition => transform.position;

    private Color _baseColor;

    public void Bind(HeroState hero)
    {
        PartyIndex = hero.PartyIndex;
        _baseColor = _spriteRenderer.color;

        hero.OnDamageTaken += _ => PlayDamageFlash(new Color(1f, 0.3f, 0.3f));
        hero.OnDeath       += PlayDeathAnim;
    }

    // ── 애니메이션 ────────────────────────────────────────────────────────

    /// <summary>평타/스킬 발동 시 우측으로 펀치</summary>
    public void PlayAttackAnim()
    {
        transform.DOPunchPosition(Vector3.right * 0.15f, 0.2f, 5, 0.5f);
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

    /// <summary>사망 시 회색화 후 반투명 처리</summary>
    public void PlayDeathAnim()
    {
        _spriteRenderer.DOKill(false);
        DOTween.Sequence()
            .Append(_spriteRenderer.DOColor(Color.gray, 0.3f))
            .Append(_spriteRenderer.DOFade(0.3f, 0.4f));
    }

    // ── 헬퍼 ──────────────────────────────────────────────────────────────

    private void FlashColor(Color flashColor, Color baseColor, float duration)
    {
        _spriteRenderer.DOKill(false);
        DOTween.Sequence()
            .Append(_spriteRenderer.DOColor(flashColor, duration * 0.4f).SetEase(Ease.OutQuad))
            .Append(_spriteRenderer.DOColor(baseColor,  duration * 0.6f).SetEase(Ease.InQuad));
    }

    private void Reset()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
    }
}
