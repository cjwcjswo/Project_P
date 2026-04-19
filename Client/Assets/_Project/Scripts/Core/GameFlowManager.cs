using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 씬 전환 상태 머신. ServiceLocator를 통해 전역 접근.
/// MainMenu → StageSelect → BattleScene → (RewardOverlay) → StageSelect
/// </summary>
public class GameFlowManager
{
    public int CurrentStageId { get; private set; }

    // 씬 이름 상수
    public const string SceneMainMenu    = "MainMenu";
    public const string SceneStageSelect = "StageSelect";
    public const string SceneBattle      = "Battle";

    public void LoadMainMenu()
    {
        SceneManager.LoadScene(SceneMainMenu);
    }

    public void LoadStageSelect()
    {
        SceneManager.LoadScene(SceneStageSelect);
    }

    /// <summary>
    /// 스테이지 선택 후 전투 씬으로 진입. BattleScene Awake에서
    /// CurrentStageId를 읽어 BattleSetupData를 빌드한다.
    /// </summary>
    public void LoadBattleScene(int stageId)
    {
        CurrentStageId = stageId;
        SceneManager.LoadScene(SceneBattle);
    }

    /// <summary>
    /// 전투 결과 화면 → BattleScene이 단일 씬이면 BattleRewardView 오버레이를 활성화.
    /// 씬을 분리할 경우 SceneManager.LoadScene(SceneReward) 로 교체.
    /// </summary>
    public void ShowRewardScreen()
    {
        // BattleRewardView는 BattleScene 내 오버레이이므로 씬 전환 없음.
        // BattleRewardView가 EventBus를 통해 BattleCompleteEvent를 받아 스스로 활성화.
        Debug.Log("[GameFlowManager] ShowRewardScreen — BattleRewardView handles activation via EventBus.");
    }
}
