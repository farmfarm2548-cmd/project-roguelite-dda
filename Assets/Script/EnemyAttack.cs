using UnityEngine;

public class EnemyAttack : MonoBehaviour
{
    public float attackDamage = 10f;
    public float attackSpeed = 1.6f;
    private float nextAttackTime = 0f;
    private Animator anim;
    private bool hasAttack, hasAttackTrigger;

    void Awake()
    {
        anim = GetComponentInChildren<Animator>();
        if (anim != null && anim.runtimeAnimatorController != null)
        {
            foreach (var p in anim.parameters)
            {
                if (p.name == "Attack") hasAttack = true;
                if (p.name == "AttackTrigger") hasAttackTrigger = true;
            }
        }
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        if (!collision.CompareTag("Player")) return;
        if (Time.time < nextAttackTime) return;

        Health playerHealth = collision.GetComponentInParent<Health>();
        if (playerHealth != null)
        {
            if (hasAttack) anim.SetTrigger("Attack");
            else if (hasAttackTrigger) anim.SetTrigger("AttackTrigger");
            playerHealth.TakeDamage(attackDamage);
            nextAttackTime = Time.time + attackSpeed;
        }
    }
}

