# Day 4 — Stage & Monster System

> GDD `스테이지 및 몬스터 시스템 v1.0` 기반의 데이터 주도 스테이지 진행 흐름 전체 구현.  
> MonsterData/StageData JSON 로드 → 스테이지 선택 UI → 에너지 시스템 → 전투 보상 → 게임 플로우 오케스트레이션.

---

## 의존성 맵

```
T-D4-001 (MonsterData) ──┐
T-D4-002 (StageData)  ───┼──▶ T-D4-003 (BattleSetupData) ──┐
T-D4-004 (Energy)     ───┤                                   │
                         └──▶ T-D4-006 (StageSelectUI)       │
T-D4-005 (Rewards) ──────────▶ T-D4-007 (RewardUI)           │
                                                              └──▶ T-D4-008 (GameFlow)
```

---

## T-D4-001 MonsterData.json 생성 및 MonsterDataRepository 구현
**Priority:** High | **Status:** Pending

### 작업 내용
- `Client/Assets/_Project/Resources/MonsterData.json` 신규 작성
  - GDD 2.1 스키마 준수: Id, DisplayName, PrefabPath, MaxHP, Attack, SkillCooldown, SkillCastTime, SkillDamageMultiplier, AutoAttackInterval, AutoAttackRatio
  - 샘플 몬스터 2종: 슬라임(1001), 화염 오크(2005)
- `Client/Assets/_Project/Scripts/Data/MonsterDataRepository.cs` 신규 작성
  - `Resources.Load<TextAsset>("MonsterData")`로 JSON 로드
  - `Dictionary<int, MonsterData>` 캐시
  - `MonsterData GetById(int id)`
  - `IReadOnlyList<MonsterData> GetAll()`

### 수정 파일
- `Client/Assets/_Project/Resources/MonsterData.json` _(신규)_
- `Client/Assets/_Project/Scripts/Data/MonsterDataRepository.cs` _(신규)_

---

## T-D4-002 StageData.json 생성 및 StageDataRepository 구현
**Priority:** High | **Status:** Pending

### 작업 내용
- `Client/Assets/_Project/Resources/StageData.json` 신규 작성
  - GDD 2.2 스키마 준수: StageId, StageName, EntryCost, Waves[], ClearRewards
  - 샘플 스테이지: 1-1 고블린 숲(2웨이브), 1-2 어둠의 동굴(2웨이브)
- `Client/Assets/_Project/Scripts/Data/StageDataRepository.cs` 신규 작성
  - `StageData GetById(int stageId)`
  - `IReadOnlyList<StageData> GetAll()`

### 수정 파일
- `Client/Assets/_Project/Resources/StageData.json` _(신규)_
- `Client/Assets/_Project/Scripts/Data/StageDataRepository.cs` _(신규)_

---

## T-D4-003 BattleSetupData 모델 및 Stage 기반 BattleManager 초기화
**Priority:** High | **Status:** Pending  
**Dependencies:** T-D4-001, T-D4-002

### 작업 내용
- `BattleSetupData.cs` 신규 작성
  - 생성자: `BattleSetupData(StageData stage, MonsterDataRepository repo)`
  - `EnemyWave[] Waves` 프로퍼티: MonsterIds → EnemyState 배열 변환
  - GDD 규칙 적용: 인덱스 0 = 전방 탱커, PrefabPath로 스프라이트 로드
- `BattleManager.StartBattle(HeroParty party, BattleSetupData setup)` 시그니처 변경
  - 기존 하드코딩된 적 데이터 제거

### 수정 파일
- `Client/Assets/_Project/Scripts/Battle/BattleSetupData.cs` _(신규)_
- `Client/Assets/_Project/Scripts/Battle/BattleManager.cs`

---

## T-D4-004 에너지 시스템 구현
**Priority:** High | **Status:** Pending

### 작업 내용
- `UserGameDataDTO.cs`에 `int Energy` 필드 추가, 기본값 20
- `GameManager.cs`
  - `bool TryConsumeEnergy(int cost)`: 부족 시 false 반환 (차감 없음), 충분하면 차감 후 true
  - 스테이지 진입 시 `EntryCost` 검사 수행
- `Server/Program.cs`에 스텁 추가
  ```
  POST /api/user/energy/consume
  Request:  { userId, amount }
  Response: { remainingEnergy }
  ```

### 수정 파일
- `Client/Assets/_Project/Scripts/Data/UserDataDTO.cs`
- `Client/Assets/_Project/Scripts/Core/GameManager.cs`
- `Server/Program.cs`

---

## T-D4-005 전투 보상 시스템 구현
**Priority:** High | **Status:** Pending  
**Dependencies:** T-D4-002

### 작업 내용
- `BattleResult.cs` 신규 작성
  ```csharp
  int StageId
  int DeployedHeroCount
  List<int> SurvivedHeroIds
  bool IsCleared
  ```
- `RewardCalculator.cs` 신규 작성
  - EXP 분배: `Mathf.FloorToInt(TotalExp / DeployedHeroCount)`
  - Gold: 계정 즉시 합산
- `BattleManager.cs`: 클리어 조건 달성 시 `BattleCompleteEvent(BattleResult)` 발행
- `Server/Program.cs`에 스텁 추가
  ```
  POST /api/battle/complete
  Request:  BattleResult
  Response: { heroExpGains: [{heroId, exp}], goldGained }
  ```

### 수정 파일
- `Client/Assets/_Project/Scripts/Battle/BattleResult.cs` _(신규)_
- `Client/Assets/_Project/Scripts/Battle/RewardCalculator.cs` _(신규)_
- `Client/Assets/_Project/Scripts/Battle/BattleManager.cs`
- `Server/Program.cs`

---

## T-D4-006 Stage Select UI 구현
**Priority:** Medium | **Status:** Pending  
**Dependencies:** T-D4-002, T-D4-004

### 작업 내용
- `StageSelectView.cs` 신규 작성
  - Awake에서 `StageDataRepository.GetAll()`로 목록 로드
  - ScrollView 내 `StageCellView` 프리팹 동적 생성
- `StageCellView.cs` 신규 작성
  - 표시 항목: StageName, 에너지 아이콘 + EntryCost, 웨이브 수, TotalExp / TotalGold
  - 클릭 처리:
    - 에너지 부족 → "에너지가 부족합니다" 팝업
    - 충분 → 에너지 차감 → `GameFlowManager.LoadBattleScene(stageId)`

### 수정 파일
- `Client/Assets/_Project/Scripts/UI/StageSelectView.cs` _(신규)_
- `Client/Assets/_Project/Scripts/UI/StageCellView.cs` _(신규)_

---

## T-D4-007 Battle Reward UI 구현
**Priority:** Medium | **Status:** Pending  
**Dependencies:** T-D4-005

### 작업 내용
- `BattleRewardView.cs` 신규 작성
  - `BattleCompleteEvent` 수신 시 오버레이 활성화
  - 히어로별 초상화 + 획득 EXP 목록 렌더링
  - 총 획득 골드 표시
  - 버튼 3종:
    - **다음 스테이지**: StageId+1 존재 여부 확인 후 진입 (없으면 비활성)
    - **재도전**: 동일 StageId로 재진입 (에너지 재차감)
    - **스테이지 선택으로**: StageSelectView로 복귀

### 수정 파일
- `Client/Assets/_Project/Scripts/UI/BattleRewardView.cs` _(신규)_

---

## T-D4-008 게임 플로우 오케스트레이션 (GameFlowManager)
**Priority:** Medium | **Status:** Pending  
**Dependencies:** T-D4-003, T-D4-006, T-D4-007

### 작업 내용
- `GameFlowManager.cs` 신규 작성
  - ServiceLocator에 등록
  - 씬 전환 상태 머신
    ```
    MainMenu → StageSelect → BattleScene → RewardScreen → StageSelect
    ```
  - `int CurrentStageId` 프로퍼티로 선택 스테이지 유지
  - `LoadBattleScene(int stageId)`: BattleSetupData 빌드 → BattleManager 전달
  - `LoadRewardScreen()`, `LoadStageSelect()` 전환 메서드
- `GameManager.cs`에서 `GameFlowManager` 초기화 및 ServiceLocator 등록 추가

### 수정 파일
- `Client/Assets/_Project/Scripts/Core/GameFlowManager.cs` _(신규)_
- `Client/Assets/_Project/Scripts/Core/GameManager.cs`

---

## 검증 방법

| # | 시나리오 | 기대 결과 |
|---|---------|---------|
| 1 | `MonsterDataRepository.GetById(1001)` 호출 | 슬라임 데이터 반환 (MaxHP=500, Attack=50) |
| 2 | StageSelect에서 1-1 선택 (에너지 충분) | 에너지 차감 후 배틀 씬 진입 |
| 3 | StageSelect에서 1-1 선택 (에너지 0) | "에너지 부족" 팝업, 진입 차단 |
| 4 | 전투 클리어 (히어로 2명 출전, TotalExp=1200) | 히어로당 EXP=600 표시 |
| 5 | RewardView "다음 스테이지" 버튼 클릭 | StageId 2로 에너지 차감 후 진입 |
| 6 | RewardView "스테이지 선택으로" 버튼 클릭 | StageSelectView로 복귀 |
