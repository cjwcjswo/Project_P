# Day 1 - Core Puzzle Logic 구현 가이드

## T-001: 프로젝트 스캐폴딩 및 패키지 설정

### 목표
Unity 프로젝트에 필수 패키지를 추가하고, 게임 진입점(Boot 씬)을 구성한다.

### 작업 내용

#### 1. UniTask 패키지 추가
`Client/Packages/manifest.json`의 `dependencies`에 추가:
```json
"com.cysharp.unitask": "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask"
```

#### 2. DOTween 패키지 추가
`Client/Packages/manifest.json`의 `dependencies`에 추가:
```json
"com.demigiant.dotween": "https://github.com/nicktho/DOTween-Unity-Package.git"
```
> 또는 Asset Store에서 DOTween Free를 직접 import하여 `Client/Assets/Plugins/DOTween/`에 배치.

#### 3. Boot 씬 생성
- `Client/Assets/_Project/Scenes/Boot.unity` 씬 생성
- 빈 GameObject `[GameManager]`를 생성하고 `GameManager.cs` 컴포넌트 부착
- Build Settings에서 Boot 씬을 Scene 0번으로 등록

#### 4. GameManager 기본 구조
```
경로: Client/Assets/_Project/Scripts/Core/GameManager.cs
```
```csharp
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        InitializeServices();
    }

    private void InitializeServices()
    {
        // ServiceLocator에 각 시스템 등록 (T-002 이후 채워짐)
    }
}
```

### 완료 조건
- [ ] Unity 에디터에서 패키지 매니저 > UniTask, DOTween 정상 인식
- [ ] Boot 씬 Play 시 GameManager 싱글톤 정상 생성

---

## T-002: Core 프레임워크 - EventBus 및 ServiceLocator

### 목표
시스템 간 느슨한 결합을 위한 이벤트 버스와 서비스 등록/조회 패턴을 구현한다.

### 작업 내용

#### 1. EventBus
```
경로: Client/Assets/_Project/Scripts/Core/EventBus.cs
```

제네릭 이벤트 시스템으로, 이벤트 타입(struct)을 키로 사용한다.

```csharp
using System;
using System.Collections.Generic;

public static class EventBus
{
    private static readonly Dictionary<Type, Delegate> _events = new();

    public static void Subscribe<T>(Action<T> handler) where T : struct
    {
        var type = typeof(T);
        if (_events.TryGetValue(type, out var existing))
            _events[type] = Delegate.Combine(existing, handler);
        else
            _events[type] = handler;
    }

    public static void Unsubscribe<T>(Action<T> handler) where T : struct
    {
        var type = typeof(T);
        if (_events.TryGetValue(type, out var existing))
        {
            var result = Delegate.Remove(existing, handler);
            if (result == null) _events.Remove(type);
            else _events[type] = result;
        }
    }

    public static void Publish<T>(T evt) where T : struct
    {
        if (_events.TryGetValue(typeof(T), out var handler))
            ((Action<T>)handler)?.Invoke(evt);
    }

    public static void Clear() => _events.Clear();
}
```

이벤트 구조체 예시 (향후 각 시스템에서 정의):
```csharp
public struct OnMatchFoundEvent
{
    public List<MatchResult> Matches;
    public int ComboStep;
}
```

#### 2. ServiceLocator
```
경로: Client/Assets/_Project/Scripts/Core/ServiceLocator.cs
```

```csharp
using System;
using System.Collections.Generic;

public static class ServiceLocator
{
    private static readonly Dictionary<Type, object> _services = new();

    public static void Register<T>(T service) where T : class
        => _services[typeof(T)] = service;

    public static T Get<T>() where T : class
        => _services.TryGetValue(typeof(T), out var svc) ? (T)svc : null;

    public static void Clear() => _services.Clear();
}
```

### 설계 원칙
- EventBus: **fire-and-forget 브로드캐스트** (1:N). 발신자가 수신자를 몰라도 됨
- ServiceLocator: **직접 참조** (1:1). 특정 시스템 인스턴스에 직접 접근 필요 시 사용
- 둘 다 **순수 C# static 클래스** — MonoBehaviour 의존 없음

### 완료 조건
- [ ] EventBus로 구조체 이벤트 publish/subscribe 동작 확인
- [ ] ServiceLocator에 임의 클래스 register/get 동작 확인

---

## T-003: Board 데이터 모델 및 블록 스폰

### 목표
7x6 퍼즐 보드의 데이터 구조를 정의하고, 초기 매치 없는 랜덤 보드를 생성한다.

### 작업 내용

#### 1. Constants 정의
```
경로: Client/Assets/_Project/Scripts/Core/Constants.cs
```
```csharp
public static class Constants
{
    public const int BOARD_WIDTH = 7;
    public const int BOARD_HEIGHT = 6;
    public const int BLOCK_TYPE_COUNT = 4;
    public const int MIN_MATCH = 3;
}
```

#### 2. BlockType Enum
```
경로: Client/Assets/_Project/Scripts/Puzzle/BlockType.cs
```
```csharp
public enum BlockType
{
    None = 0,   // 빈 셀
    Red,        // 공격
    Blue,       // 방어
    Green,      // 회복
    Yellow      // 유틸 (CC)
}
```

#### 3. Board 클래스
```
경로: Client/Assets/_Project/Scripts/Puzzle/Board.cs
```

핵심 데이터 구조:
```csharp
public class Board
{
    private BlockType[,] _grid; // [col, row] — col: 0~6(좌→우), row: 0~5(하→상)

    public int Width => Constants.BOARD_WIDTH;
    public int Height => Constants.BOARD_HEIGHT;

    public Board()
    {
        _grid = new BlockType[Width, Height];
    }

    public BlockType GetBlock(int col, int row) => _grid[col, row];
    public void SetBlock(int col, int row, BlockType type) => _grid[col, row] = type;
}
```

#### 4. 초기 보드 생성 (매치 없는 스폰)
```csharp
public void Initialize()
{
    var rand = new System.Random();
    var types = new[] { BlockType.Red, BlockType.Blue, BlockType.Green, BlockType.Yellow };

    for (int col = 0; col < Width; col++)
    {
        for (int row = 0; row < Height; row++)
        {
            BlockType selected;
            do
            {
                selected = types[rand.Next(types.Length)];
            }
            while (WouldCauseMatch(col, row, selected));

            _grid[col, row] = selected;
        }
    }
}
```

`WouldCauseMatch` 로직:
- 왼쪽 2칸이 같은 타입이면 가로 매치 발생 → 해당 타입 제외
- 아래쪽 2칸이 같은 타입이면 세로 매치 발생 → 해당 타입 제외
- 이미 배치된 칸만 검사 (col 또는 row가 2 미만이면 검사 생략)

### 좌표계 규칙
```
row 5  [ ][ ][ ][ ][ ][ ][ ]   ← 화면 상단 (보드 최상단)
row 4  [ ][ ][ ][ ][ ][ ][ ]
row 3  [ ][ ][ ][ ][ ][ ][ ]
row 2  [ ][ ][ ][ ][ ][ ][ ]
row 1  [ ][ ][ ][ ][ ][ ][ ]
row 0  [ ][ ][ ][ ][ ][ ][ ]   ← 화면 하단 (보드 최하단)
       c0 c1 c2 c3 c4 c5 c6
```
> row 0이 바닥이므로 중력은 row 값이 감소하는 방향.

### 완료 조건
- [ ] Board 인스턴스 생성 후 Initialize() 호출 시 42칸 모두 None이 아닌 블록으로 채워짐
- [ ] 생성된 보드에 3연속 매치가 존재하지 않음 (Matcher로 검증 — T-004)

---

## T-004: 매치 감지 알고리즘

### 목표
보드 전체를 스캔하여 3개 이상 연속된 동일 블록 그룹을 찾는다.

### 작업 내용

#### 1. MatchResult 구조체
```
경로: Client/Assets/_Project/Scripts/Puzzle/Matcher.cs 내부 또는 별도 파일
```
```csharp
public struct MatchResult
{
    public BlockType Type;
    public List<(int col, int row)> Positions;  // 매치에 포함된 모든 셀 좌표
}
```

#### 2. Matcher 클래스
```
경로: Client/Assets/_Project/Scripts/Puzzle/Matcher.cs
```

알고리즘 (2-패스 스캔):

**가로 스캔:**
```
for each row (0 ~ Height-1):
    streak 시작, col=0부터 순회
    현재 블록과 이전 블록이 같으면 streak 연장
    다르거나 열 끝이면:
        streak.length >= 3이면 매치 등록
        새 streak 시작
```

**세로 스캔:**
```
for each col (0 ~ Width-1):
    동일 로직을 row 방향으로 수행
```

**병합 처리:**
- 가로/세로 매치가 셀을 공유할 수 있음 (L자, T자, +자 형태)
- 같은 BlockType이고 교집합이 있는 매치들은 하나의 MatchResult로 합침
- Union-Find 또는 단순 HashSet 병합으로 구현

```csharp
public class Matcher
{
    public List<MatchResult> FindMatches(Board board)
    {
        var allMatches = new List<HashSet<(int, int)>>();

        // 가로 스캔
        FindDirectionalMatches(board, allMatches, horizontal: true);
        // 세로 스캔
        FindDirectionalMatches(board, allMatches, horizontal: false);
        // 병합
        return MergeOverlapping(board, allMatches);
    }
}
```

#### 3. 메서드 시그니처
```csharp
// 전체 매치 찾기
public List<MatchResult> FindMatches(Board board);

// 특정 위치 주변만 검사 (스왑 후 최적화용, 선택사항)
public List<MatchResult> FindMatchesAround(Board board, int col, int row);
```

### 주의사항
- `BlockType.None`은 매치 대상에서 제외
- 4연속, 5연속도 하나의 매치로 처리 (특수 블록 확장 시 활용 가능)

### 완료 조건
- [ ] 가로 3연속 매치 정상 감지
- [ ] 세로 3연속 매치 정상 감지
- [ ] L자/T자 형태 병합 정상 동작
- [ ] 초기 보드(T-003)에서 FindMatches 결과가 0개인지 검증

---

## T-005: 중력 및 리필 로직

### 목표
매치된 블록을 제거한 후, 빈 공간으로 블록을 낙하시키고 새 블록을 채운다.

### 작업 내용

#### 1. 블록 제거
```csharp
// Board 클래스에 추가
public void ClearBlocks(List<(int col, int row)> positions)
{
    foreach (var (col, row) in positions)
        _grid[col, row] = BlockType.None;
}
```

#### 2. 중력 처리 (열 단위 낙하)

각 열을 아래(row 0)부터 위로 스캔하여 빈 칸 위의 블록을 아래로 이동:

```csharp
public struct BlockMove
{
    public int Col;
    public int FromRow;
    public int ToRow;
    public BlockType Type;
    public bool IsNew;  // 새로 스폰된 블록 여부
}

public List<BlockMove> ApplyGravity()
{
    var moves = new List<BlockMove>();

    for (int col = 0; col < Width; col++)
    {
        int writeRow = 0;
        for (int readRow = 0; readRow < Height; readRow++)
        {
            if (_grid[col, readRow] != BlockType.None)
            {
                if (readRow != writeRow)
                {
                    moves.Add(new BlockMove
                    {
                        Col = col, FromRow = readRow, ToRow = writeRow,
                        Type = _grid[col, readRow], IsNew = false
                    });
                    _grid[col, writeRow] = _grid[col, readRow];
                    _grid[col, readRow] = BlockType.None;
                }
                writeRow++;
            }
        }
    }
    return moves;
}
```

#### 3. 리필 (빈 칸에 새 블록 스폰)
```csharp
public List<BlockMove> Refill()
{
    var moves = new List<BlockMove>();
    var rand = new System.Random();
    var types = new[] { BlockType.Red, BlockType.Blue, BlockType.Green, BlockType.Yellow };

    for (int col = 0; col < Width; col++)
    {
        int spawnOffset = 0;
        for (int row = 0; row < Height; row++)
        {
            if (_grid[col, row] == BlockType.None)
            {
                var type = types[rand.Next(types.Length)];
                _grid[col, row] = type;
                moves.Add(new BlockMove
                {
                    Col = col,
                    FromRow = Height + spawnOffset,  // 화면 위에서 떨어지는 연출용
                    ToRow = row,
                    Type = type,
                    IsNew = true
                });
                spawnOffset++;
            }
        }
    }
    return moves;
}
```

### 뷰 레이어 연동 포인트
- `ApplyGravity()`와 `Refill()`이 반환하는 `List<BlockMove>`를 뷰에서 받아 DOTween 시퀀스로 애니메이션
- `FromRow`→`ToRow`로 이동하는 거리에 비례한 낙하 시간 계산

### 완료 조건
- [ ] 매치 클리어 후 빈 칸 위의 블록이 아래로 정상 이동
- [ ] 빈 칸이 새 랜덤 블록으로 채워짐
- [ ] BlockMove 리스트에 올바른 이동 정보 포함

---

## T-006: 스왑 입력 및 캐스케이드 루프

### 목표
유저의 블록 스왑 → 매치 확인 → 클리어 → 중력 → 리필 → 재매치 확인 전체 루프를 구현한다.

### 작업 내용

#### 1. 이벤트 구조체 정의
```csharp
// Events.cs 또는 각 파일에 분산 정의
public struct SwapEvent
{
    public int Col1, Row1, Col2, Row2;
    public bool IsValid; // 매치 성공 여부
}

public struct MatchFoundEvent
{
    public List<MatchResult> Matches;
    public int ComboStep;
}

public struct CascadeCompleteEvent
{
    public int TotalCombo;
    public List<MatchResult> AllMatches; // 전체 캐스케이드 동안의 모든 매치
}

public struct BoardStabilizedEvent { }
```

#### 2. 스왑 로직
```csharp
// Board 클래스에 추가
public bool IsAdjacent(int c1, int r1, int c2, int r2)
{
    int dc = Math.Abs(c1 - c2);
    int dr = Math.Abs(r1 - r2);
    return (dc + dr) == 1; // 상하좌우 1칸만 허용, 대각선 불가
}

public void Swap(int c1, int r1, int c2, int r2)
{
    (_grid[c1, r1], _grid[c2, r2]) = (_grid[c2, r2], _grid[c1, r1]);
}
```

#### 3. 캐스케이드 루프 (UniTask async)
```csharp
// BoardController.cs 또는 Board.cs에 위치
public async UniTask<CascadeResult> TrySwapAsync(int c1, int r1, int c2, int r2)
{
    if (!IsAdjacent(c1, r1, c2, r2)) return CascadeResult.Invalid;

    // 1. 스왑 실행
    Swap(c1, r1, c2, r2);
    EventBus.Publish(new SwapEvent { Col1=c1, Row1=r1, Col2=c2, Row2=r2, IsValid=true });
    await UniTask.Delay(TimeSpan.FromSeconds(0.2f)); // 스왑 애니메이션 대기

    // 2. 매치 확인
    var matches = _matcher.FindMatches(this);
    if (matches.Count == 0)
    {
        // 무효 스왑 — 원위치
        Swap(c1, r1, c2, r2);
        EventBus.Publish(new SwapEvent { Col1=c1, Row1=r1, Col2=c2, Row2=r2, IsValid=false });
        await UniTask.Delay(TimeSpan.FromSeconds(0.2f));
        return CascadeResult.Invalid;
    }

    // 3. 캐스케이드 루프
    var allMatches = new List<MatchResult>();
    int combo = 0;

    while (matches.Count > 0)
    {
        combo++;
        allMatches.AddRange(matches);

        // 매치된 블록 좌표 수집
        var clearPositions = matches.SelectMany(m => m.Positions).Distinct().ToList();

        EventBus.Publish(new MatchFoundEvent { Matches = matches, ComboStep = combo });

        // 클리어
        ClearBlocks(clearPositions);
        await UniTask.Delay(TimeSpan.FromSeconds(0.3f)); // 파괴 애니메이션 대기

        // 중력
        var gravityMoves = ApplyGravity();
        var refillMoves = Refill();
        // 뷰에서 애니메이션 처리하도록 이벤트 발행
        await UniTask.Delay(TimeSpan.FromSeconds(0.3f)); // 낙하 애니메이션 대기

        // 재매치 확인
        matches = _matcher.FindMatches(this);
    }

    EventBus.Publish(new CascadeCompleteEvent
    {
        TotalCombo = combo,
        AllMatches = allMatches
    });

    return new CascadeResult { Combo = combo, Matches = allMatches };
}
```

### 타이밍 상수 (조절 가능)
| 동작 | 시간 | 설명 |
|------|------|------|
| 스왑 애니메이션 | 0.2s | 두 블록 위치 교환 |
| 파괴 애니메이션 | 0.3s | 매치된 블록 소멸 |
| 낙하 애니메이션 | 0.3s | 중력 + 리필 |

> 실제 시간은 뷰 레이어에서 DOTween 완료 콜백으로 제어하는 것이 이상적. 초기에는 고정 딜레이 사용.

### 완료 조건
- [ ] 인접 블록 스왑 시 매치가 없으면 원위치
- [ ] 매치 발견 시 클리어 → 중력 → 리필 정상 동작
- [ ] 연쇄 매치(캐스케이드) 발생 시 combo 증가 확인
- [ ] CascadeCompleteEvent에 전체 매치 결과 포함

---

## T-007: PuzzleBoardView 및 BlockView (비주얼 레이어)

### 목표
Board 로직의 시각적 표현과 유저 입력 처리를 담당하는 MonoBehaviour 뷰를 구현한다.

### 작업 내용

#### 1. BlockView (개별 블록 뷰)
```
경로: Client/Assets/_Project/Scripts/UI/BlockView.cs
```
```csharp
public class BlockView : MonoBehaviour
{
    [SerializeField] private SpriteRenderer _spriteRenderer;

    private BlockType _type;
    public int Col { get; private set; }
    public int Row { get; private set; }

    // 블록 타입별 색상 (임시 — 나중에 스프라이트로 교체)
    private static readonly Dictionary<BlockType, Color> BlockColors = new()
    {
        { BlockType.Red,    Color.red },
        { BlockType.Blue,   Color.blue },
        { BlockType.Green,  Color.green },
        { BlockType.Yellow, Color.yellow }
    };

    public void Setup(BlockType type, int col, int row)
    {
        _type = type;
        Col = col;
        Row = row;
        _spriteRenderer.color = BlockColors.GetValueOrDefault(type, Color.white);
    }

    public void UpdatePosition(int col, int row)
    {
        Col = col;
        Row = row;
    }
}
```

#### 2. 블록 애니메이션 (DOTween)
```csharp
// BlockView 내부 메서드
public UniTask AnimateMoveTo(Vector3 targetPos, float duration = 0.3f)
{
    return transform.DOMove(targetPos, duration)
        .SetEase(Ease.OutBounce)
        .ToUniTask();
}

public UniTask AnimateDestroy(float duration = 0.2f)
{
    return DOTween.Sequence()
        .Append(transform.DOScale(Vector3.zero, duration).SetEase(Ease.InBack))
        .AppendCallback(() => gameObject.SetActive(false))
        .ToUniTask();
}

public UniTask AnimateSpawn(Vector3 fromPos, Vector3 toPos, float duration = 0.3f)
{
    transform.position = fromPos;
    transform.localScale = Vector3.one;
    return transform.DOMove(toPos, duration)
        .SetEase(Ease.OutBounce)
        .ToUniTask();
}
```

#### 3. PuzzleBoardView (보드 뷰)
```
경로: Client/Assets/_Project/Scripts/UI/PuzzleBoardView.cs
```

**레이아웃 계산:**
- 화면 하단 50% 영역에 7x6 그리드 배치
- 셀 크기: 화면 너비 / 7 (정사각형)
- 보드 원점: 화면 좌하단에서 오프셋

```csharp
public class PuzzleBoardView : MonoBehaviour
{
    [SerializeField] private BlockView _blockPrefab;

    private BlockView[,] _blockViews;
    private Board _board;
    private float _cellSize;
    private Vector3 _boardOrigin;
    private bool _isProcessing; // 캐스케이드 중 입력 차단

    public void Initialize(Board board) { /* ... */ }

    public Vector3 GridToWorld(int col, int row)
    {
        return _boardOrigin + new Vector3(col * _cellSize, row * _cellSize, 0);
    }
}
```

#### 4. 입력 처리 (드래그 스왑)
```csharp
// PuzzleBoardView 내부
private Vector2 _dragStart;
private (int col, int row)? _selectedCell;

// Input은 Unity의 New Input System 또는 간단한 마우스/터치로 처리
// Update() 대신 UniTask 기반 입력 루프 권장

private async UniTaskVoid InputLoop(CancellationToken ct)
{
    while (!ct.IsCancellationRequested)
    {
        await UniTask.WaitUntil(() => Input.GetMouseButtonDown(0), cancellationToken: ct);
        if (_isProcessing) continue;

        var startPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        _selectedCell = WorldToGrid(startPos);
        if (_selectedCell == null) continue;

        await UniTask.WaitUntil(() => Input.GetMouseButtonUp(0), cancellationToken: ct);

        var endPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        var dir = GetSwapDirection(startPos, endPos);
        if (dir == null) continue;

        var (dc, dr) = dir.Value;
        var (sc, sr) = _selectedCell.Value;
        int tc = sc + dc, tr = sr + dr;

        if (tc < 0 || tc >= _board.Width || tr < 0 || tr >= _board.Height) continue;

        _isProcessing = true;
        await _board.TrySwapAsync(sc, sr, tc, tr);
        _isProcessing = false;
    }
}
```

**드래그 방향 판정:**
```csharp
private (int dc, int dr)? GetSwapDirection(Vector2 start, Vector2 end)
{
    var delta = end - start;
    if (delta.magnitude < _cellSize * 0.3f) return null; // 최소 드래그 거리

    if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
        return delta.x > 0 ? (1, 0) : (-1, 0);
    else
        return delta.y > 0 ? (0, 1) : (0, -1);
}
```

#### 5. 이벤트 구독 (Board ↔ View 연결)
```csharp
private void OnEnable()
{
    EventBus.Subscribe<SwapEvent>(OnSwap);
    EventBus.Subscribe<MatchFoundEvent>(OnMatchFound);
    EventBus.Subscribe<CascadeCompleteEvent>(OnCascadeComplete);
}

private void OnDisable()
{
    EventBus.Unsubscribe<SwapEvent>(OnSwap);
    EventBus.Unsubscribe<MatchFoundEvent>(OnMatchFound);
    EventBus.Unsubscribe<CascadeCompleteEvent>(OnCascadeComplete);
}
```

#### 6. Block 프리팹 구성
- `Client/Assets/_Project/Prefabs/Puzzle/Block.prefab`
- 구성: SpriteRenderer (Square 스프라이트) + BlockView 컴포넌트
- Sorting Layer: "Puzzle" (새로 생성)

### 완료 조건
- [ ] 화면 하단 50%에 7x6 그리드 정상 표시
- [ ] 드래그로 블록 스왑 가능
- [ ] 매치 시 블록 소멸 → 낙하 → 리필 애니메이션 동작
- [ ] 캐스케이드 중 추가 입력 차단
