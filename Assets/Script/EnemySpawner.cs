using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[System.Serializable]
public class WaveInfo
{
    public string waveName;
    public GameObject enemyPrefab;
    public float spawnRate = 2f;
    public int spawnCount = 3;
    public int roundsToSpawn = 5;
}

public class EnemySpawner : MonoBehaviour
{
    [Header("โหมดดึงมอนสเตอร์หลากหลายอัตโนมัติตามธีมห้อง")]
    public bool useRoomThemeMonsters = true;

    [Header("Prefab สำรอง")]
    public GameObject enemyPrefab;
    public GameObject[] themedPrefabs;

    [Header("จำนวน")]
    public int totalEnemies = 12;
    public int maxConcurrent = 5;

    [Header("ระยะการเกิด")]
    public float spawnRadius = 5f;

    [Header("(ของเดิม) เก็บไว้เพื่อความเข้ากันได้กับซีนเก่า")]
    public WaveInfo[] waves;

    public bool AllEnemiesCleared => !canSpawn || (spawnedTotal >= totalEnemies && CountAlive() == 0);
    public int RemainingCount => Mathf.Max(0, (totalEnemies - spawnedTotal) + CountAlive());

    private readonly List<GameObject> activePool = new List<GameObject>();
    private GameObject bossPrefab;
    private bool bossSpawned;
    private float spawnInterval = 2f;
    private float timer;
    private int spawnedTotal;
    private bool canSpawn = true;

    void Start()
    {
        LoadThemePool();

        if (activePool.Count == 0 && bossPrefab == null)
        {
            if (enemyPrefab == null && waves != null)
            {
                foreach (var w in waves)
                    if (w != null && w.enemyPrefab != null) { enemyPrefab = w.enemyPrefab; break; }
            }
            if (enemyPrefab != null) activePool.Add(enemyPrefab);
        }

        if (activePool.Count == 0 && bossPrefab == null)
        {
            canSpawn = false;
            Debug.LogError("[Spawner] ไม่พบ Prefab มอนสเตอร์ กรุณารันข้อ 6 และข้อ 12 ก่อน");
            return;
        }

        float rate = DDAManager.Instance != null
            ? DDAManager.Instance.EnemySpawnRate
            : ChallengeFramework.EnemySpawnRateFor(ChallengeFramework.Room1Challenge);

        spawnInterval = rate > 0.0001f ? 1f / rate : 999f;
        timer = spawnInterval;
    }

    void LoadThemePool()
    {
        activePool.Clear();
        bossPrefab = null;

        if (themedPrefabs != null && themedPrefabs.Length > 0)
        {
            foreach (var p in themedPrefabs)
                if (p != null) activePool.Add(p);
            if (activePool.Count > 0) return;
        }

        if (!useRoomThemeMonsters) return;

        string scene = SceneManager.GetActiveScene().name.ToLowerInvariant();
        string[] names;

        if (scene.Contains("boss"))
        {
            bossPrefab = Resources.Load<GameObject>("Monsters/Orc3_Boss");
            names = new[] { "Monsters/Demon_Enemy", "Monsters/MagmaGolem_Enemy", "Monsters/Orc3_Enemy", "Monsters/Slime3_Enemy" };
        }
        else if (scene.Contains("room3"))
        {
            names = new[] { "Monsters/CrystalGolem_Enemy", "Monsters/GhostKnight_Enemy", "Monsters/Orc3_Enemy", "Monsters/Slime3_Enemy" };
        }
        else if (scene.Contains("room2"))
        {
            names = new[] { "Monsters/SkeletonWarrior_Enemy", "Monsters/Ghost_Enemy", "Monsters/Orc2_Enemy", "Monsters/Slime2_Enemy" };
        }
        else
        {
            names = new[] { "Monsters/Bat_Enemy", "Monsters/Slime1_Enemy", "Monsters/Orc1_Enemy" };
        }

        foreach (string resPath in names)
        {
            var loaded = Resources.Load<GameObject>(resPath);
            if (loaded != null) activePool.Add(loaded);
        }
    }

    void Update()
    {
        if (!canSpawn || spawnedTotal >= totalEnemies) return;

        timer += Time.deltaTime;
        if (timer < spawnInterval) return;

        if (maxConcurrent > 0 && CountAlive() >= maxConcurrent) return;

        SpawnOne();
        timer = 0f;
    }

    void SpawnOne()
    {
        GameObject prefabToSpawn;
        Vector2 pos;

        if (bossPrefab != null && !bossSpawned)
        {
            bossSpawned = true;
            prefabToSpawn = bossPrefab;
            pos = (Vector2)transform.position + new Vector2(0f, 1.5f);
        }
        else
        {
            prefabToSpawn = activePool.Count > 0
                ? activePool[Random.Range(0, activePool.Count)]
                : bossPrefab;
            pos = (Vector2)transform.position + Random.insideUnitCircle * spawnRadius;
        }

        if (prefabToSpawn == null) return;

        GameObject e = Instantiate(prefabToSpawn, pos, Quaternion.identity);
        if (e.GetComponent<EnemyStats>() == null)
            EnemyStats.Apply(e, e.name);

        spawnedTotal++;
    }

    int CountAlive()
    {
        return GameObject.FindGameObjectsWithTag("Enemy").Length;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, spawnRadius);
    }
}

