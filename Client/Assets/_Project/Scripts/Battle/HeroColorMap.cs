using System;
using System.Collections.Generic;

/// <summary>
/// 히어로 배치 인덱스(0~4)와 BlockType 간 양방향 매핑.
/// GDD v2.0: Index 0=Red, 1=Yellow, 2=Green, 3=Blue, 4=Purple
/// </summary>
public static class HeroColorMap
{
    private static readonly BlockType[] IndexToColor = new[]
    {
        BlockType.Red,     // Index 0
        BlockType.Yellow,  // Index 1
        BlockType.Green,   // Index 2
        BlockType.Blue,    // Index 3
        BlockType.Purple   // Index 4
    };

    private static readonly Dictionary<BlockType, int> ColorToIndex = new()
    {
        { BlockType.Red,    0 },
        { BlockType.Yellow, 1 },
        { BlockType.Green,  2 },
        { BlockType.Blue,   3 },
        { BlockType.Purple, 4 }
    };

    /// <summary>
    /// 배치 인덱스 → 블록 색상
    /// </summary>
    public static BlockType GetBlockType(int partyIndex)
    {
        if (partyIndex < 0 || partyIndex >= IndexToColor.Length)
            throw new ArgumentOutOfRangeException(nameof(partyIndex),
                $"Party index must be 0~{IndexToColor.Length - 1}, got {partyIndex}");
        return IndexToColor[partyIndex];
    }

    /// <summary>
    /// 블록 색상 → 배치 인덱스. 매핑 없으면 -1 반환.
    /// </summary>
    public static int GetHeroIndex(BlockType type)
    {
        return ColorToIndex.TryGetValue(type, out int index) ? index : -1;
    }

    /// <summary>
    /// 활성 히어로 수에 따른 드롭 가능 색상 목록 반환.
    /// </summary>
    public static List<BlockType> GetActiveColors(int partySize)
    {
        int count = Math.Clamp(partySize, Constants.MIN_PARTY_SIZE, Constants.MAX_PARTY_SIZE);
        var colors = new List<BlockType>(count);
        for (int i = 0; i < count; i++)
            colors.Add(IndexToColor[i]);
        return colors;
    }
}
