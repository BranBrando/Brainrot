using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic; // Required for List

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
    [SerializeField] private LayerMask enemyLayer; // Layer enemies are on
    [SerializeField] private float dashEnemyCheckRadius = 1.5f; // Radius to check for enemies to ignore

    private List<Collider2D> ignoredEnemyColliders = new List<Collider2D>();

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
    private bool isFacingRight = true; // Added for sprite flipping
    private float currentDashTime;
    private float dashCooldownTimer;
    private Vector2 dashDirection;
    private float originalGravityScale;

    public bool IsAttacking { get; set; } // Flag to pause movement during attacks


    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        playerInput = GetComponent<PlayerInput>();

        // Ensure the Action Map name matches the one in your Input Actions asset
        moveAction = playerInput.actions["Player/Move"];
        jumpAction = playerInput.actions["Player/Jump"];
        dashAction = playerInput.actions["Player/Dash"]; // Ensure this action exists in your Input Actions asset
        originalGravityScale = rb.gravityScale;

        anim = GetComponentInChildren<Animator>(); // Or GetComponent<Animator>() if it's on the root
        if (anim == null)
        {
            Debug.LogWarning("PlayerMovement: Animator component not found on Player or its children.");
        }

        playerCollider = GetComponent<Collider2D>();
        if (playerCollider == null)
        {
            Debug.LogError("PlayerMovement: No Collider2D found on this GameObject. Cannot implement dash-through enemies.");
        }

        // Adjust ground check point based on collider
        if (groundCheckPoint != null)
        {
            PositionGroundCheck();
        }
        else
        {
            Debug.LogError("PlayerMovement: GroundCheckPoint is not assigned in the Inspector!");
        }
    }

    void PositionGroundCheck()
    {
        if (playerCollider != null)
        {
            float worldColliderBottomY = playerCollider.bounds.center.y - playerCollider.bounds.extents.y;
            Vector3 worldPointAtColliderBottom = new Vector3(
                groundCheckPoint.position.x, 
                worldColliderBottomY,
                groundCheckPoint.position.z
            );
            float targetLocalY = transform.InverseTransformPoint(worldPointAtColliderBottom).y + groundCheckYOffset;
            groundCheckPoint.localPosition = new Vector3(
                groundCheckPoint.localPosition.x, 
                targetLocalY,
                groundCheckPoint.localPosition.z
            );
        }
        else
        {
            // Fallback if no collider
            groundCheckPoint.localPosition = new Vector3(
                groundCheckPoint.localPosition.x, 
                groundCheckYOffset, 
                groundCheckPoint.localPosition.z
            );
            Debug.LogWarning("PlayerMovement: Collider2D not found. GroundCheckPoint offset from transform pivot.");
        }
    }


    void OnEnable()
    {
        jumpAction.performed += OnJump;
        dashAction.performed += TriggerDash;
    }

    void OnDisable()
    {
        // Ensure collisions are re-enabled if disabled during dash
        if (playerCollider != null)
        {
            foreach (Collider2D enemyCollider in ignoredEnemyColliders)
            {
                if (enemyCollider != null) 
                {
                    Physics2D.IgnoreCollision(playerCollider, enemyCollider, false);
                }
            }
        }
        ignoredEnemyColliders.Clear();

        jumpAction.performed -= OnJump;
        dashAction.performed -= TriggerDash;
    }

    void Update()
    {
        HandleInput();
        UpdateCooldowns();
        UpdateAnimator();
    }

    void HandleInput()
    {
        if (!isDashing) 
        {
            moveDirection = moveAction.ReadValue<Vector2>().x;
        }
    }

    void UpdateCooldowns()
    {
         if (dashCooldownTimer > 0)
        {
            dashCooldownTimer -= Time.deltaTime;
        }
    }

    void UpdateAnimator()
    {
        if (anim != null)
        {
            anim.SetFloat("speed", Mathf.Abs(rb.linearVelocity.x));
            if (isGrounded)
            {
                anim.SetBool("isGrounded", true); 
            }
            else
            {
                anim.SetBool("isGrounded", false); 
            }
        }
    }


     void FixedUpdate()
    {
        PerformGroundCheck();

        if (isDashing)
        {
            HandleDashPhysics();
        }
        else if (!IsAttacking)
        {
            ApplyGravityModifiers();
            HandleMovement();
        }
    }

    void ApplyGravityModifiers()
    {
         if (rb.linearVelocity.y < 0 && !isGrounded) 
        {
            rb.gravityScale = originalGravityScale * fallGravityMultiplier;
        }
        else if (rb.linearVelocity.y > 0 && !jumpAction.IsPressed() && !isGrounded)
        {
            rb.gravityScale = originalGravityScale * lowJumpGravityMultiplier;
        }
        else 
        {
            rb.gravityScale = originalGravityScale;
        }
    }

    private void PerformGroundCheck()
    {
        if (groundCheckPoint == null) return; 

        isGrounded = Physics2D.OverlapBox(groundCheckPoint.position, groundCheckSize, 0f, groundLayer);

        if (isGrounded)
        {
            canDoubleJump = true; // Reset double jump on landing
        } else {
             Debug.Log("in air"); // Keep this for debugging if needed
        }
    }

    private void HandleMovement()
    {
        rb.linearVelocity = new Vector2(moveDirection * moveSpeed, rb.linearVelocity.y);
        FlipCheck(); // Call flip check
    }

    private void FlipCheck()
    {
        // Flip sprite based on movement direction
        if (moveDirection > 0 && !isFacingRight)
        {
            Flip();
        }
        else if (moveDirection < 0 && isFacingRight)
        {
            Flip();
        }
    }

    private void Flip()
    {
        isFacingRight = !isFacingRight;
        // Multiply the player's x local scale by -1.
        Vector3 theScale = transform.localScale;
        theScale.x *= -1;
        transform.localScale = theScale;
    }

    private void TriggerDash(InputAction.CallbackContext context)
    {
        if (dashCooldownTimer <= 0 && !isDashing && isGrounded && playerCollider != null) 
        {
            // --- Start Ignoring Enemy Collisions ---
            ignoredEnemyColliders.Clear();
            Collider2D[] enemiesToIgnore = Physics2D.OverlapCircleAll(transform.position, dashEnemyCheckRadius, enemyLayer);

            foreach (Collider2D enemyCollider in enemiesToIgnore)
            {
                if (enemyCollider != playerCollider) // Don't ignore self
                {
                    Physics2D.IgnoreCollision(playerCollider, enemyCollider, true);
                    ignoredEnemyColliders.Add(enemyCollider);
                }
            }
            // --- End Ignoring Enemy Collisions ---

            isDashing = true;
            currentDashTime = 0f;
            dashCooldownTimer = dashCooldown;

            float horizontalInput = moveAction.ReadValue<Vector2>().x;
            if (Mathf.Approximately(horizontalInput, 0f))
            {
                dashDirection = new Vector2(Mathf.Sign(transform.localScale.x), 0f);
            }
            else
            {
                dashDirection = new Vector2(Mathf.Sign(horizontalInput), 0f);
            }

            rb.gravityScale = 0f; 
            rb.linearVelocity = dashDirection * dashSpeed; // Use velocity for direct control during dash
        }
    }

    private void HandleDashPhysics()
    {
        rb.linearVelocity = dashDirection * dashSpeed; // Maintain dash velocity
        currentDashTime += Time.fixedDeltaTime;

        if (currentDashTime >= dashDuration)
        {
            StopDash();
        }
    }

    private void StopDash()
    {
         // --- Start Re-enabling Enemy Collisions ---
        if (playerCollider != null)
        {
            foreach (Collider2D enemyCollider in ignoredEnemyColliders)
            {
                if (enemyCollider != null) 
                {
                    Physics2D.IgnoreCollision(playerCollider, enemyCollider, false);
                }
            }
        }
        ignoredEnemyColliders.Clear();
        // --- End Re-enabling Enemy Collisions ---

        isDashing = false;
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y); // Stop horizontal movement
        rb.gravityScale = originalGravityScale; // Restore gravity
    }


    public void OnJump(InputAction.CallbackContext context)
    {
        if (isGrounded)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f); // Reset vertical velocity before jumping
            rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
            canDoubleJump = true; 
            Debug.Log("Jumped!");
        }
        else if (canDoubleJump)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f); // Reset vertical velocity before double jumping
            rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
            canDoubleJump = false; 
            Debug.Log("Double Jumped!");
        }
    }

    // Draw the ground check radius in the editor for easier setup)
    void OnDrawGizmosSelected()
    {
        if (groundCheckPoint != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(groundCheckPoint.position, groundCheckSize);
        }

        // Draw the dash enemy check radius
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, dashEnemyCheckRadius);
    }

    public bool CanAttack()
    {
        // Player can attack if they are grounded, not dashing, and not already in an attack animation/state.
        return !isDashing && !IsAttacking;
    }
}
