using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using StarterAssets;
using Unity.Cinemachine;
using TMPro;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

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
    
    [Tooltip("The PC_UI_Manager script on the pcScreenCanvas.")]
    public PC_UI_Manager pcUIManager;

    [Header("PC Custom Cursor")]
    public RectTransform customCursor;
    public RectTransform pcCanvasRect;
    public float cursorSpeed = 1f;
    [Tooltip("Flip vertical cursor movement if it feels upside down.")]
    public bool invertCursorY = false;
    [Tooltip("How far one scroll-wheel notch scrolls lists on the PC.")]
    public float scrollSensitivity = 1f;
    
    [Header("PC Event System")]
    public GraphicRaycaster pcGraphicRaycaster;
    public EventSystem eventSystem;

    private bool isInteracting = false;
    private CinemachineCamera activePCVCam;
    private Vector2 cursorPosition;
    
    private PointerEventData pointerEventData;
    private List<RaycastResult> raycastResults;
    private GameObject hoveredObject;
    private Selectable hoveredSelectable;

    private PC_DesktopShell Shell => pcUIManager != null ? pcUIManager.Shell : null;

    /// <summary>True while the player is locked into the PC screen.</summary>
    public bool IsInteracting => isInteracting;
    
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
        
        // Find the UI Manager
        if (pcUIManager == null && pcScreenCanvas != null)
        {
            pcUIManager = pcScreenCanvas.GetComponent<PC_UI_Manager>();
        }

        pointerEventData = new PointerEventData(eventSystem);
        raycastResults = new List<RaycastResult>();
        //EndPCInteraction(); 
    }

    void Update()
    {
        if (!isInteracting) return;

        var shell = Shell;
        MoveCursor(shell);

        GameObject hit = RaycastUnderCursor();
        UpdateHover(hit, shell);

        if (inputScript.fire)
        {
            inputScript.fire = false;

            if (pcUIManager != null && pcUIManager.IsStartingUp)
            {
                // Clicking during boot / welcome skips ahead, like tapping a key on a real PC.
                pcUIManager.SkipStartup();
            }
            else if (hit != null)
            {
                Click(hit, shell);
            }
        }

        if (Mathf.Abs(inputScript.menuScroll) > 0.01f && hit != null)
        {
            pointerEventData.scrollDelta = new Vector2(0f, inputScript.menuScroll * scrollSensitivity);
            GameObject scrollHandler = ExecuteEvents.GetEventHandler<IScrollHandler>(hit);
            if (scrollHandler != null) ExecuteEvents.Execute(scrollHandler, pointerEventData, ExecuteEvents.scrollHandler);
            inputScript.menuScroll = 0f;
        }

        bool escape = false;
#if ENABLE_INPUT_SYSTEM
        escape = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#endif
        if (inputScript.interact || inputScript.build || escape) 
        {
            inputScript.interact = false;
            inputScript.build = false; 
            EndPCInteraction();
        }
    }

    private void MoveCursor(PC_DesktopShell shell)
    {
        if (customCursor == null) return;
        Vector2 mouseDelta = inputScript.look * cursorSpeed;

        if (shell != null && customCursor.parent == shell.CursorLayer)
        {
            // Desktop space: x is right and y is up on the monitor, whatever the canvas rotation.
            cursorPosition.x += mouseDelta.x;
            cursorPosition.y += invertCursorY ? mouseDelta.y : -mouseDelta.y;
            cursorPosition = shell.ClampToDesktop(cursorPosition);
        }
        else
        {
            cursorPosition.x -= mouseDelta.y;
            cursorPosition.y -= mouseDelta.x; 

            if (pcCanvasRect != null)
            {
                Rect canvasRect = pcCanvasRect.rect;
                cursorPosition.x = Mathf.Clamp(cursorPosition.x, canvasRect.xMin, canvasRect.xMax);
                cursorPosition.y = Mathf.Clamp(cursorPosition.y, canvasRect.yMin, canvasRect.yMax);
            }
        }
        customCursor.anchoredPosition = cursorPosition;
    }

    private GameObject RaycastUnderCursor()
    {
        if (pcGraphicRaycaster == null || eventSystem == null || customCursor == null) return null;

        Camera cam = weaponController != null && weaponController.mainCamera != null ? weaponController.mainCamera : Camera.main;
        if (cam == null) return null;

        // Convert the 3D world position of our cursor's tip into a 2D screen position for the raycast.
        pointerEventData.position = cam.WorldToScreenPoint(customCursor.position);
        raycastResults.Clear();
        pcGraphicRaycaster.Raycast(pointerEventData, raycastResults);
        return raycastResults.Count > 0 ? raycastResults[0].gameObject : null;
    }

    private void UpdateHover(GameObject hit, PC_DesktopShell shell)
    {
        GameObject newHover = hit != null ? ExecuteEvents.GetEventHandler<IPointerEnterHandler>(hit) : null;
        if (newHover == hoveredObject) return;

        // Send real enter/exit events so buttons show their highlight colours.
        if (hoveredObject != null) ExecuteEvents.Execute(hoveredObject, pointerEventData, ExecuteEvents.pointerExitHandler);
        hoveredObject = newHover;
        pointerEventData.pointerEnter = newHover;
        if (hoveredObject != null) ExecuteEvents.Execute(hoveredObject, pointerEventData, ExecuteEvents.pointerEnterHandler);

        hoveredSelectable = hoveredObject != null ? hoveredObject.GetComponentInParent<Selectable>() : null;
        if (shell != null) shell.OnHoverChanged(hoveredSelectable);
    }

    private void Click(GameObject hit, PC_DesktopShell shell)
    {
        // The topmost hit is often a button's label, so bubble up to whatever handles the click.
        GameObject clickHandler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hit);
        if (clickHandler == null) return;

        var selectable = clickHandler.GetComponent<Selectable>();
        if (selectable != null && !selectable.IsInteractable()) return;

        pointerEventData.pointerPress = clickHandler;
        pointerEventData.rawPointerPress = hit;
        pointerEventData.pointerPressRaycast = raycastResults[0];
        pointerEventData.eligibleForClick = true;
        pointerEventData.clickCount = 1;
        pointerEventData.clickTime = Time.unscaledTime;

        ExecuteEvents.Execute(clickHandler, pointerEventData, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.Execute(clickHandler, pointerEventData, ExecuteEvents.pointerUpHandler);
        if (shell != null) shell.OnClicked(clickHandler);
        ExecuteEvents.Execute(clickHandler, pointerEventData, ExecuteEvents.pointerClickHandler);

        pointerEventData.pointerPress = null;
        pointerEventData.eligibleForClick = false;
        // Don't leave the button stuck in its "selected" colour after the click.
        if (eventSystem != null && eventSystem.currentSelectedGameObject == clickHandler) eventSystem.SetSelectedGameObject(null);
    }

    private void ClearHover()
    {
        if (hoveredObject != null && pointerEventData != null)
        {
            ExecuteEvents.Execute(hoveredObject, pointerEventData, ExecuteEvents.pointerExitHandler);
        }
        hoveredObject = null;
        hoveredSelectable = null;
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
            var shell = Shell;
            if (shell != null) shell.AdoptCursor(customCursor);
            cursorPosition = Vector2.zero;
            customCursor.anchoredPosition = cursorPosition;
            customCursor.gameObject.SetActive(true);
        }
        ClearHover();
        
        // Swap UI
        if (playerHudCanvas != null) playerHudCanvas.SetActive(false);
        //if (pcScreenCanvas != null) pcScreenCanvas.SetActive(true);

        // Switch cameras
        if (activePCVCam != null)
        {
            activePCVCam.Priority = 20; // Give this camera priority
        }
        
        // Tell the PC UI Manager to start
        if (pcUIManager != null)
        {
            pcUIManager.StartPCInterface();
        }
    }

    /// <summary>
    // Called to return control to the player.
    /// </summary>
    public void EndPCInteraction()
    {
        isInteracting = false;
        ClearHover();
        
        // Tell the PC to stop all coroutines and typewriter effects
        if (pcUIManager != null)
        {
            pcUIManager.StopInterface();
        }

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
        //if (pcScreenCanvas != null) pcScreenCanvas.SetActive(false);

        // Give control back to player camera
        if (activePCVCam != null)
        {
            activePCVCam.Priority = 5; // Return to low priority
            activePCVCam = null;
        }
        
        if (pcUIManager != null)
        {
            // This is a bit of a hack, but it's the cleanest way.
            // We assume the pcUIManager's parent is the InteractablePC, or nearby.
            // A better way is to find the InteractablePC that is currently active.
            
            // Let's just find the script. It's simple.
            InteractablePC activePC = FindObjectOfType<InteractablePC>(); 
            if(activePC != null)
            {
                pcUIManager.ShowInteractPrompt(true, activePC.interactMessage);
            }
        }
    }
}