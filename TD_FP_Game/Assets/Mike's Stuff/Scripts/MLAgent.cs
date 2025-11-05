using UnityEngine;

/// <summary>
/// Manages the adaptive AI difficulty by checking player accuracy
/// and adjusting enemy behavior (stance).
/// </summary>
public class MLAgent : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Drag the Player object's WeaponControllerHS script here.")]
    public WeaponControllerHS playerWeaponController;

    [Header("Tuning")]
    [Tooltip("How many shots the player must fire before we check accuracy.")]
    public int shotsRequired = 10;
    
    [Space(10)]
    [Tooltip("Accuracy ABOVE this value triggers Evasive stance.")]
    [Range(0.1f, 1.0f)]
    public float highAccuracyThreshold = 0.75f; // 75%
    
    [Tooltip("Accuracy BELOW this value triggers Aggressive stance.")]
    [Range(0.1f, 1.0f)]
    public float lowAccuracyThreshold = 0.25f; // 25%

    void Update()
    {
        if (playerWeaponController == null)
        {
            Debug.LogWarning("MLAgent is missing a reference to the PlayerWeaponController!");
            return;
        }

        // 1. Check if the player has fired enough shots
        if (playerWeaponController.shotsFired >= shotsRequired)
        {
            // 2. Calculate accuracy
            float accuracy = playerWeaponController.GetAccuracy();

            // 3. Determine the new AI stance
            EnemyController.AIStance newStance = EnemyController.AIStance.Standard;
            if (accuracy >= highAccuracyThreshold)
            {
                // Player is doing well, make enemies harder
                newStance = EnemyController.AIStance.Evasive; 
            }
            else if (accuracy <= lowAccuracyThreshold)
            {
                // Player is struggling, make enemies easier (more predictable/closer)
                newStance = EnemyController.AIStance.Aggressive; 
            }

            // 4. Apply the new stance to all active enemies
            foreach (EnemyController enemy in EnemyController.ActiveEnemies)
            {
                // The enemy script handles null/destroyed objects by removing itself
                enemy.SetAIStance(newStance);
            }

            // 5. Reset the counters
            playerWeaponController.shotsFired = 0;
            playerWeaponController.shotsHit = 0;
        }
    }
}