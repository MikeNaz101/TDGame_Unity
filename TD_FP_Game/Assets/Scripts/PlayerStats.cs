using UnityEngine;

public class PlayerStats : MonoBehaviour
{
    [Header("Health & Energy")]
    public float maxHealth = 100f;
    public float currentHealth;
    public float maxEnergy = 100f;
    public float currentEnergy;

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
        // Note: HUD update logic would typically be called here if not using continuous binding
    }

    // --- TOWER BUILDING LOGIC (Called by WeaponController) ---

    /// <summary>
    /// Attempts to build a tower at the specified location if the player has enough resources.
    /// </summary>
    /// <param name="towerIndex">The index of the tower type to build (from TowerManager list).</param>
    /// <param name="position">The world position where the tower should be placed.</param>
    /// <returns>True if the tower was successfully built, False otherwise.</returns>
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
