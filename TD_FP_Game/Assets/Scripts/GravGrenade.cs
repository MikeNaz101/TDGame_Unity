using UnityEngine;
using System.Collections;
using System.Collections.Generic; // Required for List

// This script defines the behavior of a gravity grenade that pulls objects up,
// pulls them towards the center, and then scatters them.
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class GravGrenade : MonoBehaviour
{
    [Header("Explosion Settings")]
    [SerializeField] private float _delayBeforeExplosion = 1.0f; // Time after impact before explosion
    [SerializeField] private float _explosionRadius = 7.0f;     // Radius of the effect sphere
    [SerializeField] private LayerMask _affectedLayers;         // Which layers the explosion affects
    [SerializeField] private GameObject _explosionParticlePrefab; // Assign your particle effect prefab
    [SerializeField] private AudioClip _explosionSound;         // Assign your sound effect
    [SerializeField] private float _cleanupDelay = 5.0f;        // Time before destroying grenade & particles after explosion starts

    [Header("Phase 1: Lift")]
    [SerializeField] private float _upwardForceDuration = 1.0f;
    [SerializeField] private float _upwardForceMagnitude = 8.0f;  // Adjust strength as needed

    [Header("Phase 2: Pull")]
    [SerializeField] private float _pullForceDuration = 1.0f;
    [SerializeField] private float _pullForceMagnitude = 15.0f; // Adjust strength as needed

    [Header("Phase 3: Scatter")]
    [SerializeField] private float _scatterHeight = 10.0f; // How high above center
    [SerializeField] private float _scatterRangeXZ = 10.0f; // Width/Depth of scatter area (centered)

    private Rigidbody _rb;
    private Collider _collider;
    private AudioSource _audioSource;
    private bool _hasCollided = false;
    private bool _isExploding = false;
    private float _collisionTime = -1f;
    private Vector3 _impactPoint; // Still stores the *first* impact point for potential reference or gizmos

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _collider = GetComponent<Collider>();

        // Attempt to get or add an AudioSource
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
            _audioSource.spatialBlend = 1.0f; // Make sound 3D
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        // Only trigger on the first collision and if not already exploding
        if (!_hasCollided && !_isExploding)
        {
            Debug.Log(gameObject.name + " impacted with " + collision.gameObject.name);
            _hasCollided = true;
            _collisionTime = Time.time;
            _impactPoint = collision.contacts[0].point; // Store the first contact point

            // Optional: Add logic here to make the grenade stick or stop bouncing
            // e.g., _rb.isKinematic = true;
        }
    }

    void Update()
    {
        // Check if enough time has passed since the first collision to start exploding
        if (_hasCollided && !_isExploding && Time.time >= _collisionTime + _delayBeforeExplosion)
        {
            StartExplosionSequence();
        }
    }

    void StartExplosionSequence()
    {
        if (_isExploding) return; // Prevent multiple triggers

        _isExploding = true;
        Debug.Log(gameObject.name + " exploding!");

        // --- Set Explosion Center ---
        // Use the grenade's current position at the moment of explosion
        Vector3 explosionCenter = transform.position;
        // (Previously used _impactPoint, which is the *first* contact point)

        // --- Visual/Audio Effects ---
        GameObject particleInstance = null; // To store reference to the spawned particle system
        if (_explosionParticlePrefab != null)
        {
            // Instantiate the particle effect at the explosion center and store its reference
            particleInstance = Instantiate(_explosionParticlePrefab, explosionCenter, Quaternion.identity);
        }

        if (_explosionSound != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(_explosionSound);
        }

        // --- Disable Grenade ---
        // Hide mesh, disable further collisions/physics for the grenade itself
        var renderer = GetComponent<Renderer>();
        if (renderer != null) renderer.enabled = false;
        if (_collider != null) _collider.enabled = false;
        if (_rb != null) _rb.isKinematic = true; // Stop it from moving

        // --- Find Affected Objects ---
        // Use the calculated explosionCenter for the overlap sphere
        Collider[] affectedColliders = Physics.OverlapSphere(explosionCenter, _explosionRadius, _affectedLayers);
        List<Rigidbody> affectedRigidbodies = new List<Rigidbody>();

        foreach (Collider hitCollider in affectedColliders)
        {
            // Ensure we don't affect the grenade itself if its layer is included
            if (hitCollider == _collider) continue;

            Rigidbody hitRb = hitCollider.attachedRigidbody;

            // We only want dynamic rigidbodies that haven't already been added
            if (hitRb != null && !hitRb.isKinematic && !affectedRigidbodies.Contains(hitRb))
            {
                affectedRigidbodies.Add(hitRb);
            }
        }

        // --- Apply Forces/Teleport via Coroutine ---
        foreach (Rigidbody rbToAffect in affectedRigidbodies)
        {
            // Pass the calculated explosionCenter to the coroutine
            StartCoroutine(ApplyPhasedForces(rbToAffect, explosionCenter));
        }

        // --- Schedule Cleanup ---
        // Destroy the grenade object after the delay
        Destroy(gameObject, _cleanupDelay);

        // Destroy the particle effect instance after the same delay, if it exists
        if (particleInstance != null)
        {
            Destroy(particleInstance, _cleanupDelay);
        }
    }

    IEnumerator ApplyPhasedForces(Rigidbody targetRb, Vector3 center)
    {
        // --- Phase 1: Upward Lift ---
        float timer = 0f;
        while (timer < _upwardForceDuration)
        {
            if (targetRb == null) yield break; // Target might have been destroyed
            targetRb.AddForce(Vector3.up * _upwardForceMagnitude, ForceMode.Acceleration);
            timer += Time.deltaTime;
            yield return null; // Wait for the next frame
        }

        // --- Phase 2: Pull Towards Center ---
        timer = 0f; // Reset timer
        while (timer < _pullForceDuration)
        {
            if (targetRb == null) yield break;
            Vector3 directionToCenter = (center - targetRb.position).normalized; // Uses the passed 'center'
            targetRb.AddForce(directionToCenter * _pullForceMagnitude, ForceMode.Acceleration);
            timer += Time.deltaTime;
            yield return null; // Wait for the next frame
        }

        // --- Phase 3: Scatter Teleport ---
        if (targetRb != null) // Check one last time
        {
            float randomX = Random.Range(-_scatterRangeXZ / 2f, _scatterRangeXZ / 2f);
            float randomZ = Random.Range(-_scatterRangeXZ / 2f, _scatterRangeXZ / 2f);
            Vector3 scatterOffset = new Vector3(randomX, _scatterHeight, randomZ);
            Vector3 targetPosition = center + scatterOffset; // Uses the passed 'center'

            // Instantly move the object
            targetRb.position = targetPosition;

            // Optional: Stop its momentum after teleporting
            targetRb.linearVelocity = Vector3.zero;
            targetRb.angularVelocity = Vector3.zero;

            Debug.Log($"Scattered {targetRb.name} to {targetPosition}");
        }
    }

    // Draw gizmo in editor for visualization
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        // Gizmo still uses _impactPoint if available after collision, otherwise transform.position
        // This shows the initial detection radius.
        Vector3 gizmoCenter = Application.isPlaying && _hasCollided ? _impactPoint : transform.position;
        Gizmos.DrawWireSphere(gizmoCenter, _explosionRadius);

        // Gizmo for scatter area - uses transform.position if exploding, otherwise impact point/transform position
        if (Application.isPlaying && _isExploding)
        {
             // For the scatter gizmo, using the actual explosion center (transform.position when exploding) makes sense
             Vector3 currentExplosionCenter = transform.position;
             Gizmos.color = Color.yellow;
             Vector3 scatterBase = currentExplosionCenter + Vector3.up * _scatterHeight;
             Vector3 scatterSize = new Vector3(_scatterRangeXZ, 0.1f, _scatterRangeXZ);
             Gizmos.DrawWireCube(scatterBase, scatterSize);
        }
        // Optionally add a gizmo draw for the scatter area even before explosion based on gizmoCenter
        // else if (Application.isPlaying && _hasCollided) { ... }
    }
}