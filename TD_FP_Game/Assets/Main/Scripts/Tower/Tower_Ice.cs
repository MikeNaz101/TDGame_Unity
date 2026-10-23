using UnityEngine;

/// <summary>
/// A utility tower that doesn't attack. Every enemy inside its range is slowed.
/// The slow is applied once on enter and removed on exit, and it stacks correctly
/// with other slows (strongest wins) instead of being overwritten every frame.
/// </summary>
public class Tower_Ice : TowerController
{
    [Header("Ice Tower Stats")]
    [Tooltip("The speed multiplier. 0.5 = 50% slow, 0.7 = 30% slow.")]
    [SerializeField] private float slowMultiplier = 0.6f;
    [Tooltip("Upgrades can't slow enemies below this multiplier.")]
    [SerializeField] private float minSlowMultiplier = 0.2f;

    [Header("Ice Visuals")]
    [Tooltip("Optional part that spins slowly while enemies are being chilled (e.g. a crystal).")]
    [SerializeField] private Transform spinningPart;
    [SerializeField] private float spinSpeed = 45f;

    protected override void Update()
    {
        // Doesn't aim or shoot. Dropping dead/destroyed enemies also releases their slow.
        CleanEnemyList();
        UpdateRecoil();

        if (spinningPart != null)
        {
            float speed = _enemiesInRange.Count > 0 ? spinSpeed * 3f : spinSpeed;
            spinningPart.Rotate(0f, speed * Time.deltaTime, 0f, Space.Self);
        }
    }

    protected override void ApplySpecificUpgrade(TowerUpgradeData upgrade)
    {
        if (upgrade.effectType != TowerUpgradeType.Ice_SlowStrength) return;

        slowMultiplier = Mathf.Max(minSlowMultiplier, slowMultiplier - upgrade.effectValue);
        // Refresh enemies already being slowed so the upgrade is felt immediately.
        foreach (EnemyController enemy in _enemiesInRange)
        {
            if (enemy != null) enemy.SetSpeedModifier(this, slowMultiplier);
        }
    }

    protected override void OnEnemyEntered(EnemyController enemy) { enemy.SetSpeedModifier(this, slowMultiplier); }
    protected override void OnEnemyExited(EnemyController enemy) { if (enemy != null) enemy.ClearSpeedModifier(this); }

    // We must override these to do nothing, otherwise the base class will try to attack.
    protected override void TryFire() { }
    protected override void Shoot() { }
    
    // When this tower is destroyed (or sold), un-slow all enemies
    protected override void OnDestroy()
    {
        base.OnDestroy();
        foreach (EnemyController enemy in _enemiesInRange)
        {
            if (enemy != null) enemy.ClearSpeedModifier(this);
        }
    }
}
