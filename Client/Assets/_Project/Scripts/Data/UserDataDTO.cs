using System;
using System.Collections.Generic;

/// <summary>
/// 배치된 히어로의 식별자와 레벨만 보관. 상세 스탯은 HeroDataRepository에서 조회.
/// </summary>
[Serializable]
public class DeployedHeroDTO
{
    /// <summary>HeroData.json의 Id와 동일. 유일 히어로 식별자.</summary>
    public int HeroId;
    public int Level;
}

/// <summary>
/// 서버 및 로컬 폴백(default_user.json)으로부터 수신하는 유저 게임 데이터.
/// </summary>
[Serializable]
public class UserGameDataDTO
{
    public string UserId;
    public List<int> OwnedHeroIds;
    public List<DeployedHeroDTO> DeployedHeroes;
    public int Energy;
    public List<int> ClearedStageIds;
}
