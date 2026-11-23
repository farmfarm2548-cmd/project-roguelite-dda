using UnityEngine;
using UnityEngine.SceneManagement;

public class WarpDoor : MonoBehaviour
{
    [Header("ชื่อซีนถัดไป (ถ้าเว้นว่างจะเดาตามลำดับ Room1 -> Room2 -> Room3 -> BossRoom -> EndScene)")]
    public string nextSceneName = "";

    [Header("โหมดโผล่กลางห้องเมื่อตีมอนหมด")]
    public bool spawnAtCenterWhenCleared = true;

    private EnemySpawner spawner;
    private Collider2D doorCollider;
    private SpriteRenderer originalSr;
    private Transform portalVisual;
    private Transform outerRing;
    private Transform innerRing;
    private Transform coreGlow;
    private bool isOpen;
    private float openAnimT;

    private static Sprite ringSprite;
    private static Sprite glowSprite;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoHookWarpDoor()
    {
        SceneManager.sceneLoaded -= OnSceneLoadedEnsureWarp;
        SceneManager.sceneLoaded += OnSceneLoadedEnsureWarp;
        OnSceneLoadedEnsureWarp(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private static void OnSceneLoadedEnsureWarp(Scene scene, LoadSceneMode mode)
    {
        string n = scene.name;
        if (n.StartsWith("Room", System.StringComparison.OrdinalIgnoreCase) ||
            n.Equals("BossRoom", System.StringComparison.OrdinalIgnoreCase))
        {
            EnsureWarpDoorInScene();
        }
    }

    public static void EnsureWarpDoorInScene()
    {
#pragma warning disable CS0618
        var existing = FindObjectOfType<WarpDoor>();
#pragma warning restore CS0618
        if (existing != null) return;

        // ถ้ามี ExitDoor ที่ใช้ RoomExit เดิม ให้ย้ายมาใช้ WarpDoor แทน
        string nextScene = "";
        GameObject exitObj = GameObject.Find("ExitDoor");
        if (exitObj != null)
        {
            var re = exitObj.GetComponent<RoomExit>();
            if (re != null)
            {
                nextScene = re.nextSceneName;
                re.enabled = false;
            }
            var wd = exitObj.AddComponent<WarpDoor>();
            wd.nextSceneName = nextScene;
            return;
        }

        GameObject go = new GameObject("ExitDoor");
        var col = go.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 1.1f;
        go.AddComponent<WarpDoor>();
    }

    void Start()
    {
        doorCollider = GetComponent<Collider2D>();
        if (doorCollider == null)
        {
            var cc = gameObject.AddComponent<CircleCollider2D>();
            cc.radius = 1.1f;
            doorCollider = cc;
        }
        doorCollider.isTrigger = true;

        originalSr = GetComponent<SpriteRenderer>();
        if (originalSr != null) originalSr.enabled = false;

#pragma warning disable CS0618
        spawner = FindObjectOfType<EnemySpawner>();
#pragma warning restore CS0618

        BuildAnimatedPortalVisual();

        if (spawnAtCenterWhenCleared && spawner != null)
        {
            isOpen = false;
            doorCollider.enabled = false;
            if (portalVisual != null) portalVisual.gameObject.SetActive(false);
        }
        else
        {
            OpenPortalNow();
        }
    }

    void Update()
    {
        if (!isOpen)
        {
            if (spawner == null || spawner.AllEnemiesCleared)
            {
                if (spawnAtCenterWhenCleared)
                {
                    Vector3 center = spawner != null ? spawner.transform.position : Vector3.zero;
                    transform.position = new Vector3(center.x, center.y, 0f);
                }
                OpenPortalNow();
            }
            return;
        }

        openAnimT = Mathf.Min(1f, openAnimT + Time.deltaTime * 2.4f);
        float pop = 1f - Mathf.Pow(1f - openAnimT, 3f);
        if (portalVisual != null) portalVisual.localScale = Vector3.one * pop;
        if (outerRing != null) outerRing.Rotate(0f, 0f, 95f * Time.deltaTime);
        if (innerRing != null) innerRing.Rotate(0f, 0f, -155f * Time.deltaTime);
        if (coreGlow != null)
        {
            float pulse = 0.88f + 0.18f * Mathf.Sin(Time.time * 5f);
            coreGlow.localScale = Vector3.one * (2.0f * pulse);
        }
    }

    private void OpenPortalNow()
    {
        isOpen = true;
        openAnimT = 0f;
        transform.localScale = Vector3.one;
        if (doorCollider != null) doorCollider.enabled = true;
        if (portalVisual != null)
        {
            portalVisual.gameObject.SetActive(true); GameAudioManager.PlayPortal();
            portalVisual.localScale = Vector3.zero;
        }
        Debug.Log("[WarpDoor] เคลียร์มอนสเตอร์ครบแล้ว! ประตูวาร์ปเปิดขึ้นกลางห้องแล้ว");
    }

    private void BuildAnimatedPortalVisual()
    {
        EnsurePortalSprites();

        GameObject root = new GameObject("AnimatedPortal");
        root.transform.SetParent(transform, false);
        portalVisual = root.transform;

        GameObject glow = new GameObject("CoreGlow");
        glow.transform.SetParent(portalVisual, false);
        glow.transform.localScale = Vector3.one * 2.0f;
        var gsr = glow.AddComponent<SpriteRenderer>();
        gsr.sprite = glowSprite;
        gsr.color = new Color(0.55f, 0.25f, 1f, 0.78f);
        gsr.sortingOrder = 4900;
        coreGlow = glow.transform;

        GameObject outer = new GameObject("OuterRing");
        outer.transform.SetParent(portalVisual, false);
        outer.transform.localScale = Vector3.one * 1.75f;
        var osr = outer.AddComponent<SpriteRenderer>();
        osr.sprite = ringSprite;
        osr.color = new Color(0.68f, 0.35f, 1f, 0.98f);
        osr.sortingOrder = 4905;
        outerRing = outer.transform;

        GameObject inner = new GameObject("InnerRing");
        inner.transform.SetParent(portalVisual, false);
        inner.transform.localScale = Vector3.one * 1.15f;
        var isr = inner.AddComponent<SpriteRenderer>();
        isr.sprite = ringSprite;
        isr.color = new Color(0.30f, 0.92f, 1f, 0.98f);
        isr.sortingOrder = 4910;
        innerRing = inner.transform;
    }

    private static void EnsurePortalSprites()
    {
        if (ringSprite == null)
        {
            const int s = 64;
            var t = new Texture2D(s, s, TextureFormat.RGBA32, false);
            float h = s * 0.5f;
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float dx = (x - h) / h;
                    float dy = (y - h) / h;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float ang = Mathf.Atan2(dy, dx);
                    float dash = 0.55f + 0.45f * Mathf.Sin(ang * 6f);
                    float a = (d > 0.65f && d < 0.96f) ? Mathf.Sin(((d - 0.65f) / 0.31f) * Mathf.PI) * dash : 0f;
                    t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            t.Apply();
            ringSprite = Sprite.Create(t, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 32f);
        }
        if (glowSprite == null)
        {
            const int s = 64;
            var t = new Texture2D(s, s, TextureFormat.RGBA32, false);
            float h = s * 0.5f;
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float d = Mathf.Clamp01(Vector2.Distance(new Vector2(x, y), new Vector2(h, h)) / h);
                    float a = Mathf.Pow(1f - d, 1.8f);
                    t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            t.Apply();
            glowSprite = Sprite.Create(t, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 32f);
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!isOpen || !collision.CompareTag("Player")) return;

        string target = nextSceneName;
        if (string.IsNullOrEmpty(target))
        {
            string cur = SceneManager.GetActiveScene().name.ToLowerInvariant();
            if (cur.Contains("room1")) target = "Room2";
            else if (cur.Contains("room2")) target = "Room3";
            else if (cur.Contains("room3")) target = "BossRoom";
            else target = "EndScene";
        }

        if (DDAManager.Instance != null)
        {
            if (DDAManager.Instance.currentIsBoss)
                DDAManager.Instance.EndSession(true);
            else
                DDAManager.Instance.EndRoom(true);
        }

        SceneManager.LoadScene(target);
    }
}
