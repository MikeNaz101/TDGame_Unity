using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Basic shooter with a simple 4-step level track (range, attack speed, damage, damage)
/// on top of the regular upgrade paths. Each level is bought through UpgradeTower().
/// </summary>
public class Tower_Basic : TowerController
{
    private int currentUpgradeLevel = 0;
    
    [SerializeField] private float level1RangeMult = 1.25f;
    [SerializeField] private float level2AttackSpeedMult = 1.5f;
    [SerializeField] private float level3DamageMult = 1.25f;
    [SerializeField] private float level4DamageMult = 1.5f;

    private List<Action> path1UpgradeSteps;

    public bool IsMaxLevel => currentUpgradeLevel >= path1UpgradeSteps.Count;

    protected override void Awake()
    {
        // Must call base: it sets up the range trigger, audio and tower registration.
        base.Awake();

        // Each step changes THIS tower's multipliers. (Changing _towerData directly would
        // permanently edit the shared asset and buff every Basic tower, even in the editor.)
        path1UpgradeSteps = new List<Action>
        {
            () => _rangeMultiplier *= level1RangeMult,
            () => _fireRateMultiplier *= level2AttackSpeedMult,
            () => _damageMultiplier *= level3DamageMult,
            () => _damageMultiplier *= level4DamageMult
        };
    }

    public override void UpgradeTower()
    {
        if (IsMaxLevel) return;

        path1UpgradeSteps[currentUpgradeLevel].Invoke();
        currentUpgradeLevel++;
        base.UpgradeTower();
        RecalculateStats();
    }
}
