using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class FirebaseUploader : MonoBehaviour
{
    public static FirebaseUploader Instance;

    [Header("Firebase Realtime Database")]
    public string databaseUrl = "https://projectrougelite-default-rtdb.asia-southeast1.firebasedatabase.app";
    public string authToken = "";
    public bool uploadEnabled = true;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else Destroy(gameObject);
    }

    private string BuildUrl(string path)
    {
        string url = $"{databaseUrl.TrimEnd('/')}/{path.TrimStart('/')}.json";
        if (!string.IsNullOrEmpty(authToken)) url += $"?auth={authToken}";
        return url;
    }

    // 1) เช็คว่ามี ID 6 หลักนี้บน Firebase แล้วหรือยัง -> ถ้ายังไม่มี ให้จอง Node ทันที
    public void CheckAndReserveId(string id, Action<bool, string> onDone)
    {
        if (!uploadEnabled || string.IsNullOrEmpty(databaseUrl))
        {
            onDone?.Invoke(true, "Offline");
            return;
        }
        StartCoroutine(CheckReserveRoutine(SanitizeKey(id), onDone));
    }

    private IEnumerator CheckReserveRoutine(string safeId, Action<bool, string> onDone)
    {
        string checkUrl = BuildUrl($"participants/{safeId}/_reserved");
        using (UnityWebRequest getReq = UnityWebRequest.Get(checkUrl))
        {
            yield return getReq.SendWebRequest();
            if (getReq.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[Firebase] Check ID error: {getReq.error}");
                onDone?.Invoke(true, "OfflineFallback");
                yield break;
            }

            string body = (getReq.downloadHandler.text ?? "").Trim();
            if (!string.IsNullOrEmpty(body) && body != "null")
            {
                // มีคนจอง ID นี้ไปแล้ว!
                onDone?.Invoke(false, "AlreadyTaken");
                yield break;
            }
        }

        // ยังว่าง -> จอง Node ทันทีเพื่อไม่ให้คนอื่นซ้ำ
        string reserveJson = $"{{\"id\":\"{safeId}\",\"status\":\"reserved\",\"reservedAt\":\"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\"}}";
        byte[] bytes = Encoding.UTF8.GetBytes(reserveJson);
        using (UnityWebRequest putReq = new UnityWebRequest(checkUrl, "PUT"))
        {
            putReq.uploadHandler = new UploadHandlerRaw(bytes);
            putReq.downloadHandler = new DownloadHandlerBuffer();
            putReq.SetRequestHeader("Content-Type", "application/json");
            yield return putReq.SendWebRequest();

            if (putReq.result == UnityWebRequest.Result.Success)
            {
                Debug.Log($"[Firebase] Reserved ID {safeId} successfully");
                onDone?.Invoke(true, "Reserved");
            }
            else
            {
                Debug.LogError($"[Firebase] Reserve failed: {putReq.error}");
                onDone?.Invoke(false, "ReserveError");
            }
        }
    }

    // 2) ดึงข้อมูลเดิมของ ID ที่หลุดกลับมาเล่นต่อ (เก็บเวลาเล่นเดิม + ค่าเดิมทั้งหมด)
    public void FetchSessionForResume(string id, string conditionKey, Action<bool, string> onDone)
    {
        if (!uploadEnabled || string.IsNullOrEmpty(databaseUrl))
        {
            onDone?.Invoke(false, null);
            return;
        }
        StartCoroutine(FetchRoutine(SanitizeKey(id), SanitizeKey(conditionKey), onDone));
    }

    private IEnumerator FetchRoutine(string safeId, string safeCond, Action<bool, string> onDone)
    {
        string url = BuildUrl($"participants/{safeId}/{safeCond}");
        using (UnityWebRequest req = UnityWebRequest.Get(url))
        {
            yield return req.SendWebRequest();
            if (req.result == UnityWebRequest.Result.Success)
            {
                string txt = (req.downloadHandler.text ?? "").Trim();
                bool hasData = !string.IsNullOrEmpty(txt) && txt != "null";
                onDone?.Invoke(hasData, hasData ? txt : null);
            }
            else
            {
                onDone?.Invoke(false, null);
            }
        }
    }

    // 3) อัปโหลด Telemetry ขึ้น Node: /participants/{6-digit-ID}/{Adaptive|NonAdaptive}.json
    public void UploadSession(string participantId, string conditionKey, string json)
    {
        if (!uploadEnabled || string.IsNullOrEmpty(databaseUrl)) return;
        string url = BuildUrl($"participants/{SanitizeKey(participantId)}/{SanitizeKey(conditionKey)}");
        StartCoroutine(UploadRoutine(url, json));
    }

    private IEnumerator UploadRoutine(string url, string json)
    {
        byte[] body = Encoding.UTF8.GetBytes(json);
        using (UnityWebRequest req = new UnityWebRequest(url, "PATCH"))
        {
            req.uploadHandler = new UploadHandlerRaw(body);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
                Debug.Log($"[Firebase] Uploaded -> {url}");
            else
                Debug.LogError($"[Firebase] Failed ({req.responseCode}): {req.error}");
        }
    }

    private static string SanitizeKey(string s)
    {
        if (string.IsNullOrEmpty(s)) return "000000";
        return s.Trim().Replace(".", "_").Replace("#", "_").Replace("$", "_").Replace("[", "_").Replace("]", "_").Replace("/", "_").Replace(" ", "");
    }
}
