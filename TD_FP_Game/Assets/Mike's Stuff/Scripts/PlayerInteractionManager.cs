using UnityEngine;
using UnityEngine.UI; // Required for Button and GraphicRaycaster
using UnityEngine.EventSystems; // Required for EventSystem and PointerEventData
using System.Collections.Generic; // Required for List
using StarterAssets; // Required for StarterAssetsInputs
using Unity.Cinemachine; // Required for virtual cameras
using TMPro; // Required for TextMeshPro text

/// <summary>
/// This script manages the player's state (e.g., "Playing" vs. "Interacting with PC").
/// It lives on the Player object and is responsible for enabling/disabling
/// all other player-control scripts.
/// </summary>
public class PlayerInteractionManager : MonoBehaviour
{
    [Header("Player Components")]
    public StarterAssetsInputs inputScript;
    public DoomMovement doomMovement;
    public WeaponControllerHS weaponController;

    [Header("Player UI")]
    public GameObject playerHudCanvas;

    [Header("PC Interaction")]
    public GameObject pcScreenCanvas;

    [Header("PC Custom Cursor")]
    public RectTransform customCursor;
    public RectTransform pcCanvasRect;
    public float cursorSpeed = 1f;
    
    [Header("PC Event System")]
    public GraphicRaycaster pcGraphicRaycaster;
    public EventSystem eventSystem;

    // --- NEW TYPEWRITER FIELDS ---
    [Header("PC Typewriter")]
    [Tooltip("The TypeWriterEffect script. (Can be on the PC Canvas or another manager object)")]
    public TypeWriterEffect pcTypewriter;
    [Tooltip("The Text (TMP) component on the PC screen where the text will be displayed.")]
    public TMP_Text pcDisplay;
    [Tooltip("The message to display when the player first uses the PC.")]
    [TextArea(3, 10)]
    public string pcWelcomeMessage = "Welcome, user. System booting...\nAll systems nominal.\nReady for input.";
    // --- END NEW FIELDS ---

    private bool isInteracting = false;
    private CinemachineCamera activePCVCam;
    private Vector2 cursorPosition;
    
    private PointerEventData pointerEventData;
    private List<RaycastResult> raycastResults;
    
    void Start()
    {
        // Auto-find components
        if (inputScript == null) inputScript = GetComponent<StarterAssetsInputs>();
        if (doomMovement == null) doomMovement = GetComponent<DoomMovement>();
        if (weaponController == null) weaponController = GetComponentInChildren<WeaponControllerHS>();
        if (eventSystem == null) eventSystem = FindObjectOfType<EventSystem>();
        
        // Auto-find raycaster from the canvas
        if (pcGraphicRaycaster == null && pcScreenCanvas != null)
        {
            pcGraphicRaycaster = pcScreenCanvas.GetComponent<GraphicRaycaster>();
        }

        // --- NEW ---
        // Auto-find typewriter script if not assigned
        if (pcTypewriter == null)
        {
            pcTypewriter = FindObjectOfType<TypeWriterEffect>();
        }
        // --- END NEW ---

        pointerEventData = new PointerEventData(eventSystem);
        raycastResults = new List<RaycastResult>();
        
        EndPCInteraction(); 
    }

    void Update()
    {
        if (isInteracting)
        {
            // --- 1. CURSOR MOVEMENT LOGIC ---
            if (customCursor != null)
            {
                Vector2 mouseDelta = inputScript.look * cursorSpeed;
                cursorPosition.x += mouseDelta.x;
                cursorPosition.y -= mouseDelta.y; 

                if (pcCanvasRect != null)
                {
                    Rect canvasRect = pcCanvasRect.rect;
                    cursorPosition.x = Mathf.Clamp(cursorPosition.x, canvasRect.xMin, canvasRect.xMax);
                    cursorPosition.y = Mathf.Clamp(cursorPosition.y, canvasRect.yMin, canvasRect.yMax);
                }
                customCursor.anchoredPosition = cursorPosition;
            }

            // --- 2. CLICK LOGIC (NEW) ---
            if (inputScript.fire)
            {
                inputScript.fire = false;
                
                if (pcGraphicRaycaster != null && eventSystem != null)
                {
                    pointerEventData.position = customCursor.position;
                    raycastResults.Clear();
                    pcGraphicRaycaster.Raycast(pointerEventData, raycastResults);

                    if (raycastResults.Count > 0)
                    {
                        GameObject hitObject = raycastResults[0].gameObject;
                        ExecuteEvents.Execute(hitObject, pointerEventData, ExecuteEvents.pointerClickHandler);
                        Debug.Log("Clicked on: " + hitObject.name);
                    }
                }
            }
            // --- END CLICK LOGIC ---

            // --- 3. EXIT LOGIC (FIXED) ---
            if (inputScript.interact)
            {
                inputScript.interact = false; 
                EndPCInteraction();
            }
        }
    }

    /// <summary>
    /// Called by an interactable object (like a PC) to start an interaction.
    /// </summary>
    public void BeginPCInteraction(CinemachineCamera targetPCVCam)
    {
        isInteracting = true;
        activePCVCam = targetPCVCam;

        // Disable player controls
        doomMovement.enabled = false;
        weaponController.ToggleShootingEnabled(false);
        inputScript.cursorInputForLook = true;
        inputScript.fire = false; 

        // Keep OS cursor locked, show custom cursor
        inputScript.SetCursorState(true);
        inputScript.cursorLocked = true;
        if (customCursor != null)
        {
            cursorPosition = Vector2.zero;
            customCursor.anchoredPosition = cursorPosition;
            customCursor.gameObject.SetActive(true);
        }
        
        // Swap UI
        if (playerHudCanvas != null) playerHudCanvas.SetActive(false);
        if (pcScreenCanvas != null) pcScreenCanvas.SetActive(true);

        // Switch cameras
        if (activePCVCam != null)
        {
            activePCVCam.Priority = 20; // Give this camera priority
        }

        // --- NEW ---
        // Start the typewriter effect
        if (pcTypewriter != null && pcDisplay != null)
        {
            pcTypewriter.DisplayText(pcDisplay, pcWelcomeMessage);
        }
        // --- END NEW ---
    }

    /// <summary>
    // Called to return control to the player.
    /// </summary>
    public void EndPCInteraction()
    {
        isInteracting = false;

        // --- NEW ---
        // Stop the typewriter effect
        if (pcTypewriter != null)
        {
            pcTypewriter.StopTyping();
        }
        // --- END NEW ---

        // Enable player controls
        doomMovement.enabled = true;
        weaponController.ToggleShootingEnabled(true);
        inputScript.cursorInputForLook = true;

        // Keep OS cursor locked, hide custom cursor
        inputScript.SetCursorState(true);
        inputScript.cursorLocked = true;
        if (customCursor != null)
        {
            customCursor.gameObject.SetActive(false);
        }

        // Swap UI
        if (playerHudCanvas != null) playerHudCanvas.SetActive(true);
        if (pcScreenCanvas != null) pcScreenCanvas.SetActive(false);

        // Give control back to player camera
        if (activePCVCam != null)
        {
            activePCVCam.Priority = 5; // Return to low priority
            activePCVCam = null;
        }
    }
}

