using UnityEngine;

// namespace Brainrot.Items // Assuming your project's namespace, adjust if needed
// {
[CreateAssetMenu(fileName = "NewItemEffect", menuName = "Brainrot/Items/Item Effect")]
public class ItemEffectSO : ScriptableObject
{
    public enum EffectType
    {
        // PlayerMovement buffs/debuffs
            Speed,          // Affects PlayerMovement.moveSpeed
            JumpForce,      // Affects PlayerMovement.jumpForce
            DashSpeed,      // Affects PlayerMovement.dashSpeed
            DashDuration,   // Affects PlayerMovement.dashDuration

            // Transform buffs/debuffs
            Size,           // Affects player's transform.localScale

            // Health/Combat buffs/debuffs
            DamageOutput,   // Affects damage dealt by the player (via PlayerAttack.cs)
            DamageTaken     // Affects damage received by the player (via Health.cs)
            // Future ideas: HealthRegen, Invincibility, etc.
        }

    [Header("Effect Configuration")]
    public EffectType effectType;

        [Tooltip("Magnitude of the effect. E.g., for Speed: 1.5 = 50% faster, 0.8 = 20% slower. For Size: 1.5 = 50% bigger. For DamageOutput: 1.2 = 20% more damage.")]
        public float magnitude = 1f;

        [Tooltip("Duration of the effect in seconds. Use 0 for a permanent toggle (until counteracted or removed by another specific item).")]
        public float duration = 10f;

        [Header("Visuals & Pickup")]
        [Tooltip("Sprite to represent the item in UI or as a simple in-world visual if no complex prefab is used.")]
        public Sprite itemSprite; // For simple representation or UI

        [Tooltip("The prefab that will be spawned in the game world for players to pick up. This prefab should have the ItemPickup.cs script.")]
        public GameObject itemPickupWorldPrefab; // The actual game object to spawn

        [Header("Description (Optional)")]
        [Tooltip("Short description for UI or debugging.")]
        public string itemName = "New Item";
        [TextArea]
        public string description = "Applies an effect to the player.";
    }
// }
