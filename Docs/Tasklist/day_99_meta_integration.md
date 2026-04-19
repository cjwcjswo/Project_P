# Day 4 - Meta Systems & Integration 구현 가이드

## T-014: 스테이지 진행 시스템

### 목표
스테이지별 적 데이터를 관리하고, 전투 승리 시 다음 스테이지로 진행하는 시스템을 구현한다.

### 작업 내용

#### 1. StageData ScriptableObject
```
경로: Client/Assets/_Project/Scripts/Data/StageData.cs
```
```csharp
using UnityEngine;

[CreateAssetMenu(fileName = "Stage_", menuName = "PuzzleKnight/StageData")]
public class StageData : ScriptableObject
{
    public int stageNumber;
    public string enemyName;
    public int baseEnemyHP;
    public int baseEnemyAttack;
    public float enemyAttackInterval;  // 초
    public float hpMultiplier;         // 난이도 스케일링
    public float attackMultiplier;
    public int rewardGold;
    public int rewardExp;

    public int ScaledHP => (int)(baseEnemyHP * hpMultiplier);
    public int ScaledAttack => (int)(baseEnemyAttack * attackMultiplier);
}
```

#### 2. 샘플 StageData 5개 생성
`Client/Assets/_Project/ScriptableObjects/Stages/`에 생성:

| 파일명 | Stage | HP | ATK | 간격 | HP배율 | ATK배율 | Gold | Exp |
|--------|-------|----|-----|------|--------|---------|------|-----|
| Stage_001.asset | 1 | 100 | 5 | 2.5s | 1.0 | 1.0 | 50 | 30 |
| Stage_002.asset | 2 | 150 | 8 | 2.3s | 1.0 | 1.0 | 80 | 50 |
| Stage_003.asset | 3 | 220 | 12 | 2.0s | 1.0 | 1.0 | 120 | 75 |
| Stage_004.asset | 4 | 300 | 16 | 1.8s | 1.0 | 1.0 | 170 | 100 |
| Stage_005.asset | 5 | 400 | 22 | 1.5s | 1.0 | 1.0 | 230 | 140 |

> ScriptableObject 인스턴스는 Unity 에디터에서 직접 생성하거나 에디터 스크립트로 자동 생성.

#### 3. StageManager
```
경로: Client/Assets/_Project/Scripts/Meta/StageManager.cs
```
```csharp
using System;
using System.Collections.Generic;

public class StageManager
{
    private List<StageData> _stages;
    private int _currentStageIndex;

    public StageData CurrentStage => _stages[_currentStageIndex];
    public int CurrentStageNumber => _currentStageIndex + 1;

    public event Action<StageData> OnStageChanged;

    public StageManager(List<StageData> stages)
    {
        _stages = stages;
        _currentStageIndex = 0;
    }

    /// <summary>
    /// 저장된 진행도에서 복원
    /// </summary>
    public void SetProgress(int stageIndex)
    {
        _currentStageIndex = Math.Clamp(stageIndex, 0, _stages.Count - 1);
    }

    /// <summary>
    /// 현재 스테이지의 적 상태 생성
    /// </summary>
    public EnemyState CreateEnemy()
    {
        var data = CurrentStage;
        return new EnemyState(data.ScaledHP, data.ScaledAttack, data.enemyAttackInterval);
    }

    /// <summary>
    /// 전투 승리 시 호출 — 다음 스테이지로 진행
    /// </summary>
    public StageReward AdvanceStage()
    {
        var reward = new StageReward
        {
            Gold = CurrentStage.rewardGold,
            Exp = CurrentStage.rewardExp
        };

        if (_currentStageIndex < _stages.Count - 1)
        {
            _currentStageIndex++;
            OnStageChanged?.Invoke(CurrentStage);
        }

        return reward;
    }
}

public struct StageReward
{
    public int Gold;
    public int Exp;
}
```

### 완료 조건
- [ ] StageData SO에서 적 스탯 정상 로드
- [ ] CreateEnemy()가 스케일링된 스탯으로 EnemyState 생성
- [ ] AdvanceStage() 호출 시 다음 스테이지로 이동 + 보상 반환
- [ ] 마지막 스테이지에서 인덱스 오버플로우 없음

---

## T-015: 에너지 시스템

### 목표
스테이지 진입에 필요한 에너지를 관리하고, 시간 기반으로 자동 회복한다.

### 작업 내용

#### EnergySystem
```
경로: Client/Assets/_Project/Scripts/Meta/EnergySystem.cs
```
```csharp
using System;

public class EnergySystem
{
    public const int MAX_ENERGY = 30;
    public const int COST_PER_STAGE = 1;
    private const int RECOVERY_INTERVAL_MINUTES = 10;

    public int CurrentEnergy { get; private set; }
    private DateTime _lastRecoveryTime;

    public event Action<int, int> OnEnergyChanged; // (current, max)

    public EnergySystem()
    {
        CurrentEnergy = MAX_ENERGY;
        _lastRecoveryTime = DateTime.UtcNow;
    }

    /// <summary>
    /// 앱 복귀 시 또는 에너지 확인 시 호출.
    /// 경과 시간에 따라 에너지 회복.
    /// </summary>
    public void RecoverByTime()
    {
        if (CurrentEnergy >= MAX_ENERGY) return;

        var now = DateTime.UtcNow;
        var elapsed = now - _lastRecoveryTime;
        int recovered = (int)(elapsed.TotalMinutes / RECOVERY_INTERVAL_MINUTES);

        if (recovered <= 0) return;

        CurrentEnergy = Math.Min(MAX_ENERGY, CurrentEnergy + recovered);
        // 잔여 시간 보존: 정확히 회복된 만큼만 차감
        _lastRecoveryTime = _lastRecoveryTime.AddMinutes(recovered * RECOVERY_INTERVAL_MINUTES);
        OnEnergyChanged?.Invoke(CurrentEnergy, MAX_ENERGY);
    }

    /// <summary>
    /// 다음 에너지 회복까지 남은 시간 (UI 표시용)
    /// </summary>
    public TimeSpan TimeUntilNextRecovery()
    {
        if (CurrentEnergy >= MAX_ENERGY) return TimeSpan.Zero;

        var nextRecovery = _lastRecoveryTime.AddMinutes(RECOVERY_INTERVAL_MINUTES);
        var remaining = nextRecovery - DateTime.UtcNow;
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }

    /// <summary>
    /// 스테이지 진입 시 에너지 소모. 부족하면 false 반환.
    /// </summary>
    public bool TryConsume(int amount = COST_PER_STAGE)
    {
        RecoverByTime(); // 소모 전 회복 체크

        if (CurrentEnergy < amount) return false;

        CurrentEnergy -= amount;
        OnEnergyChanged?.Invoke(CurrentEnergy, MAX_ENERGY);
        return true;
    }

    /// <summary>
    /// 저장/복원용
    /// </summary>
    public void LoadState(int energy, DateTime lastRecovery)
    {
        CurrentEnergy = Math.Clamp(energy, 0, MAX_ENERGY);
        _lastRecoveryTime = lastRecovery;
        RecoverByTime();
    }

    public (int energy, DateTime lastRecovery) SaveState()
    {
        return (CurrentEnergy, _lastRecoveryTime);
    }
}
```

### 동작 시나리오
```
에너지 30/30 → Battle 진입 → 29/30
10분 경과 → 30/30 (자동 회복)
앱 종료 후 2시간 뒤 재접속 → 12개 회복 계산
```

### 주의사항
- 시간 비교는 반드시 `DateTime.UtcNow` 사용 (PRD 서버 검증 대비)
- `Update()`로 타이머 폴링 금지 — 필요 시 `RecoverByTime()`을 명시적으로 호출
- PlayerPrefs 또는 JSON으로 `SaveState/LoadState` 영속화 (서버 연동 전까지)

### 완료 조건
- [ ] TryConsume()으로 에너지 정상 차감
- [ ] 에너지 부족 시 false 반환
- [ ] 경과 시간에 비례한 에너지 회복 정상 동작
- [ ] MAX_ENERGY 초과 회복 불가

---

## T-016: 방치형 보상 계산기

### 목표
마지막 접속 이후 경과 시간을 기반으로 오프라인 보상을 계산한다.

### 작업 내용

#### IdleRewardCalculator
```
경로: Client/Assets/_Project/Scripts/Meta/IdleRewardCalculator.cs
```
```csharp
using System;

public static class IdleRewardCalculator
{
    private const int MAX_IDLE_HOURS = 12;
    private const int GOLD_PER_STAGE_PER_MINUTE = 10;
    private const int EXP_PER_STAGE_PER_MINUTE = 5;
    private const int MIN_IDLE_SECONDS = 60; // 최소 1분

    /// <summary>
    /// 오프라인 보상 계산
    /// </summary>
    /// <param name="lastLoginUtc">마지막 접속 시각 (UTC)</param>
    /// <param name="currentStage">현재 스테이지 번호</param>
    /// <returns>보상 정보. 경과 시간이 1분 미만이면 null</returns>
    public static IdleReward? Calculate(DateTime lastLoginUtc, int currentStage)
    {
        var elapsed = DateTime.UtcNow - lastLoginUtc;

        if (elapsed.TotalSeconds < MIN_IDLE_SECONDS)
            return null;

        // 최대 12시간 상한
        double cappedMinutes = Math.Min(elapsed.TotalMinutes, MAX_IDLE_HOURS * 60);

        int gold = (int)(currentStage * GOLD_PER_STAGE_PER_MINUTE * cappedMinutes);
        int exp = (int)(currentStage * EXP_PER_STAGE_PER_MINUTE * cappedMinutes);

        return new IdleReward
        {
            Gold = gold,
            Exp = exp,
            ElapsedMinutes = (int)cappedMinutes
        };
    }
}

public struct IdleReward
{
    public int Gold;
    public int Exp;
    public int ElapsedMinutes;
}
```

### 보상 계산 예시

| 경과 시간 | 스테이지 | 골드 | 경험치 |
|-----------|---------|------|--------|
| 30분 | 1 | 300 | 150 |
| 2시간 | 3 | 3,600 | 1,800 |
| 8시간 | 5 | 24,000 | 12,000 |
| 24시간 (12시간 캡) | 5 | 36,000 | 18,000 |

### 완료 조건
- [ ] 경과 시간 1분 미만 시 null 반환
- [ ] 스테이지 * 시간에 비례한 보상 계산
- [ ] 12시간 상한 적용 확인

---

## T-017: 히어로 성장 및 레벨업

### 목표
경험치/골드 기반 히어로 레벨업과 스탯 성장 시스템을 구현한다.

### 작업 내용

#### HeroProgression
```
경로: Client/Assets/_Project/Scripts/Meta/HeroProgression.cs
```
```csharp
using System;

public class HeroProgression
{
    public int Level { get; private set; }
    public int Exp { get; private set; }
    public int Gold { get; private set; }

    // 기본 스탯 (레벨 1 기준)
    private const int BASE_HP = 100;
    private const int BASE_ATTACK = 10;
    private const int BASE_DEFENSE = 3;

    // 레벨당 증가량
    private const int HP_PER_LEVEL = 20;
    private const int ATTACK_PER_LEVEL = 3;
    private const int DEFENSE_PER_LEVEL = 1;

    public event Action<int> OnLevelUp;                     // (newLevel)
    public event Action<int, int, int> OnStatsChanged;      // (hp, atk, def)
    public event Action<int> OnGoldChanged;
    public event Action<int, int> OnExpChanged;              // (current, required)

    public int MaxHP => BASE_HP + (Level - 1) * HP_PER_LEVEL;
    public int Attack => BASE_ATTACK + (Level - 1) * ATTACK_PER_LEVEL;
    public int Defense => BASE_DEFENSE + (Level - 1) * DEFENSE_PER_LEVEL;
    public int ExpRequired => 100 * Level;

    public HeroProgression(int level = 1, int exp = 0, int gold = 0)
    {
        Level = level;
        Exp = exp;
        Gold = gold;
    }

    public void AddExp(int amount)
    {
        Exp += amount;
        while (Exp >= ExpRequired)
        {
            Exp -= ExpRequired;
            Level++;
            OnLevelUp?.Invoke(Level);
            OnStatsChanged?.Invoke(MaxHP, Attack, Defense);
        }
        OnExpChanged?.Invoke(Exp, ExpRequired);
    }

    public void AddGold(int amount)
    {
        Gold += amount;
        OnGoldChanged?.Invoke(Gold);
    }

    /// <summary>
    /// 골드로 레벨업 (수동). 비용: 100 * currentLevel
    /// </summary>
    public bool TryLevelUpWithGold()
    {
        int cost = 100 * Level;
        if (Gold < cost) return false;

        Gold -= cost;
        Level++;

        OnGoldChanged?.Invoke(Gold);
        OnLevelUp?.Invoke(Level);
        OnStatsChanged?.Invoke(MaxHP, Attack, Defense);
        OnExpChanged?.Invoke(Exp, ExpRequired);

        return true;
    }

    /// <summary>
    /// 전투 시작 시 현재 스탯으로 HeroState 생성
    /// </summary>
    public HeroState CreateBattleState()
    {
        return new HeroState(MaxHP, Attack, Defense, Level);
    }

    /// <summary>
    /// 저장/복원용
    /// </summary>
    public void LoadState(int level, int exp, int gold)
    {
        Level = Math.Max(1, level);
        Exp = Math.Max(0, exp);
        Gold = Math.Max(0, gold);
    }

    public (int level, int exp, int gold) SaveState() => (Level, Exp, Gold);
}
```

### 스탯 성장 테이블

| 레벨 | HP | ATK | DEF | 레벨업 필요 EXP | 골드 레벨업 비용 |
|------|-----|-----|-----|-----------------|------------------|
| 1 | 100 | 10 | 3 | 100 | 100 |
| 5 | 180 | 22 | 7 | 500 | 500 |
| 10 | 280 | 37 | 12 | 1,000 | 1,000 |

### 완료 조건
- [ ] EXP 추가 시 임계값 도달 시 자동 레벨업 (연속 레벨업 포함)
- [ ] 레벨업 시 MaxHP, Attack, Defense 증가
- [ ] 골드 레벨업: 비용 부족 시 false, 성공 시 골드 차감 + 레벨업
- [ ] CreateBattleState()가 현재 스탯 반영한 HeroState 생성

---

## T-018: 방치 보상 팝업 및 로비 UI

### 목표
게임 시작 시 오프라인 보상을 표시하고, 전투 진입 전 로비 화면을 구현한다.

### 작업 내용

#### 1. IdleRewardPopup
```
경로: Client/Assets/_Project/Scripts/UI/IdleRewardPopup.cs
```
```csharp
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using Cysharp.Threading.Tasks;

public class IdleRewardPopup : MonoBehaviour
{
    [SerializeField] private GameObject _panel;
    [SerializeField] private TextMeshProUGUI _timeText;
    [SerializeField] private TextMeshProUGUI _goldText;
    [SerializeField] private TextMeshProUGUI _expText;
    [SerializeField] private Button _claimButton;       // 1배 수령
    [SerializeField] private Button _doubleButton;      // 2배 수령 (광고)

    private IdleReward _reward;
    private System.Action<int, int> _onClaim; // (gold, exp)

    public void Show(IdleReward reward, System.Action<int, int> onClaim)
    {
        _reward = reward;
        _onClaim = onClaim;

        _timeText.text = $"{reward.ElapsedMinutes / 60}시간 {reward.ElapsedMinutes % 60}분 동안의 보상";
        _goldText.text = $"{reward.Gold:N0}";
        _expText.text = $"{reward.Exp:N0}";

        _panel.SetActive(true);
        _panel.transform.localScale = Vector3.zero;
        _panel.transform.DOScale(Vector3.one, 0.3f).SetEase(Ease.OutBack);

        _claimButton.onClick.RemoveAllListeners();
        _claimButton.onClick.AddListener(() => Claim(1));

        _doubleButton.onClick.RemoveAllListeners();
        _doubleButton.onClick.AddListener(() => Claim(2));
    }

    private void Claim(int multiplier)
    {
        int gold = _reward.Gold * multiplier;
        int exp = _reward.Exp * multiplier;

        _onClaim?.Invoke(gold, exp);

        _panel.transform.DOScale(Vector3.zero, 0.2f)
            .SetEase(Ease.InBack)
            .OnComplete(() => _panel.SetActive(false));
    }
}
```

#### 2. LobbyUI
```
경로: Client/Assets/_Project/Scripts/UI/LobbyUI.cs
```
```csharp
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class LobbyUI : MonoBehaviour
{
    [Header("Info Display")]
    [SerializeField] private TextMeshProUGUI _stageText;
    [SerializeField] private TextMeshProUGUI _energyText;
    [SerializeField] private TextMeshProUGUI _levelText;
    [SerializeField] private TextMeshProUGUI _goldText;

    [Header("Buttons")]
    [SerializeField] private Button _battleButton;
    [SerializeField] private Button _levelUpButton;

    [Header("Popup")]
    [SerializeField] private IdleRewardPopup _idlePopup;

    private HeroProgression _hero;
    private EnergySystem _energy;
    private StageManager _stageMgr;

    private void Start()
    {
        _hero = ServiceLocator.Get<HeroProgression>();
        _energy = ServiceLocator.Get<EnergySystem>();
        _stageMgr = ServiceLocator.Get<StageManager>();

        BindUI();
        CheckIdleReward();
    }

    private void BindUI()
    {
        UpdateDisplay();

        _hero.OnLevelUp += _ => UpdateDisplay();
        _hero.OnGoldChanged += _ => UpdateDisplay();
        _energy.OnEnergyChanged += (_, _) => UpdateDisplay();

        _battleButton.onClick.AddListener(OnBattleClicked);
        _levelUpButton.onClick.AddListener(OnLevelUpClicked);
    }

    private void UpdateDisplay()
    {
        _stageText.text = $"Stage {_stageMgr.CurrentStageNumber}";
        _energyText.text = $"{_energy.CurrentEnergy}/{EnergySystem.MAX_ENERGY}";
        _levelText.text = $"Lv.{_hero.Level}";
        _goldText.text = $"{_hero.Gold:N0}G";
    }

    private void OnBattleClicked()
    {
        if (!_energy.TryConsume())
        {
            // 에너지 부족 피드백 (텍스트 흔들기 등)
            _energyText.transform.DOShakePosition(0.3f, 5f);
            return;
        }
        SceneManager.LoadScene("Battle");
    }

    private void OnLevelUpClicked()
    {
        if (!_hero.TryLevelUpWithGold())
        {
            _goldText.transform.DOShakePosition(0.3f, 5f);
        }
    }

    private void CheckIdleReward()
    {
        // PlayerPrefs에서 마지막 로그인 시간 로드
        var lastLoginStr = PlayerPrefs.GetString("LastLoginUtc", "");
        if (string.IsNullOrEmpty(lastLoginStr))
        {
            SaveLoginTime();
            return;
        }

        var lastLogin = DateTime.Parse(lastLoginStr, null,
            System.Globalization.DateTimeStyles.RoundtripKind);
        var reward = IdleRewardCalculator.Calculate(lastLogin, _stageMgr.CurrentStageNumber);

        if (reward.HasValue)
        {
            _idlePopup.Show(reward.Value, (gold, exp) =>
            {
                _hero.AddGold(gold);
                _hero.AddExp(exp);
            });
        }

        SaveLoginTime();
    }

    private void SaveLoginTime()
    {
        PlayerPrefs.SetString("LastLoginUtc", DateTime.UtcNow.ToString("O"));
        PlayerPrefs.Save();
    }
}
```

#### 3. Lobby 씬 구성
- `Client/Assets/_Project/Scenes/Lobby.unity`
- Canvas (1080x1920, Scale With Screen Size)
  ```
  Canvas
  ├── TopBar
  │   ├── StageText
  │   ├── LevelText
  │   └── GoldText
  ├── CenterArea
  │   ├── HeroPreview (히어로 스프라이트)
  │   └── EnergyDisplay
  ├── BottomButtons
  │   ├── BattleButton
  │   └── LevelUpButton
  └── IdleRewardPopup (기본 비활성)
  ```

### 완료 조건
- [ ] 앱 시작 시 경과 시간 > 1분이면 방치 보상 팝업 표시
- [ ] 1배/2배 수령 버튼 동작, 골드/경험치 반영
- [ ] 에너지 부족 시 Battle 버튼 진입 차단 + 피드백
- [ ] 에너지 정상 차감 후 Battle 씬 로드
- [ ] 레벨업 버튼: 골드 부족 시 피드백, 성공 시 스탯 갱신

---

## T-019: DeadlockDetector 및 보드 셔플

### 목표
유효한 스왑이 없는 교착 상태를 감지하고 보드를 재배치한다.

### 작업 내용

#### DeadlockDetector
```
경로: Client/Assets/_Project/Scripts/Puzzle/DeadlockDetector.cs
```
```csharp
public class DeadlockDetector
{
    private Matcher _matcher;

    public DeadlockDetector(Matcher matcher)
    {
        _matcher = matcher;
    }

    /// <summary>
    /// 보드에 유효한 스왑이 하나라도 있는지 검사
    /// </summary>
    public bool HasValidMove(Board board)
    {
        // 모든 셀에 대해 우측/상단 스왑을 시뮬레이션
        for (int col = 0; col < board.Width; col++)
        {
            for (int row = 0; row < board.Height; row++)
            {
                // 우측 스왑 체크
                if (col < board.Width - 1)
                {
                    board.Swap(col, row, col + 1, row);
                    bool hasMatch = _matcher.FindMatches(board).Count > 0;
                    board.Swap(col, row, col + 1, row); // 원복
                    if (hasMatch) return true;
                }

                // 상단 스왑 체크
                if (row < board.Height - 1)
                {
                    board.Swap(col, row, col, row + 1);
                    bool hasMatch = _matcher.FindMatches(board).Count > 0;
                    board.Swap(col, row, col, row + 1); // 원복
                    if (hasMatch) return true;
                }
            }
        }
        return false;
    }

    /// <summary>
    /// 보드 셔플. 매치 없고 유효 스왑이 있을 때까지 반복.
    /// </summary>
    public void Shuffle(Board board)
    {
        var rand = new System.Random();
        int maxAttempts = 100;

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            // 기존 블록 수집
            var blocks = new List<BlockType>();
            for (int col = 0; col < board.Width; col++)
                for (int row = 0; row < board.Height; row++)
                    blocks.Add(board.GetBlock(col, row));

            // Fisher-Yates 셔플
            for (int i = blocks.Count - 1; i > 0; i--)
            {
                int j = rand.Next(i + 1);
                (blocks[i], blocks[j]) = (blocks[j], blocks[i]);
            }

            // 재배치
            int idx = 0;
            for (int col = 0; col < board.Width; col++)
                for (int row = 0; row < board.Height; row++)
                    board.SetBlock(col, row, blocks[idx++]);

            // 조건 검증: 매치 없고 유효 스왑 존재
            if (_matcher.FindMatches(board).Count == 0 && HasValidMove(board))
            {
                EventBus.Publish(new BoardShuffledEvent());
                return;
            }
        }

        // 최대 시도 초과 시 강제 재생성
        board.Initialize();
        EventBus.Publish(new BoardShuffledEvent());
    }
}

public struct BoardShuffledEvent { }
```

### 호출 시점
- `Board.TrySwapAsync()`의 캐스케이드 완료 후
- 리필 후 매치가 없고, `HasValidMove()`가 false이면 셔플 실행

```csharp
// Board.TrySwapAsync() 내 캐스케이드 루프 이후 추가
if (!_deadlockDetector.HasValidMove(this))
{
    _deadlockDetector.Shuffle(this);
    // 뷰에서 셔플 애니메이션 처리
}
```

### 완료 조건
- [ ] 유효 스왑이 없는 보드 상태를 정확히 감지
- [ ] 셔플 후 보드에 매치가 없고 유효 스왑이 존재
- [ ] BoardShuffledEvent 발행으로 뷰 애니메이션 트리거

---

## T-020: E2E 플로우 통합 및 스모크 테스트

### 목표
Boot → Lobby → Battle → 승리 → Lobby 복귀까지의 전체 게임 루프를 연결하고 검증한다.

### 작업 내용

#### 1. Boot 씬 초기화 플로우
```csharp
// GameManager.InitializeServices() 확장
private void InitializeServices()
{
    // 메타 시스템 초기화
    var hero = new HeroProgression();
    var energy = new EnergySystem();
    var stages = LoadStageDataList(); // Resources.LoadAll 또는 Addressables
    var stageMgr = new StageManager(stages);

    // 저장 데이터 복원
    LoadSaveData(hero, energy, stageMgr);

    // ServiceLocator 등록
    ServiceLocator.Register(hero);
    ServiceLocator.Register(energy);
    ServiceLocator.Register(stageMgr);

    // Lobby 씬 로드
    SceneManager.LoadScene("Lobby");
}
```

#### 2. Battle 씬 초기화
```csharp
// BattleSceneController.cs (Battle 씬의 루트)
public class BattleSceneController : MonoBehaviour
{
    private async UniTaskVoid Start()
    {
        var hero = ServiceLocator.Get<HeroProgression>();
        var stageMgr = ServiceLocator.Get<StageManager>();

        // 전투 상태 생성
        var heroState = hero.CreateBattleState();
        var enemyState = stageMgr.CreateEnemy();

        // 보드 초기화
        var board = new Board();
        board.Initialize();

        // 전투 시작
        var battleMgr = new BattleManager();
        battleMgr.StartBattle(heroState, enemyState);

        // 뷰 바인딩
        // ... PuzzleBoardView, BattleHUD 등 초기화

        // 전투 결과 대기
        battleMgr.OnBattleWin += () => OnBattleEnd(true);
        battleMgr.OnBattleLose += () => OnBattleEnd(false);
    }

    private void OnBattleEnd(bool isWin)
    {
        if (isWin)
        {
            var stageMgr = ServiceLocator.Get<StageManager>();
            var hero = ServiceLocator.Get<HeroProgression>();
            var reward = stageMgr.AdvanceStage();
            hero.AddGold(reward.Gold);
            hero.AddExp(reward.Exp);
        }

        // 결과 표시 후 Lobby 복귀
        SceneManager.LoadScene("Lobby");
    }
}
```

#### 3. 데이터 영속화 (PlayerPrefs 임시)
```csharp
// SaveManager.cs 또는 GameManager 내부
public static void SaveAll()
{
    var hero = ServiceLocator.Get<HeroProgression>();
    var energy = ServiceLocator.Get<EnergySystem>();
    var stageMgr = ServiceLocator.Get<StageManager>();

    var (level, exp, gold) = hero.SaveState();
    PlayerPrefs.SetInt("Hero_Level", level);
    PlayerPrefs.SetInt("Hero_Exp", exp);
    PlayerPrefs.SetInt("Hero_Gold", gold);

    var (e, lastRecovery) = energy.SaveState();
    PlayerPrefs.SetInt("Energy_Current", e);
    PlayerPrefs.SetString("Energy_LastRecovery", lastRecovery.ToString("O"));

    PlayerPrefs.SetInt("Stage_Index", stageMgr.CurrentStageNumber - 1);
    PlayerPrefs.SetString("LastLoginUtc", DateTime.UtcNow.ToString("O"));
    PlayerPrefs.Save();
}
```

#### 4. 스모크 테스트 체크리스트

**전체 플로우:**
- [ ] Boot → Lobby 씬 전환 정상
- [ ] Lobby → Battle 씬 전환 정상 (에너지 차감)
- [ ] Battle → 승리 → Lobby 복귀 (보상 적용)
- [ ] Battle → 패배 → Lobby 복귀 (보상 없음)

**퍼즐:**
- [ ] 7x6 보드 정상 표시
- [ ] 블록 드래그 스왑 동작
- [ ] 매치 3+ 감지 및 클리어
- [ ] 중력 + 리필 후 연쇄 매치 (캐스케이드) 동작
- [ ] 교착 상태 시 셔플 동작

**전투:**
- [ ] Red 매치 → 적 HP 감소 (콤보 배율 적용)
- [ ] Blue 매치 → 히어로 실드 생성
- [ ] Green 매치 → 히어로 HP 회복
- [ ] Yellow 매치 → 적 데미지 + 기절 (자동 공격 중단)
- [ ] 적 자동 공격 동작 (2초 간격)
- [ ] 궁극기 게이지 충전 → 만충 시 탭 → 대량 데미지

**메타:**
- [ ] 에너지 0에서 Battle 진입 차단
- [ ] 방치 보상 정상 계산 및 팝업 표시
- [ ] 전투 보상 → 경험치/골드 반영 → 레벨업 동작
- [ ] 데이터 저장 후 앱 재시작 시 복원

### 완료 조건
- [ ] 위 체크리스트 전항목 통과
- [ ] 이벤트 와이어링 누락 없음 (콘솔에 NullReference 없음)
- [ ] 메모리 누수 없음 (CancellationToken 정상 해제)
