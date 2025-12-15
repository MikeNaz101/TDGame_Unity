using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HUDManager : MonoBehaviour
{
    [Header("References")]
    public PlayerStats playerStats; 
    public WeaponControllerHS weaponController; // --- NEW: Drag WeaponController here

    [Header("UI Components")]
    public TextMeshProUGUI scrapText; 
    public Slider healthSlider; 
    public TextMeshProUGUI ammoText; // --- NEW: Drag your Ammo Text UI here

    void Start()
    {
        // Auto-find references if missing
        if (playerStats == null) playerStats = FindObjectOfType<PlayerStats>();
        if (weaponController == null) weaponController = FindObjectOfType<WeaponControllerHS>();
    }

    void Update()
    {
        // 1. Update Player Stats (Health/Scrap)
        if (playerStats != null)
        {
            if (scrapText) scrapText.text = "Scrap: " + playerStats.scrapMetal.ToString();
            
            if (healthSlider)
            {
                healthSlider.maxValue = playerStats.maxHealth;
                healthSlider.value = playerStats.currentHealth;
            }
        }

        // 2. Update Ammo Display
        if (weaponController != null && ammoText != null)
        {
            UpdateAmmoDisplay();
        }
    }

    void UpdateAmmoDisplay()
    {
        // Retrieve values from WeaponController
        // We need to access public getters or fields. 
        // NOTE: You might need to make some fields public in WeaponControllerHS if they aren't already.
        
        int currentClip = weaponController.CurrentClip;
        int maxClip = weaponController.MaxClip;
        int currentReserve = weaponController.CurrentReserve;
        int maxReserve = weaponController.MaxReserve;

        // Format: "1/2 // 6/10"
        ammoText.text = $"{currentClip}/{maxClip} // {currentReserve}/{maxReserve}";
    }
}