using UnityEngine;

[RequireComponent(typeof(UnityEngine.AI.NavMeshObstacle))]
public class DoorHealth : MonoBehaviour, IDamageable
{
    // --- NEW: GLOBAL TRACKING ---
    public static int DestroyedDoorCount = 0;
    // ----------------------------

    [Header("Door Stats")]
    public float maxHealth = 200f;

    private float currentHealth;
    public Transform transform => base.transform;

    void Start()
    {
        currentHealth = maxHealth;
    }

    public void TakeDamage(float amount)
    {
        currentHealth -= amount;
        // Debug.Log($"Door at {transform.position} took {amount} damage.");

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        // Increment the global counter
        DestroyedDoorCount++;
        Debug.Log($"Door Destroyed! Total Destroyed: {DestroyedDoorCount}");
        
        Destroy(gameObject);
    }
}