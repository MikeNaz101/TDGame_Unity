using UnityEngine;
using TMPro;
using StarterAssets; // Needed to read input

public class TutorialManager : MonoBehaviour
{
    [Header("References")]
    public GameObject tutorialPanel; // The UI Panel background
    public TextMeshProUGUI tutorialText;
    public StarterAssetsInputs inputScript; // To check if player pressed the button

    private enum Step { Move, Shoot, Build, Done }
    private Step _currentStep = Step.Move;

    void Start()
    {
        // Start the first step
        ShowStep("Use [W,A,S,D] to Move");
    }

    void Update()
    {
        if (_currentStep == Step.Done) return;

        switch (_currentStep)
        {
            case Step.Move:
                // Check if player moved significantly
                if (inputScript.move != Vector2.zero)
                {
                    AdvanceStep(Step.Shoot, "Press [Left Click] to Fire");
                }
                break;

            case Step.Shoot:
                if (inputScript.fire)
                {
                    AdvanceStep(Step.Build, "Press [Q] to enter Build Mode");
                }
                break;

            case Step.Build:
                if (inputScript.build)
                {
                    // Tutorial Complete!
                    _currentStep = Step.Done;
                    tutorialPanel.SetActive(false);
                }
                break;
        }
    }

    void AdvanceStep(Step nextStep, string message)
    {
        _currentStep = nextStep;
        tutorialText.text = message;
        
        // Optional: Play a "ding" sound here
    }
    
    void ShowStep(string message)
    {
        tutorialPanel.SetActive(true);
        tutorialText.text = message;
    }
}