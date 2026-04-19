using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// GDD 3.2 상태 이상 종류.
/// </summary>
public enum StatusEffectType
{
    Stun,
    Burn,
    AtkUp,
    DefDown
}

/// <summary>
/// 런타임에서 활성 중인 상태 이상 인스턴스.
/// </summary>
public class StatusEffectInstance
{
    public StatusEffectType Type { get; }
    public float Value { get; }
    public float RemainingDuration { get; set; }

    public StatusEffectInstance(StatusEffectType type, float value, float duration)
    {
        Type = type;
        Value = value;
        RemainingDuration = duration;
    }
}

/// <summary>
/// 상태 이상 적용/Tick/제거 로직을 집중 관리하는 핸들러.
/// EnemyState / HeroState에 composition으로 포함되어 사용.
/// </summary>
public class StatusEffectHandler
{
    private readonly List<StatusEffectInstance> _effects = new();

    public IReadOnlyList<StatusEffectInstance> ActiveEffects => _effects;

    /// <summary>
    /// StatusEffectData의 Probability를 판정하고 성공 시 인스턴스를 추가.
    /// </summary>
    public bool TryApply(StatusEffectData data)
    {
        if (data == null) return false;
        if (Random.value > data.Probability) return false;

        if (!System.Enum.TryParse<StatusEffectType>(data.EffectType, ignoreCase: true, out var type))
        {
            Debug.LogWarning($"[StatusEffectHandler] Unknown EffectType '{data.EffectType}'");
            return false;
        }

        // Stun: 기존 Stun이 있으면 duration 갱신
        if (type == StatusEffectType.Stun)
        {
            var existing = _effects.Find(e => e.Type == StatusEffectType.Stun);
            if (existing != null)
            {
                existing.RemainingDuration = Mathf.Max(existing.RemainingDuration, data.Duration);
                return true;
            }
        }

        _effects.Add(new StatusEffectInstance(type, data.Value, data.Duration));
        return true;
    }

    /// <summary>
    /// 매 Tick 지속 시간 감소. 만료된 효과 제거. Burn 도트 데미지 액션 반환.
    /// </summary>
    public void Tick(float deltaTime, System.Action<StatusEffectInstance> onExpired = null)
    {
        for (int i = _effects.Count - 1; i >= 0; i--)
        {
            var effect = _effects[i];
            effect.RemainingDuration -= deltaTime;
            if (effect.RemainingDuration <= 0f)
            {
                _effects.RemoveAt(i);
                onExpired?.Invoke(effect);
            }
        }
    }

    public bool HasEffect(StatusEffectType type)
    {
        return _effects.Exists(e => e.Type == type);
    }

    public StatusEffectInstance GetEffect(StatusEffectType type)
    {
        return _effects.Find(e => e.Type == type);
    }

    public void Clear()
    {
        _effects.Clear();
    }
}
