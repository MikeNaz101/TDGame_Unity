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

    // --- NEW: IDLE SCAN SETTINGS ---
    [Header("Idle Behavior")]
    [Tooltip("How fast it scans left/right.")]
    [SerializeField] private float _idleScanSpeed = 1.0f;
    [Tooltip("How wide the scan angle is (in degrees).")]
    [SerializeField] private float _idleScanAngle = 45f;
    [Tooltip("If true, the turret resets to forward before scanning.")]
    [SerializeField] private bool _resetToForward = true;
    
    // Internal Idle State
    private Quaternion _initialBaseRotation;
    private Quaternion _initialBarrelRotation; // NEW: Store initial barrel rotation
    private float _idleTimer;
    private float _randomOffset; // NEW: Random start time offset

    // --- Public Properties ---
    public string TowerName { get; private set; }
    public int TowerID { get; private set; }
    public int Level { get; private set; } = 1;
    public int[] PathProgress { get; set; }
    public TowerData Data => _towerData;

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
    protected float _fireRateMultiplier = 1f; 
    protected float _damageMultiplier = 1f;   
    protected float _rangeMultiplier = 1f;
    protected float _upgradeMultiplier = 1f;
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

        // Store initial rotations for scanning logic
        if (_turretBase != null)
        {
            _initialBaseRotation = _turretBase.rotation;
        }
        if (_barrel != null)
        {
            _initialBarrelRotation = _barrel.localRotation;
        }
        
        // Generate random offset for idle animation
        _randomOffset = Random.Range(0f, 100f);
    }

    protected virtual void Start()
    {
        RefreshGlobalStats();
    }

    public void RefreshGlobalStats()
    {
        if (UpgradeManager.Instance != null)
        {
            _globalDamageMult = UpgradeManager.Instance.GlobalTowerDamageMult;
            _globalRangeMult = UpgradeManager.Instance.GlobalTowerRangeMult;
            RecalculateStats();
        }
    }

    public void Initialize(TowerData data)
    {
        _towerData = data;
        TowerName = _towerData.towerName;
        Level = 1;
        if (_towerData.upgradePaths != null) PathProgress = new int[_towerData.upgradePaths.Count];
        RecalculateStats();
    }

    protected virtual void OnDestroy() { TowerRegistry.UnregisterTower(this); }
    
    protected void RecalculateStats()
    {
        if (_rangeTrigger != null && _towerData != null)
        {
            _rangeTrigger.radius = _towerData.range * _rangeMultiplier * _globalRangeMult;
        }
    }

    public void ApplyUpgrade(TowerUpgradeData upgrade)
    {
        switch (upgrade.effectType)
        {
            case TowerUpgradeType.Damage: _damageMultiplier += upgrade.effectValue; break;
            case TowerUpgradeType.FireRate: _fireRateMultiplier += upgrade.effectValue; break;
            case TowerUpgradeType.Range: _rangeMultiplier += upgrade.effectValue; break;
            default: ApplySpecificUpgrade(upgrade); break;
        }
        RecalculateStats();
    }

    protected virtual void ApplySpecificUpgrade(TowerUpgradeData upgrade) { }

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
        else
        {
            PerformIdleScan();
        }
    }

    protected void PerformIdleScan()
    {
        if (_turretBase == null || _barrel == null) return;

        // Use Time.time + _randomOffset to desynchronize the towers
        float time = Time.time + _randomOffset;

        // 1. Pan Left/Right (Y-Axis) - Applied to BASE
        // Sine wave between -1 and 1 based on time and speed
        float sineY = Mathf.Sin(time * _idleScanSpeed);
        float angleY = sineY * _idleScanAngle;
        
        // Rotate base relative to initial rotation
        Quaternion targetBaseRot = _initialBaseRotation * Quaternion.Euler(0f, angleY, 0f);
        _turretBase.rotation = Quaternion.RotateTowards(_turretBase.rotation, targetBaseRot, _aimSpeed * Time.deltaTime * 5f);

        // 2. Pan Up/Down (X-Axis) - Applied to BARREL
        // We use Cosine so the movement is offset from the horizontal scan (figure-8 pattern)
        // We use a smaller angle (e.g., 15 degrees) so it doesn't look at the floor/sky
        float cosineX = Mathf.Cos(time * _idleScanSpeed * 0.7f); // Different speed for variety
        float angleX = cosineX * 15f; 

        // Apply rotation relative to initial barrel rotation
        // Important: Use localRotation because barrel is a child of base
        Quaternion targetBarrelRot = _initialBarrelRotation * Quaternion.Euler(angleX, 0f, 0f);
        _barrel.localRotation = Quaternion.RotateTowards(_barrel.localRotation, targetBarrelRot, _aimSpeed * Time.deltaTime * 5f);
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

        Vector3 targetPosition = _currentTarget.transform.position;

        // --- 1. Turret Base Rotation (Y-Axis / Left-Right) ---
        Vector3 targetPosFlattened = new Vector3(targetPosition.x, _turretBase.position.y, targetPosition.z);
        Vector3 dirToTargetFlat = (targetPosFlattened - _turretBase.position).normalized;
        
        if (dirToTargetFlat != Vector3.zero)
        {
            Quaternion lookRotBase = Quaternion.LookRotation(dirToTargetFlat);
            _turretBase.rotation = Quaternion.RotateTowards(_turretBase.rotation, lookRotBase, _aimSpeed * Time.deltaTime * 50f); 
        }

        // --- 2. Barrel Rotation (X-Axis / Up-Down) ---
        Vector3 dirToTarget = (targetPosition - _barrel.position).normalized;
        Quaternion lookRotBarrel = Quaternion.LookRotation(dirToTarget);
        Quaternion localTargetRot = Quaternion.Inverse(_barrel.parent.rotation) * lookRotBarrel;

        Vector3 euler = localTargetRot.eulerAngles;
        float xAngle = euler.x;
        if (xAngle > 180) xAngle -= 360; 
        xAngle = Mathf.Clamp(xAngle, -45f, 45f);

        Quaternion finalLocalRot = Quaternion.Euler(xAngle, 0, 0);
        _barrel.localRotation = Quaternion.RotateTowards(_barrel.localRotation, finalLocalRot, _aimSpeed * Time.deltaTime * 50f);
    }

    protected virtual void TryFire()
    {
        if (_fireCooldown > 0 || _currentTarget == null || _towerData == null) return;

        Vector3 targetDir = _currentTarget.transform.position - _shootPoint.position;
        if (Vector3.Angle(_shootPoint.forward, targetDir) < _aimTolerance)
        {
            Shoot();
            float finalFireRate = _towerData.fireRate / (_fireRateMultiplier); 
            _fireCooldown = finalFireRate;
        }
    }

    protected virtual void Shoot()
    {
        if (_towerData.shootSound != null) _audioSource.PlayOneShot(_towerData.shootSound);

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
            
            // Homing Logic Support
            var homing = proj.GetComponent<HomingProjectile>();
            if(homing) homing.Initialize(_towerData, transform, _currentTarget);
            
            // Rocket Logic Support
            var slowRocket = proj.GetComponent<SlowHomingRocket>();
            if(slowRocket) { 
                slowRocket.Initialize(_towerData, _currentTarget, transform); 
                slowRocket.Launch();
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
    
    // --- HELPERS ---
    public void SetTargetingPriority(int priorityIndex) { _targetingPriority = (TargetingPriority)priorityIndex; UpdateTarget(); }
    public string GetPriorityName() { return _targetingPriority.ToString(); }
    public void CyclePriority() { int current = (int)_targetingPriority; SetTargetingPriority((current + 1) % 4); }
    public void UpgradeTower() { Level++; _upgradeMultiplier += 0.25f; Debug.Log($"{TowerName} Upgraded to Level {Level}!"); }
    public void SellTower() { Destroy(gameObject); }
    public int GetUpgradeCost() { if (_towerData == null) return 0; return Mathf.RoundToInt(_towerData.scrapCost * 0.75f * Level); }
    public int GetSellValue() { if (_towerData == null) return 0; return Mathf.RoundToInt((_towerData.scrapCost / 2f) + (GetUpgradeCost() * 0.5f)); }
    public void ApplyBuff(float fireRateBuff, float damageBuff) { _fireRateMultiplier = fireRateBuff; _damageMultiplier = damageBuff; }
    public void RemoveBuff() { _fireRateMultiplier = 1f; _damageMultiplier = 1f; }
}