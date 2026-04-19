using System;
using System.Collections.Generic;
using System.Linq;

public class HeroParty
{
    private readonly List<HeroState> _heroes;

    public IReadOnlyList<HeroState> Heroes => _heroes;
    public int Count => _heroes.Count;

    /// <summary>
    /// 히어로 사망 시 발행 (partyIndex)
    /// </summary>
    public event Action<int> OnHeroDied;

    /// <summary>
    /// 전원 사망 시 발행
    /// </summary>
    public event Action OnAllDead;

    public HeroParty(List<HeroState> heroes)
    {
        if (heroes.Count < Constants.MIN_PARTY_SIZE || heroes.Count > Constants.MAX_PARTY_SIZE)
            throw new InvalidDeckException(
                $"Party size must be {Constants.MIN_PARTY_SIZE}~{Constants.MAX_PARTY_SIZE}, got {heroes.Count}");

        _heroes = heroes;

        foreach (var hero in _heroes)
        {
            int idx = hero.PartyIndex;
            hero.OnDeath += () => HandleHeroDeath(idx);
        }
    }

    /// <summary>
    /// 색상으로 히어로 조회. 사망했거나 없으면 null.
    /// </summary>
    public HeroState GetHeroByColor(BlockType color)
    {
        int index = HeroColorMap.GetHeroIndex(color);
        if (index < 0 || index >= _heroes.Count) return null;
        var hero = _heroes[index];
        return hero.IsDead ? null : hero;
    }

    /// <summary>
    /// 파티 인덱스로 히어로 조회.
    /// </summary>
    public HeroState GetHeroByIndex(int partyIndex)
    {
        if (partyIndex < 0 || partyIndex >= _heroes.Count) return null;
        return _heroes[partyIndex];
    }

    /// <summary>
    /// 생존 히어로 목록.
    /// </summary>
    public List<HeroState> GetAliveHeroes()
    {
        return _heroes.Where(h => !h.IsDead).ToList();
    }

    /// <summary>
    /// 살아있는 최전방 히어로 (Index 0 우선). 전원 사망이면 null.
    /// </summary>
    public HeroState GetFrontHero()
    {
        return _heroes.FirstOrDefault(h => !h.IsDead);
    }

    /// <summary>
    /// 현재 활성(생존) 히어로의 색상 목록.
    /// 보드 리필 시 이 색상만 드롭.
    /// </summary>
    public List<BlockType> GetActiveColors()
    {
        return _heroes
            .Where(h => !h.IsDead)
            .Select(h => h.MappedColor)
            .ToList();
    }

    public bool AllDead => _heroes.All(h => h.IsDead);

    private void HandleHeroDeath(int partyIndex)
    {
        OnHeroDied?.Invoke(partyIndex);

        if (AllDead)
            OnAllDead?.Invoke();
    }
}

public class InvalidDeckException : Exception
{
    public InvalidDeckException(string message) : base(message) { }
}
