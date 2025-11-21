using UnityEngine;
using StarterAssets;

public class TowerInteraction : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The Camera used to aim at towers.")]
    public Camera playerCamera;
    [Tooltip("The TowerMenu UI script on your Canvas.")]
    public TowerMenu towerMenu;
    [Tooltip("The Player Input script.")]
    public StarterAssetsInputs inputScript;
    [Tooltip("Weapon controller (to disable shooting while in menu).")]
    public WeaponControllerHS weaponController;

    [Header("Settings")]
    public float interactRange = 5.0f;
    public LayerMask towerLayer;

    private TowerController _hoveredTower;
    private float _scrollCooldown = 0f;

    void Update()
    {
        // --- FIX: Safety Check ---
        if (towerMenu == null)
        {
            // Only log once or handle gracefully to avoid console spam
            // Returning here prevents the crash, but the menu won't work until assigned
            return; 
        }
        // -------------------------

        // 1. Handle Menu Input if Open
        if (towerMenu.IsOpen)
        {
            HandleMenuInput();
            return;
        }

        // 2. Handle Raycast / Detection if Menu Closed
        DetectTower();

        // 3. Handle Opening Menu
        if (_hoveredTower != null && inputScript.interact)
        {
            inputScript.interact = false; // Consume input
            towerMenu.OpenMenu(_hoveredTower);
            weaponController.ToggleShootingEnabled(false); // Stop shooting
        }
    }

    void HandleMenuInput()
    {
        // Left Click to Select
        if (inputScript.fire)
        {
            inputScript.fire = false;
            towerMenu.ExecuteSelection();
            
            // If menu closed after execution, re-enable weapons
            if (!towerMenu.IsOpen)
            {
                weaponController.ToggleShootingEnabled(true);
            }
            return;
        }

        // Right Click / Escape (optional) to Close
        if (inputScript.aim) // Assuming 'aim' is Right Click
        {
            // inputScript.aim is usually hold, checking raw input might be better for 'back'
            // For now, let's just rely on the "Close" button in the menu or re-toggling Interact
        }
        
        // Toggle Close with Interact key
        if (inputScript.interact)
        {
            inputScript.interact = false;
            towerMenu.CloseMenu();
            weaponController.ToggleShootingEnabled(true);
            return;
        }

        // Scroll Wheel Navigation
        // We add a small cooldown/threshold so it doesn't scroll 50 times a frame
        if (_scrollCooldown > 0) _scrollCooldown -= Time.deltaTime;

        if (inputScript.menuScroll != 0 && _scrollCooldown <= 0)
        {
            int direction = (inputScript.menuScroll > 0) ? 1 : -1;
            towerMenu.Scroll(direction);
            _scrollCooldown = 0.15f; // Delay between scroll steps
            
            // Reset input so we don't scroll forever
            inputScript.menuScroll = 0; 
        }
    }

    void DetectTower()
    {
        // Raycast from center of screen
        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, interactRange, towerLayer))
        {
            // Check parent because collider might be on a child part of the tower
            TowerController tower = hit.collider.GetComponentInParent<TowerController>();
            
            if (tower != null)
            {
                if (_hoveredTower != tower)
                {
                    _hoveredTower = tower;
                    towerMenu.ShowPrompt(true);
                }
                return; // Found valid tower
            }
        }

        // If we missed or hit something else
        if (_hoveredTower != null)
        {
            _hoveredTower = null;
            towerMenu.ShowPrompt(false);
        }
    }
}