#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameAudioManager : MonoBehaviour
{
    public static GameAudioManager Instance { get; private set; }

    private const int SampleRate = 44100;
    private AudioSource _bgmSource;
    private AudioSource _sfxSource;

    private bool _masterMuted = false;
    private bool _bgmEnabled = true;
    private bool _sfxEnabled = true;
    private float _bgmVolume = 0.50f; // 0..1 (scaled internally to gentle 0.24 max)
    private float _sfxVolume = 0.75f; // 0..1

    private AudioClip _bgmExplore, _bgmBoss;
    private AudioClip _sfxSlash, _sfxHit, _sfxHurt, _sfxSwordWave, _sfxDash, _sfxTempest, _sfxPotion, _sfxPortal, _sfxVictory, _sfxDeath;

    private Font _uiFont;
    private GameObject _uiCanvasGo;
    private GameObject _settingsModal;
    private Text _cornerBtnLabel;
    private Text _masterBtnLabel;
    private Text _bgmToggleLabel, _sfxToggleLabel;
    private Text _bgmPctText, _sfxPctText;
    private Image _bgmBarFill, _sfxBarFill;
    private Image _masterBtnBg, _bgmToggleBg, _sfxToggleBg;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;
        GameObject go = new GameObject("GameAudioManager");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<GameAudioManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        LoadPrefs();
        LoadFont();

        _bgmSource = gameObject.AddComponent<AudioSource>();
        _bgmSource.loop = true;
        _bgmSource.playOnAwake = false;

        _sfxSource = gameObject.AddComponent<AudioSource>();
        _sfxSource.loop = false;
        _sfxSource.playOnAwake = false;

        GenerateAllClips();
        ApplyAudioSettings();
        BuildAudioSettingsUI();

        SceneManager.sceneLoaded += OnSceneLoaded;
        UpdateBgmForScene(SceneManager.GetActiveScene().name);
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Update()
    {
        bool pressedM = false;
        bool pressedEsc = false;

#if ENABLE_INPUT_SYSTEM
        Keyboard kb = Keyboard.current;
        if (kb != null)
        {
            pressedM = kb.mKey.wasPressedThisFrame;
            pressedEsc = kb.escapeKey.wasPressedThisFrame;
        }
#else
        pressedM = Input.GetKeyDown(KeyCode.M);
        pressedEsc = Input.GetKeyDown(KeyCode.Escape);
#endif

        if (pressedM) ToggleMasterMute();
        if (pressedEsc && IsGameplayScene(SceneManager.GetActiveScene().name)) ToggleSettingsModal();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (_settingsModal != null && _settingsModal.activeSelf)
        {
            _settingsModal.SetActive(false);
            Time.timeScale = 1f;
        }
        UpdateBgmForScene(scene.name);
    }

    private bool IsGameplayScene(string name)
    {
        return name.StartsWith("Room", StringComparison.OrdinalIgnoreCase)
            || name.Equals("BossRoom", StringComparison.OrdinalIgnoreCase);
    }

    private void UpdateBgmForScene(string sceneName)
    {
        if (_bgmSource == null) return;
        string lower = sceneName.ToLowerInvariant();

        if (lower.Contains("end") || lower.Contains("victory"))
        {
            _bgmSource.Stop();
            PlayVictory();
            return;
        }

        AudioClip target = lower.Contains("boss") ? _bgmBoss : _bgmExplore;
        if (_bgmSource.clip != target || !_bgmSource.isPlaying)
        {
            _bgmSource.clip = target;
            _bgmSource.Play();
        }
        ApplyAudioSettings();
    }

    private void LoadPrefs()
    {
        _masterMuted = PlayerPrefs.GetInt("Audio_MasterMuted", 0) == 1;
        _bgmEnabled = PlayerPrefs.GetInt("Audio_BgmEnabled", 1) == 1;
        _sfxEnabled = PlayerPrefs.GetInt("Audio_SfxEnabled", 1) == 1;
        _bgmVolume = Mathf.Clamp01(PlayerPrefs.GetFloat("Audio_BgmVol", 0.55f));
        _sfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat("Audio_SfxVol", 0.75f));
    }

    private void SavePrefs()
    {
        PlayerPrefs.SetInt("Audio_MasterMuted", _masterMuted ? 1 : 0);
        PlayerPrefs.SetInt("Audio_BgmEnabled", _bgmEnabled ? 1 : 0);
        PlayerPrefs.SetInt("Audio_SfxEnabled", _sfxEnabled ? 1 : 0);
        PlayerPrefs.SetFloat("Audio_BgmVol", _bgmVolume);
        PlayerPrefs.SetFloat("Audio_SfxVol", _sfxVolume);
        PlayerPrefs.Save();
    }

    private void ApplyAudioSettings()
    {
        if (_bgmSource != null)
        {
            _bgmSource.mute = _masterMuted || !_bgmEnabled;
            _bgmSource.volume = _bgmVolume * 0.24f;
        }
        if (_sfxSource != null)
        {
            _sfxSource.mute = _masterMuted || !_sfxEnabled;
            _sfxSource.volume = _sfxVolume * 1.0f;
        }
        RefreshSettingsUI();
    }

    public void ToggleMasterMute()
    {
        _masterMuted = !_masterMuted;
        SavePrefs();
        ApplyAudioSettings();
    }

    public void ToggleBgm()
    {
        _bgmEnabled = !_bgmEnabled;
        SavePrefs();
        ApplyAudioSettings();
    }

    public void ToggleSfx()
    {
        _sfxEnabled = !_sfxEnabled;
        SavePrefs();
        ApplyAudioSettings();
    }

    public void StepBgmVolume(float delta)
    {
        _bgmVolume = Mathf.Clamp01(Mathf.Round((_bgmVolume + delta) * 10f) / 10f);
        if (_bgmVolume > 0f) { _bgmEnabled = true; _masterMuted = false; }
        SavePrefs();
        ApplyAudioSettings();
    }

    public void StepSfxVolume(float delta)
    {
        _sfxVolume = Mathf.Clamp01(Mathf.Round((_sfxVolume + delta) * 10f) / 10f);
        if (_sfxVolume > 0f) { _sfxEnabled = true; _masterMuted = false; }
        SavePrefs();
        ApplyAudioSettings();
        PlayPotion();
    }

    public void ToggleSettingsModal()
    {
        if (_settingsModal == null) return;
        bool next = !_settingsModal.activeSelf;
        _settingsModal.SetActive(next);
        if (IsGameplayScene(SceneManager.GetActiveScene().name))
        {
            Time.timeScale = next ? 0f : 1f;
        }
        RefreshSettingsUI();
    }

    private void BuildAudioSettingsUI()
    {
        _uiCanvasGo = new GameObject("AudioSettings_Canvas");
        DontDestroyOnLoad(_uiCanvasGo);

        Canvas c = _uiCanvasGo.AddComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 180;

        CanvasScaler s = _uiCanvasGo.AddComponent<CanvasScaler>();
        s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        s.referenceResolution = new Vector2(1920, 1080);
        s.matchWidthOrHeight = 0.5f;
        _uiCanvasGo.AddComponent<GraphicRaycaster>();

        // Compact bottom-left button (clear of HP/MP, Action Bar, ENEMIES LEFT, and Help button)
        Button cornerBtn = CreateButton(_uiCanvasGo.transform, "AudioBtn", "เสียง (M)",
            new Color(0.10f, 0.14f, 0.22f, 0.90f), new Color(0.95f, 0.86f, 0.52f), 17,
            0.015f, 0.025f, 0.125f, 0.075f, ToggleSettingsModal);
        _cornerBtnLabel = cornerBtn.GetComponentInChildren<Text>();

        _settingsModal = CreatePanel(_uiCanvasGo.transform, "AudioSettingsBackdrop", new Color(0f, 0f, 0f, 0.70f));
        SetAnchors(_settingsModal, 0f, 0f, 1f, 1f);

        GameObject card = CreatePanel(_settingsModal.transform, "AudioCard", new Color(0.08f, 0.10f, 0.16f, 0.98f));
        SetAnchors(card, 0.30f, 0.24f, 0.70f, 0.76f);
        AddOutline(card, new Color(0.88f, 0.70f, 0.30f, 0.80f));

        CreateText(card.transform, "Title", "ตั้งค่าเสียง (AUDIO SETTINGS)", 26, FontStyle.Bold,
            new Color(0.98f, 0.86f, 0.48f), TextAnchor.MiddleCenter, 0.05f, 0.84f, 0.95f, 0.96f);

        Button mBtn = CreateButton(card.transform, "MasterMuteBtn", "เสียงทั้งหมด: เปิดอยู่ (กด M เพื่อปิด)",
            new Color(0.16f, 0.56f, 0.34f, 1f), Color.white, 20, 0.08f, 0.68f, 0.92f, 0.80f, ToggleMasterMute);
        _masterBtnBg = mBtn.GetComponent<Image>();
        _masterBtnLabel = mBtn.GetComponentInChildren<Text>();

        CreateText(card.transform, "BgmLabel", "เพลงพื้นหลัง (BGM)", 19, FontStyle.Bold,
            new Color(0.86f, 0.90f, 0.98f), TextAnchor.MiddleLeft, 0.08f, 0.56f, 0.55f, 0.65f);
        Button bgmTgl = CreateButton(card.transform, "BgmToggle", "เปิด", new Color(0.18f, 0.55f, 0.36f, 1f), Color.white, 17,
            0.76f, 0.56f, 0.92f, 0.64f, ToggleBgm);
        _bgmToggleBg = bgmTgl.GetComponent<Image>();
        _bgmToggleLabel = bgmTgl.GetComponentInChildren<Text>();

        CreateButton(card.transform, "BgmMinus", "-", new Color(0.22f, 0.26f, 0.36f, 1f), Color.white, 24, 0.08f, 0.45f, 0.16f, 0.54f, () => StepBgmVolume(-0.1f));
        GameObject bgmTrack = CreatePanel(card.transform, "BgmTrack", new Color(0.14f, 0.17f, 0.25f, 1f));
        SetAnchors(bgmTrack, 0.18f, 0.46f, 0.82f, 0.53f);
        GameObject bgmFill = CreatePanel(bgmTrack.transform, "BgmFill", new Color(0.28f, 0.72f, 0.96f, 1f));
        SetAnchors(bgmFill, 0f, 0f, 0.5f, 1f);
        _bgmBarFill = bgmFill.GetComponent<Image>();
        _bgmPctText = CreateText(bgmTrack.transform, "BgmPct", "55%", 16, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter, 0f, 0f, 1f, 1f);
        CreateButton(card.transform, "BgmPlus", "+", new Color(0.22f, 0.26f, 0.36f, 1f), Color.white, 24, 0.84f, 0.45f, 0.92f, 0.54f, () => StepBgmVolume(0.1f));

        CreateText(card.transform, "SfxLabel", "เสียงเอฟเฟกต์ (SFX)", 19, FontStyle.Bold,
            new Color(0.86f, 0.90f, 0.98f), TextAnchor.MiddleLeft, 0.08f, 0.32f, 0.55f, 0.41f);
        Button sfxTgl = CreateButton(card.transform, "SfxToggle", "เปิด", new Color(0.18f, 0.55f, 0.36f, 1f), Color.white, 17,
            0.76f, 0.32f, 0.92f, 0.40f, ToggleSfx);
        _sfxToggleBg = sfxTgl.GetComponent<Image>();
        _sfxToggleLabel = sfxTgl.GetComponentInChildren<Text>();

        CreateButton(card.transform, "SfxMinus", "-", new Color(0.22f, 0.26f, 0.36f, 1f), Color.white, 24, 0.08f, 0.21f, 0.16f, 0.30f, () => StepSfxVolume(-0.1f));
        GameObject sfxTrack = CreatePanel(card.transform, "SfxTrack", new Color(0.14f, 0.17f, 0.25f, 1f));
        SetAnchors(sfxTrack, 0.18f, 0.22f, 0.82f, 0.29f);
        GameObject sfxFill = CreatePanel(sfxTrack.transform, "SfxFill", new Color(0.95f, 0.68f, 0.24f, 1f));
        SetAnchors(sfxFill, 0f, 0f, 0.75f, 1f);
        _sfxBarFill = sfxFill.GetComponent<Image>();
        _sfxPctText = CreateText(sfxTrack.transform, "SfxPct", "75%", 16, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter, 0f, 0f, 1f, 1f);
        CreateButton(card.transform, "SfxPlus", "+", new Color(0.22f, 0.26f, 0.36f, 1f), Color.white, 24, 0.84f, 0.21f, 0.92f, 0.30f, () => StepSfxVolume(0.1f));

        CreateButton(card.transform, "TestSfxBtn", "ทดสอบเสียง", new Color(0.20f, 0.46f, 0.70f, 1f), Color.white, 19,
            0.08f, 0.05f, 0.46f, 0.15f, PlaySwordWave);
        CreateButton(card.transform, "CloseAudioBtn", "ปิดหน้าต่าง", new Color(0.68f, 0.22f, 0.24f, 1f), Color.white, 19,
            0.54f, 0.05f, 0.92f, 0.15f, ToggleSettingsModal);

        _settingsModal.SetActive(false);
        RefreshSettingsUI();
    }

    private void RefreshSettingsUI()
    {
        if (_cornerBtnLabel != null)
            _cornerBtnLabel.text = _masterMuted ? "ปิดเสียง (M)" : "เสียง (M)";

        if (_masterBtnLabel != null && _masterBtnBg != null)
        {
            _masterBtnLabel.text = _masterMuted ? "เสียงทั้งหมด: ปิดอยู่ (คลิกเพื่อเปิด)" : "เสียงทั้งหมด: เปิดอยู่ (คลิกเพื่อปิด)";
            _masterBtnBg.color = _masterMuted ? new Color(0.66f, 0.22f, 0.24f, 1f) : new Color(0.16f, 0.56f, 0.34f, 1f);
        }

        if (_bgmToggleLabel != null && _bgmToggleBg != null)
        {
            _bgmToggleLabel.text = _bgmEnabled ? "เปิด" : "ปิด";
            _bgmToggleBg.color = _bgmEnabled ? new Color(0.18f, 0.55f, 0.36f, 1f) : new Color(0.55f, 0.22f, 0.24f, 1f);
        }
        if (_sfxToggleLabel != null && _sfxToggleBg != null)
        {
            _sfxToggleLabel.text = _sfxEnabled ? "เปิด" : "ปิด";
            _sfxToggleBg.color = _sfxEnabled ? new Color(0.18f, 0.55f, 0.36f, 1f) : new Color(0.55f, 0.22f, 0.24f, 1f);
        }

        if (_bgmBarFill != null) _bgmBarFill.rectTransform.anchorMax = new Vector2(_bgmVolume, 1f);
        if (_bgmPctText != null) _bgmPctText.text = $"{Mathf.RoundToInt(_bgmVolume * 100f)}%";
        if (_sfxBarFill != null) _sfxBarFill.rectTransform.anchorMax = new Vector2(_sfxVolume, 1f);
        if (_sfxPctText != null) _sfxPctText.text = $"{Mathf.RoundToInt(_sfxVolume * 100f)}%";
    }

    public static void PlaySlash()     { if (Instance != null) Instance.PlayOneShotSafe(Instance._sfxSlash, 0.95f, UnityEngine.Random.Range(0.92f, 1.08f)); }
    public static void PlayHit()       { if (Instance != null) Instance.PlayOneShotSafe(Instance._sfxHit, 1.0f, UnityEngine.Random.Range(0.90f, 1.12f)); }
    public static void PlayHurt()      { if (Instance != null) Instance.PlayOneShotSafe(Instance._sfxHurt, 0.95f, 1.0f); }
    public static void PlaySwordWave() { if (Instance != null) Instance.PlayOneShotSafe(Instance._sfxSwordWave, 0.70f, 1.0f); }
    public static void PlayDash()      { if (Instance != null) Instance.PlayOneShotSafe(Instance._sfxDash, 0.65f, 1.05f); }
    public static void PlayTempest()   { if (Instance != null) Instance.PlayOneShotSafe(Instance._sfxTempest, 0.70f, 1.0f); }
    public static void PlayPotion()    { if (Instance != null) Instance.PlayOneShotSafe(Instance._sfxPotion, 0.70f, 1.0f); }
    public static void PlayPortal()    { if (Instance != null) Instance.PlayOneShotSafe(Instance._sfxPortal, 0.75f, 1.0f); }
    public static void PlayVictory()   { if (Instance != null) Instance.PlayOneShotSafe(Instance._sfxVictory, 0.75f, 1.0f); }
    public static void PlayDeath()     { if (Instance != null) Instance.PlayOneShotSafe(Instance._sfxDeath, 0.75f, 1.0f); }

    private void PlayOneShotSafe(AudioClip clip, float vol, float pitch)
    {
        if (_masterMuted || !_sfxEnabled || _sfxSource == null || clip == null) return;
        _sfxSource.pitch = pitch;
        _sfxSource.PlayOneShot(clip, vol);
    }

    private void GenerateAllClips()
    {
        _bgmExplore   = CreateDarkFantasyBgm("BGM_Explore", 8.0f, new float[] { 146.83f, 174.61f, 220.00f, 196.00f }, 1.0f);
        _bgmBoss      = CreateDarkFantasyBgm("BGM_Boss",    6.0f, new float[] { 130.81f, 155.56f, 196.00f, 185.00f }, 1.6f);
        _sfxSlash     = CreateNoiseSweep("SFX_Slash", 0.14f, 900f, 260f);
        _sfxHit       = CreateImpactThud("SFX_Hit", 0.12f, 210f, 65f);
        _sfxHurt      = CreateImpactThud("SFX_Hurt", 0.22f, 150f, 52f);
        _sfxSwordWave = CreateEnergySweep("SFX_SwordWave", 0.28f, 320f, 980f);
        _sfxDash      = CreateNoiseSweep("SFX_Dash", 0.18f, 450f, 1200f);
        _sfxTempest   = CreateTempestLoop("SFX_Tempest", 0.45f);
        _sfxPotion    = CreateArpeggio("SFX_Potion", new float[] { 523.25f, 659.25f, 783.99f, 1046.50f }, 0.08f);
        _sfxPortal    = CreateArpeggio("SFX_Portal", new float[] { 293.66f, 440.00f, 587.33f, 880.00f }, 0.14f);
        _sfxVictory   = CreateArpeggio("SFX_Victory", new float[] { 523.25f, 659.25f, 783.99f, 1046.50f }, 0.16f);
        _sfxDeath     = CreateArpeggio("SFX_Death", new float[] { 293.66f, 261.63f, 220.00f, 164.81f }, 0.18f);
    }

    private AudioClip CreateDarkFantasyBgm(string name, float duration, float[] roots, float pulseHz)
    {
        int len = Mathf.RoundToInt(SampleRate * duration);
        float[] data = new float[len];
        float segLen = duration / roots.Length;
        for (int i = 0; i < len; i++)
        {
            float t = (float)i / SampleRate;
            int idx = Mathf.Min(roots.Length - 1, (int)(t / segLen));
            float f = roots[idx];
            float localT = t - idx * segLen;
            float fade = Mathf.Min(1f, localT / 0.12f) * Mathf.Min(1f, (segLen - localT) / 0.12f);
            float pad = Mathf.Sin(2f * Mathf.PI * f * t) * 0.40f + Mathf.Sin(2f * Mathf.PI * (f * 1.5f) * t) * 0.22f + Mathf.Sin(2f * Mathf.PI * (f * 2.0f) * t) * 0.10f;
            float pulse = 0.78f + 0.22f * Mathf.Sin(2f * Mathf.PI * pulseHz * t);
            data[i] = Mathf.Clamp(pad * pulse * fade * 0.45f, -0.9f, 0.9f);
        }
        AudioClip c = AudioClip.Create(name, len, 1, SampleRate, false);
        c.SetData(data, 0);
        return c;
    }

    private AudioClip CreateNoiseSweep(string name, float duration, float fStart, float fEnd)
    {
        int len = Mathf.RoundToInt(SampleRate * duration);
        float[] data = new float[len];
        System.Random rng = new System.Random(42);
        float phase = 0f;
        for (int i = 0; i < len; i++)
        {
            float p = (float)i / len;
            float env = Mathf.Sin(Mathf.PI * p) * (1f - p * 0.4f);
            phase += 2f * Mathf.PI * Mathf.Lerp(fStart, fEnd, p) / SampleRate;
            float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
            data[i] = Mathf.Clamp((noise * 0.55f + Mathf.Sin(phase) * 0.45f) * env * 0.95f, -0.98f, 0.98f);
        }
        AudioClip c = AudioClip.Create(name, len, 1, SampleRate, false);
        c.SetData(data, 0);
        return c;
    }

    private AudioClip CreateImpactThud(string name, float duration, float fStart, float fEnd)
    {
        int len = Mathf.RoundToInt(SampleRate * duration);
        float[] data = new float[len];
        float phase = 0f;
        for (int i = 0; i < len; i++)
        {
            float p = (float)i / len;
            float env = Mathf.Pow(1f - p, 2.2f);
            phase += 2f * Mathf.PI * Mathf.Lerp(fStart, fEnd, p) / SampleRate;
            float snap = (p < 0.18f) ? Mathf.Sin(phase * 3.5f) * (1f - p / 0.18f) * 0.55f : 0f; data[i] = Mathf.Clamp((Mathf.Sin(phase) * 0.95f + snap) * env * 1.15f, -0.98f, 0.98f);
        }
        AudioClip c = AudioClip.Create(name, len, 1, SampleRate, false);
        c.SetData(data, 0);
        return c;
    }

    private AudioClip CreateEnergySweep(string name, float duration, float fStart, float fEnd)
    {
        int len = Mathf.RoundToInt(SampleRate * duration);
        float[] data = new float[len];
        float phase = 0f;
        for (int i = 0; i < len; i++)
        {
            float p = (float)i / len;
            float env = Mathf.Sin(Mathf.PI * p);
            phase += 2f * Mathf.PI * Mathf.Lerp(fStart, fEnd, p) / SampleRate;
            data[i] = (Mathf.Sin(phase) * 0.6f + Mathf.Sin(phase * 2.0f) * 0.25f) * env * 0.65f;
        }
        AudioClip c = AudioClip.Create(name, len, 1, SampleRate, false);
        c.SetData(data, 0);
        return c;
    }

    private AudioClip CreateTempestLoop(string name, float duration)
    {
        int len = Mathf.RoundToInt(SampleRate * duration);
        float[] data = new float[len];
        System.Random rng = new System.Random(99);
        for (int i = 0; i < len; i++)
        {
            float t = (float)i / SampleRate;
            float p = (float)i / len;
            float env = Mathf.Sin(Mathf.PI * p);
            float swoosh = 0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * 8f * t);
            float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
            data[i] = (noise * 0.35f + Mathf.Sin(2f * Mathf.PI * 260f * t) * 0.35f) * swoosh * env * 0.65f;
        }
        AudioClip c = AudioClip.Create(name, len, 1, SampleRate, false);
        c.SetData(data, 0);
        return c;
    }

    private AudioClip CreateArpeggio(string name, float[] notes, float noteDur)
    {
        int noteSamples = Mathf.RoundToInt(SampleRate * noteDur);
        int total = noteSamples * notes.Length;
        float[] data = new float[total];
        for (int n = 0; n < notes.Length; n++)
        {
            float f = notes[n];
            for (int j = 0; j < noteSamples; j++)
            {
                float p = (float)j / noteSamples;
                float env = Mathf.Sin(Mathf.PI * p);
                float t = (float)j / SampleRate;
                data[n * noteSamples + j] = (Mathf.Sin(2f * Mathf.PI * f * t) * 0.65f + Mathf.Sin(4f * Mathf.PI * f * t) * 0.20f) * env * 0.60f;
            }
        }
        AudioClip c = AudioClip.Create(name, total, 1, SampleRate, false);
        c.SetData(data, 0);
        return c;
    }

    private void LoadFont()
    {
        try { _uiFont = Font.CreateDynamicFontFromOSFont(new string[] { "Leelawadee UI", "Tahoma", "Segoe UI", "Arial" }, 20); }
        catch { _uiFont = null; }
        if (_uiFont == null) _uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
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
