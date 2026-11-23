using UnityEngine;

//  RoomGenerator  (ทางเลือกของ EnemySpawner — เกิดศัตรูตามจุดที่กำหนดไว้ล่วงหน้า)
//  ศัตรูทุกตัวรับ HP/ATK/Speed จาก Challenge ของห้องผ่าน EnemyStats (§8.6)
//  ใช้เมื่อออกแบบตำแหน่งศัตรูแบบ fixed layout แทนการเกิดตามอัตราต่อวินาที
public class RoomGenerator : MonoBehaviour
{
    [Header("ตั้งค่าการเสกมอนสเตอร์")]
    public GameObject enemyPrefab;   // Prefab ต้องมี EnemyStats
    public Transform[] spawnPoints;

    [Tooltip("จำนวนศัตรูที่จะเกิด (สุ่มจากจุดที่กำหนด)")]
    public int enemyCount = 6;

    private void Start()
    {
        if (enemyPrefab == null || spawnPoints == null || spawnPoints.Length == 0)
        {
            return;
        }

        for (int i = 0; i < enemyCount; i++)
        {
            Transform p = spawnPoints[Random.Range(0, spawnPoints.Length)];
            GameObject e = Instantiate(enemyPrefab, p.position, Quaternion.identity);
            if (e.GetComponent<EnemyStats>() == null)
                EnemyStats.Apply(e, e.name); // ใส่ HP/ATK/Speed จาก Challenge ให้เอง
        }
    }
}
