using UnityEngine;

/// <summary>
/// BlockType → 블록 색상(Color) 매핑 유틸리티.
/// HeroEntityView/HeroHUDView의 외곽선/테두리 색상에 사용.
/// </summary>
public static class BlockTypeColors
{
    private static readonly Color Red    = HexToColor("#FC8181");
    private static readonly Color Yellow = HexToColor("#F6E05E");
    private static readonly Color Green  = HexToColor("#68D391");
    private static readonly Color Blue   = HexToColor("#63B3ED");
    private static readonly Color Purple = HexToColor("#B794F4");

    /// <summary>BlockType에 대응하는 색상을 반환. 매핑 없으면 Color.white.</summary>
    public static Color Get(BlockType type) => type switch
    {
        BlockType.Red    => Red,
        BlockType.Yellow => Yellow,
        BlockType.Green  => Green,
        BlockType.Blue   => Blue,
        BlockType.Purple => Purple,
        _                => Color.white
    };

    private static Color HexToColor(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color color);
        return color;
    }
}
