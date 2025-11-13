using UnityEngine;
using StarterAssets;
using Unity.Cinemachine;
using TMPro; 

[RequireComponent(typeof(Collider))]
public class InteractablePC : MonoBehaviour
{
    [Header("Interaction")]
    [Tooltip("The PC_UI_Manager script, which is on your PC_Screen_Canvas object.")]
    public PC_UI_Manager pcUIManager; // <-- NEW
    
    [Tooltip("The message to type out.")]
    [TextArea(2, 5)]
    public string interactMessage = "Press E to use PC";
    
    [Header("Camera")]
    [Tooltip("The Cinemachine Virtual Camera that is focused on this PC screen.")]
    public CinemachineCamera pcVCam;

    private bool canInteract = false;
    private StarterAssetsInputs playerInput;
    private PlayerInteractionManager playerInteractionManager;
    // We no longer need the 'typewriter' or 'interactPromptText' here.

    void Start()
    {
        GetComponent<Collider>().isTrigger = true;
        // Start with the UI hidden
        if (pcUIManager != null)
        {
            pcUIManager.gameObject.SetActive(false);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInput = other.GetComponent<StarterAssetsInputs>();
            playerInteractionManager = other.GetComponent<PlayerInteractionManager>();

            if (playerInput != null && playerInteractionManager != null && pcUIManager != null)
            {
                // Turn on the canvas and show the prompt
                pcUIManager.gameObject.SetActive(true);
                pcUIManager.ShowInteractPrompt(true, interactMessage);
                canInteract = true;
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (pcUIManager != null)
            {
                // Turn off the whole canvas
                pcUIManager.ShowInteractPrompt(false, null); // Stop typewriter
                pcUIManager.gameObject.SetActive(false);
            }

            canInteract = false;
            playerInput = null;
            playerInteractionManager = null;
        }
    }

    void Update()
    {
        if (canInteract && playerInput != null && playerInput.interact)
        {
            playerInput.interact = false;
            if (playerInteractionManager != null)
            {
                // Tell the UI to hide the prompt *before* starting the main interface
                if (pcUIManager != null)
                {
                    pcUIManager.ShowInteractPrompt(false, null);
                }
                
                playerInteractionManager.BeginPCInteraction(pcVCam);
            }
            // We no longer set canInteract to false here, because we're *still* interacting
        }
    }
}