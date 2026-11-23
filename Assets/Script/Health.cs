using UnityEngine.SceneManagement;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class Health : MonoBehaviour
{
    public float maxHealth = 100f;
    public float currentHealth;
    public Image healthBarFill;

    private bool isPlayer;
    private bool isDead;
    private bool isInvulnerable;
    private Vector3 spawnPosition;

    private Animator anim;
    private bool hasSpeed, hasHurt, hasDie, hasStunedTrigger, hasDeathTrigger;
    private SpriteRenderer[] allRenderers;
    private Color[] baseColors;
    private Vector3 initialUiBarScale = Vector3.one;

    private Transform barRoot;
    private Transform barFillTransform;
    private float barFullWidth = 0.9f;
    private float invScaleX = 1f;
    private float invScaleY = 1f;
    private static Sprite whitePixelSprite;

    void Awake()
    {
        isPlayer = CompareTag("Player");
        spawnPosition = transform.position;
        anim = GetComponentInChildren<Animator>();
        if (anim != null && anim.runtimeAnimatorController != null)
        {
            foreach (var p in anim.parameters)
            {
                if (p.name == "Speed") hasSpeed = true;
                if (p.name == "Hurt") hasHurt = true;
                if (p.name == "Die") hasDie = true;
                if (p.name == "StunedTrigger") hasStunedTrigger = true;
                if (p.name == "DeathTrigger") hasDeathTrigger = true;
            }
        }

        allRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        baseColors = new Color[allRenderers.Length];
        for (int i = 0; i < allRenderers.Length; i++) baseColors[i] = allRenderers[i].color;

        if (isPlayer)
        {
            maxHealth = PlayerConfig.MaxHP;
            if (healthBarFill == null)
            {
                GameObject hpObj = GameObject.Find("PlayerHp");
                if (hpObj != null) healthBarFill = hpObj.GetComponent<Image>();
            }
            if (healthBarFill != null) initialUiBarScale = healthBarFill.rectTransform.localScale;
        }
        else if (healthBarFill == null)
        {
            CreateFloatingEnemyBar();
        }
    }

    void Start()
    {
        currentHealth = maxHealth;
        UpdateHealthBar();
    }

    void LateUpdate()
    {
        if (barRoot != null)
        {
            float sx = transform.localScale.x < 0f ? -invScaleX : invScaleX;
            barRoot.localScale = new Vector3(sx, invScaleY, 1f);
        }
    }

    private void CreateFloatingEnemyBar()
    {
        if (whitePixelSprite == null)
        {
            Texture2D tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            whitePixelSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        }

        float absX = Mathf.Max(0.1f, Mathf.Abs(transform.localScale.x));
        float absY = Mathf.Max(0.1f, Mathf.Abs(transform.localScale.y));
        invScaleX = 1f / absX;
        invScaleY = 1f / absY;

        float topWorldOffset = 1.0f;
        if (allRenderers != null && allRenderers.Length > 0)
        {
            float maxY = transform.position.y + 0.8f;
            foreach (var r in allRenderers)
                if (r != null && r.sprite != null && r.bounds.max.y > maxY) maxY = r.bounds.max.y;
            topWorldOffset = Mathf.Clamp((maxY - transform.position.y) + 0.25f, 0.9f, 2.8f);
        }

        bool isBoss = gameObject.name.ToLowerInvariant().Contains("boss");
        barFullWidth = isBoss ? 1.4f : 0.95f;
        float barHeight = isBoss ? 0.16f : 0.12f;

        GameObject root = new GameObject("HPBar_Floating");
        root.transform.SetParent(transform, false);
        root.transform.localPosition = new Vector3(0f, topWorldOffset * invScaleY, 0f);
        root.transform.localScale = new Vector3(invScaleX, invScaleY, 1f);
        barRoot = root.transform;

        GameObject bg = new GameObject("BG");
        bg.transform.SetParent(barRoot, false);
        bg.transform.localScale = new Vector3(barFullWidth + 0.06f, barHeight + 0.06f, 1f);
        var bgSr = bg.AddComponent<SpriteRenderer>();
        bgSr.sprite = whitePixelSprite;
        bgSr.color = new Color(0.08f, 0.08f, 0.08f, 0.88f);
        bgSr.sortingOrder = 9999;

        GameObject fill = new GameObject("Fill");
        fill.transform.SetParent(barRoot, false);
        fill.transform.localScale = new Vector3(barFullWidth, barHeight, 1f);
        var fillSr = fill.AddComponent<SpriteRenderer>();
        fillSr.sprite = whitePixelSprite;
        fillSr.color = new Color(0.95f, 0.18f, 0.18f, 1f);
        fillSr.sortingOrder = 10000;
        barFillTransform = fill.transform;
    }

    public void TakeDamage(float damage)
    {
        if (CompareTag("Player")) GameAudioManager.PlayHurt(); else GameAudioManager.PlayHit();
        if (isDead || isInvulnerable) return;

        float before = currentHealth;
        currentHealth = Mathf.Clamp(currentHealth - damage, 0, maxHealth);
        float applied = before - currentHealth;

        if (isPlayer && GameplayTelemetry.Instance != null)
            GameplayTelemetry.Instance.RecordDamageTaken(applied);

        UpdateHealthBar();

        if (currentHealth > 0)
        {
            if (hasHurt) anim.SetTrigger("Hurt");
            else if (hasStunedTrigger) anim.SetTrigger("StunedTrigger");
            if (gameObject.activeInHierarchy) StartCoroutine(isPlayer ? PlayerHitIFrames() : HitFlash());
        }
        else Die();
    }

    IEnumerator HitFlash()
    {
        for (int i = 0; i < allRenderers.Length; i++)
            if (allRenderers[i] != null) allRenderers[i].color = new Color(1f, 0.3f, 0.3f, 1f);
        yield return new WaitForSeconds(0.12f);
        for (int i = 0; i < allRenderers.Length; i++)
            if (allRenderers[i] != null) allRenderers[i].color = baseColors[i];
    }

    void UpdateHealthBar()
    {
        float ratio = maxHealth > 0f ? Mathf.Clamp01(currentHealth / maxHealth) : 0f;
        if (healthBarFill != null)
        {
            if (healthBarFill.type == Image.Type.Filled) healthBarFill.fillAmount = ratio;
            else healthBarFill.rectTransform.localScale = new Vector3(initialUiBarScale.x * ratio, initialUiBarScale.y, initialUiBarScale.z);
        }
        if (barFillTransform != null)
        {
            float w = barFullWidth * ratio;
            barFillTransform.localScale = new Vector3(w, barFillTransform.localScale.y, 1f);
            barFillTransform.localPosition = new Vector3(-(barFullWidth - w) * 0.5f, 0f, 0f);
        }
    }

    void Die()
    {
        if (isPlayer)
        {
            if (isDead) return;
            isDead = true;
            if (GameplayTelemetry.Instance != null)
            {
                GameplayTelemetry.Instance.RecordDeath();
                GameplayTelemetry.Instance.RecordRoomAttempt();
            }
            StartCoroutine(PlayerDeathAndRespawn());
        }
        else
        {
            isDead = true;
            gameObject.tag = "Untagged";
            if (barRoot != null) barRoot.gameObject.SetActive(false);

            if (GameplayTelemetry.Instance != null)
            {
                var stats = GetComponent<EnemyStats>();
                GameplayTelemetry.Instance.RecordEnemyKilled(stats != null ? stats.enemyType : "Enemy");
            }

            var mov = GetComponent<EnemyMovement>();
            if (mov != null) mov.enabled = false;
            var atk = GetComponent<EnemyAttack>();
            if (atk != null) atk.enabled = false;
            foreach (var col in GetComponentsInChildren<Collider2D>()) col.enabled = false;

            if (hasSpeed) anim.SetFloat("Speed", 0f);
            if (hasDie) { anim.SetTrigger("Die"); Destroy(gameObject, 0.6f); }
            else if (hasDeathTrigger) { anim.SetTrigger("DeathTrigger"); Destroy(gameObject, 0.7f); }
            else Destroy(gameObject);
        }
    }

    private IEnumerator PlayerDeathAndRespawn()
    {
        var pc = GetComponent<PlayerController>();
        var pa = GetComponent<PlayerAttack>();
        var rb = GetComponent<Rigidbody2D>();
        if (pc != null) pc.enabled = false;
        if (pa != null) pa.enabled = false;
        if (rb != null)
        {
#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = Vector2.zero;
#else
            rb.velocity = Vector2.zero;
#endif
        }

        if (hasSpeed) anim.SetFloat("Speed", 0f);
        if (hasDie) anim.SetTrigger("Die");

        GameObject banner = ShowDeathBanner("YOU DIED — Restarting Room...");
        yield return new WaitForSeconds(1.25f);
        if (banner != null) Destroy(banner);
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    private void SetRenderersAlpha(float a)
    {
        for (int i = 0; i < allRenderers.Length; i++)
        {
            if (allRenderers[i] == null) continue;
            Color c = baseColors[i]; c.a = a;
            allRenderers[i].color = c;
        }
    }

    private GameObject ShowDeathBanner(string msg)
    {
        GameObject hud = GameObject.Find("HeroRPG_HUD");
        if (hud == null) return null;
        Font f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (f == null) f = Font.CreateDynamicFontFromOSFont("Arial", 28);

        GameObject bg = new GameObject("DeathBanner");
        bg.transform.SetParent(hud.transform, false);
        var rt = bg.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 0.42f); rt.anchorMax = new Vector2(1, 0.58f);
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        var img = bg.AddComponent<Image>(); img.color = new Color(0.08f, 0f, 0f, 0.78f);

        GameObject txtObj = new GameObject("Text");
        txtObj.transform.SetParent(bg.transform, false);
        var trt = txtObj.AddComponent<RectTransform>();
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
        var t = txtObj.AddComponent<Text>();
        t.font = f; t.fontSize = 32; t.fontStyle = FontStyle.Bold;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = new Color(1f, 0.25f, 0.25f, 1f);
        t.text = msg;
        return bg;
    }

    IEnumerator PlayerHitIFrames()
    {
        isInvulnerable = true;
        for (int i = 0; i < 3; i++)
        {
            for (int r = 0; r < allRenderers.Length; r++)
                if (allRenderers[r] != null) allRenderers[r].color = new Color(1f, 0.35f, 0.35f, 0.55f);
            yield return new WaitForSeconds(0.09f);
            for (int r = 0; r < allRenderers.Length; r++)
                if (allRenderers[r] != null) allRenderers[r].color = baseColors[r];
            yield return new WaitForSeconds(0.09f);
        }
        isInvulnerable = false;
    }
}
