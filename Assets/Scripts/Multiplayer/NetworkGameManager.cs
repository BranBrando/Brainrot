using UnityEngine;
using Unity.Netcode;
using UnityEngine.UI; // Assuming you'll use Unity UI
using System.Collections.Generic;
using Unity.Services.Lobbies.Models; // For Player and Lobby models
using Unity.Services.Authentication; // Required for AuthenticationService
using System.Text; // Required for Encoding

public class NetworkGameManager : MonoBehaviour
{
    public static NetworkGameManager Singleton { get; private set; }

    [Header("Player Prefabs by Character ID")]
    public List<CharacterPrefabMapping> characterPrefabs = new List<CharacterPrefabMapping>();

    [System.Serializable]
    public class CharacterPrefabMapping
    {
        public string characterId;
        public GameObject prefab;
    }

    // To store the mapping of Netcode's clientId to the player's Authentication ID
    private Dictionary<ulong, string> _clientIdToAuthId = new Dictionary<ulong, string>();

    private void Awake()
    {
        if (Singleton != null && Singleton != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Singleton = this;
            DontDestroyOnLoad(gameObject);
        }
    }

    void Start()
    {
        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("NetworkGameManager: NetworkManager.Singleton not found in the scene. Please add one.");
            return;
        }

        // Subscribe to connection events
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected; // Good practice to clean up

        // Set up connection approval
        if (NetworkManager.Singleton.IsServer) // Only server needs to approve connections
        {
            NetworkManager.Singleton.ConnectionApprovalCallback += ApprovalCheck;
        }
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
            if (NetworkManager.Singleton.IsServer)
            {
                NetworkManager.Singleton.ConnectionApprovalCallback -= ApprovalCheck;
            }
        }
    }

    // Connection Approval Callback
    private void ApprovalCheck(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        string authIdPayload = Encoding.UTF8.GetString(request.Payload);
        ulong clientId = request.ClientNetworkId;

        Debug.Log($"[Server ApprovalCheck] For Client ID: {clientId}, Received Auth ID Payload: '{authIdPayload}'");

        if (LobbyManager.Instance == null) {
            Debug.LogError($"[Server ApprovalCheck] Denying client {clientId}: LobbyManager.Instance is null.");
            response.Approved = false; response.Reason = "LobbyManager not ready."; return;
        }
        if (LobbyManager.Instance.JoinedLobby == null) {
            Debug.LogError($"[Server ApprovalCheck] Denying client {clientId}: LobbyManager.Instance.JoinedLobby is null.");
            response.Approved = false; response.Reason = "JoinedLobby not ready."; return;
        }
        Debug.Log($"[Server ApprovalCheck] For client {clientId}: LobbyManager and JoinedLobby OK. Lobby ID: {LobbyManager.Instance.JoinedLobby.Id}, Name: {LobbyManager.Instance.JoinedLobby.Name}");

        if (string.IsNullOrEmpty(authIdPayload))
        {
            Debug.LogError($"[Server ApprovalCheck] Denying client {clientId}: Auth ID payload is null or empty.");
            response.Approved = false;
            response.Reason = "Invalid Auth ID (empty).";
            return;
        }

        Debug.Log($"[Server ApprovalCheck] For client {clientId} (Auth ID: {authIdPayload}): Checking if player is in lobby...");
        bool playerInLobby = false;
        Player foundLobbyPlayer = null;
        string lobbyPlayerDetails = "Current lobby players (Auth IDs): ";
        foreach (var player in LobbyManager.Instance.JoinedLobby.Players)
        {
            lobbyPlayerDetails += player.Id + " ";
            if (player.Id == authIdPayload)
            {
                playerInLobby = true;
                foundLobbyPlayer = player;
                // No break, log all players first
            }
        }
        Debug.Log($"[Server ApprovalCheck] For client {clientId}: {lobbyPlayerDetails}");


        if (!playerInLobby)
        {
            Debug.LogError($"[Server ApprovalCheck] Denying client {clientId} (Auth ID: {authIdPayload}): Player not found in current lobby.");
            response.Approved = false;
            response.Reason = "Player not found in current lobby.";
            return;
        }
        
        Debug.Log($"[Server ApprovalCheck] For client {clientId} (Auth ID: {authIdPayload}): Player IS in lobby. Storing mapping.");
        _clientIdToAuthId[clientId] = authIdPayload;

        response.Approved = true;
        response.CreatePlayerObject = false; 
        response.PlayerPrefabHash = null; 
        response.Position = Vector3.zero; 
        response.Rotation = Quaternion.identity;
        Debug.Log($"[Server ApprovalCheck] Client {clientId} (Auth ID: {authIdPayload}) approved and mapping stored.");
    }


    public void StartRelayHost()
    {
        // For the host, we also need to set up its Auth ID mapping
        // The host doesn't go through the ApprovalCheck for itself in the same way,
        // but OnClientConnected will be called for the host (clientId 0 or NetworkManager.Singleton.LocalClientId).
        if (AuthenticationService.Instance.IsSignedIn)
        {
            string hostAuthId = AuthenticationService.Instance.PlayerId;
            ulong hostClientId = NetworkManager.Singleton.LocalClientId;
            _clientIdToAuthId[hostClientId] = hostAuthId;
            Debug.Log($"[Host Setup] Stored self-mapping for Host. ClientId: {hostClientId}, AuthId: {hostAuthId}");

            // Set ConnectionData for the host's own "connection" payload
            byte[] connectionData = System.Text.Encoding.UTF8.GetBytes(hostAuthId);
            NetworkManager.Singleton.NetworkConfig.ConnectionData = connectionData;
            Debug.Log($"[Host Setup] Set ConnectionData payload for host: '{hostAuthId}'");
        }
        else
        {
            Debug.LogError("[Host Setup] Host is NOT signed into Authentication Service. Cannot determine Auth ID for host. This will cause issues.");
            // Set empty ConnectionData if host is not signed in, consistent with client behavior
            NetworkManager.Singleton.NetworkConfig.ConnectionData = System.Text.Encoding.UTF8.GetBytes("");
            Debug.LogWarning("[Host Setup] Set empty ConnectionData payload for host as it's not signed in.");
        }

        // Ensure ApprovalCheck is assigned if we are becoming a server now
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.ConnectionApprovalCallback == null)
        {
            Debug.Log("[Host Setup] Assigning ConnectionApprovalCallback for Host.");
            NetworkManager.Singleton.ConnectionApprovalCallback += ApprovalCheck;
        }
        else if (NetworkManager.Singleton != null && NetworkManager.Singleton.ConnectionApprovalCallback != null)
        {
            Debug.Log("[Host Setup] ConnectionApprovalCallback was already assigned for Host.");
        }

        NetworkManager.Singleton.StartHost();
        Debug.Log("NetworkManager started as Host via Relay.");
    }

    public void StartRelayClient()
    {
        // The client needs to send its Auth ID. This is done by setting NetworkConfig.ConnectionData
        // This must be done *before* StartClient() is called.
        if (AuthenticationService.Instance.IsSignedIn)
        {
            string playerId = AuthenticationService.Instance.PlayerId;
            Debug.Log($"[Client] Attempting to connect. IsSignedIn: true, PlayerId to send: {playerId}"); // MODIFIED LOG
            byte[] connectionData = System.Text.Encoding.UTF8.GetBytes(playerId);
            NetworkManager.Singleton.NetworkConfig.ConnectionData = connectionData;
        }
        else
        {
            Debug.LogError("[Client] Attempting to connect. IsSignedIn: false. Sending empty Auth ID."); // MODIFIED LOG
            NetworkManager.Singleton.NetworkConfig.ConnectionData = System.Text.Encoding.UTF8.GetBytes(""); 
        }

        NetworkManager.Singleton.StartClient();
        Debug.Log("NetworkManager started as Client via Relay.");
    }

    public void StartRelayServer()
    {
        // Ensure ApprovalCheck is assigned if we are becoming a server now
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.ConnectionApprovalCallback == null)
        {
            Debug.Log("[Server Setup] Assigning ConnectionApprovalCallback for dedicated Server.");
            NetworkManager.Singleton.ConnectionApprovalCallback += ApprovalCheck;
        }
        else if (NetworkManager.Singleton != null && NetworkManager.Singleton.ConnectionApprovalCallback != null)
        {
            Debug.Log("[Server Setup] ConnectionApprovalCallback was already assigned for dedicated Server.");
        }

        NetworkManager.Singleton.StartServer(); // Dedicated server won't have a local player character in the same way
        Debug.Log("NetworkManager started as Server via Relay.");
    }

    private void OnClientConnected(ulong clientId)
    {
        if (NetworkManager.Singleton.IsServer)
        {
            Debug.Log($"Client {clientId} connected. Attempting to spawn player.");
            // We now use the Auth ID stored during connection approval
            if (_clientIdToAuthId.TryGetValue(clientId, out string authId))
            {
                SpawnPlayerForClient(clientId, authId);
            }
            else
            {
                Debug.LogError($"Auth ID not found for connected client {clientId}. Cannot spawn player. This might happen if host logic for self-mapping is missing or client didn't send Auth ID.");
                // Potentially spawn a default observer or kick the player
            }
        }
    }
    
    private void OnClientDisconnected(ulong clientId)
    {
        if (NetworkManager.Singleton.IsServer)
        {
            Debug.Log($"Client {clientId} disconnected.");
            // Clean up the mapping
            if (_clientIdToAuthId.ContainsKey(clientId))
            {
                _clientIdToAuthId.Remove(clientId);
            }
        }
    }

    private void SpawnPlayerForClient(ulong clientId, string authId)
    {
        if (LobbyManager.Instance == null || LobbyManager.Instance.JoinedLobby == null)
        {
            Debug.LogError($"LobbyManager or JoinedLobby is null. Cannot spawn player with character data for client {clientId} (Auth ID: {authId}).");
            SpawnDefaultPlayer(clientId); // Fallback
            return;
        }

        // Find the player in the lobby using their Authentication ID
        Player lobbyPlayer = LobbyManager.Instance.JoinedLobby.Players.Find(p => p.Id == authId);

        if (lobbyPlayer == null)
        {
            Debug.LogError($"Lobby player data not found for Auth ID: {authId} (Client ID: {clientId}). Spawning default prefab.");
            SpawnDefaultPlayer(clientId); // Fallback
            return;
        }

        string selectedCharacterId = "";
        if (lobbyPlayer.Data.TryGetValue("SelectedCharacter", out PlayerDataObject charData))
        {
            selectedCharacterId = charData.Value;
        }
        else
        {
            Debug.LogWarning($"Client {clientId} (Auth ID: {authId}) has no 'SelectedCharacter' data. Spawning default.");
        }

        GameObject playerPrefabToSpawn = GetPrefabForCharacterId(selectedCharacterId);

        if (playerPrefabToSpawn != null)
        {
            NetworkObject playerNetworkObject = Instantiate(playerPrefabToSpawn).GetComponent<NetworkObject>();
            playerNetworkObject.SpawnAsPlayerObject(clientId, true); // destroyWithScene = true
            Debug.Log($"Spawned '{selectedCharacterId}' (Prefab: {playerPrefabToSpawn.name}) for client {clientId} (Auth ID: {authId}).");
        }
        else
        {
            Debug.LogError($"Prefab for character ID '{selectedCharacterId}' not found for client {clientId} (Auth ID: {authId}). Spawning default prefab.");
            SpawnDefaultPlayer(clientId); // Fallback
        }
    }

    private void SpawnDefaultPlayer(ulong clientId)
    {
        Debug.LogWarning($"Spawning default player for client {clientId}.");
        GameObject defaultPlayerPrefab = GetPrefabForCharacterId(""); // Get a default prefab (empty string or "Default")
        if (defaultPlayerPrefab != null)
        {
            NetworkObject defaultPlayerNetworkObject = Instantiate(defaultPlayerPrefab).GetComponent<NetworkObject>();
            defaultPlayerNetworkObject.SpawnAsPlayerObject(clientId, true);
        }
        else
        {
            Debug.LogError($"No default player prefab assigned or found for client {clientId}!");
        }
    }

    private GameObject GetPrefabForCharacterId(string characterId)
    {
        foreach (var mapping in characterPrefabs)
        {
            if (mapping.characterId == characterId)
            {
                return mapping.prefab;
            }
        }
        // Fallback to a default prefab if the specific characterId is not found or if characterId is empty
        var defaultMapping = characterPrefabs.Find(m => string.IsNullOrEmpty(m.characterId) || m.characterId.Equals("Default", System.StringComparison.OrdinalIgnoreCase));
        if (defaultMapping != null)
        {
            Debug.LogWarning($"Character prefab for ID '{characterId}' not found. Returning default: {defaultMapping.prefab?.name ?? "null"}.");
            return defaultMapping.prefab;
        }
        
        Debug.LogError($"No prefab found for ID '{characterId}' and no default prefab is configured with an empty/\"Default\" ID.");
        return null; // Critical error: no prefab found
    }
}
