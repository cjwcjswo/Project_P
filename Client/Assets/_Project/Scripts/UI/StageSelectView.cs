using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Cysharp.Threading.Tasks;

/// <summary>
/// 스테이지 선택 화면. StageSelectScene의 루트 MonoBehaviour.
/// StageDataRepository에서 전체 스테이지 목록을 로드하고, UserDataManager의 클리어
/// 이력을 기반으로 각 셀을 클리어(재도전 가능) / 도전 가능 / 잠금 상태로 표시한다.
///
/// GameManager의 InitializeCoreServicesAsync()가 비동기이므로 Awake에서는
/// ServiceLocator에 레포지토리가 아직 없을 수 있다. Start에서 비동기 대기 후
/// 목록을 채운다 (BattleSceneManager와 동일한 패턴).
/// </summary>
public class StageSelectView : MonoBehaviour
{
    [SerializeField] private Transform _contentParent;
    [SerializeField] private StageCellView _cellPrefab;

    private void Start()
    {
        PopulateStageListWhenReadyAsync().Forget();
    }

    private async UniTaskVoid PopulateStageListWhenReadyAsync()
    {
        await WaitForService<StageDataRepository>();
        PopulateStageList();
    }

    /// <summary>
    /// GameManager.InitializeCoreServicesAsync() 완료까지 프레임 단위 대기.
    /// </summary>
    private static async UniTask<T> WaitForService<T>() where T : class
    {
        T svc;
        while ((svc = ServiceLocator.Get<T>()) == null)
            await UniTask.Yield();
        return svc;
    }

    private void PopulateStageList()
    {
        var repo = ServiceLocator.Get<StageDataRepository>();
        if (repo == null)
        {
            Debug.LogError("[StageSelectView] StageDataRepository not found in ServiceLocator.");
            return;
        }

        var udm = ServiceLocator.Get<UserDataManager>();
        IReadOnlyList<int> cleared = udm?.ClearedStageIds ?? new List<int>();

        // 기존 셀 제거 (씬 재진입 대비)
        foreach (Transform child in _contentParent)
            Destroy(child.gameObject);

        var stages = repo.GetAll().OrderBy(s => s.StageId).ToList();
        for (int i = 0; i < stages.Count; i++)
        {
            var stageData = stages[i];
            bool isCleared = cleared.Contains(stageData.StageId);

            // 첫 스테이지이거나 직전 스테이지를 클리어했으면 도전 가능
            bool isAvailable = i == 0 || cleared.Contains(stages[i - 1].StageId);

            var cell = Instantiate(_cellPrefab, _contentParent);
            cell.Bind(stageData, isCleared, isAvailable);
        }
    }
}
