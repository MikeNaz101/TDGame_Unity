using UnityEngine;

public class SlowHomingRocket : MonoBehaviour
{
    [Header("Flight Characteristics")]
    public float moveSpeed = 5f;         // Slow fly speed
    public float turnSpeed = 2.0f;       // How fast it can turn (Radians/sec)
    public float lifetime = 5.0f;        // Explode after 5 seconds
    
    [Header("Visuals")]
    public GameObject engineParticles;   // Trail effect

    private TowerData _data;
    private Transform _target;
    private Transform _attacker; // The tower that shot this
    private bool _isLaunched = false;
    private Vector3 _lastKnownTargetPos;

    public void Initialize(TowerData data, EnemyController target, Transform attacker)
    {
        _data = data;
        _attacker = attacker;
        
        if (target != null)
        {
            _target = target.transform;
            _lastKnownTargetPos = _target.position;
        }

        // Disable collision while sitting on the launcher
        Collider col = GetComponent<Collider>();
        if(col) col.enabled = false;
        
        // Disable engine particles initially
        if (engineParticles != null) engineParticles.SetActive(false);
    }

    public void Launch()
    {
        _isLaunched = true;
        
        // Enable collision
        Collider col = GetComponent<Collider>();
        if(col) col.enabled = true;

        // Enable engine trail
        if (engineParticles != null) engineParticles.SetActive(true);

        // Start the countdown for auto-detonation
        //Destroy(gameObject, lifetime);
        // Also invoke explosion logic if lifetime ends (Unity Destroy doesn't call custom logic)
        Invoke("TimeOutExplosion", lifetime);
    }

    void Update()
    {
        if (!_isLaunched) return;

        // 1. Determine where we want to go
        Vector3 targetPos;
        if (_target != null)
        {
            targetPos = _target.position; //+ Vector3.up; // Aim slightly up for center mass
            _lastKnownTargetPos = targetPos;
        }
        else
        {
            targetPos = _lastKnownTargetPos; // Target dead/gone, fly to last spot
        }

        // 2. Calculate Smooth Turn
        Vector3 directionToTarget = targetPos - transform.position;
        
        // RotateTowards gives us the smooth turn effect
        Vector3 newDirection = Vector3.RotateTowards(transform.forward, directionToTarget, turnSpeed * Time.deltaTime, 0.0f);
        
        // Apply Rotation
        transform.rotation = Quaternion.LookRotation(newDirection);

        // 3. Move Forward
        transform.position += transform.forward * moveSpeed * Time.deltaTime;
    }

    void OnTriggerEnter(Collider other)
    {
        if (!_isLaunched) return; // Don't explode if sitting on the launcher

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
        CancelInvoke("TimeOutExplosion"); // Stop the timer check

        // 1. Visuals
        if (_data.impactParticlePrefab != null)
        {
            Instantiate(_data.impactParticlePrefab, transform.position, Quaternion.identity);
        }

        // 2. Area Damage
        Collider[] hits = Physics.OverlapSphere(transform.position, _data.explosionRadius);
        foreach (Collider hit in hits)
        {
            if (hit.CompareTag("Enemy"))
            {
                EnemyController enemy = hit.GetComponent<EnemyController>();
                if (enemy != null)
                {
                    enemy.TakeExplosion(
                        _data.damage, 
                        _attacker, 
                        transform.position, 
                        _data.explosionForce, 
                        _data.explosionRadius
                    );
                }
            }
        }

        Destroy(gameObject);
    }
}