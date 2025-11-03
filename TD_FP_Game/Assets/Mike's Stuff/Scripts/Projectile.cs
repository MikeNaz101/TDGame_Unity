using UnityEngine;

public class Projectile : MonoBehaviour
{
    // Public variables to be set by the WeaponController when spawned
    public float damageAmount;
    public float projectileSpeed = 2f; 

    // How long the projectile exists before being destroyed (to clean up the scene)
    public float lifetime = 6f; 

    void Start()
    {
        // Give the projectile initial forward velocity
        // Time.deltaTime is not used here because we want instant, high velocity
        // Rigidbody.velocity is typically better for physics-based movement
        
        // --- Option 1: Using Rigidbody (Recommended for physical projectiles) ---
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = transform.forward * projectileSpeed;
        } 
        else // Fallback if no Rigidbody is present (using Transform movement)
        {
            Debug.LogWarning("Projectile has no Rigidbody. Falling back to Transform movement.");
        }

        // Set a timer to destroy the projectile after its lifetime
        Destroy(gameObject, lifetime);
    }

    void Update()
    {
        // If not using a Rigidbody, you would use this line for movement:
        // transform.Translate(Vector3.forward * projectileSpeed * Time.deltaTime);
    }

    // Called when the projectile collides with another object
    void OnTriggerEnter(Collider other)
    {
        // Try to get the EnemyController component from the collided object
        EnemyController enemy = other.GetComponent<EnemyController>();

        if (enemy != null)
        {
            // If it hits an enemy, deal damage using the TakeDamage method
            //enemy.TakeDamage(damageAmount);
        }

        // Destroy the projectile after it hits anything (or a specific target)
        Destroy(gameObject);
    }
}