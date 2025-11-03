using UnityEngine;
using System.Collections;
using StarterAssets; // Required to access the public input booleans
using UnityEngine.InputSystem; // Still needed for CallbackContext but not used for firing logic
using System.Linq; 

public class WeaponController : MonoBehaviour
{/*
    // --- SETUP REFERENCES (Assigned in Inspector) ---
    [Header("Setup")]
    [Tooltip("The script that holds the current state of player inputs (StarterAssetsInputs).")]
    public StarterAssetsInputs inputScript; // MUST be assigned in Inspector
    [Tooltip("The Transform where bullets spawn.")]
    public Transform shootPoint;
    [Tooltip("The CameraShake script on the camera.")]
    public CameraShake cameraShake; 
    [Tooltip("The object that marks the ADS position.")]
    public Transform adsTarget;

    // --- TOWER BUILDING REFERENCES ---
    [Header("Tower Building")]
    [Tooltip("The UI panel that shows the list of buildable towers.")]
    public GameObject buildMenuUI;
    [Tooltip("The component used to draw the red aim ray.")]
    public LineRenderer aimRay;
    [Tooltip("Max distance the player can place a tower.")]
    public float maxBuildDistance = 15f;
    [Tooltip("The object that indicates the current valid build location.")]
    public GameObject buildIndicatorPrefab; // Placeholder or ghost tower prefab
    
    // --- WEAPON DATA ---
    [Header("Weapon Data")]
    [Tooltip("List of all weapons the player can switch between.")]
    public WeaponData[] inventory;

    // --- PRIVATE STATE VARIABLES ---
    private WeaponData currentWeapon;
    private float nextFireTime;
    private bool isReloading = false;
    private int currentAmmo;
    private int currentReserveAmmo;
    private bool isAiming = false; // Tracks current ADS state
    private bool wasFiringLastFrame = false; // Used for semi-auto/ammo check
    private bool buildIndicatorAvailable = false; // Tracks if the indicator is over a valid spot
    private float adsSpeed;
    private Vector3 initialPosition;
    private int currentWeaponIndex = 0; // Tracks equipped index

    // --- BUILDING STATE MACHINE ---
    private enum BuildState { Idle, Aiming, MenuOpen }
    private BuildState buildState = BuildState.Idle;
    private Transform buildIndicatorInstance; // The red/green ghost object
    private int selectedTowerIndex = 0; // Index for tower selection

    // --- UNITY LIFECYCLE ---

    void Start()
    {
        // Find LineRenderer on the camera
        if (aimRay == null)
        {
            aimRay = GetComponentInParent<LineRenderer>();
            if (aimRay == null)
            {
                Debug.LogError("WeaponController requires a LineRenderer component in the parent hierarchy for the build ray.");
            }
        }
        
        // Find StarterAssetsInputs script if not assigned
        if (inputScript == null)
        {
            inputScript = FindObjectOfType<StarterAssetsInputs>();
            if (inputScript == null)
            {
                Debug.LogError("StarterAssetsInputs script not found in scene. Inputs will not work.");
            }
        }

        initialPosition = transform.localPosition;
        
        // Start with the first weapon
        if (inventory.Length > 0)
        {
            EquipWeapon(0);
        }

        // Hide UI and LineRenderer on start
        if (buildMenuUI != null) buildMenuUI.SetActive(false);
        if (aimRay != null) aimRay.enabled = false;
    }

    void Update()
    {
        if (currentWeapon == null || inputScript == null) return;
        
        // Update Cooldown
        if (nextFireTime > Time.time)
        {
            return; // Can't shoot yet
        }
        
        HandleADS();
        
        // Input logic now starts here
        HandleBuildingInput();
        
        // Only allow shooting if we are NOT building
        if (buildState == BuildState.Idle)
        {
            HandleShootingInput();
            HandleReloadInput();
        }
    }
    
    // --- INPUT HANDLERS (Polling) ---

    void HandleShootingInput()
    {
        // We only allow shooting if the weapon is off cooldown and not reloading.
        bool shotReady = nextFireTime <= Time.time;
        
        if (shotReady)
        {
            if (currentWeapon.isAutomatic)
            {
                // Automatic: Fire continuously while the button is held
                if (inputScript.fire)
                {
                    Shoot();
                }
            }
            else // Semi-Automatic
            {
                // Semi-Auto: Fire only on the frame the button is pressed down (input is TRUE AND it was FALSE last frame)
                if (inputScript.fire && !wasFiringLastFrame)
                {
                    Shoot();
                }
            }
        }
        
        // Update input state for the next frame's semi-auto check
        wasFiringLastFrame = inputScript.fire;
    }
    
    void HandleReloadInput()
    {
        // Handle Manual Reload Input
        if (inputScript.reload && !isReloading && currentAmmo < currentWeapon.maxAmmo)
        {
            StartCoroutine(Reload());
        }
        // NOTE: We don't reset the boolean here; the StarterAssetsInputs script must handle its own state reset.
    }

    void HandleBuildingInput()
    {
        // --- 1. Handle Q (Build) input ---
        if (inputScript.build) // Q is pressed
        {
            if (buildState == BuildState.Idle)
            {
                // Start Aiming Phase
                buildState = BuildState.Aiming;
                if (aimRay != null) aimRay.enabled = true;
            }
            // Keep AimingPhase running in Update()
        }
        else if (buildState != BuildState.Idle) // Q was released
        {
            // Cancel the build from any state if Q is released
            CancelBuild();
            return; // Exit function immediately after cancel
        }

        // --- 2. Handle Fire input when in Building States ---
        if (inputScript.fire)
        {
            if (buildState == BuildState.Aiming)
            {
                // Check if the raycast is hitting a valid spot
                if (!buildIndicatorAvailable)
                {
                    Debug.LogWarning("Cannot build: location is not valid or too far.");
                    return;
                }
                
                // Transition to MenuOpen
                buildState = BuildState.MenuOpen;
                if (aimRay != null) aimRay.enabled = false; // Deactivate ray
                if (buildIndicatorInstance != null) buildIndicatorInstance.gameObject.SetActive(true); // Keep ghost visible
                Cursor.lockState = CursorLockMode.None; // Show cursor
                if (buildMenuUI != null) buildMenuUI.SetActive(true); // Show UI
            } 
            else if (buildState == BuildState.MenuOpen)
            {
                // Confirm the build (Fire button clicked while menu is open)
                // **TODO: Call the resource check and build logic**
                // PlayerStats.TryBuildTower(selectedTowerIndex, buildIndicatorInstance.position);
                
                Debug.Log("Tower Build Confirmed at location! (Call to PlayerStats missing)");
                CancelBuild(); 
            }
        }
        
        // --- 3. Handle Scroll Wheel for Menu (assuming the scroll value is polled in StarterAssetsInputs) ---
        // We will assume a dedicated method or property for scrolling here. For now, we will use a key check:
        // if (inputScript.menuScroll != 0) { // Cycle Towers }
        
        // Run state logic
        HandleBuildingState();
    }


    // --- STATE MACHINE HANDLERS ---
    
    void HandleBuildingState()
    {
        switch (buildState)
        {
            case BuildState.Aiming:
                AimingPhase();
                break;
            case BuildState.MenuOpen:
                // MenuPhase is active (UI is visible, cursor is unlocked)
                break;
            case BuildState.Idle:
                // Clean up happens in CancelBuild, so nothing needed here.
                break;
        }
    }

    void AimingPhase()
    {
        // Ensure indicator and ray are visible
        if (aimRay != null) aimRay.enabled = true;

        RaycastHit hit;
        // Raycast origin is camera, direction is camera forward
        if (Physics.Raycast(transform.parent.position, transform.parent.forward, out hit, maxBuildDistance, LayerMask.GetMask("Default")))
        {
            // Assuming LayerMask "Default" covers all walkable ground
            buildIndicatorAvailable = true;
            
            // Position the ray end and the indicator
            aimRay.SetPosition(0, shootPoint.position);
            aimRay.SetPosition(1, hit.point);
            UpdateBuildIndicator(hit.point, true); // True = can build
        }
        else
        {
            buildIndicatorAvailable = false;
            // Ray misses the ground within max distance
            aimRay.SetPosition(0, shootPoint.position);
            aimRay.SetPosition(1, shootPoint.position + transform.parent.forward * maxBuildDistance);
            UpdateBuildIndicator(Vector3.zero, false); // False = invalid location
        }
    }

    void CancelBuild()
    {
        buildState = BuildState.Idle;
        if (aimRay != null) aimRay.enabled = false;
        if (buildMenuUI != null) buildMenuUI.SetActive(false);
        
        // Only destroy the indicator if it exists
        if (buildIndicatorInstance != null) Destroy(buildIndicatorInstance.gameObject);
        
        // Lock cursor for FPS view
        Cursor.lockState = CursorLockMode.Locked; 
    }
    
    void UpdateBuildIndicator(Vector3 position, bool isValid)
    {
        if (buildIndicatorPrefab == null) return;
        
        if (buildIndicatorInstance == null)
        {
            // Create the ghost indicator on first use
            buildIndicatorInstance = Instantiate(buildIndicatorPrefab, position, Quaternion.identity).transform;
            buildIndicatorInstance.SetParent(transform.root);
        }

        // Set position and color based on validity
        buildIndicatorInstance.position = position;
        
        // Ensure renderer exists before trying to change color
        Renderer indicatorRenderer = buildIndicatorInstance.GetComponent<Renderer>();
        if(indicatorRenderer != null)
        {
            indicatorRenderer.material.color = isValid ? Color.green : Color.red;
        }

        buildIndicatorInstance.gameObject.SetActive(true);
    }

    // --- SHOOTING & ADS HANDLERS ---
    
    void HandleADS()
    {
        // Get target position (uses the isAiming state set by OnAim)
        Vector3 targetPosition = inputScript.aim ? adsTarget.localPosition : initialPosition;
        
        // Smoothly move the gun
        transform.localPosition = Vector3.Lerp(
            transform.localPosition, 
            targetPosition, 
            Time.deltaTime * currentWeapon.adsSpeed
        );
    }

    void Shoot()
    {
        // 1. Check for Ammunition and Auto-Reload
        if (currentAmmo <= 0)
        {
            Debug.Log("Out of Ammo! Reloading automatically...");
            if (currentReserveAmmo > 0 && !isReloading)
            {
                StartCoroutine(Reload()); 
            }
            return; 
        }

        // 2. Decrement Ammo & Set Cooldown
        currentAmmo--;
        nextFireTime = Time.time + currentWeapon.fireRate;

        // 3. CORE MECHANIC: Spawn Projectile
        /*if (currentWeapon.bulletPrefab != null)
        {
            // Instantiate and pass damage/speed data
            GameObject projectileObject = Instantiate(currentWeapon.bulletPrefab, shootPoint.position, shootPoint.rotation);
            Projectile projectileScript = projectileObject.GetComponent<Projectile>(); 
            
            if (projectileScript != null)
            {
                projectileScript.damageAmount = currentWeapon.damage;
            }
        }
        else
        {
            Debug.LogError("Bullet Prefab is NULL in Weapon Data asset!");
        }

        // 4. Apply recoil/camera shake
        if (cameraShake != null)
        {
            cameraShake.Shake(currentWeapon.recoilKickback);
        }

        Debug.Log("Fired! Ammo Remaining: " + currentAmmo);
    }
    
    // --- COROUTINE ---

    IEnumerator Reload()
    {
        isReloading = true;
        Debug.Log("Reloading...");
        
        // Wait for the specified reload time
        yield return new WaitForSeconds(currentWeapon.reloadTime);
        
        // Calculate amount to reload
        int ammoNeeded = currentWeapon.maxAmmo - currentAmmo;
        int ammoToUse = Mathf.Min(ammoNeeded, currentReserveAmmo);

        // Update values
        currentAmmo += ammoToUse;
        currentReserveAmmo -= ammoToUse;

        Debug.Log("Reload Complete! Ammo: " + currentAmmo + " Reserve: " + currentReserveAmmo);
        isReloading = false;
    }
    
    // --- PUBLIC METHODS ---

    public void EquipWeapon(int index)
    {
        if (index < 0 || index >= inventory.Length)
        {
            Debug.LogError("Invalid weapon index.");
            return;
        }

        // 1. Deactivate old weapon model
        if (currentWeapon != null && currentWeapon.weaponPrefab != null)
        {
            currentWeapon.weaponPrefab.SetActive(false);
        }

        // 2. Set new weapon data
        currentWeapon = inventory[index];
        
        // 3. ACTIVATE and SEARCH the new weapon model
        if (currentWeapon.weaponPrefab != null)
        {
            // 3a. Instantiate the model if it's not already in the scene, OR...
            // 3b. If using the setup where the models are already children of GunHolder:
            currentWeapon.weaponPrefab.SetActive(true); 

            // CRITICAL STEP: Search the newly activated weapon model for the Muzzle tag.
            Transform newShootPoint = currentWeapon.weaponPrefab.GetComponentInChildren<Transform>()
                .GetComponentsInChildren<Transform>()
                .FirstOrDefault(t => t.CompareTag("Muzzle"));

            if (newShootPoint != null)
            {
                // Assign the found Transform to the public shootPoint field
                shootPoint = newShootPoint; 
            }
            else
            {
                Debug.LogError("ShootPoint/Muzzle Tag Not Found in new weapon prefab: " + currentWeapon.weaponName);
            }
        }
        
        // 4. Initialize state for new weapon
        currentAmmo = currentWeapon.maxAmmo;
        currentReserveAmmo = currentWeapon.reserveAmmo;
        adsSpeed = currentWeapon.adsSpeed;

        // 5. Activate new weapon model and ensure it starts at the hip position
        if (currentWeapon.weaponPrefab != null)
        {
            currentWeapon.weaponPrefab.SetActive(true);
        }
        
        transform.localPosition = initialPosition;

        Debug.Log("Equipped: " + currentWeapon.weaponName);
    }
*/}
