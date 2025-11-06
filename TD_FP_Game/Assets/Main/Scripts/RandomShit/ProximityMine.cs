using UnityEngine;

public class ProximityMine : MonoBehaviour
{
    [Header("Explosion Settings")]
    [SerializeField] private float _explosionRadius = 5f; // How big the boom area is
    [SerializeField] private float _explosionForce = 700f; // How much push! Werk it!
    [SerializeField] private float _upwardModifier = 0.2f; // A little vertical lift for drama!
    [SerializeField] private LayerMask _enemyLayer; // ✨ VERY IMPORTANT: Set this in Inspector! ✨
    
    [SerializeField] private LayerMask _stickableSurfaces; // Surfaces it can stick to
    [SerializeField] private Transform _raycastOrigin; // Empty GameObject at the center
    [SerializeField] private float _raycastDistance = 5f; // Raycast range
    [SerializeField] private GameObject _explosionEffect; // Particle system prefab
    [SerializeField] private AudioClip _explosionSound; // Explosion sound
    [SerializeField] private Transform _holdPosition; // Reference to the hold position from Grab script
    private Rigidbody _rb;
    private bool _isArmed = false; // Only starts raycasting when armed
    private bool set = false;

    private void Start()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.isKinematic = false; // Enable physics so it behaves normally when dropped/thrown
    }

    private void Update()
    {
        Debug.Log("set = "+ set);
        if (_isArmed)
        {
            CheckForEnemy();
        }
        if(transform.parent == _holdPosition) // Check if grabbed
        {
            set = true;
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        //if (_isArmed || transform.parent == _holdPosition) return; // Don't activate if grabbed

        if (_isArmed || set)
        {
            // Check if the object we hit is in the "Obstacles" layer
            if (collision.gameObject.layer == LayerMask.NameToLayer("Obstacles"))
            {
                StickToSurface(collision);
            }
            else
            {
                Debug.Log("Mine hit a non-stickable surface: " + collision.gameObject.name);
            }
        }
    }

    private void StickToSurface(Collision collision)
    {
        _rb.isKinematic = true; // Disable physics to prevent movement
        transform.position = collision.contacts[0].point; // Stick to impact point

        // Align the mine’s UP direction to match the surface normal
        transform.rotation = Quaternion.FromToRotation(transform.up, collision.contacts[0].normal) * transform.rotation;

        _isArmed = true;
        Debug.Log("Mine Activated!");
    }



    private void CheckForEnemy()
    {
        RaycastHit hit;
        if (Physics.Raycast(_raycastOrigin.position, _raycastOrigin.forward, out hit, _raycastDistance))
        {
            if (hit.collider.CompareTag("Enemy"))
            {
                //Destroy(hit.collider.gameObject);
                Explode();
            }
        }

        // Debug visualization
        Debug.DrawRay(_raycastOrigin.position, _raycastOrigin.forward * _raycastDistance, Color.red);
    }
    
    private void Explode()
    {
        Debug.Log("Mine Exploding NOW!", this);

        // --- Find all potential victims in the area ---
        Collider[] collidersInRange = Physics.OverlapSphere(transform.position, _explosionRadius);

        // --- Tell each enemy caught in the blast to ragdoll and fly! ---
        foreach (Collider hitCollider in collidersInRange)
        {
            // Try to find an EnemyController on the object hit, or its parent
            EnemyController enemy = hitCollider.GetComponentInParent<EnemyController>();

            if (enemy != null)
            {
                //enemy.TakeExplosion(transform.position, _explosionForce, _explosionRadius, _upwardModifier);
            }
            else
            {
                Rigidbody otherRb = hitCollider.GetComponent<Rigidbody>();
                if (otherRb != null && !otherRb.isKinematic) // Check if it's a dynamic rigidbody
                {
                    otherRb.AddExplosionForce(_explosionForce * 0.5f, transform.position, _explosionRadius, _upwardModifier, ForceMode.Impulse); // Maybe less force
                }
            }
        }

        // --- Play explosion effects (sound and particles) ---
        if (_explosionEffect)
        {
            Instantiate(_explosionEffect, transform.position, Quaternion.identity);
        }
        if (_explosionSound)
        {
            AudioSource.PlayClipAtPoint(_explosionSound, transform.position);
        }

        // --- Destroy the mine GameObject ---
        Destroy(gameObject);
    }

    private void OnDrawGizmos()
    {
        if (_isArmed)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawRay(_raycastOrigin.position, _raycastOrigin.forward * _raycastDistance);
        }
    }
}