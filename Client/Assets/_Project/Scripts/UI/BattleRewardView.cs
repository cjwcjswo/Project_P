using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 전투 완료 시 오버레이로 활성화되는 보상 UI.
/// BattleCompleteEvent를 EventBus로 수신하여 결과를 렌더링한다.
/// BattleScene 내 높은 Sort Order의 Canvas에 배치하고 초기 비활성화.
/// </summary>
public class BattleRewardView : MonoBehaviour
{
    [Header("보상 정보")]
    [SerializeField] private TextMeshProUGUI _goldText;
    [SerializeField] private TextMeshProUGUI _resultTitleText;

    [Header("히어로 EXP 목록")]
    [SerializeField] private Transform _heroExpListParent;
    [SerializeField] private HeroExpRowView _heroExpRowPrefab;

    [Header("버튼")]
    [SerializeField] private Button _nextStageButton;
    [SerializeField] private Button _retryButton;
    [SerializeField] private Button _stageSelectButton;

    private BattleResult _lastResult;

    private void OnEnable()
    {
        EventBus.Subscribe<BattleCompleteEvent>(OnBattleComplete);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<BattleCompleteEvent>(OnBattleComplete);
    }

    private void Start()
    {
        gameObject.SetActive(false);

        _nextStageButton.onClick.AddListener(OnNextStageClicked);
        _retryButton.onClick.AddListener(OnRetryClicked);
        _stageSelectButton.onClick.AddListener(OnStageSelectClicked);
    }

    private void OnBattleComplete(BattleCompleteEvent evt)
    {
        _lastResult = evt.Result;

        if (evt.Result.IsCleared)
            ServiceLocator.Get<UserDataManager>()?.AddClearedStage(evt.Result.StageId);

        Render(evt.Result);
        gameObject.SetActive(true);
    }

    private void Render(BattleResult result)
    {
        _resultTitleText.text = result.IsCleared ? "전투 승리!" : "전투 패배";

        var stageRepo  = ServiceLocator.Get<StageDataRepository>();
        var stageData  = stageRepo?.GetById(result.StageId);
        var calcResult = stageData != null
            ? RewardCalculator.Calculate(result, stageData)
            : null;

        _goldText.text = calcResult != null ? $"Gold +{calcResult.GoldGained}" : "Gold +0";

        // 히어로 EXP 행 렌더링
        foreach (Transform child in _heroExpListParent)
            Destroy(child.gameObject);

        if (calcResult != null)
        {
            foreach (var kv in calcResult.HeroExpMap)
            {
                var row = Instantiate(_heroExpRowPrefab, _heroExpListParent);
                row.Bind(heroPartyIndex: kv.Key, expGained: kv.Value);
            }
        }

        // 다음 스테이지 버튼: 다음 스테이지가 존재해야 활성화
        bool hasNextStage = stageRepo != null && stageRepo.Exists(result.StageId + 1);
        _nextStageButton.interactable = hasNextStage && result.IsCleared;
    }

    private void OnNextStageClicked()
    {
        if (_lastResult == null) return;
        int nextId = _lastResult.StageId + 1;

        var gm = GameManager.Instance;
        var stageRepo = ServiceLocator.Get<StageDataRepository>();
        if (gm == null || stageRepo == null) return;

        var nextStage = stageRepo.GetById(nextId);
        if (nextStage == null) return;

        if (!gm.TryConsumeEnergy(nextStage.EntryCost))
        {
            Debug.Log("[BattleRewardView] 에너지가 부족합니다.");
            return;
        }

        var flowManager = ServiceLocator.Get<GameFlowManager>();
        flowManager?.LoadBattleScene(nextId);
    }

    private void OnRetryClicked()
    {
        if (_lastResult == null) return;

        var gm = GameManager.Instance;
        var stageRepo = ServiceLocator.Get<StageDataRepository>();
        if (gm == null || stageRepo == null) return;

        var stageData = stageRepo.GetById(_lastResult.StageId);
        if (stageData == null) return;

        if (!gm.TryConsumeEnergy(stageData.EntryCost))
        {
            Debug.Log("[BattleRewardView] 에너지가 부족합니다.");
            return;
        }

        var flowManager = ServiceLocator.Get<GameFlowManager>();
        flowManager?.LoadBattleScene(_lastResult.StageId);
    }

    private void OnStageSelectClicked()
    {
        var flowManager = ServiceLocator.Get<GameFlowManager>();
        flowManager?.LoadStageSelect();
    }
}
