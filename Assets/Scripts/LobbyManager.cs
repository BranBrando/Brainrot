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

                // Check for game start signal
                if (_joinedLobby.Data != null && _joinedLobby.Data.ContainsKey("RelayJoinCode"))
                {
                    string relayJoinCode = _joinedLobby.Data["RelayJoinCode"].Value;
                    if (!string.IsNullOrEmpty(relayJoinCode) && !_isGameStartingOrStarted)
                    {
                        Debug.Log($"Game started via polling! Relay Join Code: {relayJoinCode}");
                        _isGameStartingOrStarted = true;
                        OnGameStarted?.Invoke(_joinedLobby);
                    }
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
            // Create Relay allocation first
            string relayJoinCode = await RelayManager.Instance.CreateRelayAllocation(maxPlayers);
            if (string.IsNullOrEmpty(relayJoinCode))
            {
                Debug.LogError("Failed to create Relay allocation. Cannot create lobby.");
                return null;
            }

            CreateLobbyOptions options = new CreateLobbyOptions
            {
                IsPrivate = isPrivate,
                Player = GetPlayer(),
                Data = new Dictionary<string, DataObject>
                {
                    { "RelayJoinCode", new DataObject(DataObject.VisibilityOptions.Member, relayJoinCode) }
                }
            };

            Lobby lobby = await LobbyService.Instance.CreateLobbyAsync(lobbyName, maxPlayers, options);
            _joinedLobby = lobby;
            Debug.Log($"Created Lobby: {lobby.Name} ({lobby.Id}) with code {lobby.LobbyCode}. Relay Join Code: {relayJoinCode}");

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
            Debug.Log($"Joined Lobby: {lobby.Name} ({lobby.Id})");

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
            Debug.Log($"Joined Lobby: {lobby.Name} ({lobby.Id})");

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

        try
        {
            // Update lobby data to signal game start and include Relay join code
            string relayJoinCode = _joinedLobby.Data["RelayJoinCode"].Value;
            UpdateLobbyOptions options = new UpdateLobbyOptions
            {
                Data = new Dictionary<string, DataObject>
                {
                    { "RelayJoinCode", new DataObject(DataObject.VisibilityOptions.Member, relayJoinCode) }
                }
            };
            _joinedLobby = await LobbyService.Instance.UpdateLobbyAsync(_joinedLobby.Id, options);
            Debug.Log("Lobby updated to signal game start.");
            if (!_isGameStartingOrStarted)
            {
                _isGameStartingOrStarted = true;
                OnGameStarted?.Invoke(_joinedLobby);
                Debug.Log("OnGameStarted invoked from StartGame.");
            }
            else
            {
                Debug.Log("OnGameStarted already invoked or game is starting/started.");
            }
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError($"Failed to start game: {e.Message}");
        }
    }

    private Player GetPlayer()
    {
        return new Player(AuthenticationService.Instance.PlayerId, null, new Dictionary<string, PlayerDataObject>
        {
            { "PlayerName", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, "Player" + UnityEngine.Random.Range(100, 999)) } // Example player name
        });
    }
}
