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

    [Header("Motion (프리팹마다 데미지/스킬 등으로 구분)")]
    [SerializeField] private float _spawnYOffset   = 0f;
    [SerializeField] private float _floatDistance  = 0.8f;
    [SerializeField] private float _totalDuration  = 1.0f;
    [SerializeField] private float _fadeDuration   = 0.4f;

    private Sequence _sequence;

    /// <summary>인스턴스 생성 시 TMP(프리팹)에 설정된 Face 색. 풀 재사용 시 알파 복원에 사용.</summary>
    private Color _faceColorFromPrefab;

    private void Awake()
    {
        _faceColorFromPrefab = _tmp.color;
    }

    /// <summary>풀 소유자가 설정. 애니메이션 완료 시 호출되어 풀에 반환.</summary>
    public Action<FloatingTextView> OnRelease { get; set; }

    /// <summary>
    /// 지정 위치에서 텍스트를 표시하고 위로 이동하며 페이드 아웃 후 풀로 반환.
    /// 글자 색은 프리팹·TMP에 설정된 색을 그대로 쓰며, 코드에서 색을 바꾸지 않는다.
    /// </summary>
    public void Show(string text, Vector3 worldPos)
    {
        _sequence?.Kill();

        var start = worldPos + Vector3.up * _spawnYOffset;
        transform.position = start;
        _tmp.text   = text;
        _tmp.color  = _faceColorFromPrefab;
        gameObject.SetActive(true);

        float moveDur = Mathf.Max(0.01f, _totalDuration);
        float fadeDur = Mathf.Clamp(_fadeDuration, 0.01f, moveDur);
        var endPos = start + Vector3.up * _floatDistance;

        _sequence = DOTween.Sequence();
        _sequence
            .Join(transform.DOMove(endPos, moveDur).SetEase(Ease.OutCubic))
            .Join(DOTween.To(
                () => _tmp.alpha,
                a  => _tmp.alpha = a,
                0f,
                fadeDur
            ).SetDelay(moveDur - fadeDur).SetEase(Ease.InQuad))
            .OnComplete(() =>
            {
                _sequence = null;
                gameObject.SetActive(false);
                OnRelease?.Invoke(this);
            });
    }

    private void Reset()
    {
        _tmp = GetComponent<TextMeshPro>();
    }
}
