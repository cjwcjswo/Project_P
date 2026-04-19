using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 유저 데이터 조회 서비스.
/// 서버 API 우선 → 실패 시 로컬 JSON 폴백.
/// </summary>
public class UserDataService
{
    private readonly ApiClient _api;
    private const string LOCAL_FALLBACK_PATH = "DefaultUser";

    public UserDataService(ApiClient api)
    {
        _api = api;
    }

    /// <summary>
    /// 유저의 배치 히어로 데이터를 서버에서 조회.
    /// 서버 실패 시 Resources/default_user.json 폴백.
    /// </summary>
    public async UniTask<UserGameDataDTO> FetchUserGameDataAsync(string userId)
    {
        // 서버 시도
        var result = await _api.GetAsync<UserGameDataDTO>($"api/user/{userId}/gamedata");
        if (result != null)
        {
            Debug.Log($"[UserDataService] Server data loaded for user: {userId}");
            return result;
        }

        // 로컬 폴백
        Debug.Log("[UserDataService] Server unavailable, loading local fallback.");
        return LoadLocalFallback();
    }

    private static UserGameDataDTO LoadLocalFallback()
    {
        var textAsset = Resources.Load<TextAsset>(LOCAL_FALLBACK_PATH);
        if (textAsset == null)
        {
            Debug.LogError($"[UserDataService] Local fallback not found: Resources/{LOCAL_FALLBACK_PATH}.json");
            return null;
        }

        return JsonUtility.FromJson<UserGameDataDTO>(textAsset.text);
    }
}
