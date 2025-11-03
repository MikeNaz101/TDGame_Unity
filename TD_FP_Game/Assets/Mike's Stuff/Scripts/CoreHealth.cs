using UnityEngine;

public class CoreHealth : MonoBehaviour, IDamageable
{
    [Header("Core Stats")]
    [Tooltip("The total health of the Core.")]
    public float maxHealth = 1000f;

    private float currentHealth;
    public Transform transform => base.transform;

    void Start()
    {
        currentHealth = maxHealth;
    }

    public void TakeDamage(float amount)
    {
        currentHealth -= amount;
        Debug.Log($"CORE TOOK {amount} DAMAGE! {currentHealth} HEALTH REMAINING!");

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        // Game Over
        Debug.LogError("GAME OVER: The Core has been destroyed!");
        Time.timeScale = 0; 
    }
}