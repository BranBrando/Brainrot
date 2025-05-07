using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(PlayerInput))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float jumpForce = 10f;
    [SerializeField] private float fallGravityMultiplier = 2.5f; // Multiplies gravity when falling
    [SerializeField] private float lowJumpGravityMultiplier = 2f;  // Multiplies gravity if jump is released early while rising

    [Header("Dash Settings")]
    [SerializeField] private float dashSpeed = 20f;
    [SerializeField] private float dashDuration = 0.2f;
    [SerializeField] private float dashCooldown = 1f;

    [Header("Ground Check")]
    [SerializeField] private Transform groundCheckPoint;
    [SerializeField] private Vector2 groundCheckSize = new Vector2(0.4f, 0.1f);
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float groundCheckYOffset = -0.1f;

    private Rigidbody2D rb;
    private PlayerInput playerInput;
    private InputAction moveAction;
    private InputAction jumpAction;
    private InputAction dashAction;
    private Animator anim;
    private Collider2D playerCollider;

    private bool isGrounded;
    private bool canDoubleJump;
    private float moveDirection;
    private bool isDashing;
    private float currentDashTime;
    private float dashCooldownTimer;
    private Vector2 dashDirection;
    private float originalGravityScale;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        playerInput = GetComponent<PlayerInput>();

        // Ensure the Action Map name matches the one in your Input Actions asset
        moveAction = playerInput.actions["Player/Move"];
        jumpAction = playerInput.actions["Player/Jump"];
        dashAction = playerInput.actions["Player/Dash"];
        originalGravityScale = rb.gravityScale;

        anim = GetComponentInChildren<Animator>(); // Or GetComponent<Animator>() if it's on the root
        if (anim == null)
        {
            Debug.LogWarning("PlayerMovement: Animator component not found on Player or its children.");
        }

        playerCollider = GetComponent<Collider2D>();
        if (playerCollider == null)
        {
            Debug.LogError("PlayerMovement: No Collider2D found on this GameObject. GroundCheckPoint positioning might be inaccurate.");
        }

        if (groundCheckPoint != null)
        {
            float targetLocalY;
            if (playerCollider != null)
            {
                // Calculate the world Y position of the collider's bottom edge.
                // bounds.center is the center of the AABB in world space.
                // bounds.extents is half the size of the AABB.
                float worldColliderBottomY = playerCollider.bounds.center.y - playerCollider.bounds.extents.y;

                // To set groundCheckPoint.localPosition.y, we need to convert this world Y
                // into a local Y relative to the player's transform.
                // We'll construct a point in world space at the collider's bottom,
                // using the groundCheckPoint's current world X and Z to maintain its horizontal alignment.
                Vector3 worldPointAtColliderBottom = new Vector3(
                    groundCheckPoint.position.x, // Use groundCheckPoint's current world X
                    worldColliderBottomY,
                    groundCheckPoint.position.z  // Use groundCheckPoint's current world Z
                );

                // Convert this world point to a local position relative to this player's transform.
                // Then, take the Y component and add the offset.
                targetLocalY = transform.InverseTransformPoint(worldPointAtColliderBottom).y + groundCheckYOffset;
            }
            else
            {
                // Fallback: If no collider, use the offset from the transform's pivot (original behavior)
                targetLocalY = groundCheckYOffset;
                Debug.LogWarning("PlayerMovement: Collider2D not found. GroundCheckPoint offset from transform pivot.");
            }

            groundCheckPoint.localPosition = new Vector3(
                groundCheckPoint.localPosition.x, // Keep existing local X
                targetLocalY,
                groundCheckPoint.localPosition.z  // Keep existing local Z
            );
        }
        else
        {
            Debug.LogError("PlayerMovement: GroundCheckPoint is not assigned in the Inspector!");
        }
    }

    void OnEnable()
    {
        jumpAction.performed += OnJump;
        dashAction.performed += TriggerDash;
    }


    void OnDisable()
    {
        jumpAction.performed -= OnJump;
        dashAction.performed -= TriggerDash;
    }

    void Update()
    {
        // Read horizontal movement input
        if (!isDashing) // Only read moveDirection if not dashing for movement
        {
            moveDirection = moveAction.ReadValue<Vector2>().x;
        }
        else // if dashing, ensure moveDirection reflects dash for animation if needed
        {
             // If you want walking animation during dash based on dashDirection:
             // moveDirection = dashDirection.x; 
             // Or keep it 0 if dash has its own animation triggered separately.
             // For now, let's assume dash doesn't use the 'speed' parameter for walking.
        }

        if (dashCooldownTimer > 0)
        {
            dashCooldownTimer -= Time.deltaTime;
        }

        // Animation control for speed
        if (anim != null)
        {
            if (isDashing)
            {
                // If dashing, you might want speed to be 0 (if dash has its own anim)
                // or based on dash speed if it's a super-run.
                // For a burst dash, usually set to 0 or trigger a specific dash animation.
                anim.SetFloat("speed", 0f); 
            }
            else
            {
                // Use the magnitude of the Rigidbody's horizontal velocity for the speed parameter
                anim.SetFloat("speed", Mathf.Abs(rb.linearVelocity.x));
            }
        }
    }

    void FixedUpdate()
    {
        PerformGroundCheck(); // Always perform ground check

        if (isDashing)
        {
            HandleDashPhysics();
        }
        else // Not dashing: Apply custom gravity logic and then normal movement
        {
            if (rb.linearVelocity.y < 0 && !isGrounded) // Player is falling
            {
                rb.gravityScale = originalGravityScale * fallGravityMultiplier;
            }
            // Player is rising AND jump button is released AND not grounded (for variable jump height)
            else if (rb.linearVelocity.y > 0 && !jumpAction.IsPressed() && !isGrounded)
            {
                rb.gravityScale = originalGravityScale * lowJumpGravityMultiplier;
            }
            else // Grounded or normal jump ascent with button held
            {
                rb.gravityScale = originalGravityScale;
            }

            HandleMovement(); // Normal movement
        }
    }

    private void PerformGroundCheck()
    {
        // Perform a boxcast instead of an overlap circle
        RaycastHit2D hit = Physics2D.BoxCast(groundCheckPoint.position, groundCheckSize, 0f, Vector2.down, 0f, groundLayer);
        isGrounded = hit.collider != null;

        // Reset double jump when landing
        if (!isGrounded)
        {
            Debug.Log("in air");
            return;
        }
        canDoubleJump = true;
    }

    private void HandleMovement()
    {
        // Apply horizontal velocity using normal moveSpeed
        rb.linearVelocity = new Vector2(moveDirection * moveSpeed, rb.linearVelocity.y);

        // Flip sprite logic (if you have it)
        // if (moveDirection > 0 && !isDashing) transform.localScale = new Vector3(1, 1, 1);
        // else if (moveDirection < 0 && !isDashing) transform.localScale = new Vector3(-1, 1, 1);
    }

    private void HandleDashPhysics()
    {
        currentDashTime += Time.fixedDeltaTime;
        rb.linearVelocity = dashDirection * dashSpeed; // Maintain dash velocity

        if (currentDashTime >= dashDuration)
        {
            isDashing = false;
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y); // Stop horizontal movement, maintain vertical if any
            rb.gravityScale = originalGravityScale; // Restore gravity
        }
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        if (isGrounded)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f); // Reset vertical velocity before jumping
            rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
            canDoubleJump = true; // Allow double jump after the first jump
            Debug.Log("Jumped!");
        }
        else if (canDoubleJump)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f); // Reset vertical velocity before double jumping
            rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
            canDoubleJump = false; // Disable double jump after using it
            Debug.Log("Double Jumped!");
        }
    }

    // Draw the ground check radius in the editor for easier setup
    void OnDrawGizmosSelected()
    {
        if (groundCheckPoint != null)
        {
            Gizmos.color = Color.red;
            // Draw the boxcast as a wire cube
            Gizmos.matrix = Matrix4x4.TRS(groundCheckPoint.position, Quaternion.identity, Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, groundCheckSize);
        }
    }

    private void TriggerDash(InputAction.CallbackContext context)
    {
        if (dashCooldownTimer <= 0 && !isDashing && isGrounded) // Dash only if cooldown ready, not already dashing, and grounded
        {
            isDashing = true;
            currentDashTime = 0f;
            dashCooldownTimer = dashCooldown;

            float horizontalInput = moveAction.ReadValue<Vector2>().x;
            if (Mathf.Approximately(horizontalInput, 0f))
            {
                // If no horizontal input, dash in the direction the player is visually facing
                // Assumes positive scale.x is facing right, negative is facing left.
                dashDirection = new Vector2(Mathf.Sign(transform.localScale.x), 0f);
            }
            else
            {
                // Dash in the direction of the current horizontal input
                dashDirection = new Vector2(Mathf.Sign(horizontalInput), 0f);
            }

            // Temporarily disable gravity for a more horizontal dash
            // originalGravityScale should be stored in Awake()
            rb.gravityScale = 0f;
            rb.linearVelocity = dashDirection * dashSpeed; // Initial burst
        }
    }
}
