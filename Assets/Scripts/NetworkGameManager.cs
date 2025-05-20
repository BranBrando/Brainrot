using UnityEngine;
using Unity.Netcode;
using UnityEngine.UI; // Assuming you'll use Unity UI

public class NetworkGameManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Button hostButton;
    [SerializeField] private Button clientButton;
    [SerializeField] private Button serverButton; // Optional: for dedicated server

    void Awake()
    {
        // Subscribe UI buttons to network actions
        if (hostButton != null)
        {
            hostButton.onClick.AddListener(() => NetworkManager.Singleton.StartHost());
        }
        if (clientButton != null)
        {
            clientButton.onClick.AddListener(() => NetworkManager.Singleton.StartClient());
        }
        if (serverButton != null)
        {
            serverButton.onClick.AddListener(() => NetworkManager.Singleton.StartServer());
        }

    }

    void Start()
    {
        // Ensure NetworkManager is in the scene
        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("NetworkGameManager: NetworkManager.Singleton not found in the scene. Please add one.");
            return;
        }

        // Optional: Hide buttons after connection starts
        NetworkManager.Singleton.OnClientStarted += HideConnectionButtons;
        NetworkManager.Singleton.OnServerStarted += HideConnectionButtons;
    }

    void OnDestroy()
    {
        // Unsubscribe to prevent memory leaks
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientStarted -= HideConnectionButtons;
            NetworkManager.Singleton.OnServerStarted -= HideConnectionButtons;
        }
    }

    private void HideConnectionButtons()
    {
        if (hostButton != null) hostButton.gameObject.SetActive(false);
        if (clientButton != null) clientButton.gameObject.SetActive(false);
        if (serverButton != null) serverButton.gameObject.SetActive(false);
    }
    // You might add more UI elements and methods here for:
    // - Displaying connection status
    // - Inputting IP addresses for direct connect (requires a different transport)
    // - Handling disconnects
    // - Displaying player list, etc.
}
