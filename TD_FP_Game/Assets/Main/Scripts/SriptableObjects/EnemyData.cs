using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewEnemyData", menuName = "Aegis/Enemy Data", order = 1)]
public class EnemyData : ScriptableObject
{
    // --- NEW: Simplified AI Behavior ---
    public enum AIMovementType
    {
        Direct, // Moves directly to the target (CoreAttacker, Patroller, Spawner)
        Wander  // Moves in a randomized "wavy" path towards the target (new Roamer)
    }

    [Header("AI Behavior")]
    [Tooltip("How this enemy moves towards its goal.")]
    public AIMovementType movementType = AIMovementType.Direct;

    [Tooltip("Can this enemy be distracted by player sounds or attacks?")]
    public bool canBeDistracted = true;

    [Tooltip("The base scale of the enemy. '1' is normal.")]
    public float baseScale = 1f;

    [Header("Identification")]
    public string enemyName = "New Grunt";
    public GameObject enemyPrefab; 

    [Header("Combat Stats")]
    public float baseHealth = 50f;
    public float attackDamage = 10f;
    public float attackCooldown = 1.5f;

    [Header("Movement & AI")]
    public float moveSpeed = 3.5f;
    [Tooltip("The max distance the enemy will 'hear' a gunshot from.")]
    public float hearingRange = 30f;
    [Tooltip("The range the enemy will 'see' the player from.")]
    public float visionRange = 15f;
    [Tooltip("How long enemy waits at a patrol point or after investigating a sound.")]
    public float patrolWaitTime = 2f; 

    [Header("Economy")]
    public GameObject scrapMetalPrefab;
    
    // --- THIS IS THE MISSING SECTION ---
    [Header("VFX")]
    [Tooltip("The particle effect to play when the enemy dies (if not ragdolling).")]
    public GameObject deathParticlePrefab;
    // ------------------------------------

    [Header("Spawner (Queen) Settings")]
    [Tooltip("Is this enemy a spawner?")]
    public bool isSpawner = false;
    public float spawnerTimeLimit = 30f;
    public List<GameObject> spawnPrefabs = new List<GameObject>();
    public float spawnInterval = 5f;

    [Header("Growth (Queen) Settings")]
    public bool canGrow = false;
    public float growthInterval = 2f;
    [Tooltip("e.g., 1.1 = 10% larger")]
    public float growthRate = 1.1f;
    public float maxGrowthSize = 3f;
}