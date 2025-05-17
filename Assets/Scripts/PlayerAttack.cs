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

    [Header("Special Attack Parameters")]
    [SerializeField] private float spinAttackDamage = 15f;
    [SerializeField] private Vector2 spinAttackSize = new Vector2(0.8f, 0.8f); // Changed from radius to size
    [SerializeField] private Vector2 spinAttackOffset = new Vector2(0f, 0f);   // Added offset
    private bool isSpinAttacking = false;

    private PlayerInput playerInput;
    private InputAction attackAction;
    private InputAction specialAttackAction; // Added for special attack
    private Animator anim;
    private Rigidbody2D rb;
    private PlayerMovement playerMovement;

    void Awake()
    {
        playerInput = GetComponent<PlayerInput>();
        attackAction = playerInput.actions["Player/Attack"];
        specialAttackAction = playerInput.actions["Player/SpecialAttack"]; // Initialize SpecialAttack action
        anim = GetComponentInChildren<Animator>();
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
        specialAttackAction.started += OnSpecialAttack; // Subscribe to SpecialAttack
    }

    void OnDisable()
    {
        attackAction.started -= OnAttack;
        specialAttackAction.started -= OnSpecialAttack; // Unsubscribe from SpecialAttack
    }

    private void OnAttack(InputAction.CallbackContext context)
    {
        // Basic attack logic, ensure it doesn't conflict with special attack
        if (anim != null && !isSpinAttacking && (playerMovement == null || playerMovement.CanAttack()))
        {
            anim.SetTrigger("attack");
        }
    }

    private void OnSpecialAttack(InputAction.CallbackContext context)
    {
        // Cooldown check removed
        if (!isSpinAttacking && (playerMovement == null || playerMovement.CanAttack()))
        {
            isSpinAttacking = true;
            // lastSpinAttackTime = Time.time; // Removed
            if (anim != null)
            {
                anim.SetTrigger("spinAttack");
            }
            // Optional: if special attack should affect movement state
            // if (playerMovement != null) playerMovement.IsAttacking = true;
            Debug.Log("Special Attack Initiated!");
        }
    }

    public void PerformLightAttackDamageCheck()
    {
        bool isFacingRight = transform.localScale.x > 0;
        Vector2 currentAttackOffset = isFacingRight ? lightAttackOffset : new Vector2(-lightAttackOffset.x, lightAttackOffset.y);
        Vector2 attackPosition = (Vector2)transform.position + currentAttackOffset;
        Collider2D[] hitColliders = Physics2D.OverlapBoxAll(attackPosition, lightAttackSize, 0, hittableLayer);

        foreach (Collider2D hitCollider in hitColliders)
        {
            Health health = hitCollider.GetComponent<Health>();
            if (health != null)
            {
                Vector2 knockbackDirection = (hitCollider.transform.position - transform.position).normalized;
                health.ApplyDamageAndKnockback(lightAttackDamage, knockbackDirection);
            }
        }
        Debug.Log("Light Attack performed! Hit " + hitColliders.Length + " targets.");
    }

    public void PerformHeavyAttackDamageCheck()
    {
        bool isFacingRight = transform.localScale.x > 0;
        Vector2 currentAttackOffset = isFacingRight ? heavyAttackOffset : new Vector2(-heavyAttackOffset.x, heavyAttackOffset.y);
        Vector2 attackPosition = (Vector2)transform.position + currentAttackOffset;
        Collider2D[] hitColliders = Physics2D.OverlapBoxAll(attackPosition, heavyAttackSize, 0, hittableLayer);

        foreach (Collider2D hitCollider in hitColliders)
        {
            Health health = hitCollider.GetComponent<Health>();
            if (health != null)
            {
                Vector2 knockbackDirection = (hitCollider.transform.position - transform.position).normalized;
                health.ApplyDamageAndKnockback(heavyAttackDamage, knockbackDirection);
            }
        }
        Debug.Log("Heavy Attack performed! Hit " + hitColliders.Length + " targets.");
    }

    public void PerformSpecialAttackDamageCheck()
    {
        bool isFacingRight = transform.localScale.x > 0;
        Vector2 currentAttackOffset = isFacingRight ? spinAttackOffset : new Vector2(-spinAttackOffset.x, spinAttackOffset.y);
        Vector2 attackPosition = (Vector2)transform.position + currentAttackOffset;
        Collider2D[] hitColliders = Physics2D.OverlapBoxAll(attackPosition, spinAttackSize, 0, hittableLayer);

        foreach (Collider2D hitCollider in hitColliders)
        {
            Health health = hitCollider.GetComponent<Health>();
            if (health != null)
            {
                Vector2 knockbackDirection = (hitCollider.transform.position - transform.position).normalized;
                // Assuming special attack might have different knockback or just damage
                health.ApplyDamageAndKnockback(spinAttackDamage, knockbackDirection);
            }
        }
        Debug.Log("Special Attack damage check! Hit " + hitColliders.Length + " targets.");
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

    public void FinishSpecialAttack()
    {
        isSpinAttacking = false;
        // Optional: if special attack affected movement state
        // if (playerMovement != null) playerMovement.IsAttacking = false;
        Debug.Log("Special attack finished!");
    }

    public void ResetAttackTrigger()
    {
        if (anim != null)
        {
            anim.ResetTrigger("attack");
        }
    }

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

        // Special Attack Gizmo
        Gizmos.color = Color.cyan;
        Vector2 currentSpinAttackOffset = transform.localScale.x > 0 ? spinAttackOffset : new Vector2(-spinAttackOffset.x, spinAttackOffset.y); // If directional
        Vector2 spinGizmoPosition = (Vector2)transform.position + currentSpinAttackOffset; // Assuming centered for Gizmo too
        Gizmos.DrawWireCube(spinGizmoPosition, spinAttackSize); // Changed to DrawWireCube
    }
}
