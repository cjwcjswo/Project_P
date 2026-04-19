using System.Collections.Generic;
using UnityEngine;

public class RewardCalculationResult
{
    /// <summary>히어로 PartyIndex → 획득 EXP</summary>
    public Dictionary<int, int> HeroExpMap { get; }
    public int GoldGained { get; }

    public RewardCalculationResult(Dictionary<int, int> heroExpMap, int goldGained)
    {
        HeroExpMap = heroExpMap;
        GoldGained = goldGained;
    }
}

public static class RewardCalculator
{
    /// <summary>
    /// GDD 3.2 보상 규칙 적용:
    /// - EXP: TotalExp / 출전 히어로 수 (소수점 내림), 출전한 히어로 전원에게 동일 지급
    /// - Gold: ClearRewards.TotalGold 즉시 획득
    /// </summary>
    public static RewardCalculationResult Calculate(BattleResult result, StageData stageData)
    {
        var expMap = new Dictionary<int, int>();
        int goldGained = 0;

        if (!result.IsCleared)
            return new RewardCalculationResult(expMap, goldGained);

        int deployedCount = result.DeployedHeroCount;
        if (deployedCount <= 0)
            return new RewardCalculationResult(expMap, goldGained);

        int expPerHero = Mathf.FloorToInt((float)stageData.ClearRewards.TotalExp / deployedCount);
        goldGained = stageData.ClearRewards.TotalGold;

        // 출전한 모든 히어로(PartyIndex 0 ~ deployedCount-1)에게 EXP 분배
        for (int i = 0; i < deployedCount; i++)
            expMap[i] = expPerHero;

        return new RewardCalculationResult(expMap, goldGained);
    }
}
