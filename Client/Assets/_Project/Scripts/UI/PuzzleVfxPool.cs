using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// 퍼즐 연출용 VFX 풀.
/// - 블록 파괴 파티클: 블록 비활성화/풀 반환과 무관하게 끝까지 재생되도록 분리 스폰
/// - 오브 흡수: 블록 위치 → 히어로 위치로 빠르게 이동 후 반환
/// </summary>
public class PuzzleVfxPool : MonoBehaviour
{
    [Header("Prefabs")]
    [SerializeField] private ParticleSystem _blockDestroyParticlePrefab;
    [SerializeField] private GameObject _orbPrefab;

    private readonly Queue<ParticleSystem> _destroyParticles = new();
    private readonly Queue<GameObject> _orbs = new();

    public void SpawnBlockDestroy(Vector3 worldPos, Color color)
    {
        var ps = RentDestroyParticle();
        if (ps == null) return;

        ps.transform.position = worldPos;
        ps.transform.rotation = Quaternion.identity;

        ApplyParticleColor(ps, color);
        ps.gameObject.SetActive(true);
        ps.Play(true);

        ReleaseAfterParticleDone(ps).Forget();
    }

    public void SpawnOrbToHero(Vector3 fromWorldPos, Vector3 toWorldPos, Color color, float duration, System.Action onArrived)
    {
        var orb = RentOrb();
        if (orb == null) { onArrived?.Invoke(); return; }

        orb.transform.DOKill(false);
        orb.transform.position = fromWorldPos;
        orb.transform.rotation = Quaternion.identity;
        orb.SetActive(true);

        ApplyOrbColor(orb, color);
        ClearOrbTrail(orb);

        orb.transform
            .DOMove(toWorldPos, duration)
            .SetEase(Ease.OutQuad)
            .OnComplete(() =>
            {
                onArrived?.Invoke();
                ReturnOrb(orb);
            });
    }

    private ParticleSystem RentDestroyParticle()
    {
        if (_blockDestroyParticlePrefab == null) return null;
        if (_destroyParticles.Count > 0)
        {
            var ps = _destroyParticles.Dequeue();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            return ps;
        }

        return Instantiate(_blockDestroyParticlePrefab, transform);
    }

    private GameObject RentOrb()
    {
        if (_orbPrefab == null) return null;
        if (_orbs.Count > 0)
            return _orbs.Dequeue();
        return Instantiate(_orbPrefab, transform);
    }

    private void ReturnOrb(GameObject orb)
    {
        if (orb == null) return;
        orb.transform.DOKill(false);
        orb.SetActive(false);
        _orbs.Enqueue(orb);
    }

    private async UniTaskVoid ReleaseAfterParticleDone(ParticleSystem ps)
    {
        if (ps == null) return;
        var main = ps.main;
        float total = main.duration + main.startLifetime.constantMax;
        int loops = main.loop ? 1 : 0;

        // loop 파티클이면 안전하게 duration만 기다리고 반환 (실제 프리팹은 loop=false를 권장)
        if (loops > 0) total = main.duration;

        int ms = Mathf.CeilToInt(total * 1000f);
        if (ms < 1) ms = 1;

        await UniTask.Delay(ms, cancellationToken: this.GetCancellationTokenOnDestroy());

        if (ps == null) return;
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ps.gameObject.SetActive(false);
        _destroyParticles.Enqueue(ps);
    }

    private static void ApplyParticleColor(ParticleSystem ps, Color color)
    {
        if (ps == null) return;
        foreach (var child in ps.GetComponentsInChildren<ParticleSystem>(true))
        {
            var m = child.main;
            m.startColor = color;
        }
    }

    private static void ApplyOrbColor(GameObject orb, Color color)
    {
        if (orb == null) return;

        ApplyTrailColor(orb, color);

        var sr = orb.GetComponentInChildren<SpriteRenderer>(true);
        if (sr != null)
        {
            sr.color = color;
            return;
        }

        var uiGraphic = orb.GetComponentInChildren<UnityEngine.UI.Graphic>(true);
        if (uiGraphic != null)
            uiGraphic.color = color;
    }

    private static void ApplyTrailColor(GameObject orb, Color color)
    {
        if (orb == null) return;

        var tr = orb.GetComponentInChildren<TrailRenderer>(true);
        if (tr == null) return;

        tr.startColor = color;
        tr.endColor = new Color(color.r, color.g, color.b, 0f);

        var g = tr.colorGradient;
        g.SetKeys(
            new[]
            {
                new GradientColorKey(color, 0f),
                new GradientColorKey(color, 1f)
            },
            new[]
            {
                new GradientAlphaKey(color.a, 0f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        tr.colorGradient = g;
    }

    private static void ClearOrbTrail(GameObject orb)
    {
        if (orb == null) return;
        var tr = orb.GetComponentInChildren<TrailRenderer>(true);
        tr?.Clear();
    }
}

