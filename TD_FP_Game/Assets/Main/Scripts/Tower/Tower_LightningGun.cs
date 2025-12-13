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
        
        // FIX: Don't disable the game object. 
        // Just make sure it's in Manual Mode so it doesn't fire until we say so.
        if (downwardLightning != null)
        {
            downwardLightning.ManualMode = true;
            // We can disable the LineRenderer to hide it initially
            var lr = downwardLightning.GetComponent<LineRenderer>();
            if(lr != null) lr.enabled = false;
        }

        // Initialize Charge Particles
        if (chargeParticles != null)
        {
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
                
                if (_isCharging) ResetChargeEffect();
                return;
            }
        }

        AimAtTarget(); 
        HandleChargeEffect();
        TryFire();
        RotateBlades(1.0f); 
    }

    private void HandleChargeEffect()
    {
        if (chargeParticles == null) return;

        if (!_isCharging)
        {
            _isCharging = true;
            chargeParticles.Play();
        }

        float chargePercent = 1.0f;
        if (_towerData != null && _towerData.fireRate > 0)
        {
            float totalRate = _towerData.fireRate / _fireRateMultiplier;
            if(totalRate > 0.01f)
            {
                chargePercent = 1.0f - Mathf.Clamp01(_fireCooldown / totalRate);
            }
        }

        var main = chargeParticles.main;
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
                // Enable the Line Renderer so it can be seen
                var lr = downwardLightning.GetComponent<LineRenderer>();
                if(lr != null) lr.enabled = true;
                
                downwardLightning.StartObject = null;
                downwardLightning.EndObject = null;
                
                Vector3 enemyPos = target.transform.position;
                Vector3 skyPos = enemyPos + (Vector3.up * 50f); 
                
                downwardLightning.StartPosition = skyPos;
                downwardLightning.EndPosition = enemyPos;
                
                // Fire!
                downwardLightning.Trigger(); 
            }

            //target.TakeDamage(damage, transform);
            target.TakeDamage(damage, transform, DamageType.Electric);

            if (Random.value < paralyzeChance)
            {
                StartCoroutine(ParalyzeEnemy(target));
            }
            
            // Wait for duration of bolt
            yield return new WaitForSeconds(0.2f);
            
            // Hide it again
            if (downwardLightning != null)
            {
                var lr = downwardLightning.GetComponent<LineRenderer>();
                if(lr != null) lr.enabled = false;
            }
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