using UnityEngine;
using System.Collections.Generic; // Required for using Lists

public class ScrapMetalPickup : MonoBehaviour
{
    public int value = 10;

    // A static (global) list that all scrap objects will add themselves to.
    public static List<Transform> AvailableScrap = new List<Transform>();

    void Start()
    {
        // Add this piece of scrap to the global list
        if (!AvailableScrap.Contains(this.transform))
        {
            AvailableScrap.Add(this.transform);
        }
    }

    void OnDestroy()
    {
        // Remove this piece of scrap from the global list
        AvailableScrap.Remove(this.transform);
    }

    private void OnTriggerEnter(Collider other)
    {
        // --- Original Player Collection Logic ---
        PlayerStats player = other.GetComponent<PlayerStats>();
        if (player != null)
        {
            // Add resource to the player
            player.AddScrap(value);

            // Destroy the pickup (this will automatically call OnDestroy)
            Destroy(gameObject);
            return; // Exit after player collects
        }

        // --- New Bot Collection Logic ---
        // This is a fallback in case the bot's main check fails.
        // The bot will primarily use a distance check, not a trigger.
        ScrapCollectorBot bot = other.GetComponent<ScrapCollectorBot>();
        if (bot != null)
        {
            // Tell the bot it just collected this item
            // The bot's script will handle adding the scrap to the player
            bot.NotifyCollection(this);
        }
    }
}