#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class HeroSkillAndHUD : MonoBehaviour
{
    public static HeroSkillAndHUD Instance { get; private set; }

    [Header("Mana Settings")]
    public float maxMana = 100f;
    public float currentMana = 100f;
    public float manaRegenPerSec = 7.5f;

    [Header("Skill Costs & Cooldowns")]
    public float qManaCost = 20f;
    public float qCooldown = 3.5f;
    public float qDamage = 38f;
    public float qSpeed = 13.5f;
    public float qMaxDist = 9.5f;

    public float eManaCost = 15f;
    public float eCooldown = 3.0f;
    public float eDashSpeed = 16.5f;
    public float eDashDuration = 0.22f;

    public float rManaCost = 35f;
    public float rCooldown = 10.0f;
    public float rDuration = 1.5f;
    public float rRadius = 2.65f;
    public float rTickDamage = 16f;
    public float rLifestealPerHit = 5f;
    public float rMaxHealPerCast = 50f;

    [Header("Potions (Reset per Room)")]
    public int hpPotions = 3;
    public int mpPotions = 3;
    public float hpPotionAmount = 45f;
    public float mpPotionAmount = 40f;

    private float _qCdLeft = 0f;
    private float _eCdLeft = 0f;
    private float _rCdLeft = 0f;
    private bool _isDashing = false;
    private bool _isSpinning = false;

    private GameObject _playerGo;
    private Component _playerHealth;
    private Rigidbody2D _playerRb;
    private SpriteRenderer _playerSr;
    private Font _uiFont;

    private GameObject _hudCanvasGo;
    private Image _hpFill;
    private Image _hpTrail;
    private Text _hpText;
    private Image _mpFill;
    private Text _mpText;
    private Text _enemyCountText;
    private RectTransform _hpBarRoot;
    private float _hpTrailRatio = 1f;
    private float _lastHpRatio = 1f;
    private float _hpShakeTimer = 0f;

    private Image _qCdOverlay, _eCdOverlay, _rCdOverlay;
    private Text _qCdText, _eCdText, _rCdText;
    private Text _hpPotText, _mpPotText;
    private GameObject _statusModal;
    private Text _statusBodyText;
    private Text _toastText;
    private float _toastTimer = 0f;

    private static Sprite _whiteSprite;
    private static Sprite _circleSprite;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoInit()
    {
        if (Instance != null) return;
        GameObject go = new GameObject("HeroSkillAndHUD");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<HeroSkillAndHUD>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        LoadFont();
        EnsureProceduralSprites();
        SceneManager.sceneLoaded += OnSceneLoaded;
        SetupForScene(SceneManager.GetActiveScene().name);
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SetupForScene(scene.name);
    }

    private void SetupForScene(string sceneName)
    {
        StopAllCoroutines(); if (_hudCanvasGo != null) Destroy(_hudCanvasGo);
        _isDashing = false;
        _isSpinning = false;

        bool isGameplay = sceneName.StartsWith("Room", StringComparison.OrdinalIgnoreCase)
                       || sceneName.Equals("BossRoom", StringComparison.OrdinalIgnoreCase);
        if (!isGameplay) return;

        currentMana = maxMana;
        hpPotions = 3;
        mpPotions = 3;
        _qCdLeft = 0f;
        _eCdLeft = 0f;
        _rCdLeft = 0f;
        _hpTrailRatio = 1f;
        _lastHpRatio = 1f;

        BindPlayer();
        BuildHUD();
    }

    private void BindPlayer()
    {
        _playerGo = GameObject.FindGameObjectWithTag("Player");
        if (_playerGo == null)
        {
            PlayerController pc = FindFirstCompat<PlayerController>();
            if (pc != null) _playerGo = pc.gameObject;
        }
        if (_playerGo != null)
        {
            _playerHealth = _playerGo.GetComponent("Health");
            _playerRb = _playerGo.GetComponent<Rigidbody2D>();
            _playerSr = _playerGo.GetComponentInChildren<SpriteRenderer>();
        }
    }

    private void Update()
    {
        if (_hudCanvasGo == null) return;
        if (_playerGo == null) { BindPlayer(); if (_playerGo == null) return; }
        if (Time.timeScale <= 0f) return;

        float dt = Time.deltaTime;
        currentMana = Mathf.Min(maxMana, currentMana + manaRegenPerSec * dt);
        if (_qCdLeft > 0f) _qCdLeft = Mathf.Max(0f, _qCdLeft - dt);
        if (_eCdLeft > 0f) _eCdLeft = Mathf.Max(0f, _eCdLeft - dt);
        if (_rCdLeft > 0f) _rCdLeft = Mathf.Max(0f, _rCdLeft - dt);

#if ENABLE_INPUT_SYSTEM
        Keyboard kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.qKey.wasPressedThisFrame) TryCastSwordWave();
            if (kb.eKey.wasPressedThisFrame) TryCastShadowDash();
            if (kb.rKey.wasPressedThisFrame) TryCastBladeTempest();
            if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame) TryUseHpPotion();
            if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame) TryUseMpPotion();
            if (kb.tabKey.wasPressedThisFrame) ToggleStatusModal();
        }
#else
#if ENABLE_INPUT_SYSTEM
        Keyboard kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.qKey.wasPressedThisFrame) TryCastSwordWave();
            if (kb.eKey.wasPressedThisFrame) TryCastShadowDash();
            if (kb.rKey.wasPressedThisFrame) TryCastBladeTempest();
            if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame) TryUseHpPotion();
            if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame) TryUseMpPotion();
            if (kb.tabKey.wasPressedThisFrame) ToggleStatusModal();
        }
#else
        if (Input.GetKeyDown(KeyCode.Q)) TryCastSwordWave();
        if (Input.GetKeyDown(KeyCode.E)) TryCastShadowDash();
        if (Input.GetKeyDown(KeyCode.R)) TryCastBladeTempest();
        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) TryUseHpPotion();
        if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) TryUseMpPotion();
        if (Input.GetKeyDown(KeyCode.Tab)) ToggleStatusModal();
#endif
#endif

        UpdateHUDVisuals(dt);

        if (_toastTimer > 0f)
        {
            _toastTimer -= dt;
            if (_toastTimer <= 0f && _toastText != null) _toastText.text = "";
        }
    }

    // [Q] SWORD WAVE — PIERCING CRESCENT SLASH TOWARD MOUSE
    public void TryCastSwordWave()
    {
        if (_playerGo == null) return;
        if (_qCdLeft > 0f) { ShowToast("Sword Wave ติดคูลดาวน์"); return; }
        if (currentMana < qManaCost) { ShowToast("มานาไม่พอ!"); return; }

        currentMana -= qManaCost;
        _qCdLeft = qCooldown;

        Vector3 dir = GetAimDirection();
        GameObject wave = new GameObject("SwordWaveProjectile");
        wave.transform.position = _playerGo.transform.position + dir * 0.65f;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        wave.transform.rotation = Quaternion.Euler(0f, 0f, angle);

        GameObject outer = new GameObject("OuterArc");
        outer.transform.SetParent(wave.transform, false);
        SpriteRenderer sr1 = outer.AddComponent<SpriteRenderer>();
        sr1.sprite = _circleSprite;
        sr1.color = new Color(0.28f, 0.85f, 1.0f, 0.92f);
        sr1.sortingOrder = 5500;
        outer.transform.localScale = new Vector3(0.55f, 1.65f, 1f);

        GameObject inner = new GameObject("InnerArc");
        inner.transform.SetParent(wave.transform, false);
        SpriteRenderer sr2 = inner.AddComponent<SpriteRenderer>();
        sr2.sprite = _circleSprite;
        sr2.color = new Color(1.0f, 0.95f, 0.65f, 0.95f);
        sr2.sortingOrder = 5501;
        inner.transform.localScale = new Vector3(0.28f, 1.25f, 1f);
        inner.transform.localPosition = new Vector3(0.10f, 0f, 0f);

        SwordWaveProjectile proj = wave.AddComponent<SwordWaveProjectile>();
        proj.Init(dir, qSpeed, qMaxDist, qDamage, _playerGo);
        GameAudioManager.PlaySwordWave(); NotifyTelemetry("RecordSkillUsed");
    }

    // [E] SHADOW DASH — QUICK DASH + AFTERIMAGES + I-FRAMES
    public void TryCastShadowDash()
    {
        if (_playerGo == null || _isDashing) return;
        if (_eCdLeft > 0f) { ShowToast("Shadow Dash ติดคูลดาวน์"); return; }
        if (currentMana < eManaCost) { ShowToast("มานาไม่พอ!"); return; }

        currentMana -= eManaCost;
        _eCdLeft = eCooldown;
        GameAudioManager.PlayDash(); StartCoroutine(ShadowDashCoroutine()); NotifyTelemetry("RecordSkillUsed");
    }

    private IEnumerator ShadowDashCoroutine()
    {
        _isDashing = true;
        PlayerController pc = _playerGo.GetComponent<PlayerController>();
        if (pc != null) pc.enabled = false;

        try
        {
            Vector2 moveInput = Vector2.zero;
#if ENABLE_INPUT_SYSTEM
            Keyboard kbDash = Keyboard.current;
            if (kbDash != null)
            {
                if (kbDash.aKey.isPressed || kbDash.leftArrowKey.isPressed) moveInput.x -= 1f;
                if (kbDash.dKey.isPressed || kbDash.rightArrowKey.isPressed) moveInput.x += 1f;
                if (kbDash.sKey.isPressed || kbDash.downArrowKey.isPressed) moveInput.y -= 1f;
                if (kbDash.wKey.isPressed || kbDash.upArrowKey.isPressed) moveInput.y += 1f;
            }
#else
            moveInput = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
#endif
            Vector3 dir = moveInput.sqrMagnitude > 0.01f ? (Vector3)moveInput.normalized : GetAimDirection();

            TryInvoke(_playerHealth, "GrantInvulnerability", 0.32f);
            TrySetFloat(_playerHealth, "invulnerableTimer", 0.32f);

            float elapsed = 0f;
            float nextGhost = 0f;

            while (elapsed < eDashDuration && _playerGo != null)
            {
                if (_playerRb != null)
                {
#if UNITY_6000_0_OR_NEWER
                    _playerRb.linearVelocity = dir * eDashSpeed;
#else
                    _playerRb.velocity = dir * eDashSpeed;
#endif
                }
                else
                {
                    _playerGo.transform.position += dir * eDashSpeed * Time.deltaTime;
                }

                if (elapsed >= nextGhost)
                {
                    SpawnAfterimage();
                    nextGhost += 0.055f;
                }

                elapsed += Time.deltaTime;
                yield return null;
            }
        }
        finally
        {
            if (pc != null) pc.enabled = true;
            _isDashing = false;
        }
    }

    private void SpawnAfterimage()
    {
        if (_playerSr == null || _playerGo == null) return;
        GameObject ghost = new GameObject("DashAfterimage");
        ghost.transform.position = _playerSr.transform.position;
        ghost.transform.rotation = _playerSr.transform.rotation;
        ghost.transform.localScale = _playerSr.transform.lossyScale;

        SpriteRenderer sr = ghost.AddComponent<SpriteRenderer>();
        sr.sprite = _playerSr.sprite;
        sr.flipX = _playerSr.flipX;
        sr.color = new Color(0.42f, 0.72f, 1.0f, 0.55f);
        sr.sortingOrder = _playerSr.sortingOrder - 1;
        StartCoroutine(FadeAndDestroy(sr, 0.24f));
    }

    // [R] BLADE TEMPEST — 1.5s SPINNING WHIRLWIND + LIFESTEAL
    public void TryCastBladeTempest()
    {
        if (_playerGo == null || _isSpinning) return;
        if (_rCdLeft > 0f) { ShowToast("Blade Tempest ติดคูลดาวน์"); return; }
        if (currentMana < rManaCost) { ShowToast("มานาไม่พอ!"); return; }

        currentMana -= rManaCost;
        _rCdLeft = rCooldown;
        GameAudioManager.PlayTempest(); StartCoroutine(BladeTempestCoroutine()); NotifyTelemetry("RecordSkillUsed");
    }

    private IEnumerator BladeTempestCoroutine()
    {
        _isSpinning = true;

        GameObject vortex = new GameObject("BladeTempestVortex");
        vortex.transform.SetParent(_playerGo.transform, false);
        vortex.transform.localPosition = Vector3.zero;

        SpriteRenderer ringSr = vortex.AddComponent<SpriteRenderer>();
        ringSr.sprite = _circleSprite;
        ringSr.color = new Color(1.0f, 0.38f, 0.26f, 0.32f);
        ringSr.sortingOrder = 5400;
        vortex.transform.localScale = Vector3.one * (rRadius * 2f);

        GameObject blades = new GameObject("SpinningBlades");
        blades.transform.SetParent(vortex.transform, false);
        for (int i = 0; i < 2; i++)
        {
            GameObject b = new GameObject("Blade_" + i);
            b.transform.SetParent(blades.transform, false);
            SpriteRenderer bsr = b.AddComponent<SpriteRenderer>();
            bsr.sprite = _circleSprite;
            bsr.color = new Color(1.0f, 0.85f, 0.35f, 0.85f);
            bsr.sortingOrder = 5405;
            float sign = (i == 0) ? 1f : -1f;
            b.transform.localPosition = new Vector3(sign * 0.36f, 0f, 0f);
            b.transform.localScale = new Vector3(0.12f, 0.65f, 1f);
        }

        float elapsed = 0f;
        float tickInterval = 0.30f;
        float nextTick = 0f;
        float healedThisCast = 0f;

        while (elapsed < rDuration && _playerGo != null && vortex != null && blades != null)
        {
            blades.transform.Rotate(0f, 0f, -720f * Time.deltaTime);

            if (elapsed >= nextTick)
            {
                nextTick += tickInterval;
                Collider2D[] hits = Physics2D.OverlapCircleAll(_playerGo.transform.position, rRadius);
                HashSet<Component> hitSet = new HashSet<Component>();

                foreach (Collider2D col in hits)
                {
                    if (col == null || col.gameObject == _playerGo || col.transform.IsChildOf(_playerGo.transform)) continue;
                    Component h = col.GetComponentInParent(typeof(Health));
                    if (h == null || h == _playerHealth || hitSet.Contains(h)) continue;
                    hitSet.Add(h);

                    TryInvoke(h, "TakeDamage", rTickDamage);

                    Rigidbody2D erb = h.GetComponent<Rigidbody2D>();
                    if (erb != null)
                    {
                        Vector2 push = ((Vector2)(h.transform.position - _playerGo.transform.position)).normalized;
                        erb.AddForce(push * 2.2f, ForceMode2D.Impulse);
                    }

                    if (healedThisCast < rMaxHealPerCast)
                    {
                        float healAmt = Mathf.Min(rLifestealPerHit, rMaxHealPerCast - healedThisCast);
                        HealPlayer(healAmt);
                        healedThisCast += healAmt;
                    }
                }
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (vortex != null) Destroy(vortex);
        _isSpinning = false;
    }

    public void TryUseHpPotion()
    {
        if (hpPotions <= 0) { ShowToast("ยาเลือดหมดแล้ว!"); return; }
        if (_playerHealth == null) return;
        float cur = GetPlayerHp();
        float max = GetPlayerMaxHp();
        if (cur >= max - 0.5f) { ShowToast("เลือดเต็มอยู่แล้ว!"); return; }

        hpPotions--;
        HealPlayer(hpPotionAmount);
        SpawnRingFlash(new Color(0.28f, 0.95f, 0.48f, 0.60f), 1.6f);
        ShowToast($"+{hpPotionAmount:F0} HP");
        GameAudioManager.PlayPotion(); NotifyTelemetry("RecordPotionUsed");
    }

    public void TryUseMpPotion()
    {
        if (mpPotions <= 0) { ShowToast("ยามานาหมดแล้ว!"); return; }
        if (currentMana >= maxMana - 0.5f) { ShowToast("มานาเต็มอยู่แล้ว!"); return; }

        mpPotions--;
        currentMana = Mathf.Min(maxMana, currentMana + mpPotionAmount);
        SpawnRingFlash(new Color(0.32f, 0.72f, 1.0f, 0.60f), 1.6f);
        ShowToast($"+{mpPotionAmount:F0} MP");
        GameAudioManager.PlayPotion(); NotifyTelemetry("RecordPotionUsed");
    }

    private void HealPlayer(float amount)
    {
        if (_playerHealth == null) return;
        if (TryInvoke(_playerHealth, "Heal", amount)) return;
        float cur = GetPlayerHp();
        float max = GetPlayerMaxHp();
        float next = Mathf.Min(max, cur + amount);
        TrySetFloat(_playerHealth, "currentHealth", next);
        TrySetFloat(_playerHealth, "currentHP", next);
        TrySetFloat(_playerHealth, "CurrentHP", next);
    }

    private void SpawnRingFlash(Color color, float targetScale)
    {
        if (_playerGo == null) return;
        GameObject fx = new GameObject("PotionFlash");
        fx.transform.position = _playerGo.transform.position;
        SpriteRenderer sr = fx.AddComponent<SpriteRenderer>();
        sr.sprite = _circleSprite;
        sr.color = color;
        sr.sortingOrder = 5500;
        StartCoroutine(ExpandAndFade(fx.transform, sr, targetScale, 0.35f));
    }

    private void BuildHUD()
    {
        _hudCanvasGo = new GameObject("HeroSkillAndHUD_Canvas");
        Canvas canvas = _hudCanvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        CanvasScaler scaler = _hudCanvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        _hudCanvasGo.AddComponent<GraphicRaycaster>();

        GameObject topLeft = CreatePanel(_hudCanvasGo.transform, "VitalPanel", new Color(0.07f, 0.08f, 0.13f, 0.86f));
        SetAnchors(topLeft, 0.015f, 0.875f, 0.255f, 0.980f);
        AddOutline(topLeft, new Color(0.85f, 0.68f, 0.28f, 0.55f));

        GameObject hpBg = CreatePanel(topLeft.transform, "HpBg", new Color(0.18f, 0.08f, 0.10f, 1f));
        SetAnchors(hpBg, 0.05f, 0.54f, 0.95f, 0.88f);
        _hpBarRoot = hpBg.GetComponent<RectTransform>();

        GameObject hpTrailGo = CreatePanel(hpBg.transform, "HpTrail", new Color(0.98f, 0.78f, 0.28f, 0.90f));
        SetAnchors(hpTrailGo, 0f, 0f, 1f, 1f);
        _hpTrail = hpTrailGo.GetComponent<Image>();

        GameObject hpFillGo = CreatePanel(hpBg.transform, "HpFill", new Color(0.86f, 0.18f, 0.22f, 1f));
        SetAnchors(hpFillGo, 0f, 0f, 1f, 1f);
        _hpFill = hpFillGo.GetComponent<Image>();

        _hpText = CreateText(hpBg.transform, "HpTxt", "HP 180 / 180", 17, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter, 0f, 0f, 1f, 1f);

        GameObject mpBg = CreatePanel(topLeft.transform, "MpBg", new Color(0.08f, 0.12f, 0.22f, 1f));
        SetAnchors(mpBg, 0.05f, 0.14f, 0.95f, 0.44f);
        GameObject mpFillGo = CreatePanel(mpBg.transform, "MpFill", new Color(0.20f, 0.55f, 0.95f, 1f));
        SetAnchors(mpFillGo, 0f, 0f, 1f, 1f);
        _mpFill = mpFillGo.GetComponent<Image>();
        _mpText = CreateText(mpBg.transform, "MpTxt", "MP 100 / 100", 15, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter, 0f, 0f, 1f, 1f);

        GameObject enemyBox = CreatePanel(_hudCanvasGo.transform, "EnemyCounterBox", new Color(0.08f, 0.07f, 0.11f, 0.85f));
        SetAnchors(enemyBox, 0.81f, 0.92f, 0.985f, 0.98f);
        AddOutline(enemyBox, new Color(0.90f, 0.66f, 0.26f, 0.55f));
        _enemyCountText = CreateText(enemyBox.transform, "EnemyTxt", "ENEMIES LEFT: 0", 20, FontStyle.Bold,
            new Color(0.99f, 0.86f, 0.45f), TextAnchor.MiddleCenter, 0f, 0f, 1f, 1f);

        GameObject bar = CreatePanel(_hudCanvasGo.transform, "ActionBar", new Color(0.06f, 0.08f, 0.13f, 0.88f));
        SetAnchors(bar, 0.27f, 0.02f, 0.73f, 0.125f);
        AddOutline(bar, new Color(0.85f, 0.68f, 0.28f, 0.50f));

        BuildSlot(bar.transform, "Q", "Sword Wave", new Color(0.18f, 0.56f, 0.76f), 0.02f, 0.20f, out _qCdOverlay, out _qCdText, TryCastSwordWave);
        BuildSlot(bar.transform, "E", "Shadow Dash", new Color(0.45f, 0.26f, 0.72f), 0.22f, 0.40f, out _eCdOverlay, out _eCdText, TryCastShadowDash);
        BuildSlot(bar.transform, "R", "Blade Tempest", new Color(0.76f, 0.26f, 0.22f), 0.42f, 0.60f, out _rCdOverlay, out _rCdText, TryCastBladeTempest);
        BuildPotionSlot(bar.transform, "1", "HP Pot", new Color(0.20f, 0.60f, 0.34f), 0.64f, 0.80f, out _hpPotText, TryUseHpPotion);
        BuildPotionSlot(bar.transform, "2", "MP Pot", new Color(0.22f, 0.44f, 0.78f), 0.82f, 0.98f, out _mpPotText, TryUseMpPotion);

        _toastText = CreateText(_hudCanvasGo.transform, "SkillToast", "", 20, FontStyle.Bold,
            new Color(1f, 0.92f, 0.48f), TextAnchor.MiddleCenter, 0.25f, 0.135f, 0.75f, 0.185f);

        _statusModal = CreatePanel(_hudCanvasGo.transform, "BlindedStatusModal", new Color(0.07f, 0.09f, 0.14f, 0.95f));
        SetAnchors(_statusModal, 0.33f, 0.24f, 0.67f, 0.76f);
        AddOutline(_statusModal, new Color(0.88f, 0.70f, 0.30f, 0.75f));
        CreateText(_statusModal.transform, "StHeader", "CHARACTER STATUS  [TAB]", 24, FontStyle.Bold,
            new Color(0.98f, 0.86f, 0.48f), TextAnchor.MiddleCenter, 0.05f, 0.84f, 0.95f, 0.96f);
        _statusBodyText = CreateText(_statusModal.transform, "StBody", "", 19, FontStyle.Normal,
            new Color(0.88f, 0.91f, 0.96f), TextAnchor.UpperLeft, 0.08f, 0.08f, 0.92f, 0.80f);
        _statusModal.SetActive(false);
    }

    private void BuildSlot(Transform parent, string key, string label, Color accent, float x0, float x1,
        out Image cdOverlay, out Text cdText, Action onClick)
    {
        GameObject slot = CreatePanel(parent, "Slot_" + key, new Color(0.12f, 0.14f, 0.21f, 1f));
        SetAnchors(slot, x0, 0.12f, x1, 0.88f);
        AddOutline(slot, accent);
        Button btn = slot.AddComponent<Button>();
        btn.onClick.AddListener(() => onClick?.Invoke());

        GameObject tag = CreatePanel(slot.transform, "KeyTag", accent);
        SetAnchors(tag, 0.04f, 0.62f, 0.32f, 0.95f);
        CreateText(tag.transform, "K", key, 15, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter, 0f, 0f, 1f, 1f);
        CreateText(slot.transform, "Name", label, 15, FontStyle.Bold, new Color(0.95f, 0.93f, 0.86f), TextAnchor.MiddleCenter, 0.04f, 0.06f, 0.96f, 0.56f);

        GameObject ov = CreatePanel(slot.transform, "CdOverlay", new Color(0f, 0f, 0f, 0.68f));
        SetAnchors(ov, 0f, 0f, 1f, 0f);
        cdOverlay = ov.GetComponent<Image>();
        cdText = CreateText(slot.transform, "CdTxt", "", 22, FontStyle.Bold, new Color(1f, 0.90f, 0.42f), TextAnchor.MiddleCenter, 0f, 0f, 1f, 1f);
    }

    private void BuildPotionSlot(Transform parent, string key, string label, Color accent, float x0, float x1,
        out Text countTxt, Action onClick)
    {
        GameObject slot = CreatePanel(parent, "Pot_" + key, new Color(0.12f, 0.14f, 0.21f, 1f));
        SetAnchors(slot, x0, 0.12f, x1, 0.88f);
        AddOutline(slot, accent);
        Button btn = slot.AddComponent<Button>();
        btn.onClick.AddListener(() => onClick?.Invoke());

        GameObject tag = CreatePanel(slot.transform, "KeyTag", accent);
        SetAnchors(tag, 0.05f, 0.60f, 0.34f, 0.94f);
        CreateText(tag.transform, "K", key, 15, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter, 0f, 0f, 1f, 1f);
        CreateText(slot.transform, "Name", label, 14, FontStyle.Bold, new Color(0.92f, 0.94f, 0.98f), TextAnchor.MiddleCenter, 0.05f, 0.05f, 0.95f, 0.45f);
        countTxt = CreateText(slot.transform, "Count", "x2", 20, FontStyle.Bold, new Color(0.55f, 0.98f, 0.68f), TextAnchor.MiddleRight, 0.38f, 0.50f, 0.92f, 0.94f);
    }

    private void UpdateHUDVisuals(float dt)
    {
        float hp = GetPlayerHp();
        float maxHp = Mathf.Max(1f, GetPlayerMaxHp());
        float hpRatio = Mathf.Clamp01(hp / maxHp);

        if (hpRatio < _lastHpRatio - 0.005f) _hpShakeTimer = 0.22f;
        _lastHpRatio = hpRatio;

        if (_hpTrailRatio > hpRatio) _hpTrailRatio = Mathf.MoveTowards(_hpTrailRatio, hpRatio, dt * 0.55f);
        else _hpTrailRatio = hpRatio;

        if (_hpFill != null) _hpFill.rectTransform.anchorMax = new Vector2(hpRatio, 1f);
        if (_hpTrail != null) _hpTrail.rectTransform.anchorMax = new Vector2(_hpTrailRatio, 1f);
        if (_hpText != null) _hpText.text = $"HP {Mathf.CeilToInt(hp)} / {Mathf.RoundToInt(maxHp)}";

        if (_hpFill != null)
        {
            if (hpRatio <= 0.30f)
            {
                float pulse = 0.65f + 0.35f * Mathf.Abs(Mathf.Sin(Time.time * 7f));
                _hpFill.color = new Color(0.98f * pulse, 0.14f, 0.18f, 1f);
            }
            else
            {
                _hpFill.color = new Color(0.86f, 0.18f, 0.22f, 1f);
            }
        }

        if (_hpBarRoot != null)
        {
            if (_hpShakeTimer > 0f)
            {
                _hpShakeTimer -= dt;
                _hpBarRoot.anchoredPosition = UnityEngine.Random.insideUnitCircle * 4f;
            }
            else
            {
                _hpBarRoot.anchoredPosition = Vector2.zero;
            }
        }

        float mpRatio = Mathf.Clamp01(currentMana / Mathf.Max(1f, maxMana));
        if (_mpFill != null) _mpFill.rectTransform.anchorMax = new Vector2(mpRatio, 1f);
        if (_mpText != null) _mpText.text = $"MP {Mathf.FloorToInt(currentMana)} / {Mathf.RoundToInt(maxMana)}";

        UpdateCdSlot(_qCdOverlay, _qCdText, _qCdLeft, qCooldown);
        UpdateCdSlot(_eCdOverlay, _eCdText, _eCdLeft, eCooldown);
        UpdateCdSlot(_rCdOverlay, _rCdText, _rCdLeft, rCooldown);

        if (_hpPotText != null) _hpPotText.text = "x" + hpPotions;
        if (_mpPotText != null) _mpPotText.text = "x" + mpPotions;

        if (_enemyCountText != null)
        {
            EnemySpawner sp = FindFirstCompat<EnemySpawner>();
            int rem = sp != null ? GetEnemyRemaining(sp) : 0;
            _enemyCountText.text = rem > 0 ? $"ENEMIES LEFT: {rem}" : "PORTAL OPEN!";
            _enemyCountText.color = rem > 0 ? new Color(0.99f, 0.86f, 0.45f) : new Color(0.68f, 0.98f, 0.72f);
        }

        if (_statusModal != null && _statusModal.activeSelf && _statusBodyText != null)
        {
            _statusBodyText.text =
                $"HP:           {Mathf.CeilToInt(hp)} / {Mathf.RoundToInt(maxHp)}\n" +
                $"Mana:         {Mathf.FloorToInt(currentMana)} / {Mathf.RoundToInt(maxMana)}\n" +
                $"Move Speed:   {PlayerConfig.MoveSpeed:F1}\n" +
                $"Attack Power: {PlayerConfig.AttackDamage:F0}\n" +
                $"Attack CD:    {PlayerConfig.AttackCooldown:F2}s\n\n" +
                $"[Q] Sword Wave    (DMG {qDamage:F0} | MP {qManaCost:F0})\n" +
                $"[E] Shadow Dash   (i-Frame 0.32s | MP {eManaCost:F0})\n" +
                $"[R] Blade Tempest (Spin + Lifesteal | MP {rManaCost:F0})\n\n" +
                $"HP Potions [1]: {hpPotions}   |   MP Potions [2]: {mpPotions}";
        }
    }

    private void UpdateCdSlot(Image ov, Text txt, float left, float total)
    {
        if (ov == null || txt == null) return;
        float ratio = total > 0f ? Mathf.Clamp01(left / total) : 0f;
        ov.rectTransform.anchorMax = new Vector2(1f, ratio);
        txt.text = left > 0.05f ? left.ToString("F1") : "";
    }

    private void ShowToast(string msg)
    {
        if (_toastText == null) return;
        _toastText.text = msg;
        _toastTimer = 1.4f;
    }

    private void ToggleStatusModal()
    {
        if (_statusModal != null) _statusModal.SetActive(!_statusModal.activeSelf);
    }

    private Vector3 GetAimDirection()
    {
        Camera cam = Camera.main;
        if (cam == null || _playerGo == null) return Vector3.right;
        Vector3 mousePos = Vector3.zero;
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
        {
            Vector2 mp = Mouse.current.position.ReadValue();
            mousePos = new Vector3(mp.x, mp.y, 0f);
        }
#else
        mousePos = Input.mousePosition;
#endif
        mousePos.z = Mathf.Abs(cam.transform.position.z - _playerGo.transform.position.z);
        Vector3 world = cam.ScreenToWorldPoint(mousePos);
        world.z = _playerGo.transform.position.z;
        Vector3 dir = world - _playerGo.transform.position;
        return dir.sqrMagnitude > 0.001f ? dir.normalized : Vector3.right;
    }

    private float GetPlayerHp()
    {
        if (_playerHealth == null) return PlayerConfig.MaxHP;
        if (TryGetFloat(_playerHealth, "currentHealth", out float v)) return v;
        if (TryGetFloat(_playerHealth, "currentHP", out v)) return v;
        if (TryGetFloat(_playerHealth, "CurrentHP", out v)) return v;
        return PlayerConfig.MaxHP;
    }

    private float GetPlayerMaxHp()
    {
        if (_playerHealth == null) return PlayerConfig.MaxHP;
        if (TryGetFloat(_playerHealth, "maxHealth", out float v) && v > 0f) return v;
        if (TryGetFloat(_playerHealth, "maxHP", out v) && v > 0f) return v;
        if (TryGetFloat(_playerHealth, "MaxHP", out v) && v > 0f) return v;
        return PlayerConfig.MaxHP;
    }

    private int GetEnemyRemaining(EnemySpawner sp)
    {
        Type t = sp.GetType();
        PropertyInfo p = t.GetProperty("RemainingCount");
        if (p != null) return Convert.ToInt32(p.GetValue(sp));
        FieldInfo f = t.GetField("remainingEnemies", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (f != null) return Convert.ToInt32(f.GetValue(sp));
        return 0;
    }

    private void NotifyTelemetry(string methodName)
    {
        if (GameplayTelemetry.Instance == null) return;
        Type gt = GameplayTelemetry.Instance.GetType();
        if (methodName == "RecordSkillUsed")
        {
            // เรียกเมธอดถ้ามี หรือบวก currentRoom.skillUses ตรงๆ
            MethodInfo m = gt.GetMethod("RecordSkillUse", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                        ?? gt.GetMethod("LogSkillUse", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                        ?? gt.GetMethod("RecordSkillUsed", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (m != null && m.GetParameters().Length == 0) { m.Invoke(GameplayTelemetry.Instance, null); return; }

            FieldInfo fCur = gt.GetField("currentRoom", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (fCur != null)
            {
                object roomObj = fCur.GetValue(GameplayTelemetry.Instance);
                if (roomObj != null)
                {
                    FieldInfo fSk = roomObj.GetType().GetField("skillUses", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (fSk != null && fSk.FieldType == typeof(int))
                    {
                        fSk.SetValue(roomObj, (int)fSk.GetValue(roomObj) + 1);
                    }
                }
            }
        }
    }

    private IEnumerator FadeAndDestroy(SpriteRenderer sr, float dur)
    {
        float t = 0f;
        Color c = sr.color;
        while (t < dur && sr != null)
        {
            t += Time.deltaTime;
            c.a = Mathf.Lerp(0.55f, 0f, t / dur);
            sr.color = c;
            yield return null;
        }
        if (sr != null) Destroy(sr.gameObject);
    }

    private IEnumerator ExpandAndFade(Transform tr, SpriteRenderer sr, float targetScale, float dur)
    {
        float t = 0f;
        Color c = sr.color;
        while (t < dur && tr != null && sr != null)
        {
            t += Time.deltaTime;
            float p = t / dur;
            tr.localScale = Vector3.one * Mathf.Lerp(0.3f, targetScale, p);
            c.a = Mathf.Lerp(0.60f, 0f, p);
            sr.color = c;
            yield return null;
        }
        if (tr != null) Destroy(tr.gameObject);
    }

    private void LoadFont()
    {
        try { _uiFont = Font.CreateDynamicFontFromOSFont(new string[] { "Leelawadee UI", "Tahoma", "Segoe UI", "Arial" }, 20); }
        catch { _uiFont = null; }
        if (_uiFont == null) _uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    private static void EnsureProceduralSprites()
    {
        if (_whiteSprite == null)
        {
            Texture2D tex = new Texture2D(4, 4);
            Color[] cols = new Color[16];
            for (int i = 0; i < 16; i++) cols[i] = Color.white;
            tex.SetPixels(cols); tex.Apply();
            _whiteSprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
        }
        if (_circleSprite == null)
        {
            int sz = 64;
            Texture2D tex = new Texture2D(sz, sz, TextureFormat.RGBA32, false);
            float r = sz * 0.5f;
            for (int y = 0; y < sz; y++)
            {
                for (int x = 0; x < sz; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(r, r));
                    float a = Mathf.Clamp01((r - d) / 4f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply();
            _circleSprite = Sprite.Create(tex, new Rect(0, 0, sz, sz), new Vector2(0.5f, 0.5f), 64f);
        }
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

    private bool TryInvoke(object target, string method, float arg)
    {
        if (target == null) return false;
        MethodInfo m = target.GetType().GetMethod(method, new Type[] { typeof(float) });
        if (m != null) { m.Invoke(target, new object[] { arg }); return true; }
        return false;
    }

    private bool TryGetFloat(object target, string name, out float val)
    {
        val = 0f;
        if (target == null) return false;
        Type t = target.GetType();
        FieldInfo f = t.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (f != null && f.FieldType == typeof(float)) { val = (float)f.GetValue(target); return true; }
        PropertyInfo p = t.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (p != null && p.PropertyType == typeof(float)) { val = (float)p.GetValue(target); return true; }
        return false;
    }

    private void TrySetFloat(object target, string name, float val)
    {
        if (target == null) return;
        Type t = target.GetType();
        FieldInfo f = t.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (f != null && f.FieldType == typeof(float)) { f.SetValue(target, val); return; }
        PropertyInfo p = t.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (p != null && p.CanWrite && p.PropertyType == typeof(float)) { p.SetValue(target, val); }
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

public class SwordWaveProjectile : MonoBehaviour
{
    private Vector3 _dir;
    private float _speed;
    private float _maxDist;
    private float _damage;
    private GameObject _owner;
    private float _traveled = 0f;
    private readonly HashSet<Component> _hitTargets = new HashSet<Component>();

    public void Init(Vector3 dir, float speed, float maxDist, float damage, GameObject owner)
    {
        _dir = dir.normalized;
        _speed = speed;
        _maxDist = maxDist;
        _damage = damage;
        _owner = owner;
    }

    private void Update()
    {
        float step = _speed * Time.deltaTime;
        transform.position += _dir * step;
        _traveled += step;

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, 0.85f);
        foreach (Collider2D col in hits)
        {
            if (col == null) continue;
            if (_owner != null && (col.gameObject == _owner || col.transform.IsChildOf(_owner.transform))) continue;

            Component h = col.GetComponentInParent(typeof(Health));
            if (h != null && !_hitTargets.Contains(h))
            {
                _hitTargets.Add(h);
                MethodInfo m = h.GetType().GetMethod("TakeDamage", new Type[] { typeof(float) });
                if (m != null) m.Invoke(h, new object[] { _damage });

                Rigidbody2D rb = h.GetComponent<Rigidbody2D>();
                if (rb != null) rb.AddForce((Vector2)_dir * 3.0f, ForceMode2D.Impulse);
            }
            else if (!col.isTrigger && h == null && (col.gameObject.name.IndexOf("Wall", StringComparison.OrdinalIgnoreCase) >= 0 || col.GetComponent<UnityEngine.Tilemaps.TilemapCollider2D>() != null))
            {
                Destroy(gameObject);
                return;
            }
        }

        if (_traveled >= _maxDist) Destroy(gameObject);
    }
}
