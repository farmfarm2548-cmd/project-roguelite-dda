using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MonsterSanitizerAndVisualGuard : MonoBehaviour
{
    private static MonsterSanitizerAndVisualGuard _instance;
    private float _scanTimer = 0f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (_instance != null) return;
        GameObject go = new GameObject("MonsterSanitizerAndVisualGuard");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<MonsterSanitizerAndVisualGuard>();
    }

    private void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
        ApplyToScene(SceneManager.GetActiveScene().name);
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyToScene(scene.name);
    }

    private void ApplyToScene(string sceneName)
    {
        if (!sceneName.StartsWith("Room", StringComparison.OrdinalIgnoreCase) &&
            !sceneName.Equals("BossRoom", StringComparison.OrdinalIgnoreCase))
            return;

        SanitizeSpawners(sceneName);
        AttachGuardsToExistingEnemies();
    }

    private void Update()
    {
        _scanTimer += Time.deltaTime;
        if (_scanTimer >= 0.35f)
        {
            _scanTimer = 0f;
            AttachGuardsToExistingEnemies();
        }
    }

    private void ApplyFiveMinuteWaveCount(EnemySpawner sp, string sceneName)
    {
        string lower = sceneName.ToLowerInvariant();
        int targetCount = 16;
        if (lower.Contains("boss")) targetCount = 12;
        else if (lower.Contains("3")) targetCount = 22;
        else if (lower.Contains("2")) targetCount = 20;

        Type t = sp.GetType();
        BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        foreach (FieldInfo f in t.GetFields(flags))
        {
            if (f.FieldType != typeof(int)) continue;
            string fn = f.Name.ToLowerInvariant();
            if (fn.Contains("total") || fn.Contains("count") || fn.Contains("maxenemies") || fn.Contains("spawnlimit") || fn.Contains("wave"))
            {
                if (!fn.Contains("concurrent") && !fn.Contains("alive") && !fn.Contains("spawned") && !fn.Contains("killed"))
                {
                    f.SetValue(sp, targetCount);
                }
            }
            else if (fn.Contains("concurrent"))
            {
                f.SetValue(sp, 4);
            }
        }
    }

    private void SanitizeSpawners(string sceneName)
    {
        List<GameObject> pool = LoadPixelPoolForScene(sceneName);
        if (pool.Count == 0) return;

        EnemySpawner[] spawners = FindObjectsCompat<EnemySpawner>();
        foreach (EnemySpawner sp in spawners)
        {
            if (sp == null) continue;
            ApplyFiveMinuteWaveCount(sp, sceneName);
            Type t = sp.GetType();
            BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

            foreach (FieldInfo f in t.GetFields(flags))
            {
                if (f.FieldType == typeof(GameObject[]))
                {
                    string fn = f.Name.ToLowerInvariant();
                    if (fn.Contains("enemy") || fn.Contains("monster") || fn.Contains("pool") || fn.Contains("prefab"))
                    {
                        f.SetValue(sp, pool.ToArray());
                    }
                }
                else if (f.FieldType == typeof(List<GameObject>))
                {
                    string fn = f.Name.ToLowerInvariant();
                    if (fn.Contains("enemy") || fn.Contains("monster") || fn.Contains("pool") || fn.Contains("prefab"))
                    {
                        f.SetValue(sp, new List<GameObject>(pool));
                    }
                }
                else if (f.FieldType == typeof(GameObject))
                {
                    string fn = f.Name.ToLowerInvariant();
                    if (fn.Contains("boss"))
                    {
                        GameObject boss = Resources.Load<GameObject>("Monsters/Orc3_Boss");
                        if (boss != null) f.SetValue(sp, boss);
                    }
                    else if (fn.Contains("enemy") || fn.Contains("monster") || fn.Contains("prefab"))
                    {
                        GameObject cur = f.GetValue(sp) as GameObject;
                        if (cur == null || (!cur.name.StartsWith("Slime") && !cur.name.StartsWith("Orc")))
                        {
                            f.SetValue(sp, pool[0]);
                        }
                    }
                }
            }
        }
    }

    private List<GameObject> LoadPixelPoolForScene(string sceneName)
    {
        string lower = sceneName.ToLowerInvariant();
        string[] names;
        if (lower.Contains("boss"))
            names = new string[] { "Orc3_Boss", "Orc3", "Slime3" };
        else if (lower.Contains("3"))
            names = new string[] { "Slime3", "Orc3" };
        else if (lower.Contains("2"))
            names = new string[] { "Slime2", "Orc2" };
        else
            names = new string[] { "Slime1", "Orc1" };

        List<GameObject> list = new List<GameObject>();
        foreach (string n in names)
        {
            GameObject p = Resources.Load<GameObject>("Monsters/" + n);
            if (p != null) list.Add(p);
        }

        if (list.Count == 0)
        {
            GameObject[] all = Resources.LoadAll<GameObject>("Monsters");
            foreach (GameObject g in all)
            {
                if (g != null && (g.name.StartsWith("Slime") || g.name.StartsWith("Orc")))
                    list.Add(g);
            }
        }
        return list;
    }

    private void AttachGuardsToExistingEnemies()
    {
        EnemyMovement[] enemies = FindObjectsCompat<EnemyMovement>();
        foreach (EnemyMovement em in enemies)
        {
            if (em == null) continue;
            if (em.GetComponent<EnemySpriteGuard>() == null)
            {
                em.gameObject.AddComponent<EnemySpriteGuard>();
            }
        }
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

public class EnemySpriteGuard : MonoBehaviour
{
    private SpriteRenderer[] _renderers;
    private Sprite[] _lastValidSprites;
    private Component _healthComp;

    private void Awake()
    {
        CacheRenderers();
        _healthComp = GetComponent("Health");
    }

    private void CacheRenderers()
    {
        SpriteRenderer[] all = GetComponentsInChildren<SpriteRenderer>(true);
        List<SpriteRenderer> bodyList = new List<SpriteRenderer>();
        foreach (SpriteRenderer sr in all)
        {
            if (sr == null) continue;
            string n = sr.gameObject.name.ToLowerInvariant();
            if (n.Contains("hp") || n.Contains("health") || n.Contains("bar") || n.Contains("shadow")) continue;
            bodyList.Add(sr);
        }
        _renderers = bodyList.ToArray();
        _lastValidSprites = new Sprite[_renderers.Length];
        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] != null) _lastValidSprites[i] = _renderers[i].sprite;
        }
    }

    private void LateUpdate()
    {
        if (_renderers == null || _renderers.Length == 0) return;
        if (!IsAlive()) return;

        int targetOrder = 5000 - Mathf.RoundToInt(transform.position.y * 100f);

        for (int i = 0; i < _renderers.Length; i++)
        {
            SpriteRenderer sr = _renderers[i];
            if (sr == null) continue;

            if (sr.sortingOrder < 1000)
            {
                sr.sortingOrder = targetOrder;
            }

            if (sr.sprite != null)
            {
                _lastValidSprites[i] = sr.sprite;
            }
            else if (_lastValidSprites[i] != null)
            {
                sr.sprite = _lastValidSprites[i];
            }

            if (!sr.enabled) sr.enabled = true;
            Color c = sr.color;
            if (c.a < 0.85f)
            {
                c.a = 1f;
                sr.color = c;
            }
        }
    }

    private bool IsAlive()
    {
        if (_healthComp == null) return true;
        Type t = _healthComp.GetType();
        FieldInfo fDead = t.GetField("isDead", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (fDead != null && fDead.FieldType == typeof(bool) && (bool)fDead.GetValue(_healthComp)) return false;

        FieldInfo fHp = t.GetField("currentHealth", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (fHp == null) fHp = t.GetField("currentHP", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (fHp != null && fHp.FieldType == typeof(float) && (float)fHp.GetValue(_healthComp) <= 0f) return false;
        return true;
    }
}
