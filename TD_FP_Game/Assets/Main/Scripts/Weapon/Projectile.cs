using UnityEngine;

public class Projectile : MonoBehaviour
{
    // --- NEW ---
    // These are set by the TowerController that fires this projectile.
    [HideInInspector] public TowerData towerData;
    [HideInInspector] public Transform attacker;

    [Header("Tuning")]
    public float projectileSpeed = 20f;
    public float lifetime = 4f;

    // --- PRIVATE ---
    private bool _hasImpacted = false;

    void Start()
    {
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = transform.forward * projectileSpeed;
        }
        else
        {
            Debug.LogWarning("Projectile has no Rigidbody. Falling back to Transform movement.");
        }
        Destroy(gameObject, lifetime);
    }

    void Update()
    {
        // Fallback movement if no Rigidbody is present
        if (GetComponent<Rigidbody>() == null)
        {
            transform.Translate(Vector3.forward * projectileSpeed * Time.deltaTime);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        // Prevent the projectile from triggering multiple times
        if (_hasImpacted) return;

        // Check if we hit an enemy or the environment
        bool isEnemy = other.CompareTag("Enemy");
        bool isEnvironment = other.gameObject.layer == LayerMask.NameToLayer("Default");

        if (isEnemy || isEnvironment)
        {
            _hasImpacted = true; // Lock this projectile

            if (towerData == null)
            {
                Debug.LogError("Projectile impacted but has no TowerData!");
                Destroy(gameObject);
                return;
            }

            // --- Handle Explosion or Direct Hit ---
            if (towerData.isExplosive)
            {
                HandleExplosion(transform.position);
            }
            else if (isEnemy)
            {
                // Direct hit logic (non-explosive)
                HandleDirectHit(other.GetComponent<EnemyController>());
            }

            // Spawn impact particle (works for both explosion and direct hit)
            if (towerData.impactParticlePrefab != null)
            {
                Instantiate(towerData.impactParticlePrefab, transform.position, Quaternion.identity);
            }

            // Destroy the projectile
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Applies damage to a single enemy.
    /// </summary>
    void HandleDirectHit(EnemyController enemy)
    {
        if (enemy != null)
        {
            enemy.TakeDamage(towerData.damage, attacker);
        }
    }

    /// <summary>
    /// Finds all enemies in a radius and applies explosive damage/force.
    /// </summary>
    void HandleExplosion(Vector3 explosionCenter)
    {
        Collider[] hits = Physics.OverlapSphere(explosionCenter, towerData.explosionRadius);

        foreach (Collider hit in hits)
        {
            if (hit.CompareTag("Enemy"))
            {
                EnemyController enemy = hit.GetComponent<EnemyController>();
                if (enemy != null)
                {
                    // We now pass the 'attacker' transform
                    enemy.TakeExplosion(
                        towerData.damage,
                        attacker, // <-- This is the added parameter
                        explosionCenter,
                        towerData.explosionForce,
                        towerData.explosionRadius
                    );
                }
            }
        }
    }
}