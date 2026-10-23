using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class SlowHomingRocket : MonoBehaviour
{
    [Header("Flight Characteristics")]
    public float moveSpeed = 5f;         
    public float turnSpeed = 5.0f;       
    public float lifetime = 5.0f;        
    
    [Header("Visuals")]
    public GameObject engineParticles;   
    public Vector3 modelRotationOffset; 

    [Header("Audio")]
    public AudioClip flightLoopSound;
    public AudioClip explosionSound;
    
    [Header("Weapon Fallback Stats")]
    public float weaponDamage = 50f;
    public float weaponRadius = 5f;
    public float weaponForce = 500f;

    // Multipliers set by Tower
    [HideInInspector] public float speedMultiplier = 1f;
    [HideInInspector] public float radiusMultiplier = 1f;
    [HideInInspector] public float damageMultiplier = 1f;

    private TowerData _data;
    private Transform _target;
    private Transform _attacker; 
    private bool _isLaunched = false;
    private bool _hasExploded = false;
    private Vector3 _lastKnownTargetPos;
    private AudioSource _audioSource;

    public void Initialize(TowerData data, EnemyController target, Transform attacker)
    {
        _data = data;
        _attacker = attacker;
        _audioSource = GetComponent<AudioSource>();
        
        if (target != null)
        {
            _target = target.transform;
            _lastKnownTargetPos = _target.position;
        }

        Collider col = GetComponent<Collider>();
        if(col) col.enabled = false;
        
        if (engineParticles != null) engineParticles.SetActive(false);
    }

    /// <summary>Point the rocket at a new target before (or after) launch.</summary>
    public void Retarget(EnemyController target)
    {
        if (target == null) return;
        _target = target.transform;
        _lastKnownTargetPos = _target.position + Vector3.up;
    }

    public void Launch()
    {
        _isLaunched = true;
        
        Collider col = GetComponent<Collider>();
        if(col) col.enabled = true;

        if (engineParticles != null) engineParticles.SetActive(true);

        // --- NEW: Play Flight Sound ---
        if (_audioSource != null && flightLoopSound != null)
        {
            _audioSource.clip = flightLoopSound;
            _audioSource.loop = true;
            _audioSource.Play();
        }
        // ------------------------------

        Invoke(nameof(TimeOutExplosion), lifetime);
    }

    void Update()
    {
        if (!_isLaunched) return;

        float currentSpeed = moveSpeed * speedMultiplier;

        // --- CASE 1: HOMING ENABLED (Target Exists) ---
        if (_target != null)
        {
            // Update last known position in case they disappear/die
            if (_target.gameObject.activeInHierarchy)
            {
                _lastKnownTargetPos = _target.position + Vector3.up; 
            }

            Vector3 directionToTarget = _lastKnownTargetPos - transform.position;
            Vector3 newDirection = Vector3.RotateTowards(transform.forward, directionToTarget, turnSpeed * Time.deltaTime, 0.0f);
            
            if (newDirection != Vector3.zero)
            {
                transform.rotation = Quaternion.LookRotation(newDirection) * Quaternion.Euler(modelRotationOffset);
            }
        }
        // --- CASE 2: DUMB FIRE (Fly Straight) ---
        else
        {
            // Just keep current rotation and move forward
            // (No rotation code needed, it stays facing launch direction)
        }

        // Apply movement (works for both cases)
        transform.position += transform.forward * currentSpeed * Time.deltaTime;
    }

    void OnTriggerEnter(Collider other)
    {
        if (!_isLaunched) return; 

        if (other.CompareTag("Enemy") || other.gameObject.layer == ExplosionUtility.DefaultLayer)
        {
            Explode();
        }
    }

    void TimeOutExplosion()
    {
        if (this != null) Explode();
    }

    void Explode()
    {
        if (_hasExploded) return;
        _hasExploded = true;
        CancelInvoke(nameof(TimeOutExplosion)); 

        // Play Sound
        if (explosionSound != null) AudioSource.PlayClipAtPoint(explosionSound, transform.position);

        // Spawn VFX
        if (_data != null && _data.impactParticlePrefab != null)
        {
            GameObject vfx = Instantiate(_data.impactParticlePrefab, transform.position, Quaternion.identity);
            if (radiusMultiplier > 1.0f) vfx.transform.localScale *= radiusMultiplier;
        }

        // --- CALCULATE DAMAGE ---
        // Default to Weapon Stats
        float finalDamage = weaponDamage * damageMultiplier;
        float finalRadius = weaponRadius * radiusMultiplier;
        float finalForce = weaponForce;

        // If TowerData exists, override with Tower Stats
        if (_data != null)
        {
            finalDamage = _data.damage * damageMultiplier;
            finalRadius = _data.explosionRadius * radiusMultiplier;
            finalForce = _data.explosionForce;
        }
        // ------------------------

        ExplosionUtility.DamageEnemiesInRadius(transform.position, finalRadius, finalDamage, _attacker, finalForce);

        Destroy(gameObject);
    }
}