using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 3성 궁극기 발동 시 히어로 일러스트가 좌→우로 슬라이드 인/아웃하는 컷인 연출.
/// BattleManager.ActivateUltimateAsync()에서 PlayAsync(Sprite)를 Forget()으로 병행 재생.
/// </summary>
public class CutInView : MonoBehaviour
{
    [SerializeField] private Image       _illustrationImage;
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private RectTransform _illustrationRect;

    [Header("Animation Settings")]
    [SerializeField] private float _slideDuration  = 0.3f;
    [SerializeField] private float _holdDuration   = 0.6f;
    [SerializeField] private float _fadeOutDuration = 0.3f;

    private void Awake()
    {
        // 씬에서 비활성(m_IsActive:0)이면 Awake는 첫 SetActive(true)까지 지연된다.
        // PlayAsync가 SetActive(true)한 직후 이 Awake에서 다시 SetActive(false)를 호출하면
        // 첫 궁극기 연출이 즉시 꺼져 보이지 않는다(두 번째부터는 Awake가 재실행되지 않아 정상).
        if (_canvasGroup != null)
        {
            _canvasGroup.alpha           = 0f;
            _canvasGroup.blocksRaycasts  = false;
            _canvasGroup.interactable    = false;
        }
    }

    /// <summary>
    /// 일러스트를 좌측에서 우측으로 슬라이드하며 페이드인→홀드→페이드아웃 재생.
    /// </summary>
    public async UniTask PlayAsync(Sprite illustration)
    {
        if (illustration == null) return;

        _illustrationImage.sprite = illustration;

        float screenWidth = ((RectTransform)transform).rect.width;
        if (screenWidth <= 0f) screenWidth = Screen.width;

        float startX = -screenWidth;
        float endX   = screenWidth * 0.15f;

        _illustrationRect.anchoredPosition = new Vector2(startX, _illustrationRect.anchoredPosition.y);
        _canvasGroup.alpha = 0f;
        gameObject.SetActive(true);

        // 슬라이드 인 + 페이드 인 (병렬)
        await UniTask.WhenAll(
            _illustrationRect.DOAnchorPosX(endX, _slideDuration).SetEase(Ease.OutCubic).ToUniTask(),
            _canvasGroup.DOFade(0.9f, _slideDuration).ToUniTask()
        );

        // 홀드
        await UniTask.Delay(System.TimeSpan.FromSeconds(_holdDuration));

        // 슬라이드 아웃 + 페이드 아웃 (병렬)
        await UniTask.WhenAll(
            _illustrationRect.DOAnchorPosX(screenWidth, _fadeOutDuration).SetEase(Ease.InCubic).ToUniTask(),
            _canvasGroup.DOFade(0f, _fadeOutDuration).ToUniTask()
        );

        gameObject.SetActive(false);
    }
}
