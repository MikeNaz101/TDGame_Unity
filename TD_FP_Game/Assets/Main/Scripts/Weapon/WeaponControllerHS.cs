using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using StarterAssets;
using Unity.Cinemachine; 

[RequireComponent(typeof(AudioSource))]
public class WeaponControllerHS : MonoBehaviour
{
    // --- EXTERNAL REFERENCES ---
    [Header("Setup")]
    public CameraShake cameraShake;
    public Camera mainCamera;
    [Tooltip("Assign your Player's Virtual Camera here if using Cinemachine.")]
    public CinemachineCamera playerVirtualCamera; 

    public float rangeToZoomRatio = 50f; 
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
    public WeaponData[] inventory; // Drag ALL possible weapons here

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
    
    private float baseFOV; 

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
        
        if (transform.parent != null) hipPosition = transform.localPosition;
        
        if (mainCamera == null && cameraShake != null) mainCamera = cameraShake.GetComponentInParent<Camera>();

        if (playerVirtualCamera != null) baseFOV = playerVirtualCamera.Lens.FieldOfView;
        else if (mainCamera != null) baseFOV = mainCamera.fieldOfView;
        else Debug.LogError("No Camera assigned to WeaponControllerHS!");
        
        // Equip the first unlocked weapon
        EquipFirstUnlocked();

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

            if (inputScript.reload && !isReloading)
            {
                int maxClip = GetUpgradedClipSize();
                if (currentAmmo < maxClip)
                {
                    inputScript.reload = false; 
                    StartCoroutine(Reload());
                }
            }
        }
        wasFiringLastFrame = inputScript.fire;
    }

    // --- NEW: SMART SWITCHING LOGIC ---
    void HandleDirectSwitching()
    {
        int direction = 0;
        bool shouldSwitch = false;
        if (inventory.Length <= 1) return;

        if (inputScript.weapon3) { direction = 1; inputScript.weapon3 = false; shouldSwitch = true; }
        else if (inputScript.weapon2) { direction = -1; inputScript.weapon2 = false; shouldSwitch = true; }
        
        if (shouldSwitch)
        {
            // Find next UNLOCKED weapon index
            int newIndex = GetNextUnlockedIndex(direction);
            if (newIndex != -1 && newIndex != currentWeaponIndex)
            {
                EquipWeapon(newIndex);
            }
        }
    }

    private int GetNextUnlockedIndex(int direction)
    {
        if (inventory.Length == 0) return -1;
        
        int checkIndex = currentWeaponIndex;
        int attempts = 0;

        // Loop until we find an unlocked weapon or check them all
        while (attempts < inventory.Length)
        {
            checkIndex = (checkIndex + direction + inventory.Length) % inventory.Length;
            
            if (IsWeaponUnlocked(inventory[checkIndex]))
            {
                return checkIndex;
            }
            attempts++;
        }
        return currentWeaponIndex; // Fallback to current if nothing else found
    }

    private void EquipFirstUnlocked()
    {
        for (int i = 0; i < inventory.Length; i++)
        {
            if (IsWeaponUnlocked(inventory[i]))
            {
                EquipWeapon(i);
                return;
            }
        }
    }

    private bool IsWeaponUnlocked(WeaponData weapon)
    {
        if (UpgradeManager.Instance == null) return true; // Default to unlocked if manager missing
        return UpgradeManager.Instance.IsWeaponUnlocked(weapon.weaponName);
    }
    // ----------------------------------

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

    void HandleADS()
    {
        Vector3 targetLocalPosition = hipPosition;
        float targetFOV = baseFOV;
        bool isAiming = inputScript.aim && !isReloading;
        
        if (isAiming)
        {
            if (adsTarget != null) targetLocalPosition = adsTarget.localPosition;
            float zoomFactor = 1f + (currentWeapon.range / rangeToZoomRatio);
            targetFOV = Mathf.Clamp(baseFOV / zoomFactor, 10f, baseFOV);
        }
        
        if (hipCrosshairUI != null) hipCrosshairUI.SetActive(!isAiming);
        if (currentAdsReticleInstance != null) currentAdsReticleInstance.SetActive(isAiming);

        float lerpSpeed = currentWeapon != null ? currentWeapon.adsSpeed : 10f;
        transform.localPosition = Vector3.Lerp(transform.localPosition, targetLocalPosition, Time.deltaTime * lerpSpeed);
        
        if (playerVirtualCamera != null)
        {
            var lens = playerVirtualCamera.Lens;
            lens.FieldOfView = Mathf.Lerp(lens.FieldOfView, targetFOV, Time.deltaTime * lerpSpeed);
            playerVirtualCamera.Lens = lens;
        }
        else if (mainCamera != null)
        {
            mainCamera.fieldOfView = Mathf.Lerp(mainCamera.fieldOfView, targetFOV, Time.deltaTime * lerpSpeed);
        }
    }

    void Shoot()
    {
        if (currentAmmo <= 0) return; 
        currentAmmo--;

        float fireRateMult = 1f;
        float damageMult = 1f;

        if (UpgradeManager.Instance != null)
        {
            fireRateMult = UpgradeManager.Instance.GetWeaponFireRateMult(currentWeapon.weaponName);
            damageMult = UpgradeManager.Instance.GetWeaponDamageMult(currentWeapon.weaponName);
        }
        
        nextFireTime = currentWeapon.fireRate / fireRateMult;

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
                //enemy.TakeDamage(currentWeapon.damage * damageMult, transform.root);
                enemy.TakeDamage(currentWeapon.damage * damageMult, transform.root, currentWeapon.damageType);
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
        
        int maxClip = GetUpgradedClipSize();
        int maxReserve = GetUpgradedMaxAmmo();

        int ammoNeeded = maxClip - currentAmmo;
        // Simplified ammo logic: Just refill from infinite pool based on max capacity
        // If you want finite reserve, logic goes here.
        
        currentAmmo = maxClip; // Full reload
        
        isReloading = false;
    }
    
    private int GetUpgradedClipSize()
    {
        float mult = 1f;
        if (UpgradeManager.Instance != null) mult = UpgradeManager.Instance.GetWeaponClipSizeMult(currentWeapon.weaponName);
        return Mathf.RoundToInt(currentWeapon.maxAmmo * mult); 
    }

    private int GetUpgradedMaxAmmo()
    {
        float mult = 1f;
        if (UpgradeManager.Instance != null) mult = UpgradeManager.Instance.GetWeaponMaxAmmoMult(currentWeapon.weaponName);
        return Mathf.RoundToInt(currentWeapon.reserveAmmo * mult);
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
        
        currentAmmo = GetUpgradedClipSize();
        currentReserveAmmo = GetUpgradedMaxAmmo();
        
        isReloading = false;
        nextFireTime = 0f;
    }
}