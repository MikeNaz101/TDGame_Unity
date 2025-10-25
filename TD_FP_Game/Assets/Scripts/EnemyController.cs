using UnityEngine;
using UnityEngine.AI; // IMPORTANT: Required for NavMeshAgent

public class EnemyController : MonoBehaviour
{
    // --- Public Variables ---
    public float health = 50f;
    public GameObject scrapMetalPrefab;
    public Transform playerTarget; // Assigned in Inspector (Drag the Player here)
    public float detectionRange = 15f;
    public float attackRange = 2.5f;
    public float attackDamage = 10f;
    public float attackCooldown = 2f;

    // --- Private Variables ---
    private NavMeshAgent agent;
    private float timeSinceLastAttack;
    private PlayerStats playerStats; // Reference to the player's stats script

    void Start()
    {
        // Get the required components
        agent = GetComponent<NavMeshAgent>();
        
        // Find the player's target automatically if not set
        if (playerTarget == null)
        {
            playerTarget = GameObject.FindGameObjectWithTag("Player").transform;
        }

        // Get the PlayerStats component for damaging the player
        if (playerTarget != null)
        {
            playerStats = playerTarget.GetComponent<PlayerStats>();
        }

        timeSinceLastAttack = attackCooldown; // Ready to attack immediately
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
        // NOTE: If distance is > detectionRange, the enemy stops.
    }

    void MoveToPlayer()
    {
        // 1. Tell the NavMeshAgent to calculate a path and move.
        agent.isStopped = false;
        agent.SetDestination(playerTarget.position);

        // 2. Face the player while moving (optional but better look)
        Quaternion lookRotation = Quaternion.LookRotation(playerTarget.position - transform.position);
        transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * 5f);
    }

    void AttackPlayer()
    {
        // 1. Stop moving so the attack is stable
        agent.isStopped = true;

        // 2. Face the player before attacking
        transform.LookAt(playerTarget);

        // 3. Handle the attack cooldown
        timeSinceLastAttack += Time.deltaTime;

        if (timeSinceLastAttack >= attackCooldown)
        {
            // Execute the attack
            if (playerStats != null)
            {
                // Damage logic would go into PlayerStats.cs (we'll assume a TakeDamage method there)
                // For now, let's just log it:
                Debug.Log("Enemy attacks Player for " + attackDamage + " damage!");
                // **TODO:** Implement playerStats.TakeDamage(attackDamage);
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
        // 2. Destroy the Enemy
        Destroy(gameObject);
    }
}
