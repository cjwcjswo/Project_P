using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// 적 1마리의 HUD 슬롯 컴포넌트.
/// BattleHUD가 EnemyWave 내 적 수만큼 동적으로 생성하여 각 EnemyState에 바인딩.
///
/// 프리팹 구성:
///   - Image (PortraitImage)        : 적 초상화
///   - Image (HPFill)               : Type=Filled, Method=Horizontal
///   - Button (TargetButton)        : 초상화 위에 배치, 터치 시 일점사 지정
///   - GameObject (CastingBarGroup) : 스킬 있는 적만 표시 (SkillCooldown > 0)
///     └─ Image (CastingBarFill)    : Type=Filled, Method=Horizontal
///   - GameObject (DeadOverlay)     : 반투명 회색 오버레이, 기본 비활성
/// </summary>
public class EnemyHUDView : MonoBehaviour
{
    [SerializeField] private Image      _portraitImage;
    [SerializeField] private Image      _hpFill;
    [SerializeField] private Button     _targetButton;
    [SerializeField] private GameObject _castingBarGroup;
    [SerializeField] private Image      _castingBarFill;
    [SerializeField] private GameObject _deadOverlay;

    public void Bind(EnemyState enemy, TargetingSystem targeting)
    {
        // 초기 상태 설정
        _hpFill.fillAmount = 1f;
        _deadOverlay.SetActive(false);

        // 스킬 없는 몬스터는 캐스팅바 숨김 (GDD 2.1)
        bool hasSkill = enemy.SkillCooldown > 0f;
        _castingBarGroup.SetActive(false); // 캐스팅 시작 전까지 숨김
        if (!hasSkill)
            _castingBarGroup.SetActive(false);

        // HP
        enemy.OnHPChanged += (cur, max) =>
        {
            float ratio = (float)cur / max;
            _hpFill.DOFillAmount(ratio, 0.3f);
        };

        // 캐스팅 바 (스킬 있는 적만)
        if (hasSkill)
        {
            enemy.OnSkillCastProgress += progress =>
            {
                _castingBarGroup.SetActive(progress > 0f);
                _castingBarFill.fillAmount = progress;
            };

            enemy.OnSkillCast += () =>
            {
                _castingBarGroup.SetActive(false);
            };
        }

        // 사망
        enemy.OnDeath += () =>
        {
            _deadOverlay.SetActive(true);
            _targetButton.interactable = false;
            _castingBarGroup.SetActive(false);
        };

        // 터치 → 일점사 타겟 지정 (GDD 5.2)
        _targetButton.onClick.AddListener(() =>
        {
            if (!enemy.IsDead)
                targeting.SetManualTarget(enemy.WaveIndex);
        });
    }
}
