using UnityEngine;

public class Tower_HomingLauncher : TowerController
{
    [Header("Launcher Settings")]
    [Tooltip("If true, the tower will look at the target. If false, it stays static (like a silo).")]
    public bool rotateTowardsTarget = false;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    // --- UPGRADE STATS ---
    private float _rocketSpeedMult = 1f;
    private float _blastRadiusMult = 1f;

    // --- UPGRADE LOGIC ---
    protected override void ApplySpecificUpgrade(TowerUpgradeData upgrade)
    {
        switch (upgrade.effectType)
        {
            case TowerUpgradeType.Rocket_Speed:
                // Increase speed (e.g. +0.5 for 50% faster)
                _rocketSpeedMult += upgrade.effectValue;
                Debug.Log($"Rocket Tower: Thrusters upgraded! Speed Mult: {_rocketSpeedMult}");
                break;

            case TowerUpgradeType.Rocket_BlastRadius:
                // Increase blast radius (e.g. +0.5 for 50% larger)
                _blastRadiusMult += upgrade.effectValue;
                Debug.Log($"Rocket Tower: Warhead upgraded! Radius Mult: {_blastRadiusMult}");
                break;
        }
    }

    // --- FIRE LOGIC ---

    protected override void TryFire()
    {
        // 1. Standard Checks
        if (_fireCooldown > 0 || _currentTarget == null || _towerData == null) return;

        // 2. Just shoot (Silo style usually ignores angle, unless rotating)
        if (rotateTowardsTarget)
        {
            // Use base angle check
            base.TryFire();
        }
        else
        {
            // Fire immediately if target exists
            Shoot();
            // Calculate cooldown using base fire rate and multipliers
            _fireCooldown = _towerData.fireRate / _fireRateMultiplier;
        }
    }

    protected override void AimAtTarget()
    {
        if (rotateTowardsTarget)
        {
            base.AimAtTarget();
        }
    }

    protected override void Shoot()
    {
        if (showDebugLogs) Debug.Log($"[HomingLauncher] Shoot called! Target: {_currentTarget.name}");

        if (_towerData.shootSound != null)
        {
            _audioSource.PlayOneShot(_towerData.shootSound);
        }

        if (_towerData.projectilePrefab == null) return;

        // 1. Spawn Missile
        GameObject proj = Instantiate(
            _towerData.projectilePrefab, 
            _shootPoint.position, 
            Quaternion.LookRotation(Vector3.up) // Launch upwards initially
        );

        // 2. Configure Missile
        HomingProjectile missile = proj.GetComponent<HomingProjectile>();
        if (missile != null)
        {
            // Calculate total damage multiplier (Base Upgrades * Global Upgrades)
            float totalDamageMult = _damageMultiplier * _globalDamageMult;

            // Pass stats to the missile
            missile.speedMultiplier = _rocketSpeedMult;
            missile.radiusMultiplier = _blastRadiusMult;
            missile.damageMultiplier = totalDamageMult;

            missile.Initialize(_towerData, this.transform, _currentTarget);
        }
    }
}