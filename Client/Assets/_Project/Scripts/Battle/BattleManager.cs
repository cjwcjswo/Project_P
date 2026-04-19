using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

public class BattleManager
{
    private HeroParty _party;
    private BattleSetupData _setupData;
    private EnemyWave _currentWave;
    private int _currentWaveIndex;
    private BoardController _boardController;
    private SkillSystem _skillSystem;
    private ComboCalculator _comboCalc;
    private UltimateGaugeManager _ultManager;
    private TargetingSystem _targeting;
    private CancellationTokenSource _cts;
    private CutInView _cutInView;

    public event Action OnBattleWin;
    public event Action OnBattleLose;

    /// <summary>웨이브 전환 시 발행. 인자: 새 웨이브, 웨이브 인덱스(0-based)</summary>
    public event Action<EnemyWave, int> OnWaveChanged;

    public UltimateGaugeManager UltManager => _ultManager;
    public TargetingSystem Targeting => _targeting;
    public EnemyWave CurrentWave => _currentWave;
    public int CurrentWaveIndex => _currentWaveIndex;
    public int TotalWaveCount => _setupData?.Waves.Length ?? 0;

    public void StartBattle(HeroParty party, BattleSetupData setupData, BoardController boardController)
    {
        _party = party;
        _setupData = setupData;
        _boardController = boardController;
        _currentWaveIndex = 0;

        _targeting = new TargetingSystem();
        _comboCalc = new ComboCalculator();
        _ultManager = new UltimateGaugeManager();
        _skillSystem = new SkillSystem(party, setupData.Waves[0], _targeting);

        _ultManager.Initialize(party);

        ServiceLocator.Register(_comboCalc);
        ServiceLocator.Register(_ultManager);
        ServiceLocator.Register(_skillSystem);
        ServiceLocator.Register(_targeting);

        EventBus.Subscribe<MatchStepSkillTriggerEvent>(OnMatchStepSkillTrigger);
        EventBus.Subscribe<CascadeCompleteEvent>(OnCascadeComplete);
        EventBus.Subscribe<SkillBlockTappedEvent>(OnSkillBlockTapped);
        _party.OnHeroDied += OnHeroDied;
        _party.OnAllDead += HandleAllHeroesDead;

        _cts = new CancellationTokenSource();

        foreach (var hero in party.Heroes)
            HeroAutoAttackLoop(hero, _cts.Token).Forget();

        StatusEffectTickLoop(_cts.Token).Forget();

        ActivateWave(setupData.Waves[0]);
    }

    /// <summary>
    /// 지정 웨이브를 활성화하고 적 루프를 시작한다.
    /// </summary>
    private void ActivateWave(EnemyWave wave)
    {
        if (_currentWave != null)
            _currentWave.OnAllEnemiesDead -= HandleAllEnemiesDead;

        _currentWave = wave;
        _currentWave.OnAllEnemiesDead += HandleAllEnemiesDead;
        _skillSystem.SetWave(wave);

        OnWaveChanged?.Invoke(wave, _currentWaveIndex);

        foreach (var enemy in wave.Enemies)
        {
            EnemyAutoAttackLoop(enemy, _cts.Token).Forget();
            if (enemy.SkillCooldown > 0)
                EnemySkillLoop(enemy, _cts.Token).Forget();
        }
    }

    /// <summary>
    /// 현재 웨이브 클리어 후 다음 웨이브로 진행. 마지막 웨이브면 전투 승리 처리.
    /// </summary>
    private void HandleAllEnemiesDead()
    {
        int nextIndex = _currentWaveIndex + 1;
        if (nextIndex < _setupData.Waves.Length)
        {
            _currentWaveIndex = nextIndex;
            ActivateWave(_setupData.Waves[_currentWaveIndex]);
        }
        else
        {
            _cts?.Cancel();
            var result = BuildBattleResult(isCleared: true);
            OnBattleWin?.Invoke();
            EventBus.Publish(new BattleCompleteEvent { Result = result });
            Cleanup();
        }
    }

    private void HandleAllHeroesDead()
    {
        _cts?.Cancel();
        var result = BuildBattleResult(isCleared: false);
        OnBattleLose?.Invoke();
        EventBus.Publish(new BattleCompleteEvent { Result = result });
        Cleanup();
    }

    private BattleResult BuildBattleResult(bool isCleared)
    {
        var survivedIds = new List<int>();
        foreach (var hero in _party.Heroes)
        {
            if (!hero.IsDead)
                survivedIds.Add(hero.PartyIndex);
        }
        return new BattleResult(
            stageId: _setupData.Stage.StageId,
            deployedHeroCount: _party.Heroes.Count,
            survivedHeroIds: survivedIds,
            isCleared: isCleared
        );
    }

    /// <summary>
    /// 캐스케이드 각 스텝마다 즉시 호출. 스텝 색상 데이터로 스킬/콤보/궁극기 처리.
    /// </summary>
    private void OnMatchStepSkillTrigger(MatchStepSkillTriggerEvent evt)
    {
        _comboCalc.IncrementCombo();
        _skillSystem.ExecuteFromCascade(evt.ColorBreakdown, _comboCalc);
        _ultManager.ChargeFromCascade(evt.ColorBreakdown);
    }

    /// <summary>
    /// 캐스케이드 전체 완료 시 콤보 리셋.
    /// </summary>
    private void OnCascadeComplete(CascadeCompleteEvent evt)
    {
        _comboCalc.Reset();
    }

    /// <summary>
    /// 히어로 사망 시 보드에서 해당 색상 블록을 Disabled(회색 장애물)로 전환.
    /// </summary>
    private void OnHeroDied(int partyIndex)
    {
        var color = HeroColorMap.GetBlockType(partyIndex);
        _boardController.DisableColorAsync(color).Forget();
    }

    /// <summary>
    /// 특정 히어로의 궁극기 발동 (UI에서 호출).
    /// </summary>
    public async UniTask ActivateUltimateAsync(int heroIndex)
    {
        var hero = _party.GetHeroByIndex(heroIndex);
        if (hero == null || hero.IsDead) return;
        if (hero.Grade < 3 || hero.UltimateSkill == null) return;

        var result = _ultManager.Activate(heroIndex, hero.Attack, _boardController.Board);
        // 게이지 미충전 시 취소
        if (!result.IsActivated) return;

        // 컷인 연출 (설정된 경우)
        if (_cutInView != null && !string.IsNullOrEmpty(hero.IllustrationPath))
        {
            var illustration = UnityEngine.Resources.Load<UnityEngine.Sprite>(hero.IllustrationPath);
            if (illustration != null)
                await _cutInView.PlayAsync(illustration);
        }

        // UltimateSkill 발동 (SkillSystem 경유)
        _skillSystem.ExecuteUltimateSkill(hero);

        // 보드 블록 파괴 + 캐스케이드
        if (result.DestroyedPositions != null && result.DestroyedPositions.Count > 0)
        {
            _boardController.Board.ClearBlocks(result.DestroyedPositions);
            EventBus.Publish(new GravityRefillEvent
            {
                GravityMoves = _boardController.Board.ApplyGravity(),
                RefillMoves  = _boardController.Board.Refill()
            });
            await UniTask.Delay(TimeSpan.FromSeconds(0.3f));
            await _boardController.ProcessCascadeAsync();
        }
    }

    public void SetCutInView(CutInView cutInView)
    {
        _cutInView = cutInView;
    }

    /// <summary>
    /// 매 Tick 모든 히어로/적의 StatusEffect 지속 시간을 감소시키고 만료된 효과를 제거한다.
    /// Burn 도트 데미지 등 지속 효과도 이 루프에서 처리한다.
    /// </summary>
    private async UniTaskVoid StatusEffectTickLoop(CancellationToken ct)
    {
        const float tickInterval = 0.1f;
        while (!ct.IsCancellationRequested)
        {
            await UniTask.Delay(
                TimeSpan.FromSeconds(tickInterval),
                cancellationToken: ct);

            // 히어로 Tick
            foreach (var hero in _party.Heroes)
            {
                if (hero.IsDead) continue;
                hero.TickEffects(tickInterval);
            }

            // 적 Tick (현재 웨이브)
            if (_currentWave != null)
            {
                foreach (var enemy in _currentWave.Enemies)
                {
                    if (enemy.IsDead) continue;

                    // Burn 도트 데미지 적용
                    var burn = enemy.ActiveEffects;
                    enemy.TickEffects(tickInterval);

                    // Burn이 활성 중이면 tick당 데미지 (TickEffects 전 체크)
                    foreach (var effect in burn)
                    {
                        if (effect.Type == StatusEffectType.Burn && effect.RemainingDuration > 0f)
                        {
                            int dotDamage = Mathf.Max(1, (int)(effect.Value * tickInterval));
                            enemy.TakeDamage(dotDamage);
                        }
                    }
                }
            }
        }
    }

    private async UniTaskVoid HeroAutoAttackLoop(HeroState hero, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await UniTask.Delay(
                TimeSpan.FromSeconds(hero.AutoAttackInterval),
                cancellationToken: ct);

            if (hero.IsDead) break;

            var target = _targeting.GetPriorityTarget(_currentWave.AliveEnemies);
            if (target == null) continue;

            int damage = hero.GetAutoAttackDamage();
            target.TakeDamage(damage);
            hero.NotifyAutoAttack(damage);
        }
    }

    private async UniTaskVoid EnemyAutoAttackLoop(EnemyState enemy, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await UniTask.Delay(
                TimeSpan.FromSeconds(enemy.AutoAttackInterval),
                cancellationToken: ct);

            if (enemy.IsDead) break;
            if (enemy.IsStunned) continue;

            var heroTarget = _targeting.GetHeroTarget(_party);
            if (heroTarget == null) continue;

            int damage = enemy.GetAutoAttackDamage();
            heroTarget.TakeDamage(damage);
            enemy.NotifyAutoAttack(damage);
        }
    }

    private async UniTaskVoid EnemySkillLoop(EnemyState enemy, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await UniTask.Delay(
                TimeSpan.FromSeconds(enemy.SkillCooldown),
                cancellationToken: ct);

            if (enemy.IsDead) break;
            if (enemy.IsStunned) continue;

            float castElapsed = 0f;
            bool interrupted = false;

            while (castElapsed < enemy.SkillCastTime)
            {
                await UniTask.Delay(
                    TimeSpan.FromSeconds(0.05f),
                    cancellationToken: ct);

                if (enemy.IsDead) { interrupted = true; break; }
                if (enemy.IsStunned)
                {
                    interrupted = true;
                    enemy.UpdateCastProgress(0f);
                    break;
                }

                castElapsed += 0.05f;
                enemy.UpdateCastProgress(castElapsed / enemy.SkillCastTime);
            }

            if (interrupted) continue;

            enemy.UpdateCastProgress(0f);
            enemy.NotifySkillCast();

            var heroTarget = _targeting.GetHeroTarget(_party);
            if (heroTarget != null)
                heroTarget.TakeDamage(enemy.SkillDamage);
        }
    }

    /// <summary>
    /// 2성 특수 블록 탭 발동: 십자 파괴 + UniqueSkill 즉시 실행.
    /// </summary>
    private void OnSkillBlockTapped(SkillBlockTappedEvent evt)
    {
        var hero = _party.GetHeroByColor(evt.Color);
        if (hero == null || hero.IsDead) return;

        // 1. 십자 모양 블록 파괴
        var destroyed = _boardController.Board.ClearCrossPattern(evt.Col, evt.Row);
        EventBus.Publish(new GravityRefillEvent
        {
            GravityMoves = _boardController.Board.ApplyGravity(),
            RefillMoves  = _boardController.Board.Refill()
        });

        // 2. UniqueSkill 즉시 발동
        _skillSystem.ExecuteUniqueSkill(hero);

        // 3. 보드 캐스케이드 후처리 (비동기)
        _boardController.ProcessCascadeAsync().Forget();
    }

    private void Cleanup()
    {
        EventBus.Unsubscribe<MatchStepSkillTriggerEvent>(OnMatchStepSkillTrigger);
        EventBus.Unsubscribe<CascadeCompleteEvent>(OnCascadeComplete);
        EventBus.Unsubscribe<SkillBlockTappedEvent>(OnSkillBlockTapped);
        if (_party != null)
        {
            _party.OnHeroDied -= OnHeroDied;
            _party.OnAllDead -= HandleAllHeroesDead;
        }
        if (_currentWave != null)
            _currentWave.OnAllEnemiesDead -= HandleAllEnemiesDead;
    }
}
