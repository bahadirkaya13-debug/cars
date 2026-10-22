using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class PlayerController : MonoBehaviour
{
    [Header("Hareket")]
    [SerializeField] float moveSpeed = 8f;
    [SerializeField] float acceleration = 60f;
    [SerializeField] float deceleration = 70f;
    [SerializeField] float airControl = 0.65f;

    [Header("Zıplama")]
    [SerializeField] float jumpForce = 14f;
    [SerializeField] float coyoteTime = 0.12f;
    [SerializeField] float jumpBufferTime = 0.12f;
    [SerializeField] float fallGravityMultiplier = 2.2f;
    [SerializeField] float lowJumpGravityMultiplier = 2.6f;

    [Header("Yere Temas")]
    [SerializeField] Transform groundCheck;
    [SerializeField] Vector2 groundCheckSize = new Vector2(0.45f, 0.08f);
    [SerializeField] LayerMask groundLayer = ~0;
    [SerializeField] float groundCheckDistance = 0.08f;

    Rigidbody2D rb;
    float moveInput;
    float coyoteCounter;
    float jumpBufferCounter;
    bool isGrounded;
    bool jumpHeld;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.gravityScale = 3f;

        if (groundCheck == null)
        {
            var check = new GameObject("GroundCheck");
            check.transform.SetParent(transform);
            check.transform.localPosition = new Vector3(0f, -0.5f, 0f);
            groundCheck = check.transform;
        }
    }

    void Update()
    {
        moveInput = Input.GetAxisRaw("Horizontal");
        jumpHeld = Input.GetKey(KeyCode.Space);

        if (Input.GetKeyDown(KeyCode.Space))
            jumpBufferCounter = jumpBufferTime;

        if (isGrounded)
            coyoteCounter = coyoteTime;
        else
            coyoteCounter -= Time.deltaTime;

        jumpBufferCounter -= Time.deltaTime;

        if (jumpBufferCounter > 0f && coyoteCounter > 0f)
        {
            Jump();
            jumpBufferCounter = 0f;
            coyoteCounter = 0f;
        }

        FlipSprite();
    }

    void FixedUpdate()
    {
        CheckGrounded();
        ApplyHorizontalMovement();
        ApplyJumpGravity();
    }

    void CheckGrounded()
    {
        Vector2 origin = groundCheck.position;
        isGrounded = Physics2D.OverlapBox(origin, groundCheckSize, 0f, groundLayer)
                     || Physics2D.BoxCast(origin, groundCheckSize, 0f, Vector2.down, groundCheckDistance, groundLayer);
    }

    void ApplyHorizontalMovement()
    {
        float targetSpeed = moveInput * moveSpeed;
        float speedDiff = targetSpeed - rb.linearVelocity.x;
        float accel = Mathf.Abs(targetSpeed) > 0.01f ? acceleration : deceleration;
        if (!isGrounded)
            accel *= airControl;

        float movement = Mathf.Clamp(speedDiff, -accel, accel) * Time.fixedDeltaTime;
        rb.linearVelocity = new Vector2(rb.linearVelocity.x + movement, rb.linearVelocity.y);
    }

    void ApplyJumpGravity()
    {
        if (rb.linearVelocity.y < 0f)
            rb.gravityScale = 3f * fallGravityMultiplier;
        else if (rb.linearVelocity.y > 0f && !jumpHeld)
            rb.gravityScale = 3f * lowJumpGravityMultiplier;
        else
            rb.gravityScale = 3f;
    }

    void Jump()
    {
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
        rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
        isGrounded = false;
    }

    void FlipSprite()
    {
        if (moveInput > 0.01f)
            transform.localScale = new Vector3(Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);
        else if (moveInput < -0.01f)
            transform.localScale = new Vector3(-Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);
    }

    void OnDrawGizmosSelected()
    {
        if (groundCheck == null) return;
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(groundCheck.position, groundCheckSize);
    }
}
