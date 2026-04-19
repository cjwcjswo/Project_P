using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;

/// <summary>
/// Battle 씬 전용 초기화 매니저. DontDestroyOnLoad가 아닌 씬 로컬 오브젝트.
/// 씬이 로드되면 Awake에서 ServiceLocator의 전역 서비스(UserDataManager 등)가
/// 준비될 때까지 대기한 후 전투 초기화와 UI 바인딩을 수행한다.
///
/// GameManager가 담당했던 전투 관련 로직을 전담한다:
///   - GameFlowManager.CurrentStageId 기반 스테이지 선택
///   - UserDataManager.DeployedHeroes → HeroParty 구성
///   - Board / BoardController 초기화
///   - BattleManager.StartBattle()
///   - PuzzleBoardView / BattleHUD / BattleSceneView 바인딩
/// </summary>
public class BattleSceneManager : MonoBehaviour
{
    [Header("Battle Views")]
    [SerializeField] private PuzzleBoardView _puzzleBoardView;
    [SerializeField] private BattleHUD       _battleHUD;
    [SerializeField] private BattleSceneView _battleSceneView;

    private BattleManager _battleManager;

    private void Awake()
    {
        InitializeBattleAsync().Forget();
    }

    private async UniTaskVoid InitializeBattleAsync()
    {
        // GameManager의 InitializeCoreServicesAsync가 비동기이므로
        // UserDataManager가 ServiceLocator에 등록될 때까지 대기
        var udm         = await WaitForService<UserDataManager>();
        var stageRepo   = await WaitForService<StageDataRepository>();
        var monsterRepo = await WaitForService<MonsterDataRepository>();
        var flow        = ServiceLocator.Get<GameFlowManager>();

        // ── 1. 스테이지 결정 ──────────────────────────────────────────────
        int stageId   = (flow != null && flow.CurrentStageId > 0) ? flow.CurrentStageId : 1;
        var stageData = stageRepo.GetById(stageId);
        if (stageData == null)
        {
            Debug.LogError($"[BattleSceneManager] StageData not found for stageId={stageId}.");
            return;
        }

        // ── 2. HeroParty 구성 ─────────────────────────────────────────────
        var deployedHeroes = udm.DeployedHeroes;
        if (deployedHeroes == null || deployedHeroes.Count == 0)
        {
            Debug.LogError("[BattleSceneManager] No deployed heroes found.");
            return;
        }

        var heroRepo  = await WaitForService<HeroDataRepository>();
        var skillRepo = await WaitForService<SkillDataRepository>();

        var heroes = new List<HeroState>();
        for (int i = 0; i < deployedHeroes.Count; i++)
        {
            var dto      = deployedHeroes[i];
            var heroData = heroRepo.GetById(dto.HeroId);
            if (heroData == null)
            {
                Debug.LogError($"[BattleSceneManager] HeroData not found for HeroId={dto.HeroId}. Skipping.");
                continue;
            }

            var skillData = skillRepo.GetById(heroData.SkillId);
            int level     = dto.Level > 0 ? dto.Level : 1;
            heroes.Add(HeroFactory.Create(heroData, skillData, level, partyIndex: i));
        }

        if (heroes.Count == 0)
        {
            Debug.LogError("[BattleSceneManager] No heroes could be created. Aborting battle.");
            return;
        }

        var party = new HeroParty(heroes);

        // ── 3. Board / BoardController 초기화 ────────────────────────────
        var activeColors = party.GetActiveColors();
        var board        = new Board();
        board.Initialize(activeColors);
        var controller = new BoardController(board, activeColors);
        ServiceLocator.Register(controller);

        _puzzleBoardView.Initialize(controller);

        // ── 4. 전투 시작 ──────────────────────────────────────────────────
        var setupData  = new BattleSetupData(stageData, monsterRepo);
        _battleManager = new BattleManager();
        _battleManager.StartBattle(party, setupData, controller);

        // ── 5. UI 바인딩 ──────────────────────────────────────────────────
        _battleHUD.BindParty(party, _battleManager.UltManager, _battleManager);
        _battleHUD.BindCombo(ServiceLocator.Get<ComboCalculator>());

        _battleSceneView.Bind(party, _battleManager.CurrentWave,
            ServiceLocator.Get<SkillSystem>(),
            _battleManager.Targeting);
        _battleSceneView.BindBattleManager(_battleManager);

        Debug.Log($"[BattleSceneManager] Battle initialized. StageId={stageId}");
    }

    /// <summary>
    /// 지정한 타입이 ServiceLocator에 등록될 때까지 프레임 단위로 대기.
    /// GameManager.InitializeCoreServicesAsync()가 비동기이므로 타이밍 차이를 흡수한다.
    /// </summary>
    private static async UniTask<T> WaitForService<T>() where T : class
    {
        T svc;
        while ((svc = ServiceLocator.Get<T>()) == null)
            await UniTask.Yield();
        return svc;
    }
}
