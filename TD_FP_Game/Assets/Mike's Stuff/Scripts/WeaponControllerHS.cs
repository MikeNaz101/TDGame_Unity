using UnityEngine;
using System.Collections;
using StarterAssets; // Required to access the public input booleans
using System.Linq;
using System.Collections.Generic; // NEW: Required for List

public class WeaponControllerHS : MonoBehaviour
{
    // --- SETUP REFERENCES (Assigned in Inspector) ---
    [Header("Setup")]
    [Tooltip("The script that holds the current state of player inputs.")]
    public StarterAssetsInputs inputScript; // Assigned in Inspector (The Player object)
    [Tooltip("The Transform where bullets spawn.")]
    public Transform shootPoint; // Assigned in Inspector (The Muzzle)
    [Tooltip("The player camera's shake script for recoil effects.")]
    public CameraShake cameraShake; // Assigned in Inspector (The Main Camera)
    [Tooltip("The object that marks the ADS position.")]
    public Transform adsTarget; // Assigned in Inspector
    [Tooltip("Layers the hitscan will detect (Enemy, Default, etc.)")]
    public LayerMask hitScanLayer;
    [Tooltip("Max distance the player can place a tower.")]
    public float maxBuildDistance = 15f;
    [Tooltip("The object that indicates the current valid build location.")]
    public GameObject buildIndicatorPrefab; // Placeholder or ghost tower prefab
    
    // --- TOWER BUILDING REFERENCES ---
    [Header("Building References")]
    [Tooltip("The UI panel that shows the list of buildable towers.")]
    public GameObject buildMenuUI;
    [Tooltip("The component used to draw the red aim ray. Found in parent hierarchy.")]
    public LineRenderer aimRay;
    
    // --- MANAGER REFERENCES (Found in Start) ---
    private PlayerStats playerStats;

    // --- WEAPON DATA ---
    [Header("Weapon Data")]
    [Tooltip("List of all weapons the player can switch between.")]
    public WeaponData[] inventory;
    private WeaponData currentWeapon;
    
    // --- NEW: Scene Instances for switching ---
    [Tooltip("List of weapon GOs found in scene hierarchy.")]
    private List<GameObject> weaponInstances = new List<GameObject>();


    // --- PRIVATE STATE VARIABLES ---
    private float nextFireTime;
    private bool isReloading = false;
    private int currentAmmo;
    private int currentReserveAmmo;
    private bool wasFiringLastFrame = false; // Tracks semi-auto clicks/anti-spam
    private int currentWeaponIndex = 0; // Ensure this is initialized

    // --- VISUAL & MOVEMENT STATE ---
    private Vector3 initialPosition; // For ADS and Recoil
    private Vector3 hipPosition = Vector3.zero; 
    private bool adsModeActive = false; // Tracks current ADS state
    
    // --- BUILDING STATE MACHINE ---
    private enum BuildState { Idle, Aiming, MenuOpen }
    private BuildState buildState = BuildState.Idle;
    private Transform buildIndicatorInstance; // The red/green ghost object
    private int selectedTowerIndex = 0; // Index for tower selection

    // --- UNITY LIFECYCLE ---

    void Start()
    {
        // 1. Get initial resting position
        initialPosition = transform.localPosition;
        hipPosition = initialPosition;
        
        // 2. Find external references
        playerStats = GetComponentInParent<PlayerStats>();
        aimRay = GetComponentInParent<LineRenderer>();
        
        if (aimRay == null)
        {
            Debug.LogError("WeaponController requires a LineRenderer component in the parent hierarchy for the build ray.");
        }

        // 3. Populate weapon instances list and disable them
        PopulateWeaponInstances(); // NEW STEP

        // 4. Equip the first weapon on start (Index 0)
        if (inventory.Length <= 1)
        {
            EquipWeapon(0);
        }

        // 5. Initial state setup
        if (buildMenuUI != null) buildMenuUI.SetActive(false);
        if (aimRay != null) aimRay.enabled = false;
        
        // Ensure cursor is locked on start
        Cursor.lockState = CursorLockMode.Locked;
    }
    
    // NEW METHOD: Finds all weapons under the GunHolder and maps them to the inventory data.
    void PopulateWeaponInstances()
    {
        // Clear the list to ensure a clean start
        weaponInstances.Clear();

        // Check if inventory data matches scene instances
        if (inventory.Length == 0)
        {
            Debug.LogError("Inventory list is empty in Weapon Data!");
            return;
        }

        // We assume the children of this object (GunHolder) are the weapon models.
        // We will match the position in the hierarchy to the position in the inventory array.
        
        // Use a loop that matches the inventory array length
        for (int i = 0; i < inventory.Length; i++)
        {
            WeaponData data = inventory[i];
            
            // Search this object's children for a GameObject that matches the data's prefab name.
            GameObject instance = transform.Cast<Transform>()
                .Select(t => t.gameObject)
                .FirstOrDefault(g => g.name == data.weaponPrefab.name);

            if (instance != null)
            {
                weaponInstances.Add(instance);
                // Ensure all found instances start disabled, except the one that's about to be equipped (optional, safer to disable all here)
                instance.SetActive(false);
            }
            else
            {
                Debug.LogError($"Weapon model for {data.weaponName} not found as a child of GunHolder! Switching will fail.");
                // Add a null entry to keep the index matching
                weaponInstances.Add(null); 
            }
        }
    }


    void Update()
    {
        if (currentWeapon == null) return;

        // 1. Update Cooldown
        if (nextFireTime > 0f)
        {
            nextFireTime -= Time.deltaTime;
        }

        // 3. Handle Direct Key Switching (New)
        HandleDirectSwitching();
        
        // 4. Handle Building State (Highest priority input)
        HandleBuildingInput();
        if (buildState != BuildState.Idle)
        {
            // If we are building, skip all combat logic
            return;
        }

        // 5. Handle Aim Down Sights (ADS)
        HandleADS();

        // 6. Handle Firing Input Logic (Only if not building)
        HandleFiringInput();

        // 7. Handle Manual Reload Input
        if (inputScript.reload && !isReloading && currentAmmo < currentWeapon.maxAmmo)
        {
            StartCoroutine(Reload());
            // Consume the reload input immediately
            inputScript.reload = false; 
        }

        // 8. Update Input State for next frame (MUST be last)
        wasFiringLastFrame = inputScript.fire;
    }

    // --- NEW METHOD: DIRECT KEY SWITCHING ---

    void HandleDirectSwitching()
    {
        // Only allow switching when not reloading or building
        if (isReloading || buildState != BuildState.Idle) return;
        
        // Check for single weapon case (Length <= 1)
        if (inventory.Length <= 1)
        {
            if (inputScript.weapon2 || inputScript.weapon3)
            {
                Debug.Log("No other weapon to switch to.");
                inputScript.weapon2 = false;
                inputScript.weapon3 = false;
            }
            return;
        }

        int direction = 0;

        // Check for '2' key press (Previous Weapon)
        if (inputScript.weapon2)
        {
            direction = -1; 
            inputScript.weapon2 = false; // Consume input
        }
        // Check for '3' key press (Next Weapon)
        else if (inputScript.weapon3)
        {
            direction = 1; 
            inputScript.weapon3 = false; // Consume input
        }

        if (direction != 0)
        {
            int newIndex = currentWeaponIndex + direction;

            // Looping (Wrapping) logic:
            // Add inventory.Length before modulo to handle negative results (for direction = -1)
            newIndex = (newIndex + inventory.Length) % inventory.Length;
            
            // Perform the switch
            if (newIndex != currentWeaponIndex)
            {
                currentWeaponIndex = newIndex;
                EquipWeapon(currentWeaponIndex);
            }
        }
    }


    // --- INPUT HANDLERS ---
    
    void HandleFiringInput()
    {
        bool shotReady = nextFireTime <= 0f;
        
        if (!isReloading && shotReady)
        {
            // Determine if a shot attempt is valid this frame
            bool isAttemptingShot = false;
            
            if (currentWeapon.isAutomatic)
            {
                // Full Auto: Fire continuously while button is held
                if (inputScript.fire)
                {
                    isAttemptingShot = true;
                }
            }
            else // Semi-Automatic (Requires single press)
            {
                // Semi-Auto: Fire only on the frame the button is pressed down (input is TRUE AND it was FALSE last frame)
                if (inputScript.fire && !wasFiringLastFrame)
                {
                    isAttemptingShot = true;
                }
            }
            
            if (isAttemptingShot)
            {
                Shoot();
            }
        }
    }

    void HandleBuildingInput()
    {
        // --- Input Conflict Check (Prevent building while aiming/firing) ---
        if (buildState == BuildState.Idle)
        {
            // Block all building if any combat input is active
            if (inputScript.fire || inputScript.aim || inputScript.reload || isReloading)
            {
                if (aimRay != null) aimRay.enabled = false;
                return;
            }
        }
        
        // --- State Transition Logic ---
        
        switch (buildState)
        {
            case BuildState.Idle:
                // Transition to Aiming State (Stage 1)
                if (inputScript.build)
                {
                    buildState = BuildState.Aiming;
                    if (aimRay != null) aimRay.enabled = true;
                    if (buildMenuUI != null) buildMenuUI.SetActive(false);
                    // Cursor is locked by default movement script
                }
                break;

            case BuildState.Aiming:
                AimingPhase();
                
                // Transition to MenuOpen State (Stage 2)
                if (inputScript.fire && !wasFiringLastFrame)
                {
                    buildState = BuildState.MenuOpen;
                    if (aimRay != null) aimRay.enabled = false;
                    Cursor.lockState = CursorLockMode.None; // Unlock cursor
                    if (buildMenuUI != null) buildMenuUI.SetActive(true); // Show UI
                }
                
                // Exit: Q released
                if (!inputScript.build)
                {
                    CancelBuild();
                }
                break;
                
            case BuildState.MenuOpen:
                // Handle scrolling input for selection
                if (inputScript.menuScroll != 0)
                {
                    // Logic to cycle through available towers using scroll wheel value 
                    selectedTowerIndex = (selectedTowerIndex + (int)Mathf.Sign(inputScript.menuScroll)) % playerStats.towerManager.availableTowers.Length;
                    if (selectedTowerIndex < 0) selectedTowerIndex += playerStats.towerManager.availableTowers.Length;
                    
                    Debug.Log("Selected Tower Index: " + selectedTowerIndex);
                }

                // Transition to Build Confirmed (Final Stage)
                if (inputScript.fire && !wasFiringLastFrame)
                {
                    Debug.Log("Tower Build Confirmed!");

                    // Final Check before calling PlayerStats
                    if (playerStats != null && buildIndicatorInstance != null)
                    {
                        // **ACTUAL BUILD CALL**
                        playerStats.TryBuildTower(selectedTowerIndex, buildIndicatorInstance.position);
                    }
                    CancelBuild(); 
                }
                
                // Exit: Q released
                if (!inputScript.build)
                {
                    CancelBuild();
                }
                break;
        }
    }

    void AimingPhase()
    {
        // Check if the ray should be drawn (ensures ray is off if buildState is Idle)
        if (aimRay != null) aimRay.enabled = true;
        
        // Raycast to find the build location
        RaycastHit hit;
        
        // Raycast origin is camera, direction is camera forward
        // We use the camera's forward direction (transform.parent.forward)
        if (Physics.Raycast(transform.parent.position, transform.parent.forward, out hit, maxBuildDistance))
        {
            // Position the ray end and the indicator
            aimRay.SetPosition(0, shootPoint.position);
            aimRay.SetPosition(1, hit.point);
            UpdateBuildIndicator(hit.point, true); // True = can build
        }
        else
        {
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
        if (buildIndicatorInstance != null) Destroy(buildIndicatorInstance.gameObject);
        Cursor.lockState = CursorLockMode.Locked; // Lock cursor for FPS view
    }
    
    void UpdateBuildIndicator(Vector3 position, bool isValid)
    {
        if (buildIndicatorPrefab == null) return;
        
        // Lazy instantiation for the build indicator ghost
        if (buildIndicatorInstance == null)
        {
            buildIndicatorInstance = Instantiate(buildIndicatorPrefab, transform.root).transform;
        }

        // Set position and color based on validity
        buildIndicatorInstance.position = position;
        
        // Ensure renderer exists before trying to change color
        Renderer indicatorRenderer = buildIndicatorInstance.GetComponent<Renderer>();
        if(indicatorRenderer != null)
        {
            indicatorRenderer.material.color = isValid ? Color.green : Color.red;
        }

        // Only show indicator if we are in a build state and the location is valid
        buildIndicatorInstance.gameObject.SetActive(buildState != BuildState.Idle && isValid);
    }
    
    // --- VISUAL & MOVEMENT ---
    
    void HandleADS()
    {
        Vector3 targetLocalPosition = hipPosition;
        
        // Check if ADS input is active and we are not currently reloading
        if (inputScript.aim && !isReloading)
        {
            // Set the target position to the ADS position from the data asset
            if (adsTarget != null)
            {
                // We use the local position of the adsTarget marker as the target position
                targetLocalPosition = adsTarget.localPosition;
            }
            adsModeActive = true;
        }
        else
        {
            // Otherwise, target the hip position
            adsModeActive = false;
        }

        // Smoothly interpolate between the current position and the target position
        float lerpSpeed = currentWeapon != null ? currentWeapon.adsSpeed : 10f;
        transform.localPosition = Vector3.Lerp(transform.localPosition, targetLocalPosition, Time.deltaTime * lerpSpeed);
    }
    
    // --- CORE GAMEPLAY METHODS ---

    void Shoot()
    {
        // 1. Check for Ammunition and Auto-Reload
        if (currentAmmo <= 0)
        {
            Debug.Log("Out of Ammo! Reloading automatically...");
            if (currentReserveAmmo > 0)
            {
                // Only start reload if we are not already reloading and have reserve ammo
                if (!isReloading)
                {
                    StartCoroutine(Reload());
                }
            }
            // Return regardless of whether reload started or not (stops shot attempt)
            return; 
        }

        // 2. Decrement Ammo & Set Cooldown
        currentAmmo--;
        nextFireTime = currentWeapon.fireRate;

        // 3. CORE MECHANIC: Hit Scan
        RaycastHit hit;
        // The raycast is always centered on the screen (camera forward)
        if (Physics.Raycast(transform.parent.position, transform.parent.forward, out hit, currentWeapon.range, hitScanLayer))
        {
            // Hit detected! Apply damage.
            EnemyController enemy = hit.collider.GetComponentInParent<EnemyController>();
            
            if (enemy != null)
            {
                enemy.TakeDamage(currentWeapon.damage);
            }
            
            // Draw brief visual feedback (Line Renderer flash)
            StartCoroutine(HitScanVisuals(hit.point));
        }
        else
        {
            // Draw visual feedback for a miss
            StartCoroutine(HitScanVisuals(transform.parent.position + transform.parent.forward * currentWeapon.range));
        }

        // 4. Recoil and Visual Feedback
        if (cameraShake != null)
        {
            cameraShake.Shake(currentWeapon.recoilKickback);
        }

        Debug.Log("Fired! Ammo Remaining: " + currentAmmo);
    }

    IEnumerator HitScanVisuals(Vector3 hitPoint)
    {
        LineRenderer line = shootPoint.GetComponent<LineRenderer>();
        if (line != null)
        {
            line.enabled = true;
            line.SetPosition(0, shootPoint.position);
            line.SetPosition(1, hitPoint);
            
            yield return new WaitForSeconds(0.05f); // Flash quickly
            
            line.enabled = false;
        }
        else
        {
            // Optional: Create a temporary LineRenderer if none exists
            yield break;
        }
    }
    
    // --- COROUTINE ---

    IEnumerator Reload()
    {
        Debug.Log("Reloading...");
        isReloading = true;

        // Wait for the specified reload time
        yield return new WaitForSeconds(currentWeapon.reloadTime);

        // Calculate ammo to transfer from reserve to clip
        int neededAmmo = currentWeapon.maxAmmo - currentAmmo;
        int ammoToTransfer = Mathf.Min(neededAmmo, currentReserveAmmo);

        // Update counts
        currentAmmo += ammoToTransfer;
        currentReserveAmmo -= ammoToTransfer;

        isReloading = false;
        Debug.Log("Reload Complete. Ammo: " + currentAmmo + " / Reserve: " + currentReserveAmmo);
    }

    // --- PUBLIC METHODS ---

    public void EquipWeapon(int index)
    {
        if (index < 0 || index >= inventory.Length) return;

        // 1. DEACTIVATE all weapon instances
        foreach (GameObject instance in weaponInstances)
        {
            if (instance != null)
            {
                instance.SetActive(false);
            }
        }
        
        // 2. Set the new weapon data
        currentWeapon = inventory[index];

        // 3. ACTIVATE the desired scene instance
        if (index < weaponInstances.Count && weaponInstances[index] != null)
        {
            GameObject instance = weaponInstances[index];
            instance.SetActive(true);

            // CRITICAL STEP: Search the newly activated weapon model for the Muzzle tag.
            Transform newShootPoint = instance.GetComponentsInChildren<Transform>()
                .FirstOrDefault(t => t.CompareTag("Muzzle"));

            if (newShootPoint != null)
            {
                shootPoint = newShootPoint; 
            }
            else
            {
                Debug.LogError("ShootPoint/Muzzle Tag Not Found in new weapon prefab: " + currentWeapon.weaponName);
            }
        }
        
        // 4. Initialize ammo
        currentAmmo = currentWeapon.maxAmmo;
        currentReserveAmmo = currentWeapon.reserveAmmo;
        isReloading = false;
        
        Debug.Log("Equipped: " + currentWeapon.weaponName);
    }
}
