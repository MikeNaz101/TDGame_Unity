using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

[RequireComponent(typeof(SphereCollider))]
[RequireComponent(typeof(AudioSource))]
public class TowerController : MonoBehaviour
{
    // --- NEW: Targeting Options ---
    public enum TargetingPriority
    {
        First,      // Targets the enemy that entered the range first
        Last,       // Targets the enemy that entered the range last
        Weakest,    // Targets the enemy with the lowest current health
        Strongest   // Targets the enemy with the highest current health
    }
    
    [Header("Tower Components")]
    [Tooltip("The part of the tower that rotates left/right (Y-Axis).")]
    [SerializeField] private Transform _turretBase;
    [Tooltip("The part of the tower that rotates up/down (X-Axis).")]
    [SerializeField] private Transform _barrel;
    [Tooltip("The empty GameObject where shots/projectiles originate.")]
    [SerializeField] public Transform _shootPoint;
    
    [Header("Targeting")]
    [Tooltip("The targeting strategy for this tower.")]
    [SerializeField] private TargetingPriority _targetingPriority = TargetingPriority.First;

    [Header("Tuning")]
    [SerializeField] private float _aimSpeed = 10f;
    [SerializeField] private float _aimTolerance = 3f;

    // --- Public Properties (for PC UI) ---
    [HideInInspector] public string TowerName { get; private set; }
    [HideInInspector] public int TowerID { get; private set; }

    // --- Protected References (for child classes) ---
    protected TowerData _towerData;
    protected AudioSource _audioSource;
    protected LineRenderer _hitscanTracer;
    protected PlayerStats _playerStats;
    protected Transform _playerTarget;

    // --- Target & State (Protected for child classes) ---
    protected List<EnemyController> _enemiesInRange = new List<EnemyController>();
    protected EnemyController _currentTarget;
    protected float _fireCooldown = 0f;
    protected float _fireRateMultiplier = 1f;
    protected float _damageMultiplier = 1f;
    
    private SphereCollider _rangeTrigger;


    protected virtual void Awake()
    {
        _rangeTrigger = GetComponent<SphereCollider>();
        _audioSource = GetComponent<AudioSource>();
        
        _rangeTrigger.isTrigger = true;
        
        _hitscanTracer = GetComponentInChildren<LineRenderer>();
        if (_hitscanTracer != null)
        {
            _hitscanTracer.enabled = false;
        }
        
        // Register this tower with the global registry and get a unique ID
        TowerID = TowerRegistry.RegisterTower(this);
        
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            _playerTarget = playerObj.transform;
            _playerStats = playerObj.GetComponent<PlayerStats>();
        }
    }

    public void Initialize(TowerData data)
    {
        _towerData = data;
        _rangeTrigger.radius = _towerData.range;
        
        // Set its public name from the TowerData
        TowerName = _towerData.towerName;
    }

    protected virtual void OnDestroy()
    {
        // Tell the registry we are being destroyed
        TowerRegistry.UnregisterTower(this);
    }
    
    /// <summary>
    /// This is the default Update loop for an attacking tower.
    /// Utility towers (like Radar) will override this.
    /// </summary>
    protected virtual void Update()
    {
        // 1. Cooldown
        if (_fireCooldown > 0)
        {
            _fireCooldown -= Time.deltaTime;
        }

        // 2. Target Acquisition
        if (_currentTarget == null || !_currentTarget.gameObject.activeInHierarchy)
        {
            UpdateTarget();
        }

        // 3. Aim & Fire
        if (_currentTarget != null)
        {
            AimAtTarget();
            TryFire();
        }
    }

    /// <summary>
    /// Cleans the list, sorts it based on priority, and sets a new target.
    /// </summary>
    protected void UpdateTarget()
    {
        // 1. Clean the list
        _enemiesInRange.RemoveAll(enemy => enemy == null || !enemy.gameObject.activeInHierarchy);

        // 2. Apply sorting logic
        switch (_targetingPriority)
        {
            case TargetingPriority.Weakest:
                _enemiesInRange = _enemiesInRange.OrderBy(e => e.CurrentHealth).ToList();
                break;
            case TargetingPriority.Strongest:
                _enemiesInRange = _enemiesInRange.OrderByDescending(e => e.CurrentHealth).ToList();
                break;
        }

        // 3. Set the new target
        if (_enemiesInRange.Count > 0)
        {
            _currentTarget = _enemiesInRange[0];
        }
        else
        {
            _currentTarget = null;
        }
    }

    /// <summary>
    /// Rotates the turret and barrel to face the current target.
    /// </summary>
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

    /// <summary>
    /// Default check for firing.
    /// </summary>
    protected virtual void TryFire()
    {
        if (_fireCooldown > 0 || _currentTarget == null || _towerData == null)
        {
            return;
        }

        Vector3 targetDir = _currentTarget.transform.position - _shootPoint.position;
        if (Vector3.Angle(_shootPoint.forward, targetDir) < _aimTolerance)
        {
            Shoot();
            _fireCooldown = _towerData.fireRate/ _fireRateMultiplier;
        }
    }

    /// <summary>
    /// Default attack logic (Hitscan or Projectile).
    /// Child classes (like Flamethrower) will override this.
    /// </summary>
    protected virtual void Shoot()
    {
        if (_towerData.shootSound != null)
        {
            _audioSource.PlayOneShot(_towerData.shootSound);
        }

        if (_towerData.attackType == TowerData.AttackType.Hitscan)
        {
            _currentTarget.TakeDamage(_towerData.damage * _damageMultiplier, this.transform);
            if (_hitscanTracer != null)
            {
                StartCoroutine(ShowHitscanTrace());
            }
        }
        else
        {
            if (_towerData.projectilePrefab == null) return;
            
            GameObject proj = Instantiate(
                _towerData.projectilePrefab, 
                _shootPoint.position, 
                _shootPoint.rotation
            );
            
            Projectile pScript = proj.GetComponent<Projectile>();
            if (pScript != null)
            {
                pScript.towerData = _towerData;
                pScript.attacker = this.transform;
                pScript.damageMultiplier = _damageMultiplier;
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

    // --- Trigger Detection ---
    protected virtual void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Enemy"))
        {
            EnemyController enemy = other.GetComponent<EnemyController>();
            if (enemy != null && !_enemiesInRange.Contains(enemy))
            {
                switch (_targetingPriority)
                {
                    case TargetingPriority.Last:
                        _enemiesInRange.Insert(0, enemy);
                        break;
                    
                    case TargetingPriority.First:
                    case TargetingPriority.Weakest:
                    case TargetingPriority.Strongest:
                    default:
                        _enemiesInRange.Add(enemy);
                        break;
                }
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
                
                if (_currentTarget == enemy)
                {
                    _currentTarget = null;
                }
            }
        }
    }
    
    /// <summary>
    /// Public method called by the PC UI to change targeting.
    /// </summary>
    public void SetTargetingPriority(int priorityIndex)
    {
        _targetingPriority = (TargetingPriority)priorityIndex;
        UpdateTarget(); // Re-sort and find a new target immediately
    }
    
    /// <summary>
    /// Called by a Radar Tower to apply a buff.
    /// </summary>
    public void ApplyBuff(float fireRateBuff, float damageBuff)
    {
        _fireRateMultiplier = fireRateBuff;
        _damageMultiplier = damageBuff;
    }
    
    /// <summary>
    /// Called by a Radar Tower when a buff expires.
    /// </summary>
    public void RemoveBuff()
    {
        _fireRateMultiplier = 1f;
        _damageMultiplier = 1f;
    }
}