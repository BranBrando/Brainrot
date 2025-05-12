using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerAttack : MonoBehaviour
{
    [SerializeField] private LayerMask hittableLayer; // Layer for objects that can be hit

    [Header("Light Attack Parameters")]
    [SerializeField] private float lightAttackDamage = 10f;
    [SerializeField] private Vector2 lightAttackSize = new Vector2(0.5f, 0.5f);
    [SerializeField] private Vector2 lightAttackOffset = new Vector2(0.5f, 0f);

    [Header("Heavy Attack Parameters")]
    [SerializeField] private float heavyAttackDamage = 20f;
    [SerializeField] private Vector2 heavyAttackSize = new Vector2(0.4f, 0.4f);
    [SerializeField] private Vector2 heavyAttackOffset = new Vector2(0.3f, 0f);
    [SerializeField] private float heavyAttackDashForce = 15f;
    [SerializeField] private ForceMode2D heavyAttackDashMode = ForceMode2D.Impulse;

    private PlayerInput playerInput;
    private InputAction attackAction;
    private Animator anim;
    private Rigidbody2D rb;
    private PlayerMovement playerMovement;

    void Awake()
    {
        playerInput = GetComponent<PlayerInput>();
        attackAction = playerInput.actions["Player/Attack"]; // Assuming "Player" action map and "Attack" action
        anim = GetComponentInChildren<Animator>(); // Or GetComponent<Animator>()
        rb = GetComponent<Rigidbody2D>();
        playerMovement = GetComponent<PlayerMovement>();

        if (anim == null)
        {
            Debug.LogWarning("PlayerAttack: Animator component not found on Player or its children.");
        }
        if (rb == null)
        {
            Debug.LogWarning("PlayerAttack: Rigidbody2D component not found on Player.");
        }
        if (playerMovement == null)
        {
            Debug.LogWarning("PlayerAttack: PlayerMovement component not found on Player.");
        }
    }

    void OnEnable()
    {
        attackAction.started += OnAttack;
    }

    void OnDisable()
    {
        attackAction.started -= OnAttack;
    }

    private void OnAttack(InputAction.CallbackContext context)
    {
        if (anim != null)
        {
            anim.SetTrigger("attack"); // Or SetBool("attack", true); if using a bool
        }
    }

    public void PerformLightAttackDamageCheck()
    {
        // Determine current facing direction for attack offset
        bool isFacingRight = transform.localScale.x > 0;
        Vector2 currentAttackOffset = isFacingRight ? lightAttackOffset : new Vector2(-lightAttackOffset.x, lightAttackOffset.y);

        // Calculate attack position using the potentially flipped offset
        Vector2 attackPosition = (Vector2)transform.position + currentAttackOffset;

        // Detect all colliders within the attack range
        Collider2D[] hitColliders = Physics2D.OverlapBoxAll(attackPosition, lightAttackSize, 0, hittableLayer);

        // Apply knockback to the hit objects
        foreach (Collider2D hitCollider in hitColliders)
        {
            Health health = hitCollider.GetComponent<Health>();
            if (health != null)
            {
                // Calculate knockback direction
                Vector2 knockbackDirection = (hitCollider.transform.position - transform.position).normalized;

                // Apply damage and knockback using the new method
                health.ApplyDamageAndKnockback(lightAttackDamage, knockbackDirection);
            }
        }
        Debug.Log("Light Attack performed! Hit " + hitColliders.Length + " targets.");
    }

    public void PerformHeavyAttackDamageCheck()
    {
        // Determine current facing direction for attack offset
        bool isFacingRight = transform.localScale.x > 0;
        Vector2 currentAttackOffset = isFacingRight ? heavyAttackOffset : new Vector2(-heavyAttackOffset.x, heavyAttackOffset.y);

        // Calculate attack position using the potentially flipped offset
        Vector2 attackPosition = (Vector2)transform.position + currentAttackOffset;

        // Detect all colliders within the attack range
        Collider2D[] hitColliders = Physics2D.OverlapBoxAll(attackPosition, heavyAttackSize, 0, hittableLayer);

        // Apply knockback to the hit objects
        foreach (Collider2D hitCollider in hitColliders)
        {
            Health health = hitCollider.GetComponent<Health>();
            if (health != null)
            {
                // Calculate knockback direction
                Vector2 knockbackDirection = (hitCollider.transform.position - transform.position).normalized;

                // Apply damage and knockback using the new method
                health.ApplyDamageAndKnockback(heavyAttackDamage, knockbackDirection);
            }
        }
        Debug.Log("Heavy Attack performed! Hit " + hitColliders.Length + " targets.");
    }

    public void ExecuteHeavyAttackDash()
    {
        if (rb != null)
        {
            float direction = transform.localScale.x > 0 ? 1f : -1f;
            Vector2 dashVector = new Vector2(direction * heavyAttackDashForce, 0);
            rb.AddForce(dashVector, heavyAttackDashMode);
            Debug.Log("Dash Executed!");
            if (playerMovement != null) playerMovement.IsAttacking = true;
        }
    }

    public void FinishHeavyAttack()
    {
        if (playerMovement != null) playerMovement.IsAttacking = false;
        Debug.Log("Heavy attack finished!");
    }

    public void ResetAttackTrigger()
    {
        if (anim != null)
        {
            anim.ResetTrigger("attack"); // Reset the attack trigger
        }
    }

    // Draw attack area in the editor for easier setup
    void OnDrawGizmosSelected()
    {
        bool isFacingRight = transform.localScale.x > 0;

        // Light Attack Gizmo
        Gizmos.color = Color.red;
        Vector2 currentLightAttackOffset = isFacingRight ? lightAttackOffset : new Vector2(-lightAttackOffset.x, lightAttackOffset.y);
        Vector2 lightAttackPosition = (Vector2)transform.position + currentLightAttackOffset;
        Gizmos.DrawWireCube(lightAttackPosition, lightAttackSize);

        // Heavy Attack Gizmo
        Gizmos.color = Color.magenta;
        Vector2 currentHeavyAttackOffset = isFacingRight ? heavyAttackOffset : new Vector2(-heavyAttackOffset.x, heavyAttackOffset.y);
        Vector2 heavyAttackPosition = (Vector2)transform.position + currentHeavyAttackOffset;
        Gizmos.DrawWireCube(heavyAttackPosition, heavyAttackSize);
    }
}
