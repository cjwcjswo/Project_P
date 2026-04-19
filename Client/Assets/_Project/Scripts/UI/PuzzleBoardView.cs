using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// PuzzleBoardView: Board 데이터 모델의 시각적 표현 + 유저 입력 처리.
/// 화면 하단 50% 영역에 7x6 그리드로 BlockView를 배치.
/// EventBus를 구독하여 스왑/매치/캐스케이드 이벤트에 반응.
/// </summary>
public class PuzzleBoardView : MonoBehaviour
{
    [SerializeField] private BlockView _blockPrefab;

    private BlockView[,] _blockViews;
    private BoardController _controller;
    private Board _board;
    private HeroParty _party;

    // 그리드 셀 "센터 간격"(월드 유닛). 블록 간 간격(보이는 gap)과 분리해서 관리한다.
    private float _cellStep;
    // 블록이 차지할 목표 월드 사이즈(월드 유닛). _cellStep - _blockGapWorld 로 산출.
    private float _blockWorldSize;
    private Vector3 _boardOrigin;
    private bool _isProcessing;

    private CancellationTokenSource _cts;

    private InputAction _pointerPress;
    private InputAction _pointerPosition;

    private readonly Queue<BlockView> _blockPool = new();

    [Header("Layout (World Units)")]
    [Tooltip("보드 배치를 제한할 화면 세로 비율. 0.5면 하단 50% 영역을 사용.")]
    [Range(0.1f, 1f)]
    [SerializeField] private float _verticalAreaRatio = 0.5f;

    [Tooltip("보드 바깥 여백(월드 유닛). 하단 50% 영역 내부에서만 적용된다.")]
    [SerializeField] private float _paddingLeft = 0.2f;
    [SerializeField] private float _paddingRight = 0.2f;
    [SerializeField] private float _paddingTop = 0.2f;
    [SerializeField] private float _paddingBottom = 0.2f;

    [Tooltip("셀(step) 대비 블록 사이에 남길 간격(월드 유닛). 값이 작을수록 블록이 더 촘촘해진다.")]
    [Min(0f)]
    [SerializeField] private float _blockGapWorld = 0.05f;

    // ── 초기화 ────────────────────────────────────────────────────────────

    public void Initialize(BoardController controller, HeroParty party = null)
    {
        _controller = controller;
        _board      = controller.Board;
        _party      = party;

        CalculateLayout();
        SpawnAllBlocks();

        _cts = new CancellationTokenSource();
        InputLoop(_cts.Token).Forget();
    }

    /// <summary>
    /// 화면 하단 일부 영역(기본 50%)에 맞게 보드 레이아웃을 계산한다.
    /// - padding(상/좌/우/하)은 해당 영역 내부에서만 적용된다.
    /// - 셀 센터 간격(_cellStep)과 블록 실제 크기(_blockWorldSize)를 분리하여 블록 간 "보이는 간격"을 제어한다.
    /// </summary>
    private void CalculateLayout()
    {
        var cam = Camera.main;
        float screenHeight = cam.orthographicSize * 2f;
        float screenWidth  = screenHeight * cam.aspect;

        float areaHeight = screenHeight * Mathf.Clamp01(_verticalAreaRatio);
        float areaWidth  = screenWidth;

        float screenBottom = cam.transform.position.y - cam.orthographicSize;
        float screenLeft   = cam.transform.position.x - screenWidth * 0.5f;

        float availableWidth  = Mathf.Max(0.01f, areaWidth  - (_paddingLeft + _paddingRight));
        float availableHeight = Mathf.Max(0.01f, areaHeight - (_paddingTop  + _paddingBottom));

        _cellStep = Mathf.Min(
            availableWidth  / Constants.BOARD_WIDTH,
            availableHeight / Constants.BOARD_HEIGHT
        );

        _blockWorldSize = Mathf.Max(0.01f, _cellStep - _blockGapWorld);

        float boardWidth  = _cellStep * Constants.BOARD_WIDTH;
        float boardHeight = _cellStep * Constants.BOARD_HEIGHT;

        float areaLeft   = screenLeft + _paddingLeft;
        float areaBottom = screenBottom + _paddingBottom;

        _boardOrigin = new Vector3(
            areaLeft + (availableWidth - boardWidth) * 0.5f + _cellStep * 0.5f,
            areaBottom + _cellStep * 0.5f,
            0f
        );
    }

    private BlockView GetBlockFromPool(Vector3 worldPos)
    {
        if (_blockPrefab == null)
        {
            Debug.LogError("[PuzzleBoardView] _blockPrefab must be assigned.");
            return null;
        }

        if (_blockPool.Count > 0)
        {
            var pooled = _blockPool.Dequeue();
            pooled.transform.DOKill();
            pooled.transform.position = worldPos;
            return pooled;
        }

        return Instantiate(_blockPrefab, worldPos, Quaternion.identity, transform);
    }

    private void ReturnToPool(BlockView view)
    {
        if (view == null) return;
        view.transform.DOKill();
        view.gameObject.SetActive(false);
        _blockPool.Enqueue(view);
    }

    /// <summary>
    /// (col,row)에 해당 타입 블록 뷰를 둔다. 이미 뷰가 있으면 재사용만 하고, 없으면 풀/생성한다.
    /// </summary>
    private BlockView EnsureBlockViewAt(int col, int row, BlockType type, Vector3 worldPos)
    {
        var current = _blockViews[col, row];
        if (current != null)
        {
            current.Setup(type, col, row);
            current.transform.position = worldPos;
            current.SetWorldSize(_blockWorldSize);
            return current;
        }

        var view = GetBlockFromPool(worldPos);
        view.Setup(type, col, row);
        view.SetWorldSize(_blockWorldSize);
        return view;
    }

    private void SpawnAllBlocks()
    {
        _blockViews = new BlockView[_board.Width, _board.Height];
        for (int col = 0; col < _board.Width; col++)
        {
            for (int row = 0; row < _board.Height; row++)
            {
                var type = _board.GetBlock(col, row);
                var pos  = GridToWorld(col, row);
                var view = EnsureBlockViewAt(col, row, type, pos);
                _blockViews[col, row] = view;
                ApplyClassIcon(view, type);
            }
        }
    }

    /// <summary>블록 타입에 해당하는 히어로의 직업 아이콘을 BlockView에 적용.</summary>
    private void ApplyClassIcon(BlockView view, BlockType type)
    {
        if (_party == null || view == null) return;
        var hero = _party.GetHeroByColor(type);
        view.SetClassIcon(hero?.HeroClass ?? HeroClass.None);
    }

    // ── 좌표 변환 ─────────────────────────────────────────────────────────

    public Vector3 GridToWorld(int col, int row)
        => _boardOrigin + new Vector3(col * _cellStep, row * _cellStep, 0f);

    private (int col, int row)? WorldToGrid(Vector3 worldPos)
    {
        float localX = worldPos.x - (_boardOrigin.x - _cellStep * 0.5f);
        float localY = worldPos.y - (_boardOrigin.y - _cellStep * 0.5f);

        int col = Mathf.FloorToInt(localX / _cellStep);
        int row = Mathf.FloorToInt(localY / _cellStep);

        if (col < 0 || col >= _board.Width || row < 0 || row >= _board.Height)
            return null;
        return (col, row);
    }

    // ── 입력 처리 ─────────────────────────────────────────────────────────

    private async UniTaskVoid InputLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await UniTask.WaitUntil(() => _pointerPress.WasPressedThisFrame(), cancellationToken: ct);

            if (_isProcessing) continue;

            var startScreen = _pointerPosition.ReadValue<Vector2>();
            var startWorld  = Camera.main.ScreenToWorldPoint(new Vector3(startScreen.x, startScreen.y, 0f));
            var selected    = WorldToGrid(startWorld);
            if (selected == null) continue;

            var (sc, sr) = selected.Value;

            // 특수 블록(스킬 블록) 탭 감지: 스왑 없이 즉시 발동
            if (_board.IsSkillBlock(sc, sr))
            {
                await UniTask.WaitUntil(() => _pointerPress.WasReleasedThisFrame(), cancellationToken: ct);
                var releaseScreen = _pointerPosition.ReadValue<Vector2>();
                var releaseWorld  = Camera.main.ScreenToWorldPoint(new Vector3(releaseScreen.x, releaseScreen.y, 0f));
                // 드래그가 아닌 탭(이동 거리 작음)이면 발동
                if (Vector3.Distance(startWorld, releaseWorld) < _cellStep * 0.3f)
                {
                    var color = _board.GetBlock(sc, sr);
                    EventBus.Publish(new SkillBlockTappedEvent { Color = color, Col = sc, Row = sr });
                    if (_blockViews[sc, sr] != null)
                        _blockViews[sc, sr].SetSkillBlock(false);
                }
                continue;
            }

            await UniTask.WaitUntil(() => _pointerPress.WasReleasedThisFrame(), cancellationToken: ct);

            var endScreen = _pointerPosition.ReadValue<Vector2>();
            var endWorld  = Camera.main.ScreenToWorldPoint(new Vector3(endScreen.x, endScreen.y, 0f));
            var dir       = GetSwapDirection(startWorld, endWorld);
            if (dir == null) continue;

            var (dc, dr) = dir.Value;
            int tc = sc + dc, tr = sr + dr;

            if (tc < 0 || tc >= _board.Width || tr < 0 || tr >= _board.Height) continue;

            _isProcessing = true;
            await _controller.TrySwapAsync(sc, sr, tc, tr);
            _isProcessing = false;
        }
    }

    private (int dc, int dr)? GetSwapDirection(Vector2 start, Vector2 end)
    {
        var delta = end - start;
        if (delta.magnitude < _cellStep * 0.3f) return null;

        if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
            return delta.x > 0 ? (1, 0) : (-1, 0);
        else
            return delta.y > 0 ? (0, 1) : (0, -1);
    }

    // ── EventBus 구독 ─────────────────────────────────────────────────────

    private void OnEnable()
    {
        _pointerPress    = new InputAction("PointerPress",    binding: "<Pointer>/press");
        _pointerPosition = new InputAction("PointerPosition", binding: "<Pointer>/position");
        _pointerPress.Enable();
        _pointerPosition.Enable();

        EventBus.Subscribe<SwapEvent>(OnSwap);
        EventBus.Subscribe<MatchFoundEvent>(OnMatchFound);
        EventBus.Subscribe<GravityRefillEvent>(OnGravityRefill);
        EventBus.Subscribe<CascadeCompleteEvent>(OnCascadeComplete);
        EventBus.Subscribe<HeroColorRemovedEvent>(OnHeroColorRemoved);
        EventBus.Subscribe<HeroColorDisabledEvent>(OnHeroColorDisabled);
        EventBus.Subscribe<BoardReshuffleEvent>(OnBoardReshuffle);
        EventBus.Subscribe<SkillBlockCreatedEvent>(OnSkillBlockCreated);
    }

    private void OnDisable()
    {
        _pointerPress?.Disable();
        _pointerPress?.Dispose();
        _pointerPosition?.Disable();
        _pointerPosition?.Dispose();

        EventBus.Unsubscribe<SwapEvent>(OnSwap);
        EventBus.Unsubscribe<MatchFoundEvent>(OnMatchFound);
        EventBus.Unsubscribe<GravityRefillEvent>(OnGravityRefill);
        EventBus.Unsubscribe<CascadeCompleteEvent>(OnCascadeComplete);
        EventBus.Unsubscribe<HeroColorRemovedEvent>(OnHeroColorRemoved);
        EventBus.Unsubscribe<HeroColorDisabledEvent>(OnHeroColorDisabled);
        EventBus.Unsubscribe<BoardReshuffleEvent>(OnBoardReshuffle);
        EventBus.Unsubscribe<SkillBlockCreatedEvent>(OnSkillBlockCreated);

        _cts?.Cancel();
        _cts?.Dispose();
    }

    // ── 이벤트 핸들러 ─────────────────────────────────────────────────────

    private void OnSwap(SwapEvent evt)
    {
        var ease = evt.IsValid ? DG.Tweening.Ease.OutBounce : DG.Tweening.Ease.InOutSine;

        var viewA = _blockViews[evt.Col1, evt.Row1];
        var viewB = _blockViews[evt.Col2, evt.Row2];
        if (viewA == null || viewB == null) return;

        var posA = GridToWorld(evt.Col1, evt.Row1);
        var posB = GridToWorld(evt.Col2, evt.Row2);

        viewA.AnimateMoveTo(posB, Constants.SWAP_ANIM_DURATION, ease).Forget();
        viewB.AnimateMoveTo(posA, Constants.SWAP_ANIM_DURATION, ease).Forget();

        viewA.UpdatePosition(evt.Col2, evt.Row2);
        viewB.UpdatePosition(evt.Col1, evt.Row1);

        _blockViews[evt.Col2, evt.Row2] = viewA;
        _blockViews[evt.Col1, evt.Row1] = viewB;
    }

    private void OnMatchFound(MatchFoundEvent evt)
    {
        foreach (var match in evt.Matches)
        {
            foreach (var (col, row) in match.Positions)
            {
                var view = _blockViews[col, row];
                if (view != null)
                {
                    var captured = view;
                    view.AnimateDestroy(Constants.DESTROY_ANIM_DURATION,
                        () => ReturnToPool(captured)).Forget();
                    _blockViews[col, row] = null;
                }
            }
        }
    }

    private void OnGravityRefill(GravityRefillEvent evt)
    {
        foreach (var move in evt.GravityMoves)
        {
            var view = _blockViews[move.Col, move.FromRow];
            if (view == null) continue;

            var targetPos = GridToWorld(move.Col, move.ToRow);
            view.AnimateMoveTo(targetPos, Constants.FALL_ANIM_DURATION).Forget();
            view.UpdatePosition(move.Col, move.ToRow);

            _blockViews[move.Col, move.ToRow]   = view;
            _blockViews[move.Col, move.FromRow]  = null;
        }

        foreach (var move in evt.RefillMoves)
        {
            var fromPos = GridToWorld(move.Col, move.FromRow);
            var toPos   = GridToWorld(move.Col, move.ToRow);
            var view    = EnsureBlockViewAt(move.Col, move.ToRow, move.Type, fromPos);
            view.AnimateSpawn(fromPos, toPos, Constants.FALL_ANIM_DURATION).Forget();
            _blockViews[move.Col, move.ToRow] = view;
        }
    }

    private void OnCascadeComplete(CascadeCompleteEvent evt)
    {
        Debug.Log($"[PuzzleBoardView] Cascade Complete — Combo: {evt.TotalCombo}, Matches: {evt.AllMatches.Count}");
    }

    private void OnHeroColorRemoved(HeroColorRemovedEvent evt)
    {
        foreach (var (col, row) in evt.Positions)
        {
            var view = _blockViews[col, row];
            if (view == null) continue;

            var captured = view;
            view.AnimateDestroy(Constants.DESTROY_ANIM_DURATION,
                () => ReturnToPool(captured)).Forget();
            _blockViews[col, row] = null;
        }
    }

    private void OnHeroColorDisabled(HeroColorDisabledEvent evt)
    {
        foreach (var (col, row) in evt.Positions)
        {
            var view = _blockViews[col, row];
            if (view == null) continue;

            view.Setup(BlockType.Disabled, col, row);
            view.SetWorldSize(_blockWorldSize);
        }
    }

    private void OnBoardReshuffle(BoardReshuffleEvent evt)
    {
        foreach (var (col, row) in evt.UpdatedPositions)
        {
            var view = _blockViews[col, row];
            if (view == null) continue;

            var type = _board.GetBlock(col, row);
            view.Setup(type, col, row);
            view.SetWorldSize(_blockWorldSize);
            view.AnimateReshuffle().Forget();
        }
    }

    private void OnSkillBlockCreated(SkillBlockCreatedEvent evt)
    {
        var view = _blockViews[evt.Col, evt.Row];
        view?.SetSkillBlock(true);
    }
}
