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
    public CameraShake cameraShake;
    
    [Header("Hit Effects")]
    public GameObject hitParticlePrefab;
    public AudioClip hitSound;
    [Tooltip("Intensity of the shake when hurt (different from explosion).")]
    public float hitShakeIntensity = 0.8f; 
    [Tooltip("How long the hit shake lasts.")]
    public float hitShakeDuration = 0.2f;

    void Start()
    {
        currentHealth = maxHealth;
        currentEnergy = maxEnergy;

        if (towerManager == null) towerManager = Object.FindFirstObjectByType<TowerManager>();
        if (cameraShake == null) cameraShake = Object.FindFirstObjectByType<CameraShake>();
    }

    // --- DAMAGE & HEALTH ---

    public void TakeDamage(float amount)
    {
        currentHealth -= amount;
        PlayHitEffects();
        Debug.Log($"Player took {amount} damage. Health: {currentHealth}");
        if (currentHealth <= 0)
        {
            Die();
        }
    }
    
    private void PlayHitEffects()
    {
        // 1. Camera Shake (Distinct Jolt)
        if (cameraShake != null)
        {
            // We reuse the existing Shake method but with specific "Hurt" values
            cameraShake.Shake(hitShakeIntensity, hitShakeDuration);
        }

        // 2. Audio
        if (hitSound != null)
        {
            // Play at camera position so it sounds "in your head"
            AudioSource.PlayClipAtPoint(hitSound, transform.position);
        }

        // 3. Particle (e.g., Screen Blood or Sparks)
        if (hitParticlePrefab != null)
        {
            // Instantiate slightly in front of the camera if it's a world particle
            // OR simply instantiate it if it's a UI canvas prefab
            Instantiate(hitParticlePrefab, transform.position + transform.forward * 0.5f, transform.rotation);
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