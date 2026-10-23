using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// A custom tower that fires in bursts and deals damage in a cone.
/// Includes support for specific upgrades like Wider Flames and Burn DoT.
/// </summary>
public class Tower_Flamethrower : TowerController
{
    [Header("Flamethrower Stats")]
    [Tooltip("The angle of the flame cone (e.g., 30 degrees).")]
    public float flameAngle = 30f;
    
    [Header("Flamethrower Overheat")]
    [Tooltip("How long the tower fires for.")]
    [SerializeField] private float fireDuration = 5f;
    [Tooltip("How long the tower must recharge after firing.")]
    [SerializeField] private float rechargeDuration = 5f;

    [Header("Upgrades")]
    public float burnDuration = 0f; // 0 = No burn, >0 = Seconds
    public float burnDamage = 5f;   // Damage per second for the burn

    [Header("Performance")]
    [Tooltip("Flame damage is dealt in ticks instead of every frame. Same DPS, far fewer hit events.")]
    [SerializeField] private float damageTickInterval = 0.2f;
    private float _tickTimer = 0f;

    // State Machine
    private enum FlameState { Ready, Firing, Recharging }
    private FlameState _flameState = FlameState.Ready;
    private float _timer = 0f;

    // Visuals
    private ParticleSystem[] allFlameParticles;

    protected override void Awake()
    {
        base.Awake(); // Setup base tower logic
        
        // Find particles on the shoot point
        if (_shootPoint != null)
        {
            allFlameParticles = _shootPoint.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var ps in allFlameParticles)
            {
                var main = ps.main;
                main.loop = true;
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }
    }

    // --- UPGRADE LOGIC ---
    protected override void ApplySpecificUpgrade(TowerUpgradeData upgrade)
    {
        switch (upgrade.effectType)
        {
            case TowerUpgradeType.Flame_Width:
                // Increase cone width by percentage (e.g. 0.5 = +50%)
                flameAngle *= (1f + upgrade.effectValue); 
                Debug.Log($"Flamethrower: Flames widened to {flameAngle} degrees!");
                break;
                
            case TowerUpgradeType.Flame_BurnDot:
                // Enable burning (value is duration, e.g. 5.0)
                burnDuration = upgrade.effectValue; 
                Debug.Log("Flamethrower: Napalm loaded!");
                break;
        }
    }

    protected override void Update()
    {
        UpdateRecoil();

        // Cool down even with no target in range (it used to freeze until an enemy showed up)
        if (_flameState == FlameState.Recharging)
        {
            _timer -= Time.deltaTime;
            if (_timer <= 0f) _flameState = FlameState.Ready;
        }

        if (!IsValidTarget(_currentTarget))
        {
            UpdateTarget();
            if (_currentTarget == null)
            {
                if (_flameState == FlameState.Firing) StopFiring();
                
                // --- FIX: Call the scan ---
                PerformIdleScan(); 
                // --------------------------
                return;
            }
        }

        // 2. Aim
        AimAtTarget();

        // 3. Firing State Machine
        switch (_flameState)
        {
            case FlameState.Ready:
                // If we have a target, start blasting
                if (_currentTarget != null) StartFiring();
                break;

            case FlameState.Firing:
                _timer -= Time.deltaTime;
                _tickTimer -= Time.deltaTime;
                if (_tickTimer <= 0f)
                {
                    _tickTimer += damageTickInterval;
                    ApplyFlameDamage(damageTickInterval);
                }
                
                if (_timer <= 0f)
                {
                    StartRecharge();
                }
                break;

            case FlameState.Recharging:
                break; // handled above
        }
    }

    private void StartFiring()
    {
        _flameState = FlameState.Firing;
        _timer = fireDuration;
        _tickTimer = 0f;

        if (allFlameParticles != null)
        {
            foreach (var ps in allFlameParticles) ps.Play();
        }
        
        if (_towerData.shootSound && _audioSource)
        {
            _audioSource.clip = _towerData.shootSound;
            _audioSource.loop = true;
            _audioSource.Play();
        }
    }

    private void StopFiring()
    {
        // If we lose target mid-fire, we usually want to stop visuals 
        // but maybe keep the heat (timer) running or switch to recharge.
        if (_flameState == FlameState.Firing) 
        {
            StartRecharge();
        }
    }

    private void StartRecharge()
    {
        _flameState = FlameState.Recharging;
        _timer = rechargeDuration;

        if (allFlameParticles != null)
        {
            foreach (var ps in allFlameParticles) ps.Stop();
        }

        if (_audioSource.isPlaying) _audioSource.Stop();
    }

    private void ApplyFlameDamage(float tickDuration)
    {
        // Damage per second: Base Damage * all multipliers (upgrades, global, radar buffs)
        float tickDamage = CurrentDamage * tickDuration;
        float maxRange = CurrentRange;
        float maxRangeSqr = maxRange * maxRange;
        float halfAngle = flameAngle * 0.5f;
        Vector3 origin = _shootPoint.position;
        Vector3 forward = _shootPoint.forward;

        CleanEnemyList();

        // Iterate backwards: an enemy can die (and be removed) mid-loop
        for (int i = _enemiesInRange.Count - 1; i >= 0; i--)
        {
            if (i >= _enemiesInRange.Count) continue;
            EnemyController enemy = _enemiesInRange[i];
            if (!IsValidTarget(enemy)) continue;

            Vector3 toEnemy = GetAimPoint(enemy) - origin;
            if (toEnemy.sqrMagnitude <= maxRangeSqr && Vector3.Angle(forward, toEnemy) <= halfAngle)
            {
                enemy.TakeDamage(tickDamage, transform, DamageType.Fire);

                // Apply Burn DoT if unlocked
                if (burnDuration > 0)
                {
                    ApplyBurn(enemy);
                }
            }
        }
    }

    private void ApplyBurn(EnemyController enemy)
    {
        // Check if enemy already has the burn component
        BurnDebuff burn = enemy.GetComponent<BurnDebuff>();
        if (burn == null)
        {
            burn = enemy.gameObject.AddComponent<BurnDebuff>();
        }
        
        // Refresh/Apply duration
        // Burn damage scales with your tower's damage upgrades too
        burn.StartBurn(burnDuration, burnDamage * DamageMultiplier, transform);
    }

    // --- Override base methods we don't use ---
    // We override these to be empty because we handle firing in Update()
    protected override void TryFire() { } 
    protected override void Shoot() { }   
}

// --- Nested Helper Component for DoT ---
// This sits on the ENEMY and hurts them over time
public class BurnDebuff : MonoBehaviour
{
    private const float TICK_INTERVAL = 0.25f;

    private float _timer;
    private float _dps;
    private float _tickTimer;
    private Transform _attacker;
    private EnemyController _target;

    public void StartBurn(float duration, float damagePerSecond, Transform attacker = null)
    {
        _timer = duration;
        // Re-applying refreshes the duration and keeps the strongest burn.
        _dps = Mathf.Max(_dps, damagePerSecond);
        _attacker = attacker;
        if (_target == null) _target = GetComponent<EnemyController>();
    }

    void Update()
    {
        if (_timer <= 0f || _target == null || _target._isDead || !_target.gameObject.activeInHierarchy)
        {
            Destroy(this);
            return;
        }

        _timer -= Time.deltaTime;
        _tickTimer += Time.deltaTime;
        if (_tickTimer >= TICK_INTERVAL)
        {
            // Burn is fire damage, so armor/weakness rules apply (it used to count as Physical).
            _target.TakeDamage(_dps * _tickTimer, _attacker, DamageType.Fire);
            _tickTimer = 0f;
        }
    }
}