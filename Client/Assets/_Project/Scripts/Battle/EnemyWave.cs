using System;
using System.Collections.Generic;
using System.Linq;

public class EnemyWave
{
    private readonly List<EnemyState> _enemies;

    public IReadOnlyList<EnemyState> Enemies => _enemies;
    public List<EnemyState> AliveEnemies => _enemies.Where(e => !e.IsDead).ToList();
    public bool AllDead => _enemies.All(e => e.IsDead);

    public event Action OnAllEnemiesDead;

    public EnemyWave(List<EnemyState> enemies)
    {
        _enemies = enemies;

        foreach (var enemy in _enemies)
        {
            enemy.OnDeath += CheckAllDead;
        }
    }

    public EnemyState GetEnemy(int waveIndex)
    {
        return _enemies.FirstOrDefault(e => e.WaveIndex == waveIndex && !e.IsDead);
    }

    /// <summary>
    /// 가장 전방(최소 WaveIndex)의 살아있는 적
    /// </summary>
    public EnemyState GetFrontEnemy()
    {
        return _enemies
            .Where(e => !e.IsDead)
            .OrderBy(e => e.WaveIndex)
            .FirstOrDefault();
    }

    private void CheckAllDead()
    {
        if (AllDead)
            OnAllEnemiesDead?.Invoke();
    }
}
