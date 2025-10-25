using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HUDManager : MonoBehaviour
{
    public PlayerStats playerStats; // Drag the Player Character here in the Inspector
    public TextMeshProUGUI scrapText; // Drag the Scrap Text UI component here
    public Slider healthSlider; // Drag the Health Slider UI component here

    void Update()
    {
        if (playerStats != null)
        {
            // Update Scrap Metal Text
            scrapText.text = "Scrap: " + playerStats.scrapMetal.ToString();

            // Update Health Bar Slider
            healthSlider.maxValue = playerStats.maxHealth;
            healthSlider.value = playerStats.currentHealth;
        }
    }
}
