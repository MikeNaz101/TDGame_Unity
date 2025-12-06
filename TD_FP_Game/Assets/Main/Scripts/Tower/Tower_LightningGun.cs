using UnityEngine;
using System.Collections;
using DigitalRuby.LightningBolt; 

public class Tower_LightningGun : TowerController
{
    [Header("Lightning Visuals")]
    public Transform go_OuterBlade;
    public Transform go_InnerBlade;
    public float rotationSpeed = 100f;

    [Header("Strike Settings")]
    [Tooltip("Delay before the lightning strikes down (simulating travel time or charge).")]
    public float strikeDelay = 0.2f;
    
    public float paralyzeChance = 0.3f;
    public float paralyzeDuration = 2.0f;

    [Header("Effects")]
    [Tooltip("The 'Strike' beam coming down. Assign the LightningBoltScript component.")]
    public LightningBoltScript downwardLightning; 

    // --- Charge Effect ---
    [Tooltip("Particle System for the charging buildup.")]
    public ParticleSystem chargeParticles;
    [Tooltip("Max size of the charge particles right before firing.")]
    public float maxChargeSize = 2.0f;
    // ---------------------

    private bool _isCharging = false;

    protected override void Awake()
    {
        base.Awake();
        
        if (downwardLightning != null)
        {
            downwardLightning.ManualMode = true;
            downwardLightning.gameObject.SetActive(false);
        }

        // Initialize Charge Particles
        if (chargeParticles != null)
        {
            // Ensure it's not playing on awake
            chargeParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = chargeParticles.main;
            main.startSize = 0.1f; 
        }
    }

    protected override void Update()
    {
        if (_fireCooldown > 0) _fireCooldown -= Time.deltaTime;

        if (_currentTarget == null || !_currentTarget.gameObject.activeInHierarchy)
        {
            UpdateTarget();
            if (_currentTarget == null)
            {
                PerformIdleScan(); 
                RotateBlades(0.2f);
                
                // If we lose target, stop charging immediately
                if (_isCharging) ResetChargeEffect();
                return;
            }
        }

        AimAtTarget(); 
        
        // Handle Charge Visuals while we have a target
        HandleChargeEffect();

        TryFire();
        RotateBlades(1.0f); 
    }

    private void HandleChargeEffect()
    {
        if (chargeParticles == null) return;

        // Start playing if not already
        if (!_isCharging)
        {
            _isCharging = true;
            chargeParticles.Play();
        }

        // Calculate charge progress based on cooldown
        // If cooldown is 0, we are at 100% charge (max size)
        // If cooldown is full, we are at 0% charge
        float chargePercent = 1.0f;
        
        if (_towerData != null && _towerData.fireRate > 0)
        {
            float totalRate = _towerData.fireRate / _fireRateMultiplier;
            // Prevent divide by zero if rate is super fast
            if(totalRate > 0.01f)
            {
                chargePercent = 1.0f - Mathf.Clamp01(_fireCooldown / totalRate);
            }
        }

        // Apply size
        var main = chargeParticles.main;
        // Use curve mode for smoother visual if needed, but constant is fine for frame updates
        main.startSize = Mathf.Lerp(0.1f, maxChargeSize, chargePercent);
    }

    private void ResetChargeEffect()
    {
        _isCharging = false;
        if (chargeParticles != null)
        {
            chargeParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = chargeParticles.main;
            main.startSize = 0.1f;
        }
    }

    protected override void TryFire()
    {
        if (_fireCooldown > 0 || _currentTarget == null || _towerData == null) return;

        float dist = Vector3.Distance(transform.position, _currentTarget.transform.position);
        if (dist <= _towerData.range)
        {
            StartCoroutine(PerformLightningStrike(_currentTarget));
            
            float finalFireRate = _towerData.fireRate / (_fireRateMultiplier);
            _fireCooldown = finalFireRate;
            
            // Visual reset happens here
            ResetChargeEffect(); 
        }
    }

    private IEnumerator PerformLightningStrike(EnemyController target)
    {
        if (_towerData.shootSound != null) _audioSource.PlayOneShot(_towerData.shootSound);

        yield return new WaitForSeconds(strikeDelay);

        if (target != null && target.gameObject.activeInHierarchy)
        {
            float damage = _towerData.damage * _damageMultiplier * _globalDamageMult;

            if (downwardLightning != null)
            {
                downwardLightning.gameObject.SetActive(true);
                
                // IMPORTANT: Wait one frame for the LightningBoltScript Start() to run
                // if it hasn't already. This prevents NullReferenceException.
                yield return null; 
                
                downwardLightning.StartObject = null;
                downwardLightning.EndObject = null;
                
                Vector3 enemyPos = target.transform.position;
                Vector3 skyPos = enemyPos + (Vector3.up * 50f); 
                
                downwardLightning.StartPosition = skyPos;
                downwardLightning.EndPosition = enemyPos;
                
                downwardLightning.Trigger(); 
            }

            target.TakeDamage(damage, transform);

            if (Random.value < paralyzeChance)
            {
                StartCoroutine(ParalyzeEnemy(target));
            }
            
            yield return new WaitForSeconds(0.2f);
            
            if (downwardLightning != null) downwardLightning.gameObject.SetActive(false);
        }
    }

    private IEnumerator ParalyzeEnemy(EnemyController enemy)
    {
        enemy.ApplySpeedModification(0f); 
        yield return new WaitForSeconds(paralyzeDuration);
        if (enemy != null) enemy.ApplySpeedModification(1.0f);
    }

    private void RotateBlades(float speedMult)
    {
        float speed = rotationSpeed * speedMult * Time.deltaTime;
        if (go_InnerBlade != null) go_InnerBlade.Rotate(0, speed * -1, 0);
        if (go_OuterBlade != null) go_OuterBlade.Rotate(0, speed, 0);
    }

    protected override void Shoot() { }
}