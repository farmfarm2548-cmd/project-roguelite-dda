using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class ParticipantIdUI : MonoBehaviour
{
    private InputField resumeIdInput;
    private InputField prevGameIdInput;
    private Text currentIdText;
    private Text statusText;
    private Text modeBtnText;
    private int reserveAttempts;
    private static bool hooked;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        if (hooked) return;
        hooked = true;
        OnScene(SceneManager.GetActiveScene());
        SceneManager.sceneLoaded += (s, _) => OnScene(s);
    }

    private static void OnScene(Scene s)
    {
        string lower = s.name.ToLowerInvariant();
        if (lower.Contains("menu") || lower.Contains("end"))
        {
            var go = new GameObject("ParticipantIdUI");
            var ui = go.AddComponent<ParticipantIdUI>();
            ui.BuildForScene(lower.Contains("end"));
        }
    }

    private void EnsureSingletons()
    {
        if (GameplayTelemetry.Instance == null)
            new GameObject("GameplayTelemetry (auto)").AddComponent<GameplayTelemetry>();
        if (FirebaseUploader.Instance == null)
            new GameObject("FirebaseUploader (auto)").AddComponent<FirebaseUploader>();
    }

    private void BuildForScene(bool isEndScene)
    {
        EnsureSingletons();
        Font f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (f == null) f = Font.CreateDynamicFontFromOSFont("Arial", 16);

        GameObject canvasObj = new GameObject("ParticipantID_Canvas");
        canvasObj.transform.SetParent(transform, false);
        var c = canvasObj.AddComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 100;
        var scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        canvasObj.AddComponent<GraphicRaycaster>();

        if (isEndScene)
        {
            var tel = GameplayTelemetry.Instance;
            GameObject box = MakeRect("EndIdBox", canvasObj.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -20), new Vector2(560, 74));
            box.AddComponent<Image>().color = new Color(0.08f, 0.09f, 0.14f, 0.94f);
            MakeText("EndTxt", box.transform, f, 20, $"Your ID: {tel.participantId}   |   Play Time: {tel.totalPlayTimeSec:F1}s", new Color(1f, 0.88f, 0.42f, 1f));
            return;
        }

        // แผงจัดการ ID 6 หลัก + Resume คนหลุด + Prev Game ID ด้านบนหน้า MainMenu
        GameObject panel = MakeRect("IdPanel", canvasObj.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -14), new Vector2(780, 108));
        panel.AddComponent<Image>().color = new Color(0.07f, 0.08f, 0.13f, 0.92f);

        // แถวที่ 1: Auto 6-Digit ID + Status + New ID + Mode A/B
        currentIdText = MakeText("CurId", panel.transform, f, 18, "ID: Checking...", new Color(1f, 0.92f, 0.48f, 1f));
        SetBox(currentIdText.rectTransform, 14, -10, 210, 36);
        currentIdText.alignment = TextAnchor.MiddleLeft;

        statusText = MakeText("Status", panel.transform, f, 13, "Reserving on Firebase...", new Color(0.55f, 0.92f, 0.65f, 1f));
        SetBox(statusText.rectTransform, 226, -10, 250, 36);
        statusText.alignment = TextAnchor.MiddleLeft;

        var newBtn = MakeButton(panel.transform, f, "New ID", new Color(0.24f, 0.42f, 0.62f, 1f), 490, -10, 110, 34);
        newBtn.onClick.AddListener(AutoGenerateAndReserve);

        var modeBtn = MakeButton(panel.transform, f, GetModeLabel(), new Color(0.34f, 0.24f, 0.50f, 1f), 612, -10, 150, 34);
        modeBtnText = modeBtn.GetComponentInChildren<Text>();
        modeBtn.onClick.AddListener(() => {
            var tel = GameplayTelemetry.Instance;
            bool isAd = tel.condition == ExperimentCondition.Adaptive;
            tel.condition = isAd ? ExperimentCondition.NonAdaptive : ExperimentCondition.Adaptive;
            tel.sequence  = isAd ? "NA→AD" : "AD→NA";
            modeBtnText.text = GetModeLabel();
        });

        // แถวที่ 2: ช่อง Resume ID (คนหลุด) + ช่อง Prev Game ID (คนเล่นรอบก่อนแล้ว)
        var rLbl = MakeText("RLbl", panel.transform, f, 13, "Resume ID:", Color.white);
        SetBox(rLbl.rectTransform, 14, -58, 90, 34);
        rLbl.alignment = TextAnchor.MiddleLeft;

        resumeIdInput = MakeInput(panel.transform, f, "6-digit ID", 104, -58, 125, 34);
        var resumeBtn = MakeButton(panel.transform, f, "Resume", new Color(0.18f, 0.52f, 0.36f, 1f), 236, -58, 96, 34);
        resumeBtn.onClick.AddListener(TryResumeId);

        var pLbl = MakeText("PLbl", panel.transform, f, 13, "Prev Game ID:", Color.white);
        SetBox(pLbl.rectTransform, 360, -58, 110, 34);
        pLbl.alignment = TextAnchor.MiddleLeft;

        prevGameIdInput = MakeInput(panel.transform, f, "ID เกมที่แล้ว (ถ้ามี)", 474, -58, 190, 34);
        prevGameIdInput.onValueChanged.AddListener(v => {
            if (GameplayTelemetry.Instance != null)
                GameplayTelemetry.Instance.previousParticipantId = v.Trim();
        });

        // เจนเลข 6 หลัก + เช็กและจองบน Firebase อัตโนมัติทันทีที่เข้า MainMenu
        if (string.IsNullOrEmpty(GameplayTelemetry.Instance.participantId) || GameplayTelemetry.Instance.participantId.Length != 6)
        {
            AutoGenerateAndReserve();
        }
        else
        {
            currentIdText.text = $"ID: {GameplayTelemetry.Instance.participantId}";
            statusText.text = "Ready ✓";
        }
    }

    private void AutoGenerateAndReserve()
    {
        reserveAttempts = 0;
        TryReserveRandom6Digit();
    }

    private void TryReserveRandom6Digit()
    {
        reserveAttempts++;
        string candidate = Random.Range(100000, 1000000).ToString();
        currentIdText.text = $"ID: {candidate}";
        statusText.text = "Checking Firebase...";

        FirebaseUploader.Instance.CheckAndReserveId(candidate, (ok, reason) =>
        {
            if (ok)
            {
                GameplayTelemetry.Instance.participantId = candidate;
                GameplayTelemetry.Instance.totalPlayTimeSec = 0f;
                GameplayTelemetry.Instance.isResumedSession = false;
                GameplayTelemetry.Instance.SaveParticipantIdToTxt("RESERVED");
                currentIdText.text = $"ID: {candidate}";
                statusText.text = reason == "Reserved" ? "Reserved on Firebase ✓" : "Ready (Local) ✓";
            }
            else if (reserveAttempts < 10)
            {
                TryReserveRandom6Digit(); // ซ้ำ -> สุ่ม 6 หลักใหม่ทันที
            }
            else
            {
                statusText.text = "Retry New ID";
            }
        });
    }

    private void TryResumeId()
    {
        string targetId = resumeIdInput != null ? resumeIdInput.text.Trim() : "";
        if (targetId.Length != 6)
        {
            statusText.text = "กรุณากรอกเลข ID 6 หลัก";
            return;
        }

        statusText.text = $"Loading ID {targetId}...";
        var tel = GameplayTelemetry.Instance;
        string condKey = tel.condition == ExperimentCondition.Adaptive ? "Adaptive" : "NonAdaptive";

        FirebaseUploader.Instance.FetchSessionForResume(targetId, condKey, (found, json) =>
        {
            tel.participantId = targetId;
            if (found && !string.IsNullOrEmpty(json))
            {
                tel.ApplyResumedJson(json);
                if (prevGameIdInput != null && !string.IsNullOrEmpty(tel.previousParticipantId))
                    prevGameIdInput.text = tel.previousParticipantId;
                statusText.text = $"Resumed ✓ ({tel.totalPlayTimeSec:F0}s)";
            }
            else
            {
                tel.isResumedSession = true;
                statusText.text = "Resumed ID (No prior room)";
            }
            tel.SaveParticipantIdToTxt("RESUMED");
            currentIdText.text = $"ID: {targetId}";
        });
    }

    private string GetModeLabel()
    {
        bool isAd = GameplayTelemetry.Instance == null || GameplayTelemetry.Instance.condition == ExperimentCondition.Adaptive;
        return isAd ? "Mode: A" : "Mode: B";
    }

    private void SetBox(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(x, y); rt.sizeDelta = new Vector2(w, h);
    }

    private GameObject MakeRect(string name, Transform parent, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = aMin; rt.anchorMax = aMax; rt.pivot = pivot;
        rt.anchoredPosition = pos; rt.sizeDelta = size;
        return go;
    }

    private Text MakeText(string name, Transform parent, Font f, int size, string s, Color col)
    {
        GameObject go = MakeRect(name, parent, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        var t = go.AddComponent<Text>();
        t.font = f; t.fontSize = size; t.fontStyle = FontStyle.Bold;
        t.alignment = TextAnchor.MiddleCenter; t.color = col; t.text = s;
        return t;
    }

    private Button MakeButton(Transform parent, Font f, string label, Color col, float x, float y, float w, float h)
    {
        GameObject go = new GameObject("Btn_" + label);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        SetBox(rt, x, y, w, h);
        go.AddComponent<Image>().color = col;
        MakeText("Txt", go.transform, f, 13, label, Color.white);
        return go.AddComponent<Button>();
    }

    private InputField MakeInput(Transform parent, Font f, string placeholder, float x, float y, float w, float h)
    {
        GameObject go = new GameObject("Inp_" + placeholder);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        SetBox(rt, x, y, w, h);
        go.AddComponent<Image>().color = new Color(0.16f, 0.19f, 0.27f, 1f);
        var inp = go.AddComponent<InputField>();
        var ph = MakeText("Placeholder", go.transform, f, 13, placeholder, new Color(0.6f, 0.65f, 0.72f, 0.7f));
        var tx = MakeText("Text", go.transform, f, 14, "", new Color(1f, 0.94f, 0.6f, 1f));
        inp.placeholder = ph;
        inp.textComponent = tx;
        return inp;
    }
}
