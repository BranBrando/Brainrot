using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;
using System.Collections;
// Removed namespace Brainrot.Player
public class PlayerBuffManager : NetworkBehaviour
{
    // References to other player components
    private PlayerMovement _playerMovement;
    private Health _playerHealth;
    private PlayerAttack _playerAttack;
    private Transform _playerTransform;
    private Rigidbody2D _rb;

    // --- Networked Multipliers ---
    public NetworkVariable<float> SpeedMultiplier { get; } = new NetworkVariable<float>(1f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<float> JumpForceMultiplier { get; } = new NetworkVariable<float>(1f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<float> DashSpeedMultiplier { get; } = new NetworkVariable<float>(1f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<float> DashDurationMultiplier { get; } = new NetworkVariable<float>(1f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<Vector3> ScaleMultiplier { get; } = new NetworkVariable<Vector3>(Vector3.one, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private Vector3 _originalScale;
    public NetworkVariable<float> DamageOutputMultiplier { get; } = new NetworkVariable<float>(1f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<float> DamageTakenMultiplier { get; } = new NetworkVariable<float>(1f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // --- Active Buff Management ---
    private class ActiveBuff
    {
        public ItemEffectSO.EffectType Type { get; }
        public float Magnitude { get; }
        public float OriginalDuration { get; } // To store the initial duration for reset
        public float RemainingTime { get; set; } // Can be used for UI or other timed logic
        public Coroutine ExpiryCoroutine { get; set; }

        public ActiveBuff(ItemEffectSO.EffectType type, float magnitude, float duration, float remainingTime)
        {
            Type = type;
            Magnitude = magnitude;
            OriginalDuration = duration; // Store the original duration
            RemainingTime = remainingTime;
        }
    }
    private readonly List<ActiveBuff> _activeBuffsList = new List<ActiveBuff>();

    public override void OnNetworkSpawn()
    {
        _playerMovement = GetComponent<PlayerMovement>();
        _playerHealth = GetComponent<Health>();
        _playerAttack = GetComponent<PlayerAttack>();
        _playerTransform = transform;
        _rb = GetComponent<Rigidbody2D>();

        if (IsClient)
        {
            ScaleMultiplier.OnValueChanged += OnScaleMultiplierChangedClient;
        }

        if (IsOwner)
        {
             // Store the absolute original scale, ignoring current facing direction for this base.
             _originalScale = new Vector3(Mathf.Abs(_playerTransform.localScale.x), Mathf.Abs(_playerTransform.localScale.y), Mathf.Abs(_playerTransform.localScale.z));
        }
        // For remote clients, _originalScale might need to be networked if they also lerp scale
        // or if their initial scale can vary and needs to be preserved.
    }

    public override void OnNetworkDespawn()
    {
        if (IsClient)
        {
            ScaleMultiplier.OnValueChanged -= OnScaleMultiplierChangedClient;
        }
        if (IsServer)
        {
            foreach (var buff in _activeBuffsList)
            {
                if (buff.ExpiryCoroutine != null) StopCoroutine(buff.ExpiryCoroutine);
            }
            _activeBuffsList.Clear();
        }
    }
    
    private void Update()
    {
        if (IsOwner && _playerTransform != null) // Ensure _playerTransform is not null
        {
            if (_originalScale != Vector3.zero) // Ensure _originalScale has been initialized
            {
                // Determine the target scale magnitude based on the original scale and the multiplier
                Vector3 targetMagnifiedScale = new Vector3(
                    _originalScale.x * ScaleMultiplier.Value.x,
                    _originalScale.y * ScaleMultiplier.Value.y,
                    _originalScale.z * ScaleMultiplier.Value.z
                );

                // Preserve the current facing direction (sign of localScale.x)
                float currentFacingDirection = Mathf.Sign(_playerTransform.localScale.x);
                if (currentFacingDirection == 0) currentFacingDirection = 1; // Avoid zero scale

                Vector3 newScale = new Vector3(
                    targetMagnifiedScale.x * currentFacingDirection, // Apply facing direction to X
                    targetMagnifiedScale.y,
                    targetMagnifiedScale.z
                );

                _playerTransform.localScale = Vector3.Lerp(_playerTransform.localScale, newScale, Time.deltaTime * 10f);
            }
            // If _originalScale is zero (should not happen if OnNetworkSpawn runs correctly for owner),
            // we might need a fallback, but ideally, _originalScale is always valid for the owner.
        }
    }

    private void OnScaleMultiplierChangedClient(Vector3 previousValue, Vector3 newValue)
    {
        // Called on ALL clients when ScaleMultiplier changes.
        // Owner handles its scale in Update for smoothness.
        // Remote clients can apply scale directly here, also preserving their current facing direction.
        if (!IsOwner && _playerTransform != null)
        {
            float currentFacingDirection = Mathf.Sign(_playerTransform.localScale.x);
            if (currentFacingDirection == 0) currentFacingDirection = 1;

            // newValue is the direct target multiplier (e.g., (1.5, 1.5, 1.5) for 50% bigger)
            // We assume a base scale of (1,1,1) for remote clients for this multiplier's application.
            // A more robust solution might involve syncing _originalScale if it can vary per player significantly at spawn.
            Vector3 targetMagnifiedScale = new Vector3(
                1f * newValue.x, 
                1f * newValue.y,
                1f * newValue.z
            );
            
            Vector3 newScale = new Vector3(
                targetMagnifiedScale.x * currentFacingDirection,
                targetMagnifiedScale.y,
                targetMagnifiedScale.z
            );
            _playerTransform.localScale = newScale; // Apply directly for remote clients
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void ApplyEffectServerRpc(ItemEffectSO.EffectType effectType, float magnitude, float duration, ServerRpcParams rpcParams = default)
    {
        if (!IsServer) return;

        Debug.Log($"Server (PlayerBuffManager on {OwnerClientId}): Applying effect {effectType} with mag {magnitude}, dur {duration}");

        ActiveBuff existingBuff = _activeBuffsList.Find(b => b.Type == effectType);
        if (existingBuff != null)
        {
            if (existingBuff.ExpiryCoroutine != null) StopCoroutine(existingBuff.ExpiryCoroutine);
            ResetEffectValues(existingBuff.Type, existingBuff.Magnitude);
            _activeBuffsList.Remove(existingBuff);
        }
        
        ActiveBuff newBuff = new ActiveBuff(effectType, magnitude, duration, duration);

        switch (effectType)
        {
            case ItemEffectSO.EffectType.Speed: SpeedMultiplier.Value *= magnitude; break;
            case ItemEffectSO.EffectType.JumpForce: JumpForceMultiplier.Value *= magnitude; break;
            case ItemEffectSO.EffectType.DashSpeed: DashSpeedMultiplier.Value *= magnitude; break;
            case ItemEffectSO.EffectType.DashDuration: DashDurationMultiplier.Value *= magnitude; break;
            case ItemEffectSO.EffectType.Size: 
                // Magnitude is the target scale factor (e.g., 1.5 for 50% bigger, 0.5 for 50% smaller)
                ScaleMultiplier.Value = new Vector3(magnitude, magnitude, magnitude); 
                break;
            case ItemEffectSO.EffectType.DamageOutput: DamageOutputMultiplier.Value *= magnitude; break;
            case ItemEffectSO.EffectType.DamageTaken: DamageTakenMultiplier.Value *= magnitude; break;
        }

        if (duration > 0)
        {
            newBuff.ExpiryCoroutine = StartCoroutine(TimedEffectRemovalCoroutine(newBuff));
            _activeBuffsList.Add(newBuff);
        }
    }

    private IEnumerator TimedEffectRemovalCoroutine(ActiveBuff buffToExpire)
    {
        yield return new WaitForSeconds(buffToExpire.OriginalDuration);

        if (_activeBuffsList.Contains(buffToExpire))
        {
            ResetEffectValues(buffToExpire.Type, buffToExpire.Magnitude);
            _activeBuffsList.Remove(buffToExpire);
            Debug.Log($"Server (PlayerBuffManager on {OwnerClientId}): Timed effect {buffToExpire.Type} expired.");
            SanitizeMultipliers();
        }
    }

    private void ResetEffectValues(ItemEffectSO.EffectType effectType, float magnitude)
    {
        if (!IsServer) return;

        switch (effectType)
        {
            case ItemEffectSO.EffectType.Speed: SpeedMultiplier.Value /= magnitude; break;
            case ItemEffectSO.EffectType.JumpForce: JumpForceMultiplier.Value /= magnitude; break;
            case ItemEffectSO.EffectType.DashSpeed: DashSpeedMultiplier.Value /= magnitude; break;
            case ItemEffectSO.EffectType.DashDuration: DashDurationMultiplier.Value /= magnitude; break;
            case ItemEffectSO.EffectType.Size: 
                // Reset scale multiplier to 1 (original size)
                ScaleMultiplier.Value = Vector3.one; 
                break;
            case ItemEffectSO.EffectType.DamageOutput: DamageOutputMultiplier.Value /= magnitude; break;
            case ItemEffectSO.EffectType.DamageTaken: DamageTakenMultiplier.Value /= magnitude; break;
        }
    }

    private void SanitizeMultipliers()
    {
        if (!IsServer) return;
        if (Mathf.Approximately(SpeedMultiplier.Value, 1f)) SpeedMultiplier.Value = 1f;
        if (Mathf.Approximately(JumpForceMultiplier.Value, 1f)) JumpForceMultiplier.Value = 1f;
        if (Mathf.Approximately(DashSpeedMultiplier.Value, 1f)) DashSpeedMultiplier.Value = 1f;
        if (Mathf.Approximately(DashDurationMultiplier.Value, 1f)) DashDurationMultiplier.Value = 1f;
        if (Mathf.Approximately(DamageOutputMultiplier.Value, 1f)) DamageOutputMultiplier.Value = 1f;
        if (Mathf.Approximately(DamageTakenMultiplier.Value, 1f)) DamageTakenMultiplier.Value = 1f;

        if (Mathf.Approximately(ScaleMultiplier.Value.x, 1f) &&
            Mathf.Approximately(ScaleMultiplier.Value.y, 1f) &&
            Mathf.Approximately(ScaleMultiplier.Value.z, 1f))
        {
            ScaleMultiplier.Value = Vector3.one;
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void RemoveBuffByTypeServerRpc(ItemEffectSO.EffectType effectTypeToRemove)
    {
        if (!IsServer) return;

        ActiveBuff buffToRemove = _activeBuffsList.Find(b => b.Type == effectTypeToRemove);
        if (buffToRemove != null)
        {
            if (buffToRemove.ExpiryCoroutine != null) StopCoroutine(buffToRemove.ExpiryCoroutine);
            ResetEffectValues(buffToRemove.Type, buffToRemove.Magnitude);
            _activeBuffsList.Remove(buffToRemove);
            Debug.Log($"Server (PlayerBuffManager on {OwnerClientId}): Removed buff {effectTypeToRemove} by RPC.");
            SanitizeMultipliers();
        }
    }
    
    [ServerRpc(RequireOwnership = false)]
    public void ClearAllBuffsServerRpc()
    {
        if (!IsServer) return;
        foreach (var activeBuff in new List<ActiveBuff>(_activeBuffsList))
        {
             if (activeBuff.ExpiryCoroutine != null) StopCoroutine(activeBuff.ExpiryCoroutine);
             ResetEffectValues(activeBuff.Type, activeBuff.Magnitude);
        }
        _activeBuffsList.Clear();
        SpeedMultiplier.Value = 1f;
        JumpForceMultiplier.Value = 1f;
        DashSpeedMultiplier.Value = 1f;
        DashDurationMultiplier.Value = 1f;
        ScaleMultiplier.Value = Vector3.one;
        DamageOutputMultiplier.Value = 1f;
        DamageTakenMultiplier.Value = 1f;
        Debug.Log($"Server (PlayerBuffManager on {OwnerClientId}): Cleared all buffs by RPC.");
    }
}
