using System.Collections.Generic;

public class BattleResult
{
    public int StageId { get; }
    public int DeployedHeroCount { get; }
    public List<int> SurvivedHeroIds { get; }
    public bool IsCleared { get; }

    public BattleResult(int stageId, int deployedHeroCount, List<int> survivedHeroIds, bool isCleared)
    {
        StageId = stageId;
        DeployedHeroCount = deployedHeroCount;
        SurvivedHeroIds = survivedHeroIds ?? new List<int>();
        IsCleared = isCleared;
    }
}
