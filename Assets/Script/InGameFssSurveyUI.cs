#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif
using System;
using System.Collections;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class InGameFssSurveyUI : MonoBehaviour
{
    public const string FirebaseBaseUrl = "https://projectrougelite-default-rtdb.asia-southeast1.firebasedatabase.app";
    private static InGameFssSurveyUI _instance;

    private static readonly string[] QuestionsThai = new string[]
    {
        "1. ฉันรู้สึกว่าระดับความท้าทายของเกมมีความเหมาะสมพอดี",
        "2. ความคิดและการกระทำของฉันดำเนินไปอย่างต่อเนื่องและราบรื่น",
        "3. ฉันไม่ทันสังเกตว่าเวลาผ่านไป",
        "4. ฉันไม่มีความยากลำบากในการจดจ่อกับสิ่งที่กำลังทำ",
        "5. จิตใจของฉันปลอดโปร่งและชัดเจน",
        "6. ฉันจดจ่อและมีส่วนร่วมอย่างเต็มที่กับสิ่งที่กำลังทำ",
        "7. ความคิดหรือการเคลื่อนไหวที่เหมาะสมเกิดขึ้นได้อย่างเป็นธรรมชาติ",
        "8. ฉันรู้ว่าต้องทำอะไรในแต่ละขั้นตอนของการเล่น",
        "9. ฉันรู้สึกว่าสามารถควบคุมสถานการณ์ในการเล่นได้",
        "10. ฉันจดจ่ออยู่กับสิ่งที่กำลังทำจนแทบไม่สนใจสิ่งอื่น"
    };

    private Font _uiFont;
    private GameObject _canvasGo;
    private GameObject _modalRoot;
    private GameObject _reopenBtnGo;
    private GameObject _page1Group;
    private GameObject _page2Group;
    private Text _statusText;
    private Button _submitBtn;
    private readonly int[] _answers = new int[10]; // 0 = unanswered, 1..7 = score
    private readonly Image[,] _ratingBtnBgs = new Image[10, 7];
    private bool _isSubmitting = false;
    private bool _submitted = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (_instance != null) return;
        GameObject go = new GameObject("InGameFssSurveyUI");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<InGameFssSurveyUI>();
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
        if (lower.Contains("end") || lower.Contains("victory"))
        {
            Array.Clear(_answers, 0, _answers.Length);
            _isSubmitting = false;
            _submitted = false;
            EnsureEventSystem();
            BuildSurveyUI();
        }
    }

    private void BuildSurveyUI()
    {
        _canvasGo = new GameObject("InGame_FSS_Survey_Canvas");
        Canvas c = _canvasGo.AddComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 220;

        CanvasScaler s = _canvasGo.AddComponent<CanvasScaler>();
        s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        s.referenceResolution = new Vector2(1920, 1080);
        s.matchWidthOrHeight = 0.5f;
        _canvasGo.AddComponent<GraphicRaycaster>();

        Button reopen = CreateButton(_canvasGo.transform, "ReopenFssBtn", "แบบประเมินหลังเล่น (FSS 10 ข้อ)",
            new Color(0.80f, 0.56f, 0.14f, 1f), Color.white, 20, 0.34f, 0.91f, 0.66f, 0.975f,
            () => { if (_modalRoot != null) _modalRoot.SetActive(true); });
        _reopenBtnGo = reopen.gameObject;
        _reopenBtnGo.SetActive(false);

        _modalRoot = CreatePanel(_canvasGo.transform, "FssBackdrop", new Color(0.02f, 0.03f, 0.06f, 0.92f));
        SetAnchors(_modalRoot, 0f, 0f, 1f, 1f);

        GameObject card = CreatePanel(_modalRoot.transform, "FssCard", new Color(0.08f, 0.10f, 0.16f, 0.99f));
        SetAnchors(card, 0.10f, 0.05f, 0.90f, 0.95f);
        AddOutline(card, new Color(0.92f, 0.74f, 0.30f, 0.85f));

        string pid = GetParticipantId();
        CreateText(card.transform, "Title", $"แบบประเมินประสบการณ์การเล่น (10 ข้อ)  |  รหัสผู้เล่น: {pid}",
            28, FontStyle.Bold, new Color(0.98f, 0.86f, 0.48f), TextAnchor.MiddleLeft, 0.04f, 0.90f, 0.86f, 0.97f);

        CreateText(card.transform, "ScaleHint", "เลือกระดับความเห็นด้วยในแต่ละข้อ:   1 = ไม่เห็นด้วยอย่างยิ่ง   ...   4 = ปานกลาง   ...   7 = เห็นด้วยอย่างยิ่ง",
            19, FontStyle.Normal, new Color(0.76f, 0.84f, 0.94f), TextAnchor.MiddleLeft, 0.04f, 0.84f, 0.96f, 0.90f);

        _page1Group = CreateEmptyContainer(card.transform, "Page1_Items1to5", 0.04f, 0.15f, 0.96f, 0.83f);
        _page2Group = CreateEmptyContainer(card.transform, "Page2_Items6to10", 0.04f, 0.15f, 0.96f, 0.83f);

        BuildQuestionPage(_page1Group.transform, 0, 5);
        BuildQuestionPage(_page2Group.transform, 5, 10);
        _page2Group.SetActive(false);

        _statusText = CreateText(card.transform, "FssStatus", "หน้า 1 / 2 — กรุณาเลือกคะแนนให้ครบทุกข้อแล้วกด 'หน้าถัดไป'",
            19, FontStyle.Bold, new Color(0.98f, 0.84f, 0.42f), TextAnchor.MiddleCenter, 0.04f, 0.095f, 0.96f, 0.145f);

        CreateButton(card.transform, "PrevPageBtn", "<- ข้อ 1–5", new Color(0.22f, 0.26f, 0.36f, 1f), Color.white, 20,
            0.04f, 0.02f, 0.22f, 0.085f, () => ShowPage(1));

        CreateButton(card.transform, "NextPageBtn", "ข้อ 6–10 ->", new Color(0.18f, 0.46f, 0.72f, 1f), Color.white, 20,
            0.24f, 0.02f, 0.42f, 0.085f, () => ShowPage(2));

        _submitBtn = CreateButton(card.transform, "SubmitFssBtn", "กรุณาตอบให้ครบ 10 ข้อ (0/10)", new Color(0.26f, 0.28f, 0.32f, 0.85f), new Color(0.75f, 0.78f, 0.82f), 20,
            0.46f, 0.02f, 0.82f, 0.085f, OnSubmitClicked); RefreshFssSubmitButton();

        CreateButton(card.transform, "HideModalBtn", "ปิดไว้ก่อน", new Color(0.45f, 0.20f, 0.22f, 1f), Color.white, 18,
            0.84f, 0.02f, 0.96f, 0.085f, () => { _modalRoot.SetActive(false); _reopenBtnGo.SetActive(true); });
    }

    private void BuildQuestionPage(Transform parent, int startIdx, int endIdx)
    {
        int count = endIdx - startIdx;
        float rowH = 1f / count;

        for (int i = 0; i < count; i++)
        {
            int qIdx = startIdx + i;
            float yMax = 1f - i * rowH - 0.015f;
            float yMin = 1f - (i + 1) * rowH + 0.015f;

            GameObject row = CreatePanel(parent, "Q_" + (qIdx + 1), new Color(0.12f, 0.15f, 0.22f, 0.96f));
            SetAnchors(row, 0f, yMin, 1f, yMax);
            AddOutline(row, new Color(0.35f, 0.45f, 0.62f, 0.40f));

            CreateText(row.transform, "QText", QuestionsThai[qIdx], 20, FontStyle.Bold,
                new Color(0.95f, 0.94f, 0.90f), TextAnchor.MiddleLeft, 0.02f, 0.08f, 0.56f, 0.92f);

            // 7 Rating Buttons (1..7)
            float startX = 0.57f;
            float stepX = 0.06f;
            for (int score = 1; score <= 7; score++)
            {
                int capturedQ = qIdx;
                int capturedS = score;
                float x0 = startX + (score - 1) * stepX;
                float x1 = x0 + stepX - 0.008f;

                Button b = CreateButton(row.transform, $"Btn_{qIdx}_{score}", score.ToString(),
                    new Color(0.18f, 0.21f, 0.30f, 1f), Color.white, 21, x0, 0.16f, x1, 0.84f,
                    () => SelectRating(capturedQ, capturedS));
                _ratingBtnBgs[qIdx, score - 1] = b.GetComponent<Image>();
            }
        }
    }

    private void ShowPage(int page)
    {
        bool p1 = (page == 1);
        if (_page1Group != null) _page1Group.SetActive(p1);
        if (_page2Group != null) _page2Group.SetActive(!p1);
        if (!_submitted && _statusText != null)
        {
            int answered = CountAnswered();
            _statusText.text = p1
                ? $"หน้า 1 / 2 — ตอบแล้ว {answered} / 10 ข้อ"
                : $"หน้า 2 / 2 — ตอบแล้ว {answered} / 10 ข้อ (ตอบครบแล้วกด 'ส่งแบบประเมินลงระบบ')";
            _statusText.color = new Color(0.98f, 0.84f, 0.42f);
        }
    }

    private void SelectRating(int qIdx, int score)
    {
        if (_submitted) return;
        _answers[qIdx] = score;
        for (int s = 1; s <= 7; s++)
        {
            Image img = _ratingBtnBgs[qIdx, s - 1];
            if (img != null)
            {
                img.color = (s == score)
                    ? new Color(0.86f, 0.64f, 0.16f, 1f)
                    : new Color(0.18f, 0.21f, 0.30f, 1f);
            }
        }
        RefreshFssSubmitButton(); int answered = CountAnswered();
        if (_statusText != null)
        {
            _statusText.text = answered == 10
                ? "ตอบครบทั้ง 10 ข้อแล้ว! กดปุ่ม 'ส่งแบบประเมินลงระบบ' ได้เลยครับ"
                : $"ตอบแล้ว {answered} / 10 ข้อ";
            _statusText.color = answered == 10 ? new Color(0.45f, 0.96f, 0.65f) : new Color(0.98f, 0.84f, 0.42f);
        }
    }

    private void RefreshFssSubmitButton()
    {
        if (_submitBtn == null) return;
        int answered = CountAnswered();
        bool ready = (answered == 10 && !_submitted && !_isSubmitting);
        _submitBtn.interactable = ready;
        Image bg = _submitBtn.GetComponent<Image>();
        if (bg != null) bg.color = ready ? new Color(0.16f, 0.62f, 0.36f, 1f) : new Color(0.26f, 0.28f, 0.32f, 0.85f);
        Text lbl = _submitBtn.GetComponentInChildren<Text>();
        if (lbl != null)
        {
            lbl.text = ready ? "✓ ส่งแบบประเมินลงระบบ (SUBMIT)" : $"กรุณาตอบให้ครบ 10 ข้อ ({answered}/10)";
            lbl.color = ready ? Color.white : new Color(0.75f, 0.78f, 0.82f);
        }
    }

    private int CountAnswered()
    {
        int c = 0;
        for (int i = 0; i < 10; i++) if (_answers[i] >= 1) c++;
        return c;
    }

    private void OnSubmitClicked()
    {
        if (_isSubmitting || _submitted) return;
        for (int i = 0; i < 10; i++)
        {
            if (_answers[i] < 1)
            {
                ShowPage(i < 5 ? 1 : 2);
                if (_statusText != null)
                {
                    _statusText.text = $"กรุณาเลือกคะแนนข้อที่ {i + 1} ให้ครบก่อนส่งครับ";
                    _statusText.color = new Color(0.98f, 0.42f, 0.42f);
                }
                return;
            }
        }
        StartCoroutine(UploadFssToFirebaseCoroutine());
    }

    private IEnumerator UploadFssToFirebaseCoroutine()
    {
        _isSubmitting = true;
        if (_statusText != null)
        {
            _statusText.text = "กำลังคำนวณค่า Sum / Mean / Min / Max และส่งเข้า Firebase...";
            _statusText.color = new Color(0.55f, 0.86f, 1.0f);
        }

        int sum = 0;
        int minVal = 7;
        int maxVal = 1;
        for (int i = 0; i < 10; i++)
        {
            sum += _answers[i];
            if (_answers[i] < minVal) minVal = _answers[i];
            if (_answers[i] > maxVal) maxVal = _answers[i];
        }
        float mean = sum / 10f;
        // Proposal v3 §11: Fluency = mean(FSS02,04,05,07,08,09), Absorption = mean(FSS01,03,06,10)
        float fluency = (_answers[1] + _answers[3] + _answers[4] + _answers[6] + _answers[7] + _answers[8]) / 6f;
        float absorption = (_answers[0] + _answers[2] + _answers[5] + _answers[9]) / 4f;

        string pid = GetParticipantId();
        string cond = GetConditionName();
        string nowIso = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
        CultureInfo inv = CultureInfo.InvariantCulture;

        StringBuilder sb = new StringBuilder();
        sb.Append("{");
        sb.AppendFormat("\"participantId\":\"{0}\",", pid);
        sb.AppendFormat("\"condition\":\"{0}\",", cond);
        for (int i = 0; i < 10; i++)
        {
            sb.AppendFormat("\"fss{0:00}\":{1},", i + 1, _answers[i]);
        }
        sb.AppendFormat(inv, "\"sum\":{0},\"mean\":{1:F2},\"min\":{2},\"max\":{3},", sum, mean, minVal, maxVal);
        sb.AppendFormat(inv, "\"fluencyMean\":{0:F2},\"absorptionMean\":{1:F2},", fluency, absorption);
        sb.AppendFormat("\"submittedAt\":\"{0}\"", nowIso);
        sb.Append("}");

        string url = $"{FirebaseBaseUrl}/participants/{pid}/{cond}/fss.json";
        using (UnityWebRequest req = new UnityWebRequest(url, "PUT"))
        {
            byte[] raw = Encoding.UTF8.GetBytes(sb.ToString());
            req.uploadHandler = new UploadHandlerRaw(raw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.timeout = 10;

            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                _submitted = true;
                if (_statusText != null)
                {
                    _statusText.text = $"ส่งแบบประเมินสำเร็จ! (Sum={sum} | Mean={mean:F2} | Min={minVal} | Max={maxVal})";
                    _statusText.color = new Color(0.45f, 0.98f, 0.65f);
                }
                if (_submitBtn != null) _submitBtn.interactable = false;
                yield return new WaitForSeconds(1.8f);
                if (_modalRoot != null) _modalRoot.SetActive(false);
            }
            else
            {
                if (_statusText != null)
                {
                    _statusText.text = $"ส่งไม่สำเร็จ ({req.error}) — กด 'ส่งแบบประเมินลงระบบ' อีกครั้ง";
                    _statusText.color = new Color(0.98f, 0.42f, 0.42f);
                }
            }
        }
        _isSubmitting = false;
    }

    private string GetParticipantId()
    {
        if (!string.IsNullOrEmpty(MainMenuAndTutorialUI.CurrentParticipantId))
            return MainMenuAndTutorialUI.CurrentParticipantId;
        return PlayerPrefs.GetString("ParticipantID", "000000");
    }

    private string GetConditionName()
    {
        bool adaptive = PlayerPrefs.GetInt("IsAdaptiveMode", MainMenuAndTutorialUI.IsAdaptiveMode ? 1 : 0) == 1;
        return adaptive ? "Adaptive" : "NonAdaptive";
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

    private GameObject CreateEmptyContainer(Transform parent, string name, float x0, float y0, float x1, float y1)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        SetAnchors(go, x0, y0, x1, y1);
        return go;
    }

    private GameObject CreatePanel(Transform parent, string name, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<Image>().color = color;
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

    private Text CreateText(Transform p, string name, string txt, int size, FontStyle style, Color c, TextAnchor align, float x0, float y0, float x1, float y1)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(p, false);
        SetAnchors(go, x0, y0, x1, y1);
        Text t = go.AddComponent<Text>();
        t.font = _uiFont; t.text = txt; t.fontSize = size; t.fontStyle = style; t.color = c; t.alignment = align;
        t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }

    private Button CreateButton(Transform p, string name, string label, Color bg, Color fg, int size, float x0, float y0, float x1, float y1, Action onClick)
    {
        GameObject go = CreatePanel(p, name, bg);
        SetAnchors(go, x0, y0, x1, y1);
        AddOutline(go, new Color(1f, 1f, 1f, 0.25f));
        Button b = go.AddComponent<Button>();
        b.onClick.AddListener(() => onClick?.Invoke());
        CreateText(go.transform, "Label", label, size, FontStyle.Bold, fg, TextAnchor.MiddleCenter, 0f, 0f, 1f, 1f);
        return b;
    }
}
