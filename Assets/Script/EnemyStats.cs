using UnityEngine;

//  EnemyStats  (Proposal §7.3, §8.6, §10)
//  ตั้งค่าศัตรูจาก "พารามิเตอร์ที่ map มาจาก Challenge Score ของห้อง" ผ่าน DDAManager
//    Enemy Speed        → EnemyMovement.speed
//    Enemy Attack Power → EnemyAttack.attackDamage   (damage/hit)
//    Enemy HP           → Health.maxHealth
//  และบันทึกการเกิดของศัตรู (EnemySpawn) ลง telemetry
//
//  * ถ้า prefab ศัตรู "ไม่มี" component นี้ ตัว Spawner จะเรียก EnemyStats.Apply()
//    ให้เองหลัง Instantiate — จึงไม่จำเป็นต้องแปะ EnemyStats บน prefab
public class EnemyStats : MonoBehaviour
{
    [Header("Fallback base stats (ใช้เมื่อทดสอบซีนเดี่ยวโดยไม่มี DDAManager)")]
    public float baseHp = 125f;
    public float baseDamage = 25f;
    public float baseSpeed = 2f;

    [Header("ชนิดศัตรู (สำหรับ telemetry event)")]
    public string enemyType = "Enemy";

    private void Awake()
    {
        Apply(gameObject, enemyType, baseHp, baseDamage, baseSpeed);
    }

    public static void Apply(GameObject e, string enemyType = "Enemy",
                             float baseHp = 125f, float baseDamage = 25f, float baseSpeed = 2f)
    {
        float hp = baseHp, dmg = baseDamage, speed = baseSpeed;

        if (DDAManager.Instance != null)
        {
            hp    = DDAManager.Instance.EnemyHp;
            dmg   = DDAManager.Instance.EnemyAttackPower;
            speed = DDAManager.Instance.EnemySpeed;
        }

        Health h = e.GetComponent<Health>();
        if (h != null) { h.maxHealth = hp; h.currentHealth = hp; }

        EnemyAttack att = e.GetComponent<EnemyAttack>();
        if (att != null) att.attackDamage = dmg;

        EnemyMovement mov = e.GetComponent<EnemyMovement>();
        if (mov != null) mov.speed = speed;

        if (GameplayTelemetry.Instance != null)
            GameplayTelemetry.Instance.RecordEnemySpawn(enemyType);
    }
}
