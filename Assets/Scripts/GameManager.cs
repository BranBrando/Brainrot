using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq; // Required for OfType<T>()
using GameInterfaces; // Required for IHealth interface

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Game Settings")]
    public Transform[] respawnPoints; // Assign in Inspector
    public int defaultPlayerStartingStocks = 3;
    public int defaultAIStartingStocks = 1; // AI can have different stock counts
    public float respawnDelay = 2.0f;

    private List<IHealth> activeEntities = new List<IHealth>();

    void Awake()
    {
        // Singleton setup
        if (Instance == null)
        {
            Instance = this;
            // Optional: DontDestroyOnLoad(gameObject);
        }
        else
        {
            Debug.LogWarning("Duplicate GameManager found, destroying this one.");
            Destroy(gameObject);
        }
    }

    void Start()
    {
        // Find all entities with Health and register them
        SetupNewGame();
    }

    public void RegisterEntity(IHealth entityHealth)
    {
        if (!activeEntities.Contains(entityHealth))
        {
            activeEntities.Add(entityHealth);
            Debug.Log($"Registered entity: {entityHealth.gameObject.name}. Total active entities: {activeEntities.Count}");
        }
    }

    public void RequestRespawn(IHealth entityHealth)
    {
        if (activeEntities.Contains(entityHealth)) // Only respawn if still in the game
        {
            StartCoroutine(RespawnEntityCoroutine(entityHealth));
        }
    }

    private IEnumerator RespawnEntityCoroutine(IHealth entityHealth)
    {
        yield return new WaitForSeconds(respawnDelay);

        if (entityHealth != null) // Check if the entity still exists
        {
            Vector3 respawnPos = GetRandomRespawnPoint();
            entityHealth.RespawnAt(respawnPos);
            Debug.Log($"Respawning {entityHealth.gameObject.name} at {respawnPos}");
        }
    }

    public void EntityDefeated(IHealth entityHealth)
    {
        Debug.Log($"{entityHealth.gameObject.name} is out of the game!");
        if (activeEntities.Contains(entityHealth))
        {
            activeEntities.Remove(entityHealth);
        }
        entityHealth.gameObject.SetActive(false); // Deactivate the defeated entity
        CheckForWinCondition();
    }

    private void CheckForWinCondition()
    {
        int playersRemaining = 0;
        IHealth lastPlayer = null;

        // Count remaining players
        foreach (IHealth entity in activeEntities)
        {
            if (entity.isPlayerCharacter)
            {
                playersRemaining++;
                lastPlayer = entity;
            }
        }

        Debug.Log($"Players remaining: {playersRemaining}");

        if (playersRemaining <= 1)
        {
            // Game over or winner declared logic
            if (playersRemaining == 1)
            {
                Debug.Log($"Player {lastPlayer.gameObject.name} wins!");
                // TODO: Implement win screen or game end logic
            }
            else
            {
                Debug.Log("Draw or no players remaining.");
                // TODO: Implement draw or game end logic
            }
            // TODO: Potentially call a method to end the current match
        }
    }

    public Vector3 GetRandomRespawnPoint()
    {
        if (respawnPoints != null && respawnPoints.Length > 0)
        {
            int randomIndex = Random.Range(0, respawnPoints.Length);
            return respawnPoints[randomIndex].position;
        }
        else
        {
            Debug.LogWarning("No respawn points assigned in GameManager!");
            return Vector3.zero; // Default to origin if no points are set
        }
    }

    // Call this method to start a new game
    public void SetupNewGame()
    {
        activeEntities.Clear();
        // Find all MonoBehaviour components and filter for those that implement IHealth
        IHealth[] allEntities = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).OfType<IHealth>().ToArray();

        foreach (IHealth entity in allEntities)
        {
            // Reset state and register
            int stocks = entity.isPlayerCharacter ? defaultPlayerStartingStocks : defaultAIStartingStocks;
            Vector3 initialPosition = GetRandomRespawnPoint(); // Assign initial position
            entity.ResetStateForNewGame(stocks, initialPosition);
            RegisterEntity(entity);
        }
        Debug.Log("New game setup complete.");
        // TODO: Potentially add UI updates or other game start logic
    }
}
