using UnityEngine;

public class Projectile : MonoBehaviour
{
    // --- DATA ---
    [HideInInspector] public TowerData towerData;
    [HideInInspector] public Transform attacker;
    [HideInInspector] public float damageMultiplier = 1f;

    [Header("Movement & Fuse")]
    public float projectileSpeed = 20f;
    [Tooltip("Time in seconds before the bomb automatically explodes.")]
    public float fuseTime = 3.0f; 
    
    [Header("Collision Logic")]
    [Tooltip("Should it explode immediately when touching an enemy?")]
    public bool explodeOnEnemyContact = true;
    [Tooltip("Should it explode immediately when touching the ground/walls? (Uncheck for bouncing bombs)")]
    public bool explodeOnEnvironmentContact = true;

    // --- STATE ---
    private bool _hasImpacted = false;
    private float _timer;
    private Rigidbody _rb;

    void Start()
    {
        _timer = fuseTime;
        _rb = GetComponent<Rigidbody>();

        if (_rb != null)
        {
            _rb.linearVelocity = transform.forward * projectileSpeed;
        }
        else
        {
            Debug.LogWarning("Projectile has no Rigidbody. Falling back to Transform movement.");
        }
        
        Destroy(gameObject, fuseTime + 5f);
    }

    void Update()
    {
        if (_rb == null)
        {
            transform.Translate(Vector3.forward * projectileSpeed * Time.deltaTime);
        }

        // Fuse Timer Logic
        if (towerData != null && towerData.isExplosive && !_hasImpacted)
        {
            _timer -= Time.deltaTime;
            if (_timer <= 0f)
            {
                Explode(transform.position);
            }
        }
    }

    // --- CASE 1: Projectile is a TRIGGER (Ghost) ---
    void OnTriggerEnter(Collider other)
    {
        HandleImpact(other.gameObject, other.transform.position);
    }

    // --- CASE 2: Projectile is SOLID (Physics Object) ---
    void OnCollisionEnter(Collision collision)
    {
        HandleImpact(collision.gameObject, collision.contacts[0].point);
    }

    // --- SHARED LOGIC ---
    void HandleImpact(GameObject otherObj, Vector3 hitPoint)
    {
        if (_hasImpacted) return;

        bool isEnemy = otherObj.CompareTag("Enemy");
        
        // Check layers safely
        bool isEnvironment = otherObj.layer == LayerMask.NameToLayer("Default"); // Adjust if your walls are on a different layer

        if (towerData == null)
        {
            Destroy(gameObject);
            return;
        }

        // --- EXPLOSIVE LOGIC ---
        if (towerData.isExplosive)
        {
            if (isEnemy && explodeOnEnemyContact)
            {
                Explode(transform.position); // Use bomb position for center of blast
            }
            else if (isEnvironment && explodeOnEnvironmentContact)
            {
                Explode(hitPoint);
            }
            // If explodeOnEnvironmentContact is FALSE, it will just bounce!
        }
        // --- BULLET LOGIC ---
        else
        {
            if (isEnemy)
            {
                _hasImpacted = true;
                HandleDirectHit(otherObj.GetComponent<EnemyController>());
                Destroy(gameObject);
            }
            else if (isEnvironment)
            {
                Destroy(gameObject);
            }
        }
    }

    void Explode(Vector3 position)
    {
        _hasImpacted = true;
        HandleExplosion(position);

        if (towerData.impactParticlePrefab != null)
        {
            Instantiate(towerData.impactParticlePrefab, position, Quaternion.identity);
        }

        Destroy(gameObject);
    }

    void HandleDirectHit(EnemyController enemy)
    {
        if (enemy != null)
        {
            enemy.TakeDamage(towerData.damage * damageMultiplier, attacker);
        }
    }

    void HandleExplosion(Vector3 explosionCenter)
    {
        Collider[] hits = Physics.OverlapSphere(explosionCenter, towerData.explosionRadius);

        foreach (Collider hit in hits)
        {
            // Check for tag to avoid friendly fire or hitting yourself
            if (hit.CompareTag("Enemy"))
            {
                EnemyController enemy = hit.GetComponent<EnemyController>();
                if (enemy != null)
                {
                    enemy.TakeExplosion(
                        towerData.damage * damageMultiplier,
                        attacker,
                        explosionCenter,
                        towerData.explosionForce,
                        towerData.explosionRadius
                    );
                }
            }
        }
    }
}