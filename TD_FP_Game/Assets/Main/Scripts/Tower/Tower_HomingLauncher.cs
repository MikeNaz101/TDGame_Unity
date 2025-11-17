using UnityEngine;

public class Tower_HomingLauncher : TowerController
{
    [Header("Launcher Settings")]
    [Tooltip("If true, the tower will look at the target. If false, it stays static (like a silo).")]
    public bool rotateTowardsTarget = false;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    // --- FIX: Override TryFire to bypass the aiming check ---
    protected override void TryFire()
    {
        // 1. Standard Checks (Cooldown, Target, Data)
        if (_fireCooldown > 0 || _currentTarget == null || _towerData == null)
        {
            return;
        }

        // 2. SKIP the angle check! 
        // Just shoot if we have a valid target and cooldown is ready.
        Shoot();
        _fireCooldown = _towerData.fireRate;
    }

    protected override void AimAtTarget()
    {
        // Only rotate if we explicitly want to (e.g. a turret, not a silo)
        if (rotateTowardsTarget)
        {
            base.AimAtTarget();
        }
    }

    protected override void Shoot()
    {
        if (showDebugLogs) Debug.Log($"[HomingLauncher] Shoot called! Target: {_currentTarget.name}");

        // 1. Play Sound
        if (_towerData.shootSound != null)
        {
            _audioSource.PlayOneShot(_towerData.shootSound);
        }

        if (_towerData.projectilePrefab == null)
        {
            Debug.LogError("[HomingLauncher] FAILED: No Projectile Prefab assigned in TowerData!");
            return;
        }

        // 2. Instantiate the missile
        // We spawn it at the shoot point, but force rotation to UP
        GameObject proj = Instantiate(
            _towerData.projectilePrefab, 
            _shootPoint.position, 
            Quaternion.LookRotation(Vector3.zero) 
        );
        
        if (showDebugLogs) Debug.Log("[HomingLauncher] Missile instantiated.");

        // 3. Initialize the Homing Logic
        HomingProjectile missile = proj.GetComponent<HomingProjectile>();
        if (missile != null)
        {
            missile.Initialize(_towerData, this.transform, _currentTarget);
            if (showDebugLogs) Debug.Log("[HomingLauncher] Missile initialized successfully.");
        }
        else
        {
            Debug.LogError($"[HomingLauncher] The projectile prefab '{proj.name}' is missing the 'HomingProjectile' script!");
        }
    }
}