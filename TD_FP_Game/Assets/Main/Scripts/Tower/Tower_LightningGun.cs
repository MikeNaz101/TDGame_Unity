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

    [Tooltip("Upgrades can't push paralyze chance above this.")]
    public float maxParalyzeChance = 0.9f;

    private bool _isCharging = false;
    private LineRenderer _boltRenderer;
    private WaitForSeconds _strikeWait;
    private WaitForSeconds _paralyzeWait;
    private static readonly WaitForSeconds BoltVisibleWait = new WaitForSeconds(0.2f);

    protected override void Awake()
    {
        base.Awake();
        
        // FIX: Don't disable the game object. 
        // Just make sure it's in Manual Mode so it doesn't fire until we say so.
        if (downwardLightning != null)
        {
            downwardLightning.ManualMode = true;
            _boltRenderer = downwardLightning.GetComponent<LineRenderer>();
            if (_boltRenderer != null) _boltRenderer.enabled = false;
        }

        _strikeWait = new WaitForSeconds(strikeDelay);
        _paralyzeWait = new WaitForSeconds(paralyzeDuration);

        // Initialize Charge Particles
        if (chargeParticles != null)
        {
            chargeParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = chargeParticles.main;
            main.startSize = 0.1f; 
        }
    }

    protected override void ApplySpecificUpgrade(TowerUpgradeData upgrade)
    {
        if (upgrade.effectType == TowerUpgradeType.Lightning_ParalyzeChance)
        {
            paralyzeChance = Mathf.Min(maxParalyzeChance, paralyzeChance + upgrade.effectValue);
        }
    }

    protected override void Update()
    {
        if (_fireCooldown > 0) _fireCooldown -= Time.deltaTime;
        UpdateRecoil();

        if (!IsValidTarget(_currentTarget))
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
            float totalRate = CurrentFireInterval;
            if (totalRate > 0.01f)
            {
                // Ease-in so the charge builds slowly then swells right before the strike.
                float linear = 1.0f - Mathf.Clamp01(_fireCooldown / totalRate);
                chargePercent = linear * linear;
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

        float range = CurrentRange;
        if ((_currentTarget.transform.position - transform.position).sqrMagnitude <= range * range)
        {
            StartCoroutine(PerformLightningStrike(_currentTarget));
            _fireCooldown = CurrentFireInterval;
            ResetChargeEffect(); 
        }
    }

    private IEnumerator PerformLightningStrike(EnemyController target)
    {
        if (_towerData.shootSound != null) _audioSource.PlayOneShot(_towerData.shootSound);

        yield return _strikeWait;

        if (IsValidTarget(target))
        {
            float damage = CurrentDamage;
            PlayFireFeedback(false); // the ShootPoint particles here are the charge effect, not a muzzle flash

            if (downwardLightning != null)
            {
                if (_boltRenderer != null) _boltRenderer.enabled = true;
                
                downwardLightning.StartObject = null;
                downwardLightning.EndObject = null;
                
                Vector3 enemyPos = target.transform.position;
                Vector3 skyPos = enemyPos + (Vector3.up * 50f); 
                
                downwardLightning.StartPosition = skyPos;
                downwardLightning.EndPosition = enemyPos;
                
                // Fire!
                downwardLightning.Trigger(); 
            }

            target.TakeDamage(damage, transform, DamageType.Electric);

            if (Random.value < paralyzeChance)
            {
                // Run on the enemy so the paralysis still ends if this tower is sold mid-effect
                target.StartCoroutine(ParalyzeEnemy(target));
            }
            
            // Wait for duration of bolt
            yield return BoltVisibleWait;
            
            if (_boltRenderer != null) _boltRenderer.enabled = false;
        }
    }

    private IEnumerator ParalyzeEnemy(EnemyController enemy)
    {
        // Each paralysis gets its own key, so an Ice tower releasing its slow
        // (or a second strike ending early) can't cancel this one.
        object paralyzeKey = new object();
        enemy.SetSpeedModifier(paralyzeKey, 0f);
        yield return _paralyzeWait;
        if (enemy != null) enemy.ClearSpeedModifier(paralyzeKey);
    }

    private void RotateBlades(float speedMult)
    {
        float speed = rotationSpeed * speedMult * Time.deltaTime;
        if (go_InnerBlade != null) go_InnerBlade.Rotate(0, speed * -1, 0);
        if (go_OuterBlade != null) go_OuterBlade.Rotate(0, speed, 0);
    }

    protected override void Shoot() { }
}