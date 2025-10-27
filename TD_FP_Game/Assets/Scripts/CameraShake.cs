using UnityEngine;
using System.Collections;

public class CameraShake : MonoBehaviour
{
    // The original, resting local position of the camera
    private Vector3 originalPos;

    // How quickly the camera snaps back to its original position after the shake
    [Tooltip("How fast the camera returns to its original position.")]
    public float returnSpeed = 10f;

    // How aggressively the camera shakes (amplitude)
    [Tooltip("Multiplier for the shake amount.")]
    public float shakeIntensity = 0.5f; 

    // How quickly the random shake positions are generated (frequency)
    [Tooltip("Speed of the random noise generation.")]
    public float shakeSpeed = 50f;

    // --- Private Shake State ---
    private float shakeDuration = 0f;
    private float shakeAmount = 0f;

    void Start()
    {
        // Store the camera's starting position relative to its parent (the player's head)
        originalPos = transform.localPosition;
    }

    void Update()
    {
        // If we are currently shaking
        if (shakeDuration > 0)
        {
            // Reduce the remaining shake duration over time
            shakeDuration -= Time.deltaTime;
            
            // Calculate a random, smooth offset using Perlin Noise for shaking
            // We use Time.time * shakeSpeed to create a continuous, random-looking pattern.
            float offsetX = (Mathf.PerlinNoise(Time.time * shakeSpeed, 0) * 2 - 1) * shakeAmount * shakeIntensity;
            float offsetY = (Mathf.PerlinNoise(0, Time.time * shakeSpeed) * 2 - 1) * shakeAmount * shakeIntensity;

            // Apply the offset to the camera's local position
            transform.localPosition = originalPos + new Vector3(offsetX, offsetY, 0);

            // Gradually decrease the shake intensity as the duration runs out
            shakeAmount = Mathf.Lerp(shakeAmount, 0f, Time.deltaTime * returnSpeed);
        }
        else
        {
            // If the shake duration is zero, smoothly return the camera to its original position
            shakeDuration = 0f;
            transform.localPosition = Vector3.Lerp(transform.localPosition, originalPos, Time.deltaTime * returnSpeed);
        }
    }

    /// <summary>
    /// Initiates a camera shake. This is called from the WeaponController.
    /// </summary>
    /// <param name="kickbackAmount">The raw kickback value from WeaponData (recoilKickback).</param>
    /// <param name="duration">How long the effect should last (usually a small fraction of a second).</param>
    public void Shake(float kickbackAmount, float duration = 0.15f)
    {
        // Reset the duration and set the initial intensity based on the weapon's kickback
        shakeDuration = duration;
        shakeAmount = kickbackAmount;
    }
}
