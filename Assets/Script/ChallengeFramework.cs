using UnityEngine;

//  ChallengeFramework  (Aligned 100% to Proposal_Flow_Adaptive_Challenge_Skill_Revised_v3)
//  - §7.3 Reference Ranges: Speed 1-3, ATK 10-40, Spawn 0.20-0.80, HP 50-200
//  - §8.1 Challenge Score:  Cₜ = 0.20V + 0.30A + 0.20R + 0.30H
//  - §8.2 Skill Score:      Sₜ = 0.30Acc + 0.25Time + 0.30Survival + 0.15Death
//  - §8.4 CSBI:             CSBIₜ₊₁ = Cₜ₊₁ / (Sₜ + 0.001)
//  - §8.5 Algorithm 1:      C* = Clamp(1.05*Sₜ, 0.20, 0.90), ΔC = Clamp(C* - Cₜ, -0.15, +0.15), Cₜ₊₁ = Cₜ + ΔC
//  - §13.1 Balance Metrics: AEₜ = BEₜ = |Cₜ - Sₜ₋₁|, BRₜ = Cₜ / (Sₜ + 0.001), Balanced: 0.90 <= BRₜ <= 1.10
public static class ChallengeFramework
{
    public const float SpeedMin = 1.0f;   public const float SpeedMax = 3.0f;
    public const float AtkMin   = 10f;    public const float AtkMax   = 40f;
    public const float SpawnMin = 0.20f;  public const float SpawnMax = 0.80f;
    public const float HpMin    = 50f;    public const float HpMax    = 200f;

    public const float wSpeed = 0.20f;
    public const float wAtk   = 0.30f;
    public const float wSpawn = 0.20f;
    public const float wHp    = 0.30f;

    public const float Epsilon      = 0.001f;
    public const float BalancedLow  = 0.90f;
    public const float BalancedHigh = 1.10f;

    public const float SkillToChallengeMultiplier = 1.05f;
    public const float ChallengeFloor   = 0.20f;
    public const float ChallengeCeil    = 0.90f;
    public const float MaxChallengeStep = 0.15f;

    public const float Room1Challenge     = 0.50f;
    public const float NonAdaptiveRoom2   = 0.60f;
    public const float NonAdaptiveRoom3   = 0.70f;
    public const float BossFixedChallenge = 0.70f;

    public static float Normalize(float x, float min, float max)
    {
        if (Mathf.Approximately(max, min)) return 0f;
        return Mathf.Clamp01((x - min) / (max - min));
    }

    public static float NormalizeInverted(float x, float min, float max)
    {
        return Mathf.Clamp01(1f - Normalize(x, min, max));
    }

    public static float ChallengeScore(float enemySpeed, float enemyAtk, float spawnRate, float enemyHp)
    {
        float v = Normalize(enemySpeed, SpeedMin, SpeedMax);
        float a = Normalize(enemyAtk,   AtkMin,   AtkMax);
        float r = Normalize(spawnRate,  SpawnMin, SpawnMax);
        float h = Normalize(enemyHp,    HpMin,    HpMax);
        return Mathf.Clamp01(wSpeed * v + wAtk * a + wSpawn * r + wHp * h);
    }

    public static float MapToParam(float challenge, float pMin, float pMax)
    {
        return pMin + Mathf.Clamp01(challenge) * (pMax - pMin);
    }

    public static float EnemySpeedFor(float c)     => MapToParam(c, SpeedMin, SpeedMax);
    public static float EnemyAttackFor(float c)    => MapToParam(c, AtkMin,   AtkMax);
    public static float EnemySpawnRateFor(float c) => MapToParam(c, SpawnMin, SpawnMax);
    public static float EnemyHpFor(float c)        => MapToParam(c, HpMin,    HpMax);

    // §8.2: Sₜ = 0.30·Acc + 0.25·Time + 0.30·Survival + 0.15·Death
    public static float SkillScore(float accuracy, float clearTime, float damageTaken, int deathCount, SkillReferences refs)
    {
        float accPerf   = Normalize(accuracy, 0f, 1f);
        float timePerf  = NormalizeInverted(clearTime,   refs.clearTimeMin, refs.clearTimeMax);
        float dmgPerf   = NormalizeInverted(damageTaken, refs.damageMin,    refs.damageMax);
        float deathPerf = NormalizeInverted(deathCount,  refs.deathMin,     refs.deathMax);

        float s = refs.wAccuracy  * accPerf
                + refs.wClearTime * timePerf
                + refs.wDamage    * dmgPerf
                + refs.wDeath     * deathPerf;
        return Mathf.Clamp01(s);
    }

    public static float NextChallenge(float skillPrev, float challengeCurrent)
    {
        float target = Mathf.Clamp(SkillToChallengeMultiplier * skillPrev, ChallengeFloor, ChallengeCeil);
        float delta  = Mathf.Clamp(target - challengeCurrent, -MaxChallengeStep, MaxChallengeStep);
        return Mathf.Clamp(challengeCurrent + delta, ChallengeFloor, ChallengeCeil);
    }

    // §8.4: CSBIₜ₊₁ = Cₜ₊₁ / (Sₜ + ε)
    public static float CSBI(float challengeNext, float skillPrev)
    {
        return challengeNext / (skillPrev + Epsilon);
    }

    // §13.1: BRₜ = Cₜ / (Sₜ + ε)
    public static float BalanceRatio(float challengeCurrent, float skillCurrent)
    {
        return challengeCurrent / (skillCurrent + Epsilon);
    }

    // §8.5 & §13.1: AEₜ = BEₜ = |Cₜ - Sₜ₋₁|
    public static float AdaptationError(float challengeCurrent, float skillPrev)
    {
        return Mathf.Abs(challengeCurrent - skillPrev);
    }

    public static bool IsBalanced(float ratio)
    {
        return ratio >= BalancedLow && ratio <= BalancedHigh;
    }
}

[System.Serializable]
public class SkillReferences
{
    [Header("Reference ranges (min-max) — §8.3")]
    public float clearTimeMin = 20f;   public float clearTimeMax = 120f;
    public float damageMin    = 0f;    public float damageMax    = 300f;
    public int   deathMin     = 0;     public int   deathMax     = 5;

    [Header("Skill weights — Proposal v3 §8.2 (รวม = 1.00)")]
    public float wAccuracy  = 0.30f;
    public float wClearTime = 0.25f;
    public float wDamage    = 0.30f; // Survival
    public float wDeath     = 0.15f;
}
