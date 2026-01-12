using UnityEngine;

public class CameraOrbit : MonoBehaviour
{
    public Transform target; // The GameObject to orbit around
    public float orbitSpeed = 5f; // Speed of rotation
    public float distance = 5f; // Distance from the target
    public bool autoRotate = true; // Enable auto-rotation

    private float rotationY = 0f;

    void LateUpdate()
    {
        if (target == null)
        {
            Debug.LogError("CameraOrbit: Target GameObject not assigned!");
            return;
        }

        rotationY += orbitSpeed * Time.deltaTime; // Rotate automatically
        

        // Calculate the desired position
        Quaternion rotation = Quaternion.Euler(20, rotationY, 20);
        Vector3 desiredPosition = target.position - (rotation * Vector3.forward * distance);

        // Apply the position and rotation
        transform.position = desiredPosition;
        transform.LookAt(target);
    }
}