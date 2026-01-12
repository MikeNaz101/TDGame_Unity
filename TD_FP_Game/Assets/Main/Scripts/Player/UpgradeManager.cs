using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class UpgradeManager : MonoBehaviour
{
    public static UpgradeManager Instance;

    [Header("References")]
    public PlayerStats playerStats;
    public DoomMovement playerMovement;
    public WeaponControllerHS weaponController;

    // --- DATA STRUCTURES ---

    [System.Serializable]
    public class UpgradePath
    {
        public string upgradeName; // e.g., "Damage", "Fire Rate"
        public int currentLevel = 1;
        public int maxLevel = 10;
        public int baseCost = 100;
        public float costMultiplier = 1.5f; 

        public int GetCost()
        {
            if (currentLevel >= maxLevel) return int.MaxValue;
            return Mathf.RoundToInt(baseCost * Mathf.Pow(costMultiplier, currentLevel - 1));
        }
    }

    [System.Serializable]
    public class WeaponUpgradeData
    {
        public string weaponName; // ID
        public int unlockCost = 500;
        public bool isUnlocked = false;
        public bool guidanceUnlocked = false;
        public int guidanceCost = 1000;

        // Each weapon has its own upgrade paths
        public UpgradePath damagePath; 
        public UpgradePath fireRatePath;
        // --- NEW PATHS ---
        public UpgradePath maxAmmoPath;
        public UpgradePath clipSizePath;
    }

    // --- UPGRADE LISTS ---

    [Header("Player Upgrades")]
    public UpgradePath healthUpgrade; 
    public UpgradePath speedUpgrade;  

    [Header("Global Tower Upgrades")]
    public UpgradePath towerDamageGlobal; 
    public UpgradePath towerRangeGlobal;  

    [Header("Weapon Database")]
    public List<WeaponUpgradeData> weaponUpgrades = new List<WeaponUpgradeData>();

    // --- RUNTIME MULTIPLIERS ---
    public float PlayerHealthMult { get; private set; } = 1f;
    public float PlayerSpeedMult { get; private set; } = 1f;
    public float GlobalTowerDamageMult { get; private set; } = 1f;
    public float GlobalTowerRangeMult { get; private set; } = 1f;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        if (playerStats == null) playerStats = FindObjectOfType<PlayerStats>();
        if (playerMovement == null) playerMovement = FindObjectOfType<DoomMovement>();
        if (weaponController == null) weaponController = FindObjectOfType<WeaponControllerHS>();
        
        // Ensure starting weapon is unlocked
        if(weaponUpgrades.Count > 0 && !weaponUpgrades[0].isUnlocked)
        {
            weaponUpgrades[0].isUnlocked = true;
        }
    }

    // --- PLAYER / TOWER PURCHASE ---

    public bool TryBuyGlobalUpgrade(UpgradePath path, string type)
    {
        int cost = path.GetCost();
        if (playerStats.SpendScrap(cost))
        {
            path.currentLevel++;
            ApplyGlobalEffect(type);
            return true;
        }
        return false;
    }

    private void ApplyGlobalEffect(string type)
    {
        switch (type)
        {
            case "Health":
                PlayerHealthMult += 0.25f; 
                playerStats.maxHealth = 100f * PlayerHealthMult; 
                playerStats.Heal(25f); 
                break;
            case "Speed":
                PlayerSpeedMult += 0.10f; 
                if(playerMovement != null) playerMovement.ApplySpeedUpgrade(PlayerSpeedMult);
                break;
            case "TowerDmg":
                GlobalTowerDamageMult += 0.20f;
                UpdateAllActiveTowers();
                break;
        }
    }

    // --- WEAPON PURCHASE / UNLOCK ---

    public bool IsWeaponUnlocked(string weaponName)
    {
        var w = weaponUpgrades.Find(x => x.weaponName == weaponName);
        return w != null && w.isUnlocked;
    }

    public int GetWeaponUnlockCost(string weaponName)
    {
        var w = weaponUpgrades.Find(x => x.weaponName == weaponName);
        return w != null ? w.unlockCost : 0;
    }

    public bool TryUnlockWeapon(string weaponName)
    {
        var w = weaponUpgrades.Find(x => x.weaponName == weaponName);
        if (w == null || w.isUnlocked) return false;

        if (playerStats.SpendScrap(w.unlockCost))
        {
            w.isUnlocked = true;
            Debug.Log($"Unlocked {weaponName}");
            return true;
        }
        return false;
    }

    public bool TryBuyWeaponStat(string weaponName, string statType)
    {
        var w = weaponUpgrades.Find(x => x.weaponName == weaponName);
        if (w == null || !w.isUnlocked) return false;
        
        if (statType == "Guidance")
        {
            if (!w.guidanceUnlocked && playerStats.SpendScrap(w.guidanceCost))
            {
                w.guidanceUnlocked = true;
                Debug.Log($"Guidance System installed on {weaponName}");
                return true;
            }
            return false;
        }

        UpgradePath path = null;
        switch (statType)
        {
            case "Damage": path = w.damagePath; break;
            case "Rate": path = w.fireRatePath; break;
            case "MaxAmmo": path = w.maxAmmoPath; break;
            case "ClipSize": path = w.clipSizePath; break;
        }

        if (path == null) return false;

        int cost = path.GetCost();

        if (playerStats.SpendScrap(cost))
        {
            path.currentLevel++;
            Debug.Log($"Upgraded {weaponName} {statType} to Lv {path.currentLevel}");
            
            // If updating ammo capacity, we might want to refill ammo immediately or wait for reload
            // For simplicity, let's just update stats. WeaponController reads these every frame or on reload.
            return true;
        }
        return false;
    }

    // --- RETRIEVE WEAPON STATS ---

    public float GetWeaponDamageMult(string weaponName)
    {
        var w = weaponUpgrades.Find(x => x.weaponName == weaponName);
        if (w == null) return 1f;
        // Formula: +20% per level
        return 1f + (0.20f * (w.damagePath.currentLevel - 1));
    }

    public float GetWeaponFireRateMult(string weaponName)
    {
        var w = weaponUpgrades.Find(x => x.weaponName == weaponName);
        if (w == null) return 1f;
        // Formula: +10% speed per level
        return 1f + (0.10f * (w.fireRatePath.currentLevel - 1));
    }

    public float GetWeaponMaxAmmoMult(string weaponName)
    {
        var w = weaponUpgrades.Find(x => x.weaponName == weaponName);
        if (w == null) return 1f;
        // Formula: +20% max ammo per level
        return 1f + (0.20f * (w.maxAmmoPath.currentLevel - 1));
    }

    public float GetWeaponClipSizeMult(string weaponName)
    {
        var w = weaponUpgrades.Find(x => x.weaponName == weaponName);
        if (w == null) return 1f;
        // Formula: +20% clip size per level
        return 1f + (0.20f * (w.clipSizePath.currentLevel - 1));
    }
    
    public WeaponUpgradeData GetWeaponData(string weaponName)
    {
        return weaponUpgrades.Find(x => x.weaponName == weaponName);
    }

    private void UpdateAllActiveTowers()
    {
        foreach(var tower in TowerRegistry.ActiveTowers) tower.RefreshGlobalStats();
    }
    
    public bool IsGuidanceUnlocked(string weaponName)
    {
        var w = weaponUpgrades.Find(x => x.weaponName == weaponName);
        return w != null && w.guidanceUnlocked;
    }
}