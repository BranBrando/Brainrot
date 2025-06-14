using UnityEngine;
using System.Collections;
using System.Collections.Generic; // Added for List
using Unity.Netcode;
using Unity.VisualScripting; // Added for networking
// using Brainrot.Player; // Removed as PlayerBuffManager is now in global namespace

[RequireComponent(typeof(Rigidbody2D))]
public class Health : NetworkBehaviour // Changed to NetworkBehaviour
{
    public bool isPlayerCharacter = true; // Inspector-settable flag to differentiate behavior

    [Header("Combat Mechanics")]
    public float currentDamagePercentage = 0f;
    public int stocks = 3; // Default, can be overridden by GameManager for players

    [Header("Knockback & Knockout Forces")]
    public float standardKnockbackForce = 5f; // The constant force for regular hits.
    public float criticalKnockoutForce = 25f; // A significantly higher force for successful probabilistic knockouts.

    [Header("Knockout Probability")]
    [Tooltip("Base chance (0.0 to 1.0) of a knockout at 0% damage.")]
    public float knockoutProbabilityBase = 0.01f; // e.g., 1% base chance
    [Tooltip("How much the knockout chance increases per 1% of damage. E.g., 0.005 means 0.5% increase per 1% damage.")]
    public float knockoutProbabilityPerDamagePercent = 0.005f; // So at 100% damage, chance = base + 100 * 0.005 = base + 0.5

    [Header("References")]
    public GameManager gameManager;
    private Rigidbody2D rb;
    private PlayerBuffManager _buffManager; // Now in global namespace

    [Header("Damping Settings")]
    public float normalLinearDamping = 1f; // Set this to your desired default in Inspector
    public float collisionLinearDamping = 50f; // High damping when pushing against another character
    private bool isBeingKnockedBack = false;
    private bool inContactWithOtherCharacter = false; // New flag to track contact

    // Public getter for the knockback state
    public bool IsBeingKnockedBack()
    {
        return isBeingKnockedBack;
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearDamping = normalLinearDamping; // Initialize with normal damping
        }
        else
        {
             Debug.LogError("Rigidbody2D not found on " + gameObject.name);
        }
        _buffManager = GetComponent<PlayerBuffManager>();
        if (_buffManager == null)
        {
            // This might be okay if non-player entities with Health don't have buffs
            // Debug.LogWarning("Health: PlayerBuffManager component not found on " + gameObject.name);
        }
    }

    void Start()
    {
        // Always try to get the GameManager via its singleton Instance.
        // This is more reliable than FindFirstObjectByType, especially for timing issues.
        GameManager gmInstance = GameManager.Instance;

        if (gmInstance != null)
        {
            this.gameManager = gmInstance; // Assign to the local field if you keep it.
            gmInstance.RegisterEntity(this); // Register with the instance.
        }
        else
        {
            // This warning will now only appear if GameManager.Instance itself is null,
            // which would mean the GameManager object isn't in the scene or its Awake() hasn't run.
            Debug.LogWarning("GameManager.Instance is null. Cannot register entity: " + gameObject.name);
        }
    }

    // Updated to handle damage and determine knockback/knockout force probabilistically
    public void ApplyDamageAndKnockback(float damageAmount, Vector2 knockbackDirection) // Removed baseKnockbackForce parameter
    {
        float actualDamage = damageAmount;
        if (_buffManager != null)
        {
            actualDamage *= _buffManager.DamageTakenMultiplier.Value;
        }
        currentDamagePercentage += actualDamage;

        // Calculate knockout probability based on damage percentage
        float knockoutChance = knockoutProbabilityBase + (currentDamagePercentage * knockoutProbabilityPerDamagePercent);
        knockoutChance = Mathf.Clamp01(knockoutChance); // Ensure probability is between 0 and 1

        // Determine force based on probability and set final direction
        float forceToApply;
        Vector2 finalKnockbackDirection = knockbackDirection.normalized; // Default to the attack's direction
        float randomRoll = Random.Range(0f, 1f);

        if (randomRoll < knockoutChance)
        {
            forceToApply = criticalKnockoutForce;
            // CRITICAL KNOCKOUT: Combine upward force with original direction
            finalKnockbackDirection = (Vector2.up * 0.5f + knockbackDirection.normalized * 0.5f).normalized;
            Debug.Log($"{gameObject.name} CRITICAL KNOCKOUT! Direction: {finalKnockbackDirection} (Damage: {currentDamagePercentage:F1}%, Chance: {knockoutChance:P1})");
            // TODO: Add visual/audio effects for critical knockout
        }
        else
        {
            forceToApply = standardKnockbackForce;
            // finalKnockbackDirection remains the original knockbackDirection.normalized
            Debug.Log($"{gameObject.name} standard knockback. Direction: {finalKnockbackDirection} (Damage: {currentDamagePercentage:F1}%, Chance for KO was: {knockoutChance:P1})");
        }


        if (rb != null)
        {
            StartCoroutine(PerformKnockbackSequence(finalKnockbackDirection, forceToApply));
        }
    }

    // Renamed and updated knockback sequence
    private IEnumerator PerformKnockbackSequence(Vector2 direction, float force)
    {
        isBeingKnockedBack = true;
        rb.linearDamping = normalLinearDamping; // Crucial: Use normal damping during knockback

        rb.linearVelocity = Vector2.zero; // Clear current velocity
        rb.AddForce(direction * force, ForceMode2D.Impulse);

        yield return new WaitForSeconds(0.3f); // Knockback duration, tune this

        isBeingKnockedBack = false;

        // After knockback, check if still in contact to reapply collision damping if needed
        if (inContactWithOtherCharacter)
        {
             rb.linearDamping = collisionLinearDamping;
        }
        else
        {
            rb.linearDamping = normalLinearDamping;
        }
    }

    // New method for Blast Zone interaction
    public void EnteredBlastZone()
    {
        stocks--;
        Debug.Log($"{gameObject.name} knocked out! Stocks remaining: {stocks}");

        if (gameManager != null)
        {
            if (stocks > 0)
            {
                // Request respawn from the server
                if (IsOwner) // Only the owner client should request respawn for its own character
                {
                    RequestRespawnServerRpc();
                }
                // The object will be set inactive by the server via the ServerRpc,
                // or by the client via the ClientRpc if it's the owner.
            }
            else
            {
                gameManager.EntityDefeated(this);
                // GameManager will handle deactivation or other game over logic
            }
        }
        else // No GameManager, just deactivate (for non-networked scenarios or testing)
        {
            gameObject.SetActive(false);
        }
    }

    [ServerRpc(RequireOwnership = true)]
    void RequestRespawnServerRpc(ServerRpcParams rpcParams = default)
    {
        // This code runs ONLY on the SERVER
        if (GameManager.Instance != null)
        {
            Vector3 respawnPos = GameManager.Instance.GetRandomRespawnPoint(); // Server picks the point

            // Deactivate on server first, so it's hidden for all clients
            gameObject.SetActive(false);

            // Tell the specific client (owner of this NetworkObject) to respawn.
            RespawnClientRpc(respawnPos);
        }
    }

    [ClientRpc]
    void RespawnClientRpc(Vector3 respawnPosition, ClientRpcParams clientRpcParams = default)
    {
        // This code runs on the client that owns this Health component
        // The object should already be inactive due to the server call.
        RespawnAt(respawnPosition); // Use the existing method to move and reset state
    }

    // New method for respawning (called by GameManager or RespawnClientRpc)
    public void RespawnAt(Vector3 position)
    {
        transform.position = position;
        currentDamagePercentage = 0f;
        rb.linearVelocity = Vector2.zero;
        isBeingKnockedBack = false;
        
        inContactWithOtherCharacter = false; // Reset contact state
        rb.linearDamping = normalLinearDamping;
        gameObject.SetActive(true);

        PlayerMovement playerMovement = GetComponent<PlayerMovement>();
        if (playerMovement != null)
        {
            playerMovement.ResetMovementStates();
        }

        PlayerAttack playerAttack = GetComponent<PlayerAttack>();
        if (playerAttack != null)
        {
            playerAttack.ResetAttackStates();
        }
    }

    // New method to reset state for a new game
    public void ResetStateForNewGame(int startingStocks, Vector3 initialPosition)
    {
        this.stocks = startingStocks;
        currentDamagePercentage = 0f;
        transform.position = initialPosition;
        rb.linearVelocity = Vector2.zero;
        isBeingKnockedBack = false;
        inContactWithOtherCharacter = false; // Reset contact state
        rb.linearDamping = normalLinearDamping;
        gameObject.SetActive(true);
    }

    // Collision Damping Logic (Refined)
    void OnCollisionEnter2D(Collision2D collision)
    {
        Health otherHealth = collision.gameObject.GetComponent<Health>();
        if (otherHealth != null) // Colliding with another character
        {
            inContactWithOtherCharacter = true;
            if (!isBeingKnockedBack)
            {
                rb.linearDamping = collisionLinearDamping;
            }
        }
        // Original Player tag check is removed as all relevant entities should have Health.cs
    }

    void OnCollisionStay2D(Collision2D collision)
    {
        Health otherHealth = collision.gameObject.GetComponent<Health>();
        if (otherHealth != null) // Colliding with another character
        {
            inContactWithOtherCharacter = true; // Ensure flag is set
            if (!isBeingKnockedBack)
            {
                rb.linearDamping = collisionLinearDamping;
            }
        }
        // Original Player tag check is removed
    }

    void OnCollisionExit2D(Collision2D collision)
    {
        Health otherHealth = collision.gameObject.GetComponent<Health>();
        if (otherHealth != null) // No longer colliding with another character
        {
            inContactWithOtherCharacter = false;
            if (!isBeingKnockedBack) // Only reset if not currently being knocked back
            {
                rb.linearDamping = normalLinearDamping;
            }
        }
        // Original Player tag check is removed
    }
}
