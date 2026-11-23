using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-200)]
public class DDAManager : MonoBehaviour
{
    public static DDAManager Instance;

    [Header("Skill model references (§8.2 / §8.3)")]
    public SkillReferences skillRefs = new SkillReferences();

    [Header("Boss (Fixed Challenge — §9.1, §18)")]
    [Range(0f, 1f)] public float bossFixedChallenge = ChallengeFramework.BossFixedChallenge;

    [Header("Runtime (read-only)")]
    public float currentChallenge = ChallengeFramework.Room1Challenge;
    public float currentSkill;
    public float currentCSBI;
    public float currentBalanceRatio;
    public float currentAdaptationError;
    public float lastComputedNextC = float.NaN;
    public int   currentRoomIndex = 1;
    public bool  currentIsBoss;

    private bool  hasPrevSkill;
    private float prevSkill;
    private float pendingChallenge;
    private bool  hasPending;
    private string lastBegunScene;

    public float EnemySpeed       { get; private set; }
    public float EnemyAttackPower { get; private set; }
    public float EnemySpawnRate   { get; private set; }
    public float EnemyHp          { get; private set; }

    private ExperimentCondition Condition =>
        GameplayTelemetry.Instance != null ? GameplayTelemetry.Instance.condition
                                           : ExperimentCondition.Adaptive;

    private void Awake()
    {
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
        else { Destroy(gameObject); return; }

        EnsureTelemetry();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void Start()
    {
        AutoBeginFromScene(SceneManager.GetActiveScene().name);
    }

    private void OnDestroy()
    {
        if (Instance == this) SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        AutoBeginFromScene(scene.name);
    }

    private void AutoBeginFromScene(string sceneName)
    {
        if (sceneName == lastBegunScene) return;
        if (!TryParseRoom(sceneName, out int idx, out bool isBoss)) return;
        BeginRoom(idx, isBoss);
    }

    private static bool TryParseRoom(string sceneName, out int idx, out bool isBoss)
    {
        idx = 1; isBoss = false;
        if (string.IsNullOrEmpty(sceneName)) return false;
        string lower = sceneName.ToLowerInvariant();
        if (lower.Contains("boss")) { isBoss = true; return true; }
        if (lower.Contains("room"))
        {
            Match m = Regex.Match(sceneName, @"(\d+)");
            if (m.Success) { idx = int.Parse(m.Value); return true; }
        }
        return false;
    }

    private void EnsureTelemetry()
    {
        if (GameplayTelemetry.Instance == null)
            new GameObject("GameplayTelemetry (auto)").AddComponent<GameplayTelemetry>();
        if (FirebaseUploader.Instance == null)
            new GameObject("FirebaseUploader (auto)").AddComponent<FirebaseUploader>();
        if (GoogleSheetUploader.Instance == null)
            new GameObject("GoogleSheetUploader (auto)").AddComponent<GoogleSheetUploader>();
    }

    public void BeginRoom(int roomIndex, bool isBoss)
    {
        lastBegunScene   = SceneManager.GetActiveScene().name;
        currentRoomIndex = roomIndex;
        currentIsBoss    = isBoss;

        if (roomIndex <= 1 && !isBoss)
        {
            hasPrevSkill = false;
            hasPending   = false;
            lastComputedNextC = float.NaN;
        }

        currentChallenge = ResolveChallenge(roomIndex, isBoss);

        // Algorithm 1 Step 7: Pj,t+1 = pmin,j + Ct+1 * (pmax,j - pmin,j)
        EnemySpeed       = ChallengeFramework.EnemySpeedFor(currentChallenge);
        EnemyAttackPower = ChallengeFramework.EnemyAttackFor(currentChallenge);
        EnemySpawnRate   = ChallengeFramework.EnemySpawnRateFor(currentChallenge);
        EnemyHp          = ChallengeFramework.EnemyHpFor(currentChallenge);

        RoomTelemetry row = null;
        if (GameplayTelemetry.Instance != null)
        {
            row = GameplayTelemetry.Instance.BeginRoom(
                RoomIdOf(roomIndex, isBoss), isBoss ? 4 : roomIndex, currentChallenge,
                EnemySpeed, EnemyAttackPower, EnemySpawnRate, EnemyHp);
        }

        // §8.4 CSBIₜ₊₁ = Cₜ₊₁ / (Sₜ + ε) และ §8.5/§13.1 AEₜ = BEₜ = |Cₜ - Sₜ₋₁|
        if (hasPrevSkill && !isBoss)
        {
            currentCSBI = ChallengeFramework.CSBI(currentChallenge, prevSkill);
            currentAdaptationError = ChallengeFramework.AdaptationError(currentChallenge, prevSkill);
            if (row != null)
            {
                row.csbi         = currentCSBI;
                row.balanceError = currentAdaptationError;
            }
        }
        else
        {
            currentCSBI = float.NaN;
            currentAdaptationError = float.NaN;
            if (row != null)
            {
                row.csbi         = float.NaN;
                row.balanceError = float.NaN;
            }
        }

        Debug.Log($"[DDA] {RoomIdOf(roomIndex, isBoss)} | Cond={Condition} | C={currentChallenge:F2} | " +
                  $"Spd={EnemySpeed:F2} ATK={EnemyAttackPower:F0} Rate={EnemySpawnRate:F2}/s HP={EnemyHp:F0} | " +
                  $"CSBI={currentCSBI:F2} AE/BE={currentAdaptationError:F2}");

        if (GameplayTelemetry.Instance != null)
            GameplayTelemetry.Instance.UploadToFirebase();
    }

    private float ResolveChallenge(int roomIndex, bool isBoss)
    {
        if (isBoss) return bossFixedChallenge;
        if (roomIndex <= 1) return ChallengeFramework.Room1Challenge;

        if (Condition == ExperimentCondition.NonAdaptive)
        {
            if (roomIndex == 2) return ChallengeFramework.NonAdaptiveRoom2;
            return ChallengeFramework.NonAdaptiveRoom3;
        }

        if (hasPending) return Mathf.Clamp(pendingChallenge, ChallengeFramework.ChallengeFloor, ChallengeFramework.ChallengeCeil);
        return ChallengeFramework.Room1Challenge;
    }

    public void EndRoom(bool cleared)
    {
        var tel = GameplayTelemetry.Instance;
        RoomTelemetry row = tel != null ? tel.Current : null;
        if (tel != null) tel.EndRoom(cleared);

        float accuracy    = row != null ? row.Accuracy    : 0f;
        float clearTime   = row != null ? row.clearTime   : 0f;
        float damageTaken = row != null ? row.damageTaken : 0f;
        int   deathCount  = row != null ? row.deathCount  : 0;

        // Algorithm 1 Step 1: St <- CalculateSkillScore(Tt)
        float skill = ChallengeFramework.SkillScore(accuracy, clearTime, damageTaken, deathCount, skillRefs);
        currentSkill = skill;

        // §13.1 Balance Ratio: BRt = Ct / (St + ε) และ Balanced State (0.90 <= BRt <= 1.10)
        currentBalanceRatio = ChallengeFramework.BalanceRatio(currentChallenge, skill);
        bool balanced = ChallengeFramework.IsBalanced(currentBalanceRatio);

        // Algorithm 1 Steps 2-6: คำนวณ Ct+1 (ทั้ง C2 หลัง Room 1 และ C3 หลัง Room 2 ไว้วิเคราะห์การเปลี่ยนแปลง)
        if (!currentIsBoss)
        {
            float nextC = ChallengeFramework.NextChallenge(skill, currentChallenge);
            lastComputedNextC = nextC;
            if (Condition == ExperimentCondition.Adaptive)
            {
                pendingChallenge = nextC;
                hasPending = true;
            }
            if (row != null) row.nextChallenge = nextC;
            Debug.Log($"[DDA] End {RoomIdOf(currentRoomIndex, false)} | S={skill:F2} | BR={currentBalanceRatio:F2} (Balanced={balanced}) -> Next C={nextC:F2}");
        }
        else
        {
            if (row != null) row.nextChallenge = float.NaN;
            Debug.Log($"[DDA] End Boss | Cleared={cleared} | S={skill:F2} | BR={currentBalanceRatio:F2}");
        }

        if (row != null)
        {
            row.skillScore       = skill;
            row.balanceRatio     = currentBalanceRatio;
            row.inBalancedRegion = balanced;
        }

        prevSkill    = skill;
        hasPrevSkill = true;

        if (GameplayTelemetry.Instance != null)
            GameplayTelemetry.Instance.UploadToFirebase();

        if (GoogleSheetUploader.Instance != null && row != null)
            GoogleSheetUploader.Instance.SendRoom(row);
    }

    public void EndSession(bool bossCleared)
    {
        EndRoom(bossCleared);
        if (GameplayTelemetry.Instance != null && GameplayTelemetry.Instance.autoExportOnSessionEnd)
            GameplayTelemetry.Instance.ExportCsv();
    }

    private static string RoomIdOf(int idx, bool isBoss) => isBoss ? "Boss" : "R" + idx;
}
