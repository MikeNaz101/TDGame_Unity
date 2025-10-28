using UnityEngine;
using UnityEngine.AI; // Required for NavMeshAgent
using System.Collections; // Required for Coroutines

public class EnemyController : MonoBehaviour
{
    // --- Public Variables (Set by WaveManager) ---
    [HideInInspector] public float health; // Set by WaveManager from EnemyData
    [HideInInspector] public GameObject scrapMetalPrefab; // Set by EnemyData (if needed)
    [HideInInspector] public Transform playerTarget; // The target (likely the Energy Core)

    // --- Configuration ---
    public float detectionRange = 15f;
    public float attackRange = 2.5f;
    public float attackDamage = 10f; // Can also be set by EnemyData
    public float attackCooldown = 2f;

    // --- Private References ---
    private NavMeshAgent agent;
    private float timeSinceLastAttack;
    private PlayerStats playerStats; // Reference to the player's stats script
    private WaveManager waveManager; // Reference to the central manager

    void Start()
    {
        // 1. Get required components
        agent = GetComponent<NavMeshAgent>();
        timeSinceLastAttack = attackCooldown; 

        // 2. Find required managers in the scene
        waveManager = FindObjectOfType<WaveManager>();

        // 3. Find the PlayerStats script on the target (if the target is the player)
        if (playerTarget != null)
        {
            // Note: If playerTarget is the CORE, this should be FindObjectOfType<PlayerStats>();
            // If the core is the target, we only need to attack the core, not the player.
            // Assuming the core target is the player's position for now (as per early design)
            playerStats = playerTarget.GetComponent<PlayerStats>();
        }

        if (agent == null || waveManager == null)
        {
            Debug.LogError("Enemy setup error: NavMeshAgent or WaveManager is missing/null.", this);
        }
    }

    // Function equivalent to Unreal's Event AnyDamage
    public void TakeDamage(float amount)
    {
        health -= amount;
        if (health <= 0f)
        {
            Die();
        }
    }

    void Update()
    {
        if (playerTarget == null) return;

        float distanceToPlayer = Vector3.Distance(transform.position, playerTarget.position);

        // State Machine Logic
        if (distanceToPlayer <= attackRange)
        {
            AttackPlayer();
        }
        else if (distanceToPlayer <= detectionRange)
        {
            MoveToPlayer();
        }
        else
        {
            // Stop if target is too far
            if (agent != null && agent.isActiveAndEnabled)
            {
                agent.isStopped = true;
            }
        }
    }

    void MoveToPlayer()
    {
        if (agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh)
        {
            Debug.LogWarning("Agent not ready or off NavMesh.", this);
            return;
        }

        // 1. Tell the NavMeshAgent to calculate a path and move.
        agent.isStopped = false;
        agent.SetDestination(playerTarget.position);

        // 2. Face the player while moving (optional but better look)
        Quaternion lookRotation = Quaternion.LookRotation(playerTarget.position - transform.position);
        transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * 5f);
    }

    void AttackPlayer()
    {
        if (agent != null && agent.isActiveAndEnabled)
        {
            // 1. Stop moving so the attack is stable
            agent.isStopped = true;
        }

        // 2. Face the player before attacking
        Vector3 lookPosition = playerTarget.position;
        lookPosition.y = transform.position.y; // Keep Y level to prevent tilting
        transform.LookAt(lookPosition);

        // 3. Handle the attack cooldown
        timeSinceLastAttack += Time.deltaTime;

        if (timeSinceLastAttack >= attackCooldown)
        {
            // Execute the attack
            if (playerStats != null)
            {
                playerStats.TakeDamage(attackDamage);
                Debug.Log("Enemy attacks target for " + attackDamage + " damage!");
            }
            timeSinceLastAttack = 0f;
        }
    }

    void Die()
    {
        // 1. Spawn Scrap Metal
        if (scrapMetalPrefab != null)
        {
            Instantiate(scrapMetalPrefab, transform.position, Quaternion.identity);
        }
        
        // 2. Notify the WaveManager before destroying
        if (waveManager != null)
        {
            waveManager.EnemyDestroyed();
        }
        else
        {
            Debug.LogError("WaveManager not found! Cannot report enemy destruction.");
        }

        // 3. Destroy the Enemy
        Destroy(gameObject);
    }
}
