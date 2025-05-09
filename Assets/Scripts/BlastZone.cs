using UnityEngine;

public class BlastZone : MonoBehaviour
{
    void OnTriggerEnter2D(Collider2D other)
    {
        Health health = other.GetComponent<Health>();
        if (health != null)
        {
            health.EnteredBlastZone();
        }
    }
}
