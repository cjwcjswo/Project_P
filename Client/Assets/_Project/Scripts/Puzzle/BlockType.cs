public enum BlockType
{
    None   = 0,
    Red    = 1,   // Index 0 — 최전방
    Yellow = 2,   // Index 1
    Green  = 3,   // Index 2
    Blue   = 4,   // Index 3
    Purple = 5,   // Index 4 — 최후방

    Disabled = 99 // 히어로 사망 시 비활성 장애물 (이동·매칭 불가, 중력 고정)
}
