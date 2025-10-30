using StarterAssets;
using UnityEngine;
//using StarterAssetsInputs;

// This script provides the core character movement logic, similar to the Doom-style FPS feel.
// It uses serialization attributes to make tuning fast and easy in the Inspector.
[RequireComponent(typeof(CharacterController))]
public class DoomMovement : MonoBehaviour
{
    // --- REFERENCES ---
    [Header("Component References")]
    [Tooltip("The CharacterController component attached to this GameObject.")]
    [SerializeField] private CharacterController _controller;
    [Tooltip("The camera transform, used for look rotation.")]
    [SerializeField] private Transform _cameraTransform;
    [Tooltip("The input script holding the current state of player input (Move, Look, Jump, Sprint).")]
    public StarterAssetsInputs inputScript; 

    // --- MOVEMENT TUNING ---
    [Header("Movement Tuning")]
    [Tooltip("Base walking speed.")]
    [Range(1f, 10f)] public float moveSpeed = 7.0f;
    [Tooltip("Speed when sprinting.")]
    [Range(5f, 20f)] public float sprintSpeed = 12.0f;
    [Tooltip("Acceleration applied to change movement direction.")]
    [Range(5f, 50f)] public float acceleration = 25.0f;
    [Tooltip("Deceleration applied when no input is given.")]
    [Range(5f, 50f)] public float deceleration = 30.0f;

    // --- JUMPING & GRAVITY ---
    [Header("Jumping & Gravity")]
    [Tooltip("The force applied when jumping.")]
    [Range(1f, 5f)] public float jumpHeight = 2.0f;
    [Tooltip("Amount of horizontal control the player has in the air (0 = none, 1 = full).")]
    [Range(0f, 1f)] public float airControl = 0.5f;
    [Tooltip("The force of gravity applied.")]
    public float gravity = -15.0f;

    // --- CAMERA LOOK TUNING ---
    [Header("Camera Look Tuning")]
    [Tooltip("Sensitivity multiplier for horizontal and vertical look.")]
    [Range(0.1f, 10f)] public float lookSensitivity = 1.0f;
    [Tooltip("The maximum vertical angle the player can look up (in degrees).")]
    [Range(70f, 90f)] public float verticalLookClampMax = 85.0f;
    [Tooltip("The minimum vertical angle the player can look down (in degrees).")]
    [Range(-90f, -70f)] public float verticalLookClampMin = -85.0f;

    // --- PRIVATE RUNTIME VARIABLES ---
    private Vector3 _currentVelocity;
    private float _verticalVelocity;
    private float _cameraPitch = 0.0f;

    // --- UNITY LIFECYCLE ---

    private void Start()
    {
        // Safety checks for required components
        if (_controller == null)
        {
            _controller = GetComponent<CharacterController>();
        }
        if (_cameraTransform == null)
        {
            // Try to find the camera automatically (must be tagged "MainCamera")
            if (Camera.main != null)
            {
                _cameraTransform = Camera.main.transform;
            }
            else
            {
                Debug.LogError("Main Camera not assigned and not found via tag. Look will not function.");
            }
        }
        
        // Lock the cursor to the center of the screen
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        HandleLook();
        HandleMovement();
    }

    // --- INPUT HANDLERS ---

    private void HandleLook()
    {
        if (_cameraTransform == null) return;

        // Get raw look input from the input script
        Vector2 lookInput = inputScript.look;

        // 1. Horizontal Rotation (Applied to the entire body)
        float yaw = lookInput.x * lookSensitivity;
        transform.Rotate(Vector3.up * yaw);

        // 2. Vertical Rotation (Applied to the camera only)
        float pitch = lookInput.y * lookSensitivity;// * -1f;  Inverted for typical FPS look

        // Clamp the vertical angle (pitch)
        _cameraPitch = Mathf.Clamp(_cameraPitch + pitch, verticalLookClampMin, verticalLookClampMax);

        // Apply rotation to the camera
        _cameraTransform.localRotation = Quaternion.Euler(_cameraPitch, 0f, 0f);
    }

    private void HandleMovement()
    {
        if (_controller == null) return;

        // Determine current desired speed
        float targetSpeed = inputScript.sprint ? sprintSpeed : moveSpeed;
        
        // 1. Calculate desired horizontal movement vector
        Vector3 targetDirection = new Vector3(inputScript.move.x, 0f, inputScript.move.y);
        targetDirection = transform.rotation * targetDirection.normalized; // Convert to world space

        // 2. Determine current horizontal velocity (for acceleration logic)
        Vector3 horizontalVelocity = new Vector3(_currentVelocity.x, 0f, _currentVelocity.z);

        // 3. Acceleration and Deceleration
        if (targetDirection.magnitude > 0)
        {
            // Apply acceleration to move towards the target speed
            horizontalVelocity = Vector3.MoveTowards(
                horizontalVelocity,
                targetDirection * targetSpeed,
                acceleration * Time.deltaTime
            );
        }
        else
        {
            // Apply deceleration to slow down when not moving
            horizontalVelocity = Vector3.MoveTowards(
                horizontalVelocity,
                Vector3.zero,
                deceleration * Time.deltaTime
            );
        }

        // --- JUMPING & GRAVITY ---

        if (_controller.isGrounded)
        {
            // Reset vertical velocity when grounded
            _verticalVelocity = gravity;

            // Handle Jump Input
            if (inputScript.jump)
            {
                // Simple jump formula (v = sqrt(h * -2 * g))
                _verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
                // Consume the jump input immediately after use
                inputScript.jump = false; 
            }
        }
        else
        {
            // Apply air control while mid-air
            Vector3 airControlVector = targetDirection * targetSpeed * airControl;

            horizontalVelocity = Vector3.Lerp(
                horizontalVelocity, 
                horizontalVelocity + airControlVector, 
                Time.deltaTime
            );

            // Apply gravity
            _verticalVelocity += gravity * Time.deltaTime;
        }
        
        // 4. Final Movement Application
        _currentVelocity = horizontalVelocity + Vector3.up * _verticalVelocity;
        _controller.Move(_currentVelocity * Time.deltaTime);
    }
}
