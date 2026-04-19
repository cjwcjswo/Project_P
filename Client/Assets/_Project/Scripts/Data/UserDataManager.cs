using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 유저 데이터 전담 매니저. 에너지·히어로·클리어 스테이지 이력을 보유하며
/// 게임 시작 시 한 번 초기화되어 씬 전환에 걸쳐 유지된다.
/// ServiceLocator에 등록되어 어디서든 접근 가능.
/// </summary>
public class UserDataManager
{
    private UserGameDataDTO _data;

    public bool IsInitialized => _data != null;

    public int Energy => _data?.Energy ?? 0;

    public IReadOnlyList<DeployedHeroDTO> DeployedHeroes =>
        _data?.DeployedHeroes ?? new List<DeployedHeroDTO>();

    public IReadOnlyList<int> ClearedStageIds =>
        _data?.ClearedStageIds ?? new List<int>();

    /// <summary>
    /// 서버(또는 로컬 폴백)에서 유저 데이터를 로드하고 내부 상태를 초기화한다.
    /// </summary>
    public async UniTask InitializeAsync(ApiClient apiClient, string userId)
    {
        var service = new UserDataService(apiClient);
        _data = await service.FetchUserGameDataAsync(userId);

        if (_data == null)
        {
            Debug.LogError("[UserDataManager] Failed to load user data.");
            return;
        }

        if (_data.Energy <= 0)
            _data.Energy = 20;

        if (_data.ClearedStageIds == null)
            _data.ClearedStageIds = new List<int>();

        Debug.Log($"[UserDataManager] Initialized. Energy={_data.Energy}, " +
                  $"Heroes={_data.DeployedHeroes?.Count ?? 0}, " +
                  $"ClearedStages={_data.ClearedStageIds.Count}");
    }

    public bool TryConsumeEnergy(int cost)
    {
        if (_data == null || _data.Energy < cost)
            return false;

        _data.Energy -= cost;
        Debug.Log($"[UserDataManager] Energy consumed: -{cost}, Remaining: {_data.Energy}");
        return true;
    }

    public void AddClearedStage(int stageId)
    {
        if (_data == null) return;
        if (_data.ClearedStageIds == null)
            _data.ClearedStageIds = new List<int>();

        if (!_data.ClearedStageIds.Contains(stageId))
        {
            _data.ClearedStageIds.Add(stageId);
            Debug.Log($"[UserDataManager] Stage {stageId} cleared and recorded.");
        }
    }

    public bool HasCleared(int stageId) =>
        _data?.ClearedStageIds?.Contains(stageId) ?? false;
}
