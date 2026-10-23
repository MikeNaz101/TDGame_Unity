using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shared, allocation-free explosion damage used by tower and player projectiles.
/// Each enemy is damaged once per explosion even if several of its colliders are caught in the blast.
/// </summary>
public static class ExplosionUtility
{
    private static readonly Collider[] HitBuffer = new Collider[64];
    private static readonly HashSet<EnemyController> DamagedThisBlast = new HashSet<EnemyController>();

    private static int _defaultLayer = -1;

    /// <summary>Cached LayerMask.NameToLayer("Default"); the string lookup is slow to repeat every impact.</summary>
    public static int DefaultLayer
    {
        get
        {
            if (_defaultLayer < 0) _defaultLayer = LayerMask.NameToLayer("Default");
            return _defaultLayer;
        }
    }

    public static void DamageEnemiesInRadius(Vector3 center, float radius, float damage, Transform attacker, float force)
    {
        int count = Physics.OverlapSphereNonAlloc(center, radius, HitBuffer, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
        DamagedThisBlast.Clear();

        for (int i = 0; i < count; i++)
        {
            Collider hit = HitBuffer[i];
            if (hit == null || !hit.CompareTag("Enemy")) continue;

            EnemyController enemy = hit.GetComponentInParent<EnemyController>();
            if (enemy == null || !DamagedThisBlast.Add(enemy)) continue;

            enemy.TakeExplosion(damage, attacker, center, force, radius);
        }

        DamagedThisBlast.Clear();
        System.Array.Clear(HitBuffer, 0, count);
    }
}
