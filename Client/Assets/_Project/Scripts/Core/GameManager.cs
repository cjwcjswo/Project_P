using UnityEngine;
using Cysharp.Threading.Tasks;

/// <summary>
/// 전역 서비스 초기화 전담 매니저. DontDestroyOnLoad로 게임 전체에서 유지된다.
/// 유저 데이터·리소스 레포지토리·게임 플로우 매니저를 ServiceLocator에 등록하는 것이 책임의 전부.
/// 전투 관련 초기화(HeroParty, Board, BattleManager, UI 바인딩)는 BattleSceneManager가 담당한다.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Server")]
    [SerializeField] private string _serverBaseUrl = "http://localhost:5000";
    [SerializeField] private string _userId = "local_default";

    /// <summary>
    /// 에너지 현재값. UserDataManager에 위임.
    /// </summary>
    public int CurrentEnergy => ServiceLocator.Get<UserDataManager>()?.Energy ?? 0;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        InitializeCoreServicesAsync().Forget();
    }

    private async UniTaskVoid InitializeCoreServicesAsync()
    {
        // ── 1. 유저 데이터 로드 ─────────────────────────────────────────────
        var apiClient = new ApiClient(_serverBaseUrl);
        var userDataManager = new UserDataManager();
        await userDataManager.InitializeAsync(apiClient, _userId);

        if (!userDataManager.IsInitialized)
        {
            Debug.LogError("[GameManager] UserDataManager initialization failed.");
            return;
        }

        ServiceLocator.Register(userDataManager);

        // ── 2. 리소스 레포지토리 등록 ───────────────────────────────────────
        ServiceLocator.Register(new HeroDataRepository());
        ServiceLocator.Register(new SkillDataRepository());
        ServiceLocator.Register(new MonsterDataRepository());
        ServiceLocator.Register(new StageDataRepository());

        // ── 3. GameFlowManager 등록 ─────────────────────────────────────────
        ServiceLocator.Register(new GameFlowManager());

        Debug.Log("[GameManager] Core services initialized.");
    }

    /// <summary>
    /// 에너지 소모 시도. UserDataManager에 위임.
    /// 부족하면 false 반환(차감 없음), 충분하면 차감 후 true.
    /// </summary>
    public bool TryConsumeEnergy(int cost)
    {
        var udm = ServiceLocator.Get<UserDataManager>();
        if (udm == null)
        {
            Debug.LogError("[GameManager] UserDataManager not registered.");
            return false;
        }
        return udm.TryConsumeEnergy(cost);
    }
}
