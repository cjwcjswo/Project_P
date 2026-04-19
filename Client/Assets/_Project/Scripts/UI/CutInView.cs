using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 3성 궁극기 발동 시 히어로 일러스트가 좌→우로 슬라이드 인/아웃하는 컷인 연출.
/// BattleManager.ActivateUltimateAsync()에서 PlayAsync(Sprite)를 await.
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
        gameObject.SetActive(false);
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

        // 슬라이드 인 + 페이드 인
        var seq = DOTween.Sequence();
        seq.Join(_illustrationRect.DOAnchorPosX(endX, _slideDuration).SetEase(Ease.OutCubic));
        seq.Join(_canvasGroup.DOFade(0.9f, _slideDuration));
        await seq.ToUniTask();

        // 홀드
        await UniTask.Delay(System.TimeSpan.FromSeconds(_holdDuration));

        // 슬라이드 아웃 + 페이드 아웃
        var seqOut = DOTween.Sequence();
        seqOut.Join(_illustrationRect.DOAnchorPosX(screenWidth, _fadeOutDuration).SetEase(Ease.InCubic));
        seqOut.Join(_canvasGroup.DOFade(0f, _fadeOutDuration));
        await seqOut.ToUniTask();

        gameObject.SetActive(false);
    }
}
