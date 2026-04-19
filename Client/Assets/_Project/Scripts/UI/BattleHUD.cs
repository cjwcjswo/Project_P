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

    [Header("Combo")]
    [SerializeField] private TextMeshProUGUI _comboText;
    [SerializeField] private GameObject      _comboGroup;

    /// <summary>
    /// 파티 전원에 대해 HeroHUDView를 동적 생성하고 바인딩.
    /// </summary>
    public void BindParty(HeroParty party, UltimateGaugeManager ultManager, BattleManager battleManager)
    {
        foreach (var hero in party.Heroes)
        {
            var view = Instantiate(_heroHUDPrefab, _heroHUDContainer);
            view.gameObject.SetActive(true);
            int idx  = hero.PartyIndex;
            view.Bind(hero, ultManager, () => battleManager.ActivateUltimateAsync(idx).Forget());
        }
    }

    // BindWave 제거 — Enemy HUD는 EnemyEntityView 월드 스페이스 Canvas에서 직접 처리 (T-D3-009)

    /// <summary>
    /// 콤보 카운터 바인딩.
    /// </summary>
    public void BindCombo(ComboCalculator combo)
    {
        combo.OnComboChanged += count =>
        {
            _comboGroup.SetActive(count > 0);
            _comboText.text = $"{count}";
            if (count > 0)
            {
                _comboText.transform.localScale = Vector3.one * 1.5f;
                _comboText.transform.DOScale(Vector3.one, 0.3f).SetEase(Ease.OutBack);
                _comboText.transform.DOPunchScale(Vector3.one * 0.4f, 0.25f);
            }
        };
    }
}
