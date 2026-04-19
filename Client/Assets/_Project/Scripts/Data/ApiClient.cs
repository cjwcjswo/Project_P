using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// UnityWebRequest 기반 async HTTP 클라이언트.
/// 서버 API와의 통신을 담당하며, JSON 직렬화/역직렬화를 처리한다.
/// </summary>
public class ApiClient
{
    private readonly string _baseUrl;
    private readonly int _timeoutSeconds;

    public ApiClient(string baseUrl, int timeoutSeconds = 10)
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _timeoutSeconds = timeoutSeconds;
    }

    /// <summary>
    /// GET 요청을 보내고 JSON 응답을 T로 역직렬화하여 반환.
    /// 실패 시 null 반환 (호출부에서 폴백 처리).
    /// </summary>
    public async UniTask<T> GetAsync<T>(string endpoint) where T : class
    {
        string url = $"{_baseUrl}/{endpoint.TrimStart('/')}";

        using var request = UnityWebRequest.Get(url);
        request.timeout = _timeoutSeconds;
        request.SetRequestHeader("Content-Type", "application/json");

        try
        {
            await request.SendWebRequest().ToUniTask();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[ApiClient] GET {url} failed: {request.error}");
                return null;
            }

            string json = request.downloadHandler.text;
            return JsonUtility.FromJson<T>(json);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[ApiClient] GET {url} exception: {ex.Message}");
            return null;
        }
    }
}
