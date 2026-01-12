using UnityEngine;
using System.Collections;

public class Tower_RocketLauncher : TowerController
{
    [Header("Rocket Launcher Settings")]
    [Tooltip("Time to prepare rocket before launch.")]
    public float prepareTime = 1.0f;

    [Tooltip("Assign multiple points here to cycle through them (e.g. Left, Right). If empty, uses default ShootPoint.")]
    public Transform[] alternateFirePoints;

    // --- NEW: Launch Sound ---
    [Header("Audio")]
    public AudioClip launchSound; 
    // -------------------------

    // Specific Multipliers
    private float _rocketSpeedMult = 1f;
    private float _blastRadiusMult = 1f;
    private bool _isReloading = false;
    private int _currentFirePointIndex = 0;

    protected override void ApplySpecificUpgrade(TowerUpgradeData upgrade)
    {
        switch (upgrade.effectType)
        {
            case TowerUpgradeType.Rocket_Speed:
                _rocketSpeedMult += upgrade.effectValue; 
                Debug.Log($"Rocket: Speed boosted to {_rocketSpeedMult}x");
                break;
            case TowerUpgradeType.Rocket_BlastRadius:
                _blastRadiusMult += upgrade.effectValue; 
                Debug.Log($"Rocket: Radius boosted to {_blastRadiusMult}x");
                break;
        }
    }

    protected override void TryFire()
    {
        if (_isReloading || _fireCooldown > 0 || _currentTarget == null) return;
        StartCoroutine(FireSequence());
    }

    private IEnumerator FireSequence()
    {
        _isReloading = true;

        if (_towerData.projectilePrefab != null)
        {
            // 1. Determine which point to use
            Transform currentPoint = _shootPoint; // Default
            
            if (alternateFirePoints != null && alternateFirePoints.Length > 0)
            {
                currentPoint = alternateFirePoints[_currentFirePointIndex];
                _currentFirePointIndex = (_currentFirePointIndex + 1) % alternateFirePoints.Length;
            }

            // 2. Spawn Visual Rocket
            GameObject rocketObj = Instantiate(_towerData.projectilePrefab, currentPoint.position, currentPoint.rotation, currentPoint);
            
            SlowHomingRocket rocketScript = rocketObj.GetComponent<SlowHomingRocket>();

            if (rocketScript != null)
            {
                rocketScript.Initialize(_towerData, _currentTarget, transform);
                
                // Apply Upgrades
                rocketScript.moveSpeed *= _rocketSpeedMult; 
                rocketScript.radiusMultiplier = _blastRadiusMult; // Ensure this exists in SlowHomingRocket if needed

                // 3. Wait
                yield return new WaitForSeconds(prepareTime);

                // 4. Launch
                if (rocketObj != null)
                {
                    rocketObj.transform.SetParent(null);
                    rocketScript.Launch();
                    
                    // --- PLAY LAUNCH SOUND ---
                    if (launchSound != null && _audioSource != null)
                    {
                        _audioSource.PlayOneShot(launchSound);
                    }
                    // -------------------------
                }
            }
        }

        // 5. Cooldown
        float baseRate = _towerData.fireRate / _fireRateMultiplier;
        float remainingCooldown = Mathf.Max(0f, baseRate - prepareTime);
        _fireCooldown = remainingCooldown;
        yield return new WaitForSeconds(remainingCooldown);

        _isReloading = false;
    }

    protected override void Shoot() { } 
}