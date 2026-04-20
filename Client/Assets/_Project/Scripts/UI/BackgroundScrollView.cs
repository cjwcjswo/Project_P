using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// 웨이브 전환 시 배경 레이어를 패럴랙스 방식으로 좌측 스크롤하는 컴포넌트.
/// BattleSceneView.WaveTransitionAsync에서 ScrollAsync를 await하여 사용한다.
///
/// 씬에 배경 레이어가 없을 경우(null 또는 빈 배열)에도 _scrollDuration 만큼 대기 후
/// 정상 반환하므로 배경 없는 상태에서도 웨이브 전환 타이밍이 보장된다.
/// </summary>
public class BackgroundScrollView : MonoBehaviour
{
    [Header("Parallax Layers")]
    [Tooltip("원경 → 근경 순으로 할당. 비워두면 단순 Delay로 동작.")]
    [SerializeField] private Transform[] _bgLayers;

    [Tooltip("레이어별 스크롤 속도 배수 (배열 크기 = _bgLayers 크기). 미지정 시 1.0 적용.")]
    [SerializeField] private float[]     _layerSpeedMultipliers;

    [Header("Scroll Settings")]
    [SerializeField] private float _scrollDistance = 12f;
    [SerializeField] private float _scrollDuration  = 1.2f;

    private Vector3[] _originPositions;

    private void Awake()
    {
        CacheOriginPositions();
    }

    /// <summary>
    /// 모든 배경 레이어를 왼쪽으로 스크롤한다. 완료까지 await한다.
    /// 레이어가 없으면 _scrollDuration 동안 대기 후 반환.
    /// </summary>
    public async UniTask ScrollAsync(CancellationToken ct)
    {
        if (_bgLayers == null || _bgLayers.Length == 0)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(_scrollDuration), cancellationToken: ct);
            return;
        }

        var tasks = new List<UniTask>(_bgLayers.Length);
        for (int i = 0; i < _bgLayers.Length; i++)
        {
            if (_bgLayers[i] == null) continue;

            float multiplier = (_layerSpeedMultipliers != null && i < _layerSpeedMultipliers.Length)
                ? _layerSpeedMultipliers[i]
                : 1f;

            float targetX = _bgLayers[i].position.x - _scrollDistance * multiplier;
            var tween = _bgLayers[i]
                .DOMoveX(targetX, _scrollDuration)
                .SetEase(Ease.InOutSine);

            tasks.Add(tween.ToUniTask(cancellationToken: ct));
        }

        await UniTask.WhenAll(tasks);
    }

    /// <summary>
    /// 모든 레이어를 초기 위치로 순간 이동시킨다. 다음 웨이브 전환 준비용.
    /// </summary>
    public void ResetPositions()
    {
        if (_bgLayers == null || _originPositions == null) return;

        for (int i = 0; i < _bgLayers.Length; i++)
        {
            if (_bgLayers[i] == null) continue;
            if (i >= _originPositions.Length) break;
            _bgLayers[i].position = _originPositions[i];
        }
    }

    private void CacheOriginPositions()
    {
        if (_bgLayers == null) return;

        _originPositions = new Vector3[_bgLayers.Length];
        for (int i = 0; i < _bgLayers.Length; i++)
        {
            if (_bgLayers[i] != null)
                _originPositions[i] = _bgLayers[i].position;
        }
    }
}
