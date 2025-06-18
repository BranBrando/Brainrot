using UnityEngine;
using GameInterfaces; // Added for IHealth interface and IPlayerAttack
public class BlastZone : MonoBehaviour
{
    void OnTriggerEnter2D(Collider2D other)
    {
        // Check if the other object has the IHealth interface
        IHealth health = other.GetComponent<IHealth>();
        health?.EnteredBlastZone();
    }
}
