using UnityEngine;

public class EnemyMovement : MonoBehaviour
{
    public float speed = 3f;
    public float stopDistance = 0.85f;
    public float separationRadius = 0.90f;
    public float separationStrength = 1.6f;

    private Transform player;
    private Rigidbody2D rb;
    private Animator anim;
    private bool hasSpeedParam, hasMoveTrigger;
    private bool wasMoving;
    private float baseScaleX = 1f;
    private Vector2 moveDir;
    private Vector2 knockbackVel;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponentInChildren<Animator>();
        if (anim != null && anim.runtimeAnimatorController != null)
        {
            foreach (var p in anim.parameters)
            {
                if (p.name == "Speed") hasSpeedParam = true;
                if (p.name == "MoveTrigger") hasMoveTrigger = true;
            }
        }
        baseScaleX = Mathf.Abs(transform.localScale.x);
        if (baseScaleX < 0.01f) baseScaleX = 1f;
        FindPlayerAndIgnoreBodyCollision();
    }

    void FindPlayerAndIgnoreBodyCollision()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj == null) return;
        player = playerObj.transform;

        var myCols = GetComponentsInChildren<Collider2D>();
        var playerCols = playerObj.GetComponentsInChildren<Collider2D>();
        foreach (var mc in myCols)
        {
            if (mc.isTrigger) continue;
            foreach (var pc in playerCols)
                if (!pc.isTrigger) Physics2D.IgnoreCollision(mc, pc, true);
        }
    }

    public void ApplyKnockback(Vector2 dir, float force) => knockbackVel = dir.normalized * force;

    void Update()
    {
        if (player == null)
        {
            FindPlayerAndIgnoreBodyCollision();
            SetMovingAnim(false);
            moveDir = Vector2.zero;
            return;
        }

        Vector2 diff = (Vector2)player.position - (Vector2)transform.position;
        float dist = diff.magnitude;

        if (dist > stopDistance)
        {
            moveDir = diff / dist;
            SetMovingAnim(true);
            if (Mathf.Abs(moveDir.x) > 0.05f)
            {
                Vector3 s = transform.localScale;
                transform.localScale = new Vector3(Mathf.Sign(moveDir.x) * baseScaleX, s.y, s.z);
            }
        }
        else
        {
            moveDir = Vector2.zero;
            SetMovingAnim(false);
        }

        Vector2 sep = Vector2.zero;
        Collider2D[] near = Physics2D.OverlapCircleAll(transform.position, separationRadius);
        foreach (var col in near)
        {
            if (col.gameObject == gameObject || !col.CompareTag("Enemy") || col.isTrigger) continue;
            Vector2 away = (Vector2)transform.position - (Vector2)col.transform.position;
            float d = away.magnitude;
            if (d > 0.001f && d < separationRadius)
                sep += (away / d) * ((separationRadius - d) / separationRadius);
        }

        Vector2 finalVel = moveDir * speed + sep * separationStrength + knockbackVel;
        knockbackVel = Vector2.Lerp(knockbackVel, Vector2.zero, 12f * Time.deltaTime);

        if (rb == null && finalVel.sqrMagnitude > 0.001f)
            transform.position += (Vector3)(finalVel * Time.deltaTime);
    }

    private void SetMovingAnim(bool moving)
    {
        if (hasSpeedParam) anim.SetFloat("Speed", moving ? 1f : 0f);
        if (hasMoveTrigger && moving && !wasMoving) anim.SetTrigger("MoveTrigger");
        wasMoving = moving;
    }

    void FixedUpdate()
    {
        if (rb == null) return;
        Vector2 finalVel = moveDir * speed + knockbackVel;
        if (finalVel.sqrMagnitude > 0.001f)
            rb.MovePosition(rb.position + finalVel * Time.fixedDeltaTime);
    }
}
