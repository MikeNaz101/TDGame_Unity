using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyController : MonoBehaviour, IDamageable
{
    // --- AI States ---
    private enum EnemyState { Pursuing, Attacking, Ragdolled, SeekingCover, Dodging }
    private EnemyState _state;

    // --- EXISTING DATA ---
    [Header("Data & Manager References")]
    public EnemyData enemyData;
    public WaveManager _waveManager;
    public Transform _playerTarget;
    public FieldOfView fov;
    public LayerMask obstacleLayer;
    
    [Header("Fall Kill Settings")]
    public float fallKillThreshold = -20f;

    [Header("Ragdoll")]
    public Rigidbody pelvisRigidbody;
    public Rigidbody[] _ragdollRigidbodies;
    public float ragdollMinTime = 0.5f;

    [Header("AI & Navigation")]
    [SerializeField] private NavMeshAgent _agent;
    [SerializeField] private float _attackThreshold = 2.5f;
    [Tooltip("How often to check if the path to the core is blocked.")]
    [SerializeField] private float _pathCheckInterval = 1.0f;

    // --- NEW: Rank & Enhancements ---
    [Header("Military Rank Enhancements")]
    [SerializeField] private MilitaryRank _currentRankLevel = MilitaryRank.None;
    [SerializeField] private SkinnedMeshRenderer _meshRenderer; // For Ghost Mode transparency
    
    // Buff State Tracking
    private bool _hasBuffedHealth = false;
    private float _regenTimer = 0f;
    private float _dodgeCooldown = 0f;
    private float _coverCooldown = 0f;
    private bool _isGhostMode = false;

    // --- RUNTIME STATE ---
    private float _currentHealth;
    private bool _isDead = false;
    private float _timeSinceLastAttack = 0f;
    private Collider _mainCollider;
    private Animator _animator;
    private Rigidbody _mainRigidbody;
    private bool _isWeakened = false;
    
    public float CurrentHealth => _currentHealth;
    public static List<EnemyController> ActiveEnemies = new List<EnemyController>();
    
    private IDamageable _currentTarget;
    private Transform _coreTarget;
    private Transform _lastKnownPlayerPos;
    private Transform _soundInvestigationPos;
    private Transform _attackSource;
    private float _sensorCooldown = 0f;
    private float _pathCheckTimer = 0f;
    
    // Stance / Movement defaults
    private float _originalMoveSpeed;
    private float _originalStoppingDistance;
    
    // Spawner/Queen Logic
    private float _spawnerTimeAlive = 0f;
    private float _timeSinceLastGrowth = 0f;
    private float _timeSinceLastSpawn;

    private const float SENSOR_UPDATE_RATE = 0.5f;

    #region --- Unity Lifecycle ---

    void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        _mainCollider = GetComponent<Collider>();
        _animator = GetComponentInChildren<Animator>();
        _mainRigidbody = GetComponent<Rigidbody>();
        if(_meshRenderer == null) _meshRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
    }

    void Start()
    {
        if (enemyData == null) { Debug.LogError("EnemyData is NULL!", this); return; }
        if (_waveManager == null) _waveManager = FindObjectOfType<WaveManager>();
        
        // Find Player
        if (_playerTarget == null) { GameObject p = GameObject.FindGameObjectWithTag("Player"); if (p != null) _playerTarget = p.transform; }
        
        // Find Core
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
    }

    void OnDestroy()
    {
        if (enemyData != null && enemyData.canBeDistracted) EnemyAIAudioEvents.OnGunshotReported -= HandleGunshot;
        ActiveEnemies.Remove(this);
    }

    void Update()
    {
        if (_isDead) return;
        
        // --- NEW: FALL CHECK ---
        if (transform.position.y < fallKillThreshold)
        {
            HandleFallDeath();
            return;
        }
        
        _timeSinceLastAttack += Time.deltaTime;
        _sensorCooldown -= Time.deltaTime;
        
        // Spawner Logic
        if (enemyData.isSpawner) UpdateSpawnerLogic();

        // --- RANK LOGIC UPDATES ---
        HandleRankAbilities();
        
        RunAILogic();
    }

    #endregion

    #region --- Rank & Promotion Logic ---

    /// <summary>
    /// Called by a Commander unit to unlock abilities on this unit.
    /// </summary>
    public void ReceivePromotion(MilitaryRank commanderRank)
    {
        // We only upgrade if the commander is higher rank than our current level
        if (commanderRank > _currentRankLevel)
        {
            _currentRankLevel = commanderRank;
            ApplyRankStats();
        }
    }

    private void ApplyRankStats()
    {
        // Rank 0: Quicker
        if (_currentRankLevel >= MilitaryRank.Specialist)
        {
            _agent.speed = _originalMoveSpeed * 1.5f;
        }

        // Rank 1: Stronger (Health)
        if (_currentRankLevel >= MilitaryRank.Corporal && !_hasBuffedHealth)
        {
            float healthBuff = enemyData.baseHealth * 0.5f; // +50% HP
            _currentHealth += healthBuff;
            _hasBuffedHealth = true;
        }
        
        // Rank 7: Ghost Mode (Instant)
        if (_currentRankLevel >= MilitaryRank.CommandSergeantMajor)
        {
            SetTransparency(0.2f);
            _isGhostMode = true;
        }
    }

    private void HandleRankAbilities()
    {
        // Rank 3: Regen
        if (_currentRankLevel >= MilitaryRank.StaffSergeant)
        {
            _regenTimer += Time.deltaTime;
            if (_regenTimer >= 1.0f)
            {
                // Full strength: 5 HP/sec. Weakened: 2.5 HP/sec.
                float healAmount = _isWeakened ? 2.5f : 5f;
                
                if (_currentHealth < enemyData.baseHealth) 
                    _currentHealth += healAmount; 
                    
                _regenTimer = 0f;
            }
        }

        // Cooldowns (Rank 2 & 5) - Penalties applied in usage logic if desired, 
        // but usually speed/dmg/health are the noticeable ones.
        if (_dodgeCooldown > 0) _dodgeCooldown -= Time.deltaTime;
        if (_coverCooldown > 0) _coverCooldown -= Time.deltaTime;
    }
    
    // Called by the Commander when they die. Halves effects.
    public void ApplyCommanderDeathPenalty()
    {
        if (_isWeakened) return; // Already weakened
        _isWeakened = true;
        Debug.Log($"{name} has been weakened by Commander death!");

        // 1. Speed: Halve the boost
        // Original boost was 1.5x (+50%). New boost is 1.25x (+25%).
        if (_currentRankLevel >= MilitaryRank.Specialist)
        {
            float baseSpeed = enemyData.moveSpeed; // Assuming data holds the original
            _agent.speed = baseSpeed * 1.25f; 
        }
        
        // 2. Transparency: Make them more visible (0.2 -> 0.6)
        if (_currentRankLevel >= MilitaryRank.SergeantMajor)
        {
            SetTransparency(0.6f);
        }
        
        // 3. Health: We don't remove health (that feels unfair/buggy), 
        // but we stop future regen in the Update loop.
    }

    #endregion

    #region --- AI Logic ---
    
    private void RunAILogic()
    {
        if (_state == EnemyState.Ragdolled) return;
        if (!_agent.enabled || !_agent.isOnNavMesh) return;

        // Default Targeting Logic
        if ((_currentTarget as UnityEngine.Object) == null || _currentTarget.transform == null || !_currentTarget.transform.gameObject.activeInHierarchy)
        {
            if (_coreTarget != null) _currentTarget = _coreTarget.GetComponent<IDamageable>();
            _state = EnemyState.Pursuing;
            if (_currentTarget == null) return;
        }

        if (_sensorCooldown <= 0f) { UpdateSensors(); _sensorCooldown = SENSOR_UPDATE_RATE; }

        // --- PRIORITY CHECKS FOR SMART AI ---

        // Rank 2: Smarter (Dodge Gaze)
        if (_currentRankLevel >= MilitaryRank.Sergeant && _state != EnemyState.Dodging && _dodgeCooldown <= 0f)
        {
            if (CheckIfPlayerIsLooking())
            {
                StartCoroutine(PerformDodge());
                return;
            }
        }

        // Rank 5: Smarter (Cover Seeking)
        if (_currentRankLevel >= MilitaryRank.FirstSergeant && _state != EnemyState.SeekingCover && _coverCooldown <= 0f)
        {
             // If we are targeting the player (not the core) and visible, try to hide
             if (_currentTarget.transform == _playerTarget && CheckLineOfSight(_playerTarget))
             {
                 Vector3 coverPos = FindBestCoverSpot();
                 if (coverPos != Vector3.zero)
                 {
                     StartCoroutine(SeekCoverRoutine(coverPos));
                     return;
                 }
             }
        }

        // Standard States
        switch (_state)
        {
            case EnemyState.Pursuing: UpdatePursueState(); break;
            case EnemyState.Attacking: UpdateAttackState(); break;
            case EnemyState.Dodging: break; // Handled by coroutine
            case EnemyState.SeekingCover: break; // Handled by coroutine
            case EnemyState.Ragdolled: break;
        }
    }
    
    private void UpdateSensors() {
        if (enemyData.canBeDistracted) {
            if (_attackSource != null) { SetNewTarget(_attackSource.GetComponent<IDamageable>()); _attackSource = null; return; }
            if (fov != null && _playerTarget != null && fov.IsTargetVisible(_playerTarget)) { _lastKnownPlayerPos = _playerTarget; SetNewTarget(_playerTarget.GetComponent<IDamageable>()); return; }
        }
        if (_currentTarget.transform != _playerTarget && _coreTarget != null) { SetNewTarget(_coreTarget.GetComponent<IDamageable>()); }
    }

    private void UpdatePursueState() 
    { 
        _agent.isStopped = false;
        
        // --- BLOCKED PATH CHECK ---
        if (_currentTarget.transform == _coreTarget)
        {
            _pathCheckTimer += Time.deltaTime;
            if (_pathCheckTimer > _pathCheckInterval)
            {
                _pathCheckTimer = 0f;
                
                if (!_agent.pathPending && (_agent.pathStatus == NavMeshPathStatus.PathPartial || _agent.pathStatus == NavMeshPathStatus.PathInvalid))
                {
                    GameObject bestDoor = FindBestDoor();
                    if (bestDoor != null)
                    {
                        SetNewTarget(bestDoor.GetComponent<IDamageable>());
                        Debug.Log($"Path to Core blocked. {name} switching target to Door: {bestDoor.name}");
                    }
                }
            }
        }
        
        if (Vector3.Distance(transform.position, _currentTarget.transform.position) <= _agent.stoppingDistance) { _state = EnemyState.Attacking; return; }
        _agent.SetDestination(_currentTarget.transform.position);
    }
    
    // --- UPDATED: RANDOM DOOR SELECTION ---
    private GameObject FindBestDoor()
    {
        // 1. Find all doors in the scene
        GameObject[] doors = GameObject.FindGameObjectsWithTag("Door");
        
        if (doors.Length == 0) return null;

        // 2. Filter out doors that might be null or destroyed (just in case)
        // and pick a RANDOM one from the list.
        // This ensures enemies spread out to different doors.
        GameObject randomDoor = doors[Random.Range(0, doors.Length)];

        return randomDoor;
    }
    // --------------------------------------

    private void UpdateAttackState() { 
        if (Vector3.Distance(transform.position, _currentTarget.transform.position) > _agent.stoppingDistance + 1.5f) { _state = EnemyState.Pursuing; return; }
        _agent.isStopped = true; 
        RotateTowards(_currentTarget.transform.position);
        if (_timeSinceLastAttack >= enemyData.attackCooldown)
        {
            TryAttack();
        }
    }

    void TryAttack() 
    { 
        if (_currentTarget == null) return; 
        if (_timeSinceLastAttack >= enemyData.attackCooldown) { 
            
            float dmg = enemyData.attackDamage;
            
            // Rank 4: Stronger (Attack Multiplier)
            if (_currentRankLevel >= MilitaryRank.MasterSergeant)
            {
                // Full: 2x damage. Weakened: 1.5x damage.
                float multiplier = _isWeakened ? 1.5f : 2.0f;
                dmg *= multiplier;
            }

            _currentTarget.TakeDamage(dmg); 
            _timeSinceLastAttack = 0f; 
        } 
    }

    #endregion

    #region --- Smart AI Behaviors (Rank Specific) ---

    private bool CheckIfPlayerIsLooking()
    {
        if (_playerTarget == null) return false;
        
        // Calculate Dot Product
        Vector3 toEnemy = (transform.position - _playerTarget.position).normalized;
        Vector3 playerLook = _playerTarget.forward;
        
        // If Dot > 0.8, player is looking roughly at us
        if (Vector3.Dot(playerLook, toEnemy) > 0.8f)
        {
            // Check distance (don't dodge if miles away)
            if (Vector3.Distance(transform.position, _playerTarget.position) < 20f)
                return true;
        }
        return false;
    }

    private IEnumerator PerformDodge()
    {
        _state = EnemyState.Dodging;
        _agent.isStopped = true;
        
        // Dodge perpendicular to player look direction
        Vector3 toPlayer = (_playerTarget.position - transform.position).normalized;
        Vector3 dodgeDir = Vector3.Cross(toPlayer, Vector3.up); // Left or Right
        if (Random.value > 0.5f) dodgeDir = -dodgeDir;

        // Dash
        float dashTime = 0.3f;
        float dashSpeed = 20f;
        Vector3 startPos = transform.position;
        Vector3 targetPos = startPos + (dodgeDir * 5f);

        float t = 0;
        while(t < dashTime)
        {
            t += Time.deltaTime;
            // Simple transform move for instant reaction, assuming NavMeshAgent handles correction next frame
            _agent.Move(dodgeDir * dashSpeed * Time.deltaTime);
            yield return null;
        }

        _dodgeCooldown = 4.0f; // Don't dodge again for 4 seconds
        _state = EnemyState.Pursuing;
        _agent.isStopped = false;
    }

    private Vector3 FindBestCoverSpot()
    {
        // Simple cover finding: Raycast towards random obstacles
        // Realistically, this should query a CoverManager, but here is a localized logic:
        
        int checks = 5;
        for(int i=0; i<checks; i++)
        {
            Vector3 randomDir = Random.insideUnitSphere * 10f;
            randomDir.y = 0;
            Vector3 checkPos = transform.position + randomDir;
            
            if (NavMesh.SamplePosition(checkPos, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            {
                // Check if this spot is hidden from player
                Vector3 dirToPlayer = (_playerTarget.position - hit.position).normalized;
                if (Physics.Raycast(hit.position + Vector3.up, dirToPlayer, out RaycastHit rayHit, 50f, obstacleLayer))
                {
                    // We hit an obstacle before the player, so this is cover
                    return hit.position;
                }
            }
        }
        return Vector3.zero;
    }

    private IEnumerator SeekCoverRoutine(Vector3 coverPos)
    {
        _state = EnemyState.SeekingCover;
        _agent.SetDestination(coverPos);
        
        float timeout = 3f;
        while(timeout > 0 && _agent.remainingDistance > 1f)
        {
            timeout -= Time.deltaTime;
            yield return null;
        }

        yield return new WaitForSeconds(1.0f); // Wait in cover
        
        _coverCooldown = 8.0f; // Don't seek cover again for a while
        _state = EnemyState.Pursuing;
    }

    private void SetTransparency(float alpha)
    {
        if (_meshRenderer != null)
        {
            foreach(Material mat in _meshRenderer.materials)
            {
                // Standard Shader manipulation
                if (mat.HasProperty("_Color"))
                {
                    Color c = mat.color;
                    c.a = alpha;
                    mat.color = c;
                    
                    // Standard Shader setup for transparency
                    mat.SetFloat("_Mode", 3); // Transparent
                    mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    mat.SetInt("_ZWrite", 0);
                    mat.DisableKeyword("_ALPHATEST_ON");
                    mat.EnableKeyword("_ALPHABLEND_ON");
                    mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                    mat.renderQueue = 3000;
                }
            }
        }
    }

    #endregion

    #region --- Damage & Death ---
    
    private void HandleFallDeath()
    {
        if (_isDead) return;
        _isDead = true;

        // 1. Tell WaveManager we are gone
        if (_waveManager != null) _waveManager.EnemyDestroyed();

        // 2. Spawn Scrap at Bot's Home Base
        if (enemyData != null && enemyData.scrapMetalPrefab != null)
        {
            Vector3 spawnPos = new Vector3(0, 5f, 0); // Default fallback
            
            ScrapCollectorBot bot = FindObjectOfType<ScrapCollectorBot>();
            if (bot != null)
            {
                // Spawn 5 units ABOVE the bot's home
                spawnPos = bot.GetHomePosition() + Vector3.up * 5f;
            }
            
            Instantiate(enemyData.scrapMetalPrefab, spawnPos, Quaternion.identity);
        }

        // 3. Destroy Self
        Destroy(gameObject);
    }
    
    public void TakeDamage(float amount)
    {
        // Call the main method with "null" for attacker and "Physical" as the default type
        TakeDamage(amount, null, DamageType.Physical);
    }
    
    // Replace your duplicate TakeDamage methods with this ONE complete method:
    public void TakeDamage(float amount, Transform attacker, DamageType damageType)
    {
        if (_isDead) return;

        // --- 1. GHOST MODE CHECK (Rank 7) ---
        if (_currentRankLevel >= MilitaryRank.CommandSergeantMajor)
        {
            // Ghost Mode is immune to Physical attacks (Bullets/Explosions), 
            // but vulnerable to Elemental attacks (Fire/Energy/etc.)
            if (damageType == DamageType.Physical) 
            {
                if (!_isWeakened)
                {
                    Debug.Log("Enemy is in Ghost Mode! Immune.");
                    return; // Total Immunity
                }
                else
                {
                    // If the commander died, they are weakened (take 50% damage)
                    amount *= 0.5f;
                }
            }
        }

        // --- 2. TRANSPARENCY CHECK (Rank 6) ---
        // Make them visible for a moment if hit
        if (_currentRankLevel >= MilitaryRank.SergeantMajor && !_isWeakened)
        {
            SetTransparency(0.3f);
        }

        // --- 3. ARMOR CALCULATION ---
        float multiplier = DamageMultiplier.GetMultiplier(damageType, enemyData.armorType);
        float finalDamage = amount * multiplier;

        // Optional: Visual logs for weakness/resistance
        if (multiplier > 1.0f) Debug.Log("Critical Hit! Weakness Exploited!");
        if (multiplier < 1.0f) Debug.Log("Resisted!");

        // --- 4. APPLY DAMAGE ---
        _currentHealth -= finalDamage;
    
        // Distraction logic
        if (enemyData.canBeDistracted) _attackSource = attacker;
    
        // Death check
        if (_currentHealth <= 0f) Die(true);
    }
    
    // --- TakeExplosion (Resets state if already hit) ---
    // Update TakeExplosion to use Explosive type
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
                    TakeDamage(damage, attacker, DamageType.Explosive);
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
    
    // --- ANTI-CLIP PUNISHMENT ---
    // Teleports the enemy away and ragdolls them. Used when they glitch through doors.
    public void PunishTrespasser()
    {
        if (_isDead) return;

        // 1. Calculate Teleport Position (20 units X/Z, 10 units Y)
        Vector3 randomOffset;
        if (Random.value > 0.5f)
            randomOffset = new Vector3(20f * (Random.value > 0.5f ? 1 : -1), 10f, 0f);
        else
            randomOffset = new Vector3(0f, 10f, 20f * (Random.value > 0.5f ? 1 : -1));

        Vector3 punishPos = transform.position + randomOffset;

        Debug.LogWarning($"{name} cheated! Teleporting to {punishPos}");

        // 2. Teleport (Must use Warp for NavMeshAgents)
        if (_agent != null)
        {
            _agent.Warp(punishPos);
        }
        else
        {
            transform.position = punishPos;
        }

        // 3. Apply "Explosion" effect (Ragdoll + Force)
        // We simulate an explosion right below their feet to launch them
        TakeExplosion(
            10f, // 0 Damage (or add damage if you want to hurt them)
            null, // No specific attacker
            punishPos + Vector3.down, // Explosion origin (below feet)
            5f, // Force
            5f,  // Radius
            2.0f // Upward modifier
        );
    }

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
    
    void HandleGunshot(Vector3 soundPosition) 
    { 
        // 1. Basic checks
        if (_isDead || enemyData == null || !enemyData.canBeDistracted) return; 

        // 2. Check distance
        float distance = Vector3.Distance(transform.position, soundPosition); 
        if (distance <= enemyData.hearingRange) 
        { 
            // 3. Investigate the sound
            // We set the destination directly to the sound
            if (_agent.enabled && _agent.isOnNavMesh)
            {
                _agent.SetDestination(soundPosition);
            }
        } 
    }
    
    private bool CheckLineOfSight(Transform target)
    {
        if (target == null) return false;

        // Define "Eye" positions (approx. 1.5m up from pivot)
        Vector3 startPos = transform.position + Vector3.up * 1.5f; 
        Vector3 targetPos = target.position + Vector3.up * 1.5f;
        
        Vector3 direction = (targetPos - startPos).normalized;
        float distance = Vector3.Distance(startPos, targetPos);

        // Cast a ray. If it hits an obstacle, we DON'T have line of sight.
        if (Physics.Raycast(startPos, direction, distance, obstacleLayer))
        {
            return false; // Blocked by a wall
        }

        return true; // Path is clear
    }
    
    private void UpdateSpawnerLogic() 
    { 
        // 1. Handle Growth (getting bigger over time)
        if (enemyData.canGrow) 
        { 
            _timeSinceLastGrowth += Time.deltaTime; 
            if (_timeSinceLastGrowth >= enemyData.growthInterval) 
            { 
                Grow(); 
                _timeSinceLastGrowth = 0f; 
            } 
        }

        // 2. Handle Time Limit (if the spawner dies of old age)
        if (enemyData.spawnerTimeLimit > 0) 
        { 
            _spawnerTimeAlive -= Time.deltaTime; 
            if (_spawnerTimeAlive <= 0f) return; 
        }

        // 3. Handle Spawning Minions
        _timeSinceLastSpawn += Time.deltaTime; 
        if (_timeSinceLastSpawn >= enemyData.spawnInterval) 
        { 
            SpawnMinion(); 
            _timeSinceLastSpawn = 0f; 
        }
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
        
        // Pick a random spot near the spawner
        Vector3 randomPos = transform.position + (Random.insideUnitSphere * 3f); 
        randomPos.y = transform.position.y; 
        
        // Pick a random minion prefab
        GameObject prefabToSpawn = enemyData.spawnPrefabs[Random.Range(0, enemyData.spawnPrefabs.Count)]; 
        Instantiate(prefabToSpawn, randomPos, Quaternion.identity); 
    }
    
    public void ApplySpeedModification(float speedMultiplier) 
    { 
        if (_agent != null) 
        {
            // Multiply our base speed by the slow factor (e.g., 0.5)
            _agent.speed = _originalMoveSpeed * speedMultiplier; 
        }
    }
    private void RotateTowards(Vector3 targetPosition) { Vector3 direction = (targetPosition - transform.position).normalized; direction.y = 0; if (direction != Vector3.zero) { Quaternion lookRotation = Quaternion.LookRotation(direction); transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * 5f); } }
    
    #endregion
}