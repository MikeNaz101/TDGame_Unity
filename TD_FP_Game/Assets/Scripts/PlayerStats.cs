using UnityEngine;

public class PlayerStats : MonoBehaviour
{
    // Global Player Variables
    public float maxHealth = 100f;
    public float currentHealth;
    public int scrapMetal = 0;
    public float energy = 100f;

    void Start()
    {
        currentHealth = maxHealth;
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
}
