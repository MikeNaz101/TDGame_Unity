using UnityEngine;

public class PlayerStats : MonoBehaviour
{
    // Global Player Variables
    public float maxHealth = 100f;
    public float currentHealth;
    public int scrapMetal = 0; // The resource variable
    public float energy = 100f;

    void Start()
    {
        currentHealth = maxHealth;
    }

    // Method called by the Enemy to inflict damage
    public void TakeDamage(float amount)
    {
        currentHealth -= amount;
        
        // Ensure health doesn't go below zero
        if (currentHealth < 0)
        {
            currentHealth = 0;
        }

        Debug.Log("Player Health: " + currentHealth);

        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    // Method to add scrap when the player picks it up
    public void AddScrap(int amount)
    {
        scrapMetal += amount;
        Debug.Log("Scrap Metal: " + scrapMetal);
    }

    // Method to deduct scrap when building a tower
    public bool TryDeductScrap(int cost)
    {
        if (scrapMetal >= cost)
        {
            scrapMetal -= cost;
            Debug.Log("Scrap deducted. Remaining: " + scrapMetal);
            return true;
        }
        return false;
    }

    void Die()
    {
        Debug.Log("Player has fallen! Game Over!");
        // TODO: Implement actual game over screen and restart logic
        Time.timeScale = 0f; // Pauses the game immediately
    }
}