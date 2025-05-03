using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerAttack : MonoBehaviour
{
    [Header("Attack Parameters")]
    [SerializeField] private float attackRange = 0.5f;
    [SerializeField] private Vector2 attackSize = new Vector2(0.5f, 0.5f);
    [SerializeField] private Vector2 attackOffset = new Vector2(0.5f, 0f); // Offset from the player's center
    [SerializeField] private float knockbackForce = 5f;
    [SerializeField] private LayerMask hittableLayer; // Layer for objects that can be hit

    private PlayerInput playerInput;
    private InputAction attackAction;

    void Awake()
    {
        playerInput = GetComponent<PlayerInput>();
        attackAction = playerInput.actions["Player/Attack"]; // Assuming "Player" action map and "Attack" action
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
        // Calculate attack position and size
        Vector2 attackPosition = (Vector2)transform.position + attackOffset;

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

                // Apply knockback
                health.TakeKnockback(knockbackDirection, knockbackForce);
            }
        }
        Debug.Log("Attack performed! Hit " + hitColliders.Length + " targets.");
    }

    // Draw attack area in the editor for easier setup
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Vector2 attackPosition = (Vector2)transform.position + attackOffset;
        Gizmos.DrawWireCube(attackPosition, attackSize);
    }
}
