using UnityEngine;

[CreateAssetMenu(fileName = "NewEnemyData", menuName = "Aegis/Enemy Data")]
public class EnemyData : ScriptableObject
{
    [Header("Visuals & Prefab")]
    public string enemyName = "New Enemy";
    [Tooltip("The actual prefab GameObject to be instantiated (must have EnemyController.cs).")]
    public GameObject enemyPrefab;

    [Header("Stats")]
    [Tooltip("The base health for this enemy type.")]
    public float baseHealth = 50f;
    [Tooltip("The speed the NavMeshAgent should move at.")]
    public float speed = 3.5f;
    [Tooltip("The amount of scrap metal this enemy drops upon death.")]
    public int scrapDropAmount = 10;
}
