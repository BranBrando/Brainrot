using UnityEngine;

namespace GameInterfaces
{
    public interface IHealth
    {
        int stocks { get; set; }
        float currentDamagePercentage { get; set; }
        bool isPlayerCharacter { get; }
        GameObject gameObject { get; }
        Transform transform { get; }
        bool IsBeingKnockedBack();

        void ApplyDamageAndKnockback(float damageAmount, Vector2 knockbackDirection);
        void EnteredBlastZone();
        void RespawnAt(Vector3 position);
        void ResetStateForNewGame(int startingStocks, Vector3 initialPosition);
    }
}