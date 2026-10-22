using UnityEngine;
using UnityEngine.AI;
using StarterAssets;
using TMPro;
using UnityEngine.InputSystem;
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
    [Tooltip("The PlayerInput component. (Unused, kept for reference)")]
    public PlayerInput playerInput;

    [Header("Building Visuals")]
    [Tooltip("The UI panel that shows the list of buildable towers.")]
    public GameObject buildMenuUI;
    [Tooltip("The LineRenderer component used to draw the aim ray.")]
    public LineRenderer aimRay;
    [Tooltip("Prefab for the ghost indicator (must have Renderer).")]
    public GameObject buildIndicatorPrefab;
    public Color validColor = new Color(0.2f, 1f, 0.3f, 0.6f);
    public Color invalidColor = new Color(1f, 0.2f, 0.2f, 0.6f);
    public Color cantAffordColor = new Color(1f, 0.6f, 0.1f, 0.6f);

    [Header("Tuning")]
    [Tooltip("Max distance the player can place a tower.")]
    public float maxBuildDistance = 15f;
    [Tooltip("Layers the build ray can hit (ground). Defaults to 'Default' if empty.")]
    [SerializeField] private LayerMask _groundLayers;
    [Tooltip("The name of the NavMesh Area that blocks building (e.g., 'EnemyPath').")]
    [SerializeField] private string _unbuildableAreaName = "EnemyPath";
    [Tooltip("How close to the ground the raycast hit must be to a valid NavMesh point.")]
    [SerializeField] private float _navMeshSampleDistance = 1.0f;
    [Tooltip("The minimum distance allowed between towers.")]
    [SerializeField] private float _minBuildProximity = 3.0f;
    [Tooltip("The LayerMask containing only the 'Tower' layer.")]
    [SerializeField] private LayerMask _towerLayer;

    [Header("Build Menu UI")]
    public Image towerIconImage;
    public TextMeshProUGUI towerNameText;
    public TextMeshProUGUI towerCostText;
    public TextMeshProUGUI towerDescriptionText;
    public Color affordableCostColor = Color.white;
    public Color unaffordableCostColor = new Color(1f, 0.35f, 0.35f);

    [Header("Audio (optional)")]
    public AudioSource audioSource;
    public AudioClip buildFailSound;

    private enum BuildState { Idle, Aiming, MenuOpen }
    private BuildState buildState = BuildState.Idle;
    private Transform buildIndicatorInstance;
    private Renderer _indicatorRenderer;
    private MaterialPropertyBlock _indicatorMpb;
    private int selectedTowerIndex = 0;
    private int _unbuildableAreaMask;
    private Vector3 currentBuildLocation = Vector3.zero;
    private bool _hasValidLocation;

    private static readonly Collider[] s_overlapBuffer = new Collider[8];
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    public bool IsBuilding => buildState != BuildState.Idle;

    void Start()
    {
        if (playerStats == null) playerStats = GetComponentInParent<PlayerStats>();
        if (towerManager == null) towerManager = FindFirstObjectByType<TowerManager>();
        if (playerInput == null) playerInput = GetComponentInParent<PlayerInput>();
        if (inputScript == null) inputScript = GetComponentInParent<StarterAssetsInputs>();
        if (weaponController == null) weaponController = GetComponentInParent<WeaponControllerHS>();
        if (_groundLayers.value == 0) _groundLayers = LayerMask.GetMask("Default");

        _indicatorMpb = new MaterialPropertyBlock();

        int unbuildableAreaIndex = NavMesh.GetAreaFromName(_unbuildableAreaName);
        if (unbuildableAreaIndex == -1)
        {
            Debug.LogWarning($"NavMesh Area '{_unbuildableAreaName}' not found. Enemy-path build blocking is disabled.", this);
            _unbuildableAreaMask = 0;
        }
        else
        {
            _unbuildableAreaMask = 1 << unbuildableAreaIndex;
        }

        if (buildMenuUI != null) buildMenuUI.SetActive(false);
        if (aimRay != null) aimRay.enabled = false;
    }

    void Update()
    {
        if (weaponController == null || inputScript == null) return;
        if (GameManager.Instance != null && GameManager.Instance.IsGameOver)
        {
            if (buildState != BuildState.Idle) CancelBuild();
            return;
        }

        // 1. Non-fire combat inputs always cancel building.
        if (weaponController.IsReloading || inputScript.aim || inputScript.reload)
        {
            if (buildState != BuildState.Idle) CancelBuild();
            return;
        }

        // 2. While idle, 'fire' belongs to the weapon.
        if (buildState == BuildState.Idle && inputScript.fire) return;

        HandleBuildingStateTransitions();

        switch (buildState)
        {
            case BuildState.Aiming:   AimingPhase(); break;
            case BuildState.MenuOpen: MenuSelectionPhase(); break;
        }
    }

    void HandleBuildingStateTransitions()
    {
        bool buildPressed = inputScript.build;
        bool firePressed = inputScript.fire;

        // IDLE -> AIMING (Q held)
        if (buildState == BuildState.Idle && buildPressed)
        {
            buildState = BuildState.Aiming;
            if (aimRay != null) aimRay.enabled = true;
            weaponController.ToggleShootingEnabled(false);
        }
        // AIMING -> MENUOPEN (Fire pressed on a valid spot)
        else if (buildState == BuildState.Aiming && firePressed)
        {
            inputScript.fire = false; // consume
            if (_hasValidLocation)
            {
                buildState = BuildState.MenuOpen;
                if (aimRay != null) aimRay.enabled = false;
                ClampSelectedIndex();
                UpdateBuildMenuUI(selectedTowerIndex);
                if (buildMenuUI != null) buildMenuUI.SetActive(true);
            }
            else
            {
                PlayFail();
            }
        }
        // MENUOPEN -> BUILD CONFIRMED (Fire pressed again)
        else if (buildState == BuildState.MenuOpen && firePressed)
        {
            inputScript.fire = false;

            bool success = playerStats != null && playerStats.TryBuildTower(selectedTowerIndex, currentBuildLocation);
            if (!success)
            {
                PlayFail();
                // Stay in the menu so the player can pick a cheaper tower.
                UpdateBuildMenuUI(selectedTowerIndex);
                return;
            }
            CancelBuild();
        }
        // CANCEL (Q released from any non-idle state)
        else if (buildState != BuildState.Idle && !buildPressed)
        {
            CancelBuild();
        }
    }

    void AimingPhase()
    {
        Transform cam = weaponController.transform.parent != null ? weaponController.transform.parent : weaponController.transform;
        Ray ray = new Ray(cam.position, cam.forward);
        Vector3 rayStart = weaponController.ShootPoint != null ? weaponController.ShootPoint.position : cam.position;

        _hasValidLocation = false;
        bool hitSomething = Physics.Raycast(ray, out RaycastHit hit, maxBuildDistance, _groundLayers, QueryTriggerInteraction.Ignore);
        Vector3 indicatorPos = hitSomething ? hit.point : ray.GetPoint(maxBuildDistance);

        if (hitSomething && NavMesh.SamplePosition(hit.point, out NavMeshHit navHit, _navMeshSampleDistance, NavMesh.AllAreas))
        {
            bool onEnemyPath = (navHit.mask & _unbuildableAreaMask) != 0;
            if (!onEnemyPath)
            {
                int towersFound = Physics.OverlapSphereNonAlloc(navHit.position, _minBuildProximity, s_overlapBuffer, _towerLayer, QueryTriggerInteraction.Ignore);
                if (towersFound == 0)
                {
                    _hasValidLocation = true;
                    currentBuildLocation = navHit.position;
                    indicatorPos = navHit.position;
                }
            }
        }

        if (aimRay != null)
        {
            aimRay.SetPosition(0, rayStart);
            aimRay.SetPosition(1, indicatorPos);
        }

        bool canAfford = playerStats == null || towerManager == null || !towerManager.IsValidIndex(selectedTowerIndex)
            || playerStats.CanAfford(towerManager.GetTowerCost(selectedTowerIndex));
        UpdateBuildIndicator(indicatorPos, _hasValidLocation, canAfford);
    }

    void MenuSelectionPhase()
    {
        if (towerManager == null || towerManager.availableTowers == null || towerManager.availableTowers.Length == 0) return;

        if (inputScript.menuScroll != 0)
        {
            int scrollDirection = (int)Mathf.Sign(inputScript.menuScroll);
            inputScript.menuScroll = 0;

            int maxTowers = towerManager.availableTowers.Length;
            selectedTowerIndex = (selectedTowerIndex + scrollDirection + maxTowers) % maxTowers;
            UpdateBuildMenuUI(selectedTowerIndex);
        }
    }

    private void ClampSelectedIndex()
    {
        int count = towerManager != null && towerManager.availableTowers != null ? towerManager.availableTowers.Length : 0;
        if (count == 0) { selectedTowerIndex = 0; return; }
        selectedTowerIndex = ((selectedTowerIndex % count) + count) % count;
    }

    void UpdateBuildMenuUI(int towerIndex)
    {
        if (towerManager == null) return;
        TowerData selectedTower = towerManager.GetTowerData(towerIndex);
        if (selectedTower == null) return;

        if (towerIconImage != null)
        {
            towerIconImage.sprite = selectedTower.towerIcon;
            towerIconImage.enabled = selectedTower.towerIcon != null;
        }
        if (towerNameText != null) towerNameText.text = selectedTower.towerName;
        if (towerDescriptionText != null) towerDescriptionText.text = selectedTower.description;
        if (towerCostText != null)
        {
            towerCostText.text = selectedTower.scrapCost + " SCRAP";
            bool affordable = playerStats == null || playerStats.CanAfford(selectedTower.scrapCost);
            towerCostText.color = affordable ? affordableCostColor : unaffordableCostColor;
        }
    }

    public void CancelBuild()
    {
        buildState = BuildState.Idle;
        _hasValidLocation = false;
        weaponController.ToggleShootingEnabled(true);

        if (aimRay != null) aimRay.enabled = false;
        if (buildMenuUI != null) buildMenuUI.SetActive(false);
        if (buildIndicatorInstance != null) buildIndicatorInstance.gameObject.SetActive(false);

        inputScript.build = false;
    }

    void UpdateBuildIndicator(Vector3 position, bool isValid, bool canAfford)
    {
        if (buildIndicatorPrefab == null) return;

        if (buildIndicatorInstance == null)
        {
            // Created once and toggled, instead of Instantiate/Destroy every build attempt.
            buildIndicatorInstance = Instantiate(buildIndicatorPrefab).transform;
            _indicatorRenderer = buildIndicatorInstance.GetComponentInChildren<Renderer>();
        }

        buildIndicatorInstance.position = position;
        buildIndicatorInstance.gameObject.SetActive(true);

        if (_indicatorRenderer != null)
        {
            Color c = !isValid ? invalidColor : (canAfford ? validColor : cantAffordColor);
            // Property block: no per-frame material instance allocation.
            _indicatorMpb.SetColor(BaseColorId, c);
            _indicatorMpb.SetColor(ColorId, c);
            _indicatorRenderer.SetPropertyBlock(_indicatorMpb);
        }
    }

    private void PlayFail()
    {
        if (audioSource != null && buildFailSound != null) audioSource.PlayOneShot(buildFailSound);
    }

    void OnDestroy()
    {
        if (buildIndicatorInstance != null) Destroy(buildIndicatorInstance.gameObject);
    }
}
