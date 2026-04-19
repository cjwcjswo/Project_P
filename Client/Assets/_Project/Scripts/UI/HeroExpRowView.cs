using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// BattleRewardView 내 히어로별 EXP 행. HeroExpRow 프리팹에 부착.
/// </summary>
public class HeroExpRowView : MonoBehaviour
{
    [SerializeField] private Image _heroPortrait;
    [SerializeField] private TextMeshProUGUI _heroNameText;
    [SerializeField] private TextMeshProUGUI _expText;

    public void Bind(int heroPartyIndex, int expGained)
    {
        _heroNameText.text = $"영웅 {heroPartyIndex + 1}";
        _expText.text      = $"EXP +{expGained}";

        // 초상화는 HeroColorMap 기반 색상 표시 (스프라이트 없는 경우 색상으로 대체)
        if (_heroPortrait != null)
        {
            _heroPortrait.color = HeroColorMap.GetBlockType(heroPartyIndex) switch
            {
                BlockType.Red    => new Color(0.9f, 0.3f, 0.3f),
                BlockType.Blue   => new Color(0.3f, 0.5f, 0.9f),
                BlockType.Green  => new Color(0.3f, 0.8f, 0.4f),
                BlockType.Yellow => new Color(0.9f, 0.8f, 0.2f),
                BlockType.Purple => new Color(0.7f, 0.3f, 0.9f),
                _                => Color.white
            };
        }
    }
}
