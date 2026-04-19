using System;
using System.Collections.Generic;

/// <summary>
/// 히어로별 개별 궁극기 게이지 관리.
/// GDD v2.0: 자기 색상 블록 매칭 시에만 해당 히어로 게이지 충전.
/// </summary>
public class UltimateGaugeManager
{
    public const int MAX_GAUGE = 10;

    private readonly Dictionary<int, int> _gauges = new(); // partyIndex → gauge
    private readonly Dictionary<int, int> _grades = new(); // partyIndex → grade

    public event Action<int, int, int> OnGaugeChanged;        // (heroIndex, current, max)
    public event Action<int> OnUltimateReady;                  // (heroIndex)
    public event Action<int, int, List<(int, int)>> OnUltimateActivated; // (heroIndex, damage, destroyedPositions)

    public void Initialize(HeroParty party)
    {
        _gauges.Clear();
        _grades.Clear();
        foreach (var hero in party.Heroes)
        {
            _gauges[hero.PartyIndex] = 0;
            _grades[hero.PartyIndex] = hero.Grade;
        }
    }

    public int GetGauge(int heroIndex) =>
        _gauges.TryGetValue(heroIndex, out int val) ? val : 0;

    public bool CanActivate(int heroIndex) =>
        GetGauge(heroIndex) >= MAX_GAUGE;

    /// <summary>
    /// 캐스케이드 결과로부터 색상별 게이지 충전.
    /// 자기 색상 블록만 해당 히어로 게이지에 반영.
    /// </summary>
    public void ChargeFromCascade(List<ColorMatchData> colorBreakdown)
    {
        foreach (var data in colorBreakdown)
        {
            int heroIndex = HeroColorMap.GetHeroIndex(data.Color);
            if (heroIndex < 0 || !_gauges.ContainsKey(heroIndex)) continue;

            // 3성 미만 히어로는 궁극기 게이지 충전 불가
            if (_grades.TryGetValue(heroIndex, out int grade) && grade < 3) continue;

            int before = _gauges[heroIndex];
            _gauges[heroIndex] = Math.Min(MAX_GAUGE, before + data.BlockCount);

            OnGaugeChanged?.Invoke(heroIndex, _gauges[heroIndex], MAX_GAUGE);

            if (before < MAX_GAUGE && _gauges[heroIndex] >= MAX_GAUGE)
                OnUltimateReady?.Invoke(heroIndex);
        }
    }

    /// <summary>
    /// 특정 히어로의 궁극기 발동. 게이지 소모. 보드 블록 파괴는 하지 않으며 SkillSystem이 전투 효과만 처리한다.
    /// </summary>
    public UltimateResult Activate(int heroIndex, int heroAttack)
    {
        if (!CanActivate(heroIndex))
            return new UltimateResult { IsActivated = false, Damage = 0, DestroyedPositions = null };

        _gauges[heroIndex] = 0;
        OnGaugeChanged?.Invoke(heroIndex, 0, MAX_GAUGE);

        var empty = new List<(int, int)>();
        OnUltimateActivated?.Invoke(heroIndex, 0, empty);

        return new UltimateResult
        {
            IsActivated        = true,
            Damage             = 0,
            DestroyedPositions = empty
        };
    }
}

public struct UltimateResult
{
    public bool IsActivated;
    public int Damage;
    public List<(int col, int row)> DestroyedPositions;
}
