using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq; // <-- IMPORTANT: We need this for sorting!

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
    [SerializeField] private Transform _shootPoint;
    
    [Header("Targeting")]
    [Tooltip("The targeting strategy for this tower.")]
    [SerializeField] private TargetingPriority _targetingPriority = TargetingPriority.First;

    [Header("Tuning")]
    [SerializeField] private float _aimSpeed = 10f;
    [SerializeField] private float _aimTolerance = 3f;

    // --- Private References ---
    private TowerData _towerData;
    private SphereCollider _rangeTrigger;
    private AudioSource _audioSource;
    private LineRenderer _hitscanTracer;
    
    [HideInInspector] public string TowerName { get; private set; }
    [HideInInspector] public int TowerID { get; private set; }

    // --- Target & State ---
    // This list is now the core of our targeting
    private List<EnemyController> _enemiesInRange = new List<EnemyController>();
    private EnemyController _currentTarget;
    private float _fireCooldown = 0f;

    void Awake()
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
    }

    public void Initialize(TowerData data)
    {
        _towerData = data;
        _rangeTrigger.radius = _towerData.range;
        
        // Set its public name from the TowerData
        TowerName = _towerData.towerName;
    }

    void Update()
    {
        // 1. Cooldown
        if (_fireCooldown > 0)
        {
            _fireCooldown -= Time.deltaTime;
        }

        // 2. Target Acquisition
        // Check if our target is dead, destroyed, or no longer active
        if (_currentTarget == null || !_currentTarget.gameObject.activeInHierarchy)
        {
            // Find a new target based on our rules
            UpdateTarget();
        }

        // 3. Aim & Fire
        if (_currentTarget != null)
        {
            AimAtTarget();
            Debug.Log("Tower Aimed and is calling ReyFire!!");
            TryFire();
        }
    }

    /// <summary>
    /// This is the new brain. It cleans the list, sorts it if needed,
    /// and sets the new _currentTarget to be the enemy at the front.
    /// </summary>
    private void UpdateTarget()
    {
        // 1. Clean the list of any enemies that were destroyed
        _enemiesInRange.RemoveAll(enemy => enemy == null || !enemy.gameObject.activeInHierarchy);

        // 2. Apply sorting logic ONLY for Weakest/Strongest
        // (First/Last are sorted automatically by how we add them)
        switch (_targetingPriority)
        {
            case TargetingPriority.Weakest:
                // Sorts the list by health, lowest to highest
                _enemiesInRange = _enemiesInRange.OrderBy(e => e.CurrentHealth).ToList();
                break;
            case TargetingPriority.Strongest:
                // Sorts the list by health, highest to lowest
                _enemiesInRange = _enemiesInRange.OrderByDescending(e => e.CurrentHealth).ToList();
                break;
        }

        // 3. Set the new target to the one at the "front" of the list
        if (_enemiesInRange.Count > 0)
        {
            _currentTarget = _enemiesInRange[0];
        }
        else
        {
            _currentTarget = null; // We have no targets
        }
    }

    /// <summary>
    /// Rotates the turret and barrel to face the current target.
    /// </summary>
    private void AimAtTarget()
    {
        if (_turretBase == null || _barrel == null || _currentTarget == null) return;

        // --- Aiming logic is unchanged ---
        Vector3 targetDir = _currentTarget.transform.position - _turretBase.position;
        Quaternion lookRotation = Quaternion.LookRotation(targetDir);
        Vector3 euler = Quaternion.Slerp(_turretBase.rotation, lookRotation, Time.deltaTime * _aimSpeed).eulerAngles;

        _turretBase.rotation = Quaternion.Euler(0f, euler.y, 0f);
        
        Vector3 localTargetPos = _turretBase.InverseTransformPoint(_currentTarget.transform.position);
        Quaternion barrelRotation = Quaternion.LookRotation(localTargetPos);
        _barrel.localRotation = Quaternion.Slerp(_barrel.localRotation, barrelRotation, Time.deltaTime * _aimSpeed);
    }

    /// <summary>
    /// Checks if the tower is aimed and the cooldown is ready, then fires.
    /// </summary>
    private void TryFire()
    {
        Debug.Log("Tower Should be trying to fire!");
        if (_fireCooldown > 0 || _currentTarget == null || _towerData == null)
        {
            return;
        }

        Vector3 targetDir = _currentTarget.transform.position - _shootPoint.position;
        Shoot();
        _fireCooldown = _towerData.fireRate;
        if (Vector3.Angle(_shootPoint.forward, targetDir) < _aimTolerance)
        {
            Shoot();
            _fireCooldown = _towerData.fireRate;
        }
    }

    private void Shoot()
    {
        // --- Firing logic is unchanged ---
        if (_towerData.shootSound != null)
        {
            _audioSource.PlayOneShot(_towerData.shootSound);
        }

        if (_towerData.attackType == TowerData.AttackType.Hitscan)
        {
            Debug.Log("Tower Shot HS Ray!");
            _currentTarget.TakeDamage(_towerData.damage, this.transform);
            if (_hitscanTracer != null)
            {
                StartCoroutine(ShowHitscanTrace());
            }
        }
        else
        {
            Debug.Log("Tower Shot Bomb!!");
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
            }
        }
    }

    private IEnumerator ShowHitscanTrace()
    {
        // --- Tracer logic is unchanged ---
        if (_currentTarget == null) yield break; // Safety check
        
        _hitscanTracer.enabled = true;
        _hitscanTracer.SetPosition(0, _shootPoint.position);
        _hitscanTracer.SetPosition(1, _currentTarget.transform.position + Vector3.up * 0.5f);
        
        yield return new WaitForSeconds(0.07f);
        
        _hitscanTracer.enabled = false;
    }
    
    /// <summary>
    /// This is called by the PC UI buttons to change the targeting mode.
    /// </summary>
    /// <param name="priorityIndex">0=First, 1=Last, 2=Weakest, 3=Strongest</param>
    public void SetTargetingPriority(int priorityIndex)
    {
        // Cast the integer from the button click to the enum
        _targetingPriority = (TargetingPriority)priorityIndex;
        
        // Force the tower to find a new target based on the new rules
        UpdateTarget();
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Enemy"))
        {
            EnemyController enemy = other.GetComponent<EnemyController>();
            if (enemy != null && !_enemiesInRange.Contains(enemy))
            {
                // Add the enemy to the list based on our targeting rule
                switch (_targetingPriority)
                {
                    case TargetingPriority.Last:
                        // Adds the new enemy to the "front" of the list
                        _enemiesInRange.Insert(0, enemy);
                        break;
                    
                    case TargetingPriority.First:
                    case TargetingPriority.Weakest:
                    case TargetingPriority.Strongest:
                    default:
                        // Adds the new enemy to the "end" of the list
                        _enemiesInRange.Add(enemy);
                        break;
                }
            }
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Enemy"))
        {
            EnemyController enemy = other.GetComponent<EnemyController>();
            if (enemy != null && _enemiesInRange.Contains(enemy))
            {
                // Remove the enemy from the list
                _enemiesInRange.Remove(enemy);
                
                // If the enemy that left WAS our target,
                // set target to null so we find a new one next frame.
                if (_currentTarget == enemy)
                {
                    _currentTarget = null;
                }
            }
        }
    }
    
    void OnDestroy()
    {
        // Tell the registry we are being destroyed
        TowerRegistry.UnregisterTower(this);
    }
}