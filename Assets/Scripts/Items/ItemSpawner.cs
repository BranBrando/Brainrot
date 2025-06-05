using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;
// ItemEffectSO is now in the global namespace

public class ItemSpawner : NetworkBehaviour
{
    [Header("Spawn Settings")]
    [Tooltip("List of ItemEffectSOs to choose from for spawning.")]
    public List<ItemEffectSO> availableItems;

    [Tooltip("Minimum time between item spawns.")]
    public float minSpawnInterval = 5f;
    [Tooltip("Maximum time between item spawns.")]
    public float maxSpawnInterval = 15f;

    [Tooltip("Maximum number of items allowed in the scene at once.")]
    public int maxConcurrentItems = 5;

    [Header("Spawn Area (Rectangular)")]
    [Tooltip("The center point of the rectangular spawn area.")]
    public Vector2 spawnAreaCenter;
    [Tooltip("The size (width, height) of the rectangular spawn area.")]
    public Vector2 spawnAreaSize = new Vector2(10f, 5f);

    private float _spawnTimer;
    private List<NetworkObject> _spawnedItems = new List<NetworkObject>();

    public static ItemSpawner Instance { get; private set; } // Singleton pattern

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer)
        {
            enabled = false; // Only server runs the spawning logic
            return;
        }

        _spawnTimer = Random.Range(minSpawnInterval, maxSpawnInterval);
        // Clean up any existing items if this is a fresh server spawn
        CleanupExistingItems();
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer)
        {
            CleanupExistingItems();
        }
    }

    void Update()
    {
        if (!IsServer) return; // Only server handles spawning

        _spawnTimer -= Time.deltaTime;
        if (_spawnTimer <= 0f)
        {
            TrySpawnItem();
            _spawnTimer = Random.Range(minSpawnInterval, maxSpawnInterval);
        }

        // Remove despawned items from our tracking list
        _spawnedItems.RemoveAll(item => item == null || !item.IsSpawned);
    }

    private void TrySpawnItem()
    {
        if (_spawnedItems.Count >= maxConcurrentItems)
        {
            Debug.Log("Max concurrent items reached. Skipping spawn.");
            return;
        }

        if (availableItems == null || availableItems.Count == 0)
        {
            Debug.LogWarning("No items configured for spawning in ItemSpawner.");
            return;
        }

        ItemEffectSO itemToSpawn = availableItems[Random.Range(0, availableItems.Count)];
        if (itemToSpawn.itemPickupWorldPrefab == null)
        {
            Debug.LogWarning($"ItemEffectSO '{itemToSpawn.name}' has no itemPickupWorldPrefab assigned. Skipping spawn.");
            return;
        }

        Vector3 spawnPosition = GetRandomSpawnPosition();
        
        // Instantiate the prefab
        GameObject spawnedGameObject = Instantiate(itemToSpawn.itemPickupWorldPrefab, spawnPosition, Quaternion.identity);
        
        // Get the NetworkObject component
        NetworkObject networkObject = spawnedGameObject.GetComponent<NetworkObject>();
        if (networkObject != null)
        {
            // Spawn it over the network
            networkObject.Spawn();
            _spawnedItems.Add(networkObject);
            Debug.Log($"Server: Spawned {itemToSpawn.name} at {spawnPosition}. Total items: {_spawnedItems.Count}");
        }
        else
        {
            Debug.LogError($"Prefab for {itemToSpawn.name} does not have a NetworkObject component! Cannot spawn over network. Destroying local instance.", spawnedGameObject);
            Destroy(spawnedGameObject);
        }
    }

    private Vector3 GetRandomSpawnPosition()
    {
        float randomX = Random.Range(spawnAreaCenter.x - spawnAreaSize.x / 2, spawnAreaCenter.x + spawnAreaSize.x / 2);
        float randomY = Random.Range(spawnAreaCenter.y - spawnAreaSize.y / 2, spawnAreaCenter.y + spawnAreaSize.y / 2);
        return new Vector3(randomX, randomY, 0f); // Assuming 2D game, Z is 0
    }

    private void CleanupExistingItems()
    {
        // Find all NetworkObjects with ItemPickup component and despawn them
        ItemPickup[] existingPickups = FindObjectsByType<ItemPickup>(FindObjectsSortMode.None);
        foreach (ItemPickup pickup in existingPickups)
        {
            if (pickup.IsSpawned) // Ensure it's a networked object
            {
                pickup.GetComponent<NetworkObject>().Despawn();
            }
            else
            {
                Destroy(pickup.gameObject); // Destroy non-networked ones if any
            }
        }
        _spawnedItems.Clear(); // Clear our tracking list
        Debug.Log("Server: Cleaned up existing items.");
    }

    // Draw the spawn area in the editor
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(spawnAreaCenter, spawnAreaSize);
    }
}
