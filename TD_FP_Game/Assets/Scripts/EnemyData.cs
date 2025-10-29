using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "EnemyData", menuName = "Aegis/Enemy Data", order = 1)]
public class EnemyData : ScriptableObject
{
    // --- IDENTITY ---
    [Header("Identification")]
    public string enemyName = "New Grunt";
    public GameObject enemyPrefab; // The main model with EnemyController script attached

    // --- CORE COMBAT STATS ---
    [Header("Combat Stats")]
    [Tooltip("Starting health of the enemy.")]
    public float baseHealth = 50f;
    [Tooltip("Damage dealt to player/core per attack.")]
    public float attackDamage = 10f;
    [Tooltip("Time delay between each attack.")]
    public float attackCooldown = 1.5f;

    // --- MOVEMENT & AI ---
    [Header("Movement & AI")]
    [Tooltip("Speed the NavMeshAgent moves at.")]
    public float moveSpeed = 3.5f;
    [Tooltip("Distance at which the enemy detects the player/switches from Patrol.")]
    public float detectionRange = 15f;
    [Tooltip("Time enemy waits at a patrol point or after investigating a sound.")]
    public float patrolWaitTime = 2f; 

    // --- ECONOMY ---
    [Header("Economy")]
    [Tooltip("Prefab for the resource dropped when this enemy is destroyed.")]
    public GameObject scrapMetalPrefab;

    // --- RAGDOLL / VISUALS ---
    [Header("Visuals")]
    [Tooltip("Optional: Particle effect played upon death/destruction.")]
    public GameObject deathEffect;

    // NOTE: Ragdoll rigidbodies are assigned on the prefab/model itself, not in the data asset.
}