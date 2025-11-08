using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Manages the entire UI state machine for the interactive PC.
/// Lives on the PC_Screen_Canvas.
/// </summary>
public class PC_UI_Manager : MonoBehaviour
{
    [Header("Screen Containers")]
    [Tooltip("The parent object for the main menu buttons (Player, Towers, etc.)")]
    public GameObject MainMenu_Container;
    [Tooltip("The parent object for the generated tower list buttons.")]
    public GameObject TowerList_Container;
    [Tooltip("The parent object for the 'Settings', 'Upgrades', 'Salvage' buttons.")]
    public GameObject TowerOptions_Container;
    [Tooltip("The parent object for the 4 targeting priority buttons.")]
    public GameObject TowerSettings_Container;

    [Header("List Prefab")]
    [Tooltip("The Button prefab to be spawned for each tower in the list.")]
    public GameObject TowerListButton_Prefab;
    [Tooltip("The parent object (with a Vertical Layout Group) to spawn buttons into.")]
    public Transform TowerList_ContentParent;

    [Header("Animation")]
    [Tooltip("The RectTransform of the selected button (for moving it).")]
    public RectTransform SelectedButton_Transform;
    [Tooltip("An empty object on the canvas marking the 'selected' position.")]
    public Transform SelectedButton_TargetPosition;
    public float buttonAnimSpeed = 10f;

    [Header("Typewriter")]
    public TypeWriterEffect typewriter;
    public TMP_Text pcDisplay;
    [TextArea(3, 10)]
    public string pcWelcomeMessage = "Welcome. System ready.\nSelect an option.";

    // --- Internal State ---
    private TowerController _selectedTower;
    private GameObject _selectedButtonInstance;
    private Vector3 _selectedButtonOriginalPos;
    private Transform _selectedButtonOriginalParent;
    
    private bool hasShownWelcomeMessage = false;
    private Coroutine _startupCoroutine;

    void Start()
    {
        // Start with ALL screens hidden.
        ShowScreen(null); 
    }
    
    /// <summary>
    /// This is the main entry point called by PlayerInteractionManager.
    /// </summary>
    public void StartPCInterface()
    {
        // Stop any previous startup routine, just in case
        if (_startupCoroutine != null)
        {
            StopCoroutine(_startupCoroutine);
        }
        _startupCoroutine = StartCoroutine(StartupRoutine());
    }

    /// <summary>
    /// This stops all UI processes when exiting the PC.
    /// </summary>
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
        // Hide everything
        ShowScreen(null);
        ClearSelectedTower(); // Make sure to clear selection on exit
    }

    /// <summary>
    /// This coroutine handles the "first time" welcome message logic.
    /// </summary>
    private IEnumerator StartupRoutine()
    {
        // Ensure the text display area is visible
        if (pcDisplay != null) pcDisplay.gameObject.SetActive(true);
        
        if (!hasShownWelcomeMessage)
        {
            // 1. First time: Hide all button containers
            ShowScreen(null); 
            
            // 2. Set the flag so this never runs again
            hasShownWelcomeMessage = true;
            
            // 3. Run the typewriter and WAIT for it to finish
            if (typewriter != null && pcDisplay != null)
            {
                // We use 'yield return' on the coroutine from the modified TypeWriterEffect
                yield return typewriter.DisplayText(pcDisplay, pcWelcomeMessage);
                
                // Optional: Wait an extra second after typing finishes
                yield return new WaitForSeconds(1f); 
            }
            
            // 4. Now that the message is done, show the main menu
            ShowMainMenu();
        }
        else
        {
            // Not the first time: Just show the main menu immediately
            ShowMainMenu();
        }
        
        _startupCoroutine = null;
    }

    // --- STATE 1: MAIN MENU ---
    public void ShowMainMenu()
    {
        // Hide the typewriter text display
        if (pcDisplay != null) pcDisplay.gameObject.SetActive(false);
        
        ShowScreen(MainMenu_Container);
        ClearSelectedTower();
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