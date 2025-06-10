using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine;

public class LobbyManager : MonoBehaviour
{
    public static LobbyManager Instance { get; private set; }

    public event Action<List<Lobby>> OnLobbyListChanged;
    public event Action<Lobby> OnJoinedLobby;
    public event Action OnLeftLobby;
    public event Action OnKickedFromLobby;
    public event Action<Lobby> OnLobbyUpdated;
    public event Action<Lobby> OnGameStarted;

    private Lobby _joinedLobby;
    private float _heartbeatTimer;
    private float _lobbyPollTimer;
    private bool _isGameStartingOrStarted = false;

    private const float HEARTBEAT_INTERVAL = 15f; // Lobby heartbeat every 15 seconds
    private const float LOBBY_POLL_INTERVAL = 1.1f; // Poll lobby updates every 1.1 seconds

    public Lobby JoinedLobby => _joinedLobby;
    public string PlayerId => AuthenticationService.Instance.PlayerId;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
    }

    private void Update()
    {
        if (_joinedLobby != null)
        {
            HandleLobbyHeartbeat();
            HandleLobbyPolling();
        }
    }

    private async void HandleLobbyHeartbeat()
    {
        if (_joinedLobby.HostId == PlayerId)
        {
            _heartbeatTimer -= Time.deltaTime;
            if (_heartbeatTimer <= 0f)
            {
                _heartbeatTimer = HEARTBEAT_INTERVAL;
                await LobbyService.Instance.SendHeartbeatPingAsync(_joinedLobby.Id);
                Debug.Log("Lobby Heartbeat Sent.");
            }
        }
    }

    private async void HandleLobbyPolling()
    {
        _lobbyPollTimer -= Time.deltaTime;
        if (_lobbyPollTimer <= 0f)
        {
            _lobbyPollTimer = LOBBY_POLL_INTERVAL;
            try
            {
                Lobby lobby = await LobbyService.Instance.GetLobbyAsync(_joinedLobby.Id);
                _joinedLobby = lobby;
                OnLobbyUpdated?.Invoke(_joinedLobby);

                // Debugging: Log current GameState and _isGameStartingOrStarted flag
                string currentGameState = "N/A";
                if (_joinedLobby.Data != null && _joinedLobby.Data.TryGetValue("GameState", out DataObject debugGameStateData))
                {
                    currentGameState = debugGameStateData.Value;
                }
                Debug.Log($"Polling Lobby: '{_joinedLobby.Name}' (ID: {_joinedLobby.Id}). Current GameState: '{currentGameState}', IsGameStartingOrStarted: {_isGameStartingOrStarted}");


                // Check for game start signal based on GameState
                if (!_isGameStartingOrStarted && // Only trigger once
                    _joinedLobby.Data != null &&
                    _joinedLobby.Data.TryGetValue("GameState", out DataObject gameStateData) &&
                    gameStateData.Value == "Starting" && // Or "InProgress"
                    _joinedLobby.Data.TryGetValue("RelayJoinCode", out DataObject relayJoinCodeData) &&
                    !string.IsNullOrEmpty(relayJoinCodeData.Value))
                {
                    Debug.Log($"Game starting via polling! GameState: {gameStateData.Value}, Relay Join Code: {relayJoinCodeData.Value}");
                    _isGameStartingOrStarted = true; // Set flag to prevent re-triggering
                    OnGameStarted?.Invoke(_joinedLobby);
                }
            }
            catch (LobbyServiceException e)
            {
                if (e.Reason == LobbyExceptionReason.LobbyNotFound || e.Reason == LobbyExceptionReason.LobbyConflict)
                {
                    Debug.LogWarning("Lobby no longer exists or conflict detected. Leaving lobby.");
                    _joinedLobby = null;
                    _isGameStartingOrStarted = false;
                    OnKickedFromLobby?.Invoke();
                }
                else
                {
                    Debug.LogError($"Error polling lobby: {e.Message}");
                }
            }
        }
    }

    public async Task<Lobby> CreateLobby(string lobbyName, int maxPlayers, bool isPrivate)
    {
        if (!UnityServicesInitializer.IsInitialized)
        {
            Debug.LogError("Unity Services not initialized. Cannot create lobby.");
            return null;
        }

        try
        {
            CreateLobbyOptions options = new CreateLobbyOptions
            {
                IsPrivate = isPrivate,
                Player = GetPlayer(),
                Data = new Dictionary<string, DataObject>
                {
                    // RelayJoinCode will be created just before game start
                    { "GameState", new DataObject(DataObject.VisibilityOptions.Public, "CharacterSelect") } // Initial game state
                }
            };

            Lobby lobby = await LobbyService.Instance.CreateLobbyAsync(lobbyName, maxPlayers, options);
            _joinedLobby = lobby;
            string createdGameState = "N/A";
            if (_joinedLobby.Data != null && _joinedLobby.Data.TryGetValue("GameState", out DataObject createdGameStateData))
            {
                createdGameState = createdGameStateData.Value;
            }
            Debug.Log($"Created Lobby: {lobby.Name} ({lobby.Id}) with code {lobby.LobbyCode}. Initial GameState: {createdGameState}");

            _isGameStartingOrStarted = false;
            OnJoinedLobby?.Invoke(lobby);
            return lobby;
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError($"Failed to create lobby: {e.Message}");
            return null;
        }
    }

    public async Task<List<Lobby>> ListLobbies()
    {
        if (!UnityServicesInitializer.IsInitialized)
        {
            Debug.LogError("Unity Services not initialized. Cannot list lobbies.");
            return new List<Lobby>();
        }

        try
        {
            QueryLobbiesOptions options = new QueryLobbiesOptions
            {
                Count = 25, // Max lobbies to return
                Filters = new List<QueryFilter>
                {
                    new QueryFilter(QueryFilter.FieldOptions.AvailableSlots, "0", QueryFilter.OpOptions.GT)
                },
                Order = new List<QueryOrder>
                {
                    new QueryOrder(false, QueryOrder.FieldOptions.Created)
                }
            };

            QueryResponse response = await LobbyService.Instance.QueryLobbiesAsync(options);
            Debug.Log($"Found {response.Results.Count} lobbies.");
            OnLobbyListChanged?.Invoke(response.Results);
            return response.Results;
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError($"Failed to list lobbies: {e.Message}");
            return new List<Lobby>();
        }
    }

    public async Task<Lobby> JoinLobbyByCode(string lobbyCode)
    {
        if (!UnityServicesInitializer.IsInitialized)
        {
            Debug.LogError("Unity Services not initialized. Cannot join lobby.");
            return null;
        }

        try
        {
            JoinLobbyByCodeOptions options = new JoinLobbyByCodeOptions
            {
                Player = GetPlayer()
            };

            Lobby lobby = await LobbyService.Instance.JoinLobbyByCodeAsync(lobbyCode, options);
            _joinedLobby = lobby;
            string joinedGameState = "N/A";
            if (_joinedLobby.Data != null && _joinedLobby.Data.TryGetValue("GameState", out DataObject joinedGameStateData))
            {
                joinedGameState = joinedGameStateData.Value;
            }
            Debug.Log($"Joined Lobby: {lobby.Name} ({lobby.Id}). Current GameState: {joinedGameState}");

            _isGameStartingOrStarted = false;
            OnJoinedLobby?.Invoke(lobby);
            return lobby;
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError($"Failed to join lobby by code: {e.Message}");
            return null;
        }
    }

    public async Task<Lobby> JoinLobbyById(string lobbyId)
    {
        if (!UnityServicesInitializer.IsInitialized)
        {
            Debug.LogError("Unity Services not initialized. Cannot join lobby.");
            return null;
        }

        try
        {
            JoinLobbyByIdOptions options = new JoinLobbyByIdOptions
            {
                Player = GetPlayer()
            };

            Lobby lobby = await LobbyService.Instance.JoinLobbyByIdAsync(lobbyId, options);
            _joinedLobby = lobby;
            string joinedGameStateById = "N/A";
            if (_joinedLobby.Data != null && _joinedLobby.Data.TryGetValue("GameState", out DataObject joinedGameStateByIdData))
            {
                joinedGameStateById = joinedGameStateByIdData.Value;
            }
            Debug.Log($"Joined Lobby: {lobby.Name} ({lobby.Id}). Current GameState: {joinedGameStateById}");

            _isGameStartingOrStarted = false;
            OnJoinedLobby?.Invoke(lobby);
            return lobby;
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError($"Failed to join lobby by ID: {e.Message}");
            return null;
        }
    }

    public async Task LeaveLobby()
    {
        if (_joinedLobby == null) return;

        try
        {
            await LobbyService.Instance.RemovePlayerAsync(_joinedLobby.Id, PlayerId);
            Debug.Log($"Left lobby: {_joinedLobby.Name}");
            _joinedLobby = null;
            _isGameStartingOrStarted = false;
            OnLeftLobby?.Invoke();
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError($"Failed to leave lobby: {e.Message}");
        }
    }

    public async Task KickPlayer(string playerId)
    {
        if (_joinedLobby == null || _joinedLobby.HostId != PlayerId)
        {
            Debug.LogWarning("Only the host can kick players.");
            return;
        }

        try
        {
            await LobbyService.Instance.RemovePlayerAsync(_joinedLobby.Id, playerId);
            Debug.Log($"Kicked player {playerId} from lobby {_joinedLobby.Name}");
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError($"Failed to kick player: {e.Message}");
        }
    }

    public async Task StartGame()
    {
        if (_joinedLobby == null || _joinedLobby.HostId != PlayerId)
        {
            Debug.LogWarning("Only the host can start the game.");
            return;
        }

        // New Check:
        if (!AreAllPlayersReadyAndSelectedCharacter())
        {
            Debug.LogWarning("Cannot start game: Not all players are ready or have selected a character.");
            return;
        }

        try
        {
            // HOST: Create/Re-create Relay Allocation NOW
            string newRelayJoinCode = await RelayManager.Instance.CreateRelayAllocation(_joinedLobby.MaxPlayers);
            if (string.IsNullOrEmpty(newRelayJoinCode))
            {
                Debug.LogError("Failed to create new Relay allocation before starting game.");
                // Potentially inform UI
                return;
            }

            Debug.Log($"Host created new Relay Join Code: {newRelayJoinCode}");

            UpdateLobbyOptions options = new UpdateLobbyOptions
            {
                Data = new Dictionary<string, DataObject>
                {
                    { "GameState", new DataObject(DataObject.VisibilityOptions.Public, "Starting") },
                    { "RelayJoinCode", new DataObject(DataObject.VisibilityOptions.Member, newRelayJoinCode) } // Update with fresh code
                }
            };
            _joinedLobby = await LobbyService.Instance.UpdateLobbyAsync(_joinedLobby.Id, options);
            Debug.Log("Lobby updated with new RelayJoinCode and GameState to 'Starting'.");
            // Polling will now pick this up and trigger OnGameStarted for everyone.
        }
        catch (Exception e) // Catch RelayServiceException and LobbyServiceException
        {
            Debug.LogError($"Failed to start game (Relay or Lobby update): {e.Message}");
        }
    }

    private Player GetPlayer()
    {
        return new Player(AuthenticationService.Instance.PlayerId, null, new Dictionary<string, PlayerDataObject>
        {
            { "PlayerName", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, "Player" + UnityEngine.Random.Range(100, 999)) }, // Example player name
            { "IsReady", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, "false") },
            { "SelectedCharacter", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, "") }
        });
    }

    public async Task SetPlayerReadyStatus(bool isReady)
    {
        if (_joinedLobby == null || string.IsNullOrEmpty(PlayerId)) return;

        try
        {
            UpdatePlayerOptions options = new UpdatePlayerOptions
            {
                Data = new Dictionary<string, PlayerDataObject>
                {
                    { "IsReady", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, isReady.ToString().ToLower()) }
                }
            };
            _joinedLobby = await LobbyService.Instance.UpdatePlayerAsync(_joinedLobby.Id, PlayerId, options);
            OnLobbyUpdated?.Invoke(_joinedLobby); // Notify UI to refresh
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError($"Failed to update player ready status: {e.Message}");
        }
    }

    public async Task SetPlayerCharacter(string characterId)
    {
        if (_joinedLobby == null || string.IsNullOrEmpty(PlayerId)) return;

        try
        {
            UpdatePlayerOptions options = new UpdatePlayerOptions
            {
                Data = new Dictionary<string, PlayerDataObject>
                {
                    { "SelectedCharacter", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, characterId) }
                }
            };
            _joinedLobby = await LobbyService.Instance.UpdatePlayerAsync(_joinedLobby.Id, PlayerId, options);
            OnLobbyUpdated?.Invoke(_joinedLobby); // Notify UI to refresh
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError($"Failed to update player character: {e.Message}");
        }
    }

    public bool AreAllPlayersReadyAndSelectedCharacter()
    {
        if (_joinedLobby == null) return false;

        foreach (Player player in _joinedLobby.Players)
        {
            if (!player.Data.TryGetValue("IsReady", out PlayerDataObject isReadyData) || 
                isReadyData.Value.ToLower() != "true")
            {
                return false; // A player is not ready
            }
            if (!player.Data.TryGetValue("SelectedCharacter", out PlayerDataObject selectedCharacterData) || 
                string.IsNullOrEmpty(selectedCharacterData.Value))
            {
                return false; // A player has not selected a character
            }
        }
        return true; // All players are ready and have selected a character
    }
}
