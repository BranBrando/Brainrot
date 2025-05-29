using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro; // Assuming you are using TextMeshPro for text
using Unity.Services.Lobbies.Models;
using System.Threading.Tasks;
using Unity.Netcode; // For NetworkManager.Singleton.SceneManager.LoadScene

public class LobbyUI : MonoBehaviour
{
    [Header("Main UI Panels")]
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private GameObject lobbyListPanel;
    [SerializeField] private GameObject inLobbyPanel;

    [Header("Status & Player Name")]
    [SerializeField] private TMP_InputField playerNameInputField;
    [SerializeField] private TextMeshProUGUI statusText;

    [Header("Create Lobby Panel")]
    [SerializeField] private TMP_InputField createLobbyNameInputField;
    [SerializeField] private TMP_InputField createLobbyMaxPlayersInputField;
    [SerializeField] private Toggle createLobbyPrivateToggle;
    [SerializeField] private Button createLobbyButton;

    [Header("Lobby List Panel")]
    [SerializeField] private Button refreshLobbiesButton;
    [SerializeField] private Transform lobbyListContentParent; // Parent for instantiated lobby items
    [SerializeField] private GameObject lobbyItemPrefab; // Prefab for displaying a single lobby
    [SerializeField] private TMP_InputField joinLobbyCodeInputField;
    [SerializeField] private Button joinLobbyByCodeButton;

    [Header("In-Lobby Panel")]
    [SerializeField] private TextMeshProUGUI currentLobbyNameText;
    [SerializeField] private TextMeshProUGUI currentLobbyCodeText;
    [SerializeField] private TextMeshProUGUI currentLobbyPlayersText; // Displays player names
    [SerializeField] private Button startGameButton;
    [SerializeField] private Button leaveLobbyButton;

    private void Awake()
    {
        // Ensure singletons are initialized
        if (!UnityServicesInitializer.IsInitialized)
        {
            GameObject initializerGO = new GameObject("UnityServicesInitializer");
            initializerGO.AddComponent<UnityServicesInitializer>();
        }
        if (RelayManager.Instance == null)
        {
            GameObject relayGO = new GameObject("RelayManager");
            relayGO.AddComponent<RelayManager>();
        }
        if (LobbyManager.Instance == null)
        {
            GameObject lobbyGO = new GameObject("LobbyManager");
            lobbyGO.AddComponent<LobbyManager>();
        }
    }

    private async void Start()
    {
        // Initialize Unity Services first
        UpdateStatus("Initializing Unity Services...");
        await UnityServicesInitializer.InitializeUnityServices();
        UpdateStatus("Unity Services Initialized and Authenticated.");

        // Set up button listeners
        createLobbyButton.onClick.AddListener(HandleCreateLobby);
        refreshLobbiesButton.onClick.AddListener(HandleRefreshLobbies);
        joinLobbyByCodeButton.onClick.AddListener(HandleJoinLobbyByCode);
        leaveLobbyButton.onClick.AddListener(HandleLeaveLobby);
        startGameButton.onClick.AddListener(HandleStartGame);

        // Subscribe to LobbyManager events
        LobbyManager.Instance.OnLobbyListChanged += UpdateLobbyListUIWithData;
        LobbyManager.Instance.OnJoinedLobby += OnJoinedLobbyUI;
        LobbyManager.Instance.OnLeftLobby += OnLeftLobbyUI;
        LobbyManager.Instance.OnKickedFromLobby += OnLeftLobbyUI; // Kicked is similar to leaving
        LobbyManager.Instance.OnLobbyUpdated += OnLobbyUpdatedUI;
        LobbyManager.Instance.OnGameStarted += OnGameStartedUI;

        // Initial UI state
        ShowPanel(mainPanel);
        ShowPanel(lobbyListPanel); // Show lobby list by default
        HidePanel(inLobbyPanel);
    }

    private void OnDestroy()
    {
        // Unsubscribe from LobbyManager events to prevent memory leaks
        if (LobbyManager.Instance != null)
        {
            LobbyManager.Instance.OnLobbyListChanged -= UpdateLobbyListUIWithData;
            LobbyManager.Instance.OnJoinedLobby -= OnJoinedLobbyUI;
            LobbyManager.Instance.OnLeftLobby -= OnLeftLobbyUI;
            LobbyManager.Instance.OnKickedFromLobby -= OnLeftLobbyUI;
            LobbyManager.Instance.OnLobbyUpdated -= OnLobbyUpdatedUI;
            LobbyManager.Instance.OnGameStarted -= OnGameStartedUI;
        }
    }

    private void UpdateStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }
    }

    private void ShowPanel(GameObject panel)
    {
        if (panel != null) panel.SetActive(true);
    }

    private void HidePanel(GameObject panel)
    {
        if (panel != null) panel.SetActive(false);
    }

    // --- Button Handlers ---

    private async void HandleCreateLobby()
    {
        UpdateStatus("Creating lobby...");
        string lobbyName = createLobbyNameInputField.text;
        int maxPlayers;
        if (!int.TryParse(createLobbyMaxPlayersInputField.text, out maxPlayers))
        {
            maxPlayers = 4; // Default if input is invalid
            Debug.LogWarning("Invalid max players input. Defaulting to 4.");
        }
        bool isPrivate = createLobbyPrivateToggle.isOn;

        Lobby lobby = await LobbyManager.Instance.CreateLobby(lobbyName, maxPlayers, isPrivate);
        if (lobby != null)
        {
            UpdateStatus($"Lobby '{lobby.Name}' created. Code: {lobby.LobbyCode}");
        }
        else
        {
            UpdateStatus("Failed to create lobby.");
        }
    }

    private async void HandleRefreshLobbies()
    {
        UpdateStatus("Refreshing lobbies...");
        await LobbyManager.Instance.ListLobbies(); // This will trigger UpdateLobbyListUI via event
        UpdateStatus("Lobbies refreshed.");
    }

    private async void HandleJoinLobbyByCode()
    {
        UpdateStatus("Joining lobby by code...");
        string lobbyCode = joinLobbyCodeInputField.text;
        Lobby lobby = await LobbyManager.Instance.JoinLobbyByCode(lobbyCode);
        if (lobby != null)
        {
            UpdateStatus($"Joined lobby '{lobby.Name}'.");
        }
        else
        {
            UpdateStatus("Failed to join lobby by code.");
        }
    }

    private async void HandleJoinLobbyFromList(string lobbyId)
    {
        UpdateStatus("Joining lobby from list...");
        Lobby lobby = await LobbyManager.Instance.JoinLobbyById(lobbyId);
        if (lobby != null)
        {
            UpdateStatus($"Joined lobby '{lobby.Name}'.");
        }
        else
        {
            UpdateStatus("Failed to join lobby from list.");
        }
    }

    private async void HandleLeaveLobby()
    {
        UpdateStatus("Leaving lobby...");
        await LobbyManager.Instance.LeaveLobby();
        UpdateStatus("Left lobby.");
    }

    private async void HandleStartGame()
    {
        UpdateStatus("Starting game...");
        await LobbyManager.Instance.StartGame();
    }

    // --- UI Update Callbacks from LobbyManager Events ---

    private void UpdateLobbyListUIWithData(List<Lobby> lobbies)
    {
        // Clear existing lobby items
        foreach (Transform child in lobbyListContentParent)
        {
            Destroy(child.gameObject);
        }

        Debug.Log("Updating lobby list UI with data...");
        Debug.Log($"Received {lobbies?.Count ?? 0} lobbies from event.");
        if (lobbies != null)
        {
            foreach (Lobby lobby in lobbies)
            {
                GameObject lobbyItemGO = Instantiate(lobbyItemPrefab, lobbyListContentParent);
                // Assuming LobbyItemPrefab has TextMeshProUGUI components for name and players, and a Button
                TextMeshProUGUI nameText = lobbyItemGO.transform.Find("LobbyNameText").GetComponent<TextMeshProUGUI>();
                TextMeshProUGUI playersText = lobbyItemGO.transform.Find("PlayersText").GetComponent<TextMeshProUGUI>();
                Button joinButton = lobbyItemGO.transform.Find("JoinButton").GetComponent<Button>();

                if (nameText != null) nameText.text = lobby.Name;
                if (playersText != null) playersText.text = $"{lobby.Players.Count}/{lobby.MaxPlayers}";
                if (joinButton != null)
                {
                    joinButton.onClick.AddListener(() => HandleJoinLobbyFromList(lobby.Id));
                    joinButton.interactable = lobby.AvailableSlots > 0;
                }
            }
        }
    }

    private void OnJoinedLobbyUI(Lobby lobby)
    {
        HidePanel(lobbyListPanel);
        ShowPanel(inLobbyPanel);

        currentLobbyNameText.text = $"Lobby: {lobby.Name}";
        currentLobbyCodeText.text = $"Code: {lobby.LobbyCode}";
        UpdateInLobbyPlayersUI(lobby);

        // Only host can start game
        startGameButton.gameObject.SetActive(lobby.HostId == LobbyManager.Instance.PlayerId);
    }

    private void OnLobbyUpdatedUI(Lobby lobby)
    {
        if (LobbyManager.Instance.JoinedLobby == null || LobbyManager.Instance.JoinedLobby.Id != lobby.Id) return; // Not the lobby we're in

        currentLobbyNameText.text = $"Lobby: {lobby.Name}";
        currentLobbyCodeText.text = $"Code: {lobby.LobbyCode}";
        UpdateInLobbyPlayersUI(lobby);

        // Update host-specific UI if host status changes or players join/leave
        startGameButton.gameObject.SetActive(lobby.HostId == LobbyManager.Instance.PlayerId);
    }

    private void UpdateInLobbyPlayersUI(Lobby lobby)
    {
        string playersString = "Players:\n";
        foreach (Player player in lobby.Players)
        {
            player.Data.TryGetValue("PlayerName", out PlayerDataObject playerNameData);
            playersString += $"- {playerNameData?.Value ?? "Unknown Player"} (ID: {player.Id})\n";
        }
        currentLobbyPlayersText.text = playersString;
    }

    private async void OnLeftLobbyUI()
    {
        HidePanel(inLobbyPanel);
        ShowPanel(lobbyListPanel);
        UpdateStatus("You have left the lobby. Refreshing list...");
        await LobbyManager.Instance.ListLobbies(); // This will trigger UpdateLobbyListUIWithData
        UpdateStatus("Lobby list refreshed after leaving.");
    }

    private async void OnGameStartedUI(Lobby lobby)
    {
        UpdateStatus("Game starting...");
        HidePanel(mainPanel); // Hide all lobby UI

        string relayJoinCode = lobby.Data["RelayJoinCode"].Value;

        if (lobby.HostId == LobbyManager.Instance.PlayerId)
        {
            // Host already created allocation in CreateLobby, just start Netcode
            NetworkGameManager.Singleton.StartRelayHost();
            // Load the game scene after Netcode starts - ONLY HOST DOES THIS
            // IMPORTANT: Replace "GameScene" with the actual name of your game scene
            // Ensure "GameScene" is added to Build Settings -> Scenes In Build
            // NetworkManager.Singleton.SceneManager.LoadScene("SampleScene", UnityEngine.SceneManagement.LoadSceneMode.Single);
            HidePanel(inLobbyPanel); // Hide in-lobby UI
        }
        else // This is a client
        {
            // Client needs to join Relay allocation
            bool joinedRelay = await RelayManager.Instance.JoinRelayAllocation(relayJoinCode);
            if (joinedRelay)
            {
                NetworkGameManager.Singleton.StartRelayClient();
                HidePanel(inLobbyPanel); // Hide in-lobby UI
                // Client does NOT load the scene. It will be loaded automatically by Netcode
                // when the server loads it.
            }
            else
            {
                UpdateStatus("Failed to join Relay allocation for game start.");
                ShowPanel(mainPanel); // Show UI again if failed
            }
        }
    }
}
