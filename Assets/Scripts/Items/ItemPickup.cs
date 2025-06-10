using UnityEngine;
using Unity.Netcode;

public class ItemPickup : NetworkBehaviour
{
    [Header("Item Configuration")]
    [Tooltip("Assign the ScriptableObject that defines this item's effect.")]
    public ItemEffectSO itemEffect;

    [Header("Colliders")]
    [Tooltip("Assign the Collider2D component that should act as the trigger for player pickup. This will have 'Is Trigger' set to true.")]
    [SerializeField] private Collider2D pickupTriggerCollider; // Assign this in the Inspector

    // Rigidbody for physics (falling, resting on ground)
    private Rigidbody2D _rb; 

    [Header("Visuals (Optional)")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private GameObject visualMesh;

    public override void OnNetworkSpawn()
    {
        _rb = GetComponent<Rigidbody2D>(); // Get the Rigidbody2D
        if (_rb == null)
        {
            Debug.LogWarning($"ItemPickup on {gameObject.name} is missing a Rigidbody2D. It might not fall or interact with ground correctly.", this);
        }

        if (itemEffect == null)
        {
            Debug.LogError($"ItemPickup on {gameObject.name} has no ItemEffectSO assigned! Destroying.", this);
            if (IsServer) GetComponent<NetworkObject>()?.Despawn();
            return;
        }

        if (spriteRenderer != null && itemEffect.itemSprite != null)
        {
            spriteRenderer.sprite = itemEffect.itemSprite;
        }
        // ... (visualMesh logic can be added here if needed) ...

        if (pickupTriggerCollider == null)
        {
            Debug.LogError($"ItemPickup on {gameObject.name} requires a 'Pickup Trigger Collider' to be assigned in the Inspector.", this);
            if (IsServer) GetComponent<NetworkObject>()?.Despawn();
            return;
        }
        pickupTriggerCollider.isTrigger = true; // Ensure the designated pickup collider is a trigger
    }

    // OnTriggerEnter2D should now only be called if the pickupTriggerCollider is intersected
    private void OnTriggerEnter2D(Collider2D other) 
    {
        // This method will be called if *any* trigger collider on this GameObject is entered.
        // We rely on the fact that only 'pickupTriggerCollider' should be a trigger managed by this script's logic.
        // If 'other' is the collider that triggered this event, and it's our designated pickupTriggerCollider...
        // However, 'other' is the collider of the *other* object.
        // The event fires on this script if *any* of its trigger colliders are touched by 'other'.
        // So, we just proceed assuming if OnTriggerEnter2D fired, it's relevant for pickup.

        if (!IsServer) return;

        PlayerMovement player = other.GetComponent<PlayerMovement>();
        if (player != null)
        {
            PlayerBuffManager playerBuffManager = player.GetComponent<PlayerBuffManager>();
            if (playerBuffManager != null)
            {
                foreach (var effectData in itemEffect.effects)
                {
                    playerBuffManager.ApplyEffectServerRpc(
                        effectData.effectType,
                        effectData.magnitude,
                        effectData.duration
                    );
                }
                Debug.Log($"Server: Player {player.OwnerClientId} picked up {itemEffect.itemName}. Applied {itemEffect.effects.Count} effects.");
                GetComponent<NetworkObject>()?.Despawn();
            }
            else
            {
                Debug.LogWarning($"Player {player.OwnerClientId} collided with item, but has no PlayerBuffManager!", player);
            }
        }
    }

    void OnDrawGizmos()
    {
        if (pickupTriggerCollider != null && pickupTriggerCollider.isTrigger)
        {
            Gizmos.color = Color.green;
            // For BoxCollider2D
            if (pickupTriggerCollider is BoxCollider2D boxCollider)
            {
                Vector3 worldPos = transform.TransformPoint(boxCollider.offset);
                Gizmos.DrawWireCube(worldPos, Vector3.Scale(transform.lossyScale, boxCollider.size));
            }
            // For CircleCollider2D
            else if (pickupTriggerCollider is CircleCollider2D circleCollider)
            {
                Vector3 worldPos = transform.TransformPoint(circleCollider.offset);
                float avgScale = (transform.lossyScale.x + transform.lossyScale.y) / 2f; 
                Gizmos.DrawWireSphere(worldPos, circleCollider.radius * avgScale);
            }
        }
        // Example for drawing a non-trigger physical collider (if you had a reference to it)
        /*
        Collider2D physicalCollider = GetComponent<Collider2D>(); // Or a specific reference
        if (physicalCollider != null && !physicalCollider.isTrigger) {
            Gizmos.color = Color.blue;
            if (physicalCollider is BoxCollider2D box) {
                Vector3 worldPos = transform.TransformPoint(box.offset);
                Gizmos.DrawWireCube(worldPos, Vector3.Scale(transform.lossyScale, box.size));
            } else if (physicalCollider is CircleCollider2D circle) {
                Vector3 worldPos = transform.TransformPoint(circle.offset);
                float avgScale = (transform.lossyScale.x + transform.lossyScale.y) / 2f;
                Gizmos.DrawWireSphere(worldPos, circle.radius * avgScale);
            }
        }
        */
    }
}
