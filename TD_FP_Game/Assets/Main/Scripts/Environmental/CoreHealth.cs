using UnityEngine;

public class CoreHealth : MonoBehaviour, IDamageable
{
    [Header("Core Stats")]
    public float maxHealth = 1000f;
    
    // --- NEW: SECURITY SETTINGS ---
    [Header("Security Fail-Safe")]
    [Tooltip("How many doors must be destroyed before the Core takes damage?")]
    public int requiredDoorsDestroyed = 1;
    [Tooltip("Radius around the core to scan for cheating enemies.")]
    public float securityScanRadius = 10f;
    // ------------------------------

    private float currentHealth;
    public Transform transform => base.transform;

    void Start()
    {
        currentHealth = maxHealth;
        // Reset door count on level load (optional, depends on your scene structure)
        // DoorHealth.DestroyedDoorCount = 0; 
    }

    public void TakeDamage(float amount)
    {
        // 1. CHECK: Are the doors destroyed?
        if (DoorHealth.DestroyedDoorCount < requiredDoorsDestroyed)
        {
            Debug.LogWarning("Core attacked prematurely! Activating Security Protocol.");
            TriggerSecurityPulse();
            return; // IGNORE DAMAGE
        }

        // 2. Normal Damage Logic
        currentHealth -= amount;
        Debug.Log($"CORE TOOK {amount} DAMAGE! {currentHealth} HEALTH REMAINING!");

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    // Finds nearby enemies and punishes them
    private void TriggerSecurityPulse()
    {
        // Find all colliders near the core
        Collider[] hits = Physics.OverlapSphere(transform.position, securityScanRadius);

        foreach (Collider hit in hits)
        {
            // Check if it's an enemy
            EnemyController enemy = hit.GetComponent<EnemyController>();
            
            // Also check parent in case collider is on a child part
            if (enemy == null) enemy = hit.GetComponentInParent<EnemyController>();

            if (enemy != null)
            {
                // YEET THE ENEMY
                enemy.PunishTrespasser();
            }
        }
    }

    private void Die()
    {
        Debug.LogError("GAME OVER: The Core has been destroyed!");
        Time.timeScale = 0; 
    }
    
    // Visualize the scan radius in the editor
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, securityScanRadius);
    }
}