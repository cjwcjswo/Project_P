using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class MonsterData
{
    public int Id;
    public string DisplayName;
    public string PrefabPath;
    public int MaxHP;
    public int Attack;
    public float SkillCooldown;
    public float SkillCastTime;
    public float SkillDamageMultiplier;
    public float AutoAttackInterval;
    public float AutoAttackRatio;
}

[Serializable]
public class MonsterDataList
{
    public List<MonsterData> Items;
}

public class MonsterDataRepository
{
    private readonly Dictionary<int, MonsterData> _cache = new();

    public MonsterDataRepository()
    {
        Load();
    }

    private void Load()
    {
        var asset = Resources.Load<TextAsset>("MonsterData");
        if (asset == null)
        {
            Debug.LogError("[MonsterDataRepository] Resources/MonsterData.json not found.");
            return;
        }

        // JsonUtility doesn't support top-level arrays — wrap manually
        var wrapped = "{\"Items\":" + asset.text + "}";
        var list = JsonUtility.FromJson<MonsterDataList>(wrapped);

        foreach (var data in list.Items)
            _cache[data.Id] = data;

        Debug.Log($"[MonsterDataRepository] Loaded {_cache.Count} monsters.");
    }

    public MonsterData GetById(int id)
    {
        if (_cache.TryGetValue(id, out var data)) return data;
        Debug.LogWarning($"[MonsterDataRepository] Monster id={id} not found.");
        return null;
    }

    public IReadOnlyList<MonsterData> GetAll()
    {
        return new List<MonsterData>(_cache.Values);
    }
}
