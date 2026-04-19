using System.Collections.Generic;

/// <summary>
/// StageData + MonsterDataRepository를 받아 전투에 필요한 EnemyWave 배열을 빌드한다.
/// GDD 규칙: MonsterIds 인덱스 0 = 전방(탱커) 위치.
/// </summary>
public class BattleSetupData
{
    public StageData Stage { get; }
    public EnemyWave[] Waves { get; }

    public BattleSetupData(StageData stage, MonsterDataRepository monsterRepo)
    {
        Stage = stage;
        Waves = BuildWaves(stage, monsterRepo);
    }

    private static EnemyWave[] BuildWaves(StageData stage, MonsterDataRepository monsterRepo)
    {
        var result = new EnemyWave[stage.Waves.Count];

        for (int waveIdx = 0; waveIdx < stage.Waves.Count; waveIdx++)
        {
            var waveData = stage.Waves[waveIdx];
            var enemies = new List<EnemyState>();

            for (int posIdx = 0; posIdx < waveData.MonsterIds.Length; posIdx++)
            {
                var monsterId = waveData.MonsterIds[posIdx];
                var data = monsterRepo.GetById(monsterId);
                if (data == null) continue;

                var enemy = new EnemyState(
                    maxHP: data.MaxHP,
                    attack: data.Attack,
                    skillCooldown: data.SkillCooldown,
                    skillCastTime: data.SkillCastTime,
                    skillDamageMultiplier: data.SkillDamageMultiplier,
                    autoAttackInterval: data.AutoAttackInterval,
                    autoAttackRatio: data.AutoAttackRatio,
                    waveIndex: posIdx,         // 인덱스 0 = 전방 탱커
                    monsterDataId: data.Id,
                    displayName: data.DisplayName,
                    prefabPath: data.PrefabPath
                );
                enemies.Add(enemy);
            }

            result[waveIdx] = new EnemyWave(enemies);
        }

        return result;
    }
}
