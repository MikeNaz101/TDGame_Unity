using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic; // Required for lists/dictionaries in state logic
using System.Linq;

// NOTE: You must define the PatrolRoute class separately if using patrol logic.
// [System.Serializable]
// public class PatrolRoute { public enum PatrolType { Loop, PingPong } public PatrolType patrolType = PatrolType.Loop; public List<Transform> route = new List<Transform>(); }
// NOTE: You must define the FieldOfView class separately if using FOV logic.

public class MyEnemyController1 : MonoBehaviour
{/*
    // --- Data Driven Stats ---
    [Tooltip("The Scriptable Object containing this enemy's base stats.")]
    public EnemyData enemyData; 

    // --- State Machine Definition (Merged) ---
    enum EnemyState
    {
        Patrol = 0,
        ChasePlayer = 1, // New/Renamed state for 'Aegis' core gameplay
        Attack = 2,
        Investigate = 3,
        SoundAssist = 4
    }
    
    // --- AI & NAVIGATION (Merged) ---
    [Header("AI & Navigation")]
    [Tooltip("Reference to the NavMeshAgent component.")]
    [SerializeField] private NavMeshAgent _agent;
    [Tooltip("Point closeness threshold for patrol/investigation.")]
    [SerializeField] private float _threshold = 0.5f; 
    [Tooltip("Scriptable Object defining the patrol route.")]
    [SerializeField] private PatrolRoute _patrolRoute; 
    [Tooltip("Script for line-of-sight detection (FieldOfView).")]
    [SerializeField] private FieldOfView _fov; 
    [SerializeField] private EnemyState _state = EnemyState.Patrol;
    
    // --- RAGDOLL & EXPLOSION (Merged) ---
    [Header("Ragdoll Settings")]
    [Tooltip("All Rigidbody components that make up the ragdoll.")]
    [SerializeField] private Rigidbody[] _ragdollRigidbodies;
    private bool _isRagdolling = false;
    private bool _isDying = false; // Prevents multiple Die() calls
    
    // --- PRIVATE RUNTIME VARIABLES ---
    private Transform _playerTarget;
    private PlayerStats _playerStats;
    private WaveManager _waveManager;
    private float _currentHealth;
    private float _timeSinceLastAttack;
    private Vector3 _investigationPoint;
    private float _waitTimer = 0f; 

    // --- Patrol Variables ---
    private Transform _currentPoint; 
    private int _routeIndex = 0; 
    private bool _forwardsAlongPath = true; 
    //private MyEnemyController1 _targetAlly; // Assuming MyEnemyController1 is renamed to EnemyController
    
    // --- UNITY LIFECYCLE ---

    void Start()
    {
        _agent = GetComponent<NavMeshAgent>();
        
        // Find Player and Manager references
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            _playerTarget = playerObj.transform;
            _playerStats = _playerTarget.GetComponent<PlayerStats>();
        }
        _waveManager = FindObjectOfType<WaveManager>();
        
        // Apply stats from the data object
        if (enemyData != null)
        {
            _currentHealth = enemyData.health;
            if (_agent != null)
            {
                _agent.speed = enemyData.speed;
                _agent.enabled = true;
            }
        }
        
        // Ragdoll/Setup initialization
        SetRagdollActive(false);
        if (_patrolRoute != null && _patrolRoute.route.Count > 0)
        {
            _currentPoint = _patrolRoute.route[_routeIndex];
        }
        else
        {
            // If no patrol route, go straight to Chase state for TD gameplay
            _state = EnemyState.ChasePlayer; 
        }

        _timeSinceLastAttack = 0f;
    }

    void Update()
    {
        // 1. Core Safety Check: If ragdolling or dead, do NOTHING.
        if (_isRagdolling || _isDying) return;

        // 2. Continuous Player/Core Check (Overrides Patrol/Investigate when close)
        if (_playerTarget != null)
        {
            float distanceToPlayer = Vector3.Distance(transform.position, _playerTarget.position);

            if (distanceToPlayer <= enemyData.attackRange)
            {
                _state = EnemyState.Attack;
            }
            else if (distanceToPlayer <= enemyData.detectionRange)
            {
                _state = EnemyState.ChasePlayer;
            }
            else if (_state == EnemyState.ChasePlayer || _state == EnemyState.Attack)
            {
                // If player leaves detection range, revert to Patrol/Investigate
                ReturnToPatrol(); 
            }
        }

        // 3. State Machine Execution
        switch (_state)
        {
            case EnemyState.Patrol:
                UpdatePatrol();
                break;
            case EnemyState.ChasePlayer:
                UpdateChasePlayer();
                break;
            case EnemyState.Attack:
                UpdateAttack();
                break;
            case EnemyState.Investigate:
                UpdateInvestigate();
                break;
            case EnemyState.SoundAssist:
                UpdateSoundAssist();
                break;
        }
        
        // Field of View Check (if applicable - simplified for merge)
        if (_fov != null && _state != EnemyState.Attack && _state != EnemyState.ChasePlayer && _fov.visibleObjects.Count > 0)
        {
            // If player seen outside of immediate chase/attack range, investigate their last known position
             InvestigatePoint(_fov.visibleObjects[0].position);
        }
    }

    // --- COMBAT METHODS ---

    public void TakeDamage(float amount)
    {
        if (_isDying) return;
        _currentHealth -= amount;
        
        if (_currentHealth <= 0f)
        {
            Die();
        }
    }
    
    // --- EXPLOSION / RAGDOLL METHODS (From ProximityMine) ---
    
    public void TakeExplosion(Vector3 explosionOrigin, float force, float radius, float upwardModifier)
    {
        // Damage is applied by setting health to zero
        if (_isDying) return;
        _currentHealth = 0;

        // Immediately switch to ragdoll physics
        ActivateRagdoll(); 
        
        // Apply the push (RagdollActive must be set to true first)
        foreach (Rigidbody rb in _ragdollRigidbodies)
        {
            if (rb != null) rb.AddExplosionForce(force, explosionOrigin, radius, upwardModifier, ForceMode.Impulse);
        }
        
        Die(); // Handle death logic (drops, wave count)
    }

    public void ActivateRagdoll()
    {
        if (_isRagdolling) return;

        _isRagdolling = true;

        // 1. Disable NavMesh Movement/AI
        if (_agent != null && _agent.enabled)
        {
            _agent.isStopped = true;
            _agent.ResetPath();
            _agent.enabled = false; 
        }

        // 2. Enable Physics on Ragdoll Parts!
        SetRagdollActive(true);
        // Debug.Log($"{gameObject.name}: Activating Ragdoll!", this);
    }
    
    private void SetRagdollActive(bool isActive)
    {
        // Loop through every part of ragdoll to enable/disable physics
        foreach (Rigidbody rb in _ragdollRigidbodies)
        {
            if (rb != null) 
            { 
                rb.isKinematic = !isActive; 
            }
        }

        // Disable the main character collider
        Collider mainCollider = GetComponent<Collider>();
        if (mainCollider != null) { mainCollider.enabled = !isActive; }
    }


    void Die()
    {
        if (_isDying) return;
        _isDying = true;

        // 1. Report to WaveManager
        if (_waveManager != null)
        {
            _waveManager.EnemyDestroyed();
        }

        // 2. Spawn Scrap Metal
        if (enemyData != null && enemyData.scrapMetalPrefab != null)
        {
            Vector3 dropPosition = transform.position + Vector3.up * 0.5f; 
            Instantiate(enemyData.scrapMetalPrefab, dropPosition, Quaternion.identity);
        }
        
        // 3. Destroy the Enemy after a short delay if ragdolling, or instantly if static
        if (_isRagdolling)
        {
             // Give the explosion force time to run the animation
            Destroy(gameObject, 5f); 
        }
        else
        {
            Destroy(gameObject); 
        }
    }
    
    // --- STATE UPDATE METHODS ---

    void UpdateChasePlayer()
    {
        if (_agent == null || !_agent.enabled) return;
        if (_isRagdolling) return;

        _agent.isStopped = false;
        if (_agent.destination != _playerTarget.position)
        {
             _agent.SetDestination(_playerTarget.position);
        }

        Quaternion lookRotation = Quaternion.LookRotation(_playerTarget.position - transform.position);
        transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * 5f);
    }

    void UpdateAttack()
    {
        if (_agent != null) _agent.isStopped = true;

        if (_playerTarget != null)
        {
            transform.LookAt(_playerTarget);
        }

        _timeSinceLastAttack += Time.deltaTime;

        if (_timeSinceLastAttack >= enemyData.attackCooldown)
        {
            if (_playerStats != null)
            {
                _playerStats.TakeDamage(enemyData.attackDamage);
            }
            _timeSinceLastAttack = 0f;
        }
    }

    // --- PATROL LOGIC (Merged) ---
    private void UpdatePatrol()
    {
        if (_patrolRoute == null || _patrolRoute.route.Count == 0) return;
        if (_agent == null || !_agent.enabled) return;

        // Check if we've arrived at the current point
        if (_agent.remainingDistance <= _agent.stoppingDistance + _threshold)
        {
            // If truly stopped, move to the next point
            if (!_agent.pathPending && (_agent.hasPath == false || _agent.velocity.sqrMagnitude == 0f))
            {
                NextPatrolPoint(); 
                _agent.isStopped = false; 
                _agent.SetDestination(_currentPoint.position); 
            }
        }
    }

    private void NextPatrolPoint()
    {
        if (_patrolRoute == null || _patrolRoute.route.Count == 0) return;
        int routeCount = _patrolRoute.route.Count;
        if (routeCount == 1) return;

        // Logic from MyEnemyController1 to cycle through patrol points
        _routeIndex += (_forwardsAlongPath ? 1 : -1);

        if (_routeIndex >= routeCount)
        {
            if (_patrolRoute.patrolType == PatrolRoute.PatrolType.Loop)
            {
                _routeIndex = 0; 
            }
            else // PingPong
            {
                _forwardsAlongPath = false; 
                _routeIndex = routeCount - 2; 
                if (_routeIndex < 0) _routeIndex = 0;
            }
        }
        else if (_routeIndex < 0)
        {
            _forwardsAlongPath = true;
            _routeIndex = 1;
            if (_routeIndex >= routeCount) _routeIndex = routeCount - 1;
        }

        _currentPoint = _patrolRoute.route[_routeIndex];
    }
    
    // --- INVESTIGATION LOGIC (Merged) ---
    
    public void InvestigatePoint(Vector3 investigatePoint)
    {
        if (_isRagdolling) return;

        _state = EnemyState.Investigate;
        _investigationPoint = investigatePoint;

        if (_agent != null && _agent.enabled && _agent.isOnNavMesh)
        {
            _agent.isStopped = false; 
            _agent.SetDestination(_investigationPoint);
        }
    }

    private void UpdateInvestigate()
    {
        if (_isRagdolling) return;

        if (_agent != null && _agent.enabled && !_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance + _threshold)
        {
            if (!_agent.hasPath || _agent.velocity.sqrMagnitude == 0f) 
            {
                _waitTimer += Time.deltaTime;
                if (_waitTimer > enemyData.attackCooldown) // Using attackCooldown as the wait time here
                {
                    ReturnToPatrol(); 
                }
            }
        }
        else if (_agent == null || !_agent.enabled)
        {
            ReturnToPatrol();
        }
    }

    // --- SOUND ASSIST LOGIC (Merged) ---

    private void UpdateSoundAssist()
    {
        // NOTE: This complex logic requires the PatrolRoute and FieldOfView scripts to be defined.
        // For simplicity, we implement the fallback logic.
        
        if (_targetAlly == null)
        {
             // Fallback: If no ally is found, investigate the sound directly
            InvestigatePoint(_investigationPoint); 
            return;
        }
        
        // ... (Full ally seeking logic is omitted for brevity but would go here)
        // ... (The provided FindClosestAlly method would be called here)

        // If movement completes, both agents InvestigateSound()
    }
    
    // --- UTILITY METHODS ---

    private void ReturnToPatrol()
    {
        if (_isRagdolling) return;

        _state = EnemyState.Patrol;
        _waitTimer = 0; 
        
        if (_agent != null && _agent.enabled)
        {
             _agent.isStopped = false;
        }
    }*/
}
