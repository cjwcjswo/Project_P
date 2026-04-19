var builder = WebApplication.CreateBuilder(args);

// Unity JsonUtility는 PascalCase 필드명을 사용하므로 서버 응답도 PascalCase로 통일.
// System.Text.Json 기본 정책(null)은 PropertyName 그대로 출력 = PascalCase 유지.

var app = builder.Build();

// ── 인메모리 스텁 데이터 ──────────────────────────────────────────────────

// 유저별 에너지 DB
var userEnergyDb = new Dictionary<string, int>
{
    ["local_default"] = 20
};

// 유저별 클리어 스테이지 ID 목록
var userClearedStagesDb = new Dictionary<string, List<int>>
{
    ["local_default"] = new List<int>()
};

// 유저별 소유/배치 히어로 목록 (HeroId = HeroData.Id)
var userDb = new Dictionary<string, UserProfileDto>
{
    ["local_default"] = new(
        "local_default",
        new List<int> { 10001, 10002, 10003, 10004, 10005 },
        new List<DeployedHeroDto> {
            new(10001, 1), new(10002, 1), new(10003, 1), new(10004, 1), new(10005, 1)
        }
    )
};

// ── Minimal API 엔드포인트 ────────────────────────────────────────────────

app.MapGet("/api/user/{userId}/gamedata", (string userId) =>
{
    if (!userDb.TryGetValue(userId, out var user))
        return Results.NotFound(new { Error = $"User '{userId}' not found" });

    var energy        = userEnergyDb.TryGetValue(userId, out var e) ? e : 20;
    var clearedStages = userClearedStagesDb.TryGetValue(userId, out var cs) ? cs : new List<int>();

    var response = new UserGameDataResponse(
        user.UserId,
        user.OwnedHeroIds,
        user.DeployedHeroes,
        energy,
        clearedStages
    );
    return Results.Ok(response);
});

// ── T-D4-004: 에너지 소모 스텁 ───────────────────────────────────────────
app.MapPost("/api/user/energy/consume", (EnergyConsumeRequest req) =>
{
    if (!userDb.ContainsKey(req.UserId))
        return Results.NotFound(new { error = $"User '{req.UserId}' not found" });

    if (!userEnergyDb.TryGetValue(req.UserId, out var current))
        current = 20;

    if (current < req.Amount)
        return Results.BadRequest(new { error = "에너지가 부족합니다.", remainingEnergy = current });

    userEnergyDb[req.UserId] = current - req.Amount;
    return Results.Ok(new { remainingEnergy = userEnergyDb[req.UserId] });
});

// ── 스테이지 클리어 이력 저장 스텁 ───────────────────────────────────────
app.MapPost("/api/stage/clear", (StageClearRequest req) =>
{
    if (!userDb.ContainsKey(req.UserId))
        return Results.NotFound(new { error = $"User '{req.UserId}' not found" });

    if (!userClearedStagesDb.TryGetValue(req.UserId, out var cleared))
    {
        cleared = new List<int>();
        userClearedStagesDb[req.UserId] = cleared;
    }

    if (!cleared.Contains(req.StageId))
        cleared.Add(req.StageId);

    return Results.Ok(new { clearedStageIds = cleared });
});

// ── T-D4-005: 전투 완료 보상 스텁 ────────────────────────────────────────
app.MapPost("/api/battle/complete", (BattleCompleteRequest req) =>
{
    if (!req.IsCleared)
        return Results.Ok(new BattleCompleteResponse(new List<HeroExpGain>(), 0));

    // 스테이지별 보상 테이블 (스텁)
    var rewardTable = new Dictionary<int, (int TotalExp, int Gold)>
    {
        [1] = (1200, 300),
        [2] = (1800, 450),
    };

    if (!rewardTable.TryGetValue(req.StageId, out var reward))
        reward = (1000, 200);

    int expPerHero = req.DeployedHeroCount > 0
        ? (int)Math.Floor((double)reward.TotalExp / req.DeployedHeroCount)
        : 0;

    var heroGains = req.SurvivedHeroIds
        .Select(id => new HeroExpGain(id, expPerHero))
        .ToList();

    return Results.Ok(new BattleCompleteResponse(heroGains, reward.Gold));
});

// ── T-D7-010: 히어로/스킬 데이터 API 스텁 (Day 7 업데이트) ──────────────

// 인메모리 HeroData (HeroData.json과 동일 스키마 — Class/IllustrationPath/3종 스킬 ID)
var heroDataDb = new List<HeroMasterDto>
{
    new(10001, "견습 기사 아서", "Heroes/Knight_Arthur",  "Warrior",  "Illustrations/Arthur", 3,
        5001, 5006, 5009,
        new StatDto(1000, 100, 20), new StatDto(200, 15, 5), 1.5f, 0.1f),
    new(10002, "성녀 에이다",    "Heroes/Healer_Ada",     "Healer",   "Illustrations/Ada",    2,
        5002, 5007, 0,
        new StatDto(800,  70, 15),  new StatDto(150, 10, 4), 2.0f, 0.08f),
    new(10003, "수호자 브리트",  "Heroes/Guardian_Brit",  "Warrior",  "Illustrations/Brit",   3,
        5003, 5008, 5010,
        new StatDto(1200, 80, 35),  new StatDto(250, 10, 8), 2.0f, 0.1f),
    new(10004, "전사 크롬",      "Heroes/Warrior_Crom",   "Warrior",  "",                     1,
        5004, 0,    0,
        new StatDto(900, 120, 18),  new StatDto(180, 18, 4), 1.8f, 0.12f),
    new(10005, "암살자 레이",    "Heroes/Assassin_Ray",   "Assassin", "Illustrations/Ray",    3,
        5005, 5006, 5009,
        new StatDto(700, 150, 10),  new StatDto(130, 22, 3), 1.2f, 0.15f),
};

// 인메모리 SkillData (SkillData.json과 동일 스키마 — 5006~5010 신규 추가)
var skillDataDb = new List<SkillMasterDto>
{
    new(5001, "회전 베기",   "Effects/Skills/SpinSlash",  "Attack", "Multi",  3, 1.5f,
        new List<SkillEffectDto> {
            new("Stun",    0.0f, 1.5f, 0.3f),
            new("DefDown", 0.2f, 5.0f, 1.0f) }),
    new(5002, "힐링 라이트", "Effects/Skills/HealLight",  "Heal",   "All",    0, 2.0f,
        new List<SkillEffectDto>()),
    new(5003, "수호의 방패", "Effects/Skills/Shield",     "Shield", "All",    0, 1.8f,
        new List<SkillEffectDto>()),
    new(5004, "전투 함성",   "Effects/Skills/Buff",       "Buff",   "All",    0, 0.3f,
        new List<SkillEffectDto> {
            new("AtkUp", 0.2f, 8.0f, 1.0f) }),
    new(5005, "정의의 일격", "Effects/Skills/HeavyStrike","Attack", "Single", 1, 2.5f,
        new List<SkillEffectDto>()),
    // ── Day 7: UniqueSkill / UltimateSkill ──
    new(5006, "심판의 강타",   "Effects/Skills/JudgmentStrike",  "Attack", "Single", 1, 3.5f,
        new List<SkillEffectDto> {
            new("Stun", 0.0f, 2.0f, 0.5f) }),
    new(5007, "성스러운 빛",   "Effects/Skills/HolyLight",       "Heal",   "All",    0, 1.5f,
        new List<SkillEffectDto>()),
    new(5008, "절대 방어",     "Effects/Skills/AbsoluteShield",  "Shield", "All",    0, 2.5f,
        new List<SkillEffectDto>()),
    new(5009, "신의 철퇴",     "Effects/Skills/DivineMace",      "Attack", "All",    0, 4.0f,
        new List<SkillEffectDto> {
            new("DefDown", 0.3f, 8.0f, 1.0f) }),
    new(5010, "불굴의 성벽",   "Effects/Skills/FortressWall",    "Buff",   "All",    0, 0.5f,
        new List<SkillEffectDto> {
            new("AtkUp", 0.3f, 10.0f, 1.0f) }),
};

app.MapGet("/api/heroes", () => Results.Ok(heroDataDb));

app.MapGet("/api/skills", () => Results.Ok(skillDataDb));

app.MapPost("/api/hero/levelup", (HeroLevelUpRequest req) =>
{
    var hero = heroDataDb.FirstOrDefault(h => h.Id == req.HeroDataId);
    if (hero == null)
        return Results.NotFound(new { error = $"HeroDataId={req.HeroDataId} not found" });

    // 간단 레벨업 계산 (실제 레벨 관리는 UserDataManager에서 담당)
    int newLevel = Math.Clamp(req.CurrentLevel + 1, 1, 50);
    var newStats = new StatDto(
        hero.BaseStats.MaxHP    + (newLevel - 1) * hero.GrowthStats.MaxHP,
        hero.BaseStats.Attack   + (newLevel - 1) * hero.GrowthStats.Attack,
        hero.BaseStats.Defense  + (newLevel - 1) * hero.GrowthStats.Defense
    );
    return Results.Ok(new HeroLevelUpResponse(req.HeroDataId, newLevel, newStats));
});

app.MapGet("/", () => "ProjectP Server API is running.");

app.Run();

// ── DTO Records ───────────────────────────────────────────────────────────

/// <summary>배치 히어로: HeroId(= HeroData.Id) + Level만 보관. 상세 스탯은 클라이언트가 Repository에서 조회.</summary>
record DeployedHeroDto(int HeroId, int Level);

record UserProfileDto(
    string UserId,
    List<int> OwnedHeroIds,
    List<DeployedHeroDto> DeployedHeroes
);

record UserGameDataResponse(
    string UserId,
    List<int> OwnedHeroIds,
    List<DeployedHeroDto> DeployedHeroes,
    int Energy,
    List<int> ClearedStageIds
);

record EnergyConsumeRequest(string UserId, int Amount);

record StageClearRequest(string UserId, int StageId);

record BattleCompleteRequest(
    int StageId,
    int DeployedHeroCount,
    List<int> SurvivedHeroIds,
    bool IsCleared
);

record HeroExpGain(int HeroId, int Exp);

record BattleCompleteResponse(
    List<HeroExpGain> HeroExpGains,
    int GoldGained
);

// ── T-D6-010 / T-D7-010 Records ─────────────────────────────────────────

record StatDto(int MaxHP, int Attack, int Defense);

record SkillEffectDto(string EffectType, float Value, float Duration, float Probability);

record SkillMasterDto(
    int Id,
    string DisplayName,
    string EffectPrefabPath,
    string ActionType,
    string TargetScope,
    int MaxTargetCount,
    float BaseMultiplier,
    List<SkillEffectDto> StatusEffects
);

record HeroMasterDto(
    int Id,
    string DisplayName,
    string PrefabPath,
    string Class,
    string IllustrationPath,
    int Grade,
    int MatchSkillId,
    int UniqueSkillId,
    int UltimateSkillId,
    StatDto BaseStats,
    StatDto GrowthStats,
    float AutoAttackInterval,
    float AutoAttackRatio
);

record HeroLevelUpRequest(int HeroDataId, int CurrentLevel, string UserId);

record HeroLevelUpResponse(int HeroDataId, int NewLevel, StatDto NewStats);
