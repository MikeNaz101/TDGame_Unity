using UnityEngine;

public class PlayerStats : MonoBehaviour, IDamageable
{
    [Header("Health & Energy")]
    public float maxHealth = 100f;
    public float currentHealth;
    public float maxEnergy = 100f;
    public float currentEnergy;
    
    public Transform transform => base.transform;

    [Header("Resources")]
    public int scrapMetal = 0; // Public field for the collected resource

    [Header("Game References")]
    public TowerManager towerManager; // Assigned in Inspector

    void Start()
    {
        currentHealth = maxHealth;
        currentEnergy = maxEnergy;

        // Find Tower Manager automatically if not set
        if (towerManager == null)
        {
            towerManager = FindObjectOfType<TowerManager>();
        }
    }

    // --- DAMAGE & HEALTH ---

    public void TakeDamage(float amount)
    {
        currentHealth -= amount;
        if (currentHealth <= 0)
        {
            Die();
        }
    }
    
    public void Heal(float amount)
    {
        currentHealth += amount;
        if (currentHealth > maxHealth)
        {
            currentHealth = maxHealth;
        }
    }

    void Die()
    {
        // TODO: Implement game over or player respawn logic
        Debug.Log("Player has died! Implementing game over.");
        gameObject.SetActive(false); 
    }

    // --- RESOURCE HANDLERS ---

    public void AddScrap(int amount)
    {
        scrapMetal += amount;
        Debug.Log("Scrap Metal: " + scrapMetal);
    }

    // --- TOWER BUILDING LOGIC (Called by WeaponController) ---
    // Attempts to build a tower at the specified location if the player has enough resources.
    public bool TryBuildTower(int towerIndex, Vector3 position)
    {
        if (towerManager == null)
        {
            Debug.LogError("Tower Manager is not assigned. Cannot build tower.");
            return false;
        }

        // 1. Get the cost of the selected tower
        int cost = towerManager.GetTowerCost(towerIndex);

        // 2. Check resource requirement
        if (scrapMetal >= cost)
        {
            // 3. Subtract cost and build
            scrapMetal -= cost;
            towerManager.BuildTower(towerIndex, position);
            Debug.Log($"Built tower {towerIndex} for {cost} scrap. Remaining scrap: {scrapMetal}");
            return true;
        }
        else
        {
            Debug.LogWarning($"Not enough scrap metal! Need {cost}, have {scrapMetal}.");
            // Optionally play a sound or show a UI notification for failure
            return false;
        }
    }
}
