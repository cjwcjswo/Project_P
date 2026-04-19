using System.Collections.Generic;

public class CascadeResult
{
    public static readonly CascadeResult Invalid = new() { IsValid = false };

    public bool IsValid;
    public int Combo;
    public List<MatchResult> AllMatches;
}
