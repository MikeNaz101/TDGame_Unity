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
    
    [Header("Main Menu Navigation")]
    // Drag your buttons here in order: Upgrade, Sell, Priority, Back
    public Button[] mainMenuButtons; 
    public Color normalColor = Color.white;
    public Color selectedColor = Color.yellow;
    // --- FIX: Store the base scale you want (e.g. 0.1) ---
    public Vector3 buttonBaseScale = new Vector3(0.1f, 0.1f, 0.1f);
    
    [Header("Upgrade Carousel Settings")]
    public RectTransform carouselContent; 
    public GameObject upgradeNodePrefab; 
    public float nodeSpacing = 250f; 
    public float scrollSpeed = 10f; 
    
    [Header("Details Panel")]
    public TextMeshProUGUI detailsNameText;
    public TextMeshProUGUI detailsDescText;
    public TextMeshProUGUI detailsCostText;
    public Button purchaseButton;

    private TowerController _currentTower;
    private PlayerStats _playerStats;
    
    // State
    private List<TowerUpgradeNodeUI> _spawnedNodes = new List<TowerUpgradeNodeUI>();
    private int _selectedPathIndex = 0; 
    private int _selectedNodeIndex = 0; 
    
    private int _selectedMainOptionIndex = 0; // NEW: Track main menu selection
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

    // --- INPUT HANDLING (Called by Player) ---

    public void Scroll(int direction)
    {
        if (!IsOpen) return;

        if (_inUpgradeMenu)
        {
            // Scroll Upgrade Carousel
            if (_spawnedNodes.Count == 0) return;
            _selectedNodeIndex -= direction; 
            _selectedNodeIndex = Mathf.Clamp(_selectedNodeIndex, 0, _spawnedNodes.Count - 1);
            UpdateDetailsPanel();
        }
        else
        {
            // Scroll Main Menu Options
            _selectedMainOptionIndex -= direction;
            
            // Wrap around logic
            if (_selectedMainOptionIndex < 0) _selectedMainOptionIndex = mainMenuButtons.Length - 1;
            if (_selectedMainOptionIndex >= mainMenuButtons.Length) _selectedMainOptionIndex = 0;
            
            UpdateMainMenuVisuals();
        }
    }

    public void ExecuteSelection()
    {
        if (!IsOpen) return;

        if (_inUpgradeMenu)
        {
            TryBuySelectedUpgrade();
        }
        else
        {
            // Trigger the button click of the selected option
            if (mainMenuButtons.Length > 0)
            {
                mainMenuButtons[_selectedMainOptionIndex].onClick.Invoke();
            }
        }
    }

    // --- MAIN MENU VISUALS ---

    private void UpdateMainMenuVisuals()
    {
        for (int i = 0; i < mainMenuButtons.Length; i++)
        {
            // Change color of text or image based on selection
            var btnText = mainMenuButtons[i].GetComponentInChildren<TextMeshProUGUI>();
            if (btnText != null)
            {
                btnText.color = (i == _selectedMainOptionIndex) ? selectedColor : normalColor;
            }
            
            // --- FIX: Scale relative to your base scale ---
            // If selected, scale up by 10% (1.1x). If not, reset to base scale.
            mainMenuButtons[i].transform.localScale = (i == _selectedMainOptionIndex) ? buttonBaseScale * 1.1f : buttonBaseScale;
        }
    }

    // --- STANDARD MENU STUFF ---

    public void ShowPrompt(bool show) { if(interactPrompt) interactPrompt.SetActive(show); }
    public bool IsOpen => menuRoot.activeSelf;

    public void OpenMenu(TowerController tower)
    {
        _currentTower = tower;
        _inUpgradeMenu = false;
        _selectedMainOptionIndex = 0; // Reset to top
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
        UpdateMainMenuVisuals(); // Refresh highlights
    }

    // --- BUTTON EVENTS ---

    public void OnClick_OpenUpgrades()
    {
        _inUpgradeMenu = true;
        MainOptions_Container.SetActive(false);
        UpgradeTree_Container.SetActive(true);
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
        // Optional: Update priority button text here
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

    // --- CAROUSEL LOGIC ---

    private void UpdateCarouselPosition()
    {
        if (_spawnedNodes.Count == 0) return;
        
        // 1. Target X Calculation
        // We move the CONTAINER, not the buttons. 
        // To center button at index 2 (X = 500), we move container to X = -500.
        float targetX = -(_selectedNodeIndex * nodeSpacing);
        
        // 2. Smooth Move
        Vector2 currentPos = carouselContent.anchoredPosition;
        float newX = Mathf.Lerp(currentPos.x, targetX, Time.deltaTime * scrollSpeed);
        carouselContent.anchoredPosition = new Vector2(newX, currentPos.y);

        // 3. Visuals (Scaling Effect)
        // We compare the button's world position relative to the center of the mask
        // Since we are moving the parent, the button's localPosition.x + parent.anchoredPosition.x = distance from center
        
        float contentX = carouselContent.anchoredPosition.x;
        
        for (int i = 0; i < _spawnedNodes.Count; i++)
        {
            float buttonLocalX = i * nodeSpacing;
            float distFromCenter = Mathf.Abs(contentX + buttonLocalX);
            
            // Pass this distance to the node UI to handle scaling
            _spawnedNodes[i].UpdateVisuals(distFromCenter);
        }
    }

    private void GenerateUpgradeNodes()
    {
        foreach (Transform child in carouselContent) Destroy(child.gameObject);
        _spawnedNodes.Clear();

        if (_currentTower == null || _currentTower.Data == null) return;
        // --- SAFEGUARD: Return if no paths ---
        if (_currentTower.Data.upgradePaths == null || _currentTower.Data.upgradePaths.Count <= _selectedPathIndex) return;
        
        var path = _currentTower.Data.upgradePaths[_selectedPathIndex];
        
        // --- SAFEGUARD: Return if PathProgress not initialized ---
        if (_currentTower.PathProgress == null || _currentTower.PathProgress.Length <= _selectedPathIndex) return;

        int currentLevel = _currentTower.PathProgress[_selectedPathIndex]; 

        for (int i = 0; i < path.upgrades.Count; i++)
        {
            GameObject obj = Instantiate(upgradeNodePrefab, carouselContent);
            RectTransform rt = obj.GetComponent<RectTransform>();
            
            // --- FIX: Ensure anchors are centered for correct calculation ---
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            
            // Set position manually
            rt.anchoredPosition = new Vector2(i * nodeSpacing, 0);

            TowerUpgradeNodeUI nodeUI = obj.GetComponent<TowerUpgradeNodeUI>();
            bool isPurchased = i < currentLevel;
            bool isUnlockable = i == currentLevel; 
            
            nodeUI.Setup(path.upgrades[i], isPurchased, isUnlockable, i < path.upgrades.Count - 1);
            _spawnedNodes.Add(nodeUI);
        }
        
        _selectedNodeIndex = Mathf.Clamp(currentLevel, 0, path.upgrades.Count - 1);
        
        // Force immediate snap to correct position so it doesn't slide in from 0
        float initialTargetX = -(_selectedNodeIndex * nodeSpacing);
        carouselContent.anchoredPosition = new Vector2(initialTargetX, carouselContent.anchoredPosition.y);
        
        UpdateDetailsPanel();
    }

    private void UpdateDetailsPanel()
    {
        // --- SAFEGUARD: Check nodes exist ---
        if (_currentTower == null || _spawnedNodes.Count == 0) return;
        if (_currentTower.Data.upgradePaths.Count <= _selectedPathIndex) return;

        var path = _currentTower.Data.upgradePaths[_selectedPathIndex];
        
        // --- SAFEGUARD: Check node index ---
        if (_selectedNodeIndex >= path.upgrades.Count) return;

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
            purchaseButton.interactable = true;
        }
        else
        {
            detailsCostText.text = "LOCKED";
            purchaseButton.interactable = false;
        }
    }

    public void TryBuySelectedUpgrade()
    {
        // --- SAFEGUARD: Validations before access ---
        if (_currentTower == null || _currentTower.Data == null) return;
        if (_currentTower.PathProgress == null || _currentTower.PathProgress.Length <= _selectedPathIndex) return;
        if (_currentTower.Data.upgradePaths == null || _currentTower.Data.upgradePaths.Count <= _selectedPathIndex) return;

        int currentLevel = _currentTower.PathProgress[_selectedPathIndex];
        if (_selectedNodeIndex != currentLevel) return;

        var path = _currentTower.Data.upgradePaths[_selectedPathIndex];
        
        if (_selectedNodeIndex >= path.upgrades.Count) return; // Prevent out of range if index is wrong

        var upgrade = path.upgrades[_selectedNodeIndex];

        if (_playerStats.SpendScrap(upgrade.cost))
        {
            _currentTower.ApplyUpgrade(upgrade);
            _currentTower.PathProgress[_selectedPathIndex]++;
            GenerateUpgradeNodes();
        }
        else
        {
            Debug.Log("Not enough scrap!");
        }
    }
}