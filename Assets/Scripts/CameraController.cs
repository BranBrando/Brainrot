using System.Collections.Generic;
using UnityEngine;

public class CameraController : MonoBehaviour
{
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
        mainCamera = GetComponent<Camera>();

        if (mainCamera == null)
        {
            Debug.LogError("CameraController: No Camera component found on this GameObject.");
        }

        if (playerTarget == null)
        {
            Debug.LogError("CameraController: playerTarget is not assigned.  Please assign in the inspector.");
        }
    }

    private void LateUpdate()
    {
        // Collect Active Targets
        List<Transform> currentTargets = new List<Transform>();

        if (playerTarget != null)
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
            Debug.LogWarning("CameraController: No active targets found.  Please ensure playerTarget and/or enemyTargets are assigned and active.");
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
