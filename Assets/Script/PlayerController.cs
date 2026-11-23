using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [Header("ตั้งค่าความเร็ว (คงที่จาก PlayerConfig — §7.2)")]
    public float moveSpeed = PlayerConfig.MoveSpeed;

    private Rigidbody2D rb;
    private Animator anim;
    private Vector2 movement;
    private float baseScaleX = 1f;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        moveSpeed = PlayerConfig.MoveSpeed; // §7.2 fixed for all participants
        baseScaleX = Mathf.Abs(transform.localScale.x);
        if (baseScaleX < 0.01f) baseScaleX = 1f;

        // บังคับฟิสิกส์ให้ชนกำแพงได้จริง ไม่เดินทะลุ TilemapCollider2D
        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }
        var col = GetComponent<Collider2D>();
        if (col != null)
        {
            col.isTrigger = false;
            if (col is BoxCollider2D box)
            {
                box.size = new Vector2(0.70f, 0.85f);
                box.offset = new Vector2(0f, -0.05f);
            }
        }
    }

    void OnMove(InputValue value)
    {
        movement = Vector2.ClampMagnitude(value.Get<Vector2>(), 1f);
    }

    void Update()
    {
        if (anim != null)
            anim.SetFloat("Speed", movement.sqrMagnitude);

        if (movement.x > 0.01f)
        {
            Vector3 s = transform.localScale;
            transform.localScale = new Vector3(baseScaleX, s.y, s.z);
        }
        else if (movement.x < -0.01f)
        {
            Vector3 s = transform.localScale;
            transform.localScale = new Vector3(-baseScaleX, s.y, s.z);
        }
    }

    void FixedUpdate()
    {
        if (rb != null)
        {
#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = movement * moveSpeed;
#else
            rb.velocity = movement * moveSpeed;
#endif
        }
    }
}

