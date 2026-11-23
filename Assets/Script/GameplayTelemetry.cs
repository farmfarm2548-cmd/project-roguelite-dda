using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum ExperimentCondition { NonAdaptive, Adaptive }

[System.Serializable]
public class RoomTelemetry
{
    public string participantId;
    public string previousParticipantId;
    public string condition;
    public string sequence;
    public string runId;

    public string roomId;
    public int    roomIndex;
    public float  roomStartTime;
    public float  roomEndTime;

    public float  enemySpeed;
    public float  enemyAttackPower;
    public float  enemySpawnRate;
    public float  enemyHp;

    public int    enemySpawnCount;
    public int    attackAttempts;
    public int    successfulHits;
    public int    skillUses;

    public float  damageTaken;
    public int    deathCount;
    public int    attemptCount;
    public float  clearTime;
    public float  cumulativePlayTimeSec;

    public bool   roomClear;

    public float  challengeScore;
    public float  skillScore;
    public float  nextChallenge = float.NaN;
    public float  csbi = float.NaN;
    public float  balanceRatio = float.NaN;
    public float  balanceError = float.NaN;
    public bool   inBalancedRegion;

    public float  flowScore = float.NaN;

    public float Accuracy => attackAttempts > 0 ? (float)successfulHits / attackAttempts : 0f;
    public float ObservedSpawnRate => clearTime > 0.001f ? enemySpawnCount / clearTime : 0f;
}

[System.Serializable]
public class GameEvent
{
    public float time;
    public string roomId;
    public string type;
    public string detail;
}

public class GameplayTelemetry : MonoBehaviour
{
    public static GameplayTelemetry Instance;

    [Header("Session Info")]
    public string participantId = "";
    public string previousParticipantId = "";
    public ExperimentCondition condition = ExperimentCondition.Adaptive;
    public string sequence = "AD→NA";
    public string runId;

    [Header("Cumulative Play Time (คงค่าเดิมเมื่อ Resume)")]
    public float totalPlayTimeSec = 0f;
    public bool isResumedSession = false;

    [Header("Auto Export")]
    public bool autoExportOnSessionEnd = true;

    private readonly List<RoomTelemetry> rooms = new List<RoomTelemetry>();
    private readonly List<GameEvent> events = new List<GameEvent>();
    private RoomTelemetry current;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            if (string.IsNullOrEmpty(runId))
                runId = "RUN-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
        }
        else Destroy(gameObject);
    }

    private void Update()
    {
        string sn = SceneManager.GetActiveScene().name.ToLowerInvariant();
        if (sn.Contains("room") || sn.Contains("boss"))
        {
            totalPlayTimeSec += Time.deltaTime;
        }
    }

    // บันทึกเลข ID ลงไฟล์ ParticipantIDs.txt ในเครื่องอัตโนมัติ
    public string SaveParticipantIdToTxt(string actionTag)
    {
        string dir = Path.Combine(Application.persistentDataPath, "Telemetry");
        Directory.CreateDirectory(dir);
        string logFile = Path.Combine(dir, "ParticipantIDs.txt");
        string lastFile = Path.Combine(dir, "LastParticipantID.txt");

        string condStr = condition == ExperimentCondition.Adaptive ? "Adaptive" : "NonAdaptive";
        string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | ID={participantId} | PrevID={(string.IsNullOrEmpty(previousParticipantId) ? "-" : previousParticipantId)} | Mode={condStr} | Action={actionTag} | PlayTime={totalPlayTimeSec:F1}s";
        File.AppendAllText(logFile, line + Environment.NewLine, Encoding.UTF8);
        File.WriteAllText(lastFile, participantId, Encoding.UTF8);
        Debug.Log($"[Telemetry] Saved ID to TXT -> {logFile}");
        return logFile;
    }

    // กู้คืนค่าเวลาเล่นเดิมและข้อมูลสรุปเมื่อคนหลุดกด Resume ด้วย ID เดิม
    public void ApplyResumedJson(string json)
    {
        isResumedSession = true;
        if (string.IsNullOrEmpty(json)) return;

        Match mTime = Regex.Match(json, "\"totalPlayTimeSec\"\\s*:\\s*([0-9]+(?:\\.[0-9]+)?)");
        if (mTime.Success && float.TryParse(mTime.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float prevTime))
        {
            totalPlayTimeSec = prevTime;
        }

        Match mPrev = Regex.Match(json, "\"previousParticipantId\"\\s*:\\s*\"([^\"]*)\"");
        if (mPrev.Success && string.IsNullOrEmpty(previousParticipantId))
        {
            previousParticipantId = mPrev.Groups[1].Value;
        }

        LogEvent("SessionResumed", $"restoredPlayTime={totalPlayTimeSec:F1}s");
        Debug.Log($"[Telemetry] Resumed ID={participantId} | Restored totalPlayTimeSec={totalPlayTimeSec:F1}s");
    }

    public RoomTelemetry BeginRoom(string roomId, int roomIndex, float challengeScore,
                                   float enemySpeed, float enemyAtk, float spawnRate, float enemyHp)
    {
        current = new RoomTelemetry
        {
            participantId         = participantId,
            previousParticipantId = previousParticipantId,
            condition             = condition == ExperimentCondition.Adaptive ? "Adaptive" : "Non-Adaptive",
            sequence              = sequence,
            runId                 = runId,
            roomId                = roomId,
            roomIndex             = roomIndex,
            roomStartTime         = Time.time,
            challengeScore        = challengeScore,
            enemySpeed            = enemySpeed,
            enemyAttackPower      = enemyAtk,
            enemySpawnRate        = spawnRate,
            enemyHp               = enemyHp,
            attemptCount          = 1,
            cumulativePlayTimeSec = totalPlayTimeSec
        };
        rooms.Add(current);
        LogEvent("RoomStart", roomId);
        return current;
    }

    public RoomTelemetry Current => current;

    public void EndRoom(bool cleared)
    {
        if (current == null) return;
        current.roomEndTime = Time.time;
        current.clearTime   = current.roomEndTime - current.roomStartTime;
        current.cumulativePlayTimeSec = totalPlayTimeSec;
        current.roomClear   = cleared;
        LogEvent(cleared ? "RoomClear" : "RoomFail", current.roomId);
    }

    public void RecordAttackAttempt() { if (current != null) current.attackAttempts++; LogEvent("Attack", null); }
    public void RecordSkillUse(string skillName) { if (current != null) current.skillUses++; LogEvent("SkillCast", skillName); }
    public void RecordSuccessfulHit() { if (current != null) current.successfulHits++; LogEvent("Hit", null); }
    public void RecordEnemySpawn(string enemyType = null) { if (current != null) current.enemySpawnCount++; LogEvent("EnemySpawn", enemyType); }
    public void RecordEnemyKilled(string enemyType = null) => LogEvent("EnemyKilled", enemyType);
    public void RecordDamageTaken(float amount) { if (current != null) current.damageTaken += amount; LogEvent("Damage", amount.ToString("F1", CultureInfo.InvariantCulture)); }
    public void RecordDeath() { if (current != null) current.deathCount++; LogEvent("Death", null); }
    public void RecordRoomAttempt() { if (current != null) current.attemptCount++; }

    public void LogEvent(string type, string detail)
    {
        events.Add(new GameEvent
        {
            time   = totalPlayTimeSec,
            roomId = current != null ? current.roomId : "-",
            type   = type,
            detail = detail ?? ""
        });
    }

    public float CalculateBGP()
    {
        int valid = 0, balanced = 0;
        foreach (var r in rooms)
        {
            if (r.roomIndex >= 4 || float.IsNaN(r.balanceRatio)) continue;
            valid++;
            if (r.inBalancedRegion) balanced++;
        }
        return valid > 0 ? (balanced / (float)valid) * 100f : 0f;
    }

    public IReadOnlyList<RoomTelemetry> Rooms => rooms;

    public void ExportCsv()
    {
        string dir = Path.Combine(Application.persistentDataPath, "Telemetry");
        Directory.CreateDirectory(dir);
        string stamp = string.IsNullOrEmpty(runId) ? DateTime.Now.ToString("yyyyMMdd-HHmmss") : runId;

        WriteRoomCsv(Path.Combine(dir, $"rooms_{participantId}_{stamp}.csv"));
        WriteEventCsv(Path.Combine(dir, $"events_{participantId}_{stamp}.csv"));
        UploadToFirebase();
    }

    public void UploadToFirebase()
    {
        if (FirebaseUploader.Instance != null)
            FirebaseUploader.Instance.UploadSession(participantId, condition == ExperimentCondition.Adaptive ? "Adaptive" : "NonAdaptive", BuildSessionJson());
    }

    private void WriteRoomCsv(string path)
    {
        var sb = new StringBuilder();
        sb.AppendLine("ParticipantID,PreviousID,Sequence,Condition,RunID,Room,RoomIndex," +
                      "C,S,NextC,CSBI,BalanceRatio,BalanceError,InBalance,BGP," +
                      "Accuracy,ClearTime,TotalPlayTimeSec,DamageTaken,DeathCount,AttemptCount,RoomClear," +
                      "EnemySpeed,EnemyAttackPower,EnemySpawnRate,ObservedSpawnRate,EnemyHP," +
                      "EnemySpawnCount,AttackAttempts,SuccessfulHits,SkillUses,Flow");
        float bgp = CalculateBGP();
        foreach (var r in rooms)
        {
            sb.AppendLine(string.Join(",", new[]
            {
                Esc(r.participantId), Esc(r.previousParticipantId), Esc(r.sequence), Esc(r.condition), Esc(r.runId),
                Esc(r.roomId), r.roomIndex.ToString(),
                F(r.challengeScore), F(r.skillScore), F(r.nextChallenge), F(r.csbi), F(r.balanceRatio), F(r.balanceError),
                r.inBalancedRegion ? "1" : "0", F(bgp),
                F(r.Accuracy), F(r.clearTime), F(r.cumulativePlayTimeSec), F(r.damageTaken), r.deathCount.ToString(),
                r.attemptCount.ToString(), r.roomClear ? "1" : "0",
                F(r.enemySpeed), F(r.enemyAttackPower), F(r.enemySpawnRate), F(r.ObservedSpawnRate), F(r.enemyHp),
                r.enemySpawnCount.ToString(), r.attackAttempts.ToString(), r.successfulHits.ToString(), r.skillUses.ToString(),
                float.IsNaN(r.flowScore) ? "" : F(r.flowScore)
            }));
        }
        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
    }

    private void WriteEventCsv(string path)
    {
        var sb = new StringBuilder();
        sb.AppendLine("PlayTimeSec,Room,Type,Detail");
        foreach (var e in events)
            sb.AppendLine($"{F(e.time)},{Esc(e.roomId)},{Esc(e.type)},{Esc(e.detail)}");
        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
    }

    public string BuildSessionJson()
    {
        var sb = new StringBuilder();
        sb.Append('{');
        sb.Append("\"summary\":{");
        sb.Append("\"participantId\":").Append(JStr(participantId)).Append(',');
        sb.Append("\"previousParticipantId\":").Append(JStr(previousParticipantId)).Append(',');
        sb.Append("\"condition\":").Append(JStr(condition == ExperimentCondition.Adaptive ? "Adaptive" : "Non-Adaptive")).Append(',');
        sb.Append("\"sequence\":").Append(JStr(sequence)).Append(',');
        sb.Append("\"runId\":").Append(JStr(runId)).Append(',');
        sb.Append("\"totalPlayTimeSec\":").Append(JNum(totalPlayTimeSec)).Append(',');
        sb.Append("\"isResumed\":").Append(isResumedSession ? "true" : "false").Append(',');
        sb.Append("\"bgp\":").Append(JNum(CalculateBGP())).Append(',');
        sb.Append("\"updatedAt\":").Append(JStr(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")));
        sb.Append("},\"rooms\":{");
        for (int i = 0; i < rooms.Count; i++)
        {
            if (i > 0) sb.Append(',');
            string rKey = string.IsNullOrEmpty(rooms[i].roomId) ? ("R" + (i + 1)) : rooms[i].roomId;
            sb.Append(JStr(rKey)).Append(':');
            AppendRoomJson(sb, rooms[i]);
        }
        sb.Append("},\"events\":[");
        for (int i = 0; i < events.Count; i++)
        {
            if (i > 0) sb.Append(',');
            var e = events[i];
            sb.Append('{')
              .Append("\"playTimeSec\":").Append(JNum(e.time)).Append(',')
              .Append("\"room\":").Append(JStr(e.roomId)).Append(',')
              .Append("\"type\":").Append(JStr(e.type)).Append(',')
              .Append("\"detail\":").Append(JStr(e.detail))
              .Append('}');
        }
        sb.Append("]}");
        return sb.ToString();
    }

    private static void AppendRoomJson(StringBuilder sb, RoomTelemetry r)
    {
        sb.Append('{')
          .Append("\"room\":").Append(JStr(r.roomId)).Append(',')
          .Append("\"roomIndex\":").Append(r.roomIndex).Append(',')
          .Append("\"C\":").Append(JNum(r.challengeScore)).Append(',')
          .Append("\"S\":").Append(JNum(r.skillScore)).Append(',')
          .Append("\"nextC\":").Append(JNum(r.nextChallenge)).Append(',')
          .Append("\"CSBI\":").Append(JNum(r.csbi)).Append(',')
          .Append("\"balanceRatio\":").Append(JNum(r.balanceRatio)).Append(',')
          .Append("\"balanceError\":").Append(JNum(r.balanceError)).Append(',')
          .Append("\"inBalance\":").Append(r.inBalancedRegion ? "true" : "false").Append(',')
          .Append("\"accuracy\":").Append(JNum(r.Accuracy)).Append(',')
          .Append("\"clearTime\":").Append(JNum(r.clearTime)).Append(',')
          .Append("\"totalPlayTimeSec\":").Append(JNum(r.cumulativePlayTimeSec)).Append(',')
          .Append("\"damageTaken\":").Append(JNum(r.damageTaken)).Append(',')
          .Append("\"deathCount\":").Append(r.deathCount).Append(',')
          .Append("\"attemptCount\":").Append(r.attemptCount).Append(',')
          .Append("\"roomClear\":").Append(r.roomClear ? "true" : "false").Append(',')
          .Append("\"enemySpeed\":").Append(JNum(r.enemySpeed)).Append(',')
          .Append("\"enemyAttackPower\":").Append(JNum(r.enemyAttackPower)).Append(',')
          .Append("\"enemySpawnRate\":").Append(JNum(r.enemySpawnRate)).Append(',')
          .Append("\"observedSpawnRate\":").Append(JNum(r.ObservedSpawnRate)).Append(',')
          .Append("\"enemyHp\":").Append(JNum(r.enemyHp)).Append(',')
          .Append("\"enemySpawnCount\":").Append(r.enemySpawnCount).Append(',')
          .Append("\"attackAttempts\":").Append(r.attackAttempts).Append(',')
          .Append("\"successfulHits\":").Append(r.successfulHits).Append(',')
          .Append("\"skillUses\":").Append(r.skillUses).Append(',')
          .Append("\"flow\":").Append(JNum(r.flowScore))
          .Append('}');
    }

    private static string JNum(float v) => (float.IsNaN(v) || float.IsInfinity(v)) ? "null" : v.ToString("0.####", CultureInfo.InvariantCulture);
    private static string JStr(string s)
    {
        if (s == null) return "null";
        var sb = new StringBuilder("\"");
        foreach (char c in s)
        {
            switch (c)
            {
                case '"':  sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n");  break;
                case '\r': sb.Append("\\r");  break;
                case '\t': sb.Append("\\t");  break;
                default:   sb.Append(c);      break;
            }
        }
        return sb.Append('"').ToString();
    }
    private static string F(float v) => float.IsNaN(v) ? "" : v.ToString("0.####", CultureInfo.InvariantCulture);
    private static string Esc(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        if (s.Contains(",") || s.Contains("\"") || s.Contains("\n"))
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        return s;
    }
}
