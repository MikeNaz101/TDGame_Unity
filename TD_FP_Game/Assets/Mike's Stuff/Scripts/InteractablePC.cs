using UnityEngine;
using StarterAssets;
using Unity.Cinemachine;
using TMPro; 

[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(TypeWriterEffect))]
public class InteractablePC : MonoBehaviour
{
    [Header("Interaction")]
    [Tooltip("The Text (TMP) UI element that says 'Press E to use PC'.")]
    public TMP_Text interactPromptText; 
    
    [Tooltip("The message to type out.")]
    [TextArea(2, 5)]
    public string interactMessage = "Press E to use PC";
    
    [Header("Camera")]
    [Tooltip("The Cinemachine Virtual Camera that is focused on this PC screen.")]
    public CinemachineCamera pcVCam;

    private bool canInteract = false;
    private StarterAssetsInputs playerInput;
    private PlayerInteractionManager playerInteractionManager;
    private TypeWriterEffect typewriter;

    void Start()
    {
        typewriter = GetComponent<TypeWriterEffect>();
        if (interactPromptText != null)
        {
            interactPromptText.gameObject.SetActive(false);
        }
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInput = other.GetComponent<StarterAssetsInputs>();
            playerInteractionManager = other.GetComponent<PlayerInteractionManager>();

            if (playerInput != null && playerInteractionManager != null)
            {
                if (interactPromptText != null)
                {
                    interactPromptText.gameObject.SetActive(true);
                    typewriter.DisplayText(interactPromptText, interactMessage);
                }
                canInteract = true;
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            typewriter.StopTyping();
            if (interactPromptText != null) interactPromptText.gameObject.SetActive(false);

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
                playerInteractionManager.BeginPCInteraction(pcVCam);
            }
            typewriter.StopTyping();
            if (interactPromptText != null) interactPromptText.gameObject.SetActive(false);
            canInteract = false;
        }
    }
}

