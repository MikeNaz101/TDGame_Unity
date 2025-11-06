using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewWaveConfig", menuName = "Aegis/Wave Config")]
public class WaveConfig : ScriptableObject
{
    [System.Serializable]
    public class EnemyGroup
    {
        [Tooltip("The EnemyData asset defining the type of enemy to spawn.")]
        public EnemyData enemyType;
        [Tooltip("The total number of this enemy type to spawn in this wave.")]
        public int count = 5;
        [Tooltip("The time delay between spawning each enemy of this type.")]
        public float spawnInterval = 0.5f;
        [Tooltip("The time delay between spawning enemy groups of this type.")]
        public float spawnPauseInterval = 0.5f;
    }

    [Header("Wave Properties")]
    public string waveName = "Wave 1";
    [Tooltip("The time (in seconds) the player gets to prepare between this wave and the last.")]
    public float preparationTime = 10f;
    
    [Header("Enemies")]
    [Tooltip("A list of enemy groups to spawn during this wave.")]
    public List<EnemyGroup> enemyGroups = new List<EnemyGroup>();
}