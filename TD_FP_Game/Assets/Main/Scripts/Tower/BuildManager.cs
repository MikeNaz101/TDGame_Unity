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
    public WeaponControllerHS weaponController;
    public PlayerStats playerStats; 
    public TowerManager towerManager;
    public StarterAssetsInputs inputScript; 
    public PlayerInput playerInput; 

    [Header("Building Visuals")]
    public GameObject buildMenuUI;
    public LineRenderer aimRay;
    
    [Tooltip("The generic pulsing ring prefab we created earlier.")]
    public GameObject rangeRipplePrefab;
    private GameObject _currentGhostInstance;
    private GameObject _currentRippleInstance;
    private RangeRipple _activeRippleScript;
    private int _lastTowerIndex = -1; // To track when we switch towers
    
    [Header("Tuning")]
    public float maxBuildDistance = 15f;
    [SerializeField] private string _unbuildableAreaName = "EnemyPath";
    [SerializeField] private float _navMeshSampleDistance = 1.0f;
    [SerializeField] private float _minBuildProximity = 1.0f; // Adjusted default
    [SerializeField] private LayerMask _towerLayer;

    private int _unbuildableAreaMask;
    
    [Header("Build Menu UI")]
    public Image towerIconImage;
    public TextMeshProUGUI towerNameText;
    public TextMeshProUGUI towerCostText;
    public TextMeshProUGUI towerDescriptionText;
    
    private InputSystemUIInputModule uiInputModule; 
    private enum BuildState { Idle, Aiming, MenuOpen }
    private BuildState buildState = BuildState.Idle;
    private Transform buildIndicatorInstance; 
    private int selectedTowerIndex = 0;
    
    private Vector3 currentBuildLocation = Vector3.zero;

    void Start()
    {
        if (playerStats == null) playerStats = GetComponentInParent<PlayerStats>();
        if (towerManager == null) towerManager = FindObjectOfType<TowerManager>();
        if (playerInput == null) playerInput = GetComponentInParent<PlayerInput>();
        uiInputModule = FindObjectOfType<InputSystemUIInputModule>();
        
        int unbuildableAreaIndex = NavMesh.GetAreaFromName(_unbuildableAreaName);
        if (unbuildableAreaIndex == -1) _unbuildableAreaMask = 0;
        else _unbuildableAreaMask = 1 << unbuildableAreaIndex;
    }

    void Update()
    {
        if (weaponController.IsReloading || inputScript.aim || inputScript.reload)
        {
            if (buildState != BuildState.Idle) CancelBuild();
            return;
        }

        if (buildState == BuildState.Idle && inputScript.fire) return; 
        
        HandleBuildingStateTransitions();
        switch (buildState)
        {
            case BuildState.Aiming: AimingPhase(); break;
            case BuildState.MenuOpen: MenuSelectionPhase(); break;
            case BuildState.Idle: if (buildMenuUI != null) buildMenuUI.SetActive(false); break;
        }
    }

    void HandleBuildingStateTransitions()
    {
        bool buildPressed = inputScript.build;
        bool firePressed = inputScript.fire;

        if (buildState == BuildState.Idle && buildPressed)
        {
            buildState = BuildState.Aiming;
            if (aimRay != null) aimRay.enabled = true;
            weaponController.ToggleShootingEnabled(false); 
        }
        else if (buildState == BuildState.Aiming && firePressed)
        {
            if (currentBuildLocation != Vector3.zero)
            {
                buildState = BuildState.MenuOpen;
                if (aimRay != null) aimRay.enabled = false;
                UpdateBuildMenuUI(selectedTowerIndex);
                if (buildMenuUI != null) buildMenuUI.SetActive(true);
            }
            inputScript.fire = false; 
            return; 
        }
        else if (buildState == BuildState.MenuOpen && firePressed)
        {
            inputScript.fire = false; 
            if (playerStats.TryBuildTower(selectedTowerIndex, currentBuildLocation)) Debug.Log($"Successfully built tower {selectedTowerIndex}.");
            else Debug.LogWarning("Build failed.");
            CancelBuild(); 
        }
        else if (buildState != BuildState.Idle && !buildPressed)
        {
            CancelBuild();
        }
    }

    void AimingPhase()
    {
        Ray ray = new Ray(weaponController.transform.parent.position, weaponController.transform.parent.forward);
        RaycastHit hit;

        bool isLocationValid = false; 

        // --- NEW: Fetch Range ---
        float towerRange = 5f; // Default safety
        if (towerManager != null && selectedTowerIndex < towerManager.availableTowers.Length)
        {
            towerRange = towerManager.availableTowers[selectedTowerIndex].range;
        }
        // ------------------------

        if (Physics.Raycast(ray, out hit, maxBuildDistance, LayerMask.GetMask("Default")))
        {
            if (NavMesh.SamplePosition(hit.point, out NavMeshHit navHit, _navMeshSampleDistance, NavMesh.AllAreas))
            {
                if ((navHit.mask & _unbuildableAreaMask) == 0)
                {
                    Collider[] hits = Physics.OverlapSphere(navHit.position, _minBuildProximity, _towerLayer);
                    bool isBlockedByTower = false;

                    foreach (Collider col in hits)
                    {
                        if (!col.isTrigger)
                        {
                            isBlockedByTower = true;
                            break; 
                        }
                    }

                    if (!isBlockedByTower)
                    {
                        isLocationValid = true;
                        currentBuildLocation = navHit.position; 
                    }
                }
            }
            
            if (aimRay != null)
            {
                aimRay.SetPosition(0, weaponController.ShootPoint.position);
                aimRay.SetPosition(1, hit.point);
            }
        }
        else
        {
            if (aimRay != null)
            {
                aimRay.SetPosition(0, weaponController.ShootPoint.position);
                aimRay.SetPosition(1, weaponController.ShootPoint.position + ray.direction * maxBuildDistance);
            }
        }
        
        // --- UPDATED CALL: Pass Range ---
        if (isLocationValid) 
            UpdateBuildIndicator(currentBuildLocation, true, towerRange); 
        else 
        { 
            currentBuildLocation = Vector3.zero; 
            UpdateBuildIndicator(Vector3.zero, false, towerRange); 
        }
    }

    void UpdateBuildIndicator(Vector3 position, bool isValid, float range)
    {
        // 1. Handle the Ripple (Create once, reuse always)
        if (_currentRippleInstance == null && rangeRipplePrefab != null)
        {
            _currentRippleInstance = Instantiate(rangeRipplePrefab);
            _activeRippleScript = _currentRippleInstance.GetComponent<RangeRipple>();
        }

        // 2. Handle the Ghost (Re-create only if tower changes)
        if (_lastTowerIndex != selectedTowerIndex)
        {
            // Destroy old ghost
            if (_currentGhostInstance != null) Destroy(_currentGhostInstance);
            
            // Spawn new ghost
            if (towerManager != null && selectedTowerIndex < towerManager.availableTowers.Length)
            {
                TowerData data = towerManager.availableTowers[selectedTowerIndex];
                if (data.ghostPrefab != null)
                {
                    _currentGhostInstance = Instantiate(data.ghostPrefab);
                    
                    // Optional: Strip colliders so the ghost doesn't interfere with raycasts
                    foreach (var c in _currentGhostInstance.GetComponentsInChildren<Collider>()) c.enabled = false;
                }
            }
            _lastTowerIndex = selectedTowerIndex;
        }

        // 3. Update Positions & State
        if (position != Vector3.zero)
        {
            // Update Ripple
            if (_currentRippleInstance != null)
            {
                _currentRippleInstance.transform.position = position;
                _currentRippleInstance.SetActive(true);
                if (_activeRippleScript != null) _activeRippleScript.SetProperties(range, isValid);
            }

            // Update Ghost
            if (_currentGhostInstance != null)
            {
                _currentGhostInstance.transform.position = position;
                _currentGhostInstance.SetActive(true);
                
                // Optional: Tint ghost Red/Green if it has standard renderers
                // (This depends on your materials, simpler to rely on the ripple for color info)
            }
        }
        else
        {
            // Hide everything if aiming at the sky/invalid area
            if (_currentRippleInstance != null) _currentRippleInstance.SetActive(false);
            if (_currentGhostInstance != null) _currentGhostInstance.SetActive(false);
        }
    }

    void MenuSelectionPhase()
    {
        if (inputScript.menuScroll != 0)
        {
            int scrollDirection = (int)Mathf.Sign(inputScript.menuScroll);
            inputScript.menuScroll = 0; 
            int maxTowers = towerManager.availableTowers.Length;
            selectedTowerIndex += scrollDirection;
            selectedTowerIndex = (selectedTowerIndex + maxTowers) % maxTowers;
            UpdateBuildMenuUI(selectedTowerIndex);
        }
    }
    
    void UpdateBuildMenuUI(int towerIndex)
    {
        if (towerManager == null || towerManager.availableTowers.Length == 0) return;
        TowerData selectedTower = towerManager.availableTowers[towerIndex];
        if (selectedTower == null) return;
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
        if (aimRay != null) aimRay.enabled = false;
        if (buildMenuUI != null) buildMenuUI.SetActive(false);
        if (_currentGhostInstance != null) _currentGhostInstance.SetActive(false);
        if (_currentRippleInstance != null) _currentRippleInstance.SetActive(false);
        inputScript.build = false; 
    }
}