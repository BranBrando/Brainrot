using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(PlayerInput))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float jumpForce = 10f;

    [Header("Ground Check")]
    [SerializeField] private Transform groundCheckPoint;
    [SerializeField] private Vector2 groundCheckSize = new Vector2(0.4f, 0.1f); // Width and height of the boxcast
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float groundCheckYOffset = -0.1f;

    private Rigidbody2D rb;
    private PlayerInput playerInput;
    private InputAction moveAction;
    private InputAction jumpAction;
    private Collider2D playerCollider;

    private bool isGrounded;
    private bool canDoubleJump;
    private float moveDirection;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        playerInput = GetComponent<PlayerInput>();

        // Ensure the Action Map name matches the one in your Input Actions asset
        moveAction = playerInput.actions["Player/Move"];
        jumpAction = playerInput.actions["Player/Jump"];

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
    }

    void OnDisable()
    {
        jumpAction.performed -= OnJump;
    }

    void Update()
    {
        // Read horizontal movement input
        moveDirection = moveAction.ReadValue<Vector2>().x;
    }

    void FixedUpdate()
    {
        PerformGroundCheck();
        HandleMovement();
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
        // Apply horizontal velocity
        rb.linearVelocity = new Vector2(moveDirection * moveSpeed, rb.linearVelocity.y);

        // Flip sprite based on movement direction (optional, requires sprite renderer reference)
        // if (moveDirection > 0) transform.localScale = new Vector3(1, 1, 1);
        // else if (moveDirection < 0) transform.localScale = new Vector3(-1, 1, 1);
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        if (isGrounded)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f); // Reset vertical velocity before jumping
            rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
            canDoubleJump = true; // Allow double jump after the first jump
            // Debug.Log("Jumped!");
        }
        else if (canDoubleJump)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f); // Reset vertical velocity before double jumping
            rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
            canDoubleJump = false; // Disable double jump after using it
            // Debug.Log("Double Jumped!");
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
}
