#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif
using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class InGameBackgroundConsentUI : MonoBehaviour
{
    public const string FirebaseBaseUrl = "https://projectrougelite-default-rtdb.asia-southeast1.firebasedatabase.app";
    private static InGameBackgroundConsentUI _instance;

    private Font _uiFont;
    private GameObject _canvasGo;
    private GameObject _modalRoot;
    private GameObject _openBtnGo;

    private bool _consentAgreed = false;
    private Image _consentBtnBg;
    private Text _consentBtnTxt;

    private InputField _ageInput;
    private string _selectedGender = "";
    private readonly Image[] _genderBgs = new Image[3];
    private int _generalExp = 0;
    private readonly Image[] _genExpBgs = new Image[5];
    private int _actionExp = 0;
    private readonly Image[] _actExpBgs = new Image[5];

    private Text _statusText; private Button _partASubmitBtn;
    private bool _isSubmitting = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (_instance != null) return;
        GameObject go = new GameObject("InGameBackgroundConsentUI");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<InGameBackgroundConsentUI>();
    }

    private void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        DontDestroyOnLoad(gameObject);
        LoadFont();
        SceneManager.sceneLoaded += OnSceneLoaded;
        CheckScene(SceneManager.GetActiveScene().name);
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        CheckScene(scene.name);
    }

    private void CheckScene(string sceneName)
    {
        if (_canvasGo != null) Destroy(_canvasGo);
        string lower = sceneName.ToLowerInvariant();
        if (lower.Contains("menu") || lower.Contains("start") || lower.Contains("title"))
        {
            _consentAgreed = false;
            _selectedGender = "";
            _generalExp = 0;
            _actionExp = 0;
            _isSubmitting = false;
            EnsureEventSystem();
            BuildBackgroundUI();
        }
    }

    private void BuildBackgroundUI()
    {
        _canvasGo = new GameObject("PartA_Consent_Background_Canvas");
        Canvas c = _canvasGo.AddComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 210;

        CanvasScaler s = _canvasGo.AddComponent<CanvasScaler>();
        s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        s.referenceResolution = new Vector2(1920, 1080);
        s.matchWidthOrHeight = 0.5f;
        _canvasGo.AddComponent<GraphicRaycaster>();

        Button reopen = CreateButton(_canvasGo.transform, "OpenPartABtn", "ข้อมูลพื้นฐาน (Part A)",
            new Color(0.18f, 0.36f, 0.56f, 0.92f), Color.white, 17,
            0.015f, 0.925f, 0.165f, 0.975f, () => { if (_modalRoot != null) _modalRoot.SetActive(true); });
        _openBtnGo = reopen.gameObject;

        _modalRoot = CreatePanel(_canvasGo.transform, "PartABackdrop", new Color(0.02f, 0.03f, 0.06f, 0.94f));
        SetAnchors(_modalRoot, 0f, 0f, 1f, 1f);

        GameObject card = CreatePanel(_modalRoot.transform, "PartACard", new Color(0.08f, 0.10f, 0.16f, 0.99f));
        SetAnchors(card, 0.14f, 0.05f, 0.86f, 0.95f);
        AddOutline(card, new Color(0.88f, 0.70f, 0.30f, 0.80f));

        CreateText(card.transform, "Header", "คำชี้แจงการวิจัย & ข้อมูลพื้นฐานผู้เข้าร่วม (Part A — ทำครั้งเดียวก่อนเล่นรอบแรก)",
            25, FontStyle.Bold, new Color(0.98f, 0.86f, 0.48f), TextAnchor.MiddleCenter, 0.04f, 0.90f, 0.96f, 0.97f);

        GameObject consentBox = CreatePanel(card.transform, "ConsentBox", new Color(0.12f, 0.15f, 0.22f, 1f));
        SetAnchors(consentBox, 0.04f, 0.72f, 0.96f, 0.89f);
        AddOutline(consentBox, new Color(0.36f, 0.70f, 0.95f, 0.55f));

        CreateText(consentBox.transform, "ConsentDesc",
            "คำชี้แจง (Informed Consent): แบบสอบถามและข้อมูลการเล่นเกมนี้จัดเก็บเพื่อวิเคราะห์ทางสถิติในงานวิจัยเท่านั้น ข้อมูลทั้งหมดจะถูกเก็บภายใต้รหัส 6 หลักโดยไม่เปิดเผยตัวตนของผู้เข้าร่วม",
            17, FontStyle.Normal, new Color(0.86f, 0.90f, 0.96f), TextAnchor.MiddleLeft, 0.03f, 0.44f, 0.97f, 0.94f);

        Button cBtn = CreateButton(consentBox.transform, "ConsentToggleBtn", "[  ] ข้าพเจ้ารับทราบและยินยอมเข้าร่วมการทดลองนี้",
            new Color(0.20f, 0.24f, 0.34f, 1f), Color.white, 19, 0.03f, 0.08f, 0.97f, 0.42f, ToggleConsent);
        _consentBtnBg = cBtn.GetComponent<Image>();
        _consentBtnTxt = cBtn.GetComponentInChildren<Text>();

        CreateText(card.transform, "Q1Label", "1. อายุ (ปี):", 20, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft, 0.04f, 0.62f, 0.24f, 0.69f);
        _ageInput = CreateInputField(card.transform, "AgeInput", "กรอกอายุ เช่น 21", 3, 0.24f, 0.62f, 0.46f, 0.69f);

        CreateText(card.transform, "Q2Label", "2. เพศ:", 20, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft, 0.04f, 0.51f, 0.22f, 0.58f);
        string[] genders = new string[] { "ชาย", "หญิง", "อื่น ๆ / ไม่ประสงค์ระบุ" };
        float[] gx0 = new float[] { 0.24f, 0.44f, 0.64f };
        float[] gx1 = new float[] { 0.42f, 0.62f, 0.96f };
        for (int i = 0; i < 3; i++)
        {
            int idx = i;
            string gVal = genders[i];
            Button gb = CreateButton(card.transform, "Gender_" + i, gVal, new Color(0.18f, 0.21f, 0.30f, 1f), Color.white, 19,
                gx0[i], 0.51f, gx1[i], 0.58f, () => SelectGender(idx, gVal));
            _genderBgs[i] = gb.GetComponent<Image>();
        }

        CreateText(card.transform, "Q3Label", "3. ประสบการณ์เล่นเกมโดยรวม  (1 = น้อยมาก  ->  5 = มากที่สุด)",
            20, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft, 0.04f, 0.42f, 0.96f, 0.48f);
        for (int sVal = 1; sVal <= 5; sVal++)
        {
            int sc = sVal;
            float x0 = 0.04f + (sVal - 1) * 0.186f;
            Button b = CreateButton(card.transform, "GenExp_" + sVal, sVal.ToString(), new Color(0.18f, 0.21f, 0.30f, 1f), Color.white, 22,
                x0, 0.34f, x0 + 0.172f, 0.41f, () => SelectGenExp(sc));
            _genExpBgs[sVal - 1] = b.GetComponent<Image>();
        }

        CreateText(card.transform, "Q4Label", "4. ประสบการณ์เล่นเกมแนว Action / Roguelite  (1 = ไม่มีประสบการณ์  ->  5 = มากที่สุด)",
            20, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft, 0.04f, 0.25f, 0.96f, 0.31f);
        for (int sVal = 1; sVal <= 5; sVal++)
        {
            int sc = sVal;
            float x0 = 0.04f + (sVal - 1) * 0.186f;
            Button b = CreateButton(card.transform, "ActExp_" + sVal, sVal.ToString(), new Color(0.18f, 0.21f, 0.30f, 1f), Color.white, 22,
                x0, 0.17f, x0 + 0.172f, 0.24f, () => SelectActExp(sc));
            _actExpBgs[sVal - 1] = b.GetComponent<Image>();
        }

        // Status
        _statusText = CreateText(card.transform, "PartAStatus", "กรุณากดยินยอมและกรอกข้อมูลให้ครบทั้ง 4 ข้อ (สำหรับผู้เล่นรอบแรก)",
            18, FontStyle.Bold, new Color(0.98f, 0.84f, 0.42f), TextAnchor.MiddleCenter, 0.04f, 0.105f, 0.96f, 0.155f);

        Button skipB = CreateButton(card.transform, "SkipRound2Btn", "เคยกรอกแล้ว (ข้ามสำหรับรอบที่ 2)", new Color(0.24f, 0.28f, 0.38f, 1f), Color.white, 19,
            0.04f, 0.025f, 0.42f, 0.095f, () => { _modalRoot.SetActive(false); }); if (string.IsNullOrEmpty(PlayerPrefs.GetString("LastParticipantID", ""))) skipB.gameObject.SetActive(false);

        _partASubmitBtn = CreateButton(card.transform, "SubmitPartABtn", "กรุณายินยอมและกรอกให้ครบทุกข้อ", new Color(0.26f, 0.28f, 0.32f, 0.85f), new Color(0.75f, 0.78f, 0.82f), 20,
            0.45f, 0.025f, 0.96f, 0.095f, OnSubmitPartA); if (_ageInput != null) _ageInput.onValueChanged.AddListener(_ => RefreshPartASubmitButton()); RefreshPartASubmitButton();

        if (PlayerPrefs.GetInt("SkipPartAOnce", 0) == 1)
        {
            PlayerPrefs.SetInt("SkipPartAOnce", 0);
            _modalRoot.SetActive(false);
        }
    }

    private void RefreshPartASubmitButton()
    {
        if (_partASubmitBtn == null) return;
        string ageStr = _ageInput != null ? _ageInput.text.Trim() : "";
        bool ageOk = int.TryParse(ageStr, out int a) && a >= 5 && a <= 100;
        bool ready = (_consentAgreed && ageOk && !string.IsNullOrEmpty(_selectedGender) && _generalExp >= 1 && _actionExp >= 1 && !_isSubmitting);
        _partASubmitBtn.interactable = ready;
        Image bg = _partASubmitBtn.GetComponent<Image>();
        if (bg != null) bg.color = ready ? new Color(0.16f, 0.62f, 0.36f, 1f) : new Color(0.26f, 0.28f, 0.32f, 0.85f);
        Text lbl = _partASubmitBtn.GetComponentInChildren<Text>();
        if (lbl != null)
        {
            lbl.text = ready ? "✓ ยินยอมและบันทึกข้อมูล (เริ่มลงทะเบียน ID)" : "กรุณายินยอมและกรอกให้ครบทุกข้อ";
            lbl.color = ready ? Color.white : new Color(0.75f, 0.78f, 0.82f);
        }
    }

    private void ToggleConsent()
    {
        _consentAgreed = !_consentAgreed;
        if (_consentBtnBg != null) _consentBtnBg.color = _consentAgreed ? new Color(0.16f, 0.58f, 0.36f, 1f) : new Color(0.20f, 0.24f, 0.34f, 1f);
        if (_consentBtnTxt != null) _consentBtnTxt.text = _consentAgreed ? "[✓] ข้าพเจ้ารับทราบและยินยอมเข้าร่วมการทดลองนี้" : "[  ] ข้าพเจ้ารับทราบและยินยอมเข้าร่วมการทดลองนี้"; RefreshPartASubmitButton();
    }

    private void SelectGender(int idx, string val)
    {
        _selectedGender = val; RefreshPartASubmitButton();
        for (int i = 0; i < 3; i++)
            if (_genderBgs[i] != null) _genderBgs[i].color = (i == idx) ? new Color(0.84f, 0.62f, 0.16f, 1f) : new Color(0.18f, 0.21f, 0.30f, 1f);
    }

    private void SelectGenExp(int score)
    {
        _generalExp = score; RefreshPartASubmitButton();
        for (int i = 0; i < 5; i++)
            if (_genExpBgs[i] != null) _genExpBgs[i].color = (i + 1 == score) ? new Color(0.84f, 0.62f, 0.16f, 1f) : new Color(0.18f, 0.21f, 0.30f, 1f);
    }

    private void SelectActExp(int score)
    {
        _actionExp = score; RefreshPartASubmitButton();
        for (int i = 0; i < 5; i++)
            if (_actExpBgs[i] != null) _actExpBgs[i].color = (i + 1 == score) ? new Color(0.84f, 0.62f, 0.16f, 1f) : new Color(0.18f, 0.21f, 0.30f, 1f);
    }

    private void OnSubmitPartA()
    {
        if (_isSubmitting) return;
        if (!_consentAgreed) { SetStatus("กรุณากดติ๊กยินยอมเข้าร่วมการทดลองก่อนครับ", new Color(0.98f, 0.42f, 0.42f)); return; }
        string ageStr = _ageInput != null ? _ageInput.text.Trim() : "";
        if (!int.TryParse(ageStr, out int age) || age < 5 || age > 100) { SetStatus("กรุณากรอกอายุเป็นตัวเลขที่ถูกต้อง (เช่น 21)", new Color(0.98f, 0.42f, 0.42f)); return; }
        if (string.IsNullOrEmpty(_selectedGender)) { SetStatus("กรุณาเลือกเพศก่อนครับ", new Color(0.98f, 0.42f, 0.42f)); return; }
        if (_generalExp < 1 || _actionExp < 1) { SetStatus("กรุณาเลือกคะแนนประสบการณ์เล่นเกม (ข้อ 3 และ 4) ให้ครบครับ", new Color(0.98f, 0.42f, 0.42f)); return; }

        StartCoroutine(UploadPartACoroutine(age));
    }

    private IEnumerator UploadPartACoroutine(int age)
    {
        _isSubmitting = true;
        SetStatus("กำลังรอรับรหัส 6 หลักและบันทึกข้อมูล Part A ลง Firebase...", new Color(0.55f, 0.86f, 1.0f));

        float wait = 0f;
        while (string.IsNullOrEmpty(MainMenuAndTutorialUI.CurrentParticipantId) && wait < 4f)
        {
            wait += Time.unscaledDeltaTime;
            yield return null;
        }

        string pid = !string.IsNullOrEmpty(MainMenuAndTutorialUI.CurrentParticipantId)
            ? MainMenuAndTutorialUI.CurrentParticipantId
            : PlayerPrefs.GetString("ParticipantID", "000000");

        string nowIso = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
        string json = $"{{\"consent\":true,\"age\":{age},\"gender\":\"{_selectedGender}\",\"generalGamingExp\":{_generalExp},\"actionRogueliteExp\":{_actionExp},\"submittedAt\":\"{nowIso}\"}}";

        string url = $"{FirebaseBaseUrl}/participants/{pid}/background.json";
        using (UnityWebRequest req = new UnityWebRequest(url, "PUT"))
        {
            byte[] raw = Encoding.UTF8.GetBytes(json);
            req.uploadHandler = new UploadHandlerRaw(raw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.timeout = 10;
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                SetStatus("บันทึกข้อมูล Part A เรียบร้อยแล้ว!", new Color(0.45f, 0.98f, 0.65f));
                yield return new WaitForSeconds(0.8f);
                if (_modalRoot != null) _modalRoot.SetActive(false);
            }
            else
            {
                SetStatus($"บันทึกไม่สำเร็จ ({req.error}) — กดบันทึกอีกครั้ง", new Color(0.98f, 0.42f, 0.42f));
            }
        }
        _isSubmitting = false;
    }

    private void SetStatus(string msg, Color c)
    {
        if (_statusText == null) return;
        _statusText.text = msg;
        _statusText.color = c;
    }

    private void EnsureEventSystem()
    {
#if UNITY_6000_0_OR_NEWER
        EventSystem es = UnityEngine.Object.FindFirstObjectByType<EventSystem>();
#else
        EventSystem es = UnityEngine.Object.FindObjectOfType<EventSystem>();
#endif
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

    private void LoadFont()
    {
        try { _uiFont = Font.CreateDynamicFontFromOSFont(new string[] { "Leelawadee UI", "Tahoma", "Segoe UI", "Arial" }, 20); }
        catch { _uiFont = null; }
        if (_uiFont == null) _uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    private GameObject CreatePanel(Transform p, string n, Color c)
    {
        GameObject go = new GameObject(n);
        go.transform.SetParent(p, false);
        go.AddComponent<Image>().color = c;
        return go;
    }

    private void SetAnchors(GameObject go, float x0, float y0, float x1, float y1)
    {
        RectTransform rt = go.GetComponent<RectTransform>();
        if (rt == null) rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(x0, y0);
        rt.anchorMax = new Vector2(x1, y1);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private void AddOutline(GameObject go, Color c)
    {
        Outline o = go.AddComponent<Outline>();
        o.effectColor = c;
        o.effectDistance = new Vector2(2f, -2f);
    }

    private Text CreateText(Transform p, string n, string txt, int size, FontStyle style, Color c, TextAnchor align, float x0, float y0, float x1, float y1)
    {
        GameObject go = new GameObject(n);
        go.transform.SetParent(p, false);
        SetAnchors(go, x0, y0, x1, y1);
        Text t = go.AddComponent<Text>();
        t.font = _uiFont; t.text = txt; t.fontSize = size; t.fontStyle = style; t.color = c; t.alignment = align;
        t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }

    private Button CreateButton(Transform p, string n, string label, Color bg, Color fg, int size, float x0, float y0, float x1, float y1, Action onClick)
    {
        GameObject go = CreatePanel(p, n, bg);
        SetAnchors(go, x0, y0, x1, y1);
        AddOutline(go, new Color(1f, 1f, 1f, 0.25f));
        Button b = go.AddComponent<Button>();
        b.onClick.AddListener(() => onClick?.Invoke());
        CreateText(go.transform, "Label", label, size, FontStyle.Bold, fg, TextAnchor.MiddleCenter, 0f, 0f, 1f, 1f);
        return b;
    }

    private InputField CreateInputField(Transform p, string n, string ph, int limit, float x0, float y0, float x1, float y1)
    {
        GameObject go = CreatePanel(p, n, new Color(0.14f, 0.17f, 0.25f, 1f));
        SetAnchors(go, x0, y0, x1, y1);
        AddOutline(go, new Color(0.45f, 0.55f, 0.72f, 0.65f));
        InputField inp = go.AddComponent<InputField>();
        inp.characterLimit = limit;
        inp.contentType = InputField.ContentType.IntegerNumber;
        inp.placeholder = CreateText(go.transform, "Placeholder", ph, 18, FontStyle.Italic, new Color(0.55f, 0.62f, 0.72f), TextAnchor.MiddleLeft, 0.05f, 0.05f, 0.95f, 0.95f);
        inp.textComponent = CreateText(go.transform, "Text", "", 22, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft, 0.05f, 0.05f, 0.95f, 0.95f);
        return inp;
    }
}
