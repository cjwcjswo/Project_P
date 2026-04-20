using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class StatusEffectData
{
    public string EffectType;
    public float Value;
    public float Duration;
    public float Probability;
}

[Serializable]
public class TargetData
{
    public string Team;
    public string Strategy;
    public int MaxCount;
    public int TargetIndex;
}

[Serializable]
public class ActionData
{
    public string ActionType;
    public TargetData Target;
    public float BaseMultiplier;
    public List<StatusEffectData> StatusEffects;
}

[Serializable]
public class SkillData
{
    public int Id;
    public string DisplayName;
    public string EffectPrefabPath;
    public List<ActionData> Actions;
}

[Serializable]
public class SkillDataList
{
    public List<SkillData> Items;
}

public class SkillDataRepository
{
    private readonly Dictionary<int, SkillData> _cache = new();

    public SkillDataRepository()
    {
        Load();
    }

    private void Load()
    {
        var asset = Resources.Load<TextAsset>("SkillData");
        if (asset == null)
        {
            Debug.LogError("[SkillDataRepository] Resources/SkillData.json not found.");
            return;
        }

        var wrapped = "{\"Items\":" + asset.text + "}";
        var list = JsonUtility.FromJson<SkillDataList>(wrapped);

        foreach (var data in list.Items)
            _cache[data.Id] = data;

        Debug.Log($"[SkillDataRepository] Loaded {_cache.Count} skills.");
    }

    public SkillData GetById(int id)
    {
        if (_cache.TryGetValue(id, out var data)) return data;
        Debug.LogWarning($"[SkillDataRepository] Skill id={id} not found.");
        return null;
    }

    public IReadOnlyList<SkillData> GetAll()
    {
        return new List<SkillData>(_cache.Values);
    }
}
