using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class HomingProjectile : MonoBehaviour
{
    private TowerData _towerData;
    private Transform _attacker;
    private EnemyController _targetEnemy;
    private Vector3 _lastKnownPosition;

    [Header("Flight Path")]
    public float launchDuration = 3.0f;
    public float launchSpeed = 10f;
    public float homingSpeed = 25f;
    public float turnSpeed = 5f;

    [Header("Visuals")]
    public ParticleSystem engineParticles;
    public float particleBoostMultiplier = 3f;
    
    // --- NEW FIELD ---
    [Tooltip("Visual fix. If rocket flies sideways, try setting X to 90 or -90.")]
    public Vector3 modelRotationOffset; 
    // -----------------

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
        Destroy(gameObject, 15f);
    }

    void FixedUpdate()
    {
        if (_hasExploded) return;

        _timer += Time.fixedDeltaTime;

        if (_timer < launchDuration)
        {
            // Phase 1: Launch
            _rb.linearVelocity = Vector3.up * launchSpeed;
        }
        else
        {
            // Phase 2: Homing
            if (!_isHoming) ActivateHomingMode();
            MoveTowardsTarget();
        }
        
        // --- UPDATED ROTATION LOGIC ---
        if (_rb.linearVelocity != Vector3.zero)
        {
            // 1. Calculate where we are going (The "Physics" Rotation)
            //Quaternion lookRot = Quaternion.LookRotation(_rb.linearVelocity);
            
            // 2. Add your offset to fix the "Sideways" mesh
            // We apply the offset LOCAL to the look rotation
            //transform.rotation = lookRot * Quaternion.Euler(modelRotationOffset);
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

    void MoveTowardsTarget()
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
        Vector3 newVelocity = Vector3.RotateTowards(_rb.linearVelocity, direction * homingSpeed, turnSpeed * Time.fixedDeltaTime, 10f);
        _rb.linearVelocity = newVelocity.normalized * homingSpeed;
        
        // 1. Calculate where we are going (The "Physics" Rotation)
        Quaternion lookRot = Quaternion.LookRotation(_rb.linearVelocity);
            
        // 2. Add your offset to fix the "Sideways" mesh
        // We apply the offset LOCAL to the look rotation
        transform.rotation = lookRot * Quaternion.Euler(modelRotationOffset);
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
            Instantiate(_towerData.impactParticlePrefab, position, Quaternion.identity);
        }

        if (_towerData != null)
        {
            Collider[] hits = Physics.OverlapSphere(position, _towerData.explosionRadius);
            foreach (Collider hit in hits)
            {
                if (hit.CompareTag("Enemy"))
                {
                    EnemyController enemy = hit.GetComponent<EnemyController>();
                    if (enemy != null)
                    {
                        enemy.TakeExplosion(
                            _towerData.damage, 
                            _attacker, 
                            position, 
                            _towerData.explosionForce, 
                            _towerData.explosionRadius
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