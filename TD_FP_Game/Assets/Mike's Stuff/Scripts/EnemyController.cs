using UnityEngine;
using UnityEngine.AI;
using System.Collections; 
using System.Collections.Generic;
using System.Linq; 

public class EnemyController : MonoBehaviour
{
    // --- STATE MACHINE DEFINITION (For internal tracking) ---
    enum EnemyState
    {
        Patrol = 0,
        Attack = 1,     // Added to prioritize player/tower attack
        Investigate = 2,
        SoundAssist = 3,
        Alarm = 4       // New state for laser alarm response
    }

    // --- SETUP & DATA (Assigned in Inspector or by WaveManager) ---
    [Header("Data & Manager References")]
    [Tooltip("The Scriptable Object containing all stats for this enemy type.")]
    public EnemyData enemyData;
    [Tooltip("The central manager for wave progression and enemy count.")]
    public WaveManager _waveManager;
    [Tooltip("The current target for the enemy (Player or Core)")]
    public Transform _playerTarget; 
    [Tooltip("The patrol route asset used by this enemy.")]
    public PatrolRoute patrolRoute; 
    [Tooltip("The Field of View component for visual detection.")]
    public FieldOfView fov; 
    [Tooltip("All rigidbodies for the ragdoll.")]
    public Rigidbody[] _ragdollRigidbodies;

    [Header("AI & Navigation")]
    [SerializeField] private NavMeshAgent _agent;
    [SerializeField] private EnemyState _state = EnemyState.Patrol;
    [SerializeField] private float _attackThreshold = 2.5f; // Distance to stop and attack
    [SerializeField] private float _attackCooldown = 1.5f; 
    
    // --- PRIVATE RUNTIME STATE ---
    private float _currentHealth;
    private bool _isRagdolling = false;
    private bool _isDead = false;
    private float _timeSinceLastAttack = 0f;
    
    // Patrol State Variables
    private Transform _currentPatrolPoint; 
    private int _routeIndex = 0; 
    private bool _forwardsAlongPath = true; 
    private Vector3 _investigationPoint;
    private float _patrolWaitTimer = 0f; 
    private float _collisionThreshold = 0.5f; // Used for distance check margin

    // --- LIFE CYCLE ---

    void Awake()
    {
        // Get NavMeshAgent component once.
        _agent = GetComponent<NavMeshAgent>();
    }

    public void Init()
    {
        // This is called by WaveManager AFTER enemyData is assigned, preventing NullReferenceException.

        if (enemyData == null)
        {
            Debug.LogError("EnemyData is NULL on Init()! Cannot initialize enemy stats.", this);
            return;
        }

        // --- Manager/Target Finding (The Fix) ---
        // Only attempt to find managers if they haven't been assigned by the WaveManager already
        if (_waveManager == null) 
        {
            // Use FindObjectOfType for singleton managers
            _waveManager = FindFirstObjectByType<WaveManager>();
            if (_waveManager == null) Debug.LogWarning($"{gameObject.name}: WaveManager not found in scene.", this);
        }
        
        // Find the player target (assuming the player is tagged "Player")
        if (_playerTarget == null) 
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) _playerTarget = playerObj.transform;
            // NOTE: If using the Core as a target, you would find that object here instead.
        }
        
        // Find the patrol route in the scene if it's not set on the prefab
        if (patrolRoute == null) patrolRoute = FindFirstObjectByType<PatrolRoute>(); 
        
        // --- Data Initialization ---
        _currentHealth = enemyData.baseHealth;
        if (_agent != null)
        {
            _agent.speed = enemyData.moveSpeed;
            // Ensure agent is enabled for the first move command
            _agent.enabled = true; 
        }

        // --- Component Setup ---
        SetRagdollActive(false);

        // --- Patrol Route Setup (Starts Patrol if valid) ---
        if (patrolRoute != null && patrolRoute.route.Count > 0)
        {
            _currentPatrolPoint = patrolRoute.route[_routeIndex];
            _state = EnemyState.Patrol;
        }
        else
        {
            // If no patrol route, immediately move to attack the player/core target
            _state = EnemyState.Attack; 
            if (_playerTarget == null) 
            {
                 Debug.LogWarning($"{gameObject.name}: No player target found. AI will be idle.", this);
            }
        }

        Debug.Log($"{gameObject.name} initialized with {enemyData.enemyName} data. Targeting: {_playerTarget?.name ?? "None"}", this);
    }

    void Update()
    {
        // --- Core Safety Checks ---
        if (_isDead || _isRagdolling || enemyData == null) return;
        if (_agent == null || !_agent.enabled || !_agent.isOnNavMesh) 
        {
            // Debug.LogWarning("Agent not ready or off NavMesh", this); 
            // If the agent is suddenly disabled, revert to attack state as a fallback
            if (_state != EnemyState.Attack) _state = EnemyState.Attack;
        }
        
        // Update attack cooldown
        if (_timeSinceLastAttack < _attackCooldown)
        {
            _timeSinceLastAttack += Time.deltaTime;
        }

        // --- High-Priority Check: Visual/Proximity Detection ---
        if (_playerTarget != null)
        {
            float distanceToPlayer = Vector3.Distance(transform.position, _playerTarget.position);

            if (distanceToPlayer <= _attackThreshold)
            {
                _state = EnemyState.Attack;
            }
            else if (distanceToPlayer <= enemyData.detectionRange) // FOV or simple proximity check
            {
                // If we see the player outside of attack range, start tracking
                if (_state != EnemyState.Attack) _state = EnemyState.Investigate;
            }
        }
        
        // --- State Machine Execution ---
        switch (_state)
        {
            case EnemyState.Patrol:
                UpdatePatrol();
                break;
            case EnemyState.Attack:
                UpdateAttack();
                break;
            case EnemyState.Investigate:
                UpdateInvestigate();
                break;
            case EnemyState.Alarm:
                UpdateAlarmResponse();
                break;
            case EnemyState.SoundAssist:
                UpdateSoundAssist();
                break;
        }
    }
    
    // --- STATE HANDLERS ---
    
    void UpdateAttack()
    {
        if (_playerTarget == null) return;

        float distanceToTarget = Vector3.Distance(transform.position, _playerTarget.position);
        
        // Move towards player/core
        if (distanceToTarget > _attackThreshold)
        {
            _agent.isStopped = false;
            _agent.SetDestination(_playerTarget.position);
            // Rotate towards target while moving
            RotateTowards(_playerTarget.position);
        }
        // Attack range reached
        else
        {
            _agent.isStopped = true;
            RotateTowards(_playerTarget.position);
            TryAttack();
        }
    }

    void UpdatePatrol()
    {
        if (patrolRoute == null || patrolRoute.route.Count == 0 || _currentPatrolPoint == null) 
        {
            StopMovement();
            return; 
        }

        // Check if we've arrived at the current point
        if (_agent != null && !_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance + _collisionThreshold)
        {
            _patrolWaitTimer += Time.deltaTime;
            if (_patrolWaitTimer >= enemyData.patrolWaitTime)
            {
                NextPatrolPoint(); // Move to the next point
                _patrolWaitTimer = 0f;
            }
        }
        
        // Ensure agent is moving towards the point
        if (_agent.isStopped)
        {
            _agent.isStopped = false;
            _agent.SetDestination(_currentPatrolPoint.position);
        }
    }

    void UpdateInvestigate()
    {
        // Simple logic: Move to the investigation point and then return to patrol
        if (_agent != null && !_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance + _collisionThreshold)
        {
            _patrolWaitTimer += Time.deltaTime;
            if (_patrolWaitTimer > enemyData.patrolWaitTime)
            {
                ReturnToPatrol();
            }
        }
    }

    void UpdateAlarmResponse()
    {
        // Essentially the same logic as Investigate, but dedicated for alarm location
        if (_agent != null && !_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance + _collisionThreshold)
        {
            // Once we reach the alarm spot, hold for a moment then return to attack/patrol
            _patrolWaitTimer += Time.deltaTime;
            if (_patrolWaitTimer > enemyData.patrolWaitTime * 2f) // Wait longer at alarm spot
            {
                // Fallback state logic: If a patrol route exists, go to patrol. Otherwise, go to attack.
                if (patrolRoute != null && patrolRoute.route.Count > 0)
                {
                    ReturnToPatrol();
                }
                else
                {
                    _state = EnemyState.Attack; // Go back to attacking the core/player
                }
            }
        }
    }
    
    void UpdateSoundAssist()
    {
        // Logic to find and move towards an ally (Not fully implemented here, requires ally tagging/tracking)
        // For simplicity, we fallback to investigating the sound source directly
        InvestigatePoint(_investigationPoint);
        // NOTE: Full implementation requires adding FindClosestAlly and related methods.
    }
    
    // --- COMBAT & DAMAGE ---
    
    // Public method called by the SecurityLasers script
    public void GoToAlarm(Vector3 alarmPosition, SecurityLasers laser)
    {
        // High priority: Interrupt current action and investigate alarm location
        _state = EnemyState.Alarm;
        _investigationPoint = alarmPosition; // Store the location
        _patrolWaitTimer = 0f; // Reset timer

        if (_agent != null)
        {
            _agent.isStopped = false;
            _agent.SetDestination(_investigationPoint);
        }
    }

    void TryAttack()
    {
        if (_timeSinceLastAttack >= _attackCooldown)
        {
            // Placeholder damage call (assuming PlayerStats has TakeDamage)
            // _playerTarget.GetComponent<PlayerStats>()?.TakeDamage(enemyData.attackDamage);
            
            Debug.Log($"{gameObject.name} attacks target for {enemyData.attackDamage} damage!", this);
            _timeSinceLastAttack = 0f;
        }
    }

    // Public method called by Projectile.cs
    public void TakeDamage(float amount)
    {
        if (_isDead) return;
        
        _currentHealth -= amount;
        if (_currentHealth <= 0f)
        {
            Die();
        }
    }
    
    // Public method called by ProximityMine.cs
    public void TakeExplosion(Vector3 explosionPosition, float explosionForce, float explosionRadius, float upwardModifier = 0.1f)
    {
        if (_isDead) return;

        // Death logic is handled immediately on explosion hit
        _currentHealth = 0;
        Die(); 
        
        // 1. Ragdoll activation
        ActivateRagdoll();

        // 2. Apply the push
        foreach (Rigidbody rb in _ragdollRigidbodies)
        {
            if (rb != null)
            {
                rb.AddExplosionForce(explosionForce, explosionPosition, explosionRadius, upwardModifier, ForceMode.Impulse);
            }
        }
    }

    void Die()
    {
        if (_isDead) return;
        _isDead = true;
        
        // 1. Report to WaveManager
        if (_waveManager != null)
        {
            _waveManager.EnemyDestroyed();
        }

        // 2. Spawn Scrap Metal
        if (enemyData != null && enemyData.scrapMetalPrefab != null)
        {
            Instantiate(enemyData.scrapMetalPrefab, transform.position, Quaternion.identity);
        }

        // 3. Disable NavMeshAgent and Activate Ragdoll
        if (_agent != null && _agent.enabled) 
        {
            _agent.enabled = false;
        }
        
        // If not exploded, just destroy the object (no ragdoll necessary)
        if (!_isRagdolling)
        {
            Destroy(gameObject, 0.1f); // Destroy immediately if not ragdolled
        } 
        else
        {
            // Destroy after a short delay to allow the ragdoll physics to play out
            Destroy(gameObject, 5f); 
        }
    }

    // --- HELPER & RAGDOLL METHODS ---
    
    private void ActivateRagdoll()
    {
        _isRagdolling = true;
        SetRagdollActive(true);
        // Ensure the root collider is disabled so the ragdoll parts can move
        Collider mainCollider = GetComponent<Collider>();
        if (mainCollider != null) { mainCollider.enabled = false; }
    }

    private void SetRagdollActive(bool isActive)
    {
        // Ensure rigidbodies are assigned!
        if (_ragdollRigidbodies.Length == 0) return;

        // Loop through every part of ragdoll
        foreach (Rigidbody rb in _ragdollRigidbodies)
        {
            if (rb != null)
            {
                rb.isKinematic = !isActive;
            }
        }
    }

    private void StopMovement()
    {
        if (_agent != null && _agent.enabled && _agent.isOnNavMesh)
        {
            _agent.isStopped = true;
        }
    }

    private void RotateTowards(Vector3 targetPosition)
    {
        Vector3 direction = (targetPosition - transform.position).normalized;
        if (direction != Vector3.zero)
        {
            Quaternion lookRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * 5f);
        }
    }
    
    private void InvestigatePoint(Vector3 investigatePoint)
    {
        if (_isRagdolling) return;

        _state = EnemyState.Investigate;
        _investigationPoint = investigatePoint;

        if (_agent != null && _agent.enabled)
        {
            _agent.isStopped = false;
            _agent.SetDestination(_investigationPoint);
        }
    }
    
    private void ReturnToPatrol()
    {
        if (_isRagdolling) return;

        _state = EnemyState.Patrol;
        _patrolWaitTimer = 0; 
        
        // Immediately trigger the next patrol point logic
        NextPatrolPoint(); 
    }

    private void NextPatrolPoint()
    {
        if (_isRagdolling || patrolRoute == null || patrolRoute.route.Count == 0) return;

        int routeCount = patrolRoute.route.Count;
        if (routeCount == 1) 
        {
            if (_agent != null && _agent.enabled) _agent.isStopped = true;
            return;
        }

        // Calculate next index based on direction
        _routeIndex += (_forwardsAlongPath ? 1 : -1);

        // Handle reaching the end of the route (PingPong logic)
        if (_routeIndex >= routeCount)
        {
            // Assuming PingPong for simplicity, otherwise use a patrolType check
            _forwardsAlongPath = false; 
            _routeIndex = Mathf.Max(routeCount - 2, 0); // Reverse and go to second-to-last
        }
        else if (_routeIndex < 0)
        {
            _forwardsAlongPath = true; 
            _routeIndex = Mathf.Min(1, routeCount - 1); // Reverse and go to second point
        }

        // Update the current target point
        _currentPatrolPoint = patrolRoute.route[_routeIndex];
        
        if (_agent != null && _agent.enabled)
        {
            _agent.isStopped = false;
            _agent.SetDestination(_currentPatrolPoint.position);
        }
    }
}
