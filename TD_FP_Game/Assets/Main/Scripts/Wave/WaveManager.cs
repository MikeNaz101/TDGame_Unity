using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq; // Required for LINQ extensions (Count())

public class WaveManager : MonoBehaviour
{
    [Header("Wave Configuration")]
    [Tooltip("List of all wave configuration assets for the level.")]
    public WaveConfig[] waves;

    [Header("Spawn Points")]
    [Tooltip("List of transforms where enemies can spawn.")]
    public Transform[] spawnPoints;
    [Tooltip("How far from the spawn point center enemies can appear.")]
    public float spawnRadius = 4.0f;

    [Header("Core Reference")]
    [Tooltip("The central core/base the enemies are targeting.")]
    public Transform coreTarget; // Still needed for context, but EnemyController finds its own target
    
    [Header("Boss Integration")]
    public BossWaveManager bossWaveManager;

    private int currentWaveIndex = 0;
    private int enemiesRemaining = 0;
    private bool isWaveActive = false;
    
    void Start()
    {
        if (waves.Length > 0)
        {
            StartCoroutine(WaveSequenceRoutine());
        }
        else
        {
            Debug.LogError("No Wave Configurations found! Please assign WaveConfig Scriptable Objects.");
        }
    }

    // Main routine controlling the flow of waves
    IEnumerator WaveSequenceRoutine()
    {
        while (currentWaveIndex < waves.Length)
        {
            WaveConfig currentWave = waves[currentWaveIndex];
            
            // 1. Preparation Phase
            Debug.Log("Starting Preparation for " + currentWave.waveName);
            yield return new WaitForSeconds(currentWave.preparationTime);
            
            // 2. Spawn Phase
            bossWaveManager.CheckForBossSpawn(currentWaveIndex + 1); // +1 because index starts at 0
            yield return StartCoroutine(SpawnWave(currentWave));
            
            // 3. Cleanup/Waiting Phase
            Debug.Log(currentWave.waveName + " spawning complete. Waiting for enemies to be destroyed.");
            
            // Wait until all enemies from the current wave are destroyed
            yield return new WaitUntil(() => enemiesRemaining <= 0);

            Debug.Log(currentWave.waveName + " is defeated!");
            currentWaveIndex++;
            isWaveActive = false;

            // Optional: Give the player a small resource bonus or restore health here
        }

        Debug.Log("All waves defeated! Game Over (Win).");
    }

    IEnumerator SpawnWave(WaveConfig wave)
    {
        isWaveActive = true;
        enemiesRemaining = 0;

        foreach (var group in wave.enemyGroups)
        {
            // Update the total count of enemies we need to track
            enemiesRemaining += group.count; 

            for (int i = 0; i < group.count; i++)
            {
                // Instantiate the enemy prefab
                GameObject enemyObj = Instantiate(
                    group.enemyType.enemyPrefab, 
                    GetRandomSpawnPoint(), 
                    Quaternion.identity
                );

                // --- CRITICAL SETUP ---
                // The enemy should find its target and manager in its own Start() method.
                // We only need to assign the EnemyData Scriptable Object.
                
                EnemyController enemyScript = enemyObj.GetComponent<EnemyController>();
                if (enemyScript != null)
                {
                    // 1. Assign the EnemyData asset! This provides all stats (health, speed, etc.)
                    enemyScript.enemyData = group.enemyType;
                    
                    // NOTE: The EnemyController's Start() method will now correctly use this
                    // data to set its _currentHealth and _agent.speed.
                }

                yield return new WaitForSeconds(group.spawnInterval);
            }
            yield return new WaitForSeconds(group.spawnPauseInterval);
        }
    }
    
    // Call this from EnemyController.Die() to decrement the count
    public void EnemyDestroyed()
    {
        enemiesRemaining--;
    }

    // Helper method to get a random spawn point
    public Vector3 GetRandomSpawnPoint()
    {
        if (spawnPoints.Length == 0)
        {
            Debug.LogError("Spawn points are not assigned!");
            return Vector3.zero;
        }

        // 1. Pick a random spawn transform
        Transform selectedPoint = spawnPoints[Random.Range(0, spawnPoints.Length)];
        
        // 2. Generate a random offset circle (X and Z only)
        Vector2 randomCircle = Random.insideUnitCircle * spawnRadius;
        
        // 3. Create the 3D offset (Keeping Y at 0 so they don't spawn in the air/ground)
        Vector3 randomOffset = new Vector3(randomCircle.x, 0, randomCircle.y);

        // 4. Return final position
        return selectedPoint.position + randomOffset;
    }
}
