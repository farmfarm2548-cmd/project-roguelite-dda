using System.Collections;
using System.Globalization;
using UnityEngine;
using UnityEngine.Networking;

//  ส่ง Telemetry ระดับห้อง (1 แถว/ห้อง) เข้า Google Sheets ผ่าน Apps Script Web App
//  (ไม่ต้องใช้ API key / OAuth / service account)
//
//  วิธีตั้งค่า: ดู GoogleSheet_AppsScript.gs.txt (วางโค้ดใน Sheet → Deploy → คัดลอก URL)
//  แล้วเอา URL มาใส่ช่อง webAppUrl
public class GoogleSheetUploader : MonoBehaviour
{
    public static GoogleSheetUploader Instance;

    [Header("Google Apps Script Web App URL")]
    [Tooltip("เช่น https://script.google.com/macros/s/XXXX/exec")]
    public string webAppUrl = "";

    [Tooltip("เปิด/ปิดการส่งเข้า Sheet แบบ real-time (ปิดไว้ถ้าใช้วิธีดึงจาก Firebase → Sheet แทน)")]
    public bool uploadEnabled = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else { Destroy(gameObject); }
    }

    public void SendRoom(RoomTelemetry r)
    {
        if (!uploadEnabled || r == null) return;
        if (string.IsNullOrEmpty(webAppUrl))
        {
            Debug.LogWarning("[Sheet] ยังไม่ได้ตั้ง webAppUrl — ข้ามการส่งเข้า Sheet");
            return;
        }
        StartCoroutine(Post(r));
    }

    private IEnumerator Post(RoomTelemetry r)
    {
        WWWForm form = new WWWForm();
        form.AddField("Participant",      r.participantId ?? "");
        form.AddField("Sequence",         r.sequence ?? "");
        form.AddField("Condition",        r.condition ?? "");
        form.AddField("RunID",            r.runId ?? "");
        form.AddField("Room",             r.roomId ?? "");
        form.AddField("RoomIndex",        r.roomIndex.ToString());
        form.AddField("C",                N(r.challengeScore));
        form.AddField("S",                N(r.skillScore));
        form.AddField("CSBI",             N(r.csbi));
        form.AddField("BalanceError",     N(r.balanceError));
        form.AddField("InBalance",        r.inBalancedRegion ? "1" : "0");
        form.AddField("Accuracy",         N(r.Accuracy));
        form.AddField("ClearTime",        N(r.clearTime));
        form.AddField("DamageTaken",      N(r.damageTaken));
        form.AddField("DeathCount",       r.deathCount.ToString());
        form.AddField("AttemptCount",     r.attemptCount.ToString());
        form.AddField("RoomClear",        r.roomClear ? "1" : "0");
        form.AddField("EnemySpeed",       N(r.enemySpeed));
        form.AddField("EnemyAttackPower", N(r.enemyAttackPower));
        form.AddField("EnemySpawnRate",   N(r.enemySpawnRate));
        form.AddField("EnemyHP",          N(r.enemyHp));
        form.AddField("EnemySpawnCount",  r.enemySpawnCount.ToString());
        form.AddField("AttackAttempts",   r.attackAttempts.ToString());
        form.AddField("SuccessfulHits",   r.successfulHits.ToString());
        form.AddField("Flow",             float.IsNaN(r.flowScore) ? "" : N(r.flowScore));

        using (UnityWebRequest req = UnityWebRequest.Post(webAppUrl, form))
        {
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
                Debug.Log($"[Sheet] ส่งเข้า Sheet สำเร็จ: {r.roomId}");
            else
                Debug.LogError($"[Sheet] ส่งล้มเหลว ({req.responseCode}): {req.error}");
        }
    }

    // NaN/Infinity → "" (ช่องว่างใน Sheet)
    private static string N(float v)
    {
        if (float.IsNaN(v) || float.IsInfinity(v)) return "";
        return v.ToString("0.####", CultureInfo.InvariantCulture);
    }
}
