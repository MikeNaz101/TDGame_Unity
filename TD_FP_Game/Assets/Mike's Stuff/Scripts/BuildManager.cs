using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Linq; 
using StarterAssets;
using TMPro;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
public class BuildManager : MonoBehaviour
{
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
    [Tooltip("The name of the NavMesh Area that blocks building (e.g., 'EnemyPath').")]
    [SerializeField] private string _unbuildableAreaName = "EnemyPath";
    [Tooltip("How close to the ground the raycast hit must be to a valid NavMesh point.")]
    [SerializeField] private float _navMeshSampleDistance = 1.0f;
    [Tooltip("The minimum distance allowed between towers.")]
    [SerializeField] private float _minBuildProximity = 3.0f;
    [Tooltip("The LayerMask containing only the 'Tower' layer.")]
    [SerializeField] private LayerMask _towerLayer;

    // This will store the bitmask for the unbuildable area
    private int _unbuildableAreaMask;
    
    
    
    [Header("Build Menu UI")]
    [Tooltip("The UI Image component that will display the tower's icon.")]
    public Image towerIconImage;
    [Tooltip("The UI Text component for the tower's name.")]
    public TextMeshProUGUI towerNameText;
    [Tooltip("The UI Text component for the tower's cost.")]
    public TextMeshProUGUI towerCostText;
    [Tooltip("The UI Text component for the tower's description.")]
    public TextMeshProUGUI towerDescriptionText;
    
    private InputSystemUIInputModule uiInputModule; 
    private enum BuildState { Idle, Aiming, MenuOpen }
    private BuildState buildState = BuildState.Idle;
    private Transform buildIndicatorInstance; // The ghost object instance
    private int selectedTowerIndex = 0;
    
    private Vector3 currentBuildLocation = Vector3.zero;

    void Start()
    {
        // Auto-find managers if they weren't assigned in the inspector
        if (playerStats == null) playerStats = GetComponentInParent<PlayerStats>();
        if (towerManager == null) towerManager = FindObjectOfType<TowerManager>();
        if (playerInput == null) playerInput = GetComponentInParent<PlayerInput>();
        
        // Find the global UI Input Module
        uiInputModule = FindObjectOfType<InputSystemUIInputModule>();
        
        // Get the area index from the name (e.g., "EnemyPath" might be index 3)
        int unbuildableAreaIndex = NavMesh.GetAreaFromName(_unbuildableAreaName);
        
        if (unbuildableAreaIndex == -1) // -1 means it wasn't found
        {
            Debug.LogWarning($"NavMesh Area '{_unbuildableAreaName}' not found. Building checks may not work.", this);
            _unbuildableAreaMask = 0; // Set to an empty mask
        }
        else
        {
            // Convert the index (e.g., 3) to a bitmask (e.g., 1 << 3, which is 8)
            _unbuildableAreaMask = 1 << unbuildableAreaIndex;
        }
    }

    void Update()
    {
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
            return; 
        }
        HandleBuildingStateTransitions();
        switch (buildState)
        {
            case BuildState.Aiming:
                AimingPhase();
                break;
            case BuildState.MenuOpen:
                MenuSelectionPhase();
                break;
            case BuildState.Idle:
                if (buildMenuUI != null) buildMenuUI.SetActive(false);
                break;
        }
    }

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
                UpdateBuildMenuUI(selectedTowerIndex);
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
        Ray ray = new Ray(weaponController.transform.parent.position, weaponController.transform.parent.forward);
        RaycastHit hit;

        bool isLocationValid = false; // Assuming the location is invalid

        if (Physics.Raycast(ray, out hit, maxBuildDistance, LayerMask.GetMask("Default")))
        {
            // Sample the NavMesh at the hit point, checking all areas
            if (NavMesh.SamplePosition(hit.point, out NavMeshHit navHit, _navMeshSampleDistance, NavMesh.AllAreas))
            {
                // Found a NavMesh point. 
                // Now check if it's on the *unbuildable* area.
                if ((navHit.mask & _unbuildableAreaMask) == 0)
                {
                    // We check a sphere at the hit point, with our proximity radius, against the tower layer.
                    int towersFound = Physics.OverlapSphere(navHit.position, _minBuildProximity, _towerLayer).Length;

                    if (towersFound == 0)
                    {
                        // PASSED ALL CHECKS: Not on enemy path AND not too close to another tower.
                        isLocationValid = true;
                        currentBuildLocation = navHit.position; // Use navHit.position for perfect placement
                    }
                }
            }
            
            // Update ray visualization (always show, even if invalid)
            if (aimRay != null)
            {
                aimRay.SetPosition(0, weaponController.ShootPoint.position);
                aimRay.SetPosition(1, hit.point);
            }
        }
        else
        {
            // Raycast hit nothing. Visualize ray miss.
            if (aimRay != null)
            {
                aimRay.SetPosition(0, weaponController.ShootPoint.position);
                aimRay.SetPosition(1, weaponController.ShootPoint.position + ray.direction * maxBuildDistance);
            }
        }
        
        // Update the ghost indicator and location variable based on the final result
        if (isLocationValid)
        {
            UpdateBuildIndicator(currentBuildLocation, true); // Show green indicator
        }
        else
        {
            currentBuildLocation = Vector3.zero; // Mark location as invalid
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
            UpdateBuildMenuUI(selectedTowerIndex);
        }
    }
    
    void UpdateBuildMenuUI(int towerIndex)
    {
        if (towerManager == null || towerManager.availableTowers.Length == 0) return;

        // 1. Get the TowerData for the selected tower
        TowerData selectedTower = towerManager.availableTowers[towerIndex];

        if (selectedTower == null) return;

        // 2. Apply the data to the UI elements
        towerIconImage.sprite = selectedTower.towerIcon;
        towerIconImage.enabled = (selectedTower.towerIcon != null);
        towerNameText.text = selectedTower.towerName;
        towerCostText.text = selectedTower.scrapCost.ToString() + " SCRAP";
        towerDescriptionText.text = selectedTower.description;
    }

    public void CancelBuild()
    {
        buildState = BuildState.Idle;
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
