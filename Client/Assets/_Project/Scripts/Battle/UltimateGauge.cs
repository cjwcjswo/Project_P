using System;
using System.Collections.Generic;

/// <summary>
/// 히어로별 개별 궁극기 게이지 관리.
/// GDD v2.0: 자기 색상 블록 매칭 시에만 해당 히어로 게이지 충전.
/// </summary>
public class UltimateGaugeManager
{
    public const int MAX_GAUGE = 100;

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
    /// 특정 히어로의 궁극기 발동. 게이지 소모.
    /// </summary>
    public UltimateResult Activate(int heroIndex, int heroAttack, Board board)
    {
        if (!CanActivate(heroIndex))
            return new UltimateResult { IsActivated = false, Damage = 0, DestroyedPositions = null };

        _gauges[heroIndex] = 0;
        OnGaugeChanged?.Invoke(heroIndex, 0, MAX_GAUGE);

        var positions = GetRandomBlockPositions(board, 10);
        OnUltimateActivated?.Invoke(heroIndex, 0, positions);

        return new UltimateResult
        {
            IsActivated        = true,
            Damage             = 0,   // 실제 데미지는 SkillSystem이 처리
            DestroyedPositions = positions
        };
    }

    private List<(int col, int row)> GetRandomBlockPositions(Board board, int count)
    {
        var candidates = new List<(int, int)>();
        for (int col = 0; col < board.Width; col++)
            for (int row = 0; row < board.Height; row++)
                if (board.GetBlock(col, row) != BlockType.None)
                    candidates.Add((col, row));

        var rand = new Random();
        var result = new List<(int, int)>();
        int pick = Math.Min(count, candidates.Count);
        for (int i = 0; i < pick; i++)
        {
            int idx = rand.Next(candidates.Count);
            result.Add(candidates[idx]);
            candidates.RemoveAt(idx);
        }
        return result;
    }
}

public struct UltimateResult
{
    public bool IsActivated;
    public int Damage;
    public List<(int col, int row)> DestroyedPositions;
}
