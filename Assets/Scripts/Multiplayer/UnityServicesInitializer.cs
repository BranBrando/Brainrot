using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Authentication;
using System.Threading.Tasks;

public class UnityServicesInitializer : MonoBehaviour
{
    public static bool IsInitialized { get; private set; } = false;
    private static bool isInitializing = false;

    private async void Awake()
    {
        if (!IsInitialized && !isInitializing)
        {
            await InitializeUnityServices();
        }
        else if (IsInitialized)
        {
            // Debug.Log("UnityServicesInitializer: Awake() called, but services already initialized.");
        }
        else // isInitializing must be true
        {
            // Debug.Log("UnityServicesInitializer: Awake() called, but services initialization is in progress.");
        }
    }

    public static async Task InitializeUnityServices()
    {
        if (IsInitialized)
        {
            Debug.Log("Unity Services already initialized.");
            return;
        }
        if (isInitializing)
        {
            Debug.LogWarning("Unity Services initialization is already in progress. Skipping this call.");
            return;
        }

        isInitializing = true;

        try
        {
            await UnityServices.InitializeAsync();
            Debug.Log("Unity Services initialized successfully.");

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
                Debug.Log("Signed in anonymously. Player ID: " + AuthenticationService.Instance.PlayerId);
            }
            else
            {
                Debug.Log("Already signed in. Player ID: " + AuthenticationService.Instance.PlayerId);
            }

            IsInitialized = true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error initializing Unity Services or signing in: {e.Message}\nStack Trace: {e.StackTrace}");
            IsInitialized = false;
        }
        finally
        {
            isInitializing = false;
        }
    }
}
