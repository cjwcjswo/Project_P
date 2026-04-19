using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 스테이지 목록의 개별 셀 뷰. StageCellView 프리팹에 부착.
/// StageSelectView에서 동적으로 생성하며 Bind()로 데이터와 상태를 주입한다.
///
/// 상태 3종:
///  - isCleared + isAvailable  : 클리어 뱃지 표시, 재도전 가능
///  - !isCleared + isAvailable : 일반 도전 가능
///  - !isAvailable             : 잠금 오버레이 표시, 버튼 비활성화
/// </summary>
public class StageCellView : MonoBehaviour
{
    [Header("텍스트")]
    [SerializeField] private TextMeshProUGUI _stageNameText;
    [SerializeField] private TextMeshProUGUI _entryCostText;
    [SerializeField] private TextMeshProUGUI _waveCountText;
    [SerializeField] private TextMeshProUGUI _rewardText;

    [Header("버튼")]
    [SerializeField] private Button _selectButton;

    [Header("상태 UI")]
    [SerializeField] private GameObject _lockOverlay;   // 잠금 오버레이 (반투명 어둠 + 자물쇠)
    [SerializeField] private GameObject _clearBadge;    // 클리어 완료 배지 ("CLEAR")

    private StageData _stageData;

    /// <summary>
    /// 스테이지 데이터와 상태를 바인딩한다.
    /// </summary>
    /// <param name="data">스테이지 데이터</param>
    /// <param name="isCleared">플레이어가 이미 클리어한 스테이지 여부</param>
    /// <param name="isAvailable">현재 도전 가능한 스테이지 여부 (직전 스테이지 클리어 또는 첫 스테이지)</param>
    public void Bind(StageData data, bool isCleared, bool isAvailable)
    {
        _stageData = data;

        _stageNameText.text = data.StageName;
        _entryCostText.text = $"{data.EntryCost}";
        _waveCountText.text = $"{data.Waves.Count}웨이브";
        _rewardText.text    = $"EXP {data.ClearRewards.TotalExp}  Gold {data.ClearRewards.TotalGold}";

        // 상태 UI 갱신
        if (_lockOverlay != null)  _lockOverlay.SetActive(!isAvailable);
        if (_clearBadge  != null)  _clearBadge.SetActive(isCleared);

        // 도전 가능 여부에 따라 버튼 활성화
        _selectButton.interactable = isAvailable;

        _selectButton.onClick.RemoveAllListeners();
        _selectButton.onClick.AddListener(OnSelectClicked);
    }

    private void OnSelectClicked()
    {
        if (_stageData == null) return;

        var gm = GameManager.Instance;
        if (gm == null)
        {
            Debug.LogError("[StageCellView] GameManager.Instance is null.");
            return;
        }

        if (!gm.TryConsumeEnergy(_stageData.EntryCost))
        {
            ShowEnergyLackPopup();
            return;
        }

        var flowManager = ServiceLocator.Get<GameFlowManager>();
        if (flowManager == null)
        {
            Debug.LogError("[StageCellView] GameFlowManager not registered in ServiceLocator.");
            return;
        }

        flowManager.LoadBattleScene(_stageData.StageId);
    }

    private void ShowEnergyLackPopup()
    {
        // TODO: 팝업 시스템 연결 후 전용 팝업으로 교체
        Debug.Log("[StageCellView] 에너지가 부족합니다.");

#if UNITY_EDITOR
        UnityEditor.EditorUtility.DisplayDialog("에너지 부족", "에너지가 부족합니다.", "확인");
#endif
    }
}
