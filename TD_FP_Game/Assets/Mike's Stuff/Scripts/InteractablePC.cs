using UnityEngine;
using UnityEngine.Events; // Required for UnityEvent
using StarterAssets; // Required for StarterAssetsInputs
//using Cinemachine;
using Unity.Cinemachine; // Required for virtual cameras

/// <summary>
/// This script goes on any interactable object (like a PC).
/// It detects the player, shows a prompt, and triggers an event on interaction.
/// </summary>
[RequireComponent(typeof(Collider))]
public class InteractablePC : MonoBehaviour
{
    [Header("Interaction")]
    [Tooltip("The UI element that says 'Press E to use PC'.")]
    public GameObject interactPromptUI;
    
    [Header("Camera")]
    [Tooltip("The Cinemachine Virtual Camera that is focused on this PC screen.")]
    public CinemachineCamera pcVCam;

    private bool canInteract = false;
    private StarterAssetsInputs playerInput;
    private PlayerInteractionManager playerInteractionManager;

    void Start()
    {
        // Make sure the prompt is hidden at start
        if (interactPromptUI != null)
        {
            interactPromptUI.SetActive(false);
        }
        // Make sure the collider is a trigger
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        // Check if the object entering is the player
        if (other.CompareTag("Player"))
        {
            // Get the player's input and interaction scripts
            playerInput = other.GetComponent<StarterAssetsInputs>();
            playerInteractionManager = other.GetComponent<PlayerInteractionManager>();

            if (playerInput != null && playerInteractionManager != null)
            {
                // Show prompt and allow interaction
                if (interactPromptUI != null) interactPromptUI.SetActive(true);
                canInteract = true;
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        // Player left the trigger
        if (other.CompareTag("Player"))
        {
            // Hide prompt and disable interaction
            if (interactPromptUI != null) interactPromptUI.SetActive(false);
            canInteract = false;
            playerInput = null;
            playerInteractionManager = null;
        }
    }

    void Update()
    {
        // While the player is in range and presses the interact button
        if (canInteract && playerInput != null && playerInput.interact)
        {
            // Consume the input
            playerInput.interact = false;
            
            // Tell the PlayerInteractionManager to take over
            if (playerInteractionManager != null)
            {
                playerInteractionManager.BeginPCInteraction(pcVCam);
            }

            // We are now interacting, so hide the prompt
            if (interactPromptUI != null) interactPromptUI.SetActive(false);
            canInteract = false;
        }
    }
}
