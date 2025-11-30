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
        // 1. Handle Target Logic (Base class)
        if (_currentTarget == null || !_currentTarget.gameObject.activeInHierarchy)
        {
            UpdateTarget();
            // If still no target, stop firing (but don't reset recharge completely if currently recharging)
            if (_currentTarget == null)
            {
                if (_flameState == FlameState.Firing) StopFiring();
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
                ApplyFlameDamage(); // Deal damage every frame while firing
                
                if (_timer <= 0f)
                {
                    StartRecharge();
                }
                break;

            case FlameState.Recharging:
                _timer -= Time.deltaTime;
                if (_timer <= 0f)
                {
                    _flameState = FlameState.Ready;
                }
                break;
        }
    }

    private void StartFiring()
    {
        _flameState = FlameState.Firing;
        _timer = fireDuration;

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

    private void ApplyFlameDamage()
    {
        // Calculate Damage per second
        // Base Damage * Upgrade Mult * Global Mult
        float dps = _towerData.damage * _damageMultiplier * _globalDamageMult;
        
        // We iterate backwards to safely handle removals if an enemy dies
        for (int i = _enemiesInRange.Count - 1; i >= 0; i--)
        {
            EnemyController enemy = _enemiesInRange[i];
            
            if (enemy == null || !enemy.gameObject.activeInHierarchy)
            {
                _enemiesInRange.RemoveAt(i);
                continue;
            }

            // Check Cone Area
            Vector3 dirToEnemy = (enemy.transform.position - _shootPoint.position).normalized;
            float angle = Vector3.Angle(_shootPoint.forward, dirToEnemy);
            float dist = Vector3.Distance(_shootPoint.position, enemy.transform.position);

            // Get current range (Base * Multipliers)
            // We can read the sphere collider radius since Base class updates it
            float currentMaxRange = GetComponent<SphereCollider>().radius; 

            if (dist <= currentMaxRange && angle <= (flameAngle / 2f))
            {
                // Deal Direct Fire Damage
                enemy.TakeDamage(dps * Time.deltaTime, transform);

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
        burn.StartBurn(burnDuration, burnDamage * _damageMultiplier * _globalDamageMult);
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
    private float _timer;
    private float _dps;
    private EnemyController _target;

    public void StartBurn(float duration, float damagePerSecond)
    {
        _timer = duration;
        _dps = damagePerSecond;
        if (_target == null) _target = GetComponent<EnemyController>();
    }

    void Update()
    {
        if (_timer > 0)
        {
            _timer -= Time.deltaTime;
            if (_target != null && _target.gameObject.activeInHierarchy)
            {
                _target.TakeDamage(_dps * Time.deltaTime);
            }
            else
            {
                Destroy(this);
            }
        }
        else
        {
            Destroy(this);
        }
    }
}