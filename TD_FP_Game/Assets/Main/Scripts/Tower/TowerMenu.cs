using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class TowerMenu : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("The panel containing the menu.")]
    public GameObject menuRoot;
    [Tooltip("Text to display 'Press E to Manage'.")]
    public GameObject interactPrompt; 
    [Tooltip("The Header text showing Tower Name and Level.")]
    public TextMeshProUGUI headerText;
    
    [Header("Menu Options (Text Objects)")]
    public TextMeshProUGUI optionUpgrade;
    public TextMeshProUGUI optionSell;
    public TextMeshProUGUI optionPriority;
    public TextMeshProUGUI optionClose;

    [Header("Styling")]
    public Color selectedColor = Color.yellow;
    public Color normalColor = Color.white;

    private TowerController _currentTower;
    private PlayerStats _playerStats;
    private int _selectedIndex = 0;
    private int _menuOptionCount = 4;

    void Start()
    {
        _playerStats = FindObjectOfType<PlayerStats>();
        CloseMenu(); // Ensure hidden on start
        if(interactPrompt) interactPrompt.SetActive(false);
    }

    public void ShowPrompt(bool show)
    {
        if(interactPrompt) interactPrompt.SetActive(show);
    }

    public void OpenMenu(TowerController tower)
    {
        _currentTower = tower;
        _selectedIndex = 0;
        
        menuRoot.SetActive(true);
        interactPrompt.SetActive(false); // Hide prompt while menu is open
        UpdateVisuals();
    }

    public void CloseMenu()
    {
        menuRoot.SetActive(false);
        _currentTower = null;
    }

    public bool IsOpen => menuRoot.activeSelf;

    public void Scroll(int direction)
    {
        if (!IsOpen) return;

        // Direction is 1 (up/prev) or -1 (down/next)
        // Note: Scroll wheel Up usually gives positive, Down gives negative
        // If we scroll Down (-1), we want index to increase (go down the list)
        
        _selectedIndex -= direction; 

        // Wrap around
        if (_selectedIndex < 0) _selectedIndex = _menuOptionCount - 1;
        if (_selectedIndex >= _menuOptionCount) _selectedIndex = 0;

        UpdateVisuals();
    }

    public void ExecuteSelection()
    {
        if (!IsOpen || _currentTower == null) return;

        switch (_selectedIndex)
        {
            case 0: // Upgrade
                TryUpgrade();
                break;
            case 1: // Sell
                TrySell();
                break;
            case 2: // Priority
                _currentTower.CyclePriority();
                UpdateVisuals(); // Update text
                break;
            case 3: // Close
                CloseMenu();
                break;
        }
    }

    private void TryUpgrade()
    {
        int cost = _currentTower.GetUpgradeCost();
        if (_playerStats.SpendScrap(cost))
        {
            _currentTower.UpgradeTower();
            UpdateVisuals();
        }
        else
        {
            Debug.Log("Not enough scrap to upgrade!");
            // Optional: Flash red or play sound
        }
    }

    private void TrySell()
    {
        int value = _currentTower.GetSellValue();
        _playerStats.AddScrap(value);
        _currentTower.SellTower();
        CloseMenu();
    }

    private void UpdateVisuals()
    {
        if (_currentTower == null) return;

        // Update Header
        headerText.text = $"{_currentTower.TowerName} (Lvl {_currentTower.Level})";

        // Update Option Texts
        optionUpgrade.text = $"Upgrade (${_currentTower.GetUpgradeCost()})";
        optionSell.text = $"Sell (+${_currentTower.GetSellValue()})";
        optionPriority.text = $"Priority: {_currentTower.GetPriorityName()}";
        optionClose.text = "Close Menu";

        // Update Colors
        optionUpgrade.color = (_selectedIndex == 0) ? selectedColor : normalColor;
        optionSell.color = (_selectedIndex == 1) ? selectedColor : normalColor;
        optionPriority.color = (_selectedIndex == 2) ? selectedColor : normalColor;
        optionClose.color = (_selectedIndex == 3) ? selectedColor : normalColor;
    }
}