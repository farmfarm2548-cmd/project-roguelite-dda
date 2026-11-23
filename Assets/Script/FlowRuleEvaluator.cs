using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

//  - ไม่แทรกแซง Adaptive Algorithm (§8.5) — ใช้เก็บข้อมูลเสริมเพื่อ Validate กับ FSS (§13.2)
//  - จำแนก 4 ช่อง: FLOW / ANXIETY / BOREDOM / APATHY จาก BalanceRatio + พฤติกรรมจริง
public class FlowRuleEvaluator : MonoBehaviour
{
    public static FlowRuleEvaluator Instance;

    public string CurrentFlowState { get; private set; } = "ASSESSING";
    public float RuleFlowScore { get; private set; } = 0.5f;
    public float IdleRatio { get; private set; } = 0f;
    public float LowHpClutchSec { get; private set; } = 0f;

    private float totalRoomTime;
    private float idleTime;
    private Vector3 lastPlayerPos;
    private Transform playerTr;
    private Health playerHp;
    private float evalTimer;
    private static bool hooked;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoHook()
    {
        if (hooked) return;
        hooked = true;
        EnsureInstance();
        SceneManager.sceneLoaded += (_, __) => { EnsureInstance(); Instance?.ResetRoom(); };
    }

    private static void EnsureInstance()
    {
        if (Instance == null)
        {
            var go = new GameObject("FlowRuleEvaluator (auto)");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<FlowRuleEvaluator>();
        }
    }

    public void ResetRoom()
    {
        totalRoomTime = 0f;
        idleTime = 0f;
        LowHpClutchSec = 0f;
        CurrentFlowState = "ASSESSING";
        playerTr = null;
        playerHp = null;
    }

    void Update()
    {
        if (playerTr == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) { playerTr = p.transform; playerHp = p.GetComponent<Health>(); lastPlayerPos = playerTr.position; }
            return;
        }

        float dt = Time.deltaTime;
        totalRoomTime += dt;

        // วัดเวลายืนนิ่ง (IdleTime) และเวลาสู้ตอนเลือดต่ำ < 30% (Clutch Absorption)
        float moved = (playerTr.position - lastPlayerPos).magnitude;
        lastPlayerPos = playerTr.position;
        if (moved < 0.005f) idleTime += dt;

        if (playerHp != null && playerHp.maxHealth > 0f && (playerHp.currentHealth / playerHp.maxHealth) < 0.30f)
            LowHpClutchSec += dt;

        IdleRatio = totalRoomTime > 0.1f ? Mathf.Clamp01(idleTime / totalRoomTime) : 0f;

        evalTimer += dt;
        if (evalTimer >= 1.5f)
        {
            evalTimer = 0f;
            EvaluateRuleBasedFlow();
        }
    }

    public void EvaluateRuleBasedFlow()
    {
        var dda = DDAManager.Instance;
        var tel = GameplayTelemetry.Instance;
        var cur = tel != null ? tel.Current : null;
        if (dda == null || cur == null) return;

        float c = dda.currentChallenge;
        float liveSkill = ChallengeFramework.SkillScore(cur.Accuracy, totalRoomTime, cur.damageTaken, cur.deathCount, dda.skillRefs);
        float br = ChallengeFramework.BalanceRatio(c, liveSkill);

        if (c < 0.35f && liveSkill < 0.35f)
            CurrentFlowState = "APATHY";
        else if (br > ChallengeFramework.BalancedHigh || cur.deathCount >= 2)
            CurrentFlowState = "ANXIETY";
        else if (br < ChallengeFramework.BalancedLow && cur.deathCount == 0)
            CurrentFlowState = "BOREDOM";
        else
            CurrentFlowState = "FLOW";

        // คะแนนประมาณการ Flow เชิงกฎ (0-1): สมดุลใกล้ 1.0 + ยืนนิ่งน้อย (Fluency สูง)
        float balanceTerm = Mathf.Clamp01(1f - Mathf.Abs(br - 1.0f));
        float fluencyTerm = Mathf.Clamp01(1f - IdleRatio);
        RuleFlowScore = Mathf.Clamp01(0.70f * balanceTerm + 0.30f * fluencyTerm);

        if (tel != null)
            tel.LogEvent("FlowRuleSample", $"{CurrentFlowState}|score={RuleFlowScore:F2}|BR={br:F2}|idle={IdleRatio:F2}");
    }
}
