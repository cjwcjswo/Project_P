using System;
using DG.Tweening;
using TMPro;
using UnityEngine;

/// <summary>
/// 월드 스페이스에서 위로 떠오르며 페이드 아웃되는 플로팅 텍스트.
/// TextMeshPro(3D 메시 텍스트) 기반. Show() 호출 후 애니메이션 완료 시 OnRelease 콜백으로 풀에 반환.
/// </summary>
[RequireComponent(typeof(TextMeshPro))]
public class FloatingTextView : MonoBehaviour
{
    [SerializeField] private TextMeshPro _tmp;

    private const float FloatDistance = 0.8f;
    private const float TotalDuration = 1.0f;
    private const float FadeDuration  = 0.4f;

    /// <summary>풀 소유자가 설정. 애니메이션 완료 시 호출되어 풀에 반환.</summary>
    public Action<FloatingTextView> OnRelease { get; set; }

    /// <summary>
    /// 지정 위치에서 텍스트를 표시하고 위로 이동하며 페이드 아웃 후 풀로 반환.
    /// </summary>
    public void Show(string text, Color color, Vector3 worldPos)
    {
        transform.position = worldPos;
        _tmp.text          = text;
        _tmp.color         = color;
        _tmp.alpha         = 1f;
        gameObject.SetActive(true);

        var endPos = worldPos + Vector3.up * FloatDistance;

        DOTween.Sequence()
            .Join(transform.DOMove(endPos, TotalDuration).SetEase(Ease.OutCubic))
            .Join(DOTween.To(
                () => _tmp.alpha,
                a  => _tmp.alpha = a,
                0f,
                FadeDuration
            ).SetDelay(TotalDuration - FadeDuration).SetEase(Ease.InQuad))
            .OnComplete(() =>
            {
                gameObject.SetActive(false);
                OnRelease?.Invoke(this);
            });
    }

    private void Reset()
    {
        _tmp = GetComponent<TextMeshPro>();
    }
}
