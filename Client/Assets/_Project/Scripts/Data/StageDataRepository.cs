using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class WaveData
{
    public int[] MonsterIds;
}

[Serializable]
public class ClearRewardsData
{
    public int TotalExp;
    public int TotalGold;
}

[Serializable]
public class StageData
{
    public int StageId;
    public string StageName;
    public int EntryCost;
    public List<WaveData> Waves;
    public ClearRewardsData ClearRewards;
}

[Serializable]
public class StageDataList
{
    public List<StageData> Items;
}

public class StageDataRepository
{
    private readonly Dictionary<int, StageData> _cache = new();

    public StageDataRepository()
    {
        Load();
    }

    private void Load()
    {
        var asset = Resources.Load<TextAsset>("StageData");
        if (asset == null)
        {
            Debug.LogError("[StageDataRepository] Resources/StageData.json not found.");
            return;
        }

        var wrapped = "{\"Items\":" + asset.text + "}";
        var list = JsonUtility.FromJson<StageDataList>(wrapped);

        foreach (var data in list.Items)
            _cache[data.StageId] = data;

        Debug.Log($"[StageDataRepository] Loaded {_cache.Count} stages.");
    }

    public StageData GetById(int stageId)
    {
        if (_cache.TryGetValue(stageId, out var data)) return data;
        Debug.LogWarning($"[StageDataRepository] Stage id={stageId} not found.");
        return null;
    }

    public IReadOnlyList<StageData> GetAll()
    {
        return new List<StageData>(_cache.Values);
    }

    public bool Exists(int stageId) => _cache.ContainsKey(stageId);
}
