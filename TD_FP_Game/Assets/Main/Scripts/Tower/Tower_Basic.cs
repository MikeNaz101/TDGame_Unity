using NUnit.Framework;
using System;
using System.Collections.Generic;
using UnityEngine;

public class Tower_Basic : TowerController
{

    private int currentUpgradeLevel = 0;
    
    [SerializeField] private float level1RangeMult = 1.25f;
    [SerializeField] private float level2AttackSpeedMult = 1.5f;
    [SerializeField] private float level3DamageMult = 1.25f;
    [SerializeField] private float level4DamageMult = 1.5f;

    private List<List<Action>> upgradePaths;

    private List<Action> path1UpgradeSteps;
    private List<Action> path2UpgradeSteps;
    private List<Action> path3UpgradeSteps;
    private void Awake()
    {

        upgradePaths = new List<List<Action>>
        {

        };
        //List of each upgrade in order
        path1UpgradeSteps = new List<Action>
        {
            () => _towerData.range *= level1RangeMult,       //Invoked at 0
            () => _towerData.range *= level2AttackSpeedMult, //Invoked at 1
            () => _towerData.range *= level3DamageMult,      //Invoked at 2
            () => _towerData.range *= level4DamageMult       //Invoked at 3
        };

        path2UpgradeSteps = new List<Action>
        {

        };

        path3UpgradeSteps = new List<Action>
        {

        };
    }

    private void UpgradeTower(int upgradeLevel)
    {
        if (currentUpgradeLevel >= path1UpgradeSteps.Count)
        {
            return;
        }

        path1UpgradeSteps[currentUpgradeLevel].Invoke();
        currentUpgradeLevel++;
    }

}
