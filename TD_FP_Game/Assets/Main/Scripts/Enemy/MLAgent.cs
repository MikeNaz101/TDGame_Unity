using UnityEngine;

/// <summary>
/// Adaptive difficulty: watches the player's accuracy and switches every active enemy's stance.
/// </summary>
public class MLAgent : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The player's WeaponControllerHS script. Found automatically if empty.")]
    public WeaponControllerHS playerWeaponController;

    [Header("Tuning")]
    [Tooltip("How many shots the player must fire before we check accuracy.")]
    public int shotsRequired = 10;
    [Tooltip("How often (seconds) to evaluate.")]
    public float evaluationInterval = 0.5f;

    [Space(10)]
    [Tooltip("Accuracy ABOVE this value triggers Evasive stance.")]
    [Range(0.1f, 1.0f)] public float highAccuracyThreshold = 0.75f;
    [Tooltip("Accuracy BELOW this value triggers Aggressive stance.")]
    [Range(0.1f, 1.0f)] public float lowAccuracyThreshold = 0.25f;

    public EnemyController.AIStance CurrentStance { get; private set; } = EnemyController.AIStance.Standard;
    public float LastAccuracy { get; private set; }

    private float _timer;
    private bool _warned;

    void Start()
    {
        if (playerWeaponController == null) playerWeaponController = FindFirstObjectByType<WeaponControllerHS>();
    }

    void Update()
    {
        if (playerWeaponController == null)
        {
            if (!_warned)
            {
                Debug.LogWarning("MLAgent is missing a reference to the WeaponControllerHS!", this);
                _warned = true;
            }
            return;
        }

        _timer -= Time.deltaTime;
        if (_timer > 0f) return;
        _timer = evaluationInterval;

        if (playerWeaponController.shotsFired < shotsRequired) return;

        LastAccuracy = playerWeaponController.GetAccuracy();

        EnemyController.AIStance newStance = EnemyController.AIStance.Standard;
        if (LastAccuracy >= highAccuracyThreshold) newStance = EnemyController.AIStance.Evasive;
        else if (LastAccuracy <= lowAccuracyThreshold) newStance = EnemyController.AIStance.Aggressive;

        CurrentStance = newStance;
        var enemies = EnemyController.ActiveEnemies;
        for (int i = 0; i < enemies.Count; i++)
        {
            if (enemies[i] != null) enemies[i].SetAIStance(newStance);
        }

        playerWeaponController.shotsFired = 0;
        playerWeaponController.shotsHit = 0;
    }
}
