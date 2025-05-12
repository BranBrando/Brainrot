using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerAttack : MonoBehaviour
{
    [Header("Attack Parameters")]
    [SerializeField] private float attackRange = 0.5f;
    [SerializeField] private Vector2 attackSize = new Vector2(0.5f, 0.5f);
    [SerializeField] private Vector2 attackOffset = new Vector2(0.5f, 0f); // Offset from the player's center
    [SerializeField] private LayerMask hittableLayer; // Layer for objects that can be hit
    [SerializeField] private float attackDamage = 10f; // Damage dealt by this attack

    private PlayerInput playerInput;
    private InputAction attackAction;
    private Animator anim;

    void Awake()
    {
        playerInput = GetComponent<PlayerInput>();
        attackAction = playerInput.actions["Player/Attack"]; // Assuming "Player" action map and "Attack" action
        anim = GetComponentInChildren<Animator>(); // Or GetComponent<Animator>()
        if (anim == null)
        {
            Debug.LogWarning("PlayerAttack: Animator component not found on Player or its children.");
        }
    }

    void OnEnable()
    {
        attackAction.performed += OnAttack;
    }

    void OnDisable()
    {
        attackAction.performed -= OnAttack;
    }

    private void OnAttack(InputAction.CallbackContext context)
    {
        if (anim != null)
        {
            anim.SetTrigger("attack"); // Or SetBool("attack", true); if using a bool
        }
    }

    public void PerformAttackDamageCheck()
    {
        // Determine current facing direction for attack offset
        bool isFacingRight = transform.localScale.x > 0;
        Vector2 currentAttackOffset = isFacingRight ? attackOffset : new Vector2(-attackOffset.x, attackOffset.y);

        // Calculate attack position using the potentially flipped offset
        Vector2 attackPosition = (Vector2)transform.position + currentAttackOffset;

        // Detect all colliders within the attack range
        Collider2D[] hitColliders = Physics2D.OverlapBoxAll(attackPosition, attackSize, 0, hittableLayer);

        // Apply knockback to the hit objects
        foreach (Collider2D hitCollider in hitColliders)
        {
            Health health = hitCollider.GetComponent<Health>();
            if (health != null)
            {
                // Calculate knockback direction
                Vector2 knockbackDirection = (hitCollider.transform.position - transform.position).normalized;

                // Apply damage and knockback using the new method
                health.ApplyDamageAndKnockback(attackDamage, knockbackDirection);
            }
        }
        Debug.Log("Attack performed! Hit " + hitColliders.Length + " targets.");
    }

    // Draw attack area in the editor for easier setup
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        // Determine current facing direction for gizmo offset
        bool isFacingRight = transform.localScale.x > 0;
        Vector2 currentAttackOffset = isFacingRight ? attackOffset : new Vector2(-attackOffset.x, attackOffset.y);
        // Calculate attack position using the potentially flipped offset
        Vector2 attackPosition = (Vector2)transform.position + currentAttackOffset;
        Gizmos.DrawWireCube(attackPosition, attackSize);
    }
}
