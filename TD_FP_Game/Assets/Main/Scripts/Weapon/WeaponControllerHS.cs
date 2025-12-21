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
    public GameObject tracerPrefab; // Assign a prefab with a LineRenderer attached
    
    [Header("Lock-On Settings")]
    public float lockOnRange = 50f;
    public float lockOnRadius = 1.5f; 
    public LayerMask enemyLayer;
    public float timeToLock = 2.0f; // --- NEW: Duration required
    
    [Header("Reticle Feedback")]
    public Color defaultReticleColor = Color.white;
    public Color acquiringReticleColor = Color.yellow; // --- NEW: In-progress color
    public Color lockedReticleColor = Color.green;
    
    [Header("Animation")]
    [Tooltip("Drag the FPS Arms (with the Animator) here.")]
    public Animator armsAnimator;

    // --- NEW: State Variables ---
    private EnemyController _pendingTarget; // Enemy currently under crosshair
    private EnemyController _finalLockedTarget; // Enemy fully locked (can shoot anywhere)
    private float _currentLockTimer = 0f;
    private UnityEngine.UI.Image _currentReticleImage;
    //private EnemyController _currentLockTarget;
    
    // --- PUBLIC GETTERS FOR HUD ---
    public int CurrentClip => currentAmmo;
    public int MaxClip => GetUpgradedClipSize();
    public int CurrentReserve => currentReserveAmmo;
    public int MaxReserve => GetUpgradedMaxAmmo();

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
    private Animator currentGunAnimator;

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
            HandleLockOn();
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
            // --- FIX: Read from Weapon Data instead of static transform ---
            if (currentWeapon != null)
            {
                // This uses the specific offset defined in the scriptable object
                targetLocalPosition = currentWeapon.aimDownSightsPosition;
            }
            else if (adsTarget != null) 
            {
                // Fallback to the physical target if weapon data is missing
                targetLocalPosition = adsTarget.localPosition;
            }
            // -------------------------------------------------------------

            float zoomFactor = 1f + (currentWeapon.range / rangeToZoomRatio);
            targetFOV = Mathf.Clamp(baseFOV / zoomFactor, 10f, baseFOV);
        }
        
        if (hipCrosshairUI != null) hipCrosshairUI.SetActive(!isAiming);
        if (currentAdsReticleInstance != null) currentAdsReticleInstance.SetActive(isAiming);

        float lerpSpeed = currentWeapon != null ? currentWeapon.adsSpeed : 10f;
        
        // Smoothly move the weapon to the calculated target position
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

        // 1. Calculate Multipliers
        float fireRateMult = 1f;
        float damageMult = 1f;

        if (UpgradeManager.Instance != null)
        {
            fireRateMult = UpgradeManager.Instance.GetWeaponFireRateMult(currentWeapon.weaponName);
            damageMult = UpgradeManager.Instance.GetWeaponDamageMult(currentWeapon.weaponName);
        }

        nextFireTime = currentWeapon.fireRate / fireRateMult;
        shotsFired++;

        // 2. Play Effects
        if (currentWeapon.shootSound != null) _audioSource.PlayOneShot(currentWeapon.shootSound);
        if (shootPoint != null) EnemyAIAudioEvents.ReportGunshot(shootPoint.position);
        if (cameraShake != null) cameraShake.Shake(currentWeapon.recoilKickback);

        // --- CASE 1: ROCKET LAUNCHER (Projectile) ---
        if (currentWeapon.projectilePrefab != null)
        {
            // Spawn Rocket
            GameObject proj = Instantiate(currentWeapon.projectilePrefab, shootPoint.position, shootPoint.rotation);

            // Handle Locking Logic (if upgraded)
            SlowHomingRocket rocket = proj.GetComponent<SlowHomingRocket>();
            if (rocket != null)
            {
                rocket.damageMultiplier = damageMult;
                // DEBUG 5: Confirm what is being passed
                if (_finalLockedTarget != null) Debug.Log($"Shoot: Firing homing rocket at {_finalLockedTarget.name}");
                else Debug.Log("Shoot: Firing dumb rocket (No lock).");
                rocket.Initialize(null, _finalLockedTarget, transform.root); 
                rocket.Launch();
                
                // --- Consume the lock (One shot per lock) ---
                ResetLockState();
            }

            // Handle Standard Projectile Logic
            Projectile p = proj.GetComponent<Projectile>();
            if (p != null)
            {
                p.attacker = transform.root;
                p.damageMultiplier = damageMult;
            }

            // Apply Launch Force
            Rigidbody rb = proj.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = mainCamera.transform.forward * currentWeapon.launchForce;
            }

            inputScript.fire = false;
            return; // EXIT FUNCTION HERE for Rockets
        }

        // --- CASE 2: HITSCAN WEAPON (Rifle & Shotgun) ---
        // This logic runs for EVERY hitscan weapon.
        // If it's a Rifle, pellets is 1. If Shotgun, pellets is 8.

        Dictionary<EnemyController, int> enemyHits = new Dictionary<EnemyController, int>();
        int totalPellets = Mathf.Max(1, currentWeapon.pellets); // Ensure at least 1 pellet

        // Create the list to hold visuals
        List<Vector3> pelletHitPoints = new List<Vector3>();

        // Loop through every pellet
        for (int i = 0; i < totalPellets; i++)
        {
            // Calculate Spread
            Vector3 forward = mainCamera.transform.forward;
            if (currentWeapon.spreadAngle > 0)
            {
                float xSpread = Random.Range(-currentWeapon.spreadAngle, currentWeapon.spreadAngle);
                float ySpread = Random.Range(-currentWeapon.spreadAngle, currentWeapon.spreadAngle);
                forward = Quaternion.Euler(xSpread, ySpread, 0) * forward;
            }

            Ray ray = new Ray(mainCamera.transform.position, forward);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit, currentWeapon.range, hitScanLayer))
            {
                // HIT: Add point to visuals
                pelletHitPoints.Add(hit.point);

                EnemyController enemy = hit.collider.GetComponentInParent<EnemyController>();
                if (enemy != null)
                {
                    if (!enemyHits.ContainsKey(enemy)) enemyHits[enemy] = 0;
                    enemyHits[enemy]++;
                }
            }
            else
            {
                // MISS: Add point at max range so visual still draws
                pelletHitPoints.Add(ray.GetPoint(currentWeapon.range));
            }
        }

        // 3. Draw Tracers (Now we are 100% sure the list is full)
        StartCoroutine(FlashMultiMuzzle(pelletHitPoints));

        // 4. Apply Damage
        foreach (var kvp in enemyHits)
        {
            EnemyController enemy = kvp.Key;
            int hits = kvp.Value;

            // Base damage is per pellet * number of hits
            float finalDamage = currentWeapon.damage * hits * damageMult;

            // Check for Full Hit Bonus (Only if shotgun has multiple pellets)
            if (hits == totalPellets && totalPellets > 1)
            {
                finalDamage *= currentWeapon.fullHitBonus;
                Debug.Log($"MEATSHOT! {enemy.name} took bonus damage from full spray.");
            }

            enemy.TakeDamage(finalDamage, transform.root, currentWeapon.damageType);
            shotsHit += hits;
        }

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
    
    IEnumerator FlashMultiMuzzle(List<Vector3> hitPoints)
    {
        // DEBUG 1: Check if the list arrived
        if (hitPoints == null || hitPoints.Count == 0)
        {
            Debug.LogError("FlashMultiMuzzle: No hit points to draw! List is empty or null.");
            yield break;
        }

        // DEBUG 2: Check references
        if (tracerPrefab == null)
        {
            Debug.LogError("FlashMultiMuzzle: Tracer Prefab is NULL! Assign it in the Inspector.");
            yield break;
        }
        if (shootPoint == null)
        {
            Debug.LogError("FlashMultiMuzzle: Shoot Point is NULL!");
            yield break;
        }

        Debug.Log($"FlashMultiMuzzle: Attempting to draw {hitPoints.Count} tracers.");

        foreach (Vector3 hitPoint in hitPoints)
        {
            // 1. Create a temporary tracer object
            GameObject tracerObj = Instantiate(tracerPrefab, shootPoint.position, Quaternion.identity);
        
            // DEBUG 3: Check instantiation
            if (tracerObj == null)
            {
                Debug.LogError("FlashMultiMuzzle: Failed to instantiate tracer!");
                continue;
            }

            LineRenderer lr = tracerObj.GetComponent<LineRenderer>();

            if (lr != null)
            {
                // 2. Set the positions
                lr.SetPosition(0, shootPoint.position);
                lr.SetPosition(1, hitPoint);
            
                // DEBUG 4: Confirm positions
                // Debug.Log($"Drawing line from {shootPoint.position} to {hitPoint}");
            }
            else
            {
                Debug.LogError("FlashMultiMuzzle: Tracer Prefab does not have a LineRenderer component!");
            }

            // 3. Destroy it after a tiny delay
            Destroy(tracerObj, 0.05f);
        }
        yield return null;
    }
    
    public void RefillCurrentReserve()
    {
        currentReserveAmmo = GetUpgradedMaxAmmo();
    }

    IEnumerator Reload()
    {
        if (isReloading) yield break; 
        isReloading = true;

        if (currentWeapon.reloadSound != null) _audioSource.PlayOneShot(currentWeapon.reloadSound);
        
        if (armsAnimator != null)
        {
            armsAnimator.SetTrigger("Reload");
        }
        else
        {
            Debug.LogWarning("Arms Animator is missing! Assign it in WeaponControllerHS.");
        }
        
        yield return new WaitForSeconds(currentWeapon.reloadTime);
        
        int maxClip = GetUpgradedClipSize();
        int ammoNeeded = maxClip - currentAmmo;

        // --- Check Infinite Logic ---
        if (IsAmmoInfinite())
        {
            // FREE RELOAD: Fill clip, do NOT touch reserve
            currentAmmo = maxClip;
        }
        else
        {
            // STANDARD RELOAD: Spend reserve
            int ammoToReload = Mathf.Min(ammoNeeded, currentReserveAmmo);
            if (ammoToReload > 0)
            {
                currentReserveAmmo -= ammoToReload; 
                currentAmmo += ammoToReload;        
            }
        }
        
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
    
    void HandleLockOn()
    {
        // DEBUG 1: Check Basics
        if (mainCamera == null) { Debug.LogError("LockOn: MainCamera is missing!"); return; }
        
        // 2. Requirement Check
        bool isUnlocked = false;
        if (UpgradeManager.Instance != null && UpgradeManager.Instance.IsGuidanceUnlocked(currentWeapon.weaponName))
        {
            isUnlocked = true;
        }

        if (!isUnlocked) 
        { 
            // Only log this once to avoid spam, or check if you definitely bought it
            // Debug.Log("LockOn: Upgrade locked."); 
            ResetLockState(); 
            return; 
        }

        // 3. Input Check
        if (!inputScript.aim)
        {
            if (_currentLockTimer > 0 || _finalLockedTarget != null) Debug.Log("LockOn: Aim released. Resetting.");
            ResetLockState();
            return;
        }

        // 4. Check Final Lock
        if (_finalLockedTarget != null)
        {
            if (_currentReticleImage != null) _currentReticleImage.color = lockedReticleColor;
            
            if (_finalLockedTarget._isDead || !_finalLockedTarget.gameObject.activeInHierarchy)
            {
                Debug.Log("LockOn: Locked target died/vanished. Resetting.");
                ResetLockState();
            }
            return; // Already locked, nothing to do
        }

        // 5. SphereCast
        Ray ray = new Ray(mainCamera.transform.position, mainCamera.transform.forward);
        RaycastHit hit;
        EnemyController hitEnemy = null;

        if (Physics.SphereCast(ray, lockOnRadius, out hit, lockOnRange, enemyLayer))
        {
             if (hit.transform.root != transform.root)
             {
                 hitEnemy = hit.collider.GetComponentInParent<EnemyController>();
                 // DEBUG 2: Log what we hit
                 // Debug.Log($"LockOn: Ray hit {hit.collider.name}");
             }
        }
        else
        {
            // DEBUG 3: Log miss (Only enable if you suspect the ray is too short)
            // Debug.Log("LockOn: Raycast missed everything.");
        }

        // 6. Logic Evaluation
        if (hitEnemy != null && !hitEnemy._isDead)
        {
            if (hitEnemy == _pendingTarget)
            {
                // Increment Timer
                _currentLockTimer += Time.deltaTime;
                
                // DEBUG 4: Print Timer progress (Every 0.5s to avoid spam)
                if (_currentLockTimer % 0.5f < Time.deltaTime) 
                    Debug.Log($"LockOn: Acquiring... {_currentLockTimer:F1}/{timeToLock}");

                if (_currentLockTimer >= timeToLock)
                {
                    // SUCCESS
                    _finalLockedTarget = hitEnemy;
                    _pendingTarget = null;
                    if (_currentReticleImage != null) _currentReticleImage.color = lockedReticleColor;
                    Debug.Log($"LockOn: TARGET LOCKED: {hitEnemy.name}");
                }
                else
                {
                    // PENDING
                    if (_currentReticleImage != null) _currentReticleImage.color = acquiringReticleColor;
                }
            }
            else
            {
                Debug.Log($"LockOn: New Target Found: {hitEnemy.name}. Timer Reset.");
                _pendingTarget = hitEnemy;
                _currentLockTimer = 0f;
                if (_currentReticleImage != null) _currentReticleImage.color = defaultReticleColor;
            }
        }
        else
        {
            if (_pendingTarget != null) Debug.Log("LockOn: Lost target. Resetting.");
            _pendingTarget = null;
            _currentLockTimer = 0f;
            if (_currentReticleImage != null) _currentReticleImage.color = defaultReticleColor;
        }
    }

    // Helper to clear everything
    private void ResetLockState()
    {
        _pendingTarget = null;
        _finalLockedTarget = null;
        _currentLockTimer = 0f;
        if (_currentReticleImage != null) _currentReticleImage.color = defaultReticleColor;
    }
    
    // Checks if the weapon is marked as "Infinite Start" AND has no upgrades yet
    private bool IsAmmoInfinite()
    {
        // 1. If it's not a starter weapon, it's never infinite
        if (!currentWeapon.infiniteAtStart) return false;

        // 2. Safety check for manager
        if (UpgradeManager.Instance == null) return true; // Default to infinite if manager missing

        // 3. Check if ANY upgrade has been purchased
        var data = UpgradeManager.Instance.GetWeaponData(currentWeapon.weaponName);
        if (data == null) return true; 

        // If any stat is above Level 1, or Guidance is unlocked, we are upgraded
        if (data.damagePath.currentLevel > 1) return false;
        if (data.fireRatePath.currentLevel > 1) return false;
        if (data.maxAmmoPath.currentLevel > 1) return false;
        if (data.clipSizePath.currentLevel > 1) return false;
        if (data.guidanceUnlocked) return false;

        // 4. No upgrades found -> Ammo is infinite
        return true;
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
        currentGunAnimator = currentGunInstance.GetComponentInChildren<Animator>();
        
        if (currentGunAnimator == null) 
        {
            Debug.LogWarning($"No Animator found on {currentWeapon.weaponName} or its children!");
        }
        if (currentWeapon.adsReticlePrefab != null && adsReticleParent != null) {
            currentAdsReticleInstance = Instantiate(currentWeapon.adsReticlePrefab, adsReticleParent);
            currentAdsReticleInstance.SetActive(false);
            
            _currentReticleImage = currentAdsReticleInstance.GetComponent<UnityEngine.UI.Image>();
            if (_currentReticleImage != null) _currentReticleImage.color = defaultReticleColor;
        } else currentAdsReticleInstance = null; 

        currentGunTracer = currentGunInstance.GetComponentInChildren<LineRenderer>();
        ShootPointTag tagScript = currentGunInstance.GetComponentInChildren<ShootPointTag>();
        if (tagScript != null)
        {
            shootPoint = tagScript.transform;
        }
        else
        {
            // Fallback: Try to find by name if tag is missing
            shootPoint = currentGunInstance.transform.Find("ShootPoint");
            if (shootPoint == null) Debug.LogError($"Could not find ShootPoint on {currentWeapon.weaponName}! Make sure it has the 'ShootPointTag' script attached.");
        }
        
        currentAmmo = GetUpgradedClipSize();
        currentReserveAmmo = GetUpgradedMaxAmmo();
        
        isReloading = false;
        nextFireTime = 0f;
    }
}