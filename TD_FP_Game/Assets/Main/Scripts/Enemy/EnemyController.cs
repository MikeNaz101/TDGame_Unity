using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyController : MonoBehaviour
{
    // --- AI States ---
    private enum EnemyState { Pursuing, Attacking, Ragdolled }
    private EnemyState _state;

    // --- SETUP & DATA ---
    [Header("Data & Manager References")]
    public EnemyData enemyData;
    public WaveManager _waveManager;
    public Transform _playerTarget;
    public FieldOfView fov;
    public LayerMask obstacleLayer;

    [Header("Ragdoll")]
    [Tooltip("The central, main rigidbody of the ragdoll (e.g., the Hips or Pelvis).")]
    public Rigidbody pelvisRigidbody;
    [Tooltip("All rigidbodies for the ragdoll.")]
    public Rigidbody[] _ragdollRigidbodies;
    [Tooltip("Minimum time to stay on ground before checking for rest.")]
    public float ragdollMinTime = 0.5f; // Short buffer

    [Header("AI & Navigation")]
    [SerializeField] private NavMeshAgent _agent;
    [SerializeField] private float _attackThreshold = 2.5f;

    // --- RUNTIME STATE ---
    private float _currentHealth;
    private bool _isDead = false;
    private float _timeSinceLastAttack = 0f;
    private Collider _mainCollider;
    private Animator _animator;
    private Rigidbody _mainRigidbody;
    
    public float CurrentHealth => _currentHealth;
    public static List<EnemyController> ActiveEnemies = new List<EnemyController>();
    
    private IDamageable _currentTarget;
    private Transform _coreTarget;
    private Transform _lastKnownPlayerPos;
    private Transform _soundInvestigationPos;
    private Transform _attackSource;
    private float _sensorCooldown = 0f;
    public enum AIStance { Standard, Aggressive, Evasive }
    private AIStance _currentStance = AIStance.Standard;
    private float _originalMoveSpeed;
    private float _originalStoppingDistance;
    private float _spawnerTimeAlive = 0f;
    private float _timeSinceLastGrowth = 0f;
    private float _timeSinceLastSpawn;
    private float _wanderTimer;

    private const float SENSOR_UPDATE_RATE = 0.5f;
    private const float WANDER_UPDATE_RATE = 3.0f;
    private const float WANDER_DISTANCE = 10f;
    private const float ATTACK_RANGE_BUFFER = 1f;

    #region --- Unity Lifecycle & Initialization ---

    void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        _mainCollider = GetComponent<Collider>();
        _animator = GetComponentInChildren<Animator>();
        _mainRigidbody = GetComponent<Rigidbody>(); 
    }

    void Start()
    {
        if (enemyData == null) { Debug.LogError("EnemyData is NULL!", this); return; }
        if (_waveManager == null) _waveManager = FindObjectOfType<WaveManager>();
        if (_playerTarget == null) { GameObject p = GameObject.FindGameObjectWithTag("Player"); if (p != null) _playerTarget = p.transform; }
        GameObject coreObj = GameObject.FindGameObjectWithTag("Core");
        if (coreObj != null) _coreTarget = coreObj.transform;
        
        _currentHealth = enemyData.baseHealth;
        _agent.speed = enemyData.moveSpeed;
        _agent.stoppingDistance = _attackThreshold - 0.5f;
        transform.localScale = Vector3.one * enemyData.baseScale;
        _agent.enabled = true;
        
        SetRagdollActive(false);
        if (_mainRigidbody != null) _mainRigidbody.isKinematic = true; 
        
        _originalMoveSpeed = enemyData.moveSpeed;
        _originalStoppingDistance = _agent.stoppingDistance;
        if (!ActiveEnemies.Contains(this)) ActiveEnemies.Add(this);
        if (_coreTarget != null) _currentTarget = _coreTarget.GetComponent<IDamageable>();
        _state = EnemyState.Pursuing;
        _timeSinceLastAttack = enemyData.attackCooldown;
        if (enemyData.canBeDistracted) EnemyAIAudioEvents.OnGunshotReported += HandleGunshot;
        if (enemyData.isSpawner) { _spawnerTimeAlive = enemyData.spawnerTimeLimit; _timeSinceLastSpawn = enemyData.spawnInterval; }
    }

    void OnDestroy()
    {
        if (enemyData != null && enemyData.canBeDistracted) EnemyAIAudioEvents.OnGunshotReported -= HandleGunshot;
        ActiveEnemies.Remove(this);
    }

    void Update()
    {
        if (_isDead) return;
        _timeSinceLastAttack += Time.deltaTime;
        _sensorCooldown -= Time.deltaTime;
        if (enemyData.isSpawner) UpdateSpawnerLogic();
        RunAILogic();
    }

    #endregion

    #region --- AI Logic Methods ---
    
    private void RunAILogic()
    {
        if (_state == EnemyState.Ragdolled) return; // Do nothing while flying/falling
        if (!_agent.enabled || !_agent.isOnNavMesh) return;

        if ((_currentTarget as UnityEngine.Object) == null || _currentTarget.transform == null || !_currentTarget.transform.gameObject.activeInHierarchy)
        {
            _currentTarget = _coreTarget.GetComponent<IDamageable>();
            _state = EnemyState.Pursuing;
            if (_currentTarget == null) return;
        }

        if (_sensorCooldown <= 0f) { UpdateSensors(); _sensorCooldown = SENSOR_UPDATE_RATE; }

        switch (_state)
        {
            case EnemyState.Pursuing: UpdatePursueState(); break;
            case EnemyState.Attacking: UpdateAttackState(); break;
            case EnemyState.Ragdolled: break;
        }
    }
    
    // ... (Copy standard AI methods: UpdateSensors, UpdatePursueState, etc.) ...
    private void UpdateSensors() {
        if (enemyData.canBeDistracted) {
            if (_attackSource != null) { SetNewTarget(_attackSource.GetComponent<IDamageable>()); _attackSource = null; return; }
            if (fov != null && _playerTarget != null && fov.IsTargetVisible(_playerTarget)) { _lastKnownPlayerPos = _playerTarget; SetNewTarget(_playerTarget.GetComponent<IDamageable>()); return; }
            if (_soundInvestigationPos != null) { if (_agent.destination != _soundInvestigationPos.position) _agent.SetDestination(_soundInvestigationPos.position); _soundInvestigationPos = null; return; }
            if (_lastKnownPlayerPos != null) { if (Vector3.Distance(transform.position, _lastKnownPlayerPos.position) < _agent.stoppingDistance + 1f) _lastKnownPlayerPos = null; }
        }
        if (_currentTarget.transform == _coreTarget) { if (CheckForDoorObstacle(out DoorHealth door)) { SetNewTarget(door); return; } }
        if (_currentTarget.transform != _playerTarget) { SetNewTarget(_coreTarget.GetComponent<IDamageable>()); }
    }
    private void UpdatePursueState() { 
        _agent.isStopped = false;
        if (Vector3.Distance(transform.position, _currentTarget.transform.position) <= _agent.stoppingDistance) { _state = EnemyState.Attacking; return; }
        if (_currentStance == AIStance.Evasive && !enemyData.isSpawner) { _wanderTimer -= Time.deltaTime; if (_wanderTimer <= 0f || _agent.remainingDistance < _agent.stoppingDistance + 1f) { Vector3 dodgePoint = GetEvasiveManeuverPoint(_currentTarget.transform.position); _agent.SetDestination(dodgePoint); _wanderTimer = WANDER_UPDATE_RATE; } }
        else if (_currentStance == AIStance.Aggressive || enemyData.movementType == EnemyData.AIMovementType.Direct) { _agent.SetDestination(_currentTarget.transform.position); }
        else if (enemyData.movementType == EnemyData.AIMovementType.Wander) { _wanderTimer -= Time.deltaTime; if (_wanderTimer <= 0f || _agent.remainingDistance < 1f) { Vector3 randomPoint = GetStandardWanderPoint(_currentTarget.transform.position); _agent.SetDestination(randomPoint); _wanderTimer = WANDER_UPDATE_RATE; } }
    }
    private void UpdateAttackState() { 
        if (Vector3.Distance(transform.position, _currentTarget.transform.position) > _agent.stoppingDistance + ATTACK_RANGE_BUFFER) { _state = EnemyState.Pursuing; return; }
        _agent.isStopped = true; RotateTowards(_currentTarget.transform.position);
        if (_timeSinceLastAttack >= enemyData.attackCooldown) { TryAttack(); }
    }
    private void UpdateSpawnerLogic() { 
        if (enemyData.canGrow) { _timeSinceLastGrowth += Time.deltaTime; if (_timeSinceLastGrowth >= enemyData.growthInterval) { Grow(); _timeSinceLastGrowth = 0f; } }
        if (enemyData.spawnerTimeLimit > 0) { _spawnerTimeAlive -= Time.deltaTime; if (_spawnerTimeAlive <= 0f) return; }
        _timeSinceLastSpawn += Time.deltaTime; if (_timeSinceLastSpawn >= enemyData.spawnInterval) { SpawnMinion(); _timeSinceLastSpawn = 0f; }
    }
    public void SetAIStance(AIStance newStance) { if (_currentStance == newStance) return; _currentStance = newStance; switch (newStance) { case AIStance.Aggressive: _agent.speed = _originalMoveSpeed * 1.5f; _agent.stoppingDistance = _originalStoppingDistance * 0.75f; break; case AIStance.Evasive: _agent.speed = _originalMoveSpeed; _agent.stoppingDistance = _originalStoppingDistance; break; case AIStance.Standard: default: _agent.speed = _originalMoveSpeed; _agent.stoppingDistance = _originalStoppingDistance; break; } }
    public void ApplySpeedModification(float speedMultiplier) { if (_agent != null) _agent.speed = _originalMoveSpeed * speedMultiplier; }
    void TryAttack() { if (_currentTarget == null) return; if (_timeSinceLastAttack >= enemyData.attackCooldown) { _currentTarget.TakeDamage(enemyData.attackDamage); _timeSinceLastAttack = 0f; } }
    private bool CheckForDoorObstacle(out DoorHealth door) { if (_agent.velocity.magnitude < 0.1f && !_agent.pathPending && _agent.remainingDistance > _agent.stoppingDistance) { Vector3 scanPos = transform.position + transform.forward * (_agent.stoppingDistance - 0.5f); Collider[] hits = Physics.OverlapBox(scanPos, new Vector3(2f, 2f, 2f), transform.rotation, obstacleLayer); foreach (Collider hit in hits) { if (hit.CompareTag("Door")) { if (hit.TryGetComponent<DoorHealth>(out door)) return true; } } } door = null; return false; }
    private Vector3 GetStandardWanderPoint(Vector3 targetPosition) { Vector3 dirToTarget = (targetPosition - transform.position).normalized; Vector3 randomDir = new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f)).normalized; Vector3 wanderDir = (dirToTarget * 0.7f + randomDir * 0.3f).normalized; Vector3 targetPoint = transform.position + wanderDir * WANDER_DISTANCE; if (NavMesh.SamplePosition(targetPoint, out NavMeshHit hit, 5f, _agent.areaMask)) return hit.position; return targetPosition; }
    private Vector3 GetEvasiveManeuverPoint(Vector3 targetPosition) { Vector3 dirToTarget = (targetPosition - transform.position).normalized; Vector3 sideDir = Vector3.Cross(dirToTarget, Vector3.up).normalized; sideDir *= (Random.Range(0, 2) * 2 - 1); float dodgeSideDistance = 5f; float dodgeForwardDistance = 4f; Vector3 targetPoint = transform.position + (sideDir * dodgeSideDistance) + (dirToTarget * dodgeForwardDistance); if (NavMesh.SamplePosition(targetPoint, out NavMeshHit hit, 5f, _agent.areaMask)) return hit.position; return targetPosition; }
    void Grow() { if (transform.localScale.x >= enemyData.maxGrowthSize) return; float newScale = Mathf.Min(transform.localScale.x * enemyData.growthRate, enemyData.maxGrowthSize); transform.localScale = new Vector3(newScale, newScale, newScale); }
    void SpawnMinion() { if (enemyData.spawnPrefabs == null || enemyData.spawnPrefabs.Count == 0) return; Vector3 randomPos = transform.position + (Random.insideUnitSphere * 3f); randomPos.y = transform.position.y; GameObject prefabToSpawn = enemyData.spawnPrefabs[Random.Range(0, enemyData.spawnPrefabs.Count)]; Instantiate(prefabToSpawn, randomPos, Quaternion.identity); }
    
    #endregion

    #region --- Event Handlers (Hearing, Damage) ---

    void HandleGunshot(Vector3 soundPosition) { if (_isDead || !enemyData.canBeDistracted) return; float distance = Vector3.Distance(transform.position, soundPosition); if (distance <= enemyData.hearingRange) { if (_currentTarget.transform != _playerTarget) { GameObject soundPosObj = new GameObject($"SoundInvestigate_@{soundPosition}"); soundPosObj.transform.position = soundPosition; _soundInvestigationPos = soundPosObj.transform; _lastKnownPlayerPos = null; Destroy(soundPosObj, 5f); } } }
    public void TakeDamage(float amount, Transform attacker) { if (_isDead) return; _currentHealth -= amount; if (enemyData.canBeDistracted) _attackSource = attacker; if (_currentHealth <= 0f) Die(true); }
    
    // --- TakeExplosion (Resets state if already hit) ---
    public void TakeExplosion(float damage, Transform attacker, Vector3 explosionPosition, float explosionForce, float explosionRadius, float upwardModifier = 3.0f)
    {
        if (_isDead) return;

        _currentHealth -= damage;
        bool hasRagdoll = _ragdollRigidbodies != null && _ragdollRigidbodies.Length > 0;
        bool hasSimplePhysics = _mainRigidbody != null;

        if (_currentHealth <= 0f)
        {
            Die(true);
            if (hasRagdoll) {
                foreach (Rigidbody rb in _ragdollRigidbodies) if (rb != null) rb.AddExplosionForce(explosionForce, explosionPosition, explosionRadius, upwardModifier, ForceMode.Impulse);
            } else if (hasSimplePhysics) {
                _mainRigidbody.AddExplosionForce(explosionForce, explosionPosition, explosionRadius, upwardModifier, ForceMode.Impulse);
            }
        }
        else if (hasRagdoll || hasSimplePhysics)
        {
            // 1. Reset state to Ragdolled (in case we were getting up)
            // 2. Apply Force
            // 3. Start/Restart Monitor Routine
            
            _state = EnemyState.Ragdolled;
            if (_agent.enabled) _agent.enabled = false;
            
            if (hasRagdoll) {
                ActivateRagdoll();
                foreach (Rigidbody rb in _ragdollRigidbodies) if (rb != null) rb.AddExplosionForce(explosionForce, explosionPosition, explosionRadius, upwardModifier, ForceMode.Impulse);
            } else {
                if (_mainRigidbody != null) {
                    _mainRigidbody.isKinematic = false;
                    _mainRigidbody.AddExplosionForce(explosionForce, explosionPosition, explosionRadius, upwardModifier, ForceMode.Impulse);
                }
            }

            // Start the monitor (it will wait until stopped and grounded)
            StopCoroutine("MonitorRagdollState"); // Stop any existing check
            StartCoroutine(MonitorRagdollState(hasRagdoll));
        }
        else
        {
            if (enemyData.canBeDistracted && attacker != null) _attackSource = attacker;
        }
    }

    void Die(bool useRagdoll)
    {
        if (_isDead) return;
        _isDead = true;
        _state = EnemyState.Ragdolled;
        
        if (_waveManager != null) _waveManager.EnemyDestroyed();
        
        // Spawn Scrap
        if (enemyData != null && enemyData.scrapMetalPrefab != null) 
            Instantiate(enemyData.scrapMetalPrefab, transform.position, Quaternion.identity);
            
        if (_agent.enabled) _agent.enabled = false; 

        // Check capabilities
        bool hasRagdoll = useRagdoll && _ragdollRigidbodies != null && _ragdollRigidbodies.Length > 0;
        bool hasSimplePhysics = _mainRigidbody != null;
        bool hasParticles = enemyData != null && enemyData.deathParticlePrefab != null;

        // --- PRIORITY 1: Ragdoll (Complex Physics) ---
        if (hasRagdoll)
        {
            ActivateRagdoll();
            Destroy(gameObject, 5f);
        }
        // --- PRIORITY 2: Particles (Instant Death) ---
        // We prioritize this over simple physics so "exploding" enemies vanish immediately
        else if (hasParticles)
        {
            Instantiate(enemyData.deathParticlePrefab, transform.position, Quaternion.identity);
            Destroy(gameObject, 0.1f); // Destroy almost instantly
        }
        // --- PRIORITY 3: Simple Physics (Flying Corpse) ---
        // Only use this if we have NO ragdoll AND NO particles
        else if (hasSimplePhysics)
        {
            _mainRigidbody.isKinematic = false;
            Destroy(gameObject, 5f); 
        }
        // --- PRIORITY 4: Fallback ---
        else
        {
            Destroy(gameObject, 0.1f);
        }
    }
    
    #endregion

    #region --- Utility & Ragdoll Methods ---

    // --- UPDATED: Robust Ground Check for both Ragdolls and Boxes ---
    private IEnumerator MonitorRagdollState(bool isRagdoll)
    {
        // 1. Wait a moment to allow the force to lift us off the ground
        yield return new WaitForSeconds(ragdollMinTime); // e.g. 0.5s

        Rigidbody rbToCheck = isRagdoll ? pelvisRigidbody : _mainRigidbody;

        // 2. Loop until we are stable AND on the ground
        while (true)
        {
            if (rbToCheck != null)
            {
                // Check A: Velocity (Are we stopped?)
                bool isStopped = rbToCheck.linearVelocity.magnitude < 0.1f;
                
                // Check B: Grounded (Are we touching floor?)
                float checkDistance = 1.0f;
                
                // If we are a box, we need to raycast further because pivot might be in center
                if (!isRagdoll && _mainCollider != null)
                {
                     checkDistance = _mainCollider.bounds.extents.y + 0.2f;
                }

                bool isGrounded = Physics.Raycast(rbToCheck.position, Vector3.down, checkDistance, 1 << LayerMask.NameToLayer("Default"));

                if (isStopped && isGrounded)
                {
                    break; // We are safe to get up
                }
            }
            else
            {
                break; // Failsafe
            }

            yield return new WaitForSeconds(0.1f); // Check 10 times a second
        }

        GetUp(isRagdoll);
    }

    private void GetUp(bool isRagdoll)
    {
        if (_isDead) return;

        Vector3 getUpPosition = transform.position;
        if (isRagdoll && pelvisRigidbody != null) getUpPosition = pelvisRigidbody.transform.position;
        else if (!isRagdoll && _mainRigidbody != null) getUpPosition = _mainRigidbody.position;
        
        DeactivateRagdoll();
        
        if (!isRagdoll && _mainRigidbody != null) 
        {
            _mainRigidbody.isKinematic = true;
            // Reset rotation so the box stands up (optional, depends on your game style)
            transform.rotation = Quaternion.identity; 
        }
        
        if (NavMesh.SamplePosition(getUpPosition, out NavMeshHit hit, 5.0f, _agent.areaMask)) _agent.Warp(hit.position);
        else _agent.Warp(getUpPosition);
        
        _agent.enabled = true;
        _state = EnemyState.Pursuing;
    }

    private void SetNewTarget(IDamageable newTarget) { if (newTarget == null || newTarget == _currentTarget) return; _currentTarget = newTarget; _state = EnemyState.Pursuing; }

    private void ActivateRagdoll() { SetRagdollActive(true); if (_mainCollider != null) _mainCollider.enabled = false; if (_animator != null) _animator.enabled = false; }
    private void DeactivateRagdoll() { SetRagdollActive(false); if (_mainCollider != null) _mainCollider.enabled = true; if (_animator != null) _animator.enabled = true; }

    private void SetRagdollActive(bool isActive) {
        if (_ragdollRigidbodies == null || _ragdollRigidbodies.Length == 0) return;
        foreach (Rigidbody rb in _ragdollRigidbodies) { if (rb != null) rb.isKinematic = !isActive; }
    }
    private void RotateTowards(Vector3 targetPosition) { Vector3 direction = (targetPosition - transform.position).normalized; direction.y = 0; if (direction != Vector3.zero) { Quaternion lookRotation = Quaternion.LookRotation(direction); transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * 5f); } }
    
    #endregion
}