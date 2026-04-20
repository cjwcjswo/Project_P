using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class TargetingSystem
{
    private int _manualTargetIndex = -1; // 유저가 탭으로 지정한 적 인덱스

    // ── 레거시 메서드 (하위 호환 유지) ──────────────────────────────────────

    /// <summary>
    /// 아군 → 적 타겟 결정 (수동 지정 우선, 없으면 전방 우선).
    /// </summary>
    public EnemyState GetPriorityTarget(List<EnemyState> aliveEnemies)
    {
        if (aliveEnemies == null || aliveEnemies.Count == 0) return null;

        if (_manualTargetIndex >= 0)
        {
            var manual = aliveEnemies.FirstOrDefault(e => e.WaveIndex == _manualTargetIndex);
            if (manual != null) return manual;
            _manualTargetIndex = -1;
        }

        var result = Resolve(aliveEnemies, TargetStrategy.Front, 1);
        return result.Count > 0 ? result[0] : null;
    }

    /// <summary>유저 탭으로 일점사 타겟 지정.</summary>
    public void SetManualTarget(int enemyWaveIndex)
    {
        _manualTargetIndex = enemyWaveIndex;
    }

    /// <summary>수동 타겟 해제.</summary>
    public void ClearManualTarget()
    {
        _manualTargetIndex = -1;
    }

    /// <summary>
    /// 적 → 아군 타겟 결정 (Index 0 우선).
    /// </summary>
    public HeroState GetHeroTarget(HeroParty party)
    {
        return party.GetFrontHero();
    }

    // ── GDD v1.0 통합 Resolve ────────────────────────────────────────────────

    /// <summary>
    /// 8종 TargetStrategy를 기반으로 적 후보에서 타겟 목록을 결정한다.
    /// </summary>
    public List<EnemyState> Resolve(List<EnemyState> candidates, TargetStrategy strategy,
                                    int maxCount, int fixedIndex = -1)
    {
        if (candidates == null || candidates.Count == 0)
            return new List<EnemyState>();

        switch (strategy)
        {
            case TargetStrategy.Front:
                return candidates.OrderBy(e => e.WaveIndex)
                                 .Take(maxCount > 0 ? maxCount : candidates.Count)
                                 .ToList();

            case TargetStrategy.Back:
                return candidates.OrderByDescending(e => e.WaveIndex)
                                 .Take(maxCount > 0 ? maxCount : candidates.Count)
                                 .ToList();

            case TargetStrategy.LowestHP:
                return candidates.OrderBy(e => (float)e.CurrentHP / e.MaxHP)
                                 .Take(maxCount > 0 ? maxCount : candidates.Count)
                                 .ToList();

            case TargetStrategy.HighestAtk:
                return candidates.OrderByDescending(e => e.Attack)
                                 .Take(maxCount > 0 ? maxCount : candidates.Count)
                                 .ToList();

            case TargetStrategy.All:
                return new List<EnemyState>(candidates);

            case TargetStrategy.FixedIndex:
            {
                var target = candidates.FirstOrDefault(e => e.WaveIndex == fixedIndex);
                return target != null ? new List<EnemyState> { target } : new List<EnemyState>();
            }

            case TargetStrategy.Random:
            {
                int count = maxCount > 0 ? Mathf.Min(maxCount, candidates.Count) : 1;
                var shuffled = candidates.OrderBy(_ => Random.value).Take(count).ToList();
                return shuffled;
            }

            // Self은 적 대상에 적용되지 않음 — 빈 리스트 반환
            case TargetStrategy.Self:
            default:
                return new List<EnemyState>();
        }
    }

    /// <summary>
    /// 8종 TargetStrategy를 기반으로 아군 후보에서 타겟 목록을 결정한다.
    /// Self 전략의 경우 self 파라미터가 필요하다.
    /// </summary>
    public List<HeroState> Resolve(List<HeroState> candidates, TargetStrategy strategy,
                                   int maxCount, HeroState self = null, int fixedIndex = -1)
    {
        if (candidates == null || candidates.Count == 0)
            return new List<HeroState>();

        switch (strategy)
        {
            case TargetStrategy.Front:
                return candidates.OrderBy(h => h.PartyIndex)
                                 .Take(maxCount > 0 ? maxCount : candidates.Count)
                                 .ToList();

            case TargetStrategy.Back:
                return candidates.OrderByDescending(h => h.PartyIndex)
                                 .Take(maxCount > 0 ? maxCount : candidates.Count)
                                 .ToList();

            case TargetStrategy.LowestHP:
                return candidates.OrderBy(h => (float)h.CurrentHP / h.MaxHP)
                                 .Take(maxCount > 0 ? maxCount : candidates.Count)
                                 .ToList();

            case TargetStrategy.HighestAtk:
                return candidates.OrderByDescending(h => h.Attack)
                                 .Take(maxCount > 0 ? maxCount : candidates.Count)
                                 .ToList();

            case TargetStrategy.Self:
                return self != null ? new List<HeroState> { self } : new List<HeroState>();

            case TargetStrategy.FixedIndex:
            {
                var target = candidates.FirstOrDefault(h => h.PartyIndex == fixedIndex);
                return target != null ? new List<HeroState> { target } : new List<HeroState>();
            }

            case TargetStrategy.All:
                return new List<HeroState>(candidates);

            case TargetStrategy.Random:
            {
                int count = maxCount > 0 ? Mathf.Min(maxCount, candidates.Count) : 1;
                return candidates.OrderBy(_ => Random.value).Take(count).ToList();
            }

            default:
                return new List<HeroState>();
        }
    }
}
