using System;
using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyController : MonoBehaviour, IDamageable
{
    // --- AI States ---
    private enum EnemyState { Pursuing, Attacking, Investigating }
    private EnemyState _state;

    // --- SETUP & DATA ---
    [Header("Data & Manager References")]
    [Tooltip("The Scriptable Object containing all stats for this enemy type.")]
    public EnemyData enemyData;
    [Tooltip("The central manager for wave progression and enemy count. Assigned automatically when spawned by the WaveManager.")]
    public WaveManager _waveManager;
    [Tooltip("The player, which will be found by tag if left empty.")]
    public Transform _playerTarget;
    [Tooltip("The Field of View component for visual detection.")]
    public FieldOfView fov;
    [Tooltip("All rigidbodies for the ragdoll.")]
    public Rigidbody[] _ragdollRigidbodies;
    [Tooltip("The LayerMask that only contains 'Door' objects.")]
    public LayerMask obstacleLayer;

    [Header("AI & Navigation")]
    [SerializeField] private NavMeshAgent _agent;
    [SerializeField] private float _attackThreshold = 2.5f;

    [Header("Feedback")]
    [Tooltip("Briefly tint the renderers when hit.")]
    [SerializeField] private bool _enableHitFlash = true;
    [SerializeField] private Color _hitFlashColor = new Color(1f, 0.3f, 0.3f, 1f);
    [SerializeField] private float _hitFlashDuration = 0.08f;

    // --- RUNTIME STATE ---
    private float _maxHealth;
    private float _currentHealth;
    private bool _isDead = false;
    private bool _initialized = false;
    private float _timeSinceLastAttack = 0f;

    // --- Static registry (used by towers, the adaptive AI and the wave manager) ---
    public static readonly List<EnemyController> ActiveEnemies = new List<EnemyController>();

    /// <summary>Fired when any enemy is killed. Subscribers MUST unsubscribe in OnDestroy.</summary>
    public static event Action<EnemyController> OnAnyEnemyKilled;
    /// <summary>Fired when any enemy takes damage (enemy, amount, killingBlow).</summary>
    public static event Action<EnemyController, float, bool> OnAnyEnemyDamaged;

    public float CurrentHealth => _currentHealth;
    public float MaxHealth => _maxHealth;
    public bool IsDead => _isDead;
    public float SpawnTime { get; private set; }

    // --- AI-Specific State Variables ---
    private IDamageable _currentTarget;      // Player, Core, or Door
    private Transform _currentTargetTransform;
    private Transform _coreTarget;
    private IDamageable _coreDamageable;
    private Vector3 _lastKnownPlayerPos;
    private bool _hasLastKnownPlayerPos;
    private float _timeSincePlayerSeen;
    private Vector3 _soundInvestigationPos;
    private bool _hasSoundToInvestigate;
    private float _investigateWaitTimer;
    private Transform _attackSource;
    private float _sensorCooldown = 0f;
    private Vector3 _lastDestination;
    private float _repathTimer;

    // --- AI Stance ---
    public enum AIStance { Standard, Aggressive, Evasive }
    private AIStance _currentStance = AIStance.Standard;
    private float _originalMoveSpeed;
    private float _originalStoppingDistance;

    private float _spawnerTimeAlive = 0f;
    private float _timeSinceLastGrowth = 0f;
    private float _timeSinceLastSpawn;
    private float _wanderTimer;

    // --- Hit flash ---
    private Renderer[] _renderers;
    private MaterialPropertyBlock _mpb;
    private float _flashTimer;
    private bool _isFlashing;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    // --- Constants ---
    private const float SENSOR_UPDATE_RATE = 0.5f;
    private const float WANDER_UPDATE_RATE = 3.0f;
    private const float WANDER_DISTANCE = 10f;
    private const float ATTACK_RANGE_BUFFER = 1f;
    private const float REPATH_INTERVAL = 0.25f;          // How often a moving target is re-pathed
    private const float REPATH_DISTANCE_SQR = 0.5f * 0.5f; // Target must move this far before we re-path
    private static readonly Collider[] s_doorScanBuffer = new Collider[8];

    #region --- Unity Lifecycle & Initialization ---

    void Awake()
    {
        if (_agent == null) _agent = GetComponent<NavMeshAgent>();
        _renderers = GetComponentsInChildren<Renderer>(true);
        _mpb = new MaterialPropertyBlock();
    }

    /// <summary>
    /// Called by whoever spawns this enemy (WaveManager / Queen). Safe to call before Start().
    /// </summary>
    public void Initialize(EnemyData data, WaveManager waveManager, float healthMultiplier = 1f)
    {
        if (data != null) enemyData = data;
        if (waveManager != null) _waveManager = waveManager;

        if (enemyData == null)
        {
            Debug.LogError("EnemyData is NULL! Cannot initialize enemy.", this);
            return;
        }
        if (_initialized) return;
        _initialized = true;

        SpawnTime = Time.time;

        // --- Find Global References ---
        if (_waveManager == null) _waveManager = WaveManager.Instance != null ? WaveManager.Instance : FindFirstObjectByType<WaveManager>();
        if (_playerTarget == null)
        {
            if (PlayerStats.Instance != null) _playerTarget = PlayerStats.Instance.transform;
            else
            {
                GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
                if (playerObj != null) _playerTarget = playerObj.transform;
            }
        }

        if (CoreHealth.Instance != null)
        {
            _coreTarget = CoreHealth.Instance.transform;
            _coreDamageable = CoreHealth.Instance;
        }
        else
        {
            GameObject coreObj = GameObject.FindGameObjectWithTag("Core");
            if (coreObj != null)
            {
                _coreTarget = coreObj.transform;
                _coreDamageable = coreObj.GetComponent<IDamageable>();
            }
        }
        if (_coreTarget == null) Debug.LogError("AI created, but no 'Core' tag found in scene!", this);

        // --- Initialize Stats ---
        _maxHealth = enemyData.baseHealth * Mathf.Max(0.01f, healthMultiplier);
        _currentHealth = _maxHealth;
        _agent.speed = enemyData.moveSpeed;
        _agent.stoppingDistance = Mathf.Max(0.1f, _attackThreshold - 0.5f);
        transform.localScale = Vector3.one * enemyData.baseScale;
        _agent.enabled = true;
        if (!_agent.isOnNavMesh && NavMesh.SamplePosition(transform.position, out NavMeshHit navHit, 5f, NavMesh.AllAreas))
        {
            // Spawn point was slightly off the NavMesh; snap to it instead of standing still forever.
            _agent.Warp(navHit.position);
        }
        SetRagdollActive(false);

        _originalMoveSpeed = enemyData.moveSpeed;
        _originalStoppingDistance = _agent.stoppingDistance;

        if (!ActiveEnemies.Contains(this)) ActiveEnemies.Add(this);
        if (_waveManager != null) _waveManager.RegisterEnemy(this);

        // --- Initialize AI Behavior: everyone starts by targeting the Core ---
        SetNewTarget(_coreDamageable);
        _state = EnemyState.Pursuing;
        _timeSinceLastAttack = enemyData.attackCooldown; // Ready to attack
        _lastDestination = new Vector3(float.NaN, 0, 0);

        if (enemyData.canBeDistracted)
        {
            EnemyAIAudioEvents.OnGunshotReported += HandleGunshot;
        }

        if (enemyData.isSpawner)
        {
            _spawnerTimeAlive = enemyData.spawnerTimeLimit;
            _timeSinceLastSpawn = enemyData.spawnInterval;
        }
    }

    void Start()
    {
        // Scene-placed enemies (or ones spawned without Initialize) set themselves up here.
        if (!_initialized) Initialize(enemyData, _waveManager);
    }

    void OnDestroy()
    {
        if (enemyData != null && enemyData.canBeDistracted)
        {
            EnemyAIAudioEvents.OnGunshotReported -= HandleGunshot;
        }
        ActiveEnemies.Remove(this);

        // If we're destroyed without dying (scene unload, debug delete) keep the wave count honest.
        if (!_isDead && _waveManager != null) _waveManager.EnemyDestroyed();
    }

    void Update()
    {
        if (_isFlashing) UpdateHitFlash();
        if (_isDead || !_initialized || !_agent.enabled || !_agent.isOnNavMesh) return;

        float dt = Time.deltaTime;
        _timeSinceLastAttack += dt;
        _sensorCooldown -= dt;
        _repathTimer -= dt;

        if (enemyData.isSpawner) UpdateSpawnerLogic(dt);

        RunAILogic(dt);
    }

    #endregion

    #region --- AI Logic Methods ---

    private void RunAILogic(float dt)
    {
        // High-priority check: Is our current target gone?
        if (!IsTargetValid(_currentTarget))
        {
            SetNewTarget(_coreDamageable);
            _state = EnemyState.Pursuing;
            if (_currentTarget == null) return; // Core is gone too; nothing to do.
        }

        if (_sensorCooldown <= 0f)
        {
            UpdateSensors();
            _sensorCooldown = SENSOR_UPDATE_RATE;
        }

        switch (_state)
        {
            case EnemyState.Pursuing:     UpdatePursueState(dt); break;
            case EnemyState.Attacking:    UpdateAttackState(); break;
            case EnemyState.Investigating: UpdateInvestigateState(dt); break;
        }
    }

    private static bool IsTargetValid(IDamageable target)
    {
        if (target == null) return false;
        var obj = target as UnityEngine.Object;
        if (obj == null) return false; // destroyed
        Transform t = target.transform;
        return t != null && t.gameObject.activeInHierarchy;
    }

    // Handles all target-finding (vision, sound, doors).
    private void UpdateSensors()
    {
        if (enemyData.canBeDistracted)
        {
            // A. Did we get shot? (Highest priority)
            if (_attackSource != null)
            {
                IDamageable attacker = _attackSource.GetComponent<IDamageable>();
                _attackSource = null;
                if (attacker != null)
                {
                    SetNewTarget(attacker);
                    _hasLastKnownPlayerPos = true;
                    _lastKnownPlayerPos = attacker.transform.position;
                    _timeSincePlayerSeen = 0f;
                    return;
                }
            }

            // B. Can we see the player?
            bool playerVisible = fov != null && _playerTarget != null && fov.IsTargetVisible(_playerTarget);
            if (playerVisible)
            {
                _lastKnownPlayerPos = _playerTarget.position;
                _hasLastKnownPlayerPos = true;
                _timeSincePlayerSeen = 0f;
                if (_currentTargetTransform != _playerTarget)
                {
                    IDamageable playerDamageable = _playerTarget.GetComponent<IDamageable>();
                    if (playerDamageable != null) SetNewTarget(playerDamageable);
                }
                return;
            }

            // C. Chasing the player but can't see them: go to the last known position, then give up.
            if (_currentTargetTransform == _playerTarget && _playerTarget != null)
            {
                _timeSincePlayerSeen += SENSOR_UPDATE_RATE;
                bool reachedLastKnown = _hasLastKnownPlayerPos &&
                    (transform.position - _lastKnownPlayerPos).sqrMagnitude < (_agent.stoppingDistance + 1f) * (_agent.stoppingDistance + 1f);
                if (reachedLastKnown || _timeSincePlayerSeen >= enemyData.loseInterestTime)
                {
                    _hasLastKnownPlayerPos = false;
                    SetNewTarget(_coreDamageable);
                }
                else
                {
                    return; // keep heading to last known pos (handled by UpdatePursueState)
                }
            }

            // D. Did we hear a sound? Investigate it (only if not busy with the player).
            if (_hasSoundToInvestigate)
            {
                _hasSoundToInvestigate = false;
                _state = EnemyState.Investigating;
                _investigateWaitTimer = 0f;
                MoveTo(_soundInvestigationPos, true);
                return;
            }
        }

        // 2. Obstacle Check (Doors) - only while heading to the Core.
        if (_currentTargetTransform == _coreTarget && _state == EnemyState.Pursuing)
        {
            if (CheckForDoorObstacle(out DoorHealth door))
            {
                SetNewTarget(door);
            }
        }
    }

    private void UpdatePursueState(float dt)
    {
        _agent.isStopped = false;

        // If we're chasing the player but lost sight, head for the last known position instead of the live one.
        Vector3 goal = (_currentTargetTransform == _playerTarget && _hasLastKnownPlayerPos && _timeSincePlayerSeen > 0f)
            ? _lastKnownPlayerPos
            : _currentTargetTransform.position;

        float stopDist = _agent.stoppingDistance;
        if ((transform.position - _currentTargetTransform.position).sqrMagnitude <= stopDist * stopDist)
        {
            _state = EnemyState.Attacking;
            return;
        }

        // 1. EVASIVE STANCE: periodic side-step "dodge" points.
        if (_currentStance == AIStance.Evasive && !enemyData.isSpawner)
        {
            _wanderTimer -= dt;
            if (_wanderTimer <= 0f || (!_agent.pathPending && _agent.remainingDistance < stopDist + 1f))
            {
                MoveTo(GetEvasiveManeuverPoint(goal), true);
                _wanderTimer = WANDER_UPDATE_RATE;
            }
        }
        // 2. AGGRESSIVE STANCE or STANDARD-DIRECT: move straight at the target (throttled re-path).
        else if (_currentStance == AIStance.Aggressive || enemyData.movementType == EnemyData.AIMovementType.Direct)
        {
            MoveTo(goal, false);
        }
        // 3. STANDARD-WANDER: wavy approach.
        else
        {
            _wanderTimer -= dt;
            if (_wanderTimer <= 0f || (!_agent.pathPending && _agent.remainingDistance < 1f))
            {
                MoveTo(GetStandardWanderPoint(goal), true);
                _wanderTimer = WANDER_UPDATE_RATE;
            }
        }
    }

    private void UpdateAttackState()
    {
        float maxRange = _agent.stoppingDistance + ATTACK_RANGE_BUFFER;
        if ((transform.position - _currentTargetTransform.position).sqrMagnitude > maxRange * maxRange)
        {
            _state = EnemyState.Pursuing;
            return;
        }

        _agent.isStopped = true;
        RotateTowards(_currentTargetTransform.position);

        if (_timeSinceLastAttack >= enemyData.attackCooldown)
        {
            TryAttack();
        }
    }

    private void UpdateInvestigateState(float dt)
    {
        _agent.isStopped = false;

        if (!_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance + 0.5f)
        {
            _investigateWaitTimer += dt;
            if (_investigateWaitTimer >= enemyData.patrolWaitTime)
            {
                SetNewTarget(_coreDamageable);
                _state = EnemyState.Pursuing;
            }
        }
    }

    private void UpdateSpawnerLogic(float dt)
    {
        if (enemyData.canGrow)
        {
            _timeSinceLastGrowth += dt;
            if (_timeSinceLastGrowth >= enemyData.growthInterval)
            {
                Grow();
                _timeSinceLastGrowth = 0f;
            }
        }

        if (enemyData.spawnerTimeLimit > 0)
        {
            _spawnerTimeAlive -= dt;
            if (_spawnerTimeAlive <= 0f) return;
        }

        _timeSinceLastSpawn += dt;
        if (_timeSinceLastSpawn >= enemyData.spawnInterval)
        {
            SpawnMinion();
            _timeSinceLastSpawn = 0f;
        }
    }

    #endregion

    #region --- AI Helper & State Methods ---

    /// <summary>
    /// Sets the agent destination, but only re-paths when the goal actually moved (or on a short timer).
    /// Calling SetDestination every frame for every enemy was the single biggest CPU cost.
    /// </summary>
    private void MoveTo(Vector3 destination, bool force)
    {
        if (!force)
        {
            bool moved = float.IsNaN(_lastDestination.x) || (destination - _lastDestination).sqrMagnitude > REPATH_DISTANCE_SQR;
            if (!moved || (_repathTimer > 0f && _agent.hasPath)) return;
        }
        _lastDestination = destination;
        _repathTimer = REPATH_INTERVAL;
        _agent.SetDestination(destination);
    }

    public void SetAIStance(AIStance newStance)
    {
        if (_currentStance == newStance || _agent == null) return;
        _currentStance = newStance;

        switch (newStance)
        {
            case AIStance.Aggressive:
                _agent.speed = _originalMoveSpeed * 1.5f;
                _agent.stoppingDistance = _originalStoppingDistance * 0.75f;
                break;
            case AIStance.Evasive:
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
        _currentTarget.TakeDamage(enemyData.attackDamage);
        _timeSinceLastAttack = 0f;
    }

    private bool CheckForDoorObstacle(out DoorHealth door)
    {
        door = null;
        if (obstacleLayer.value == 0) return false;

        // Only scan when we appear to be stuck short of the goal.
        if (_agent.velocity.sqrMagnitude < 0.01f && !_agent.pathPending && _agent.remainingDistance > _agent.stoppingDistance)
        {
            Vector3 scanPos = transform.position + transform.forward * Mathf.Max(0.5f, _agent.stoppingDistance);
            int count = Physics.OverlapBoxNonAlloc(scanPos, new Vector3(2f, 2f, 2f), s_doorScanBuffer, transform.rotation, obstacleLayer);
            for (int i = 0; i < count; i++)
            {
                Collider hit = s_doorScanBuffer[i];
                if (hit.CompareTag("Door") && hit.TryGetComponent(out door))
                {
                    return true;
                }
                DoorHealth parentDoor = hit.GetComponentInParent<DoorHealth>();
                if (parentDoor != null)
                {
                    door = parentDoor;
                    return true;
                }
            }
        }
        return false;
    }

    private Vector3 GetStandardWanderPoint(Vector3 targetPosition)
    {
        Vector3 dirToTarget = (targetPosition - transform.position).normalized;
        Vector3 randomDir = new Vector3(UnityEngine.Random.Range(-1f, 1f), 0, UnityEngine.Random.Range(-1f, 1f)).normalized;
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
        Vector3 dirToTarget = (targetPosition - transform.position).normalized;
        Vector3 sideDir = Vector3.Cross(dirToTarget, Vector3.up).normalized;
        sideDir *= (UnityEngine.Random.Range(0, 2) * 2 - 1); // -1 or 1

        const float dodgeSideDistance = 5f;
        const float dodgeForwardDistance = 4f;
        Vector3 targetPoint = transform.position + (sideDir * dodgeSideDistance) + (dirToTarget * dodgeForwardDistance);

        if (NavMesh.SamplePosition(targetPoint, out NavMeshHit hit, 5f, _agent.areaMask))
        {
            return hit.position;
        }
        return targetPosition;
    }

    void Grow()
    {
        if (transform.localScale.x >= enemyData.maxGrowthSize) return;
        float newScale = Mathf.Min(transform.localScale.x * enemyData.growthRate, enemyData.maxGrowthSize);
        transform.localScale = new Vector3(newScale, newScale, newScale);
    }

    void SpawnMinion()
    {
        if (enemyData.spawnPrefabs == null || enemyData.spawnPrefabs.Count == 0) return;

        GameObject prefabToSpawn = enemyData.spawnPrefabs[UnityEngine.Random.Range(0, enemyData.spawnPrefabs.Count)];
        if (prefabToSpawn == null) return;

        Vector3 randomPos = transform.position + (UnityEngine.Random.insideUnitSphere * 3f);
        randomPos.y = transform.position.y;
        if (NavMesh.SamplePosition(randomPos, out NavMeshHit hit, 3f, _agent.areaMask))
        {
            randomPos = hit.position;
        }

        GameObject minion = Instantiate(prefabToSpawn, randomPos, Quaternion.identity);
        EnemyController minionController = minion.GetComponent<EnemyController>();
        if (minionController != null)
        {
            // Registers the minion with the WaveManager so the wave can't end before it is dead.
            float multiplier = _maxHealth > 0f && enemyData.baseHealth > 0f ? _maxHealth / enemyData.baseHealth : 1f;
            minionController.Initialize(minionController.enemyData, _waveManager, multiplier);
        }
    }

    #endregion

    #region --- Event Handlers (Hearing, Damage) ---

    void HandleGunshot(Vector3 soundPosition)
    {
        if (_isDead || !enemyData.canBeDistracted) return;
        if (_currentTargetTransform == _playerTarget) return; // Already on the player.

        float range = enemyData.hearingRange;
        if ((transform.position - soundPosition).sqrMagnitude <= range * range)
        {
            _soundInvestigationPos = soundPosition;
            _hasSoundToInvestigate = true;
        }
    }

    /// <summary>IDamageable entry point (towers, mines, etc.). No attacker info.</summary>
    public void TakeDamage(float amount)
    {
        TakeDamage(amount, null);
    }

    public void TakeDamage(float amount, Transform attacker)
    {
        if (_isDead || amount <= 0f) return;

        _currentHealth -= amount;
        bool killingBlow = _currentHealth <= 0f;

        if (attacker != null && enemyData.canBeDistracted)
        {
            _attackSource = attacker;
            _sensorCooldown = 0f; // React immediately.
        }

        if (!killingBlow)
        {
            if (enemyData.hitSound != null) AudioSource.PlayClipAtPoint(enemyData.hitSound, transform.position);
            StartHitFlash();
        }

        OnAnyEnemyDamaged?.Invoke(this, amount, killingBlow);

        if (killingBlow)
        {
            _currentHealth = 0f;
            Die(true);
        }
    }

    public void TakeExplosion(Vector3 explosionPosition, float explosionForce, float explosionRadius, float upwardModifier = 0.1f)
    {
        if (_isDead) return;

        _currentHealth = 0;
        OnAnyEnemyDamaged?.Invoke(this, _maxHealth, true);
        Die(true);

        if (_ragdollRigidbodies == null) return;
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

        if (_waveManager != null) _waveManager.EnemyDestroyed();
        ActiveEnemies.Remove(this);

        if (enemyData != null)
        {
            if (enemyData.scrapMetalPrefab != null)
            {
                int count = Mathf.Max(1, enemyData.scrapDropCount);
                for (int i = 0; i < count; i++)
                {
                    Vector3 offset = count > 1 ? UnityEngine.Random.insideUnitSphere * 0.75f : Vector3.zero;
                    offset.y = 0.25f;
                    Instantiate(enemyData.scrapMetalPrefab, transform.position + offset, Quaternion.identity);
                }
            }
            if (enemyData.deathEffect != null)
            {
                GameObject fx = Instantiate(enemyData.deathEffect, transform.position, Quaternion.identity);
                Destroy(fx, 5f);
            }
            if (enemyData.deathSound != null) AudioSource.PlayClipAtPoint(enemyData.deathSound, transform.position);
        }

        if (_agent != null && _agent.enabled)
        {
            _agent.enabled = false;
        }

        OnAnyEnemyKilled?.Invoke(this);

        if (useRagdoll && _ragdollRigidbodies != null && _ragdollRigidbodies.Length > 0)
        {
            ActivateRagdoll();
            Destroy(gameObject, enemyData != null ? enemyData.corpseLifetime : 5f);
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
        if (newTarget == null) return;
        if (newTarget == _currentTarget) return;

        _currentTarget = newTarget;
        _currentTargetTransform = newTarget.transform;
        _state = EnemyState.Pursuing;
        _lastDestination = new Vector3(float.NaN, 0, 0); // Force a fresh path.
        _repathTimer = 0f;
    }

    private void ActivateRagdoll()
    {
        _isDead = true;
        SetRagdollActive(true);
        Collider mainCollider = GetComponent<Collider>();
        if (mainCollider != null) mainCollider.enabled = false;
    }

    private void SetRagdollActive(bool isActive)
    {
        if (_ragdollRigidbodies == null || _ragdollRigidbodies.Length == 0) return;
        foreach (Rigidbody rb in _ragdollRigidbodies)
        {
            if (rb != null) rb.isKinematic = !isActive;
        }
    }

    private void RotateTowards(Vector3 targetPosition)
    {
        Vector3 direction = targetPosition - transform.position;
        direction.y = 0;
        if (direction.sqrMagnitude > 0.0001f)
        {
            Quaternion lookRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * 5f);
        }
    }

    private void StartHitFlash()
    {
        if (!_enableHitFlash || _renderers == null || _renderers.Length == 0) return;
        _flashTimer = _hitFlashDuration;
        if (_isFlashing) return;
        _isFlashing = true;
        ApplyFlashColor(true);
    }

    private void UpdateHitFlash()
    {
        _flashTimer -= Time.deltaTime;
        if (_flashTimer <= 0f)
        {
            _isFlashing = false;
            ApplyFlashColor(false);
        }
    }

    private void ApplyFlashColor(bool on)
    {
        for (int i = 0; i < _renderers.Length; i++)
        {
            Renderer r = _renderers[i];
            if (r == null) continue;
            if (on)
            {
                _mpb.Clear();
                _mpb.SetColor(BaseColorId, _hitFlashColor);
                _mpb.SetColor(ColorId, _hitFlashColor);
                r.SetPropertyBlock(_mpb);
            }
            else
            {
                r.SetPropertyBlock(null);
            }
        }
    }

    #endregion
}
