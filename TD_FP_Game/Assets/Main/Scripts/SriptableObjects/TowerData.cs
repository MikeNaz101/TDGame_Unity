using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewTowerData", menuName = "Aegis/Tower Data")]
public class TowerData : ScriptableObject
{
    public enum AttackType { Hitscan, Projectile }

    [Header("Tower Identity")]
    public string towerName = "New Tower";
    public string description = "A basic defensive unit.";
    public Sprite towerIcon;

    [Header("Building")]
    public GameObject towerPrefab;
    public int scrapCost = 100;

    [Header("Combat Stats")]
    public AttackType attackType = AttackType.Hitscan;
    public GameObject projectilePrefab;
    public float damage = 10f;
    public float fireRate = 0.5f;
    public float range = 15f;

    [Header("Projectile Settings")]
    public bool isExplosive = false;
    public float explosionRadius = 3f;
    public float explosionForce = 500f;
    public GameObject impactParticlePrefab;

    [Header("Audio")]
    public AudioClip shootSound;
    
    [Header("Utility Stats")]
    public float healthPerSecond = 0f;
    public int scrapPerSecond = 0;
    
    [Header("Buffs/Debuffs")]
    public float fireRateBuff = 1f;
    public float damageBuff = 1f;
    public float enemySpeedDebuff = 1f;

    // --- NEW: UPGRADE PATHS ---
    [Header("Upgrade Paths")]
    // A list of paths. Each path is a list of upgrades.
    // Example: Path 1 has 4 upgrades. Path 2 has 4 upgrades.
    public List<UpgradePath> upgradePaths = new List<UpgradePath>();

    [System.Serializable]
    public class UpgradePath
    {
        public string pathName = "Path A";
        public List<TowerUpgradeData> upgrades = new List<TowerUpgradeData>();
    }
}