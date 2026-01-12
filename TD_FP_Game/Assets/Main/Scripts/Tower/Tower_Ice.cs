using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A utility tower that doesn't attack. It overrides Update()
/// to apply a slow effect to all enemies in its range.
/// </summary>
public class Tower_Ice : TowerController
{
    [Header("Ice Tower Stats")]
    [Tooltip("The speed multiplier. 0.5 = 50% slow, 0.7 = 30% slow.")]
    [SerializeField] private float slowMultiplier = 0.6f;
    
    // We need to keep track of who we are slowing
    private List<EnemyController> _enemiesCurrentlySlowed = new List<EnemyController>();

    protected override void Update()
    {
        // This tower doesn't aim or shoot, so we don't call
        // AimAtTarget() or TryFire(). We just use the list.
        
        // 1. Clean the list of destroyed enemies
        _enemiesInRange.RemoveAll(e => e == null || !e.gameObject.activeInHierarchy);
        
        // 2. Apply slow to all enemies in range
        foreach (EnemyController enemy in _enemiesInRange)
        {
            enemy.ApplySpeedModification(slowMultiplier);
            
            // Add to our list so we can un-slow them later
            if (!_enemiesCurrentlySlowed.Contains(enemy))
            {
                _enemiesCurrentlySlowed.Add(enemy);
            }
        }
        
        // 3. Un-slow enemies that have left the range
        // We do this in reverse to safely remove items from the list
        for (int i = _enemiesCurrentlySlowed.Count - 1; i >= 0; i--)
        {
            EnemyController enemy = _enemiesCurrentlySlowed[i];
            
            if (enemy == null)
            {
                // Enemy was destroyed, remove from list
                _enemiesCurrentlySlowed.RemoveAt(i);
                continue;
            }
            
            if (!_enemiesInRange.Contains(enemy))
            {
                // Enemy left the radius, restore its speed
                enemy.ApplySpeedModification(1.0f);
                _enemiesCurrentlySlowed.RemoveAt(i);
            }
        }
    }

    // We must override these to do nothing,
    // otherwise the base class will try to attack.
    protected override void TryFire() { }
    protected override void Shoot() { }
    
    // When this tower is destroyed, un-slow all enemies
    protected override void OnDestroy()
    {
        base.OnDestroy(); // This unregisters from the TowerRegistry
        
        foreach(EnemyController enemy in _enemiesCurrentlySlowed)
        {
            if (enemy != null)
            {
                enemy.ApplySpeedModification(1.0f);
            }
        }
    }
}