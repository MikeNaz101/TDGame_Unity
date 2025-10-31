using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq; 
using StarterAssets; // Required to access the public input booleans

public class WeaponControllerHS : MonoBehaviour
{
    // --- EXTERNAL REFERENCES ---
    [Header("Setup")]
    public CameraShake cameraShake;
    public Camera mainCamera;
    public float rangeToZoomRatio = 20f;
    public Transform adsTarget;
    public LayerMask hitScanLayer;
    public StarterAssetsInputs inputScript; 
    public BuildManager buildManager;

    // --- NEW UI FIELDS ---
    [Header("UI")]
    [Tooltip("The simple, static crosshair for hip fire.")]
    public GameObject hipCrosshairUI;
    [Tooltip("The parent RectTransform on your Canvas where ADS reticles will be spawned.")]
    public Transform adsReticleParent;
    // --- END NEW UI FIELDS ---
    
    [Header("Weapon Data")]
    public WeaponData[] inventory;

    // --- PRIVATE STATE VARIABLES ---
    private GameObject currentGunInstance;
    private GameObject currentAdsReticleInstance; // To hold the spawned ADS reticle
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

    // --- (Properties are unchanged) ---
    public bool IsReloading => isReloading;
    public Transform ShootPoint => shootPoint;
    
    // --- (Private references are unchanged) ---
    private Transform gunHolder; 
    private Transform shootPoint; 
    private LineRenderer currentGunTracer;
    private PlayerStats playerStats; 

    // --- UNITY LIFECYCLE ---

    void Start()
    {
        playerStats = GetComponentInParent<PlayerStats>();
        gunHolder = this.transform; 
        
        if (transform.parent != null)
        {
            hipPosition = transform.localPosition;
        }
        
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

        // Ensure hip crosshair is visible at start
        if(hipCrosshairUI != null) hipCrosshairUI.SetActive(true);
    }

    void Update()
    {
        if (currentWeapon == null || playerStats == null) return;

        if (nextFireTime > 0f)
        {
            nextFireTime -= Time.deltaTime;
        }

        if (isShootingEnabled)
        {
            HandleDirectSwitching();
            HandleADS(); // This is now updated
            HandleFiringInput();

            if (inputScript.reload && !isReloading && currentAmmo < currentWeapon.maxAmmo)
            {
                inputScript.reload = false; 
                StartCoroutine(Reload());
            }
        }
        
        wasFiringLastFrame = inputScript.fire;
    }

    // --- (HandleFiringInput and HandleDirectSwitching are unchanged) ---
    void HandleFiringInput()
    {
        bool shotReady = nextFireTime <= 0f;

        if (!isReloading && shotReady)
        {
            if (currentWeapon.isAutomatic) 
            {
                if (inputScript.fire) Shoot();
            }
            else 
            {
                if (inputScript.fire && !wasFiringLastFrame) Shoot();
            }
        }
    }
    
    void HandleDirectSwitching()
    {
        int direction = 0;
        bool shouldSwitch = false;

        if (inventory.Length <= 1) return;

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
    
    // --- UPDATED METHOD ---
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
        
        // --- ADDED UI LOGIC ---
        // Toggle the Hip Crosshair
        if (hipCrosshairUI != null)
        {
            hipCrosshairUI.SetActive(!isAiming);
        }
        
        // Toggle the weapon-specific ADS Reticle
        if (currentAdsReticleInstance != null)
        {
            currentAdsReticleInstance.SetActive(isAiming);
        }
        // --- END ADDED UI LOGIC ---

        // Lerp gun position
        float lerpSpeed = currentWeapon != null ? currentWeapon.adsSpeed : 10f;
        transform.localPosition = Vector3.Lerp(transform.localPosition, targetLocalPosition, Time.deltaTime * lerpSpeed);
        
        // Lerp camera FOV
        if (mainCamera != null)
        {
            mainCamera.fieldOfView = Mathf.Lerp(mainCamera.fieldOfView, targetFOV, Time.deltaTime * lerpSpeed);
        }
    }

    // --- (Shoot, FlashMuzzle, and Reload are unchanged) ---
    void Shoot()
    {
        if (currentAmmo <= 0)
        {
            if (currentReserveAmmo > 0 && !isReloading) StartCoroutine(Reload()); 
            return; 
        }
        currentAmmo--;
        nextFireTime = currentWeapon.fireRate;

        RaycastHit hit;
        if (Physics.Raycast(shootPoint.position, shootPoint.forward, out hit, currentWeapon.range, hitScanLayer))
        {
            EnemyController enemy = hit.collider.GetComponentInParent<EnemyController>();
            if (enemy != null) enemy.TakeDamage(currentWeapon.damage);
        }
        if (cameraShake != null) cameraShake.Shake(currentWeapon.recoilKickback);
        StartCoroutine(FlashMuzzle());
        inputScript.fire = false; 
    }

    IEnumerator FlashMuzzle()
    {
        if (currentGunTracer == null || shootPoint == null) yield break; 

        RaycastHit hit;
        Vector3 startPoint = shootPoint.position;
        Vector3 endPoint;
        float range = currentWeapon.range;

        if (Physics.Raycast(startPoint, shootPoint.forward, out hit, range, hitScanLayer))
        {
            endPoint = hit.point;
        }
        else
        {
            endPoint = startPoint + shootPoint.forward * range;
        }
        currentGunTracer.SetPosition(0, startPoint);
        currentGunTracer.SetPosition(1, endPoint);
        currentGunTracer.enabled = true;
        yield return new WaitForSeconds(0.05f); 
        currentGunTracer.enabled = false;
    }

    IEnumerator Reload()
    {
        isReloading = true;
        yield return new WaitForSeconds(currentWeapon.reloadTime);
        int ammoNeeded = currentWeapon.maxAmmo - currentAmmo;
        int ammoToUse = Mathf.Min(ammoNeeded, currentReserveAmmo);
        currentAmmo += ammoToUse;
        currentReserveAmmo -= ammoToUse;
        isReloading = false;
    }
    
    // --- (ToggleShootingEnabled is unchanged) ---
    public void ToggleShootingEnabled(bool state)
    {
        isShootingEnabled = state;
    }

    // --- UPDATED METHOD ---
    void EquipWeapon(int weaponIndex)
    {
        if (weaponIndex < 0 || weaponIndex >= inventory.Length) return;

        // 1. Destroy old gun
        if (currentGunInstance != null) Destroy(currentGunInstance);
        
        // --- ADDED: Destroy old reticle ---
        if (currentAdsReticleInstance != null) Destroy(currentAdsReticleInstance);

        // 2. Set new WeaponData
        currentWeaponIndex = weaponIndex;
        currentWeapon = inventory[currentWeaponIndex]; 

        // 3. Instantiate new gun model
        currentGunInstance = Instantiate(currentWeapon.weaponPrefab, gunHolder);
        
        // --- ADDED: Instantiate new reticle ---
        if (currentWeapon.adsReticlePrefab != null && adsReticleParent != null)
        {
            currentAdsReticleInstance = Instantiate(currentWeapon.adsReticlePrefab, adsReticleParent);
            currentAdsReticleInstance.SetActive(false); // Hide it immediately
        }
        else
        {
            currentAdsReticleInstance = null; // Ensure it's null if one doesn't exist
            if(adsReticleParent == null) Debug.LogWarning("AdsReticleParent is not assigned on WeaponControllerHS!");
        }
        // --- END ADDED BLOCK ---

        // 4. Find components on the new gun
        currentGunTracer = currentGunInstance.GetComponentInChildren<LineRenderer>();
        shootPoint = currentGunInstance.transform.Find("ShootPoint");

        // 5. Set new weapon stats
        currentAmmo = currentWeapon.maxAmmo;
        currentReserveAmmo = currentWeapon.reserveAmmo;
        isReloading = false;
        nextFireTime = 0f;

        // 6. Safety Checks
        if (currentGunTracer == null) Debug.LogError($"Weapon '{currentWeapon.weaponName}' prefab is missing a LineRenderer!");
        if (shootPoint == null) Debug.LogError($"Weapon '{currentWeapon.weaponName}' prefab is missing a 'ShootPoint' object!");
    }
}