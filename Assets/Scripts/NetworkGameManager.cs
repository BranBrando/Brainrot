using UnityEngine;
using Unity.Netcode;
using UnityEngine.UI; // Assuming you'll use Unity UI

public class NetworkGameManager : MonoBehaviour
{
    public static NetworkGameManager Singleton { get; private set; }

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

    // No longer directly using UI buttons here for starting host/client
    // These actions will be triggered by LobbyManager after Relay setup.

    void Start()
    {
        // Ensure NetworkManager is in the scene
        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("NetworkGameManager: NetworkManager.Singleton not found in the scene. Please add one.");
            return;
        }
    }

    public void StartRelayHost()
    {
        NetworkManager.Singleton.StartHost();
        Debug.Log("NetworkManager started as Host via Relay.");
    }

    public void StartRelayClient()
    {
        NetworkManager.Singleton.StartClient();
        Debug.Log("NetworkManager started as Client via Relay.");
    }

    public void StartRelayServer()
    {
        NetworkManager.Singleton.StartServer();
        Debug.Log("NetworkManager started as Server via Relay.");
    }
}
