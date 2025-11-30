using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

[RequireComponent(typeof(SphereCollider))]
[RequireComponent(typeof(AudioSource))]
public class TowerController : MonoBehaviour
{
    public enum TargetingPriority { First, Last, Weakest, Strongest }
    
    [Header("Tower Components")]
    [SerializeField] private Transform _turretBase;
    [SerializeField] private Transform _barrel;
    [SerializeField] public Transform _shootPoint;
    
    [Header("Targeting")]
    [SerializeField] private TargetingPriority _targetingPriority = TargetingPriority.First;
    [SerializeField] private float _aimSpeed = 10f;
    [SerializeField] private float _aimTolerance = 3f;

    // --- Properties ---
    public string TowerName { get; private set; }
    public int TowerID { get; private set; }
    public TowerData Data => _towerData;
    
    // We track which upgrade index we are at for each path
    // Key = Path Index, Value = Next Upgrade Index to buy
    public int[] PathProgress { get; private set; }

    // --- References ---
    protected TowerData _towerData;
    protected AudioSource _audioSource;
    protected LineRenderer _hitscanTracer;
    protected PlayerStats _playerStats;
    protected Transform _playerTarget;

    // --- State ---
    protected List<EnemyController> _enemiesInRange = new List<EnemyController>();
    protected EnemyController _currentTarget;
    protected float _fireCooldown = 0f;
    
    // Multipliers (Base 1.0)
    protected float _fireRateMultiplier = 1f; 
    protected float _damageMultiplier = 1f;   
    protected float _rangeMultiplier = 1f;
    
    // Global Multipliers
    protected float _globalDamageMult = 1f;
    protected float _globalRangeMult = 1f;

    private SphereCollider _rangeTrigger;

    protected virtual void Awake()
    {
        _rangeTrigger = GetComponent<SphereCollider>();
        _audioSource = GetComponent<AudioSource>();
        _rangeTrigger.isTrigger = true;
        
        _hitscanTracer = GetComponentInChildren<LineRenderer>();
        if (_hitscanTracer != null) _hitscanTracer.enabled = false;
        
        TowerID = TowerRegistry.RegisterTower(this);
        
        var pObj = GameObject.FindGameObjectWithTag("Player");
        if (pObj) { _playerTarget = pObj.transform; _playerStats = pObj.GetComponent<PlayerStats>(); }
    }

    public void Initialize(TowerData data)
    {
        _towerData = data;
        TowerName = _towerData.towerName;
        
        // Initialize path tracking
        if (_towerData.upgradePaths != null)
        {
            PathProgress = new int[_towerData.upgradePaths.Count];
        }

        RefreshGlobalStats();
    }

    protected virtual void OnDestroy() { TowerRegistry.UnregisterTower(this); }
    
    public void RefreshGlobalStats()
    {
        if (UpgradeManager.Instance != null)
        {
            _globalDamageMult = UpgradeManager.Instance.GlobalTowerDamageMult;
            _globalRangeMult = UpgradeManager.Instance.GlobalTowerRangeMult;
        }
        RecalculateStats();
    }

    // Call this whenever an upgrade is bought
    protected void RecalculateStats()
    {
        if (_rangeTrigger != null && _towerData != null)
        {
            // Base Range * Local Multiplier * Global Multiplier
            _rangeTrigger.radius = _towerData.range * _rangeMultiplier * _globalRangeMult;
        }
    }

    // --- NEW: UPGRADE LOGIC ---
    public void ApplyUpgrade(TowerUpgradeData upgrade)
    {
        switch (upgrade.effectType)
        {
            case TowerUpgradeType.Damage:
                // Add percentage (e.g. +0.25)
                _damageMultiplier += upgrade.effectValue;
                break;
            case TowerUpgradeType.FireRate:
                // Increase speed means reducing cooldown, or just a rate multiplier
                _fireRateMultiplier += upgrade.effectValue;
                break;
            case TowerUpgradeType.Range:
                _rangeMultiplier += upgrade.effectValue;
                break;
            default:
                // Hand off special upgrades to child classes
                ApplySpecificUpgrade(upgrade);
                break;
        }
        RecalculateStats();
    }

    protected virtual void ApplySpecificUpgrade(TowerUpgradeData upgrade)
    {
        // Override in child classes (Flamethrower, Rocket, etc.)
    }

    protected virtual void Update()
    {
        if (_fireCooldown > 0) _fireCooldown -= Time.deltaTime;
        if (_currentTarget == null || !_currentTarget.gameObject.activeInHierarchy) UpdateTarget();
        if (_currentTarget != null) { AimAtTarget(); TryFire(); }
    }

    protected void UpdateTarget()
    {
        _enemiesInRange.RemoveAll(e => e == null || !e.gameObject.activeInHierarchy);
        switch (_targetingPriority)
        {
            case TargetingPriority.Weakest: _enemiesInRange = _enemiesInRange.OrderBy(e => e.CurrentHealth).ToList(); break;
            case TargetingPriority.Strongest: _enemiesInRange = _enemiesInRange.OrderByDescending(e => e.CurrentHealth).ToList(); break;
        }
        _currentTarget = _enemiesInRange.Count > 0 ? _enemiesInRange[0] : null;
    }

    protected virtual void AimAtTarget()
    {
        if (!_turretBase || !_barrel || !_currentTarget) return;
        
        Vector3 targetDir = _currentTarget.transform.position - _turretBase.position;
        Vector3 euler = Quaternion.Slerp(_turretBase.rotation, Quaternion.LookRotation(targetDir), Time.deltaTime * _aimSpeed).eulerAngles;
        _turretBase.rotation = Quaternion.Euler(0f, euler.y, 0f);
        
        Vector3 localTarget = _turretBase.InverseTransformPoint(_currentTarget.transform.position);
        _barrel.localRotation = Quaternion.Slerp(_barrel.localRotation, Quaternion.LookRotation(localTarget), Time.deltaTime * _aimSpeed);
    }

    protected virtual void TryFire()
    {
        if (_fireCooldown > 0 || !_currentTarget || !_towerData) return;
        
        if (Vector3.Angle(_shootPoint.forward, _currentTarget.transform.position - _shootPoint.position) < _aimTolerance)
        {
            Shoot();
            // Calculate final fire rate
            float baseRate = _towerData.fireRate;
            // Higher multiplier = Faster fire = Lower cooldown
            _fireCooldown = baseRate / _fireRateMultiplier;
        }
    }

    protected virtual void Shoot()
    {
        if (_towerData.shootSound) _audioSource.PlayOneShot(_towerData.shootSound);
        
        float finalDmg = _towerData.damage * _damageMultiplier * _globalDamageMult;

        if (_towerData.attackType == TowerData.AttackType.Hitscan)
        {
            _currentTarget.TakeDamage(finalDmg, transform);
            if (_hitscanTracer) StartCoroutine(ShowHitscanTrace());
        }
        else
        {
            if (!_towerData.projectilePrefab) return;
            GameObject proj = Instantiate(_towerData.projectilePrefab, _shootPoint.position, _shootPoint.rotation);
            // Pass multipliers to projectile logic if needed
            var p = proj.GetComponent<Projectile>();
            if(p) { p.towerData = _towerData; p.attacker = transform; p.damageMultiplier = finalDmg / _towerData.damage; }
            
            // Handle specialized projectiles (Rocket)
            var slowRocket = proj.GetComponent<SlowHomingRocket>();
            if(slowRocket) { 
                slowRocket.Initialize(_towerData, _currentTarget, transform); 
                slowRocket.Launch(); // Assuming rocket logic handles launch timing inside if needed
            }
        }
    }

    protected IEnumerator ShowHitscanTrace()
    {
        if (!_currentTarget) yield break;
        _hitscanTracer.enabled = true;
        _hitscanTracer.SetPosition(0, _shootPoint.position);
        _hitscanTracer.SetPosition(1, _currentTarget.transform.position + Vector3.up * 0.5f);
        yield return new WaitForSeconds(0.07f);
        _hitscanTracer.enabled = false;
    }

    protected virtual void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Enemy"))
        {
            var e = other.GetComponent<EnemyController>();
            if (e && !_enemiesInRange.Contains(e)) _enemiesInRange.Add(e);
        }
    }
    protected virtual void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Enemy"))
        {
            var e = other.GetComponent<EnemyController>();
            if (e) { _enemiesInRange.Remove(e); if (_currentTarget == e) _currentTarget = null; }
        }
    }

    // --- UI HELPERS ---
    public void SetTargetingPriority(int index) { _targetingPriority = (TargetingPriority)index; UpdateTarget(); }
    public string GetPriorityName() => _targetingPriority.ToString();
    public void CyclePriority() { SetTargetingPriority(((int)_targetingPriority + 1) % 4); }
    public void SellTower() { Destroy(gameObject); }
    public int GetSellValue() { return Mathf.RoundToInt(_towerData.scrapCost * 0.5f); } // Simplified
    
    // --- BUFFS ---
    public void ApplyBuff(float rate, float dmg) { _fireRateMultiplier = rate; _damageMultiplier = dmg; }
    public void RemoveBuff() { _fireRateMultiplier = 1f; _damageMultiplier = 1f; }
}