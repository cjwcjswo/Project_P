using System.Collections.Generic;
using System.Linq;

public class TargetingSystem
{
    private int _manualTargetIndex = -1; // 유저가 탭으로 지정한 적 인덱스

    /// <summary>
    /// 아군 → 적 타겟 결정.
    /// 수동 지정이 있으면 해당 적, 없으면 전방 우선.
    /// </summary>
    public EnemyState GetPriorityTarget(List<EnemyState> aliveEnemies)
    {
        if (aliveEnemies == null || aliveEnemies.Count == 0) return null;

        if (_manualTargetIndex >= 0)
        {
            var manual = aliveEnemies.FirstOrDefault(e => e.WaveIndex == _manualTargetIndex);
            if (manual != null) return manual;
            // 수동 타겟이 사망했으면 자동으로 전방 우선
            _manualTargetIndex = -1;
        }

        return aliveEnemies.OrderBy(e => e.WaveIndex).First();
    }

    /// <summary>
    /// 유저 탭으로 일점사 타겟 지정
    /// </summary>
    public void SetManualTarget(int enemyWaveIndex)
    {
        _manualTargetIndex = enemyWaveIndex;
    }

    /// <summary>
    /// 수동 타겟 해제
    /// </summary>
    public void ClearManualTarget()
    {
        _manualTargetIndex = -1;
    }

    /// <summary>
    /// 적 → 아군 타겟 결정.
    /// Index 0 우선, 사망 시 다음 인덱스.
    /// </summary>
    public HeroState GetHeroTarget(HeroParty party)
    {
        return party.GetFrontHero();
    }
}
