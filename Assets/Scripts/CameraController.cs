using System.Collections.Generic;
using UnityEngine;

public class CameraController : MonoBehaviour
{
    public static CameraController Instance { get; private set; } // Singleton instance

    [Header("Targets")]
    public Transform playerTarget;
    public List<Transform> enemyTargets = new List<Transform>();

    [Header("Framing")]
    public float padding = 2f;

    [Header("Zoom")]
    public float minOrthographicSize = 3f;
    public float maxOrthographicSize = 10f;

    [Header("Smoothing")]
    public float positionSmoothTime = 0.2f;
    public float sizeSmoothTime = 0.2f;

    [Header("Offset")]
    public Vector3 offset = new Vector3(0f, 0f, -10f);

    private Camera mainCamera;
    private Vector3 currentVelocityPosition;
    private float currentVelocitySize;

    private void Awake()
    {
        // Singleton setup
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("CameraController: Another instance found. Destroying this new one.");
            Destroy(gameObject); // Destroy this new instance
            return;
        }
        Instance = this;
        // DontDestroyOnLoad(gameObject); // Consider if camera should persist across different game scenes

        mainCamera = GetComponent<Camera>();
        if (mainCamera == null)
        {
            Debug.LogError("CameraController: No Camera component found on this GameObject. Disabling script.");
            enabled = false; // Disable script if no camera
            if (Instance == this) // If this was the one that set itself as Instance
            {
                Instance = null; // Nullify the instance if it's unusable
            }
        }
    }

    private void OnDestroy()
    {
        // If this was the singleton instance, clear it when destroyed
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void LateUpdate()
    {
        if (!enabled || mainCamera == null) return; // Don't run if disabled or no camera

        // Collect Active Targets
        List<Transform> currentTargets = new List<Transform>();

        if (playerTarget != null && playerTarget.gameObject.activeInHierarchy) // Check if playerTarget is active
        {
            currentTargets.Add(playerTarget);
        }

        foreach (Transform enemy in enemyTargets)
        {
            if (enemy != null && enemy.gameObject.activeInHierarchy)
            {
                currentTargets.Add(enemy);
            }
        }

        if (currentTargets.Count == 0)
        {
            // Debug.LogWarning("CameraController: No active targets found."); // This can be spammy, maybe remove or make conditional
            return;
        }

        // Calculate Target Position and Size
        Vector3 targetPosition;
        float targetOrthographicSize;

        if (currentTargets.Count == 1)
        {
            targetPosition = currentTargets[0].position + offset;
            targetOrthographicSize = minOrthographicSize; // Or a default size you prefer when only one target is active
        }
        else
        {
            Bounds bounds = new Bounds(currentTargets[0].position, Vector3.zero);
            for (int i = 1; i < currentTargets.Count; i++)
            {
                bounds.Encapsulate(currentTargets[i].position);
            }

            targetPosition = new Vector3(bounds.center.x, bounds.center.y, 0) + offset;

            float screenAspect = mainCamera.aspect;
            float requiredSizeX = bounds.size.x * 0.5f + padding;
            float requiredSizeY = bounds.size.y * 0.5f + padding;
            targetOrthographicSize = Mathf.Max(requiredSizeY, requiredSizeX / screenAspect);
            targetOrthographicSize = Mathf.Clamp(targetOrthographicSize, minOrthographicSize, maxOrthographicSize);
        }

        // Smooth Camera Movement and Zoom
        transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref currentVelocityPosition, positionSmoothTime);
        mainCamera.orthographicSize = Mathf.SmoothDamp(mainCamera.orthographicSize, targetOrthographicSize, ref currentVelocitySize, sizeSmoothTime);
    }
}
