using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// A custom tower that fires in bursts (5s on, 5s off) and
/// deals damage in a cone to enemies.
/// </summary>
public class Tower_Flamethrower : TowerController
{
    [Header("Flamethrower Stats")]
    [Tooltip("The angle of the flame cone (e.g., 30 degrees).")]
    [SerializeField] private float flameAngle = 30f;
    
    [Header("Flamethrower Overheat")]
    [Tooltip("How long the tower fires for.")]
    [SerializeField] private float fireDuration = 5f;
    [Tooltip("How long the tower must recharge after firing.")]
    [SerializeField] private float rechargeDuration = 5f;

    [Header("Debug")]
    [Tooltip("Enable this to see per-frame updates in the log.")]
    [SerializeField] private bool _logSpammyUpdates = false;

    // We store an array of ALL particle systems
    private ParticleSystem[] allFlameParticles;

    // New State Machine
    private enum FlameState { Ready, Firing, Recharging }
    private FlameState _flameState = FlameState.Ready;
    private float _fireTimer = 0f;
    private float _rechargeTimer = 0f;

    protected override void Awake()
    {
        base.Awake(); // Calls TowerController.Awake()
        
        Debug.Log($"--- FLAMETHROWER AWAKE: {gameObject.name} ---");

        if (_shootPoint != null)
        {
            Debug.Log($"[Awake] Found _shootPoint: {_shootPoint.name}");
            
            // Get ALL particle systems on the _shootPoint and all its children
            allFlameParticles = _shootPoint.GetComponentsInChildren<ParticleSystem>(true); // 'true' includes inactive

            if (allFlameParticles != null && allFlameParticles.Length > 0)
            {
                Debug.Log($"[Awake] SUCCESS: Found {allFlameParticles.Length} particle systems!");
                
                foreach (var ps in allFlameParticles)
                {
                    var main = ps.main;
                    main.playOnAwake = false;
                    main.loop = true; // We will control the loop
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    Debug.Log($"[Awake] -- Configured {ps.name} to loop.");
                }
            }
            else
            {
                Debug.LogError($"[Awake] FAILURE: No ParticleSystem components found on '{_shootPoint.name}' or any of its children!");
            }
        }
        else
        {
            Debug.LogError("[Awake] FAILURE: The '_shootPoint' is not assigned in the Inspector!");
        }
    }

    protected override void Update()
    {
        if (_currentTarget == null || !_currentTarget.gameObject.activeInHierarchy)
        {
            UpdateTarget(); 
            if (_flameState == FlameState.Firing)
            {
                if (_logSpammyUpdates) Debug.Log("[Update] Target lost while firing. Starting recharge.");
                StartRecharge();
            }
        }

        if (_currentTarget == null)
        {
            if (_logSpammyUpdates) Debug.Log("[Update] No target. Waiting.");
            if (_flameState != FlameState.Recharging) StopFiringEffects();
            return;
        }

        AimAtTarget();
        if (_logSpammyUpdates) Debug.Log($"[Update] Aiming at {_currentTarget.name}. Current state: {_flameState}");
        
        switch (_flameState)
        {
            case FlameState.Ready:
                if (_logSpammyUpdates) Debug.Log("[Update] State is Ready. Calling StartFiring().");
                StartFiring();
                break;
                
            case FlameState.Firing:
                _fireTimer -= Time.deltaTime;
                if (_fireTimer <= 0f)
                {
                    if (_logSpammyUpdates) Debug.Log("[Update] Firing timer up. Calling StartRecharge().");
                    StartRecharge();
                }
                else
                {
                    if (_logSpammyUpdates) Debug.Log($"[Update] Firing. Time left: {_fireTimer}");
                    ApplyFlameDamage();
                }
                break;
                
            case FlameState.Recharging:
                _rechargeTimer -= Time.deltaTime;
                if (_logSpammyUpdates) Debug.Log($"[Update] Recharging. Time left: {_rechargeTimer}");
                if (_rechargeTimer <= 0f)
                {
                    if (_logSpammyUpdates) Debug.Log("[Update] Recharge complete. Setting state to Ready.");
                    _flameState = FlameState.Ready;
                }
                break;
        }
    }

    private void StartFiring()
    {
        Debug.Log("--- StartFiring() CALLED ---");
        _flameState = FlameState.Firing;
        _fireTimer = fireDuration;
        
        if (allFlameParticles != null && allFlameParticles.Length > 0)
        {
            Debug.Log($"[StartFiring] Playing all {allFlameParticles.Length} particle systems!");
            foreach (var ps in allFlameParticles)
            {
                var main = ps.main;
                main.loop = true; // Ensure loop is on
                ps.Play();
            }
        }
        
        if (_towerData.shootSound != null)
        {
            _audioSource.clip = _towerData.shootSound;
            _audioSource.loop = true;
            _audioSource.Play();
        }
    }

    private void StartRecharge()
    {
        Debug.Log("--- StartRecharge() CALLED ---");
        _flameState = FlameState.Recharging;
        _rechargeTimer = rechargeDuration;
        StopFiringEffects();
    }

    private void StopFiringEffects()
    {
        Debug.Log("--- StopFiringEffects() CALLED ---");
        
        if (allFlameParticles != null && allFlameParticles.Length > 0)
        {
            Debug.Log($"[StopFiringEffects] Stopping all {allFlameParticles.Length} particle systems.");
            foreach (var ps in allFlameParticles)
            {
                var main = ps.main;
                main.loop = false; // Turn loop off
                ps.Stop();
            }
        }
        
        if (_audioSource.isPlaying)
        {
            _audioSource.Stop();
        }
    }
    
    private void ApplyFlameDamage()
    {
        if (_towerData == null) return;
        
        float dps = _towerData.damage;
        float range = _towerData.range;
        
        foreach (EnemyController enemy in _enemiesInRange)
        {
            if (enemy == null) continue;
            
            float dist = Vector3.Distance(_shootPoint.position, enemy.transform.position);
            Vector3 dirToEnemy = (enemy.transform.position - _shootPoint.position).normalized;
            float angleToEnemy = Vector3.Angle(_shootPoint.forward, dirToEnemy);

            if (dist <= range && angleToEnemy <= (flameAngle / 2f))
            {
                if (_logSpammyUpdates) Debug.Log($"[ApplyFlameDamage] Damaging {enemy.name}");
                enemy.TakeDamage(dps * Time.deltaTime, transform);
            }
        }
    }

    protected override void TryFire() { }
    protected override void Shoot() { }
}