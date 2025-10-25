using UnityEngine;

public class ScrapMetalPickup : MonoBehaviour
{
    public int value = 10;

    private void OnTriggerEnter(Collider other)
    {
        PlayerStats player = other.GetComponent<PlayerStats>();

        if (player != null)
        {
            // Add resource to the player
            player.AddScrap(value);

            // Destroy the pickup
            Destroy(gameObject);
        }
    }
}
