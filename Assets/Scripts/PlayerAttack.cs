using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
// No longer need using Brainrot.Player; as PlayerBuffManager is now global

public class PlayerAttack : NetworkBehaviour
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

    // Original attack parameters to store initial values
    private Vector2 _originalLightAttackSize;
    private Vector2 _originalLightAttackOffset;
    private Vector2 _originalHeavyAttackSize;
    private Vector2 _originalHeavyAttackOffset;
    private Vector2 _originalSpinAttackSize;
    private Vector2 _originalSpinAttackOffset;

    // Current attack parameters, dynamically adjusted by buffs
    private Vector2 _currentLightAttackSize;
    private Vector2 _currentLightAttackOffset;
    private Vector2 _currentHeavyAttackSize;
    private Vector2 _currentHeavyAttackOffset;
    private Vector2 _currentSpinAttackSize;
    private Vector2 _currentSpinAttackOffset;

    private PlayerInput playerInput;
    private InputAction attackAction;
    private InputAction specialAttackAction; // Added for special attack
    private Animator anim;
    private Rigidbody2D rb;
    private PlayerMovement playerMovement;
    private PlayerBuffManager _buffManager; // Added for buff effects

    void Awake()
    {
        // Cache original values from serialized fields
        _originalLightAttackSize = lightAttackSize;
        _originalLightAttackOffset = lightAttackOffset;
        _originalHeavyAttackSize = heavyAttackSize;
        _originalHeavyAttackOffset = heavyAttackOffset;
        _originalSpinAttackSize = spinAttackSize;
        _originalSpinAttackOffset = spinAttackOffset;

        // Initialize current values to original values
        _currentLightAttackSize = _originalLightAttackSize;
        _currentLightAttackOffset = _originalLightAttackOffset;
        _currentHeavyAttackSize = _originalHeavyAttackSize;
        _currentHeavyAttackOffset = _originalHeavyAttackOffset;
        _currentSpinAttackSize = _originalSpinAttackSize;
        _currentSpinAttackOffset = _originalSpinAttackOffset;
    }

    public override void OnNetworkSpawn()
    {
        // Get components that are needed by all clients first
        _buffManager = GetComponent<PlayerBuffManager>();
        anim = GetComponentInChildren<Animator>(); // If needed for non-owner visuals related to attack state
        rb = GetComponent<Rigidbody2D>(); // Needed for heavy attack dash, even if not owner-controlled movement
        playerMovement = GetComponent<PlayerMovement>(); // Needed for CanAttack() check, even if not owner-controlled movement

        if (_buffManager == null)
        {
            Debug.LogError("PlayerAttack: PlayerBuffManager component not found on Player!");
            // Potentially disable the component if buffManager is critical and missing
            // enabled = false; 
            // return;
        }
        else
        {
            // ALL clients (owner and non-owners) need to know the correct attack parameters
            // for accurate representation (e.g., Gizmos, client-side effects).
            _buffManager.ScaleMultiplier.OnValueChanged += HandleScaleMultiplierChanged;
            // Apply initial scale. This is important for late joiners or if scale is set before this spawn.
            UpdateAttackParameters(_buffManager.ScaleMultiplier.Value);
        }

        if (!IsOwner)
        {
            // Disable input for non-local players.
            // Other components like PlayerMovement might also disable themselves if !IsOwner.
            PlayerInput pi = GetComponent<PlayerInput>();
            if (pi != null) pi.enabled = false;
            return; // Crucially, non-owners don't set up input actions.
        }

        // Owner-specific initializations:
        playerInput = GetComponent<PlayerInput>();
        attackAction = playerInput.actions["Player/Attack"];
        specialAttackAction = playerInput.actions["Player/SpecialAttack"];

        attackAction.started += OnAttack;
        specialAttackAction.started += OnSpecialAttack;

        // Redundant checks if already done above, but good for clarity if separated
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

    public override void OnNetworkDespawn()
    {
        // ALL clients that subscribed should unsubscribe.
        if (_buffManager != null)
        {
            _buffManager.ScaleMultiplier.OnValueChanged -= HandleScaleMultiplierChanged;
        }

        // Owner-specific cleanup
        if (IsOwner && playerInput != null) // Check playerInput as it's owner-specific
        {
            if (attackAction != null) attackAction.started -= OnAttack;
            if (specialAttackAction != null) specialAttackAction.started -= OnSpecialAttack;
        }
    }

    private void HandleScaleMultiplierChanged(Vector3 previousValue, Vector3 newValue)
    {
        // This method is called on all clients when ScaleMultiplier changes.
        // Update attack parameters based on the new scale.
        UpdateAttackParameters(newValue);
    }

    public void UpdateAttackParameters(Vector3 scaleMultiplier)
    {
        float scaleX = Mathf.Abs(scaleMultiplier.x);
        float scaleY = Mathf.Abs(scaleMultiplier.y);
        float compensationFactor = 1f; // Tune this value (0 to 1, or even slightly > 1)

        // Scale attack sizes
        _currentLightAttackSize = _originalLightAttackSize * new Vector2(scaleX, scaleY);
        _currentHeavyAttackSize = _originalHeavyAttackSize * new Vector2(scaleX, scaleY);
        _currentSpinAttackSize = _originalSpinAttackSize * new Vector2(scaleX, scaleY);

        // Calculate and apply scaled offsets with compensation
        Vector2 currentLightOffset = _originalLightAttackOffset * new Vector2(scaleX, scaleY);
        Vector2 currentHeavyOffset = _originalHeavyAttackOffset * new Vector2(scaleX, scaleY);
        Vector2 currentSpinOffset = _originalSpinAttackOffset * new Vector2(scaleX, scaleY);

        // Apply compensation if shrunk
        if (scaleX < 1.0f)
        {
            // Compensate for light attack offset X
            if (_originalLightAttackOffset.x != 0)
            {
                float lightOffsetShrinkage = _originalLightAttackOffset.x * (1.0f - scaleX);
                currentLightOffset.x += lightOffsetShrinkage * compensationFactor * Mathf.Sign(_originalLightAttackOffset.x);
            }

            // Compensate for heavy attack offset X
            if (_originalHeavyAttackOffset.x != 0)
            {
                float heavyOffsetShrinkage = _originalHeavyAttackOffset.x * (1.0f - scaleX);
                currentHeavyOffset.x += heavyOffsetShrinkage * compensationFactor * Mathf.Sign(_originalHeavyAttackOffset.x);
            }

            // Compensate for spin attack offset X
            if (_originalSpinAttackOffset.x != 0)
            {
                float spinOffsetShrinkage = _originalSpinAttackOffset.x * (1.0f - scaleX);
                currentSpinOffset.x += spinOffsetShrinkage * compensationFactor * Mathf.Sign(_originalSpinAttackOffset.x);
            }
        }

        _currentLightAttackOffset = currentLightOffset;
        _currentHeavyAttackOffset = currentHeavyOffset;
        _currentSpinAttackOffset = currentSpinOffset;
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
        Vector2 currentAttackOffset = isFacingRight ? _currentLightAttackOffset : new Vector2(-_currentLightAttackOffset.x, _currentLightAttackOffset.y);
        Vector2 attackPosition = (Vector2)transform.position + currentAttackOffset;
        Collider2D[] hitColliders = Physics2D.OverlapBoxAll(attackPosition, _currentLightAttackSize, 0, hittableLayer);

        float actualLightAttackDamage = lightAttackDamage;
        if (_buffManager != null)
        {
            actualLightAttackDamage *= _buffManager.DamageOutputMultiplier.Value;
        }

        foreach (Collider2D hitCollider in hitColliders)
        {
            if (hitCollider.CompareTag("Player") && hitCollider.gameObject == gameObject)
            {
                continue; // Skip self-hit
            }
            Health health = hitCollider.GetComponent<Health>();
            if (health != null)
            {
                Vector2 knockbackDirection = (hitCollider.transform.position - transform.position).normalized;
                health.ApplyDamageAndKnockback(actualLightAttackDamage, knockbackDirection);
            }
        }
        Debug.Log("Light Attack performed! Hit " + hitColliders.Length + " targets.");
    }

    public void PerformHeavyAttackDamageCheck()
    {
        bool isFacingRight = transform.localScale.x > 0;
        Vector2 currentAttackOffset = isFacingRight ? _currentHeavyAttackOffset : new Vector2(-_currentHeavyAttackOffset.x, _currentHeavyAttackOffset.y);
        Vector2 attackPosition = (Vector2)transform.position + currentAttackOffset;
        Collider2D[] hitColliders = Physics2D.OverlapBoxAll(attackPosition, _currentHeavyAttackSize, 0, hittableLayer);

        float actualHeavyAttackDamage = heavyAttackDamage;
        if (_buffManager != null)
        {
            actualHeavyAttackDamage *= _buffManager.DamageOutputMultiplier.Value;
        }

        foreach (Collider2D hitCollider in hitColliders)
        {
            if (hitCollider.CompareTag("Player") && hitCollider.gameObject == gameObject)
            {
                continue; // Skip self-hit
            }
            Health health = hitCollider.GetComponent<Health>();
            if (health != null)
            {
                Vector2 knockbackDirection = (hitCollider.transform.position - transform.position).normalized;
                health.ApplyDamageAndKnockback(actualHeavyAttackDamage, knockbackDirection);
            }
        }
        Debug.Log("Heavy Attack performed! Hit " + hitColliders.Length + " targets.");
    }

    public void PerformSpecialAttackDamageCheck()
    {
        bool isFacingRight = transform.localScale.x > 0;
        Vector2 currentAttackOffset = isFacingRight ? _currentSpinAttackOffset : new Vector2(-_currentSpinAttackOffset.x, _currentSpinAttackOffset.y);
        Vector2 attackPosition = (Vector2)transform.position + currentAttackOffset;
        Collider2D[] hitColliders = Physics2D.OverlapBoxAll(attackPosition, _currentSpinAttackSize, 0, hittableLayer);

        float actualSpinAttackDamage = spinAttackDamage;
        if (_buffManager != null)
        {
            actualSpinAttackDamage *= _buffManager.DamageOutputMultiplier.Value;
        }

        foreach (Collider2D hitCollider in hitColliders)
        {
            if (hitCollider.CompareTag("Player") && hitCollider.gameObject == gameObject)
            {
                continue; // Skip self-hit
            }
            Health health = hitCollider.GetComponent<Health>();
            if (health != null)
            {
                Vector2 knockbackDirection = (hitCollider.transform.position - transform.position).normalized;
                // Assuming special attack might have different knockback or just damage
                health.ApplyDamageAndKnockback(actualSpinAttackDamage, knockbackDirection);
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
        Vector2 currentLightAttackOffset = isFacingRight ? _currentLightAttackOffset : new Vector2(-_currentLightAttackOffset.x, _currentLightAttackOffset.y);
        Vector2 lightAttackPosition = (Vector2)transform.position + currentLightAttackOffset;
        Gizmos.DrawWireCube(lightAttackPosition, _currentLightAttackSize);

        // Heavy Attack Gizmo
        Gizmos.color = Color.magenta;
        Vector2 currentHeavyAttackOffset = isFacingRight ? _currentHeavyAttackOffset : new Vector2(-_currentHeavyAttackOffset.x, _currentHeavyAttackOffset.y);
        Vector2 heavyAttackPosition = (Vector2)transform.position + currentHeavyAttackOffset;
        Gizmos.DrawWireCube(heavyAttackPosition, _currentHeavyAttackSize);

        // Special Attack Gizmo
        Gizmos.color = Color.cyan;
        Vector2 currentSpinAttackOffset = transform.localScale.x > 0 ? _currentSpinAttackOffset : new Vector2(-_currentSpinAttackOffset.x, _currentSpinAttackOffset.y); // If directional
        Vector2 spinGizmoPosition = (Vector2)transform.position + currentSpinAttackOffset; // Assuming centered for Gizmo too
        Gizmos.DrawWireCube(spinGizmoPosition, _currentSpinAttackSize); // Changed to DrawWireCube
    }
}
