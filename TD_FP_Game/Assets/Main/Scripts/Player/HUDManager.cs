using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Event-driven HUD. Nothing here rebuilds strings every frame; each element updates only when
/// its source value changes. Every reference is optional, so wire up what you have.
/// </summary>
public class HUDManager : MonoBehaviour
{
    [Header("Sources")]
    public PlayerStats playerStats;
    [Tooltip("Found automatically if left empty.")]
    public WeaponControllerHS weaponController;
    [Tooltip("Found automatically if left empty.")]
    public WaveManager waveManager;
    [Tooltip("Found automatically if left empty.")]
    public CoreHealth coreHealth;

    [Header("Player")]
    public TextMeshProUGUI scrapText;
    public Slider healthSlider;
    public TextMeshProUGUI healthText;
    [Tooltip("Full-screen image flashed red when the player takes damage.")]
    public Image damageFlash;
    public float damageFlashDuration = 0.35f;
    public Color damageFlashColor = new Color(1f, 0f, 0f, 0.35f);

    [Header("Weapon")]
    public TextMeshProUGUI ammoText;
    public TextMeshProUGUI weaponNameText;
    public GameObject reloadingIndicator;
    public Color ammoNormalColor = Color.white;
    public Color ammoLowColor = new Color(1f, 0.4f, 0.3f);
    [Range(0f, 1f)] public float lowAmmoFraction = 0.25f;

    [Header("Hit Marker")]
    public GameObject hitMarker;
    public float hitMarkerDuration = 0.12f;
    [Tooltip("Optional separate marker shown on a killing blow.")]
    public GameObject killMarker;
    public AudioSource audioSource;
    public AudioClip hitMarkerSound;
    public AudioClip killSound;

    [Header("Waves")]
    public TextMeshProUGUI waveText;
    public TextMeshProUGUI waveTimerText;
    public TextMeshProUGUI enemiesRemainingText;
    [Tooltip("Shown briefly when a wave starts / is cleared.")]
    public TextMeshProUGUI announcementText;
    public float announcementDuration = 3f;

    [Header("Core")]
    public Slider coreHealthSlider;
    public TextMeshProUGUI coreHealthText;

    private float _hitMarkerTimer;
    private float _killMarkerTimer;
    private float _damageFlashTimer;
    private float _announcementTimer;
    private int _lastPrepSeconds = -1;
    private WaveManager.WavePhase _phase;

    void Start()
    {
        if (playerStats == null) playerStats = PlayerStats.Instance != null ? PlayerStats.Instance : FindFirstObjectByType<PlayerStats>();
        if (weaponController == null) weaponController = FindFirstObjectByType<WeaponControllerHS>();
        if (waveManager == null) waveManager = WaveManager.Instance != null ? WaveManager.Instance : FindFirstObjectByType<WaveManager>();
        if (coreHealth == null) coreHealth = CoreHealth.Instance != null ? CoreHealth.Instance : FindFirstObjectByType<CoreHealth>();

        Subscribe();

        // Initial state
        if (playerStats != null)
        {
            HandleHealthChanged(playerStats.currentHealth, playerStats.maxHealth);
            HandleScrapChanged(playerStats.scrapMetal);
        }
        if (weaponController != null)
        {
            HandleWeaponChanged(weaponController.CurrentWeapon);
            HandleAmmoChanged(weaponController.CurrentAmmo, weaponController.CurrentReserveAmmo);
        }
        if (coreHealth != null) HandleCoreHealthChanged(coreHealth.CurrentHealth, coreHealth.maxHealth);
        if (waveManager != null) HandleEnemyCountChanged(waveManager.EnemiesAlive);

        if (hitMarker != null) hitMarker.SetActive(false);
        if (killMarker != null) killMarker.SetActive(false);
        if (reloadingIndicator != null) reloadingIndicator.SetActive(false);
        if (announcementText != null) announcementText.gameObject.SetActive(false);
        if (damageFlash != null) damageFlash.color = new Color(damageFlashColor.r, damageFlashColor.g, damageFlashColor.b, 0f);
        if (waveTimerText != null) waveTimerText.text = "";
    }

    void OnDestroy()
    {
        Unsubscribe();
    }

    private void Subscribe()
    {
        if (playerStats != null)
        {
            playerStats.OnHealthChanged += HandleHealthChanged;
            playerStats.OnScrapChanged += HandleScrapChanged;
            playerStats.OnDamaged += HandleDamaged;
        }
        if (weaponController != null)
        {
            weaponController.OnAmmoChanged += HandleAmmoChanged;
            weaponController.OnWeaponChanged += HandleWeaponChanged;
            weaponController.OnEnemyHit += HandleEnemyHit;
            weaponController.OnReloadStateChanged += HandleReloadStateChanged;
        }
        if (waveManager != null)
        {
            waveManager.OnPreparationStarted += HandlePreparationStarted;
            waveManager.OnWaveStarted += HandleWaveStarted;
            waveManager.OnWaveCleared += HandleWaveCleared;
            waveManager.OnEnemyCountChanged += HandleEnemyCountChanged;
            waveManager.OnPhaseChanged += HandlePhaseChanged;
        }
        if (coreHealth != null)
        {
            coreHealth.OnHealthChanged += HandleCoreHealthChanged;
        }
    }

    private void Unsubscribe()
    {
        if (playerStats != null)
        {
            playerStats.OnHealthChanged -= HandleHealthChanged;
            playerStats.OnScrapChanged -= HandleScrapChanged;
            playerStats.OnDamaged -= HandleDamaged;
        }
        if (weaponController != null)
        {
            weaponController.OnAmmoChanged -= HandleAmmoChanged;
            weaponController.OnWeaponChanged -= HandleWeaponChanged;
            weaponController.OnEnemyHit -= HandleEnemyHit;
            weaponController.OnReloadStateChanged -= HandleReloadStateChanged;
        }
        if (waveManager != null)
        {
            waveManager.OnPreparationStarted -= HandlePreparationStarted;
            waveManager.OnWaveStarted -= HandleWaveStarted;
            waveManager.OnWaveCleared -= HandleWaveCleared;
            waveManager.OnEnemyCountChanged -= HandleEnemyCountChanged;
            waveManager.OnPhaseChanged -= HandlePhaseChanged;
        }
        if (coreHealth != null)
        {
            coreHealth.OnHealthChanged -= HandleCoreHealthChanged;
        }
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;

        if (_hitMarkerTimer > 0f)
        {
            _hitMarkerTimer -= dt;
            if (_hitMarkerTimer <= 0f && hitMarker != null) hitMarker.SetActive(false);
        }
        if (_killMarkerTimer > 0f)
        {
            _killMarkerTimer -= dt;
            if (_killMarkerTimer <= 0f && killMarker != null) killMarker.SetActive(false);
        }
        if (_damageFlashTimer > 0f && damageFlash != null)
        {
            _damageFlashTimer -= dt;
            float a = Mathf.Clamp01(_damageFlashTimer / damageFlashDuration) * damageFlashColor.a;
            damageFlash.color = new Color(damageFlashColor.r, damageFlashColor.g, damageFlashColor.b, a);
        }
        if (_announcementTimer > 0f)
        {
            _announcementTimer -= dt;
            if (_announcementTimer <= 0f && announcementText != null) announcementText.gameObject.SetActive(false);
        }

        // Countdown only re-renders when the whole second changes.
        if (waveTimerText != null && waveManager != null && _phase == WaveManager.WavePhase.Preparing)
        {
            int seconds = Mathf.CeilToInt(waveManager.PreparationTimeRemaining);
            if (seconds != _lastPrepSeconds)
            {
                _lastPrepSeconds = seconds;
                waveTimerText.text = seconds > 0 ? $"Next wave in {seconds}" : "";
            }
        }
    }

    // --- Player ---

    private void HandleHealthChanged(float current, float max)
    {
        if (healthSlider != null)
        {
            healthSlider.maxValue = max;
            healthSlider.value = current;
        }
        if (healthText != null) healthText.text = $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";
    }

    private void HandleScrapChanged(int scrap)
    {
        if (scrapText != null) scrapText.text = "Scrap: " + scrap;
    }

    private void HandleDamaged(float amount)
    {
        _damageFlashTimer = damageFlashDuration;
    }

    // --- Weapon ---

    private void HandleAmmoChanged(int current, int reserve)
    {
        if (ammoText == null) return;
        ammoText.text = $"{current} / {reserve}";
        int max = weaponController != null && weaponController.CurrentWeapon != null ? weaponController.CurrentWeapon.maxAmmo : 0;
        bool low = max > 0 && current <= Mathf.CeilToInt(max * lowAmmoFraction);
        ammoText.color = low ? ammoLowColor : ammoNormalColor;
    }

    private void HandleWeaponChanged(WeaponData weapon)
    {
        if (weaponNameText != null) weaponNameText.text = weapon != null ? weapon.weaponName : "";
    }

    private void HandleReloadStateChanged(bool reloading)
    {
        if (reloadingIndicator != null) reloadingIndicator.SetActive(reloading);
    }

    private void HandleEnemyHit(bool killed)
    {
        if (hitMarker != null)
        {
            hitMarker.SetActive(true);
            _hitMarkerTimer = hitMarkerDuration;
        }
        if (killed && killMarker != null)
        {
            killMarker.SetActive(true);
            _killMarkerTimer = hitMarkerDuration * 2f;
        }
        if (audioSource != null)
        {
            AudioClip clip = killed && killSound != null ? killSound : hitMarkerSound;
            if (clip != null) audioSource.PlayOneShot(clip);
        }
    }

    // --- Waves ---

    private void HandlePhaseChanged(WaveManager.WavePhase phase)
    {
        _phase = phase;
        _lastPrepSeconds = -1;
        if (phase != WaveManager.WavePhase.Preparing && waveTimerText != null) waveTimerText.text = "";
    }

    private void HandlePreparationStarted(int waveNumber, float seconds)
    {
        if (waveText != null) waveText.text = $"Wave {waveNumber}";
        Announce(waveNumber > 1 ? $"Wave {waveNumber - 1} cleared!" : "Get ready...");
    }

    private void HandleWaveStarted(int waveNumber, WaveConfig config)
    {
        if (waveText != null) waveText.text = $"Wave {waveNumber}";
        Announce($"Wave {waveNumber} incoming!");
    }

    private void HandleWaveCleared(int waveNumber)
    {
        // Preparation of the next wave announces the clear; nothing extra needed here.
    }

    private void HandleEnemyCountChanged(int alive)
    {
        if (enemiesRemainingText != null) enemiesRemainingText.text = alive > 0 ? $"Enemies: {alive}" : "";
    }

    private void Announce(string message)
    {
        if (announcementText == null) return;
        announcementText.text = message;
        announcementText.gameObject.SetActive(true);
        _announcementTimer = announcementDuration;
    }

    // --- Core ---

    private void HandleCoreHealthChanged(float current, float max)
    {
        if (coreHealthSlider != null)
        {
            coreHealthSlider.maxValue = max;
            coreHealthSlider.value = current;
        }
        if (coreHealthText != null) coreHealthText.text = $"Core: {Mathf.CeilToInt(current)}";
    }
}
