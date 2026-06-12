namespace PhaseHeist;

/// <summary>
/// 平衡性参数集中表。调参只改这里。
/// </summary>
public static class Balance
{
    // 武器：DPS = Damage * FireRate = 80，TTK(100HP) ≈ 1.25s
    public const int WeaponDamage = 20;
    public const float WeaponFireRate = 4.0f;
    public const float WeaponRange = 80.0f;

    // 回合
    public const float NegotiationSeconds = 600.0f;   // 锁门倒计时 10 分钟
    public const float PoliceBreachUnlockRatio = 0.30f;

    // 行为冷却（防刷分/防骚扰）
    public const float NegotiateCooldownSeconds = 10.0f;
    public const float ShoutDownCooldownSeconds = 8.0f;

    // 评分
    public const int ScoreKillByPolice = 30;     // 警察击倒持枪劫匪
    public const int ScoreKillByRobber = 15;     // 劫匪击倒警察
    public const int ScoreArrest = 80;           // 逮捕（无枪劫匪）
    public const int ScoreNegotiate = 3;
    public const int ScoreLootTaken = 120;
    public const int ScoreEscapeSewer = 90;
    public const int ScoreEscapeSide = 70;
    public const int ScoreEscapeThroughBlock = 15;
    public const int ScoreHostageReleasedPolice = 12;
    public const int ScoreHostageReleasedRobber = 4;
    public const int ScoreHostageKilledPolicePenalty = 35;
    public const int ScoreHostageKilledRobberPenalty = 10;

    // 交互
    public const float InteractExtraReach = 0.6f; // 在交互点半径基础上的余量

    // 突入
    public const float EntryCoverRadius = 8.0f;   // 劫匪守门判定半径
}
