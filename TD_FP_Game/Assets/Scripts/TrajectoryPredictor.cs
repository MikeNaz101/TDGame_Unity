using UnityEngine;
using System.Collections.Generic; // Required for using Lists

[RequireComponent(typeof(LineRenderer))] // Ensures a Line Renderer is attached
public class TrajectoryPredictor : MonoBehaviour
{
    public Transform launchPoint;       // Where the trajectory starts
    public float initialVelocity = 10f; // Speed of the projectile
    public int resolution = 30;         // Number of points on the line
    public float timeStep = 0.1f;       // Time interval between points
    public LayerMask collisionMask;     // Layers the trajectory line should collide with
    public float raycastRadius = 0.05f; // Radius for spherecasting (optional, but better than raycast)

    private LineRenderer lineRenderer;
    private Vector3 gravity;

    void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
        gravity = Physics.gravity; // Use Unity's gravity setting
    }

    void Update()
    {
        // --- VR Input Logic ---
        // Replace this with your actual VR button check (e.g., trigger press)
        // Example: bool showTrajectory = OVRInput.Get(OVRInput.Button.PrimaryIndexTrigger);
        // Example: bool showTrajectory = xrController.inputDevice.TryGetFeatureValue(CommonUsages.triggerButton, out bool triggerPressed) && triggerPressed;

        bool showTrajectory = Input.GetKey(KeyCode.Space); // Placeholder for VR input

        if (showTrajectory && launchPoint != null)
        {
            DrawTrajectory();
            lineRenderer.enabled = true;
        }
        else
        {
            lineRenderer.enabled = false; // Hide the line when not needed
        }
    }

    void DrawTrajectory()
    {
        Vector3 startPosition = launchPoint.position;
        Vector3 startVelocity = launchPoint.forward * initialVelocity; // Use the launch point's forward direction

        List<Vector3> points = new List<Vector3>();
        Vector3 currentPosition = startPosition;
        Vector3 currentVelocity = startVelocity;

        points.Add(startPosition); // Add the starting point

        for (int i = 1; i < resolution; i++)
        {
            // Calculate the position at the next time step using physics formula:
            // Position = initialPosition + initialVelocity * time + 0.5 * acceleration * time^2
            // We simplify by calculating step-by-step

            // Calculate position before applying gravity for this step
            Vector3 nextPosition = currentPosition + currentVelocity * timeStep;

            // Apply gravity effect for this step
            currentVelocity += gravity * timeStep;

            // --- Collision Check (Optional but Recommended) ---
            // Use Spherecast for better detection than Raycast
            if (Physics.SphereCast(currentPosition, raycastRadius, (nextPosition - currentPosition).normalized, out RaycastHit hit, Vector3.Distance(currentPosition, nextPosition), collisionMask))
            {
                // Hit something, add the hit point and stop drawing
                points.Add(hit.point);
                break; // Exit the loop
            }
            // --- End Collision Check ---

            currentPosition = nextPosition;
            points.Add(currentPosition);

            // Optional: Stop if the line goes too far down
            if (currentPosition.y < -100f) // Adjust threshold as needed
            {
                break;
            }
        }

        // Update the Line Renderer
        lineRenderer.positionCount = points.Count;
        lineRenderer.SetPositions(points.ToArray());
    }
}