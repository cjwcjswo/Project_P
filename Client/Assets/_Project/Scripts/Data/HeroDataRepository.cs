using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class HeroStatData
{
    public int MaxHP;
    public int Attack;
    public int Defense;
}

[Serializable]
public class HeroData
{
    public int Id;
    public string DisplayName;
    public string PrefabPath;
    public string Class;
    public string IllustrationPath;
    public int Grade;
    public int MatchSkillId;
    public int UniqueSkillId;
    public int UltimateSkillId;
    public HeroStatData BaseStats;
    public HeroStatData GrowthStats;
    public float AutoAttackInterval;
    public float AutoAttackRatio;
}

[Serializable]
public class HeroDataList
{
    public List<HeroData> Items;
}

public class HeroDataRepository
{
    private readonly Dictionary<int, HeroData> _cache = new();

    public HeroDataRepository()
    {
        Load();
    }

    private void Load()
    {
        var asset = Resources.Load<TextAsset>("HeroData");
        if (asset == null)
        {
            Debug.LogError("[HeroDataRepository] Resources/HeroData.json not found.");
            return;
        }

        var wrapped = "{\"Items\":" + asset.text + "}";
        var list = JsonUtility.FromJson<HeroDataList>(wrapped);

        foreach (var data in list.Items)
            _cache[data.Id] = data;

        Debug.Log($"[HeroDataRepository] Loaded {_cache.Count} heroes.");
    }

    public HeroData GetById(int id)
    {
        if (_cache.TryGetValue(id, out var data)) return data;
        Debug.LogWarning($"[HeroDataRepository] Hero id={id} not found.");
        return null;
    }

    public IReadOnlyList<HeroData> GetAll()
    {
        return new List<HeroData>(_cache.Values);
    }

    /// <summary>레벨 기반 스탯 계산: BaseStat + (level - 1) * GrowthStat</summary>
    public static int CalcStat(int baseStat, int growthStat, int level)
    {
        return baseStat + (level - 1) * growthStat;
    }
}
