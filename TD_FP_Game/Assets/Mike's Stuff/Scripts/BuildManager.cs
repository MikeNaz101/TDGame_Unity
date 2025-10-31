using UnityEngine;
using System.Collections;
using System.Linq; 
using StarterAssets;
using TMPro; // Required for input polling
using UnityEngine.InputSystem; // Required for PlayerInput access
using UnityEngine.InputSystem.UI;
using UnityEngine.UI; // Required for InputSystemUIInputModule (kept for reference, but logic removed)

public class BuildManager : MonoBehaviour
{
    // --- EXTERNAL REFERENCES (Assigned in Inspector) ---
    [Header("Core Components")]
    [Tooltip("The script that handles all weapon fire and reloading state.")]
    public WeaponControllerHS weaponController;
    [Tooltip("The player's resource and stat manager.")]
    public PlayerStats playerStats; 
    [Tooltip("The TowerManager holding the catalog of towers.")]
    public TowerManager towerManager;
    [Tooltip("The script that reports all input states.")]
    public StarterAssetsInputs inputScript; 
    [Tooltip("The PlayerInput component for disabling/enabling input. (Now unused, but kept for reference)")]
    public PlayerInput playerInput; 

    [Header("Building Visuals")]
    [Tooltip("The UI panel that shows the list of buildable towers.")]
    public GameObject buildMenuUI;
    [Tooltip("The LineRenderer component used to draw the red aim ray.")]
    public LineRenderer aimRay;
    [Tooltip("Prefab for the ghost indicator (must have Renderer).")]
    public GameObject buildIndicatorPrefab; 
    
    [Header("Tuning")]
    [Tooltip("Max distance the player can place a tower.")]
    public float maxBuildDistance = 15f;

    // --- NEW UI REFERENCE ---
    // Kept for reference, but no longer toggled as input remains enabled
    private InputSystemUIInputModule uiInputModule; 
    
    [Header("Build Menu UI")]
    [Tooltip("The UI Image component that will display the tower's icon.")]
    public Image towerIconImage;
    [Tooltip("The UI Text component for the tower's name.")]
    public TextMeshProUGUI towerNameText;
    [Tooltip("The UI Text component for the tower's cost.")]
    public TextMeshProUGUI towerCostText;
    [Tooltip("The UI Text component for the tower's description.")]
    public TextMeshProUGUI towerDescriptionText;

    // --- PRIVATE STATE VARIABLES ---
    private enum BuildState { Idle, Aiming, MenuOpen }
    private BuildState buildState = BuildState.Idle;
    private Transform buildIndicatorInstance; // The ghost object instance
    private int selectedTowerIndex = 0;
    
    // --- RUNTIME REFERENCES ---
    private Vector3 currentBuildLocation = Vector3.zero; // Stores the confirmed placement spot

    // --- UNITY LIFECYCLE ---

    void Start()
    {
        // Initial safety checks and setup
        if (buildMenuUI != null) buildMenuUI.SetActive(false);
        if (aimRay != null) aimRay.enabled = false;

        // Auto-find managers if they weren't assigned in the inspector
        if (playerStats == null) playerStats = GetComponentInParent<PlayerStats>();
        if (towerManager == null) towerManager = FindObjectOfType<TowerManager>();
        
        // Find PlayerInput component (needed for initial setup, but not toggling)
        if (playerInput == null) playerInput = GetComponentInParent<PlayerInput>();
        
        // Find the global UI Input Module (still needed to be in scene for UI events)
        uiInputModule = FindObjectOfType<InputSystemUIInputModule>();
    }

    void Update()
    {
        // Safety check to ensure core components are linked
        if (weaponController == null || inputScript == null || playerStats == null || towerManager == null) return;
        
        // --- Input Conflict Check (Revised) ---
        
        // 1. Check for non-fire combat inputs. These ALWAYS cancel building.
        if (weaponController.IsReloading || inputScript.aim || inputScript.reload)
        {
            if (buildState != BuildState.Idle)
            {
                CancelBuild();
            }
            return;
        }

        // 2. Check for 'fire' input ONLY if we are idle.
        // If we are Aiming or MenuOpen, 'fire' is a build system input, not a conflict.
        if (buildState == BuildState.Idle && inputScript.fire)
        {
            // We are idle and the player is shooting, do nothing related to building.
            return; 
        }

        // --- State Transitions ---
        HandleBuildingStateTransitions();
        
        // --- State Workload ---
        switch (buildState)
        {
            case BuildState.Aiming:
                AimingPhase();
                break;
            case BuildState.MenuOpen:
                MenuSelectionPhase();
                break;
            case BuildState.Idle:
                // Nothing needed, but ensures UI is hidden if state was manually set
                if (buildMenuUI != null) buildMenuUI.SetActive(false);
                break;
        }
    }

    // --- STATE MACHINE LOGIC ---

    void HandleBuildingStateTransitions()
    {
        bool buildPressed = inputScript.build;
        bool firePressed = inputScript.fire;

        // 1. IDLE -> AIMING (Q pressed down)
        if (buildState == BuildState.Idle && buildPressed)
        {
            buildState = BuildState.Aiming;
            if (aimRay != null) aimRay.enabled = true;
            weaponController.ToggleShootingEnabled(false); 
        }
        // 2. AIMING -> MENUOPEN (Fire pressed)
        else if (buildState == BuildState.Aiming && firePressed)
        {
            // Only proceed if the location is valid (checked in AimingPhase)
            if (currentBuildLocation != Vector3.zero)
            {
                buildState = BuildState.MenuOpen;
                
                // Hide ray
                if (aimRay != null) aimRay.enabled = false;

                // --- THIS IS THE FIX ---
                // Update the UI *before* showing the menu.
                // It uses selectedTowerIndex, which defaults to 0.
                UpdateBuildMenuUI(selectedTowerIndex); 
                // --- END OF FIX ---

                // Now show the menu UI, which is already populated
                if (buildMenuUI != null) buildMenuUI.SetActive(true);
            }
            // Consume the fire input but return immediately to prevent cancellation/weapon update THIS FRAME
            inputScript.fire = false; 
            return; 
        }
        // 3. MENUOPEN -> BUILD CONFIRMED (Fire pressed again)
        else if (buildState == BuildState.MenuOpen && firePressed)
        {
            inputScript.fire = false; // Consume input
            
            bool success = playerStats.TryBuildTower(selectedTowerIndex, currentBuildLocation);

            if (success)
            {
                Debug.Log($"Successfully built tower {selectedTowerIndex}.");
            }
            else
            {
                Debug.LogWarning("Build failed: Insufficient resources or invalid index.");
            }
            
            CancelBuild(); // Exit the system regardless of success/failure
        }
        // 4. CANCEL (Q released from any non-idle state)
        else if (buildState != BuildState.Idle && !buildPressed)
        {
            CancelBuild();
        }
    }

    void AimingPhase()
    {
        // Raycast origin is camera, direction is camera forward (assuming WeaponController exposes these)
        Ray ray = new Ray(weaponController.transform.parent.position, weaponController.transform.parent.forward);
        RaycastHit hit;
        
        // LayerMask.GetMask("Default") ensures we hit the floor/walls
        if (Physics.Raycast(ray, out hit, maxBuildDistance, LayerMask.GetMask("Default")))
        {
            currentBuildLocation = hit.point;
            
            // Visualize ray hit
            if (aimRay != null)
            {
                aimRay.SetPosition(0, weaponController.ShootPoint.position);
                aimRay.SetPosition(1, hit.point);
            }
            UpdateBuildIndicator(hit.point, true);
        }
        else
        {
            currentBuildLocation = Vector3.zero; // Mark location as invalid
            // Visualize ray miss
            if (aimRay != null)
            {
                aimRay.SetPosition(0, weaponController.ShootPoint.position);
                aimRay.SetPosition(1, weaponController.ShootPoint.position + ray.direction * maxBuildDistance);
            }
            UpdateBuildIndicator(Vector3.zero, false);
        }
    }

    void MenuSelectionPhase()
    {
        // Scroll wheel logic
        if (inputScript.menuScroll != 0)
        {
            int scrollDirection = (int)Mathf.Sign(inputScript.menuScroll);
            inputScript.menuScroll = 0; 

            int maxTowers = towerManager.availableTowers.Length;
            selectedTowerIndex += scrollDirection;
            selectedTowerIndex = (selectedTowerIndex + maxTowers) % maxTowers;

            Debug.Log($"Selected Tower Index: {selectedTowerIndex}");

            // --- THIS IS THE NEW CODE ---
            UpdateBuildMenuUI(selectedTowerIndex);
        }
    }
    
    void UpdateBuildMenuUI(int towerIndex)
    {
        if (towerManager == null || towerManager.availableTowers.Length == 0) return;

        // 1. Get the TowerData for the selected tower
        //TowerData selectedTower = towerManager.GetTowerData(towerIndex); // (Assuming TowerManager has a GetTowerData(int index) method)
        // If not, you might get it like this:
        TowerData selectedTower = towerManager.availableTowers[towerIndex];

        if (selectedTower == null) return;

        // 2. Apply the data to the UI elements
        if (towerIconImage != null)
        {
            towerIconImage.sprite = selectedTower.towerIcon;
            towerIconImage.enabled = (selectedTower.towerIcon != null);
        }

        if (towerNameText != null)
        {
            towerNameText.text = selectedTower.towerName;
        }

        if (towerCostText != null)
        {
            towerCostText.text = selectedTower.scrapCost.ToString() + " SCRAP";
        }

        if (towerDescriptionText != null)
        {
            towerDescriptionText.text = selectedTower.description;
        }
    }

    public void CancelBuild()
    {
        buildState = BuildState.Idle;
        
        // --- REACTIVATE COMBAT INPUT (NO CURSOR LOCK/UNLOCK) ---
        // PlayerInput remains enabled, Cursor remains locked.
        
        weaponController.ToggleShootingEnabled(true); 

        // Hide visuals
        if (aimRay != null) aimRay.enabled = false;
        if (buildMenuUI != null) buildMenuUI.SetActive(false);
        if (buildIndicatorInstance != null) Destroy(buildIndicatorInstance.gameObject);
        
        // Clear input state
        inputScript.build = false; 
        
        Debug.Log("Build sequence cancelled.");
    }
    
    void UpdateBuildIndicator(Vector3 position, bool isValid)
    {
        if (buildIndicatorPrefab == null) return;
        
        if (buildIndicatorInstance == null)
        {
            // Instantiate the ghost indicator
            buildIndicatorInstance = Instantiate(buildIndicatorPrefab, transform.root).transform;
        }

        // Set position and color based on validity
        buildIndicatorInstance.position = position;
        
        Renderer indicatorRenderer = buildIndicatorInstance.GetComponent<Renderer>();
        if(indicatorRenderer != null)
        {
            // You'll need to use a shared material or material property block here
            indicatorRenderer.material.color = isValid ? Color.green : Color.red;
        }

        // Only show indicator if a valid hit occurred
        buildIndicatorInstance.gameObject.SetActive(isValid);
    }
}
