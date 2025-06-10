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
    [SerializeField] private GameObject createLobbyPanel;
    [SerializeField] private GameObject joinLobbyPanel;

    [Header("Status & Player Name")]
    [SerializeField] private TMP_InputField playerNameInputField;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private Button createLobbyButtonFromMain;
    [SerializeField] private Button joinLobbyButtonFromMain;

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

    [Header("Character Selection Panel")]
    [SerializeField] private GameObject characterSelectionPanel;
    [SerializeField] private Transform characterButtonParent; // Parent for character selection buttons
    [SerializeField] private GameObject characterButtonPrefab; // Prefab for a character button
    [SerializeField] private TextMeshProUGUI selectedCharacterText; // Displays current selection

    [Header("In-Lobby Panel")]
    [SerializeField] private TextMeshProUGUI currentLobbyNameText;
    [SerializeField] private TextMeshProUGUI currentLobbyCodeText;
    [SerializeField] private TextMeshProUGUI currentLobbyPlayersText; // Displays player names
    [SerializeField] private Button startGameButton;
    [SerializeField] private Button leaveLobbyButton;
    [SerializeField] private Button toggleReadyButton; // For clients to ready up

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
        createLobbyButtonFromMain.onClick.AddListener(() =>
        {
            ShowPanel(lobbyListPanel);
            ShowPanel(createLobbyPanel);
        });
        joinLobbyButtonFromMain.onClick.AddListener(() =>
        {
            ShowPanel(lobbyListPanel);
            ShowPanel(joinLobbyPanel);
        });
        // Set up button listeners
        createLobbyButton.onClick.AddListener(HandleCreateLobby);
        refreshLobbiesButton.onClick.AddListener(HandleRefreshLobbies);
        joinLobbyByCodeButton.onClick.AddListener(HandleJoinLobbyByCode);
        leaveLobbyButton.onClick.AddListener(HandleLeaveLobby);
        startGameButton.onClick.AddListener(HandleStartGame);
        toggleReadyButton.onClick.AddListener(HandleToggleReady); // New listener

        // Subscribe to LobbyManager events
        LobbyManager.Instance.OnLobbyListChanged += UpdateLobbyListUIWithData;
        LobbyManager.Instance.OnJoinedLobby += OnJoinedLobbyUI;
        LobbyManager.Instance.OnLeftLobby += OnLeftLobbyUI;
        LobbyManager.Instance.OnKickedFromLobby += OnLeftLobbyUI; // Kicked is similar to leaving
        LobbyManager.Instance.OnLobbyUpdated += OnLobbyUpdatedUI;
        LobbyManager.Instance.OnGameStarted += OnGameStartedUI;

        // Initial UI state
        ShowPanel(mainPanel);
        HidePanel(lobbyListPanel);
        HidePanel(inLobbyPanel);
        HidePanel(createLobbyPanel);
        HidePanel(joinLobbyPanel);
        HidePanel(characterSelectionPanel); // New: Hide character selection panel initially
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

    private async void HandleToggleReady()
    {
        if (LobbyManager.Instance.JoinedLobby == null) return;

        Player localPlayer = LobbyManager.Instance.JoinedLobby.Players.Find(p => p.Id == LobbyManager.Instance.PlayerId);
        if (localPlayer == null) return;

        bool currentReadyState = false;
        if (localPlayer.Data.TryGetValue("IsReady", out PlayerDataObject readyData))
        {
            bool.TryParse(readyData.Value, out currentReadyState);
        }
        
        // Also ensure a character is selected before allowing to toggle ready ON
        if (!currentReadyState) // If trying to become ready
        {
            if (string.IsNullOrEmpty(_locallySelectedCharacterId) && 
                (!localPlayer.Data.TryGetValue("SelectedCharacter", out PlayerDataObject charData) || string.IsNullOrEmpty(charData.Value)))
            {
                UpdateStatus("Please select a character before readying up.");
                return;
            }
        }

        UpdateStatus($"Setting ready status to {!currentReadyState}...");
        await LobbyManager.Instance.SetPlayerReadyStatus(!currentReadyState);
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

    private string _locallySelectedCharacterId = ""; // To keep track of local player's selected character

    private void OnJoinedLobbyUI(Lobby lobby)
    {
        HidePanel(lobbyListPanel);
        HidePanel(mainPanel); // Hide main menu
        ShowPanel(characterSelectionPanel); // Show character selection first
        HidePanel(inLobbyPanel); // Keep in-lobby hidden for now

        // Populate character selection buttons
        PopulateCharacterSelectionButtons();

        UpdateStatus($"Joined lobby '{lobby.Name}'. Select your character.");
        // Update other UI elements as needed (lobby name, code on the character selection panel perhaps)
        currentLobbyNameText.text = $"Lobby: {lobby.Name}"; // Assuming these are visible on char select panel too
        currentLobbyCodeText.text = $"Code: {lobby.LobbyCode}";
        UpdateInLobbyPlayersUI(lobby); // Update player list on char select panel

        // Start game button should still be host-only, but its interactability will depend on new conditions
        startGameButton.gameObject.SetActive(lobby.HostId == LobbyManager.Instance.PlayerId);
        startGameButton.interactable = false; // Initially false, enabled by OnLobbyUpdatedUI

        toggleReadyButton.gameObject.SetActive(lobby.HostId != LobbyManager.Instance.PlayerId); // Only for clients
    }

    void PopulateCharacterSelectionButtons()
    {
        foreach (Transform child in characterButtonParent) Destroy(child.gameObject); // Clear old buttons

        // TODO: Get availableCharacters from somewhere (e.g., a ScriptableObject, GameManager)
        // For now, let's assume a simple list of strings:
        List<string> characterNames = new List<string> { "Tung", "Tra" }; 

        foreach (string charName in characterNames)
        {
            GameObject charButtonGO = Instantiate(characterButtonPrefab, characterButtonParent);
            TextMeshProUGUI buttonText = charButtonGO.GetComponentInChildren<TextMeshProUGUI>();
            Button button = charButtonGO.GetComponent<Button>();

            if (buttonText != null) buttonText.text = charName;
            if (button != null) button.onClick.AddListener(() => HandleCharacterSelected(charName));
        }
    }

    async void HandleCharacterSelected(string characterId)
    {
        _locallySelectedCharacterId = characterId;
        if(selectedCharacterText != null) selectedCharacterText.text = $"Selected: {characterId}";
        UpdateStatus($"You selected {characterId}.");
        await LobbyManager.Instance.SetPlayerCharacter(characterId);
        // After selecting a character, the player might automatically be considered "ready" for character selection
        // Or they need to click a separate "Confirm Character" or "Ready" button.
        // For now, selecting a character also sets them as ready.
        await LobbyManager.Instance.SetPlayerReadyStatus(true); 
        
        // Transition to the in-lobby view after selection
        ShowPanel(inLobbyPanel);
        HidePanel(characterSelectionPanel);
    }

    private void OnLobbyUpdatedUI(Lobby lobby)
    {
        if (LobbyManager.Instance.JoinedLobby == null || LobbyManager.Instance.JoinedLobby.Id != lobby.Id) return; // Not the lobby we're in

        currentLobbyNameText.text = $"Lobby: {lobby.Name}";
        currentLobbyCodeText.text = $"Code: {lobby.LobbyCode}";
        UpdateInLobbyPlayersUI(lobby);

        bool isHost = lobby.HostId == LobbyManager.Instance.PlayerId;
        startGameButton.gameObject.SetActive(isHost);
        if (isHost)
        {
            startGameButton.interactable = LobbyManager.Instance.AreAllPlayersReadyAndSelectedCharacter();
        }

        // Update client's ready button text/state if needed
        Player localPlayer = lobby.Players.Find(p => p.Id == LobbyManager.Instance.PlayerId);
        if (localPlayer != null && localPlayer.Data.TryGetValue("IsReady", out PlayerDataObject readyData))
        {
            bool isClientReady = readyData.Value.ToLower() == "true";
            // Assuming toggleReadyButton has a TextMeshProUGUI child to update
            TextMeshProUGUI readyButtonText = toggleReadyButton.GetComponentInChildren<TextMeshProUGUI>();
            if (readyButtonText != null)
            {
                readyButtonText.text = isClientReady ? "Unready" : "Ready";
            }
        }
    }

    private void UpdateInLobbyPlayersUI(Lobby lobby)
    {
        string playersString = "Players:\n";
        foreach (Player player in lobby.Players)
        {
            player.Data.TryGetValue("PlayerName", out PlayerDataObject playerNameData);
            player.Data.TryGetValue("IsReady", out PlayerDataObject isReadyData);
            player.Data.TryGetValue("SelectedCharacter", out PlayerDataObject selectedCharacterData);

            string playerName = playerNameData?.Value ?? "Unknown";
            string readyStatus = (isReadyData?.Value.ToLower() == "true") ? "[Ready]" : "[Not Ready]";
            string character = string.IsNullOrEmpty(selectedCharacterData?.Value) ? "(No Char)" : $"({selectedCharacterData.Value})";
            
            playersString += $"- {playerName} {character} {readyStatus}\n";
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

    // You might want to add a reference to your main gameplay panel/object if you have one
    // [Header("Gameplay Elements")]
    // [SerializeField] private GameObject gameplayRootObject; // Assign in Inspector

    private async void OnGameStartedUI(Lobby lobby)
    {
        UpdateStatus("Game starting...");
        HidePanel(mainPanel);
        HidePanel(inLobbyPanel);
        HidePanel(characterSelectionPanel);
        // HidePanel(lobbyListPanel); // Should already be hidden
        // HidePanel(createLobbyPanel);
        // HidePanel(joinLobbyPanel);

        // If you have a root GameObject for all your gameplay elements, show it here:
        // if (gameplayRootObject != null) gameplayRootObject.SetActive(true);

        string relayJoinCode = lobby.Data["RelayJoinCode"].Value;

        if (lobby.HostId == LobbyManager.Instance.PlayerId)
        {
            NetworkGameManager.Singleton.StartRelayHost();
            // Game now starts in the same scene.
            // If GameManager.Instance.SetupNewGame() needs to be called to prepare the scene,
            // the host could do it here after starting the network.
            // For example:
            // if (GameManager.Instance != null) GameManager.Instance.SetupNewGame();
        }
        else // This is a client
        {
            bool joinedRelay = await RelayManager.Instance.JoinRelayAllocation(relayJoinCode);
            if (joinedRelay)
            {
                NetworkGameManager.Singleton.StartRelayClient();
                // Clients will have players spawned by NetworkGameManager.
                // Game state should synchronize from the host.
            }
            else
            {
                UpdateStatus("Failed to join Relay allocation for game start.");
                ShowPanel(mainPanel); // Or revert to characterSelectionPanel / inLobbyPanel
                // Potentially show other relevant lobby panels too
                return; // Prevent further execution if Relay join fails
            }
        }

        // At this point, the network is starting/started.
        // Player prefabs will be spawned by NetworkGameManager.
        // Your GameManager should handle the actual game logic from here.
    }
}
