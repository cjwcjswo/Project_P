using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// 히어로 1명의 HUD 슬롯 컴포넌트.
/// BattleHUD가 파티 인원 수만큼 동적으로 생성하여 각 HeroState에 바인딩.
///
/// 프리팹 구성:
///   - Image (PortraitImage)       : 히어로 초상화
///   - Image (HPFill)              : Type=Filled, Method=Horizontal
///   - Image (ShieldFill)          : Type=Filled, Method=Horizontal, 기본 비활성
///   - Image (UltGaugeFill)        : Type=Filled, Method=Radial 360, FillOrigin=Top (초상화 테두리)
///   - Button (PortraitButton)     : 초상화 위에 배치, 초기 interactable=false
///   - GameObject (UltReadyEffect) : 만충 파티클/이펙트, 기본 비활성
///   - GameObject (DeadOverlay)    : 반투명 회색 오버레이, 기본 비활성
/// </summary>
public class HeroHUDView : MonoBehaviour
{
    [SerializeField] private Image      _portraitImage;
    [SerializeField] private Image      _hpFill;
    [SerializeField] private Image      _shieldFill;
    [SerializeField] private Image      _ultGaugeFill;
    [SerializeField] private GameObject _ultReadyEffect;
    [SerializeField] private Button     _portraitButton;
    [SerializeField] private GameObject _deadOverlay;

    private UltimateGaugeManager _ultManager;
    private int _heroIndex;

    public void Bind(HeroState hero, UltimateGaugeManager ultManager, System.Action onActivate)
    {
        _ultManager = ultManager;
        _heroIndex  = hero.PartyIndex;

        // 초기 상태 설정
        _hpFill.fillAmount   = 1f;
        _shieldFill.gameObject.SetActive(false);
        _ultGaugeFill.fillAmount = 0f;
        _ultReadyEffect.SetActive(false);
        _deadOverlay.SetActive(false);
        _portraitButton.interactable = false;

        // 3성 미만 히어로는 궁극기 UI 완전 숨김
        bool hasUltimate = hero.Grade >= 3;
        if (_ultGaugeFill != null) _ultGaugeFill.gameObject.SetActive(hasUltimate);
        if (_ultReadyEffect != null) _ultReadyEffect.SetActive(false);
        if (!hasUltimate) _portraitButton.interactable = false;

        // HP
        hero.OnHPChanged += (cur, max) =>
        {
            float ratio = (float)cur / max;
            _hpFill.DOFillAmount(ratio, 0.3f);
        };

        // 실드
        hero.OnShieldChanged += shield =>
        {
            _shieldFill.gameObject.SetActive(shield > 0);
        };

        // 사망
        hero.OnDeath += () =>
        {
            _deadOverlay.SetActive(true);
            _portraitButton.interactable = false;
            _ultReadyEffect.SetActive(false);
            _ultGaugeFill.DOKill();
        };

        if (!hasUltimate) return;

        // 궁극기 링 Image가 형제 순서상 초상화 Button 위에 있어 레이캐스트를 가로채지 않도록 한다.
        if (_ultGaugeFill != null)
            _ultGaugeFill.raycastTarget = false;

        DisableRaycastsUnderUltReadyEffect();

        // 궁극기 게이지 충전 (자기 heroIndex 필터링)
        ultManager.OnGaugeChanged += (idx, cur, max) =>
        {
            if (idx != _heroIndex) return;
            float ratio = (float)cur / max;
            _ultGaugeFill.DOFillAmount(ratio, 0.2f);
        };

        // 궁극기 만충
        ultManager.OnUltimateReady += idx =>
        {
            if (idx != _heroIndex) return;
            _ultReadyEffect.SetActive(true);
            DisableRaycastsUnderUltReadyEffect();
            _portraitButton.interactable = true;
            _ultGaugeFill.DOColor(new Color(1f, 0.6f, 0f), 0.5f)
                .SetLoops(-1, LoopType.Yoyo);
        };

        // 초상화 버튼 클릭 → 궁극기 발동
        _portraitButton.onClick.AddListener(() =>
        {
            if (!_ultManager.CanActivate(_heroIndex)) return;
            onActivate?.Invoke();
            _ultReadyEffect.SetActive(false);
            _portraitButton.interactable = false;
            _ultGaugeFill.DOKill();
            _ultGaugeFill.color = Color.white;
            // DOKill이 Activate 직후 시작된 DOFillAmount(0)까지 취소하므로 Fill을 즉시 0으로 맞춘다.
            _ultGaugeFill.fillAmount = 0f;
        });
    }

    /// <summary>Ready 이펙트 하위 UI Graphic이 버튼 클릭을 삼키지 않도록 한다.</summary>
    private void DisableRaycastsUnderUltReadyEffect()
    {
        if (_ultReadyEffect == null) return;
        foreach (var g in _ultReadyEffect.GetComponentsInChildren<Graphic>(true))
            g.raycastTarget = false;
    }
}
