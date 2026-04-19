using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// 개별 블록 하나의 시각적 표현.
/// 타입별 스프라이트(인스펙터 매핑) 적용, 스왑/낙하/파괴 DOTween 애니메이션 처리.
/// </summary>
public class BlockView : MonoBehaviour
{
    [SerializeField] private SpriteRenderer _spriteRenderer;

    [Tooltip("매핑이 없을 때 사용할 스프라이트(비워 두면 기존 렌더러 스프라이트 유지 + 색만 적용).")]
    [SerializeField] private Sprite _fallbackSprite;

    [Serializable]
    private class BlockSpriteEntry
    {
        public BlockType Type;
        public Sprite Sprite;
    }

    [Header("Visuals")]
    [Tooltip("BlockType별 스프라이트. 단일 Block 프리팹에서 타입만 바꿔 표현한다.")]
    [SerializeField] private List<BlockSpriteEntry> _spritesByType = new();

    private readonly Dictionary<BlockType, Sprite> _spriteByType = new();

    // 매핑/스프라이트 미지정 시에만 사용하는 임시 색상
    private static readonly Dictionary<BlockType, Color> BlockColors = new()
    {
        { BlockType.Red,      Color.red },
        { BlockType.Yellow,   Color.yellow },
        { BlockType.Green,    Color.green },
        { BlockType.Blue,     new Color(0.2f, 0.5f, 1f) },
        { BlockType.Purple,   new Color(0.6f, 0.2f, 0.9f) },
        { BlockType.Disabled, new Color(0.5f, 0.5f, 0.5f) }
    };

    public int Col { get; private set; }
    public int Row { get; private set; }
    public BlockType Type { get; private set; }

    private void Awake()
    {
        BuildSpriteCache();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        BuildSpriteCache();
    }
#endif

    private void BuildSpriteCache()
    {
        _spriteByType.Clear();
        foreach (var entry in _spritesByType)
        {
            if (entry == null || entry.Sprite == null) continue;
            if (_spriteByType.ContainsKey(entry.Type))
            {
                Debug.LogWarning($"[BlockView] Duplicate sprite mapping for {entry.Type}. Keeping the first entry.");
                continue;
            }

            _spriteByType[entry.Type] = entry.Sprite;
        }
    }

    public void Setup(BlockType type, int col, int row)
    {
        Type = type;
        Col  = col;
        Row  = row;
        transform.localScale = Vector3.one;
        ApplyVisualForType(type);
        gameObject.SetActive(true);
    }

    private void ApplyVisualForType(BlockType type)
    {
        if (_spriteRenderer == null) return;

        if (_spriteByType.TryGetValue(type, out var sprite) && sprite != null)
        {
            _spriteRenderer.sprite = sprite;
            _spriteRenderer.color  = Color.white;
            return;
        }

        if (_fallbackSprite != null)
            _spriteRenderer.sprite = _fallbackSprite;

        _spriteRenderer.color = BlockColors.GetValueOrDefault(type, Color.white);
    }

    /// <summary>
    /// 블록의 목표 월드 사이즈(가로/세로)를 맞추기 위해 스케일을 조정한다.
    /// 스프라이트가 없거나 크기 계산이 불가하면 스케일을 변경하지 않는다.
    /// </summary>
    public void SetWorldSize(float targetWorldSize)
    {
        if (targetWorldSize <= 0f) return;
        if (_spriteRenderer == null) return;
        if (_spriteRenderer.sprite == null) return;

        var localSize = _spriteRenderer.sprite.bounds.size;
        float baseSize = Mathf.Min(localSize.x, localSize.y);
        if (baseSize <= 0f) return;

        float scale = targetWorldSize / baseSize;
        transform.localScale = new Vector3(scale, scale, 1f);
    }

    public void UpdatePosition(int col, int row)
    {
        Col = col;
        Row  = row;
    }

    // ── 애니메이션 ────────────────────────────────────────────────────────

    /// <summary>targetPos로 이동 (스왑, 낙하 공용). ease 미지정 시 OutBounce 사용.</summary>
    public UniTask AnimateMoveTo(Vector3 targetPos, float duration = 0.3f, Ease ease = Ease.OutBounce)
    {
        return transform
            .DOMove(targetPos, duration)
            .SetEase(ease)
            .ToUniTask();
    }

    /// <summary>블록 소멸 — 스케일 0으로 수축 후 비활성화. onComplete는 풀 반환 등 후처리에 사용.</summary>
    public UniTask AnimateDestroy(float duration = 0.2f, Action onComplete = null)
    {
        return DOTween.Sequence()
            .Append(transform.DOScale(Vector3.zero, duration).SetEase(Ease.InBack))
            .AppendCallback(() =>
            {
                gameObject.SetActive(false);
                onComplete?.Invoke();
            })
            .ToUniTask();
    }

    /// <summary>교착 상태 해소용 재배치 — 수축 후 팽창으로 타입 변경을 시각적으로 표현.</summary>
    public UniTask AnimateReshuffle(float duration = 0.2f)
    {
        var targetScale = transform.localScale;
        return DOTween.Sequence()
            .Append(transform.DOScale(Vector3.zero, duration * 0.4f).SetEase(Ease.InBack))
            .Append(transform.DOScale(targetScale,  duration * 0.6f).SetEase(Ease.OutBack))
            .ToUniTask();
    }

    /// <summary>화면 위(fromPos)에서 toPos로 낙하하며 등장.</summary>
    public UniTask AnimateSpawn(Vector3 fromPos, Vector3 toPos, float duration = 0.3f)
    {
        transform.position = fromPos;
        gameObject.SetActive(true);
        return transform
            .DOMove(toPos, duration)
            .SetEase(Ease.OutBounce)
            .ToUniTask();
    }

    private void Reset()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
    }
}
