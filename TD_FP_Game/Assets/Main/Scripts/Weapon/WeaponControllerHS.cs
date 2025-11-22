using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using StarterAssets;
// 1. ADD THIS NAMESPACE
using Unity.Cinemachine; 

[RequireComponent(typeof(AudioSource))]
public class WeaponControllerHS : MonoBehaviour
{
    // --- EXTERNAL REFERENCES ---
    [Header("Setup")]
    public CameraShake cameraShake;
    public Camera mainCamera;
    
    // 2. ADD REFERENCE FOR CINEMACHINE CAMERA
    [Tooltip("Assign your Player's Virtual Camera here if using Cinemachine.")]
    public CinemachineCamera playerVirtualCamera; 

    public float rangeToZoomRatio = 90f; // Lower this if zoom is still too weak
    public Transform adsTarget;
    public LayerMask hitScanLayer;
    public StarterAssetsInputs inputScript; 
    public BuildManager buildManager;

    [Header("UI")]
    public GameObject hipCrosshairUI;
    public Transform adsReticleParent;
    
    [Header("Audio")]
    public AudioClip emptyClipSound;
    
    [Header("Weapon Data")]
    public WeaponData[] inventory;

    // --- PRIVATE STATE ---
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
    
    private float baseFOV; // Stored starting FOV

    // --- PUBLIC PROPERTIES ---
    [HideInInspector] public int shotsFired = 0;
    [HideInInspector] public int shotsHit = 0;
    public bool IsReloading => isReloading;
    public Transform ShootPoint => shootPoint;
    
    private Transform gunHolder; 
    private Transform shootPoint; 
    private LineRenderer currentGunTracer;
    private PlayerStats playerStats; 
    private AudioSource _audioSource;

    void Start()
    {
        playerStats = GetComponentInParent<PlayerStats>();
        gunHolder = this.transform; 
        _audioSource = GetComponent<AudioSource>();
        
        if (transform.parent != null)
        {
            hipPosition = transform.localPosition;
        }
        
        if (mainCamera == null && cameraShake != null)
        {
            mainCamera = cameraShake.GetComponentInParent<Camera>();
        }

        // 3. GRAB THE ORIGINAL FOV CORRECTLY
        if (playerVirtualCamera != null)
        {
            baseFOV = playerVirtualCamera.Lens.FieldOfView;
        }
        else if (mainCamera != null)
        {
            baseFOV = mainCamera.fieldOfView;
        }
        else
        {
            Debug.LogError("No Camera assigned to WeaponControllerHS!");
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
        if (nextFireTime > 0f) nextFireTime -= Time.deltaTime;

        if (isShootingEnabled)
        {
            HandleDirectSwitching();
            HandleADS(); 
            HandleFiringInput();

            if (inputScript.reload && !isReloading && currentAmmo < currentWeapon.maxAmmo)
            {
                inputScript.reload = false; 
                StartCoroutine(Reload());
            }
        }
        wasFiringLastFrame = inputScript.fire;
    }

    void HandleDirectSwitching()
    {
        int direction = 0;
        bool shouldSwitch = false;
        if (inventory.Length <= 1) return;

        if (inputScript.weapon3) { direction = 1; inputScript.weapon3 = false; shouldSwitch = true; }
        else if (inputScript.weapon2) { direction = -1; inputScript.weapon2 = false; shouldSwitch = true; }
        
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

    void HandleFiringInput()
    {
        bool shotReady = nextFireTime <= 0f;
        if (!isReloading && shotReady)
        {
            if (currentWeapon.isAutomatic) { if (inputScript.fire) Shoot(); }
            else { if (inputScript.fire && !wasFiringLastFrame) Shoot(); }
        }
        else if (inputScript.fire && !wasFiringLastFrame && currentAmmo <= 0 && !isReloading)
        {
            if (emptyClipSound != null) _audioSource.PlayOneShot(emptyClipSound);
        }
    }

    // 4. UPDATED ADS LOGIC FOR CINEMACHINE
    void HandleADS()
    {
        Vector3 targetLocalPosition = hipPosition;
        float targetFOV = baseFOV;
        bool isAiming = inputScript.aim && !isReloading;
        
        if (isAiming)
        {
            if (adsTarget != null) targetLocalPosition = adsTarget.localPosition;
            
            // ZOOM MATH: Divisor formula
            float zoomFactor = 1f + (currentWeapon.range / rangeToZoomRatio);
            targetFOV = Mathf.Clamp(baseFOV / zoomFactor, 10f, baseFOV);
        }
        
        if (hipCrosshairUI != null) hipCrosshairUI.SetActive(!isAiming);
        if (currentAdsReticleInstance != null) currentAdsReticleInstance.SetActive(isAiming);

        // Move Gun
        float lerpSpeed = currentWeapon != null ? currentWeapon.adsSpeed : 10f;
        transform.localPosition = Vector3.Lerp(transform.localPosition, targetLocalPosition, Time.deltaTime * lerpSpeed);
        
        // 5. APPLY FOV TO CINEMACHINE OR MAIN CAMERA
        if (playerVirtualCamera != null)
        {
            // Update Cinemachine Lens
            var lens = playerVirtualCamera.Lens;
            lens.FieldOfView = Mathf.Lerp(lens.FieldOfView, targetFOV, Time.deltaTime * lerpSpeed);
            playerVirtualCamera.Lens = lens;
        }
        else if (mainCamera != null)
        {
            // Update Standard Camera
            mainCamera.fieldOfView = Mathf.Lerp(mainCamera.fieldOfView, targetFOV, Time.deltaTime * lerpSpeed);
        }
    }
    
    void Shoot()
    {
        if (currentAmmo <= 0) return; 
        
        currentAmmo--;

        // --- NEW: Get Multiplier for THIS specific weapon name ---
        float fireRateMult = 1f;
        float damageMult = 1f;

        if (UpgradeManager.Instance != null)
        {
            // Ask the manager for stats specific to "Pistol" or "Rocket Launcher"
            fireRateMult = UpgradeManager.Instance.GetWeaponFireRateMult(currentWeapon.weaponName);
            damageMult = UpgradeManager.Instance.GetWeaponDamageMult(currentWeapon.weaponName);
        }
        
        nextFireTime = currentWeapon.fireRate / fireRateMult;
        // ----------------------------------------------------------

        shotsFired++; 
        if (currentWeapon.shootSound != null) _audioSource.PlayOneShot(currentWeapon.shootSound);
        if(shootPoint != null) EnemyAIAudioEvents.ReportGunshot(shootPoint.position); 

        Ray ray = new Ray(mainCamera.transform.position, mainCamera.transform.forward);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, currentWeapon.range, hitScanLayer))
        {
            EnemyController enemy = hit.collider.GetComponentInParent<EnemyController>();
            if (enemy != null) 
            { 
                // Apply specific damage mult
                enemy.TakeDamage(currentWeapon.damage * damageMult, transform.root); 
                shotsHit++; 
            }
        }
        if (cameraShake != null) cameraShake.Shake(currentWeapon.recoilKickback);
        StartCoroutine(FlashMuzzle());
        inputScript.fire = false; 
    }

    IEnumerator FlashMuzzle()
    {
        if (currentGunTracer == null || shootPoint == null) yield break; 
        Ray ray = new Ray(mainCamera.transform.position, mainCamera.transform.forward);
        RaycastHit hit;
        Vector3 targetPoint;
        if (Physics.Raycast(ray, out hit, currentWeapon.range, hitScanLayer)) targetPoint = hit.point; 
        else targetPoint = ray.GetPoint(currentWeapon.range); 

        currentGunTracer.SetPosition(0, shootPoint.position);
        currentGunTracer.SetPosition(1, targetPoint);
        currentGunTracer.enabled = true;
        yield return new WaitForSeconds(0.05f); 
        currentGunTracer.enabled = false;
    }

    IEnumerator Reload()
    {
        if (isReloading) yield break; 
        isReloading = true;
        if (currentWeapon.reloadSound != null) _audioSource.PlayOneShot(currentWeapon.reloadSound);
        yield return new WaitForSeconds(currentWeapon.reloadTime);
        int ammoNeeded = currentWeapon.maxAmmo - currentAmmo;
        int ammoToUse = Mathf.Min(ammoNeeded, currentReserveAmmo);
        currentAmmo += ammoToUse;
        currentReserveAmmo -= ammoToUse;
        isReloading = false;
    }
    
    public float GetAccuracy() { if (shotsFired == 0) return 0f; return (float)shotsHit / (float)shotsFired; }
    public void ToggleShootingEnabled(bool state) { isShootingEnabled = state; }

    void EquipWeapon(int weaponIndex)
    {
        if (weaponIndex < 0 || weaponIndex >= inventory.Length) return;
        if (currentGunInstance != null) Destroy(currentGunInstance);
        if (currentAdsReticleInstance != null) Destroy(currentAdsReticleInstance);

        currentWeaponIndex = weaponIndex;
        currentWeapon = inventory[currentWeaponIndex]; 
        if (currentWeapon.equipSound != null) _audioSource.PlayOneShot(currentWeapon.equipSound);

        currentGunInstance = Instantiate(currentWeapon.weaponPrefab, gunHolder);
        if (currentWeapon.adsReticlePrefab != null && adsReticleParent != null) {
            currentAdsReticleInstance = Instantiate(currentWeapon.adsReticlePrefab, adsReticleParent);
            currentAdsReticleInstance.SetActive(false); 
        } else currentAdsReticleInstance = null; 

        currentGunTracer = currentGunInstance.GetComponentInChildren<LineRenderer>();
        shootPoint = currentGunInstance.transform.Find("ShootPoint");
        currentAmmo = currentWeapon.maxAmmo;
        currentReserveAmmo = currentWeapon.reserveAmmo;
        isReloading = false;
        nextFireTime = 0f;
    }
}