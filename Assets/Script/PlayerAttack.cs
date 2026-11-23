using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerAttack : MonoBehaviour
{
    [Header("สเตตัสการฟัน (คงที่จาก PlayerConfig — §7.2)")]
    public float attackDamage = PlayerConfig.AttackDamage;
    public float attackCooldown = PlayerConfig.AttackCooldown;
    private float nextAttackTime = 0f;

    [Header("ตั้งค่าระยะ")]
    public float attackRange = 0.95f;
    public float attackReach = 0.75f;
    public float knockbackForce = 3.5f;

    public Transform slashEffect;

    private Animator anim;
    private Camera cam;
    private Vector2 aimDir = Vector2.right;

    void Start()
    {
        anim = GetComponent<Animator>();
        cam = Camera.main;
        attackDamage = PlayerConfig.AttackDamage;
        attackCooldown = PlayerConfig.AttackCooldown;
    }

    void OnDisable() => CancelInvoke(nameof(DoDamage));

    void OnAttack(InputValue value)
    {
        if (Time.time < nextAttackTime) return;
        aimDir = GetAimDirection();
        FaceAim(aimDir);
        PlaceSlash(aimDir);

        if (anim != null) anim.SetTrigger("Attack");
        CancelInvoke(nameof(DoDamage));
        Invoke(nameof(DoDamage), 0.15f);
        nextAttackTime = Time.time + attackCooldown;
    }

    Vector2 GetAimDirection()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null || Mouse.current == null) return aimDir;
        Vector2 mouseScreen = Mouse.current.position.ReadValue();
        float zDist = Mathf.Abs(cam.transform.position.z - transform.position.z);
        Vector3 mouseWorld = cam.ScreenToWorldPoint(new Vector3(mouseScreen.x, mouseScreen.y, zDist));
        Vector2 dir = (Vector2)mouseWorld - (Vector2)transform.position;
        return dir.sqrMagnitude > 0.0001f ? dir.normalized : aimDir;
    }

    void FaceAim(Vector2 dir)
    {
        if (Mathf.Abs(dir.x) < 0.01f) return;
        Vector3 s = transform.localScale;
        transform.localScale = new Vector3(Mathf.Sign(dir.x) * Mathf.Abs(s.x), s.y, s.z);
    }

    void PlaceSlash(Vector2 dir)
    {
        if (slashEffect == null) return;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        slashEffect.position = (Vector2)transform.position + dir * attackReach;
        slashEffect.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    void DoDamage()
    {
        if (GameplayTelemetry.Instance != null)
            GameplayTelemetry.Instance.RecordAttackAttempt();

        Vector2 attackPos = (Vector2)transform.position + aimDir * attackReach;
        Collider2D[] hitObjects = Physics2D.OverlapCircleAll(attackPos, attackRange);

        var damaged = new HashSet<Health>();
        bool hitSomething = false;
        foreach (Collider2D obj in hitObjects)
        {
            Health eh = obj.GetComponentInParent<Health>();
            if (eh == null || eh.CompareTag("Player") || damaged.Contains(eh)) continue;
            if (!eh.CompareTag("Enemy")) continue;

            damaged.Add(eh);
            eh.TakeDamage(attackDamage);
            hitSomething = true;

            var mov = eh.GetComponent<EnemyMovement>();
            if (mov != null)
            {
                Vector2 push = ((Vector2)eh.transform.position - (Vector2)transform.position).normalized;
                mov.ApplyKnockback(push, knockbackForce);
            }
        }

        if (hitSomething && GameplayTelemetry.Instance != null)
            GameplayTelemetry.Instance.RecordSuccessfulHit();
    }
}

