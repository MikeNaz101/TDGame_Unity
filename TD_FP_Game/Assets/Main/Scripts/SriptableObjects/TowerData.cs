// In TowerData.cs

using UnityEngine;

[CreateAssetMenu(fileName = "NewTowerData", menuName = "Aegis/Tower Data")]
public class TowerData : ScriptableObject
{
    public enum AttackType { Hitscan, Projectile }

    [Header("Tower Identity")]
    public string towerName = "New Tower";
    public string description = "A basic defensive unit.";

    [Header("UI")]
    public Sprite towerIcon;

    [Header("Building")]
    public GameObject towerPrefab;
    public int scrapCost = 100;

    [Header("Combat Stats")]
    [Tooltip("Hitscan: Instant raycast. Projectile: Fires a physical object.")]
    public AttackType attackType = AttackType.Hitscan;
    [Tooltip("The projectile prefab to fire (if attackType is Projectile).")]
    public GameObject projectilePrefab;
    public float damage = 10f;
    public float fireRate = 0.5f;
    public float range = 15f;

    // --- NEW SECTION ---
    [Header("Projectile Settings")]
    [Tooltip("Set to true if the projectile should explode on impact.")]
    public bool isExplosive = false;
    [Tooltip("The radius of the explosion (if isExplosive is true).")]
    public float explosionRadius = 3f;
    [Tooltip("The physics force of the explosion (if isExplosive is true).")]
    public float explosionForce = 500f;
    [Tooltip("The particle effect to spawn on impact.")]
    public GameObject impactParticlePrefab;
    // -------------------

    [Header("Audio")]
    public AudioClip shootSound;
    
    [Header("Utility Tower Stats")]
    [Tooltip("How much health per second the dispenser heals.")]
    public float healthPerSecond = 0f;
    [Tooltip("How much scrap per second the dispenser generates.")]
    public int scrapPerSecond = 0;
    
    [Header("Radar Buffs (e.g., 1.2 = +20%)")]
    [Tooltip("Fire rate multiplier to give other towers (1 = no buff).")]
    public float fireRateBuff = 1f;
    [Tooltip("Damage multiplier to give other towers (1 = no buff).")]
    public float damageBuff = 1f;
    
    [Header("Radar Debuffs (e.g., 0.8 = 20% slow)")]
    [Tooltip("Speed multiplier for enemies (1 = no debuff).")]
    public float enemySpeedDebuff = 1f;
}