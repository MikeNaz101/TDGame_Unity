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

    [Header("Procedural Weapon Motion")]
    [Tooltip("How much the gun lags behind mouse movement.")]
    public float swayAmount = 0.02f;
    public float swayMaxOffset = 0.06f;
    public float swaySmoothing = 8f;
    [Tooltip("Walk bob size and speed. Scaled by how fast the player is moving.")]
    public float bobAmount = 0.02f;
    public float bobFrequency = 9f;
    [Tooltip("Gun kick per shot, scaled by each weapon's Recoil Kickback.")]
    public float recoilKickBack = 0.06f;
    public float recoilKickPitch = 6f;
    public float recoilRecoverSpeed = 12f;
    [Tooltip("Raise animation when a weapon is equipped.")]
    public float equipDuration = 0.35f;
    [Tooltip("Tilt the gun down while reloading. Only used if no Arms Animator is assigned, so it never fights a real reload animation.")]
    public bool proceduralReloadTilt = true;
    [Tooltip("Sway and bob are reduced to this fraction while aiming down sights.")]
    [Range(0f, 1f)] public float adsMotionScale = 0.2f;

    [Header("Debug")]
    public bool debugLogs = false;

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

    // Procedural motion state
    private Quaternion hipRotation = Quaternion.identity;
    private Vector3 adsPosition;
    private Vector2 swayOffset;
    private float bobTimer;
    private float bobBlend;
    private Vector3 recoilPosition;
    private float recoilPitch;
    private float recoilYaw;
    private float equipBlend = 1f;
    private float reloadBlend;
    private float aimBlend;
    private CharacterController characterController;
    private bool? crosshairShown;
    private Coroutine reloadRoutine;

    // Cached per weapon so switching doesn't Instantiate/Destroy models every time
    private readonly Dictionary<int, GameObject> gunInstances = new Dictionary<int, GameObject>();
    private readonly Dictionary<int, GameObject> reticleInstances = new Dictionary<int, GameObject>();

    // Reused every shot (no per-shot allocations)
    private readonly Dictionary<EnemyController, int> enemyHits = new Dictionary<EnemyController, int>();
    private readonly List<Vector3> pelletHitPoints = new List<Vector3>();
    private readonly Stack<LineRenderer> tracerPool = new Stack<LineRenderer>();
    private readonly List<LineRenderer> activeTracers = new List<LineRenderer>();
    private readonly List<float> activeTracerExpiry = new List<float>();
    private const float TRACER_LIFETIME = 0.05f;

    void Start()
    {
        playerStats = GetComponentInParent<PlayerStats>();
        gunHolder = this.transform; 
        _audioSource = GetComponent<AudioSource>();
        
        if (transform.parent != null) hipPosition = transform.localPosition;
        hipRotation = transform.localRotation;
        adsPosition = transform.localPosition;
        characterController = GetComponentInParent<CharacterController>();
        
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
                    reloadRoutine = StartCoroutine(Reload());
                }
            }
        }
        wasFiringLastFrame = inputScript.fire;

        UpdateTracers();
        ApplyWeaponMotion();
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
        
 
        // Only touch the UI when the state actually changes
        if (crosshairShown != !isAiming)
        {
            crosshairShown = !isAiming;
            if (hipCrosshairUI != null) hipCrosshairUI.SetActive(!isAiming);
            if (currentAdsReticleInstance != null) currentAdsReticleInstance.SetActive(isAiming);
        }

        float lerpSpeed = currentWeapon != null ? currentWeapon.adsSpeed : 10f;
        float t = 1f - Mathf.Exp(-lerpSpeed * Time.deltaTime);
        
        // Smoothly move toward the ADS/hip position. Sway, bob and recoil are layered on top in ApplyWeaponMotion().
        adsPosition = Vector3.Lerp(adsPosition, targetLocalPosition, t);
        aimBlend = Mathf.Lerp(aimBlend, isAiming ? 1f : 0f, t);
        
        if (playerVirtualCamera != null)
        {
            var lens = playerVirtualCamera.Lens;
            lens.FieldOfView = Mathf.Lerp(lens.FieldOfView, targetFOV, t);
            playerVirtualCamera.Lens = lens;
        }
        else if (mainCamera != null)
        {
            mainCamera.fieldOfView = Mathf.Lerp(mainCamera.fieldOfView, targetFOV, t);
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
        AddRecoilKick(currentWeapon.recoilKickback);

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
                if (debugLogs) Debug.Log(_finalLockedTarget != null ? $"Shoot: Firing homing rocket at {_finalLockedTarget.name}" : "Shoot: Firing dumb rocket (No lock).");
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

            return; // EXIT FUNCTION HERE for Rockets
        }

        // --- CASE 2: HITSCAN WEAPON (Rifle & Shotgun) ---
        // This logic runs for EVERY hitscan weapon.
        // If it's a Rifle, pellets is 1. If Shotgun, pellets is 8.

        enemyHits.Clear();
        pelletHitPoints.Clear();
        int totalPellets = Mathf.Max(1, currentWeapon.pellets); // Ensure at least 1 pellet
        Transform camTransform = mainCamera.transform;

        // Loop through every pellet
        for (int i = 0; i < totalPellets; i++)
        {
            // Calculate Spread
            Vector3 forward = camTransform.forward;
            if (currentWeapon.spreadAngle > 0)
            {
                // Spread around the camera's own axes (the old world-space Euler skewed the pattern when looking sideways)
                float xSpread = Random.Range(-currentWeapon.spreadAngle, currentWeapon.spreadAngle);
                float ySpread = Random.Range(-currentWeapon.spreadAngle, currentWeapon.spreadAngle);
                forward = Quaternion.AngleAxis(xSpread, camTransform.right) * Quaternion.AngleAxis(ySpread, camTransform.up) * forward;
            }

            Ray ray = new Ray(camTransform.position, forward);
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

        // 3. Draw Tracers (pooled)
        ShowTracers(pelletHitPoints);

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
                if (debugLogs) Debug.Log($"MEATSHOT! {enemy.name} took bonus damage from full spray.");
            }

            enemy.TakeDamage(finalDamage, transform.root, currentWeapon.damageType);
            shotsHit += hits;
        }
        enemyHits.Clear();
        // Note: fire input is no longer cleared here. Automatic weapons keep firing while the
        // button is held; semi-auto weapons rely on the wasFiringLastFrame edge check.
    }

    // --- POOLED TRACERS ---
    private void ShowTracers(List<Vector3> hitPoints)
    {
        if (tracerPrefab == null || shootPoint == null || hitPoints.Count == 0) return;

        Vector3 origin = shootPoint.position;
        float expiry = Time.time + TRACER_LIFETIME;
        foreach (Vector3 hitPoint in hitPoints)
        {
            LineRenderer lr = GetTracer();
            if (lr == null) return;
            lr.SetPosition(0, origin);
            lr.SetPosition(1, hitPoint);
            lr.gameObject.SetActive(true);
            activeTracers.Add(lr);
            activeTracerExpiry.Add(expiry);
        }
    }

    private LineRenderer GetTracer()
    {
        while (tracerPool.Count > 0)
        {
            LineRenderer pooled = tracerPool.Pop();
            if (pooled != null) return pooled;
        }

        GameObject obj = Instantiate(tracerPrefab);
        LineRenderer lr = obj.GetComponent<LineRenderer>();
        if (lr == null)
        {
            Debug.LogError("WeaponControllerHS: Tracer Prefab needs a LineRenderer component!");
            Destroy(obj);
            return null;
        }
        lr.useWorldSpace = true;
        return lr;
    }

    private void UpdateTracers()
    {
        float now = Time.time;
        for (int i = activeTracers.Count - 1; i >= 0; i--)
        {
            if (now < activeTracerExpiry[i]) continue;
            LineRenderer lr = activeTracers[i];
            if (lr != null)
            {
                lr.gameObject.SetActive(false);
                tracerPool.Push(lr);
            }
            activeTracers.RemoveAt(i);
            activeTracerExpiry.RemoveAt(i);
        }
    }

    // --- PROCEDURAL WEAPON MOTION ---
    private void AddRecoilKick(float kickback)
    {
        float aimDamp = Mathf.Lerp(1f, 0.6f, aimBlend); // steadier when aiming
        recoilPosition += new Vector3(0f, 0f, -recoilKickBack * kickback * aimDamp);
        recoilPitch += recoilKickPitch * kickback * aimDamp;
        recoilYaw += Random.Range(-1f, 1f) * recoilKickPitch * 0.3f * kickback * aimDamp;
    }

    private void ApplyWeaponMotion()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;
        float motionScale = Mathf.Lerp(1f, adsMotionScale, aimBlend);

        // 1. Sway: the gun lags slightly behind mouse look
        Vector2 look = inputScript != null ? inputScript.look : Vector2.zero;
        Vector2 swayTarget = Vector2.ClampMagnitude(-look * swayAmount, swayMaxOffset) * motionScale;
        swayOffset = Vector2.Lerp(swayOffset, swayTarget, 1f - Mathf.Exp(-swaySmoothing * dt));

        // 2. Bob: figure-eight while walking, fades out smoothly when stopping or airborne
        float speed = 0f;
        bool grounded = true;
        if (characterController != null)
        {
            Vector3 v = characterController.velocity;
            v.y = 0f;
            speed = v.magnitude;
            grounded = characterController.isGrounded;
        }
        float targetBob = grounded ? Mathf.Clamp01(speed / 7f) : 0f;
        bobBlend = Mathf.Lerp(bobBlend, targetBob, 1f - Mathf.Exp(-8f * dt));
        bobTimer += dt * bobFrequency * Mathf.Lerp(0.6f, 1.3f, Mathf.Clamp01(speed / 12f));
        Vector3 bob = new Vector3(Mathf.Cos(bobTimer * 0.5f) * bobAmount, Mathf.Abs(Mathf.Sin(bobTimer)) * bobAmount, 0f) * (bobBlend * motionScale);

        // 3. Recoil springs back to rest
        float recover = 1f - Mathf.Exp(-recoilRecoverSpeed * dt);
        recoilPosition = Vector3.Lerp(recoilPosition, Vector3.zero, recover);
        recoilPitch = Mathf.Lerp(recoilPitch, 0f, recover);
        recoilYaw = Mathf.Lerp(recoilYaw, 0f, recover);

        // 4. Equip raise (ease-out from below)
        if (equipBlend < 1f) equipBlend = Mathf.MoveTowards(equipBlend, 1f, dt / Mathf.Max(0.01f, equipDuration));
        float equipInv = 1f - (1f - Mathf.Pow(1f - equipBlend, 3f));

        // 5. Reload dip (only when no real reload animation exists)
        bool tilt = proceduralReloadTilt && armsAnimator == null && isReloading;
        reloadBlend = Mathf.Lerp(reloadBlend, tilt ? 1f : 0f, 1f - Mathf.Exp(-10f * dt));

        Vector3 position = adsPosition
                         + new Vector3(swayOffset.x, swayOffset.y, 0f)
                         + bob
                         + recoilPosition
                         + new Vector3(0f, -0.25f * equipInv - 0.08f * reloadBlend, 0f);

        Quaternion rotation = hipRotation
                            * Quaternion.Euler(-recoilPitch + 30f * equipInv + 15f * reloadBlend,
                                               recoilYaw + swayOffset.x * 40f,
                                               swayOffset.x * -60f + 25f * reloadBlend);

        transform.localPosition = position;
        transform.localRotation = rotation;
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
        
        if (armsAnimator != null) armsAnimator.SetTrigger("Reload");
        
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
        reloadRoutine = null;
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
            ResetLockState();
            return;
        }

        // 4. Check Final Lock
        if (_finalLockedTarget != null)
        {
            if (_currentReticleImage != null) _currentReticleImage.color = lockedReticleColor;
            
            if (_finalLockedTarget._isDead || !_finalLockedTarget.gameObject.activeInHierarchy)
            {
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
                

                if (_currentLockTimer >= timeToLock)
                {
                    // SUCCESS
                    _finalLockedTarget = hitEnemy;
                    _pendingTarget = null;
                    if (_currentReticleImage != null) _currentReticleImage.color = lockedReticleColor;
                    if (debugLogs) Debug.Log($"LockOn: TARGET LOCKED: {hitEnemy.name}");
                }
                else
                {
                    // PENDING
                    if (_currentReticleImage != null) _currentReticleImage.color = acquiringReticleColor;
                }
            }
            else
            {
                _pendingTarget = hitEnemy;
                _currentLockTimer = 0f;
                if (_currentReticleImage != null) _currentReticleImage.color = defaultReticleColor;
            }
        }
        else
        {
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

        // Cancel a reload in progress so it can't finish on (and refill) the new weapon
        if (reloadRoutine != null) { StopCoroutine(reloadRoutine); reloadRoutine = null; }
        ResetLockState();

        // Hide (don't destroy) the previous weapon; models are created once and reused
        if (currentGunInstance != null) currentGunInstance.SetActive(false);
        if (currentAdsReticleInstance != null) currentAdsReticleInstance.SetActive(false);

        currentWeaponIndex = weaponIndex;
        currentWeapon = inventory[currentWeaponIndex]; 
        if (currentWeapon.equipSound != null) _audioSource.PlayOneShot(currentWeapon.equipSound);

        if (!gunInstances.TryGetValue(weaponIndex, out currentGunInstance) || currentGunInstance == null)
        {
            currentGunInstance = Instantiate(currentWeapon.weaponPrefab, gunHolder);
            gunInstances[weaponIndex] = currentGunInstance;
        }
        currentGunInstance.SetActive(true);
        currentGunAnimator = currentGunInstance.GetComponentInChildren<Animator>();

        if (currentWeapon.adsReticlePrefab != null && adsReticleParent != null)
        {
            if (!reticleInstances.TryGetValue(weaponIndex, out currentAdsReticleInstance) || currentAdsReticleInstance == null)
            {
                currentAdsReticleInstance = Instantiate(currentWeapon.adsReticlePrefab, adsReticleParent);
                reticleInstances[weaponIndex] = currentAdsReticleInstance;
            }
            currentAdsReticleInstance.SetActive(false);
            
            _currentReticleImage = currentAdsReticleInstance.GetComponent<UnityEngine.UI.Image>();
            if (_currentReticleImage != null) _currentReticleImage.color = defaultReticleColor;
        }
        else
        {
            currentAdsReticleInstance = null;
            _currentReticleImage = null;
        }
        crosshairShown = null; // force the crosshair/reticle state to refresh next frame
        equipBlend = 0f;       // play the raise animation

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