using UnityEngine;
using System.Collections;

public class Tower_Sniper : TowerController
{
    [Header("Sniper Stats")]
    [Tooltip("If true, fires two shots at once.")]
    public bool doubleShot = false;

    // --- UPGRADE LOGIC ---
    protected override void ApplySpecificUpgrade(TowerUpgradeData upgrade)
    {
        switch (upgrade.effectType)
        {
            case TowerUpgradeType.Sniper_DoubleShot:
                doubleShot = true;
                Debug.Log("Sniper Tower: Double Shot enabled!");
                break;
                
            // Damage/Speed handled by base class (TowerUpgradeType.Damage, TowerUpgradeType.FireRate)
        }
    }

    // --- FIRE LOGIC ---
    // Override Shoot to implement Hitscan logic specific to Sniper (e.g. powerful tracers)
    protected override void Shoot()
    {
        if (_towerData.shootSound != null)
        {
            _audioSource.PlayOneShot(_towerData.shootSound);
        }

        // Calculate total damage per shot
        float finalDmg = _towerData.damage * _damageMultiplier * _globalDamageMult;

        // Shot 1
        FireSingleShot(finalDmg);

        // Shot 2 (if upgraded)
        if (doubleShot)
        {
            // Small delay or instant? Instant is simpler for hitscan.
            // We can slightly offset the second shot visually if we want, 
            // but for logic, hitting the same target twice is fine.
            FireSingleShot(finalDmg);
        }
    }

    private void FireSingleShot(float damage)
    {
        if (_currentTarget == null) return;

        // Apply Damage
        //_currentTarget.TakeDamage(damage, transform);
        _currentTarget.TakeDamage(damage, transform, _towerData.damageType);

        // Visuals
        if (_hitscanTracer != null)
        {
            // We start a coroutine for the tracer. 
            // Since we might fire two instantly, we might want to offset the visual slightly 
            // or just let them overlap/refresh.
            StartCoroutine(ShowHitscanTrace());
        }
    }
}