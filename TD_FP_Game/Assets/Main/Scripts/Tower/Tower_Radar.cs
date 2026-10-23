using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// A utility tower that buffs nearby towers and debuffs enemies within its range.
/// Buffs are tracked per radar, so overlapping radars and paid upgrades never cancel each other.
/// </summary>
public class Tower_Radar : TowerController
{
    [Tooltip("How often (seconds) the radar looks for towers in range.")]
    [SerializeField] private float scanInterval = 0.5f;

    [Header("Radar Visuals")]
    [Tooltip("Optional dish that sweeps around continuously.")]
    [SerializeField] private Transform radarDish;
    [SerializeField] private float dishSpinSpeed = 90f;

    private readonly List<TowerController> _towersInRange = new List<TowerController>();
    private readonly List<TowerController> _foundTowers = new List<TowerController>();
    private float _scanTimer;
    private float _buffBonus;

    private float FireRateBuff => _towerData.fireRateBuff + _buffBonus;
    private float DamageBuff => _towerData.damageBuff + _buffBonus;

    protected override void Update()
    {
        if (_towerData == null) return;

        CleanEnemyList();

        if (radarDish != null) radarDish.Rotate(0f, dishSpinSpeed * Time.deltaTime, 0f, Space.Self);

        _scanTimer -= Time.deltaTime;
        if (_scanTimer <= 0f)
        {
            _scanTimer = scanInterval;
            ScanForTowers();
        }
    }

    protected override void ApplySpecificUpgrade(TowerUpgradeData upgrade)
    {
        if (upgrade.effectType != TowerUpgradeType.Radar_BuffStrength) return;
        _buffBonus += upgrade.effectValue;
        _scanTimer = 0f; // re-apply stronger buffs on the next frame
    }

    private void ScanForTowers()
    {
        // Uses the tower registry instead of a physics query: no "Tower" layer setup needed, no allocations.
        float range = CurrentRange;
        float rangeSqr = range * range;
        Vector3 pos = transform.position;
        _foundTowers.Clear();

        foreach (TowerController tower in TowerRegistry.ActiveTowers)
        {
            if (tower == null || tower == this || tower is Tower_Radar) continue;
            if ((tower.transform.position - pos).sqrMagnitude > rangeSqr) continue;

            tower.ApplyBuff(this, FireRateBuff, DamageBuff);
            _foundTowers.Add(tower);
        }

        // Un-buff towers that left the range (or were sold).
        foreach (TowerController tower in _towersInRange)
        {
            if (tower != null && !_foundTowers.Contains(tower)) tower.RemoveBuff(this);
        }

        _towersInRange.Clear();
        _towersInRange.AddRange(_foundTowers);
    }

    protected override void OnEnemyEntered(EnemyController enemy)
    {
        if (_towerData != null) enemy.SetSpeedModifier(this, _towerData.enemySpeedDebuff);
    }

    protected override void OnEnemyExited(EnemyController enemy) { if (enemy != null) enemy.ClearSpeedModifier(this); }

    // --- Override these to do nothing ---
    protected override void TryFire() { }
    protected override void Shoot() { }
    
    protected override void OnDestroy()
    {
        base.OnDestroy();
        foreach (TowerController tower in _towersInRange)
        {
            if (tower != null) tower.RemoveBuff(this);
        }
        foreach (EnemyController enemy in _enemiesInRange)
        {
            if (enemy != null) enemy.ClearSpeedModifier(this);
        }
    }
}
