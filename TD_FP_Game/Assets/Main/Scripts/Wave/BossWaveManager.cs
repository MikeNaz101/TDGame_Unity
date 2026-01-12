using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class BossWaveManager : MonoBehaviour
{
    [System.Serializable]
    public class BossEntry
    {
        public MilitaryRank rank;
        public GameObject bossPrefab; // Prefab must have EnemyCommander script!
    }

    [Header("Boss Configuration")]
    public List<BossEntry> bossPrefabs;
    public WaveManager waveManager;

    // Call this at the start of every wave (e.g., from WaveManager)
    public void CheckForBossSpawn(int waveNumber)
    {
        // Check if this is a Boss Round (Every 3 rounds: 3, 6, 9...)
        if (waveNumber % 3 == 0)
        {
            int bossTier = (waveNumber / 3) - 1; 
            // Round 3: Tier 0 (Specialist)
            // Round 6: Tier 1 (Corporal) -> Will spawn Spec + Cpl
            
            StartCoroutine(SpawnBossSequence(bossTier));
        }
    }

    IEnumerator SpawnBossSequence(int highestTierIndex)
    {
        // Wait a bit into the round before spawning bosses
        yield return new WaitForSeconds(10f);

        // Spawn from lowest to highest as requested
        // "At round 6 a specialist AND THEN a Corporal will get spawned"
        for (int i = 0; i <= highestTierIndex; i++)
        {
            if (i < bossPrefabs.Count)
            {
                BossEntry bossData = bossPrefabs[i];
                Debug.Log($"SPAWNING BOSS: {bossData.rank}");

                // Use a random spawn point from the WaveManager
                Vector3 spawnPos = waveManager.GetRandomSpawnPoint();
                GameObject boss = Instantiate(bossData.bossPrefab, spawnPos, Quaternion.identity);
                
                // Configure the commander
                EnemyCommander cmd = boss.GetComponent<EnemyCommander>();
                if (cmd != null)
                {
                    cmd.myRank = bossData.rank;
                }
                
                // Wait before spawning the next boss in the chain
                yield return new WaitForSeconds(5f); 
            }
        }
    }
}