using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody2D))]
public class Health : MonoBehaviour
{
    private Rigidbody2D rb;
    private float originalLinearDamping;

    [Tooltip("Very high damping applied when colliding with player to prevent pushing.")]
    [SerializeField] private float playerCollisionLinearDamping = 50f; // Needs tuning!
    private bool isBeingKnockedBack = false; // State to allow knockback to ignore high damping

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            originalLinearDamping = rb.linearDamping;
        }
        // else Debug.LogError("Rigidbody2D not found on " + gameObject.name);
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player")) // Check if colliding with Player
        {
            if (rb != null && !isBeingKnockedBack)
            {
                rb.linearDamping = playerCollisionLinearDamping;
            }
        }
    }

    void OnCollisionStay2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            if (rb != null && !isBeingKnockedBack)
            {
                // Ensure damping stays high if continuously pushed
                rb.linearDamping = playerCollisionLinearDamping;
            }
            // If player is trying to move into enemy, enemy velocity will be highly damped.
        }
    }

    void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            if (rb != null)
            {
                rb.linearDamping = originalLinearDamping;
            }
        }
    }

    public void TakeKnockback(Vector2 direction, float force)
    {
        if (rb != null)
        {
            StartCoroutine(ApplyKnockbackSequence(direction, force));
        }
    }

    private IEnumerator ApplyKnockbackSequence(Vector2 direction, float force)
    {
        isBeingKnockedBack = true;
        rb.linearDamping = originalLinearDamping; // Ensure normal damping for knockback

        rb.linearVelocity = Vector2.zero; // Clear current velocity for a clean knockback
        rb.AddForce(direction * force, ForceMode2D.Impulse);

        // Wait for a short duration to let the knockback impulse take effect
        // and hopefully move the enemy out of collision with the player.
        yield return new WaitForSeconds(0.2f); // Adjust this delay as needed

        isBeingKnockedBack = false;

        // After knockback, if still colliding with player, OnCollisionStay will re-apply high damping.
        // If not colliding, OnCollisionExit should have already restored original damping.
        // We can add a check here to be sure:
        // Collider2D playerCollider = GetPlayerColliderIfStillTouching(); // You'd need a way to check this
        // if (playerCollider != null) rb.linearDamping = playerCollisionLinearDamping;
        // else rb.linearDamping = originalLinearDamping;
        // For now, let's rely on OnCollisionStay/Exit to handle it after isBeingKnockedBack is false.
    }
}
