#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuAndTutorialUI : MonoBehaviour
{
    public const string FirebaseBaseUrl = "https://projectrougelite-default-rtdb.asia-southeast1.firebasedatabase.app";

    public static string CurrentParticipantId { get; private set; } = "";
    public static string PreviousGameId { get; private set; } = "";
    public static bool IsAdaptiveMode { get; private set; } = true; // Blinded: Mode A = Adaptive, Mode B = NonAdaptive
    public static bool IsResumedSession { get; private set; } = false;
    public static float CumulativePlayTimeSec { get; private set; } = 0f;

    private static MainMenuAndTutorialUI _instance;
    private Font _uiFont;

    private GameObject _menuCanvasGo;
    private GameObject _step1IdPanel;
    private GameObject _step2TutorialPanel;
    private GameObject _loadingOverlayPanel;
    private Image _loadingFillBar;
    private Text _loadingPercentText;
    private Text _loadingTipText;

    private Text _idDisplayText;
    private Text _statusText;
    private InputField _resumeInput;
    private InputField _prevIdInput;
    private Button _modeABtn;
    private Button _modeBBtn; private Button _regenBtn;
    private bool _isBusy = false;
    private bool _isIdReserved = false; private bool _modeACompleted = false; private bool _modeBCompleted = false;

    private GameObject _inGameHelpCanvasGo;
    private GameObject _inGameTutorialModal;
    private GameObject _endSceneCanvasGo;
    private Text _copyFeedbackText;

    private static readonly string[] LoadingTips = new string[]
    {
        "เคล็ดลับ: กด [E] Shadow Dash เพื่อพุ่งหลบการโจมตีของมอนสเตอร์พร้อมอมตะชั่วขณะ",
        "เคล็ดลับ: กด [R] Arcane Burst เพื่อระเบิดพลังรอบตัวและฟื้นฟูเลือดในจังหวะคับขัน",
        "เคล็ดลับ: กำจัดมอนสเตอร์ในห้องให้ครบเพื่อปลดล็อกประตูวาร์ปสีม่วงกลางห้อง",
        "เคล็ดลับ: กด [1] เพื่อดื่มยาฟื้นเลือด (+45 HP) และ [2] เพื่อฟื้นมานา (+40 MP)",
        "เคล็ดลับ: กด [F1] ระหว่างเล่นได้ทุกเมื่อหากต้องการเปิดดูหน้าคู่มือปุ่มควบคุมอีกครั้ง"
    };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (_instance != null) return;
        GameObject go = new GameObject("MainMenuAndTutorialUI");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<MainMenuAndTutorialUI>();
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);

        LoadUiFont();
        SceneManager.sceneLoaded += OnSceneLoaded;
        HandleCurrentScene(SceneManager.GetActiveScene().name);
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Update()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        if (IsGameplayScene(sceneName))
        {
            if (Time.timeScale > 0f)
            {
                CumulativePlayTimeSec += Time.unscaledDeltaTime;
            }
            #if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame)
#else
            if (Input.GetKeyDown(KeyCode.F1))
#endif
            {
                ToggleInGameTutorial();
            }
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        HandleCurrentScene(scene.name);
    }

    private void HandleCurrentScene(string sceneName)
    {
        if (_menuCanvasGo != null) Destroy(_menuCanvasGo);
        if (_inGameHelpCanvasGo != null) Destroy(_inGameHelpCanvasGo);
        if (_endSceneCanvasGo != null) Destroy(_endSceneCanvasGo);

        string lower = sceneName.ToLowerInvariant();
        if (lower.Contains("menu") || lower.Contains("start") || lower.Contains("title"))
        {
            Time.timeScale = 1f;
            DisableLegacyParticipantUI();
            EnsureEventSystem();
            if (!TryBindEditableMainMenu()) { HideOtherSceneCanvases(); BuildMainMenuFlowUI(); }
            StartCoroutine(AutoGenerateAndReserveIdCoroutine());
        }
        else if (lower.Contains("load"))
        {
            Time.timeScale = 1f;
            EnsureEventSystem();
            if (!TryBindEditableLoading()) { HideOtherSceneCanvases(); BuildStandaloneLoadingSceneUI(); }
            StartCoroutine(LoadSceneWithProgressBar("Room1"));
        }
        else if (IsGameplayScene(sceneName))
        {
            Time.timeScale = 1f;
            EnsureEventSystem();
            BuildInGameHelpButtonAndModal();
            SyncStateToDDAManager();
        }
        else if (lower.Contains("end") || lower.Contains("victory"))
        {
            Time.timeScale = 1f;
            if (string.IsNullOrEmpty(CurrentParticipantId))
                CurrentParticipantId = PlayerPrefs.GetString("ParticipantID", "------");
            PlayerPrefs.SetFloat("CumulativePlayTime_" + CurrentParticipantId, CumulativePlayTimeSec);
            PlayerPrefs.Save();
            EnsureEventSystem();
            if (!TryBindEditableEndScene()) { HideOtherSceneCanvases(); BuildEndSceneUI(); }
        }
    }

    private bool IsGameplayScene(string sceneName)
    {
        return sceneName.StartsWith("Room", StringComparison.OrdinalIgnoreCase)
            || sceneName.Equals("BossRoom", StringComparison.OrdinalIgnoreCase);
    }

    private bool TryBindEditableMainMenu()
    {
        GameObject existing = GameObject.Find("Editable_MainMenu_Canvas");
        if (existing == null) return false;
        ApplyThaiFontRecursive(existing);
        _step1IdPanel = FindChildByName(existing.transform, "Step1_IdPanel");
        _step2TutorialPanel = FindChildByName(existing.transform, "Step2_TutorialPanel");
        _loadingOverlayPanel = FindChildByName(existing.transform, "LoadingPanel");

        _idDisplayText = FindComponentByName<Text>(existing.transform, "IdValue");
        _statusText = FindComponentByName<Text>(existing.transform, "StatusText");
        _resumeInput = FindComponentByName<InputField>(existing.transform, "ResumeInput");
        _prevIdInput = FindComponentByName<InputField>(existing.transform, "PrevGameIdInput");
        _modeABtn = FindComponentByName<Button>(existing.transform, "ModeABtn");
        _modeBBtn = FindComponentByName<Button>(existing.transform, "ModeBBtn"); _regenBtn = FindComponentByName<Button>(existing.transform, "RegenBtn");
        _loadingFillBar = FindComponentByName<Image>(existing.transform, "BarFill");
        _loadingPercentText = FindComponentByName<Text>(existing.transform, "PercentTxt");
        _loadingTipText = FindComponentByName<Text>(existing.transform, "TipTxt");

        string lastSavedId = PlayerPrefs.GetString("LastParticipantID", "");
        if (_resumeInput != null && !string.IsNullOrEmpty(lastSavedId)) _resumeInput.text = lastSavedId;

        BindButton(existing.transform, "RegenBtn", () => { if (!_isBusy) StartCoroutine(AutoGenerateAndReserveIdCoroutine()); });
        BindButton(existing.transform, "ResumeBtn", () => { if (!_isBusy && _resumeInput != null) StartCoroutine(ResumeParticipantCoroutine(_resumeInput.text.Trim())); });
        BindButton(existing.transform, "ModeABtn", () => SelectMode(true));
        BindButton(existing.transform, "ModeBBtn", () => SelectMode(false));
        BindButton(existing.transform, "NextToTutorialBtn", OnProceedToTutorialClicked);
        BindButton(existing.transform, "BackBtn", OnBackToIdStepClicked);
        BindButton(existing.transform, "StartGameFinalBtn", OnStartGameClicked);

        if (_step1IdPanel != null) _step1IdPanel.SetActive(true);
        if (_step2TutorialPanel != null) _step2TutorialPanel.SetActive(false);
        if (_loadingOverlayPanel != null) _loadingOverlayPanel.SetActive(false);
        SelectMode(true);
        return true;
    }

    private bool TryBindEditableLoading()
    {
        GameObject existing = GameObject.Find("Editable_Loading_Canvas");
        if (existing == null) return false;
        ApplyThaiFontRecursive(existing);
        _loadingOverlayPanel = FindChildByName(existing.transform, "LoadingPanel");
        _loadingFillBar = FindComponentByName<Image>(existing.transform, "BarFill");
        _loadingPercentText = FindComponentByName<Text>(existing.transform, "PercentTxt");
        _loadingTipText = FindComponentByName<Text>(existing.transform, "TipTxt");
        if (_loadingTipText != null) _loadingTipText.text = LoadingTips[UnityEngine.Random.Range(0, LoadingTips.Length)];
        Text pidFooter = FindComponentByName<Text>(existing.transform, "PidFooter");
        if (pidFooter != null)
        {
            string pid = string.IsNullOrEmpty(CurrentParticipantId) ? PlayerPrefs.GetString("ParticipantID", "------") : CurrentParticipantId;
            pidFooter.text = $"Participant ID: {pid}  |  Mode {(IsAdaptiveMode ? "A" : "B")}";
        }
        return true;
    }

    private bool TryBindEditableEndScene()
    {
        GameObject existing = GameObject.Find("Editable_EndScene_Canvas");
        if (existing == null) return false;
        ApplyThaiFontRecursive(existing);
        Text bigId = FindComponentByName<Text>(existing.transform, "BigId");
        if (bigId != null) bigId.text = CurrentParticipantId;
        _copyFeedbackText = FindComponentByName<Text>(existing.transform, "CopyFeedback");
        Text statLeft = FindComponentByName<Text>(existing.transform, "StatLeft");
        if (statLeft != null) statLeft.text = $"โหมดที่เล่น\n{(IsAdaptiveMode ? "Mode A" : "Mode B")}";
        Text statMid = FindComponentByName<Text>(existing.transform, "StatMid");
        if (statMid != null)
        {
            int mins = Mathf.FloorToInt(CumulativePlayTimeSec / 60f);
            int secs = Mathf.FloorToInt(CumulativePlayTimeSec % 60f);
            statMid.text = $"เวลาเล่นรวม\n{mins:00}:{secs:00}";
        }
        BindButton(existing.transform, "CopyIdBtn", () => {
            GUIUtility.systemCopyBuffer = CurrentParticipantId;
            if (_copyFeedbackText != null) _copyFeedbackText.text = "คัดลอกรหัส " + CurrentParticipantId + " ลงคลิปบอร์ดแล้ว!";
        });
        BindButton(existing.transform, "BackToMenuBtn", () => { if (IsAdaptiveMode) PlayerPrefs.SetInt("SkipPartAOnce", 1); CumulativePlayTimeSec = 0f; SceneManager.LoadScene("MainMenu"); });
        BindButton(existing.transform, "QuitBtn", () => { Application.Quit(); }); if (IsAdaptiveMode && GameObject.Find("QuitBtn") != null) GameObject.Find("QuitBtn").SetActive(false); GameObject qGo = FindChildByName(existing.transform, "QuitBtn"); if (qGo != null) qGo.SetActive(!IsAdaptiveMode); GameObject bGo = FindChildByName(existing.transform, "BackToMenuBtn"); if (bGo != null && IsAdaptiveMode) { Text bTxt = bGo.GetComponentInChildren<Text>(); if (bTxt != null) bTxt.text = "กลับหน้าหลักเพื่อเล่น Mode B (MAIN MENU)"; }
        return true;
    }

    private void ApplyThaiFontRecursive(GameObject root)
    {
        if (root == null || _uiFont == null) return;
        foreach (Text t in root.GetComponentsInChildren<Text>(true))
        {
            if (t != null) t.font = _uiFont;
        }
    }

    private GameObject FindChildByName(Transform root, string targetName)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t != null && t.name == targetName) return t.gameObject;
        }
        return null;
    }

    private T FindComponentByName<T>(Transform root, string targetName) where T : Component
    {
        GameObject go = FindChildByName(root, targetName);
        return go != null ? go.GetComponent<T>() : null;
    }

    private void BindButton(Transform root, string btnName, Action action)
    {
        Button b = FindComponentByName<Button>(root, btnName);
        if (b == null) return;
        b.onClick.RemoveAllListeners();
        b.onClick.AddListener(() => action?.Invoke());
    }

    private void HideOtherSceneCanvases()
    {
        Canvas[] canvases = FindObjectsCompat<Canvas>();
        foreach (Canvas c in canvases)
        {
            if (c == null) continue;
            if (c.gameObject == _menuCanvasGo || c.gameObject == _inGameHelpCanvasGo || c.gameObject == _endSceneCanvasGo) continue;
            c.gameObject.SetActive(false);
        }
    }

    private void DisableLegacyParticipantUI()
    {
        MonoBehaviour[] allScripts = FindObjectsCompat<MonoBehaviour>();
        foreach (MonoBehaviour mb in allScripts)
        {
            if (mb == null || mb == this) continue;
            if (mb.GetType().Name == "ParticipantIdUI")
            {
                mb.enabled = false;
                Canvas c = mb.GetComponentInChildren<Canvas>(true);
                if (c != null) c.gameObject.SetActive(false);
            }
        }
    }

    private void LoadUiFont()
    {
        try
        {
            _uiFont = Font.CreateDynamicFontFromOSFont(
                new string[] { "Leelawadee UI", "Tahoma", "Segoe UI", "Arial" }, 20);
        }
        catch { _uiFont = null; }

        if (_uiFont == null)
        {
            _uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
    }

    private void EnsureEventSystem()
    {
        EventSystem es = FindFirstCompat<EventSystem>();
        if (es == null)
        {
            GameObject esGo = new GameObject("EventSystem");
            esGo.AddComponent<EventSystem>();
            #if ENABLE_INPUT_SYSTEM
            esGo.AddComponent<InputSystemUIInputModule>();
#else
            esGo.AddComponent<StandaloneInputModule>();
#endif
        }
    }

    // 1. MAIN MENU: STEP 1 (ID ENTRY) -> STEP 2 (TUTORIAL) -> LOADING OVERLAY
    private void BuildMainMenuFlowUI()
    {
        _menuCanvasGo = CreateOverlayCanvas("MainMenu_Flow_Canvas", 100);

        GameObject bg = CreatePanel(_menuCanvasGo.transform, "Backdrop", new Color(0.04f, 0.05f, 0.09f, 0.97f));
        StretchFull(bg.GetComponent<RectTransform>());

        GameObject header = CreatePanel(bg.transform, "Header", new Color(0.08f, 0.10f, 0.16f, 0.96f));
        SetAnchors(header, 0.12f, 0.85f, 0.88f, 0.96f);
        AddOutline(header, new Color(0.85f, 0.68f, 0.28f, 0.65f));

        CreateText(header.transform, "Title", "SHADOWS OF THE KEEP",
            38, FontStyle.Bold, new Color(0.98f, 0.86f, 0.48f), TextAnchor.MiddleCenter,
            new Vector2(0f, 0.34f), new Vector2(1f, 0.95f));
        CreateText(header.transform, "Subtitle", "2.5D Action Roguelite  |  ระบบลงทะเบียนผู้เล่นและคู่มือก่อนเข้าเกม",
            19, FontStyle.Normal, new Color(0.76f, 0.82f, 0.92f), TextAnchor.MiddleCenter,
            new Vector2(0f, 0.04f), new Vector2(1f, 0.38f));

        _step1IdPanel = CreatePanel(bg.transform, "Step1_IdPanel", new Color(0.08f, 0.10f, 0.16f, 0.98f));
        SetAnchors(_step1IdPanel, 0.22f, 0.06f, 0.78f, 0.82f);
        AddOutline(_step1IdPanel, new Color(0.85f, 0.68f, 0.28f, 0.60f));

        CreateText(_step1IdPanel.transform, "StepTag", "ขั้นตอนที่ 1 / 2  —  ลงทะเบียนรหัสผู้เล่น (Participant ID)",
            24, FontStyle.Bold, new Color(0.96f, 0.84f, 0.48f), TextAnchor.MiddleLeft,
            new Vector2(0.05f, 0.90f), new Vector2(0.95f, 0.97f));

        GameObject idBox = CreatePanel(_step1IdPanel.transform, "ActiveIDBox", new Color(0.12f, 0.15f, 0.23f, 1f));
        SetAnchors(idBox, 0.05f, 0.72f, 0.95f, 0.88f);
        AddOutline(idBox, new Color(0.32f, 0.78f, 0.56f, 0.80f));

        CreateText(idBox.transform, "IdLabel", "รหัสผู้เล่น 6 หลักของคุณ (สุ่มและจองอัตโนมัติบน Firebase)",
            18, FontStyle.Normal, new Color(0.74f, 0.84f, 0.92f), TextAnchor.MiddleLeft,
            new Vector2(0.04f, 0.58f), new Vector2(0.68f, 0.92f));

        _idDisplayText = CreateText(idBox.transform, "IdValue", "------",
            44, FontStyle.Bold, new Color(0.45f, 0.97f, 0.68f), TextAnchor.MiddleLeft,
            new Vector2(0.04f, 0.06f), new Vector2(0.65f, 0.62f));

        _regenBtn = CreateButton(idBox.transform, "RegenBtn", "สุ่ม ID ใหม่",
            new Color(0.22f, 0.31f, 0.48f, 1f), Color.white, 20,
            new Vector2(0.70f, 0.18f), new Vector2(0.96f, 0.82f),
            () => { if (!_isBusy) StartCoroutine(AutoGenerateAndReserveIdCoroutine()); });

        _statusText = CreateText(_step1IdPanel.transform, "StatusText", "กำลังตรวจสอบและจองรหัส 6 หลักกับ Firebase...",
            18, FontStyle.Normal, new Color(0.98f, 0.82f, 0.38f), TextAnchor.MiddleLeft,
            new Vector2(0.05f, 0.65f), new Vector2(0.95f, 0.71f));

        CreateText(_step1IdPanel.transform, "ResumeLabel", "กรณีหลุดจากเกม — กรอกรหัส 6 หลักเดิมเพื่อเล่นต่อ (เก็บเวลาเดิมครบ)",
            18, FontStyle.Bold, new Color(0.86f, 0.90f, 0.96f), TextAnchor.MiddleLeft,
            new Vector2(0.05f, 0.57f), new Vector2(0.95f, 0.63f));

        _resumeInput = CreateInputField(_step1IdPanel.transform, "ResumeInput", "กรอกรหัส 6 หลักเดิม เช่น 482910", 6,
            new Vector2(0.05f, 0.48f), new Vector2(0.70f, 0.56f));
        string lastSavedId = PlayerPrefs.GetString("LastParticipantID", "");
        if (!string.IsNullOrEmpty(lastSavedId)) _resumeInput.text = lastSavedId;

        CreateButton(_step1IdPanel.transform, "ResumeBtn", "โหลด ID เดิม",
            new Color(0.18f, 0.48f, 0.70f, 1f), Color.white, 19,
            new Vector2(0.73f, 0.48f), new Vector2(0.95f, 0.56f),
            () => { if (!_isBusy) StartCoroutine(ResumeParticipantCoroutine(_resumeInput.text.Trim())); });

        CreateText(_step1IdPanel.transform, "PrevLabel", "รหัสจากเกมที่แล้ว (ถ้าเคยเล่นเกมก่อนหน้านี้ — เว้นว่างได้)",
            18, FontStyle.Bold, new Color(0.86f, 0.90f, 0.96f), TextAnchor.MiddleLeft,
            new Vector2(0.05f, 0.39f), new Vector2(0.95f, 0.45f));

        _prevIdInput = CreateInputField(_step1IdPanel.transform, "PrevGameIdInput", "กรอกรหัสเกมก่อนหน้า (Optional)", 20,
            new Vector2(0.05f, 0.30f), new Vector2(0.95f, 0.38f));

        CreateText(_step1IdPanel.transform, "ModeLabel", "เลือกโหมดการทดสอบ (ตามที่ผู้วิจัยกำหนด)",
            18, FontStyle.Bold, new Color(0.86f, 0.90f, 0.96f), TextAnchor.MiddleLeft,
            new Vector2(0.05f, 0.22f), new Vector2(0.95f, 0.28f));

        _modeABtn = CreateButton(_step1IdPanel.transform, "ModeABtn", "MODE A",
            new Color(0.80f, 0.60f, 0.16f, 1f), Color.white, 22,
            new Vector2(0.05f, 0.14f), new Vector2(0.48f, 0.21f),
            () => SelectMode(true));

        _modeBBtn = CreateButton(_step1IdPanel.transform, "ModeBBtn", "MODE B",
            new Color(0.18f, 0.21f, 0.29f, 1f), Color.white, 22,
            new Vector2(0.52f, 0.14f), new Vector2(0.95f, 0.21f),
            () => SelectMode(false));
        SelectMode(true);

        // Next -> Step 2 (Tutorial)
        CreateButton(_step1IdPanel.transform, "NextToTutorialBtn", "ถัดไป: ดูวิธีเล่นก่อนเริ่มเกม (NEXT →)",
            new Color(0.16f, 0.62f, 0.38f, 1f), Color.white, 24,
            new Vector2(0.05f, 0.03f), new Vector2(0.95f, 0.11f),
            OnProceedToTutorialClicked);

        _step2TutorialPanel = CreatePanel(bg.transform, "Step2_TutorialPanel", new Color(0.08f, 0.10f, 0.16f, 0.99f));
        SetAnchors(_step2TutorialPanel, 0.12f, 0.06f, 0.88f, 0.82f);
        AddOutline(_step2TutorialPanel, new Color(0.38f, 0.68f, 0.98f, 0.70f));

        PopulateTutorialContent(_step2TutorialPanel.transform, true, false);
        _step2TutorialPanel.SetActive(false);

        _loadingOverlayPanel = BuildLoadingPanel(bg.transform);
        _loadingOverlayPanel.SetActive(false);
    }

    private void OnProceedToTutorialClicked()
    {
        if (_isBusy || !_isIdReserved || string.IsNullOrEmpty(CurrentParticipantId))
        {
            SetStatus("กรุณารอจองรหัส 6 หลักให้เสร็จก่อนไปขั้นตอนถัดไป", new Color(0.98f, 0.55f, 0.35f));
            return;
        }
        if (_modeACompleted && _modeBCompleted) { SetStatus("รหัสนี้เล่นครบทั้ง 2 โหมดแล้ว กรุณากด สุ่ม ID ใหม่", new Color(0.98f, 0.42f, 0.42f)); return; } _step1IdPanel.SetActive(false);
        _step2TutorialPanel.SetActive(true);
    }

    private void OnBackToIdStepClicked()
    {
        _step2TutorialPanel.SetActive(false);
        _step1IdPanel.SetActive(true);
    }

    private void PopulateTutorialContent(Transform parent, bool isPreGameStep, bool isInGamePopup)
    {
        string heading = isPreGameStep
            ? "ขั้นตอนที่ 2 / 2  —  วิธีเล่นและปุ่มควบคุม (อ่านจบแล้วกดเริ่มเกมด้านล่าง)"
            : "คู่มือวิธีเล่นและปุ่มควบคุม (How to Play)";

        CreateText(parent, "TutHeader", heading,
            24, FontStyle.Bold, new Color(0.50f, 0.82f, 1.0f), TextAnchor.MiddleLeft,
            new Vector2(0.04f, 0.90f), new Vector2(0.84f, 0.97f));

        var rows = new (string key, Color badgeColor, string title, string desc)[]
        {
            ("W A S D",  new Color(0.24f, 0.42f, 0.68f), "เคลื่อนที่ (Move)",              "เดินหลบมอนสเตอร์และจัดตำแหน่งรอบห้อง (ใช้ปุ่มลูกศรได้)"),
            ("คลิกซ้าย",  new Color(0.75f, 0.30f, 0.25f), "โจมตีปกติ (Melee Slash)",       "ฟันดาบไปทางเคอร์เซอร์เมาส์ โจมตีมอนสเตอร์ในระยะด้านหน้า"),
            ("Q",        new Color(0.68f, 0.42f, 0.16f), "สกิล Sword Wave",                "ยิงคลื่นดาบพุ่งทะลุมอนสเตอร์เป็นเส้นตรงไปทางเคอร์เซอร์เมาส์"),
            ("E",        new Color(0.46f, 0.26f, 0.72f), "สกิล Shadow Dash",               "พุ่งตัวหลบการโจมตีอย่างรวดเร็ว พร้อมอมตะชั่วขณะ"),
            ("R",        new Color(0.18f, 0.58f, 0.48f), "สกิล Blade Tempest",             "พายุหมุนดาบรอบตัว 1.5 วิ (เดินได้) ฟันรัวพร้อมดูดเลือดทุกตัวที่โดน"),
            ("1 / 2",    new Color(0.68f, 0.22f, 0.40f), "ยาฟื้นฟู (HP / MP Potion)",      "[1] ฟื้นเลือด +45 HP  |  [2] ฟื้นมานา +40 MP (มีจำกัดต่อห้อง)"),
            ("TAB / F1", new Color(0.30f, 0.36f, 0.46f), "สเตตัส & เปิดคู่มือระหว่างเล่น",   "[TAB] ดูค่าสเตตัสตัวละคร  |  [F1] เปิด/ปิดหน้าคู่มือนี้ได้ทุกเมื่อ")
        };

        float topY = 0.88f;
        float rowH = 0.084f;
        float gap = 0.011f;

        for (int i = 0; i < rows.Length; i++)
        {
            float yMax = topY - i * (rowH + gap);
            float yMin = yMax - rowH;

            GameObject rowBg = CreatePanel(parent, "Row_" + i, new Color(0.12f, 0.15f, 0.22f, 0.96f));
            SetAnchors(rowBg, 0.04f, yMin, 0.96f, yMax);

            GameObject badge = CreatePanel(rowBg.transform, "Badge", rows[i].badgeColor);
            SetAnchors(badge, 0.015f, 0.14f, 0.17f, 0.86f);
            AddOutline(badge, new Color(1f, 1f, 1f, 0.35f));

            CreateText(badge.transform, "KeyTxt", rows[i].key,
                19, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one);

            CreateText(rowBg.transform, "TitleTxt", rows[i].title,
                20, FontStyle.Bold, new Color(0.98f, 0.92f, 0.72f), TextAnchor.MiddleLeft,
                new Vector2(0.19f, 0.50f), new Vector2(0.98f, 0.95f));

            CreateText(rowBg.transform, "DescTxt", rows[i].desc,
                17, FontStyle.Normal, new Color(0.80f, 0.85f, 0.92f), TextAnchor.MiddleLeft,
                new Vector2(0.19f, 0.05f), new Vector2(0.98f, 0.52f));
        }

        GameObject objBox = CreatePanel(parent, "ObjectiveBox", new Color(0.15f, 0.12f, 0.22f, 1f));
        SetAnchors(objBox, 0.04f, isPreGameStep ? 0.13f : 0.04f, 0.96f, isPreGameStep ? 0.21f : 0.16f);
        AddOutline(objBox, new Color(0.75f, 0.52f, 0.98f, 0.65f));

        CreateText(objBox.transform, "ObjTxt",
            "เป้าหมาย: กำจัดมอนสเตอร์ในห้องให้ครบ -> เดินเข้าประตูวาร์ปสีม่วงกลางห้องเพื่อไปด่านถัดไป (Room 1 -> 2 -> 3 -> Boss) | หากเลือดหมดจะเริ่มห้องเดิมใหม่ทันที",
            17, FontStyle.Bold, new Color(0.92f, 0.88f, 1.0f), TextAnchor.MiddleCenter,
            new Vector2(0.02f, 0.05f), new Vector2(0.98f, 0.95f));

        if (isPreGameStep)
        {
            CreateButton(parent, "BackBtn", "← ย้อนกลับแก้ ID",
                new Color(0.22f, 0.26f, 0.36f, 1f), Color.white, 20,
                new Vector2(0.04f, 0.025f), new Vector2(0.28f, 0.105f),
                OnBackToIdStepClicked);

            CreateButton(parent, "StartGameFinalBtn", "▶  พร้อมแล้ว! เริ่มเล่นเกม (START GAME)",
                new Color(0.16f, 0.64f, 0.38f, 1f), Color.white, 25,
                new Vector2(0.31f, 0.025f), new Vector2(0.96f, 0.105f),
                OnStartGameClicked);
        }
        else if (isInGamePopup)
        {
            CreateButton(parent, "CloseTutBtn", "✕ ปิด (F1)",
                new Color(0.72f, 0.22f, 0.22f, 1f), Color.white, 19,
                new Vector2(0.84f, 0.90f), new Vector2(0.96f, 0.97f),
                ToggleInGameTutorial);
        }
    }

    // 2. ANIMATED LOADING SCREEN
    private GameObject BuildLoadingPanel(Transform parent)
    {
        GameObject panel = CreatePanel(parent, "LoadingPanel", new Color(0.04f, 0.05f, 0.09f, 0.99f));
        StretchFull(panel.GetComponent<RectTransform>());

        GameObject frame = CreatePanel(panel.transform, "LoadingFrame", new Color(0.08f, 0.10f, 0.16f, 0.96f));
        SetAnchors(frame, 0.18f, 0.26f, 0.82f, 0.74f);
        AddOutline(frame, new Color(0.85f, 0.68f, 0.28f, 0.65f));

        CreateText(frame.transform, "LoadingTitle", "กำลังเตรียมสนามรบ... (LOADING)",
            34, FontStyle.Bold, new Color(0.98f, 0.86f, 0.48f), TextAnchor.MiddleCenter,
            new Vector2(0.05f, 0.68f), new Vector2(0.95f, 0.90f));

        GameObject barTrack = CreatePanel(frame.transform, "BarTrack", new Color(0.14f, 0.17f, 0.25f, 1f));
        SetAnchors(barTrack, 0.08f, 0.45f, 0.92f, 0.58f);
        AddOutline(barTrack, new Color(0.85f, 0.68f, 0.28f, 0.55f));

        GameObject barFill = CreatePanel(barTrack.transform, "BarFill", new Color(0.22f, 0.76f, 0.48f, 1f));
        RectTransform fRt = barFill.GetComponent<RectTransform>();
        fRt.anchorMin = new Vector2(0f, 0f);
        fRt.anchorMax = new Vector2(0f, 1f);
        fRt.offsetMin = Vector2.zero;
        fRt.offsetMax = Vector2.zero;
        _loadingFillBar = barFill.GetComponent<Image>();

        _loadingPercentText = CreateText(barTrack.transform, "PercentTxt", "0%",
            20, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one);

        string tip = LoadingTips[UnityEngine.Random.Range(0, LoadingTips.Length)];
        _loadingTipText = CreateText(frame.transform, "TipTxt", tip,
            20, FontStyle.Italic, new Color(0.78f, 0.86f, 0.96f), TextAnchor.MiddleCenter,
            new Vector2(0.06f, 0.14f), new Vector2(0.94f, 0.38f));

        string pidLabel = string.IsNullOrEmpty(CurrentParticipantId)
            ? PlayerPrefs.GetString("ParticipantID", "------")
            : CurrentParticipantId;
        CreateText(frame.transform, "PidFooter", $"Participant ID: {pidLabel}  |  Mode {(IsAdaptiveMode ? "A" : "B")}",
            16, FontStyle.Normal, new Color(0.55f, 0.62f, 0.72f), TextAnchor.MiddleCenter,
            new Vector2(0.05f, 0.03f), new Vector2(0.95f, 0.12f));

        return panel;
    }

    private void BuildStandaloneLoadingSceneUI()
    {
        _menuCanvasGo = CreateOverlayCanvas("Loading_Scene_Canvas", 120);
        _loadingOverlayPanel = BuildLoadingPanel(_menuCanvasGo.transform);
    }

    private IEnumerator LoadSceneWithProgressBar(string targetScene)
    {
        if (_loadingOverlayPanel != null) _loadingOverlayPanel.SetActive(true);

        AsyncOperation op = SceneManager.LoadSceneAsync(targetScene);
        op.allowSceneActivation = false;

        float elapsed = 0f;
        float minDuration = 1.6f;

        while (!op.isDone)
        {
            elapsed += Time.unscaledDeltaTime;
            float realProg = Mathf.Clamp01(op.progress / 0.9f);
            float smoothProg = Mathf.Min(realProg, Mathf.Clamp01(elapsed / minDuration));

            if (_loadingFillBar != null)
            {
                RectTransform rt = _loadingFillBar.GetComponent<RectTransform>();
                rt.anchorMax = new Vector2(smoothProg, 1f);
            }
            if (_loadingPercentText != null)
            {
                _loadingPercentText.text = $"{Mathf.RoundToInt(smoothProg * 100f)}%";
            }

            if (op.progress >= 0.9f && elapsed >= minDuration)
            {
                op.allowSceneActivation = true;
            }
            yield return null;
        }
    }

    // 3. IN-GAME [?] BUTTON & [F1] TUTORIAL MODAL
    private void BuildInGameHelpButtonAndModal()
    {
        _inGameHelpCanvasGo = CreateOverlayCanvas("InGame_Tutorial_Canvas", 150);

        CreateButton(_inGameHelpCanvasGo.transform, "HelpToggleBtn", "? วิธีเล่น (F1)",
            new Color(0.10f, 0.15f, 0.25f, 0.90f), new Color(0.96f, 0.86f, 0.52f), 18,
            new Vector2(0.855f, 0.025f), new Vector2(0.985f, 0.080f),
            ToggleInGameTutorial);

        _inGameTutorialModal = CreatePanel(_inGameHelpCanvasGo.transform, "TutorialModalBackdrop", new Color(0f, 0f, 0f, 0.74f));
        StretchFull(_inGameTutorialModal.GetComponent<RectTransform>());

        GameObject card = CreatePanel(_inGameTutorialModal.transform, "TutorialModalCard", new Color(0.08f, 0.10f, 0.16f, 0.99f));
        SetAnchors(card, 0.16f, 0.08f, 0.84f, 0.92f);
        AddOutline(card, new Color(0.45f, 0.75f, 1.0f, 0.80f));

        PopulateTutorialContent(card.transform, false, true);
        _inGameTutorialModal.SetActive(false);
    }

    private void ToggleInGameTutorial()
    {
        if (_inGameTutorialModal == null) return;
        bool next = !_inGameTutorialModal.activeSelf;
        _inGameTutorialModal.SetActive(next);
        Time.timeScale = next ? 0f : 1f;
    }

    // 4. END SCENE (VICTORY + BLINDED SUMMARY + BIG ID COPY FOR FSS SURVEY)
    private void BuildEndSceneUI()
    {
        _endSceneCanvasGo = CreateOverlayCanvas("EndScene_Victory_Canvas", 120);

        GameObject bg = CreatePanel(_endSceneCanvasGo.transform, "Backdrop", new Color(0.04f, 0.05f, 0.09f, 0.97f));
        StretchFull(bg.GetComponent<RectTransform>());

        GameObject card = CreatePanel(bg.transform, "VictoryCard", new Color(0.08f, 0.10f, 0.16f, 0.98f));
        SetAnchors(card, 0.20f, 0.08f, 0.80f, 0.92f);
        AddOutline(card, new Color(0.92f, 0.74f, 0.30f, 0.80f));

        CreateText(card.transform, "VictoryTitle", "VICTORY!  —  เคลียร์ครบทุกด่านแล้ว",
            40, FontStyle.Bold, new Color(0.99f, 0.86f, 0.42f), TextAnchor.MiddleCenter,
            new Vector2(0.05f, 0.82f), new Vector2(0.95f, 0.95f));

        CreateText(card.transform, "VictorySub", "ขอบคุณที่เข้าร่วมการทดสอบเกม — ระบบบันทึกข้อมูลการเล่นเรียบร้อยแล้ว",
            20, FontStyle.Normal, new Color(0.72f, 0.94f, 0.80f), TextAnchor.MiddleCenter,
            new Vector2(0.05f, 0.74f), new Vector2(0.95f, 0.83f));

        GameObject idBox = CreatePanel(card.transform, "EndIdBox", new Color(0.12f, 0.15f, 0.24f, 1f));
        SetAnchors(idBox, 0.08f, 0.48f, 0.92f, 0.71f);
        AddOutline(idBox, new Color(0.35f, 0.82f, 0.58f, 0.85f));

        CreateText(idBox.transform, "IdHint", "รหัสผู้เล่นของคุณ (นำไปกรอกในแบบสอบถามหลังการเล่น)",
            19, FontStyle.Bold, new Color(0.82f, 0.88f, 0.96f), TextAnchor.MiddleCenter,
            new Vector2(0.04f, 0.68f), new Vector2(0.96f, 0.93f));

        CreateText(idBox.transform, "BigId", CurrentParticipantId,
            54, FontStyle.Bold, new Color(0.45f, 0.98f, 0.68f), TextAnchor.MiddleCenter,
            new Vector2(0.04f, 0.24f), new Vector2(0.65f, 0.72f));

        CreateButton(idBox.transform, "CopyIdBtn", "คัดลอกรหัส ID",
            new Color(0.18f, 0.52f, 0.74f, 1f), Color.white, 21,
            new Vector2(0.66f, 0.26f), new Vector2(0.94f, 0.64f),
            () =>
            {
                GUIUtility.systemCopyBuffer = CurrentParticipantId;
                if (_copyFeedbackText != null) _copyFeedbackText.text = "✓ คัดลอกรหัส " + CurrentParticipantId + " ลงคลิปบอร์ดแล้ว!";
            });

        _copyFeedbackText = CreateText(idBox.transform, "CopyFeedback", "",
            16, FontStyle.Bold, new Color(0.55f, 0.95f, 0.72f), TextAnchor.MiddleCenter,
            new Vector2(0.04f, 0.03f), new Vector2(0.96f, 0.22f));

        GameObject statBox = CreatePanel(card.transform, "StatsStrip", new Color(0.11f, 0.13f, 0.20f, 1f));
        SetAnchors(statBox, 0.08f, 0.28f, 0.92f, 0.44f);
        AddOutline(statBox, new Color(0.45f, 0.55f, 0.75f, 0.45f));

        int mins = Mathf.FloorToInt(CumulativePlayTimeSec / 60f);
        int secs = Mathf.FloorToInt(CumulativePlayTimeSec % 60f);
        string modeLabel = IsAdaptiveMode ? "Mode A" : "Mode B";

        CreateText(statBox.transform, "StatLeft", $"โหมดที่เล่น\n{modeLabel}",
            22, FontStyle.Bold, new Color(0.96f, 0.86f, 0.52f), TextAnchor.MiddleCenter,
            new Vector2(0.02f, 0.08f), new Vector2(0.34f, 0.92f));

        CreateText(statBox.transform, "StatMid", $"เวลาเล่นรวม\n{mins:00}:{secs:00}",
            22, FontStyle.Bold, new Color(0.52f, 0.86f, 1.0f), TextAnchor.MiddleCenter,
            new Vector2(0.34f, 0.08f), new Vector2(0.66f, 0.92f));

        CreateText(statBox.transform, "StatRight", "สถานะข้อมูล\nบันทึกแล้ว ✓",
            22, FontStyle.Bold, new Color(0.45f, 0.95f, 0.65f), TextAnchor.MiddleCenter,
            new Vector2(0.66f, 0.08f), new Vector2(0.98f, 0.92f));

        // Next Step Note
        CreateText(card.transform, "SurveyNote",
            "ขั้นตอนถัดไป: กรุณานำรหัส 6 หลักด้านบนไปกรอกในแบบสอบถามประเมินประสบการณ์การเล่น (FSS)",
            19, FontStyle.Bold, new Color(0.94f, 0.90f, 0.78f), TextAnchor.MiddleCenter,
            new Vector2(0.08f, 0.18f), new Vector2(0.92f, 0.26f));

        CreateButton(card.transform, "BackToMenuBtn", "กลับหน้าหลัก (MAIN MENU)",
            new Color(0.20f, 0.48f, 0.72f, 1f), Color.white, 22,
            new Vector2(0.08f, 0.05f), new Vector2(0.58f, 0.14f),
            () => { if (IsAdaptiveMode) PlayerPrefs.SetInt("SkipPartAOnce", 1); CumulativePlayTimeSec = 0f; SceneManager.LoadScene("MainMenu"); });

        Button dynQuit = CreateButton(card.transform, "QuitBtn", "ออกจากเกม (QUIT)",
            new Color(0.68f, 0.22f, 0.24f, 1f), Color.white, 22,
            new Vector2(0.62f, 0.05f), new Vector2(0.92f, 0.14f),
            () => { Application.Quit(); }); if (IsAdaptiveMode && GameObject.Find("QuitBtn") != null) GameObject.Find("QuitBtn").SetActive(false);
    }

    private IEnumerator AutoGenerateAndReserveIdCoroutine()
    {
        _isBusy = true;
        _isIdReserved = false;
        SetStatus("กำลังสุ่มและเช็กรหัส 6 หลักบน Firebase...", new Color(0.98f, 0.82f, 0.38f));

        string candidate = "";
        bool reservedOk = false;

        for (int attempt = 0; attempt < 10; attempt++)
        {
            candidate = UnityEngine.Random.Range(100000, 1000000).ToString();
            string checkUrl = $"{FirebaseBaseUrl}/participants/{candidate}/_reserved.json";

            using (UnityWebRequest getReq = UnityWebRequest.Get(checkUrl))
            {
                getReq.timeout = 8;
                yield return getReq.SendWebRequest();

                if (getReq.result != UnityWebRequest.Result.Success)
                {
                    SetStatus($"เชื่อมต่อ Firebase ไม่ได้ ({getReq.error}) — ใช้รหัส {candidate} แบบออฟไลน์", new Color(0.98f, 0.55f, 0.35f));
                    reservedOk = true;
                    break;
                }

                string body = (getReq.downloadHandler.text ?? "").Trim();
                if (body == "null" || string.IsNullOrEmpty(body))
                {
                    string nowIso = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
                    string payload = $"{{\"status\":\"reserved\",\"reservedAt\":\"{nowIso}\",\"prevGameId\":\"\"}}";
                    using (UnityWebRequest putReq = CreatePutRequest(checkUrl, payload))
                    {
                        yield return putReq.SendWebRequest();
                        if (putReq.result == UnityWebRequest.Result.Success)
                        {
                            reservedOk = true;
                            SetStatus($"จองรหัส {candidate} บน Firebase สำเร็จ (พร้อมไปขั้นตอนถัดไป)", new Color(0.45f, 0.95f, 0.62f));
                            break;
                        }
                    }
                }
            }
        }

        if (reservedOk && !string.IsNullOrEmpty(candidate))
        {
            CurrentParticipantId = candidate;
            IsResumedSession = false;
            CumulativePlayTimeSec = 0f;
            _isIdReserved = true;
            if (_idDisplayText != null) _idDisplayText.text = candidate;
            ApplyModeLocks(false, false); SaveLocalParticipantIdLog(candidate, "NEW_RESERVED");
        }
        else
        {
            SetStatus("ไม่สามารถจองรหัสได้ กรุณากด 'สุ่ม ID ใหม่'", new Color(0.98f, 0.40f, 0.40f));
        }

        _isBusy = false;
    }

    private IEnumerator ResumeParticipantCoroutine(string inputId)
    {
        if (string.IsNullOrEmpty(inputId) || inputId.Length != 6)
        {
            SetStatus("กรุณากรอกรหัส 6 หลักเดิมให้ครบ (ตัวเลข 6 หลัก)", new Color(0.98f, 0.42f, 0.42f));
            yield break;
        }

        _isBusy = true;
        SetStatus($"กำลังดึงข้อมูลรหัส {inputId} จาก Firebase...", new Color(0.98f, 0.82f, 0.38f));

        string url = $"{FirebaseBaseUrl}/participants/{inputId}.json";
        using (UnityWebRequest getReq = UnityWebRequest.Get(url))
        {
            getReq.timeout = 8;
            yield return getReq.SendWebRequest();

            if (getReq.result == UnityWebRequest.Result.Success)
            {
                string body = (getReq.downloadHandler.text ?? "").Trim();
                if (body == "null" || string.IsNullOrEmpty(body))
                {
                    SetStatus($"ไม่พบรหัส {inputId} ใน Firebase กรุณาตรวจสอบอีกครั้ง", new Color(0.98f, 0.42f, 0.42f));
                }
                else
                {
                    CurrentParticipantId = inputId;
                    IsResumedSession = true;
                    _isIdReserved = true;
                    CumulativePlayTimeSec = ExtractFloatFromJson(body, "totalPlayTimeSec", PlayerPrefs.GetFloat("CumulativePlayTime_" + inputId, 0f));
                    if (_idDisplayText != null) _idDisplayText.text = inputId;
                    bool aDone = IsConditionCompletedInJson(body, "Adaptive");
                    bool bDone = IsConditionCompletedInJson(body, "NonAdaptive");
                    ApplyModeLocks(aDone, bDone);
                    if (aDone && bDone)
                        SetStatus($"รหัส {inputId} เล่นครบทั้ง Mode A และ Mode B แล้ว! กด 'สุ่ม ID ใหม่' สำหรับคนถัดไป", new Color(0.98f, 0.45f, 0.45f));
                    else if (aDone)
                        SetStatus($"โหลดรหัส {inputId} สำเร็จ — Mode A ครบแล้ว (ล็อกไว้) เหลือเล่น Mode B", new Color(0.45f, 0.90f, 1.0f));
                    else if (bDone)
                        SetStatus($"โหลดรหัส {inputId} สำเร็จ — Mode B ครบแล้ว (ล็อกไว้) เหลือเล่น Mode A", new Color(0.45f, 0.90f, 1.0f));
                    else
                        SetStatus($"โหลดรหัสเดิม {inputId} สำเร็จ (เวลาสะสมเดิม {CumulativePlayTimeSec:F0} วิ)", new Color(0.45f, 0.90f, 1.0f));
                    SaveLocalParticipantIdLog(inputId, "RESUMED");
                }
            }
            else
            {
                CurrentParticipantId = inputId;
                IsResumedSession = true;
                _isIdReserved = true;
                CumulativePlayTimeSec = PlayerPrefs.GetFloat("CumulativePlayTime_" + inputId, 0f);
                if (_idDisplayText != null) _idDisplayText.text = inputId;
                SetStatus($"โหลดรหัส {inputId} จากเครื่อง (ออฟไลน์)", new Color(0.98f, 0.72f, 0.35f));
            }
        }

        _isBusy = false;
    }

    private bool IsConditionCompletedInJson(string json, string condKey)
    {
        int cIdx = json.IndexOf("\"" + condKey + "\"", StringComparison.OrdinalIgnoreCase);
        if (cIdx < 0) return false;
        string otherKey = condKey == "Adaptive" ? "\"NonAdaptive\"" : "\"Adaptive\"";
        int nextIdx = json.IndexOf(otherKey, cIdx + condKey.Length, StringComparison.OrdinalIgnoreCase);
        string slice = nextIdx > cIdx ? json.Substring(cIdx, nextIdx - cIdx) : json.Substring(cIdx);
        return slice.IndexOf("\"fss\"", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private void ApplyModeLocks(bool modeADone, bool modeBDone)
    {
        _modeACompleted = modeADone;
        _modeBCompleted = modeBDone;

        if (_modeABtn != null)
        {
            _modeABtn.interactable = !modeADone;
            Text lbl = _modeABtn.GetComponentInChildren<Text>();
            if (lbl != null) lbl.text = modeADone ? "MODE A (ครบแล้ว ✓)" : "MODE A";
        }
        if (_modeBBtn != null)
        {
            _modeBBtn.interactable = !modeBDone;
            Text lbl = _modeBBtn.GetComponentInChildren<Text>();
            if (lbl != null) lbl.text = modeBDone ? "MODE B (ครบแล้ว ✓)" : "MODE B";
        }

        bool lockRegen = (modeADone != modeBDone); if (_regenBtn != null) { _regenBtn.interactable = !lockRegen; Image rImg = _regenBtn.GetComponent<Image>(); if (rImg != null) rImg.color = lockRegen ? new Color(0.24f, 0.25f, 0.28f, 0.65f) : new Color(0.22f, 0.31f, 0.48f, 1f); Text rLbl = _regenBtn.GetComponentInChildren<Text>(); if (rLbl != null) rLbl.text = lockRegen ? "ใช้ ID เดิมเล่นต่อ" : "สุ่ม ID ใหม่"; } if (modeADone && !modeBDone) SelectMode(false);
        else if (!modeADone && modeBDone) SelectMode(true);
        else SelectMode(IsAdaptiveMode);
    }

    private void SelectMode(bool adaptive)
    {
        IsAdaptiveMode = adaptive;
        if (_modeABtn != null)
            _modeABtn.GetComponent<Image>().color = _modeACompleted ? new Color(0.24f, 0.25f, 0.28f, 0.65f) : (adaptive ? new Color(0.80f, 0.60f, 0.16f, 1f) : new Color(0.18f, 0.21f, 0.29f, 1f));
        if (_modeBBtn != null)
            _modeBBtn.GetComponent<Image>().color = _modeBCompleted ? new Color(0.24f, 0.25f, 0.28f, 0.65f) : (!adaptive ? new Color(0.80f, 0.60f, 0.16f, 1f) : new Color(0.18f, 0.21f, 0.29f, 1f));
    }

    private void OnStartGameClicked()
    {
        PreviousGameId = _prevIdInput != null ? _prevIdInput.text.Trim() : "";
        PlayerPrefs.SetString("ParticipantID", CurrentParticipantId); PlayerPrefs.SetString("ParticipantId", CurrentParticipantId); PlayerPrefs.SetString("participant_id", CurrentParticipantId);
        PlayerPrefs.SetString("LastParticipantID", CurrentParticipantId);
        PlayerPrefs.SetString("PrevGameID", PreviousGameId);
        PlayerPrefs.SetInt("IsAdaptiveMode", IsAdaptiveMode ? 1 : 0);
        PlayerPrefs.SetInt("IsResumedSession", IsResumedSession ? 1 : 0);
        PlayerPrefs.Save();

        StartCoroutine(SavePrevIdAndLoadRoom1());
    }

    private IEnumerator SavePrevIdAndLoadRoom1()
    {
        if (!string.IsNullOrEmpty(PreviousGameId))
        {
            string url = $"{FirebaseBaseUrl}/participants/{CurrentParticipantId}/_reserved/prevGameId.json";
            using (UnityWebRequest putReq = CreatePutRequest(url, $"\"{EscapeJson(PreviousGameId)}\""))
            {
                yield return putReq.SendWebRequest();
            }
            SaveLocalParticipantIdLog(CurrentParticipantId, "START_PREV=" + PreviousGameId);
        }

        SyncStateToDDAManager();
        yield return StartCoroutine(LoadSceneWithProgressBar("Room1"));
    }

    private void SyncStateToDDAManager()
    {
        if (GameplayTelemetry.Instance != null)
        {
            GameplayTelemetry.Instance.participantId = CurrentParticipantId;
            GameplayTelemetry.Instance.previousParticipantId = PreviousGameId;
            GameplayTelemetry.Instance.condition = IsAdaptiveMode
                ? ExperimentCondition.Adaptive
                : ExperimentCondition.NonAdaptive;
        }
        MonoBehaviour[] all = FindObjectsCompat<MonoBehaviour>();
        foreach (MonoBehaviour mb in all)
        {
            if (mb == null) continue;
            string tn = mb.GetType().Name;
            if (tn != "DDAManager" && tn != "GameplayTelemetry" && tn != "FirebaseUploader") continue;
            Type t = mb.GetType();
            TrySetMember(t, mb, "participantId", CurrentParticipantId);
            TrySetMember(t, mb, "ParticipantId", CurrentParticipantId); TrySetMember(t, mb, "participantID", CurrentParticipantId); TrySetMember(t, mb, "playerId", CurrentParticipantId); TrySetMember(t, mb, "sessionId", CurrentParticipantId);
            TrySetMember(t, mb, "prevGameId", PreviousGameId);
            TrySetMember(t, mb, "PrevGameId", PreviousGameId);
            TrySetMember(t, mb, "isAdaptive", IsAdaptiveMode);
            TrySetMember(t, mb, "IsAdaptive", IsAdaptiveMode);
            TrySetMember(t, mb, "enableDDA", IsAdaptiveMode);
            TrySetMember(t, mb, "isResumed", IsResumedSession);
            TrySetMember(t, mb, "cumulativePlayTimeSec", CumulativePlayTimeSec);
        }
    }

    private void SaveLocalParticipantIdLog(string pid, string tag)
    {
        try
        {
            string dir = Path.Combine(Application.persistentDataPath, "Telemetry");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            string logPath = Path.Combine(dir, "ParticipantIDs.txt");
            string lastPath = Path.Combine(dir, "LastParticipantID.txt");
            string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | ID={pid} | Mode={(IsAdaptiveMode ? "A" : "B")} | {tag}";
            File.AppendAllText(logPath, line + Environment.NewLine, Encoding.UTF8);
            File.WriteAllText(lastPath, pid, Encoding.UTF8);
        }
        catch { }
    }

    private GameObject CreateOverlayCanvas(string name, int sortOrder)
    {
        GameObject go = new GameObject(name);
        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortOrder;

        CanvasScaler scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        go.AddComponent<GraphicRaycaster>();
        return go;
    }

    private void SetStatus(string msg, Color color)
    {
        if (_statusText == null) return;
        _statusText.text = msg;
        _statusText.color = color;
    }

    private GameObject CreatePanel(Transform parent, string name, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        Image img = go.AddComponent<Image>();
        img.color = color;
        return go;
    }

    private void SetAnchors(GameObject go, float minX, float minY, float maxX, float maxY)
    {
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(minX, minY);
        rt.anchorMax = new Vector2(maxX, maxY);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private Text CreateText(Transform parent, string name, string text, int size, FontStyle style,
        Color color, TextAnchor align, Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        Text t = go.AddComponent<Text>();
        t.font = _uiFont;
        t.text = text;
        t.fontSize = size;
        t.fontStyle = style;
        t.color = color;
        t.alignment = align;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }

    private Button CreateButton(Transform parent, string name, string label, Color bgColor, Color textColor,
        int fontSize, Vector2 anchorMin, Vector2 anchorMax, Action onClick)
    {
        GameObject go = CreatePanel(parent, name, bgColor);
        SetAnchors(go, anchorMin.x, anchorMin.y, anchorMax.x, anchorMax.y);
        AddOutline(go, new Color(1f, 1f, 1f, 0.25f));

        Button btn = go.AddComponent<Button>();
        btn.onClick.AddListener(() => onClick?.Invoke());
        CreateText(go.transform, "Label", label, fontSize, FontStyle.Bold, textColor, TextAnchor.MiddleCenter,
            Vector2.zero, Vector2.one);
        return btn;
    }

    private InputField CreateInputField(Transform parent, string name, string placeholder, int charLimit,
        Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject go = CreatePanel(parent, name, new Color(0.14f, 0.17f, 0.25f, 1f));
        SetAnchors(go, anchorMin.x, anchorMin.y, anchorMax.x, anchorMax.y);
        AddOutline(go, new Color(0.45f, 0.55f, 0.72f, 0.60f));

        InputField input = go.AddComponent<InputField>();
        input.characterLimit = charLimit;

        Text ph = CreateText(go.transform, "Placeholder", placeholder, 18, FontStyle.Italic,
            new Color(0.55f, 0.62f, 0.72f), TextAnchor.MiddleLeft,
            new Vector2(0.03f, 0.05f), new Vector2(0.97f, 0.95f));
        Text txt = CreateText(go.transform, "Text", "", 20, FontStyle.Bold,
            Color.white, TextAnchor.MiddleLeft,
            new Vector2(0.03f, 0.05f), new Vector2(0.97f, 0.95f));

        input.placeholder = ph;
        input.textComponent = txt;
        return input;
    }

    private void AddOutline(GameObject go, Color color)
    {
        Outline o = go.AddComponent<Outline>();
        o.effectColor = color;
        o.effectDistance = new Vector2(2f, -2f);
    }

    private void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private UnityWebRequest CreatePutRequest(string url, string json)
    {
        UnityWebRequest req = new UnityWebRequest(url, "PUT");
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
        req.uploadHandler = new UploadHandlerRaw(bodyRaw);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        req.timeout = 8;
        return req;
    }

    private float ExtractFloatFromJson(string json, string key, float fallback)
    {
        int idx = json.IndexOf("\"" + key + "\"", StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return fallback;
        int colon = json.IndexOf(':', idx);
        if (colon < 0) return fallback;
        int start = colon + 1;
        while (start < json.Length && (json[start] == ' ' || json[start] == '"')) start++;
        int end = start;
        while (end < json.Length && (char.IsDigit(json[end]) || json[end] == '.')) end++;
        if (float.TryParse(json.Substring(start, end - start), System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out float val))
            return val;
        return fallback;
    }

    private string EscapeJson(string s)
    {
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    private void TrySetMember(Type t, object target, string name, object value)
    {
        FieldInfo f = t.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (f != null && value != null && f.FieldType.IsAssignableFrom(value.GetType()))
        {
            f.SetValue(target, value);
            return;
        }
        PropertyInfo p = t.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (p != null && p.CanWrite && value != null && p.PropertyType.IsAssignableFrom(value.GetType()))
        {
            p.SetValue(target, value);
        }
    }

    private T FindFirstCompat<T>() where T : UnityEngine.Object
    {
#if UNITY_6000_0_OR_NEWER
        return UnityEngine.Object.FindFirstObjectByType<T>();
#else
        return UnityEngine.Object.FindObjectOfType<T>();
#endif
    }

    private T[] FindObjectsCompat<T>() where T : UnityEngine.Object
    {
#if UNITY_6000_0_OR_NEWER
        return UnityEngine.Object.FindObjectsByType<T>(FindObjectsSortMode.None);
#else
        return UnityEngine.Object.FindObjectsOfType<T>();
#endif
    }
}
