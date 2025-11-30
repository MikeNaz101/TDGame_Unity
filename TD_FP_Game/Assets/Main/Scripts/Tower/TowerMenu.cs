using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;

public class TowerMenu : MonoBehaviour
{
    [Header("UI References")]
    public GameObject menuRoot;
    public GameObject interactPrompt; 
    public TextMeshProUGUI headerText;
    
    [Header("Menu Containers")]
    public GameObject MainOptions_Container; 
    public GameObject UpgradeTree_Container; 
    
    [Header("Upgrade Carousel Settings")]
    public RectTransform carouselContent; // The object that moves
    public GameObject upgradeNodePrefab; 
    public float nodeSpacing = 250f; // Distance between buttons
    public float scrollSpeed = 10f; // Lerp speed
    
    [Header("Details Panel")]
    public TextMeshProUGUI detailsNameText;
    public TextMeshProUGUI detailsDescText;
    public TextMeshProUGUI detailsCostText;
    public Button purchaseButton;

    private TowerController _currentTower;
    private PlayerStats _playerStats;
    
    // Carousel State
    private List<TowerUpgradeNodeUI> _spawnedNodes = new List<TowerUpgradeNodeUI>();
    private int _selectedPathIndex = 0; // Which path (A, B, C)
    private int _selectedNodeIndex = 0; // Which node in that path (0, 1, 2...)
    private bool _inUpgradeMenu = false;

    void Start()
    {
        _playerStats = FindObjectOfType<PlayerStats>();
        CloseMenu(); 
        if(interactPrompt) interactPrompt.SetActive(false);
    }

    void Update()
    {
        if (_inUpgradeMenu)
        {
            UpdateCarouselPosition();
        }
    }

    // --- CAROUSEL LOGIC ---

    private void UpdateCarouselPosition()
    {
        if (_spawnedNodes.Count == 0) return;

        // 1. Calculate Target Position
        // We want the selected node to be at local X = 0
        // If selected is index 1, its local X is (1 * spacing).
        // To center it, content needs to be at (-1 * spacing).
        float targetX = -(_selectedNodeIndex * nodeSpacing);
        
        // 2. Smooth Move
        Vector2 currentPos = carouselContent.anchoredPosition;
        float newX = Mathf.Lerp(currentPos.x, targetX, Time.deltaTime * scrollSpeed);
        carouselContent.anchoredPosition = new Vector2(newX, currentPos.y);

        // 3. Update Visuals (Scaling)
        float centerX = -carouselContent.anchoredPosition.x; // The 'world' x that represents the center of screen relative to content
        
        for (int i = 0; i < _spawnedNodes.Count; i++)
        {
            // Calculate how far this node is from the center point
            float nodeX = i * nodeSpacing;
            float dist = centerX - nodeX;
            _spawnedNodes[i].UpdateVisuals(dist);
        }
    }

    public void Scroll(int direction)
    {
        if (!_inUpgradeMenu || _spawnedNodes.Count == 0) return;

        // Direction: 1 (Up/Left), -1 (Down/Right)
        // If we scroll Down (-1), we want to go to the NEXT item (Index++)
        
        _selectedNodeIndex -= direction; 
        _selectedNodeIndex = Mathf.Clamp(_selectedNodeIndex, 0, _spawnedNodes.Count - 1);
        
        UpdateDetailsPanel();
    }

    private void GenerateUpgradeNodes()
    {
        // Clear old
        foreach (Transform child in carouselContent) Destroy(child.gameObject);
        _spawnedNodes.Clear();

        if (_currentTower == null || _currentTower.Data == null) return;
        
        // Get the current path
        // Safety check if tower has paths
        if (_currentTower.Data.upgradePaths.Count <= _selectedPathIndex) return;
        
        var path = _currentTower.Data.upgradePaths[_selectedPathIndex];
        int currentLevel = _currentTower.PathProgress[_selectedPathIndex]; // 0 = nothing bought

        for (int i = 0; i < path.upgrades.Count; i++)
        {
            GameObject obj = Instantiate(upgradeNodePrefab, carouselContent);
            
            // Position it manually in the horizontal line
            RectTransform rt = obj.GetComponent<RectTransform>();
            rt.anchoredPosition = new Vector2(i * nodeSpacing, 0);

            // Configure
            TowerUpgradeNodeUI nodeUI = obj.GetComponent<TowerUpgradeNodeUI>();
            bool isPurchased = i < currentLevel;
            bool isUnlockable = i == currentLevel; // The immediate next one
            
            nodeUI.Setup(path.upgrades[i], isPurchased, isUnlockable, i < path.upgrades.Count - 1);
            _spawnedNodes.Add(nodeUI);
        }
        
        // Auto-select the next available upgrade
        _selectedNodeIndex = Mathf.Clamp(currentLevel, 0, path.upgrades.Count - 1);
        UpdateDetailsPanel();
    }

    private void UpdateDetailsPanel()
    {
        if (_currentTower == null || _spawnedNodes.Count == 0) return;

        var path = _currentTower.Data.upgradePaths[_selectedPathIndex];
        var upgradeData = path.upgrades[_selectedNodeIndex];
        int currentLevel = _currentTower.PathProgress[_selectedPathIndex];

        detailsNameText.text = upgradeData.upgradeName;
        detailsDescText.text = upgradeData.description;
        
        bool isPurchased = _selectedNodeIndex < currentLevel;
        bool isUnlockable = _selectedNodeIndex == currentLevel;

        if (isPurchased)
        {
            detailsCostText.text = "OWNED";
            purchaseButton.interactable = false;
        }
        else if (isUnlockable)
        {
            detailsCostText.text = $"${upgradeData.cost}";
            // Check funds logic could govern interactable here too
            purchaseButton.interactable = true;
        }
        else
        {
            detailsCostText.text = "LOCKED";
            purchaseButton.interactable = false;
        }
    }

    public void ExecuteSelection()
    {
        if (_inUpgradeMenu)
        {
            TryBuySelectedUpgrade();
        }
        else
        {
            // Handle Main Options click (Upgrade, Sell, etc.)
            // Assuming mouse clicks handle those buttons directly via Inspector events
        }
    }

    public void TryBuySelectedUpgrade()
    {
        int currentLevel = _currentTower.PathProgress[_selectedPathIndex];
        
        // Can only buy if it's the next one in line
        if (_selectedNodeIndex != currentLevel) return;

        var path = _currentTower.Data.upgradePaths[_selectedPathIndex];
        var upgrade = path.upgrades[_selectedNodeIndex];

        if (_playerStats.SpendScrap(upgrade.cost))
        {
            // Apply upgrade to tower
            _currentTower.ApplyUpgrade(upgrade);
            
            // Increment progress
            _currentTower.PathProgress[_selectedPathIndex]++;
            
            // Refresh UI
            GenerateUpgradeNodes();
        }
        else
        {
            Debug.Log("Not enough scrap!");
        }
    }

    // --- STANDARD MENU STUFF ---

    public void ShowPrompt(bool show) { if(interactPrompt) interactPrompt.SetActive(show); }
    public bool IsOpen => menuRoot.activeSelf;

    public void OpenMenu(TowerController tower)
    {
        _currentTower = tower;
        _inUpgradeMenu = false;
        menuRoot.SetActive(true);
        interactPrompt.SetActive(false);
        ShowMainOptions();
    }

    public void CloseMenu()
    {
        menuRoot.SetActive(false);
        _currentTower = null;
    }

    private void ShowMainOptions()
    {
        MainOptions_Container.SetActive(true);
        UpgradeTree_Container.SetActive(false);
        if(headerText) headerText.text = _currentTower.TowerName;
    }

    public void OnClick_OpenUpgrades()
    {
        _inUpgradeMenu = true;
        MainOptions_Container.SetActive(false);
        UpgradeTree_Container.SetActive(true);
        
        // Reset to path 0 for now (you can add buttons to switch paths later)
        _selectedPathIndex = 0; 
        GenerateUpgradeNodes();
    }

    public void OnClick_Sell()
    {
        _playerStats.AddScrap(_currentTower.GetSellValue());
        _currentTower.SellTower();
        CloseMenu();
    }

    public void OnClick_Priority()
    {
        _currentTower.CyclePriority();
    }

    public void OnClick_Back()
    {
        if (_inUpgradeMenu)
        {
            ShowMainOptions();
            _inUpgradeMenu = false;
        }
        else
        {
            CloseMenu();
        }
    }
}