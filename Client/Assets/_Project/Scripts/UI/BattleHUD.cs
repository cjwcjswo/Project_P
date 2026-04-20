using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

/// <summary>
/// 전투 HUD 루트 컴포넌트.
/// 히어로/적 HUD 슬롯을 프리팹 기반으로 동적 생성하여 다중 히어로/다중 적을 지원.
///
/// Scene 오브젝트 필수 할당:
///   - HeroHUDPrefab, HeroHUDContainer   : 아군 진영 슬롯
///   - EnemyHUDPrefab, EnemyHUDContainer : 적군 진영 슬롯
///   - ComboText, ComboGroup             : 콤보 표시
/// </summary>
public class BattleHUD : MonoBehaviour
{
    [Header("Hero HUD")]
    [SerializeField] private HeroHUDView _heroHUDPrefab;
    [SerializeField] private Transform   _heroHUDContainer;

    // Enemy HUD는 EnemyEntityView 월드 스페이스 Canvas로 이전됨 (T-D3-009)

    [Header("Stage / Wave")]
    [SerializeField] private TextMeshProUGUI _stageText;
    [SerializeField] private TextMeshProUGUI _waveText;

    [Header("Wave Banner")]
    [Tooltip("웨이브 전환 시 화면을 가로지르는 'Wave N' 배너 텍스트. RectTransform 기반 UI에 배치.")]
    [SerializeField] private TextMeshProUGUI _waveBannerText;
    [SerializeField] private float _waveBannerEnterDuration = 0.35f;
    [SerializeField] private float _waveBannerHoldDuration  = 0.20f;
    [SerializeField] private float _waveBannerExitDuration  = 0.35f;

    private Sequence _waveBannerSeq;

    [Header("Combo")]
    [SerializeField] private TextMeshProUGUI _comboText;
    [SerializeField] private TextMeshProUGUI _comboMultiplierText;
    [SerializeField] private GameObject      _comboGroup;

    /// <summary>
    /// 스테이지/웨이브 표시 텍스트 바인딩. 웨이브 전환 시 자동 갱신.
    /// </summary>
    public void BindStageWave(int stageId, BattleManager battleManager)
    {
        UpdateStageWaveText(stageId, battleManager.CurrentWaveIndex + 1, battleManager.TotalWaveCount);
        battleManager.OnWaveChanged += (_, waveIdx) =>
        {
            UpdateStageWaveText(stageId, waveIdx + 1, battleManager.TotalWaveCount);
            PlayWaveBanner(waveIdx + 1);
        };
    }

    private void UpdateStageWaveText(int stage, int wave, int total)
    {
        if (_stageText != null) _stageText.text = $"Stage {stage}";
        if (_waveText  != null) _waveText.text  = $"Wave {wave}/{total}";
    }

    private void PlayWaveBanner(int waveNumber)
    {
        if (_waveBannerText == null) return;

        var rt = _waveBannerText.rectTransform;
        if (rt == null) return;
        var parent = rt.parent as RectTransform;
        if (parent == null) return;

        _waveBannerText.gameObject.SetActive(true);
        _waveBannerText.text = $"Wave {waveNumber}";

        LayoutRebuilder.ForceRebuildLayoutImmediate(rt);

        float parentW = parent.rect.width;
        float textW = rt.rect.width;
        float y = rt.anchoredPosition.y;
        float startX = -parentW * 0.5f - textW;
        float centerX = 0f;
        float endX = parentW * 0.5f + textW;

        _waveBannerSeq?.Kill(false);
        rt.DOKill(false);
        rt.anchoredPosition = new Vector2(startX, y);

        _waveBannerSeq = DOTween.Sequence()
            .Append(rt.DOAnchorPosX(centerX, _waveBannerEnterDuration).SetEase(Ease.OutCubic))
            .AppendInterval(_waveBannerHoldDuration)
            .Append(rt.DOAnchorPosX(endX, _waveBannerExitDuration).SetEase(Ease.InCubic));
    }

    /// <summary>
    /// 파티 전원에 대해 HeroHUDView를 동적 생성하고 바인딩.
    /// </summary>
    public void BindParty(HeroParty party, UltimateGaugeManager ultManager, BattleManager battleManager)
    {
        // SetAsFirstSibling()으로 매번 맨 앞에 삽입 → 결과적으로 역순 배치.
        // 월드 공간에서 0번(최전방)이 가장 왼쪽에 있으므로 HUD는 0번이 가장 오른쪽에 위치해야 함.
        foreach (var hero in party.Heroes)
        {
            var view = Instantiate(_heroHUDPrefab, _heroHUDContainer);
            view.transform.SetAsFirstSibling();
            view.gameObject.SetActive(true);
            int idx  = hero.PartyIndex;
            view.Bind(hero, ultManager, () => battleManager.ActivateUltimateAsync(idx).Forget());
        }
    }

    // BindWave 제거 — Enemy HUD는 EnemyEntityView 월드 스페이스 Canvas에서 직접 처리 (T-D3-009)

    /// <summary>
    /// 콤보 카운터 + 데미지 배율 바인딩.
    /// </summary>
    public void BindCombo(ComboCalculator combo)
    {
        combo.OnComboChanged += count =>
        {
            _comboGroup.SetActive(count > 0);
            _comboText.text = $"{count}";

            if (count > 0)
            {
                // 배율 텍스트 갱신
                if (_comboMultiplierText != null)
                {
                    float multiplier = combo.GetMultiplierAt(count);
                    _comboMultiplierText.text = $"x{multiplier:F1}";

                    Color targetColor = count >= 5 ? new Color(1f, 0.5f, 0f)
                                      : count >= 3 ? Color.yellow
                                      : Color.white;
                    _comboMultiplierText.DOColor(targetColor, 0.2f);
                }

                _comboText.transform.localScale = Vector3.one * 1.5f;
                _comboText.transform.DOScale(Vector3.one, 0.3f).SetEase(Ease.OutBack);
                _comboText.transform.DOPunchScale(Vector3.one * 0.4f, 0.25f);
            }
            else
            {
                if (_comboMultiplierText != null)
                {
                    _comboMultiplierText.DOKill();
                    _comboMultiplierText.color = Color.white;
                }
            }
        };
    }
}
