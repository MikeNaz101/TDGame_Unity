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
    public int scrapMetal = 0; 

    [Header("Game References")]
    public TowerManager towerManager; 

    void Start()
    {
        currentHealth = maxHealth;
        currentEnergy = maxEnergy;

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
        Debug.Log("Player has died! Implementing game over.");
        gameObject.SetActive(false); 
    }

    // --- RESOURCE HANDLERS ---

    public void AddScrap(int amount)
    {
        scrapMetal += amount;
        // Debug.Log("Scrap Metal: " + scrapMetal);
    }

    // --- NEW: SPENDING LOGIC ---
    public bool SpendScrap(int amount)
    {
        if (scrapMetal >= amount)
        {
            scrapMetal -= amount;
            return true; // Purchase successful
        }
        return false; // Not enough money
    }

    // --- TOWER BUILDING LOGIC ---
    public bool TryBuildTower(int towerIndex, Vector3 position)
    {
        if (towerManager == null) return false;

        int cost = towerManager.GetTowerCost(towerIndex);

        if (SpendScrap(cost)) // Use the new method
        {
            towerManager.BuildTower(towerIndex, position);
            Debug.Log($"Built tower {towerIndex} for {cost} scrap.");
            return true;
        }
        else
        {
            Debug.LogWarning($"Not enough scrap metal! Need {cost}, have {scrapMetal}.");
            return false;
        }
    }
}