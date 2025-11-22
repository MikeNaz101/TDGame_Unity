using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

[RequireComponent(typeof(SphereCollider))]
[RequireComponent(typeof(AudioSource))]
public class TowerController : MonoBehaviour
{
    public enum TargetingPriority
    {
        First,      
        Last,       
        Weakest,    
        Strongest   
    }
    
    [Header("Tower Components")]
    [SerializeField] private Transform _turretBase;
    [SerializeField] private Transform _barrel;
    [SerializeField] public Transform _shootPoint;
    
    [Header("Targeting")]
    [SerializeField] private TargetingPriority _targetingPriority = TargetingPriority.First;

    [Header("Tuning")]
    [SerializeField] private float _aimSpeed = 10f;
    [SerializeField] private float _aimTolerance = 3f;

    // --- Public Properties ---
    [HideInInspector] public string TowerName { get; private set; }
    [HideInInspector] public int TowerID { get; private set; }
    [HideInInspector] public int Level { get; private set; } = 1;

    // --- Protected References ---
    protected TowerData _towerData;
    protected AudioSource _audioSource;
    protected LineRenderer _hitscanTracer;
    protected PlayerStats _playerStats;
    protected Transform _playerTarget;

    // --- Target & State ---
    protected List<EnemyController> _enemiesInRange = new List<EnemyController>();
    protected EnemyController _currentTarget;
    protected float _fireCooldown = 0f;
    
    // Multipliers
    protected float _fireRateMultiplier = 1f; // From Buffs (Radar)
    protected float _damageMultiplier = 1f;   // From Buffs (Radar)
    protected float _upgradeMultiplier = 1f;  // From Levels (Individual Upgrade)
    
    // --- NEW: GLOBAL MULTIPLIERS (From UpgradeManager) ---
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
        
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            _playerTarget = playerObj.transform;
            _playerStats = playerObj.GetComponent<PlayerStats>();
        }
    }

    protected virtual void Start()
    {
        // Check for global upgrades when created
        RefreshGlobalStats();
    }

    // --- NEW: Called by UpgradeManager to update stats dynamically ---
    public void RefreshGlobalStats()
    {
        if (UpgradeManager.Instance != null)
        {
            _globalDamageMult = UpgradeManager.Instance.GlobalTowerDamageMult;
            _globalRangeMult = UpgradeManager.Instance.GlobalTowerRangeMult;
            
            // Apply Range Update immediately
            if (_rangeTrigger != null && _towerData != null)
            {
                _rangeTrigger.radius = _towerData.range * _globalRangeMult;
            }
        }
    }

    public void Initialize(TowerData data)
    {
        _towerData = data;
        _rangeTrigger.radius = _towerData.range;
        TowerName = _towerData.towerName;
        Level = 1;
        
        // Apply global stats immediately upon initialization
        RefreshGlobalStats();
    }

    protected virtual void OnDestroy()
    {
        TowerRegistry.UnregisterTower(this);
    }
    
    protected virtual void Update()
    {
        if (_fireCooldown > 0) _fireCooldown -= Time.deltaTime;

        if (_currentTarget == null || !_currentTarget.gameObject.activeInHierarchy)
        {
            UpdateTarget();
        }

        if (_currentTarget != null)
        {
            AimAtTarget();
            TryFire();
        }
    }

    protected void UpdateTarget()
    {
        _enemiesInRange.RemoveAll(enemy => enemy == null || !enemy.gameObject.activeInHierarchy);

        switch (_targetingPriority)
        {
            case TargetingPriority.Weakest:
                _enemiesInRange = _enemiesInRange.OrderBy(e => e.CurrentHealth).ToList();
                break;
            case TargetingPriority.Strongest:
                _enemiesInRange = _enemiesInRange.OrderByDescending(e => e.CurrentHealth).ToList();
                break;
        }

        if (_enemiesInRange.Count > 0) _currentTarget = _enemiesInRange[0];
        else _currentTarget = null;
    }

    protected virtual void AimAtTarget()
    {
        if (_turretBase == null || _barrel == null || _currentTarget == null) return;

        Vector3 targetDir = _currentTarget.transform.position - _turretBase.position;
        Quaternion lookRotation = Quaternion.LookRotation(targetDir);
        Vector3 euler = Quaternion.Slerp(_turretBase.rotation, lookRotation, Time.deltaTime * _aimSpeed).eulerAngles;

        _turretBase.rotation = Quaternion.Euler(0f, euler.y, 0f);
        
        Vector3 localTargetPos = _turretBase.InverseTransformPoint(_currentTarget.transform.position);
        Quaternion barrelRotation = Quaternion.LookRotation(localTargetPos);
        _barrel.localRotation = Quaternion.Slerp(_barrel.localRotation, barrelRotation, Time.deltaTime * _aimSpeed);
    }

    protected virtual void TryFire()
    {
        if (_fireCooldown > 0 || _currentTarget == null || _towerData == null) return;

        Vector3 targetDir = _currentTarget.transform.position - _shootPoint.position;
        if (Vector3.Angle(_shootPoint.forward, targetDir) < _aimTolerance)
        {
            Shoot();
            // Combined cooldown logic: Data * Buffs
            float finalFireRate = _towerData.fireRate / (_fireRateMultiplier); 
            _fireCooldown = finalFireRate;
        }
    }

    protected virtual void Shoot()
    {
        if (_towerData.shootSound != null) _audioSource.PlayOneShot(_towerData.shootSound);

        // Combine multipliers: Buffs * Local Upgrade * GLOBAL Upgrade
        float totalDmgMult = _damageMultiplier * _upgradeMultiplier * _globalDamageMult;

        if (_towerData.attackType == TowerData.AttackType.Hitscan)
        {
            _currentTarget.TakeDamage(_towerData.damage * totalDmgMult, this.transform);
            if (_hitscanTracer != null) StartCoroutine(ShowHitscanTrace());
        }
        else
        {
            if (_towerData.projectilePrefab == null) return;
            GameObject proj = Instantiate(_towerData.projectilePrefab, _shootPoint.position, _shootPoint.rotation);
            Projectile pScript = proj.GetComponent<Projectile>();
            if (pScript != null)
            {
                pScript.towerData = _towerData;
                pScript.attacker = this.transform;
                pScript.damageMultiplier = totalDmgMult;
            }
        }
    }

    protected IEnumerator ShowHitscanTrace()
    {
        if (_currentTarget == null) yield break;
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
            EnemyController enemy = other.GetComponent<EnemyController>();
            if (enemy != null && !_enemiesInRange.Contains(enemy))
            {
                if (_targetingPriority == TargetingPriority.Last) _enemiesInRange.Insert(0, enemy);
                else _enemiesInRange.Add(enemy);
            }
        }
    }

    protected virtual void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Enemy"))
        {
            EnemyController enemy = other.GetComponent<EnemyController>();
            if (enemy != null && _enemiesInRange.Contains(enemy))
            {
                _enemiesInRange.Remove(enemy);
                if (_currentTarget == enemy) _currentTarget = null;
            }
        }
    }
    
    // --- MANAGEMENT METHODS ---

    public void SetTargetingPriority(int priorityIndex)
    {
        _targetingPriority = (TargetingPriority)priorityIndex;
        UpdateTarget(); 
    }
    
    public string GetPriorityName()
    {
        return _targetingPriority.ToString();
    }

    public void CyclePriority()
    {
        int current = (int)_targetingPriority;
        int next = (current + 1) % 4; // 4 enum values
        SetTargetingPriority(next);
    }

    public void UpgradeTower()
    {
        Level++;
        _upgradeMultiplier += 0.25f; // +25% damage per level
        Debug.Log($"{TowerName} Upgraded to Level {Level}!");
    }

    public void SellTower()
    {
        Destroy(gameObject);
    }

    public int GetUpgradeCost()
    {
        if (_towerData == null) return 0;
        return Mathf.RoundToInt(_towerData.scrapCost * 0.75f * Level); 
    }

    public int GetSellValue()
    {
        if (_towerData == null) return 0;
        return Mathf.RoundToInt((_towerData.scrapCost / 2f) + (GetUpgradeCost() * 0.5f));
    }

    // --- BUFFS ---
    public void ApplyBuff(float fireRateBuff, float damageBuff)
    {
        _fireRateMultiplier = fireRateBuff;
        _damageMultiplier = damageBuff;
    }
    
    public void RemoveBuff()
    {
        _fireRateMultiplier = 1f;
        _damageMultiplier = 1f;
    }
}