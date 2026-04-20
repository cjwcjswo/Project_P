using System.Collections.Generic;

/// <summary>
/// 런타임에 스폰된 HeroEntityView를 partyIndex로 조회하기 위한 경량 레지스트리.
/// PuzzleBoardView 같은 다른 뷰 레이어에서 오브/연출 타겟을 찾는 용도로 사용한다.
/// </summary>
public static class HeroEntityViewRegistry
{
    private static readonly Dictionary<int, HeroEntityView> ViewsByPartyIndex = new();

    public static void Register(int partyIndex, HeroEntityView view)
    {
        if (view == null) return;
        ViewsByPartyIndex[partyIndex] = view;
    }

    public static void Unregister(int partyIndex, HeroEntityView view)
    {
        if (view == null) return;
        if (ViewsByPartyIndex.TryGetValue(partyIndex, out var current) && current == view)
            ViewsByPartyIndex.Remove(partyIndex);
    }

    public static HeroEntityView Get(int partyIndex)
    {
        ViewsByPartyIndex.TryGetValue(partyIndex, out var v);
        return v;
    }

    public static void Clear()
    {
        ViewsByPartyIndex.Clear();
    }
}

