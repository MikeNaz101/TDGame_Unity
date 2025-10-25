using UnityEngine;

public class EnemyController : MonoBehaviour
{
    public float health = 50f;
    public GameObject scrapMetalPrefab; // Assign the Scrap Metal prefab in the Inspector
    public Transform targetCore; // Assign the Energy Core's transform in the Inspector

    // Function equivalent to Unreal's Event AnyDamage
    public void TakeDamage(float amount)
    {
        health -= amount;
        if (health <= 0f)
        {
            Die();
        }
    }

    void Die()
    {
        // 1. Spawn Scrap Metal
        Instantiate(scrapMetalPrefab, transform.position, Quaternion.identity);

        // 2. Destroy the Enemy (Self)
        Destroy(gameObject);
    }

    // Placeholder for AI Movement (Similar to AI MoveTo logic)
    void Update()
    {
        if (targetCore != null)
        {
            // Simple movement towards the target
            float step = 5f * Time.deltaTime; // 5f is the movement speed
            transform.position = Vector3.MoveTowards(transform.position, targetCore.position, step);
        }
    }
}
