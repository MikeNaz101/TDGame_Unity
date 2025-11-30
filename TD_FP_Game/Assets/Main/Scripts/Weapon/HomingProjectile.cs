using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class HomingProjectile : MonoBehaviour
{
    private TowerData _towerData;
    private Transform _attacker;
    private EnemyController _targetEnemy;
    private Vector3 _lastKnownPosition;

    [Header("Flight Path")]
    public float launchDuration = 1.0f; // Shortened slightly for snappier feel
    public float launchSpeed = 10f;
    public float homingSpeed = 25f;
    public float turnSpeed = 5f;

    [Header("Visuals")]
    public ParticleSystem engineParticles;
    public float particleBoostMultiplier = 3f;
    public Vector3 modelRotationOffset; 

    // --- NEW: MULTIPLIERS (Set by Tower) ---
    [HideInInspector] public float speedMultiplier = 1f;
    [HideInInspector] public float radiusMultiplier = 1f;
    [HideInInspector] public float damageMultiplier = 1f;

    private bool _isHoming = false;
    private float _timer = 0f;
    private Rigidbody _rb;
    private bool _hasExploded = false;

    public void Initialize(TowerData data, Transform attacker, EnemyController target)
    {
        _towerData = data;
        _attacker = attacker;
        _targetEnemy = target;
        if(_targetEnemy != null) _lastKnownPosition = _targetEnemy.transform.position;
    }

    void Start()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.useGravity = false;
        Destroy(gameObject, 15f); // Safety destroy
    }

    void FixedUpdate()
    {
        if (_hasExploded) return;

        _timer += Time.fixedDeltaTime;

        // Apply Speed Multiplier
        float currentLaunchSpeed = launchSpeed * speedMultiplier;
        float currentHomingSpeed = homingSpeed * speedMultiplier;

        if (_timer < launchDuration)
        {
            // Phase 1: Launch Up
            _rb.linearVelocity = Vector3.up * currentLaunchSpeed;
        }
        else
        {
            // Phase 2: Homing
            if (!_isHoming) ActivateHomingMode();
            MoveTowardsTarget(currentHomingSpeed);
        }
        
        // Rotation Fix
        if (_rb.linearVelocity != Vector3.zero)
        {
            Quaternion lookRot = Quaternion.LookRotation(_rb.linearVelocity);
            transform.rotation = lookRot * Quaternion.Euler(modelRotationOffset);
        }
    }

    void ActivateHomingMode()
    {
        _isHoming = true;
        if (engineParticles != null)
        {
            var main = engineParticles.main;
            main.startSizeMultiplier *= particleBoostMultiplier;
            main.startSpeedMultiplier *= particleBoostMultiplier;
        }
    }

    void MoveTowardsTarget(float speed)
    {
        Vector3 targetPos;
        if (_targetEnemy != null && _targetEnemy.gameObject.activeInHierarchy)
        {
            targetPos = _targetEnemy.transform.position + Vector3.up;
            _lastKnownPosition = targetPos;
        }
        else
        {
            targetPos = _lastKnownPosition;
        }

        Vector3 direction = (targetPos - transform.position).normalized;
        Vector3 newVelocity = Vector3.RotateTowards(_rb.linearVelocity, direction * speed, turnSpeed * Time.fixedDeltaTime, 10f);
        _rb.linearVelocity = newVelocity.normalized * speed;
    }

    void OnTriggerEnter(Collider other)
    {
        if (_hasExploded) return;

        if (other.CompareTag("Enemy") || other.gameObject.layer == LayerMask.NameToLayer("Default"))
        {
            Explode(transform.position);
        }
    }

    void Explode(Vector3 position)
    {
        _hasExploded = true;
        _rb.linearVelocity = Vector3.zero;

        if (_towerData != null && _towerData.impactParticlePrefab != null)
        {
            // Scale particle effect for larger blasts?
            GameObject vfx = Instantiate(_towerData.impactParticlePrefab, position, Quaternion.identity);
            if (radiusMultiplier > 1.2f) vfx.transform.localScale *= radiusMultiplier; // Make explosion look bigger
        }

        if (_towerData != null)
        {
            // Calculate final radius
            float finalRadius = _towerData.explosionRadius * radiusMultiplier;

            Collider[] hits = Physics.OverlapSphere(position, finalRadius);
            foreach (Collider hit in hits)
            {
                if (hit.CompareTag("Enemy"))
                {
                    EnemyController enemy = hit.GetComponent<EnemyController>();
                    if (enemy != null)
                    {
                        enemy.TakeExplosion(
                            _towerData.damage * damageMultiplier, // Apply Damage Mult
                            _attacker, 
                            position, 
                            _towerData.explosionForce, 
                            finalRadius // Apply Radius Mult
                        );
                    }
                }
            }
        }

        if (engineParticles != null)
        {
            engineParticles.Stop();
            engineParticles.transform.parent = null; 
            Destroy(engineParticles.gameObject, 2f);
        }
        Destroy(gameObject);
    }
}