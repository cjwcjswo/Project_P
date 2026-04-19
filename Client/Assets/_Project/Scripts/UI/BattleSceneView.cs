using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 전투 월드 공간의 히어로/적 스프라이트 오브젝트를 관리.
/// HeroEntityView / EnemyEntityView 프리팹을 SpawnPoint 리스트에 인덱스 순서로 동적 스폰하고,
/// 딕셔너리(partyIndex / waveIndex → view)로 개별 애니메이션을 제어한다.
/// </summary>
public class BattleSceneView : MonoBehaviour
{
    [Header("Hero World Entities")]
    [SerializeField] private HeroEntityView  _heroEntityPrefab;
    [SerializeField] private Transform[]     _heroSpawnPoints;   // 최대 5개, Index 0 = 최전방

    [Header("Enemy World Entities")]
    [SerializeField] private EnemyEntityView _enemyEntityPrefab;
    [SerializeField] private Transform[]     _enemySpawnPoints;  // 웨이브 적 수만큼

    [Header("Floating Text")]
    [SerializeField] private FloatingTextView _damageFloatingTextPrefab;
    [Tooltip("비우면 데미지 프리팹과 동일(공용 풀). 별도 프리팹이면 스킬 전용 연출을 줄 수 있음.")]
    [SerializeField] private FloatingTextView _skillFloatingTextPrefab;

    [Header("Cut-in")]
    [SerializeField] private CutInView _cutInView;

    public CutInView CutIn => _cutInView;

    // ── 런타임 상태 ───────────────────────────────────────────────────────

    private SkillSystem    _skillSystem;
    private TargetingSystem _targeting;
    private HeroParty      _party;
    private EnemyWave      _wave;

    private readonly Dictionary<int, HeroEntityView>  _heroViews  = new();
    private readonly Dictionary<int, EnemyEntityView> _enemyViews = new();
    private readonly Queue<FloatingTextView> _damageFloatingPool = new();
    private readonly Queue<FloatingTextView> _skillFloatingPool  = new();

    // ── 초기화 ────────────────────────────────────────────────────────────

    /// <summary>
    /// 파티/웨이브 내 모든 히어로·적을 SpawnPoint에 스폰하고 이벤트 구독.
    /// </summary>
    public void Bind(HeroParty party, EnemyWave wave,
                     SkillSystem skillSystem, TargetingSystem targeting)
    {
        _party       = party;
        _wave        = wave;
        _skillSystem = skillSystem;
        _targeting   = targeting;

        SpawnHeroes(party);
        SpawnEnemies(wave);

        _skillSystem.OnSkillExecuted += OnSkillExecuted;
    }

    /// <summary>
    /// BattleManager의 멀티웨이브 이벤트를 구독하여 웨이브 전환 시 적 뷰를 갱신한다.
    /// Bind() 호출 이후에 연결.
    /// </summary>
    public void BindBattleManager(BattleManager battleManager)
    {
        battleManager.OnWaveChanged += OnWaveChanged;
    }

    private void OnWaveChanged(EnemyWave newWave, int waveIndex)
    {
        _wave = newWave;

        // 기존 적 뷰 제거 (DOTween / EnemyState 구독 정리 후 Destroy)
        foreach (var view in _enemyViews.Values)
        {
            if (view == null) continue;
            view.UnbindAndKillTweens();
            Destroy(view.gameObject);
        }
        _enemyViews.Clear();

        SpawnEnemies(newWave);
    }

    private void SpawnHeroes(HeroParty party)
    {
        var heroes = party.Heroes;
        for (int i = 0; i < heroes.Count; i++)
        {
            if (i >= _heroSpawnPoints.Length)
            {
                Debug.LogWarning($"[BattleSceneView] HeroSpawnPoints 부족: index={i}");
                break;
            }

            var view = Instantiate(_heroEntityPrefab, _heroSpawnPoints[i]);
            view.transform.localPosition = Vector3.zero;
            view.Bind(heroes[i]);

            int partyIndex = heroes[i].PartyIndex;
            _heroViews[partyIndex] = view;

            // 자동 평타 이벤트 구독
            heroes[i].OnAutoAttack += _ => OnHeroAutoAttack(partyIndex);

            // 피격 데미지 텍스트
            heroes[i].OnDamageTaken += dmg =>
                SpawnDamageText(dmg, view.WorldPosition + Vector3.up * 0.5f);
        }
    }

    private void SpawnEnemies(EnemyWave wave)
    {
        var enemies = wave.Enemies;
        for (int i = 0; i < enemies.Count; i++)
        {
            if (i >= _enemySpawnPoints.Length)
            {
                Debug.LogWarning($"[BattleSceneView] EnemySpawnPoints 부족: index={i}");
                break;
            }

            var view = Instantiate(_enemyEntityPrefab, _enemySpawnPoints[i]);
            view.transform.localPosition = Vector3.zero;
            view.Bind(enemies[i]);
            view.BindHUD(enemies[i], _targeting);

            int waveIndex = enemies[i].WaveIndex;
            _enemyViews[waveIndex] = view;

            // 자동 평타 이벤트 구독
            enemies[i].OnAutoAttack += _ => OnEnemyAutoAttack(waveIndex);

            // 피격 데미지 텍스트
            enemies[i].OnDamageTaken += dmg =>
                SpawnDamageText(dmg, view.WorldPosition + Vector3.up * 0.5f);
        }
    }

    // ── 전투 비주얼 핸들러 ────────────────────────────────────────────────

    private void OnHeroAutoAttack(int partyIndex)
    {
        if (_heroViews.TryGetValue(partyIndex, out var heroView))
            heroView.PlayAttackAnim();

        // 타겟 적 피격 플래시
        var target = _targeting.GetPriorityTarget(_wave.AliveEnemies);
        if (target != null && _enemyViews.TryGetValue(target.WaveIndex, out var enemyView))
            enemyView.PlayHitFlash(new Color(1f, 0.8f, 0.8f));
    }

    private void OnEnemyAutoAttack(int waveIndex)
    {
        if (_enemyViews.TryGetValue(waveIndex, out var enemyView))
            enemyView.PlayAttackAnim();

        // 타겟 히어로 피격 플래시
        var targetHero = _targeting.GetHeroTarget(_party);
        if (targetHero != null && _heroViews.TryGetValue(targetHero.PartyIndex, out var heroView))
            heroView.PlayDamageFlash(new Color(1f, 0.8f, 0.8f));
    }

    private void OnSkillExecuted(SkillEffect effect)
    {
        int srcPartyIndex = HeroColorMap.GetHeroIndex(effect.SourceColor);

        _heroViews.TryGetValue(srcPartyIndex, out var srcHeroView);
        var priorityEnemy = _targeting.GetPriorityTarget(_wave.AliveEnemies);

        switch (effect.ActionType)
        {
            case ActionType.Attack:
                srcHeroView?.PlayAttackAnim();
                if (effect.TargetScope == TargetScope.All ||
                    (effect.TargetScope == TargetScope.Multi && effect.MaxTargetCount >= _enemyViews.Count))
                {
                    foreach (var ev in _enemyViews.Values)
                        ev.PlayAoEHitAnim(GetBlockColor(effect.SourceColor));
                }
                else
                {
                    if (priorityEnemy != null && _enemyViews.TryGetValue(priorityEnemy.WaveIndex, out var sv))
                        sv.PlayHitFlash(GetBlockColor(effect.SourceColor));
                }
                break;

            case ActionType.Heal:
                srcHeroView?.PlayColorFlash(Color.green);
                break;

            case ActionType.Shield:
                srcHeroView?.PlayColorFlash(Color.cyan);
                break;

            case ActionType.Buff:
                if (srcHeroView != null) srcHeroView.PlayColorFlash(Color.yellow);
                break;
        }

        // 스킬 이름 플로팅 텍스트
        if (srcHeroView != null)
            SpawnSkillText(effect.SkillName, srcHeroView.WorldPosition);
    }

    // ── 플로팅 텍스트 풀 ──────────────────────────────────────────────────

    private FloatingTextView SkillFloatingPrefab =>
        _skillFloatingTextPrefab != null ? _skillFloatingTextPrefab : _damageFloatingTextPrefab;

    private bool UseSharedFloatingPools =>
        _damageFloatingTextPrefab == null ||
        _skillFloatingTextPrefab == null ||
        _skillFloatingTextPrefab == _damageFloatingTextPrefab;

    private FloatingTextView RentDamageFloating()
    {
        if (_damageFloatingTextPrefab == null) return null;
        return RentFromPool(_damageFloatingPool, _damageFloatingTextPrefab, ReturnDamageFloating);
    }

    private FloatingTextView RentSkillFloating()
    {
        var prefab = SkillFloatingPrefab;
        if (prefab == null) return null;
        if (UseSharedFloatingPools)
            return RentFromPool(_damageFloatingPool, _damageFloatingTextPrefab, ReturnDamageFloating);
        return RentFromPool(_skillFloatingPool, _skillFloatingTextPrefab, ReturnSkillFloating);
    }

    private FloatingTextView RentFromPool(
        Queue<FloatingTextView> pool,
        FloatingTextView prefab,
        Action<FloatingTextView> onRelease)
    {
        FloatingTextView view;
        if (pool.Count > 0)
            view = pool.Dequeue();
        else
        {
            view = Instantiate(prefab, transform);
            view.OnRelease = onRelease;
        }

        return view;
    }

    private void ReturnDamageFloating(FloatingTextView view)
    {
        _damageFloatingPool.Enqueue(view);
    }

    private void ReturnSkillFloating(FloatingTextView view)
    {
        _skillFloatingPool.Enqueue(view);
    }

    private void SpawnDamageText(int damage, Vector3 worldPos)
    {
        var v = RentDamageFloating();
        if (v == null) return;
        v.Show(damage.ToString(), worldPos);
    }

    private void SpawnSkillText(string skillName, Vector3 worldPos)
    {
        var v = RentSkillFloating();
        if (v == null) return;
        v.Show(skillName, worldPos);
    }

    // ── 유틸 ──────────────────────────────────────────────────────────────

    private static Color GetBlockColor(BlockType blockType) => blockType switch
    {
        BlockType.Red    => Color.red,
        BlockType.Yellow => Color.yellow,
        BlockType.Green  => Color.green,
        BlockType.Blue   => new Color(0.2f, 0.5f, 1f),
        BlockType.Purple => new Color(0.6f, 0.2f, 0.9f),
        _                => Color.white
    };

    private void OnDestroy()
    {
        if (_skillSystem != null)
            _skillSystem.OnSkillExecuted -= OnSkillExecuted;
    }
}
