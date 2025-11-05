using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyController : MonoBehaviour
{
    // --- AI States ---
    // This is the new, unified state machine
    private enum EnemyState { Pursuing, Attacking }
    private EnemyState _state;

    // --- SETUP & DATA ---
    [Header("Data & Manager References")]
    [Tooltip("The Scriptable Object containing all stats for this enemy type.")]
    public EnemyData enemyData;
    [Tooltip("The central manager for wave progression and enemy count.")]
    public WaveManager _waveManager;
    [Tooltip("The player, which will be found by tag.")]
    public Transform _playerTarget;
    [Tooltip("The Field of View component for visual detection.")]
    public FieldOfView fov;
    [Tooltip("All rigidbodies for the ragdoll.")]
    public Rigidbody[] _ragdollRigidbodies;
    [Tooltip("The LayerMask that only contains 'Door' objects.")]
    public LayerMask obstacleLayer; // Used to detect doors

    [Header("AI & Navigation")]
    [SerializeField] private NavMeshAgent _agent;
    [SerializeField] private float _attackThreshold = 2.5f; // <<<--- IT'S DECLARED HERE

    // --- RUNTIME STATE ---
    private float _currentHealth;
    private bool _isDead = false;
    private float _timeSinceLastAttack = 0f;
    
    // --- Static list for AI Manager ---
    public static List<EnemyController> ActiveEnemies = new List<EnemyController>();
    
    // --- AI-Specific State Variables ---
    private IDamageable _currentTarget;    // Our *current* target (Player, Core, or Door)
    private Transform _coreTarget;          // The ultimate goal
    private Transform _lastKnownPlayerPos;  // For distractions
    private Transform _soundInvestigationPos; // For distractions
    private Transform _attackSource;        // For distractions
    private float _sensorCooldown = 0f;     // Timer for how often to check for player/doors
    // --- AI Stance ---
    public enum AIStance { Standard, Aggressive, Evasive }
    private AIStance _currentStance = AIStance.Standard;
    private float _originalMoveSpeed;
    private float _originalStoppingDistance;
    
    private float _spawnerTimeAlive = 0f;   // For Spawner
    private float _timeSinceLastGrowth = 0f; // For Spawner Growth
    private float _timeSinceLastSpawn;      // For Spawner
    private float _wanderTimer; // Timer for how often to pick a new wander point

    // --- Constants ---
    private const float SENSOR_UPDATE_RATE = 0.5f; // How often to scan for player/doors
    private const float WANDER_UPDATE_RATE = 3.0f; // How often Wander AI picks a new point
    private const float WANDER_DISTANCE = 10f; // How far Wander AI looks for a new point
    private const float ATTACK_RANGE_BUFFER = 1f; // Buffer to stop enemies from shuffling at attack range

    #region --- Unity Lifecycle & Initialization ---

    void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        // Ragdoll RBs are now assigned in the Inspector
    }

    void Start()
    {
        if (enemyData == null)
        {
            Debug.LogError("EnemyData is NULL! Cannot initialize enemy.", this);
            return;
        }

        // --- Find Global References ---
        if (_waveManager == null) _waveManager = FindObjectOfType<WaveManager>();
        if (_playerTarget == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) _playerTarget = playerObj.transform;
        }
        GameObject coreObj = GameObject.FindGameObjectWithTag("Core");
        if (coreObj != null)
        {
            _coreTarget = coreObj.transform;
        }
        else
        {
            Debug.LogError("AI created, but no 'Core' tag found in scene!", this);
        }

        // --- Initialize Stats ---
        _currentHealth = enemyData.baseHealth;
        _agent.speed = enemyData.moveSpeed;
        _agent.stoppingDistance = _attackThreshold - 0.5f; // Set stopping distance
        transform.localScale = Vector3.one * enemyData.baseScale; // Apply base scale
        _agent.enabled = true;
        SetRagdollActive(false);
        
        _originalMoveSpeed = enemyData.moveSpeed;
        _originalStoppingDistance = _agent.stoppingDistance;

        // Add to static list
        if (!ActiveEnemies.Contains(this))
        {
            ActiveEnemies.Add(this);
        }

        // --- Initialize AI Behavior ---
        // All enemies start by targeting the Core
        if (_coreTarget != null)
        {
            _currentTarget = _coreTarget.GetComponent<IDamageable>();
        }
        _state = EnemyState.Pursuing;
        _timeSinceLastAttack = enemyData.attackCooldown; // Ready to attack

        // 5. Subscribe to Audio Events (if distractible)
        if (enemyData.canBeDistracted)
        {
            // This is the line that needs the new script
            EnemyAIAudioEvents.OnGunshotReported += HandleGunshot;
        }

        // 6. Initialize Spawner (if applicable)
        if (enemyData.isSpawner)
        {
            _spawnerTimeAlive = enemyData.spawnerTimeLimit; // This is now a countdown
            _timeSinceLastSpawn = enemyData.spawnInterval;
        }
    }

    void OnDestroy()
    {
        // Unsubscribe from events to prevent errors
        if (enemyData != null && enemyData.canBeDistracted)
        {
            EnemyAIAudioEvents.OnGunshotReported -= HandleGunshot;
        }
        ActiveEnemies.Remove(this);
    }

    void Update()
    {
        if (_isDead || !_agent.enabled || !_agent.isOnNavMesh) return;

        // Update attack cooldown
        _timeSinceLastAttack += Time.deltaTime;
        _sensorCooldown -= Time.deltaTime;

        // --- Spawner (Queen) Logic ---
        if (enemyData.isSpawner)
        {
            UpdateSpawnerLogic();
        }

        // --- MASTER AI SWITCH ---
        RunAILogic();
    }

    #endregion

    #region --- AI Logic Methods ---

    private void RunAILogic()
    {
        // High-priority check: Is our current target gone?
        if ((_currentTarget as UnityEngine.Object) == null || _currentTarget.transform == null || !_currentTarget.transform.gameObject.activeInHierarchy)
        {
            // Target is gone. Reset to go back to the Core.
            _currentTarget = _coreTarget.GetComponent<IDamageable>();
            _state = EnemyState.Pursuing;
            if (_currentTarget == null) return; // Failsafe if Core is also gone
        }

        // Run sensor checks (vision, sound, doors) on a timer
        if (_sensorCooldown <= 0f)
        {
            UpdateSensors();
            _sensorCooldown = SENSOR_UPDATE_RATE;
        }

        // Execute logic based on current state
        if (_state == EnemyState.Pursuing)
        {
            UpdatePursueState();
        }
        else if (_state == EnemyState.Attacking)
        {
            UpdateAttackState();
        }
    }
    
    // Handles all target-finding (vision, sound, doors).
    // This sets the _currentTarget, which the Pursue state will follow.
    private void UpdateSensors()
    {
        // 1. Distraction Check (Player/Attacker)
        if (enemyData.canBeDistracted)
        {
            // A. Did we get shot? (Highest priority)
            if (_attackSource != null)
            {
                SetNewTarget(_attackSource.GetComponent<IDamageable>());
                _attackSource = null; // Clear trigger
                return; // Found a target
            }

            // B. Can we see the player?
            if (fov != null && _playerTarget != null && fov.IsTargetVisible(_playerTarget))
            {
                _lastKnownPlayerPos = _playerTarget; // Remember where we saw them
                SetNewTarget(_playerTarget.GetComponent<IDamageable>());
                return; // Found a target
            }
            
            // C. Did we hear a sound?
            if (_soundInvestigationPos != null)
            {
                // Note: We don't set a target, just a destination.
                if (_agent.destination != _soundInvestigationPos.position)
                {
                    _agent.SetDestination(_soundInvestigationPos.position);
                }
                _soundInvestigationPos = null; // Clear trigger
                return; // Found a target
            }

            // D. Are we investigating a sound or last known position?
            if (_lastKnownPlayerPos != null)
            {
                if (Vector3.Distance(transform.position, _lastKnownPlayerPos.position) < _agent.stoppingDistance + 1f)
                {
                    _lastKnownPlayerPos = null; // We "lost" them, go back to Core
                }
            }
        }

        // 2. Obstacle Check (Doors)
        if (_currentTarget.transform == _coreTarget)
        {
            if (CheckForDoorObstacle(out DoorHealth door))
            {
                SetNewTarget(door); // Found a new target (the Door)
                return;
            }
        }

        // 3. Default: Target the Core
        if (_currentTarget.transform != _playerTarget)
        {
             SetNewTarget(_coreTarget.GetComponent<IDamageable>());
        }
    }
    
    // Movement logic (Direct or Wander)
    private void UpdatePursueState()
    {
        _agent.isStopped = false;

        if (Vector3.Distance(transform.position, _currentTarget.transform.position) <= _agent.stoppingDistance)
        {
            _state = EnemyState.Attacking;
            return;
        }

        // --- NEW ADAPTIVE MOVEMENT LOGIC ---

        // 1. EVASIVE STANCE: Always use the "dodge" logic on a timer.
        if (_currentStance == AIStance.Evasive && !enemyData.isSpawner)
        {
            _wanderTimer -= Time.deltaTime;
            // Check if our "dodge" timer is up OR we've reached our last dodge point
            if (_wanderTimer <= 0f || _agent.remainingDistance < _agent.stoppingDistance + 1f)
            {
                Vector3 dodgePoint = GetEvasiveManeuverPoint(_currentTarget.transform.position);
                _agent.SetDestination(dodgePoint);
                _wanderTimer = WANDER_UPDATE_RATE; // WANDER_UPDATE_RATE acts as our "dodge interval"
            }
        }
        // 2. AGGRESSIVE STANCE or STANDARD-DIRECT: Always move directly to target.
        else if (_currentStance == AIStance.Aggressive || enemyData.movementType == EnemyData.AIMovementType.Direct)
        {
            _agent.SetDestination(_currentTarget.transform.position);
        }
        // 3. STANDARD-WANDER: Use the original wander logic.
        else if (enemyData.movementType == EnemyData.AIMovementType.Wander)
        {
            _wanderTimer -= Time.deltaTime;
            if (_wanderTimer <= 0f || _agent.remainingDistance < 1f)
            {
                // Call our renamed function
                Vector3 randomPoint = GetStandardWanderPoint(_currentTarget.transform.position);
                _agent.SetDestination(randomPoint);
                _wanderTimer = WANDER_UPDATE_RATE;
            }
        }
    }
    
    // Attack logic (for Player, Core, or Door)
    private void UpdateAttackState()
    {
        if (Vector3.Distance(transform.position, _currentTarget.transform.position) > _agent.stoppingDistance + ATTACK_RANGE_BUFFER)
        {
            _state = EnemyState.Pursuing;
            return;
        }

        _agent.isStopped = true;
        RotateTowards(_currentTarget.transform.position);

        if (_timeSinceLastAttack >= enemyData.attackCooldown)
        {
            TryAttack();
        }
    }

    // Queen-specific logic (Spawning & Growth)
    private void UpdateSpawnerLogic()
    {
        if (enemyData.canGrow)
        {
            _timeSinceLastGrowth += Time.deltaTime;
            if (_timeSinceLastGrowth >= enemyData.growthInterval)
            {
                Grow();
                _timeSinceLastGrowth = 0f;
            }
        }

        if (enemyData.spawnerTimeLimit > 0)
        {
            _spawnerTimeAlive -= Time.deltaTime;
            if (_spawnerTimeAlive <= 0f) return;
        }

        _timeSinceLastSpawn += Time.deltaTime;
        if (_timeSinceLastSpawn >= enemyData.spawnInterval)
        {
            SpawnMinion();
            _timeSinceLastSpawn = 0f;
        }
    }
    
    #endregion

    #region --- AI Helper & State Methods ---
    
    public void SetAIStance(AIStance newStance)
    {
        if (_currentStance == newStance) return; // No change

        _currentStance = newStance;

        switch (newStance)
        {
            case AIStance.Aggressive:
                // --- This is your "AggressiveApproach" ---
                _agent.speed = _originalMoveSpeed * 1.5f; // 50% faster
                _agent.stoppingDistance = _originalStoppingDistance * 0.75f; // Get a bit closer
                break;
                
            case AIStance.Evasive:
                // --- This is your "EvasiveManeuvers" ---
                // We no longer slow them down. We just use their normal stats,
                // but the UpdatePursueState logic will use a new "dodge" behavior.
                _agent.speed = _originalMoveSpeed;
                _agent.stoppingDistance = _originalStoppingDistance;
                break;

            case AIStance.Standard:
            default:
                _agent.speed = _originalMoveSpeed;
                _agent.stoppingDistance = _originalStoppingDistance;
                break;
        }
    }

    void TryAttack()
    {
        if (_currentTarget == null) return;
        
        if (_timeSinceLastAttack >= enemyData.attackCooldown)
        {
            _currentTarget.TakeDamage(enemyData.attackDamage);
            Debug.Log($"{gameObject.name} attacks {_currentTarget.transform.name} for {enemyData.attackDamage} damage!");
            _timeSinceLastAttack = 0f;
        }
    }

    private bool CheckForDoorObstacle(out DoorHealth door)
    {
        if (_agent.velocity.magnitude < 0.1f && !_agent.pathPending && _agent.remainingDistance > _agent.stoppingDistance)
        {
            Vector3 scanPos = transform.position + transform.forward * (_agent.stoppingDistance - 0.5f);
            Collider[] hits = Physics.OverlapBox(scanPos, new Vector3(2f, 2f, 2f), transform.rotation, obstacleLayer);

            foreach (Collider hit in hits)
            {
                if (hit.CompareTag("Door"))
                {
                    if (hit.TryGetComponent<DoorHealth>(out door))
                    {
                        return true;
                    }
                }
            }
        }
        
        door = null;
        return false;
    }

    private Vector3 GetStandardWanderPoint(Vector3 targetPosition)
    {
        Vector3 dirToTarget = (targetPosition - transform.position).normalized;
        Vector3 randomDir = new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f)).normalized;
        Vector3 wanderDir = (dirToTarget * 0.7f + randomDir * 0.3f).normalized;
        Vector3 targetPoint = transform.position + wanderDir * WANDER_DISTANCE;

        if (NavMesh.SamplePosition(targetPoint, out NavMeshHit hit, 5f, _agent.areaMask))
        {
            return hit.position;
        }
        
        return targetPosition;
    }
    
    private Vector3 GetEvasiveManeuverPoint(Vector3 targetPosition)
    {
        // 1. Get direction to target
        Vector3 dirToTarget = (targetPosition - transform.position).normalized;
        
        // 2. Get a perpendicular side direction (left or right)
        Vector3 sideDir = Vector3.Cross(dirToTarget, Vector3.up).normalized;
        sideDir *= (Random.Range(0, 2) * 2 - 1); // Randomly -1 or 1

        // 3. Define the dodge "jerk"
        float dodgeSideDistance = 5f;  // How far to the side
        float dodgeForwardDistance = 4f; // How far forward
        
        // 4. Calculate the target point
        Vector3 targetPoint = transform.position + (sideDir * dodgeSideDistance) + (dirToTarget * dodgeForwardDistance);

        // 5. Find the closest valid point on the NavMesh
        if (NavMesh.SamplePosition(targetPoint, out NavMeshHit hit, 5f, _agent.areaMask))
        {
            return hit.position;
        }
        
        // Failsafe: just move towards the target if no valid dodge point is found
        return targetPosition;
    }
    
    void Grow()
    {
        if (transform.localScale.x >= enemyData.maxGrowthSize)
        {
            return;
        }

        float newScale = Mathf.Min(transform.localScale.x * enemyData.growthRate, enemyData.maxGrowthSize);
        transform.localScale = new Vector3(newScale, newScale, newScale);
    }

    void SpawnMinion()
    {
        if (enemyData.spawnPrefabs == null || enemyData.spawnPrefabs.Count == 0) return;

        Debug.Log("Queen is spawning a minion!");
        
        Vector3 randomPos = transform.position + (Random.insideUnitSphere * 3f);
        randomPos.y = transform.position.y;
        
        GameObject prefabToSpawn = enemyData.spawnPrefabs[Random.Range(0, enemyData.spawnPrefabs.Count)];
        
        Instantiate(prefabToSpawn, randomPos, Quaternion.identity);
    }

    #endregion

    #region --- Event Handlers (Hearing, Damage) ---

    void HandleGunshot(Vector3 soundPosition)
    {
        if (_isDead || !enemyData.canBeDistracted) return;

        float distance = Vector3.Distance(transform.position, soundPosition);
        if (distance <= enemyData.hearingRange)
        {
            if (_currentTarget.transform != _playerTarget)
            {
                GameObject soundPosObj = new GameObject($"SoundInvestigate_@{soundPosition}");
                soundPosObj.transform.position = soundPosition;
                _soundInvestigationPos = soundPosObj.transform;
                _lastKnownPlayerPos = null;
                Destroy(soundPosObj, 5f);
            }
        }
    }
    
    public void TakeDamage(float amount, Transform attacker)
    {
        if (_isDead) return;
        
        _currentHealth -= amount;

        if (enemyData.canBeDistracted)
        {
            _attackSource = attacker;
        }

        if (_currentHealth <= 0f)
        {
            Die(true);
        }
    }
    
    public void TakeExplosion(Vector3 explosionPosition, float explosionForce, float explosionRadius, float upwardModifier = 0.1f)
    {
        if (_isDead) return;

        _currentHealth = 0;
        
        Die(true);
        
        foreach (Rigidbody rb in _ragdollRigidbodies)
        {
            if (rb != null)
            {
                rb.AddExplosionForce(explosionForce, explosionPosition, explosionRadius, upwardModifier, ForceMode.Impulse);
            }
        }
    }

    void Die(bool useRagdoll)
    {
        if (_isDead) return;
        _isDead = true;
        
        if (_waveManager != null)
        {
            _waveManager.EnemyDestroyed();
        }

        if (enemyData != null && enemyData.scrapMetalPrefab != null)
        {
            Instantiate(enemyData.scrapMetalPrefab, transform.position, Quaternion.identity);
        }
        
        if (_agent != null && _agent.enabled) 
        {
            _agent.enabled = false;
        }
        
        if (useRagdoll && _ragdollRigidbodies != null && _ragdollRigidbodies.Length > 0)
        {
            ActivateRagdoll();
            Destroy(gameObject, 5f);
        }
        else
        {
            Destroy(gameObject, 0.1f);
        }
    }

    #endregion

    #region --- Utility Methods ---
    
    private void SetNewTarget(IDamageable newTarget)
    {
        if (newTarget == null || newTarget == _currentTarget) return; 

        _currentTarget = newTarget;
        _state = EnemyState.Pursuing;
    }

    private void ActivateRagdoll()
    {
        _isDead = true;
        SetRagdollActive(true);
        Collider mainCollider = GetComponent<Collider>();
        if (mainCollider != null) { mainCollider.enabled = false; }
    }

    private void SetRagdollActive(bool isActive)
    {
        if (_ragdollRigidbodies == null || _ragdollRigidbodies.Length == 0) return;
        foreach (Rigidbody rb in _ragdollRigidbodies)
        {
            if (rb != null)
            {
                rb.isKinematic = !isActive;
            }
        }
    }

    private void RotateTowards(Vector3 targetPosition)
    {
        Vector3 direction = (targetPosition - transform.position).normalized;
        direction.y = 0;
        if (direction != Vector3.zero)
        {
            Quaternion lookRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * 5f);
        }
    }
    
    #endregion
}

