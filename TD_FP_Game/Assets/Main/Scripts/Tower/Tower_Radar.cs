using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// A utility tower that buffs nearby towers and debuffs
/// enemies within its range.
/// </summary>
public class Tower_Radar : TowerController
{
    // We need to track towers we are buffing
    private List<TowerController> _towersInRange = new List<TowerController>();

    // This is the "scan" layer for finding other towers
    private LayerMask towerLayer;

    protected override void Awake()
    {
        base.Awake(); // Calls TowerController.Awake()
        towerLayer = LayerMask.GetMask("Tower"); // Make sure your towers are on this layer!
    }

    protected override void Update()
    {
        // This tower does not aim or attack.
        
        // --- 1. Debuff Enemies (uses logic from base class) ---
        _enemiesInRange.RemoveAll(e => e == null || !e.gameObject.activeInHierarchy);
        foreach (EnemyController enemy in _enemiesInRange)
        {
            enemy.ApplySpeedModification(_towerData.enemySpeedDebuff);
        }
        
        // --- 2. Buff Towers (New logic) ---
        ScanForTowers();
    }

    private void ScanForTowers()
    {
        Collider[] towersHit = Physics.OverlapSphere(transform.position, _towerData.range, towerLayer);
        
        // Create a temporary list of towers we just found
        List<TowerController> foundTowers = new List<TowerController>();

        // Apply buff to all towers we hit
        foreach (Collider col in towersHit)
        {
            TowerController tower = col.GetComponent<TowerController>();
            // Don't buff ourselves, and don't buff other radars (optional)
            if (tower != null && tower != this && !(tower is Tower_Radar))
            {
                tower.ApplyBuff(_towerData.fireRateBuff, _towerData.damageBuff);
                foundTowers.Add(tower); // Add to our "found" list
            }
        }
        
        // --- 3. Un-buff towers that left the range ---
        // We do this in reverse to safely remove from the list
        for (int i = _towersInRange.Count - 1; i >= 0; i--)
        {
            TowerController tower = _towersInRange[i];
            
            if (tower == null)
            {
                _towersInRange.RemoveAt(i);
                continue;
            }

            // If a tower in our old list is NOT in our new list,
            // it must have left the range.
            if (!foundTowers.Contains(tower))
            {
                tower.RemoveBuff();
                _towersInRange.RemoveAt(i);
            }
        }
        
        // Finally, update our main list to match what we just found
        _towersInRange = foundTowers;
    }

    // --- Override these to do nothing ---
    protected override void TryFire() { }
    protected override void Shoot() { }
    
    // --- When this tower is destroyed, remove all buffs ---
    protected override void OnDestroy()
    {
        base.OnDestroy(); // Unregisters from TowerRegistry
        
        // Remove buffs from all towers we were tracking
        foreach (TowerController tower in _towersInRange)
        {
            if (tower != null)
            {
                tower.RemoveBuff();
            }
        }
        
        // Un-slow all enemies
        foreach(EnemyController enemy in _enemiesInRange)
        {
            if (enemy != null)
            {
                enemy.ApplySpeedModification(1.0f);
            }
        }
    }
}