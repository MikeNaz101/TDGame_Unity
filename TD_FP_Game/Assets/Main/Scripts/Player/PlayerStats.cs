using System;
using UnityEngine;

public class PlayerStats : MonoBehaviour, IDamageable
{
    public static PlayerStats Instance { get; private set; }

    [Header("Health & Energy")]
    public float maxHealth = 100f;
    public float currentHealth;
    public float maxEnergy = 100f;
    public float currentEnergy;

    [Header("Health Regeneration (CoD style)")]
    [Tooltip("Enable passive health regeneration after not taking damage for a while.")]
    public bool regenerateHealth = true;
    [Tooltip("Seconds without damage before regeneration starts.")]
    public float regenDelay = 4f;
    [Tooltip("Health restored per second while regenerating.")]
    public float regenRate = 15f;

    [Header("Resources")]
    public int scrapMetal = 0;

    [Header("Game References")]
    public TowerManager towerManager;

    [Header("Feedback (optional)")]
    public AudioSource audioSource;
    public AudioClip hurtSound;
    public AudioClip scrapPickupSound;
    public CameraShake cameraShake;
    [Tooltip("Camera kick applied when the player takes damage.")]
    public float damageShake = 0.4f;

    // --- Events for HUD / other systems ---
    public event Action<float, float> OnHealthChanged;   // current, max
    public event Action<int> OnScrapChanged;             // new total
    public event Action<float> OnDamaged;                // amount
    public event Action OnDied;

    public bool IsDead { get; private set; }
    private float _timeSinceDamage;

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        currentHealth = maxHealth;
        currentEnergy = maxEnergy;
        _timeSinceDamage = regenDelay;

        if (towerManager == null) towerManager = FindFirstObjectByType<TowerManager>();
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        if (cameraShake == null) cameraShake = GetComponentInChildren<CameraShake>();

        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        OnScrapChanged?.Invoke(scrapMetal);
    }

    void Update()
    {
        if (IsDead || !regenerateHealth) return;

        _timeSinceDamage += Time.deltaTime;
        if (_timeSinceDamage >= regenDelay && currentHealth < maxHealth)
        {
            currentHealth = Mathf.Min(maxHealth, currentHealth + regenRate * Time.deltaTime);
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
        }
    }

    // --- DAMAGE & HEALTH ---

    public void TakeDamage(float amount)
    {
        if (IsDead || amount <= 0f) return;

        currentHealth = Mathf.Max(0f, currentHealth - amount);
        _timeSinceDamage = 0f;

        if (audioSource != null && hurtSound != null) audioSource.PlayOneShot(hurtSound);
        if (cameraShake != null) cameraShake.Shake(damageShake, 0.2f);

        OnDamaged?.Invoke(amount);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    public void Heal(float amount)
    {
        if (IsDead || amount <= 0f) return;
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    void Die()
    {
        if (IsDead) return;
        IsDead = true;
        OnDied?.Invoke();

        if (GameManager.Instance != null)
        {
            GameManager.Instance.GameOver("You were killed.");
        }
        else
        {
            Debug.Log("Player has died and no GameManager is present in the scene.");
            gameObject.SetActive(false);
        }
    }

    // --- RESOURCE HANDLERS ---

    public void AddScrap(int amount)
    {
        if (amount == 0) return;
        scrapMetal = Mathf.Max(0, scrapMetal + amount);
        if (amount > 0 && audioSource != null && scrapPickupSound != null)
        {
            audioSource.PlayOneShot(scrapPickupSound);
        }
        OnScrapChanged?.Invoke(scrapMetal);
    }

    public bool CanAfford(int cost) => scrapMetal >= cost;

    /// <summary>Attempts to spend scrap. Returns false (and spends nothing) if the player can't afford it.</summary>
    public bool TrySpendScrap(int cost)
    {
        if (cost < 0 || scrapMetal < cost) return false;
        scrapMetal -= cost;
        OnScrapChanged?.Invoke(scrapMetal);
        return true;
    }

    // --- TOWER BUILDING LOGIC (Called by BuildManager) ---
    public bool TryBuildTower(int towerIndex, Vector3 position)
    {
        if (towerManager == null)
        {
            Debug.LogError("Tower Manager is not assigned. Cannot build tower.");
            return false;
        }

        int cost = towerManager.GetTowerCost(towerIndex);
        if (!TrySpendScrap(cost))
        {
            return false;
        }

        if (towerManager.BuildTower(towerIndex, position) == null)
        {
            // Refund if the instantiate failed for any reason.
            AddScrap(cost);
            return false;
        }
        return true;
    }
}
