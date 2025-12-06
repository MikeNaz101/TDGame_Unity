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

    // Multipliers set by Tower
    [HideInInspector] public float speedMultiplier = 1f;
    [HideInInspector] public float radiusMultiplier = 1f;
    [HideInInspector] public float damageMultiplier = 1f;

    private TowerData _data;
    private Transform _target;
    private Transform _attacker; 
    private bool _isLaunched = false;
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

        Invoke("TimeOutExplosion", lifetime);
    }

    void Update()
    {
        if (!_isLaunched) return;

        float currentSpeed = moveSpeed * speedMultiplier;

        Vector3 targetPos;
        if (_target != null)
        {
            targetPos = _target.position + Vector3.up; 
            _lastKnownTargetPos = targetPos;
        }
        else
        {
            targetPos = _lastKnownTargetPos; 
        }

        Vector3 directionToTarget = targetPos - transform.position;
        Vector3 newDirection = Vector3.RotateTowards(transform.forward, directionToTarget, turnSpeed * Time.deltaTime, 0.0f);
        
        if (newDirection != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(newDirection) * Quaternion.Euler(modelRotationOffset);
        }

        transform.position += newDirection.normalized * currentSpeed * Time.deltaTime;
    }

    void OnTriggerEnter(Collider other)
    {
        if (!_isLaunched) return; 

        if (other.CompareTag("Enemy") || other.gameObject.layer == LayerMask.NameToLayer("Default"))
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
        CancelInvoke("TimeOutExplosion"); 

        // --- NEW: Play Explosion Sound ---
        // We create a temporary audio source at the location because this object is about to be destroyed
        if (explosionSound != null)
        {
            AudioSource.PlayClipAtPoint(explosionSound, transform.position);
        }
        // ---------------------------------

        if (_data != null && _data.impactParticlePrefab != null)
        {
            GameObject vfx = Instantiate(_data.impactParticlePrefab, transform.position, Quaternion.identity);
            // Scale explosion visual based on upgrade
            if (radiusMultiplier > 1.0f) vfx.transform.localScale *= radiusMultiplier;
        }

        if (_data != null)
        {
            float finalRadius = _data.explosionRadius * radiusMultiplier;
            float finalDamage = _data.damage * damageMultiplier;

            Collider[] hits = Physics.OverlapSphere(transform.position, finalRadius);
            foreach (Collider hit in hits)
            {
                if (hit.CompareTag("Enemy"))
                {
                    EnemyController enemy = hit.GetComponent<EnemyController>();
                    if (enemy != null)
                    {
                        enemy.TakeExplosion(
                            finalDamage, 
                            _attacker, 
                            transform.position, 
                            _data.explosionForce, 
                            finalRadius
                        );
                    }
                }
            }
        }

        Destroy(gameObject);
    }
}