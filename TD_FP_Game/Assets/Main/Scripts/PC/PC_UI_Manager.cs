using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class PC_UI_Manager : MonoBehaviour
{
    [Header("Screen Containers")]
    public GameObject MainMenu_Container;
    public GameObject TowerList_Container;
    public GameObject TowerOptions_Container;
    public GameObject TowerSettings_Container;
    public GameObject BotUpgrade_Container;
    // No "InteractPrompt_Container" needed, we will reuse the 'pcDisplay' text.

    [Header("List Prefab")]
    public GameObject TowerListButton_Prefab;
    public Transform TowerList_ContentParent;
    
    [Header("Bot Upgrade UI")]
    public Button btn_UpgradeCapacity;
    public Button btn_UpgradeFlight;
    public Button btn_UpgradeHeal;
    public TextMeshProUGUI botStatusText;

    [Header("Animation")]
    public RectTransform SelectedButton_Transform;
    public Transform SelectedButton_TargetPosition;
    public float buttonAnimSpeed = 10f;

    [Header("Typewriter")]
    public TypeWriterEffect typewriter;
    [Tooltip("The MAIN text display for both the prompt and the welcome message.")]
    public TMP_Text pcDisplay;
    [TextArea(3, 10)]
    public string pcWelcomeMessage = "Welcome. System ready.\nSelect an option.";

    // --- Internal State ---
    private TowerController _selectedTower;
    private GameObject _selectedButtonInstance;
    private ScrapCollectorBot _botScript;
    private PlayerStats _playerStats;
    private Vector3 _selectedButtonOriginalPos;
    private Transform _selectedButtonOriginalParent;
    
    private const int COST_CAPACITY = 50;
    private const int COST_FLIGHT = 150;
    private const int COST_HEAL = 300;
    
    private bool hasShownWelcomeMessage = false;
    private Coroutine _startupCoroutine;

    void Start()
    {
        // Start with ALL screens hidden.
        ShowScreen(null); 
        pcDisplay.gameObject.SetActive(false);
        
        // Find Bot and Player references
        _botScript = FindObjectOfType<ScrapCollectorBot>();
        _playerStats = FindObjectOfType<PlayerStats>();
    }
    
    public void StartPCInterface()
    {
        if (_startupCoroutine != null)
        {
            StopCoroutine(_startupCoroutine);
        }
        _startupCoroutine = StartCoroutine(StartupRoutine());
    }

    public void StopInterface()
    {
        if (typewriter != null)
        {
            typewriter.StopTyping();
        }
        if (_startupCoroutine != null)
        {
            StopCoroutine(_startupCoroutine);
            _startupCoroutine = null;
        }
        ShowScreen(null);
        pcDisplay.gameObject.SetActive(false); // Hide text
        ClearSelectedTower();
    }

    private IEnumerator StartupRoutine()
    {
        // This is called AFTER the 'E' key is pressed
        pcDisplay.gameObject.SetActive(true);
        
        if (!hasShownWelcomeMessage)
        {
            ShowScreen(null); 
            hasShownWelcomeMessage = true;
            
            if (typewriter != null && pcDisplay != null)
            {
                yield return typewriter.DisplayText(pcDisplay, pcWelcomeMessage);
                yield return new WaitForSeconds(1f); 
            }
            
            ShowMainMenu();
        }
        else
        {
            ShowMainMenu();
        }
        
        _startupCoroutine = null;
    }

    // --- STATE 1: MAIN MENU ---
    public void ShowMainMenu()
    {
        pcDisplay.gameObject.SetActive(false); // Hide welcome text
        ShowScreen(MainMenu_Container);
        ClearSelectedTower();
    }

    // --- NEW METHOD ---
    /// <summary>
    /// Shows JUST the interact prompt (called by InteractablePC)
    /// </summary>
    public void ShowInteractPrompt(bool show, string message)
    {
        if (show)
        {
            // Hide all button menus
            ShowScreen(null);
            
            // Show and run the prompt text
            pcDisplay.gameObject.SetActive(true);
            if (typewriter != null)
            {
                typewriter.DisplayText(pcDisplay, message);
            }
        }
        else
        {
            // Hide everything
            ShowScreen(null);
            pcDisplay.gameObject.SetActive(false);
            if (typewriter != null)
            {
                typewriter.StopTyping();
            }
        }
    }
    
    // --- BOT UPGRADE MENU ---
    
    public void OnMainMenu_BotUpgradesClicked()
    {
        RefreshBotUI();
        ShowScreen(BotUpgrade_Container);
        if (typewriter != null) typewriter.DisplayText(pcDisplay, "Bot Modification Module Loaded.");
    }

    private void RefreshBotUI()
    {
        if (_botScript == null)
        {
            botStatusText.text = "ERROR: No Bot Found.";
            return;
        }

        // Logic: Show Capacity upgrade first. 
        // Then Flight. 
        // Then Heal.
        
        bool hasMaxCapacity = _botScript.carryCapacity >= 5;
        bool hasFlight = _botScript.canFly;
        bool hasHeal = _botScript.canHeal;

        // Upgrade 1: Capacity
        btn_UpgradeCapacity.gameObject.SetActive(!hasMaxCapacity);
        btn_UpgradeCapacity.GetComponentInChildren<TMP_Text>().text = $"Expand Cargo ({COST_CAPACITY} Scrap)";

        // Upgrade 2: Flight (Only appears if Capacity is upgraded at least once)
        btn_UpgradeFlight.gameObject.SetActive(_botScript.carryCapacity > 1 && !hasFlight);
        btn_UpgradeFlight.GetComponentInChildren<TMP_Text>().text = $"Flight Systems ({COST_FLIGHT} Scrap)";

        // Upgrade 3: Heal (Only appears if Flight is unlocked)
        btn_UpgradeHeal.gameObject.SetActive(hasFlight && !hasHeal);
        btn_UpgradeHeal.GetComponentInChildren<TMP_Text>().text = $"Medical Module ({COST_HEAL} Scrap)";
        
        // Status Text
        string status = $"Current Load: {_botScript.carryCapacity} slots\n";
        status += hasFlight ? "Flight: ONLINE\n" : "Flight: Offline\n";
        status += hasHeal ? "Medical: ONLINE" : "Medical: Offline";
        botStatusText.text = status;
    }

    public void OnBuy_Capacity()
    {
        if (_playerStats.SpendScrap(COST_CAPACITY))
        {
            _botScript.UpgradeCapacity(_botScript.carryCapacity + 2); // Add 2 slots
            if(typewriter != null) typewriter.DisplayText(pcDisplay, "Cargo capacity expanded.");
            RefreshBotUI();
        }
        else
        {
            if(typewriter != null) typewriter.DisplayText(pcDisplay, "Insufficient Funds.");
        }
    }

    public void OnBuy_Flight()
    {
        if (_playerStats.SpendScrap(COST_FLIGHT))
        {
            _botScript.UpgradeFlight();
            if(typewriter != null) typewriter.DisplayText(pcDisplay, "Flight systems installed.");
            RefreshBotUI();
        }
        else
        {
            if(typewriter != null) typewriter.DisplayText(pcDisplay, "Insufficient Funds.");
        }
    }

    public void OnBuy_Heal()
    {
        if (_playerStats.SpendScrap(COST_HEAL))
        {
            _botScript.UpgradeHealing();
            if(typewriter != null) typewriter.DisplayText(pcDisplay, "Medical protocols active.");
            RefreshBotUI();
        }
        else
        {
            if(typewriter != null) typewriter.DisplayText(pcDisplay, "Insufficient Funds.");
        }
    }

    // --- STATE 2: TOWER LIST ---
    public void OnMainMenu_TowersClicked()
    {
        if (typewriter != null) typewriter.StopTyping();
        
        // 1. Clear any old buttons from the list
        foreach (Transform child in TowerList_ContentParent)
        {
            Destroy(child.gameObject);
        }

        // 2. Generate new buttons from the TowerRegistry
        foreach (TowerController tower in TowerRegistry.ActiveTowers)
        {
            GameObject buttonObj = Instantiate(TowerListButton_Prefab, TowerList_ContentParent);
            
            // Set the button's text
            TMP_Text buttonText = buttonObj.GetComponentInChildren<TMP_Text>();
            if (buttonText != null)
            {
                // e.g., "BombTower (1)"
                buttonText.text = $"{tower.TowerName} ({tower.TowerID})";
            }

            // Add a listener for when this button is clicked
            Button button = buttonObj.GetComponent<Button>();
            
            // We use a local copy 't' so the listener captures the correct tower
            TowerController t = tower; 
            button.onClick.AddListener(() => OnTowerListButton_Clicked(t, buttonObj));
        }

        // 3. Show the tower list screen
        ShowScreen(TowerList_Container);
    }

    // --- STATE 3: TOWER OPTIONS ---
    public void OnTowerListButton_Clicked(TowerController tower, GameObject buttonInstance)
    {
        _selectedTower = tower;
        
        // Hide the list screen
        ShowScreen(TowerOptions_Container); 

        // --- Handle Button Animation ---
        
        // 1. Store the button's original state
        _selectedButtonInstance = buttonInstance;
        _selectedButtonOriginalPos = buttonInstance.transform.position;
        _selectedButtonOriginalParent = buttonInstance.transform.parent;
        
        // 2. Move the button to the "Selected" area
        // We use the 'SelectedButton_Transform' as a dummy container
        buttonInstance.transform.SetParent(SelectedButton_Transform, true);
        
        // 3. Update the text to match the new container's layout
        TMP_Text buttonText = buttonInstance.GetComponentInChildren<TMP_Text>();
        if (buttonText != null)
        {
            buttonText.text = $"{_selectedTower.TowerName} ({_selectedTower.TowerID})";
        }
    }

    void Update()
    {
        // This coroutine-like update animates the selected button to its target position
        if (_selectedButtonInstance != null && SelectedButton_TargetPosition != null)
        {
            _selectedButtonInstance.transform.position = Vector3.Lerp(
                _selectedButtonInstance.transform.position,
                SelectedButton_TargetPosition.position,
                Time.deltaTime * buttonAnimSpeed
            );
        }
    }

    // --- STATE 4: TOWER SETTINGS ---
    public void OnTowerOptions_SettingsClicked()
    {
        // Hide Options, Show Settings
        ShowScreen(TowerSettings_Container);
    }
    
    // --- BUTTON ACTIONS ---
    
    /// <summary>
    /// This is called by the "First", "Last", "Weakest", "Strongest" buttons.
    /// You must assign the index in the Inspector (0, 1, 2, 3).
    /// </summary>
    public void OnTowerSettings_SetPriority(int priorityIndex)
    {
        if (_selectedTower != null)
        {
            _selectedTower.SetTargetingPriority(priorityIndex);
        }
        
        // Go back to the options menu
        ShowScreen(TowerOptions_Container);
    }

    // --- NAVIGATION ---
    
    /// <summary>Called by a "Back" button to go to the tower list.</summary>
    public void BackToTowerList()
    {
        ClearSelectedTower();
        OnMainMenu_TowersClicked(); // Re-build the list and show it
    }
    
    /// <summary>Called by a "Back" button to go to the tower options.</summary>
    public void BackToTowerOptions()
    {
        ShowScreen(TowerOptions_Container);
    }

    // --- HELPER METHODS ---
    
    /// <summary>Hides all screens and shows the requested one.</summary>
    private void ShowScreen(GameObject screenToShow)
    {
        MainMenu_Container.SetActive(false);
        TowerList_Container.SetActive(false);
        TowerOptions_Container.SetActive(false);
        TowerSettings_Container.SetActive(false);
        if(BotUpgrade_Container != null) BotUpgrade_Container.SetActive(false); // Hide bot screen too
        
        if (screenToShow != null)
        {
            screenToShow.SetActive(true);
        }
    }

    /// <summary>Returns the selected button to the list.</summary>
    private void ClearSelectedTower()
    {
        _selectedTower = null;
        if (_selectedButtonInstance != null)
        {
            // Restore its original parent and position (or just destroy it)
            Destroy(_selectedButtonInstance);
            _selectedButtonInstance = null;
        }
    }
}