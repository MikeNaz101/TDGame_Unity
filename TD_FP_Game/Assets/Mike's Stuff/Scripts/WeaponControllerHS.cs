using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq; 
using StarterAssets; // Required for StarterAssetsInputs

// This line automatically adds an AudioSource component when you add this script.
[RequireComponent(typeof(AudioSource))]
public class WeaponControllerHS : MonoBehaviour
{
    // --- EXTERNAL REFERENCES ---
    [Header("Setup")]
    [Tooltip("The player camera's shake script for recoil effects.")]
    public CameraShake cameraShake;
    [Tooltip("The main player camera that will be zoomed.")]
    public Camera mainCamera;
    [Tooltip("How much 'range' affects zoom. Higher number = LESS zoom.")]
    public float rangeToZoomRatio = 20f;
    [Tooltip("The object that marks the ADS position.")]
    public Transform adsTarget;
    [Tooltip("The layer mask used for hit scanning (Enemies & Environment).")]
    public LayerMask hitScanLayer;
    [Tooltip("The script that holds the current state of player inputs.")]
    public StarterAssetsInputs inputScript; 
    [Tooltip("The dedicated script that handles all building FSM logic.")]
    public BuildManager buildManager;

    [Header("UI")]
    [Tooltip("The simple, static crosshair for hip fire.")]
    public GameObject hipCrosshairUI;
    [Tooltip("The parent RectTransform on your Canvas where ADS reticles will be spawned.")]
    public Transform adsReticleParent;
    
    [Header("Audio")]
    [Tooltip("The sound to play when the player tries to fire an empty clip.")]
    public AudioClip emptyClipSound;
    
    [Header("Weapon Data")]
    [Tooltip("List of all weapons the player can switch between.")]
    public WeaponData[] inventory;

    // --- PRIVATE STATE VARIABLES ---
    private GameObject currentGunInstance;
    private GameObject currentAdsReticleInstance;
    private WeaponData currentWeapon;
    private float nextFireTime;
    private int currentAmmo;
    private int currentReserveAmmo;
    private bool isReloading = false;
    private bool wasFiringLastFrame = false;
    private int currentWeaponIndex = 0;
    private Vector3 hipPosition;
    private bool isShootingEnabled = true;
    private float originalFOV; 

    // --- PUBLIC PROPERTIES (For BuildManager to Read) ---
    public bool IsReloading => isReloading;
    public Transform ShootPoint => shootPoint;
    
    // --- PRIVATE REFERENCES ---
    private Transform gunHolder; 
    private Transform shootPoint; 
    private LineRenderer currentGunTracer;
    private PlayerStats playerStats; 
    private AudioSource _audioSource; // For playing sounds

    // --- UNITY LIFECYCLE ---

    void Start()
    {
        playerStats = GetComponentInParent<PlayerStats>();
        gunHolder = this.transform; 
        _audioSource = GetComponent<AudioSource>(); // Get the AudioSource
        
        if (transform.parent != null)
        {
            hipPosition = transform.localPosition;
        }
        
        // Find camera from CameraShake script if not assigned
        if (mainCamera == null && cameraShake != null)
        {
            mainCamera = cameraShake.GetComponentInParent<Camera>();
        }

        if (mainCamera != null)
        {
            originalFOV = mainCamera.fieldOfView;
        }
        else
        {
            Debug.LogError("Main Camera reference is missing on WeaponControllerHS!");
        }
        
        if (inventory.Length > 0)
        {
            EquipWeapon(currentWeaponIndex); 
        }

        if(hipCrosshairUI != null) hipCrosshairUI.SetActive(true);
    }

    void Update()
    {
        if (currentWeapon == null || playerStats == null) return;

        // Update Cooldown
        if (nextFireTime > 0f)
        {
            nextFireTime -= Time.deltaTime;
        }

        if (isShootingEnabled)
        {
            HandleDirectSwitching();
            HandleADS(); 
            HandleFiringInput();

            // Handle Manual Reload
            if (inputScript.reload && !isReloading && currentAmmo < currentWeapon.maxAmmo)
            {
                inputScript.reload = false; 
                StartCoroutine(Reload());
            }
        }
        
        wasFiringLastFrame = inputScript.fire;
    }

    // --- INPUT HANDLERS (Combat) ---

    void HandleFiringInput()
    {
        bool shotReady = nextFireTime <= 0f;

        if (!isReloading && shotReady)
        {
            if (currentWeapon.isAutomatic) 
            {
                if (inputScript.fire) Shoot();
            }
            else // Semi-Automatic
            {
                if (inputScript.fire && !wasFiringLastFrame) Shoot();
            }
        }
        // Play empty clip sound
        else if (inputScript.fire && !wasFiringLastFrame && currentAmmo <= 0 && !isReloading)
        {
            if (emptyClipSound != null) _audioSource.PlayOneShot(emptyClipSound);
        }
    }
    
    void HandleDirectSwitching()
    {
        int direction = 0;
        bool shouldSwitch = false;

        if (inventory.Length <= 1) return;

        // Consume input and determine direction
        if (inputScript.weapon3) 
        {
            direction = 1;
            inputScript.weapon3 = false; 
            shouldSwitch = true;
        }
        else if (inputScript.weapon2) 
        {
            direction = -1;
            inputScript.weapon2 = false; 
            shouldSwitch = true;
        }
        
        if (shouldSwitch)
        {
            int newIndex = (currentWeaponIndex + direction + inventory.Length) % inventory.Length;
            if (newIndex != currentWeaponIndex)
            {
                currentWeaponIndex = newIndex;
                EquipWeapon(currentWeaponIndex);
            }
        }
    }
    
    // --- COMBAT & MOVEMENT HANDLERS ---
    
    void HandleADS()
    {
        Vector3 targetLocalPosition = hipPosition;
        float targetFOV = originalFOV;
        bool isAiming = inputScript.aim && !isReloading;
        
        if (isAiming)
        {
            // Set gun position
            if (adsTarget != null)
            {
                targetLocalPosition = adsTarget.localPosition;
            }
            
            // Set FOV
            float zoomAmount = (currentWeapon.range / rangeToZoomRatio);
            targetFOV = Mathf.Clamp(originalFOV - zoomAmount, 10f, originalFOV);
        }
        
        // Toggle Hip Crosshair
        if (hipCrosshairUI != null)
        {
            hipCrosshairUI.SetActive(!isAiming);
        }
        
        // Toggle weapon-specific ADS Reticle
        if (currentAdsReticleInstance != null)
        {
            currentAdsReticleInstance.SetActive(isAiming);
        }

        // Lerp gun position
        float lerpSpeed = currentWeapon != null ? currentWeapon.adsSpeed : 10f;
        transform.localPosition = Vector3.Lerp(transform.localPosition, targetLocalPosition, Time.deltaTime * lerpSpeed);
        
        // Lerp camera FOV
        if (mainCamera != null)
        {
            mainCamera.fieldOfView = Mathf.Lerp(mainCamera.fieldOfView, targetFOV, Time.deltaTime * lerpSpeed);
        }
    }

    void Shoot()
    {
        // Check for Ammunition
        if (currentAmmo <= 0)
        {
            return; 
        }

        // Decrement Ammo & Set Cooldown
        currentAmmo--;
        nextFireTime = currentWeapon.fireRate;

        // Play shoot sound
        if (currentWeapon.shootSound != null)
        {
            _audioSource.PlayOneShot(currentWeapon.shootSound);
        }

        // Report gunshot to AI
        if(shootPoint != null)
        {
            // This is the static event call for the Roamer AI
            EnemyAIAudioEvents.ReportGunshot(shootPoint.position); 
        }

        // Parallax-Fixed Raycast from Camera
        Ray ray = new Ray(mainCamera.transform.position, mainCamera.transform.forward);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, currentWeapon.range, hitScanLayer))
        {
            // Damage Enemy
            EnemyController enemy = hit.collider.GetComponentInParent<EnemyController>();
            if (enemy != null)
            {
                // Pass attacker transform (the Player) to the enemy
                enemy.TakeDamage(currentWeapon.damage, transform.root); 
            }
        }

        // Recoil
        if (cameraShake != null)
        {
            cameraShake.Shake(currentWeapon.recoilKickback);
        }
        
        // Muzzle Flash
        StartCoroutine(FlashMuzzle());
        
        // Consume Input
        inputScript.fire = false; 
    }

    // --- COROUTINES ---

    IEnumerator FlashMuzzle()
    {
        if (currentGunTracer == null || shootPoint == null) yield break; 

        // Find target for laser (Parallax-Fixed)
        Ray ray = new Ray(mainCamera.transform.position, mainCamera.transform.forward);
        RaycastHit hit;
        Vector3 targetPoint;

        if (Physics.Raycast(ray, out hit, currentWeapon.range, hitScanLayer))
        {
            targetPoint = hit.point; // Hit a wall/enemy
        }
        else
        {
            targetPoint = ray.GetPoint(currentWeapon.range); // Hit nothing
        }

        // Draw line from gun muzzle to reticle target
        currentGunTracer.SetPosition(0, shootPoint.position);
        currentGunTracer.SetPosition(1, targetPoint);
        
        currentGunTracer.enabled = true;
        yield return new WaitForSeconds(0.05f); // Flash duration
        currentGunTracer.enabled = false;
    }

    IEnumerator Reload()
    {
        if (isReloading) yield break; // Prevent multiple reloads
        isReloading = true;
        
        // Play reload sound
        if (currentWeapon.reloadSound != null)
        {
            _audioSource.PlayOneShot(currentWeapon.reloadSound);
        }
        
        yield return new WaitForSeconds(currentWeapon.reloadTime);
        
        int ammoNeeded = currentWeapon.maxAmmo - currentAmmo;
        int ammoToUse = Mathf.Min(ammoNeeded, currentReserveAmmo);
        currentAmmo += ammoToUse;
        currentReserveAmmo -= ammoToUse;
        isReloading = false;
    }
    
    // --- PUBLIC METHODS ---
    
    // Allows BuildManager to temporarily halt weapon operation
    public void ToggleShootingEnabled(bool state)
    {
        isShootingEnabled = state;
    }

    // --- WEAPON EQUIP METHOD ---
    void EquipWeapon(int weaponIndex)
    {
        if (weaponIndex < 0 || weaponIndex >= inventory.Length) return;

        // Destroy old gun and reticle
        if (currentGunInstance != null) Destroy(currentGunInstance);
        if (currentAdsReticleInstance != null) Destroy(currentAdsReticleInstance);

        // Set new WeaponData
        currentWeaponIndex = weaponIndex;
        currentWeapon = inventory[currentWeaponIndex]; 

        // Play equip sound
        if (currentWeapon.equipSound != null)
        {
            _audioSource.PlayOneShot(currentWeapon.equipSound);
        }

        // Instantiate new gun model
        currentGunInstance = Instantiate(currentWeapon.weaponPrefab, gunHolder);
        
        // Instantiate new reticle
        if (currentWeapon.adsReticlePrefab != null && adsReticleParent != null)
        {
            currentAdsReticleInstance = Instantiate(currentWeapon.adsReticlePrefab, adsReticleParent);
            currentAdsReticleInstance.SetActive(false); // Hide it
        }
        else
        {
            currentAdsReticleInstance = null; 
            if(adsReticleParent == null) Debug.LogWarning("AdsReticleParent is not assigned on WeaponControllerHS!");
        }

        // Find components on new gun
        currentGunTracer = currentGunInstance.GetComponentInChildren<LineRenderer>();
        shootPoint = currentGunInstance.transform.Find("ShootPoint");

        // Set new weapon stats
        currentAmmo = currentWeapon.maxAmmo;
        currentReserveAmmo = currentWeapon.reserveAmmo;
        isReloading = false;
        nextFireTime = 0f;

        // Safety Checks
        if (currentGunTracer == null) Debug.LogError($"Weapon '{currentWeapon.weaponName}' prefab is missing a LineRenderer!");
        if (shootPoint == null) Debug.LogError($"Weapon '{currentWeapon.weaponName}' prefab is missing a 'ShootPoint' object!");
    }
}

