using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

[RequireComponent(typeof(SphereCollider))]
[RequireComponent(typeof(AudioSource))]
public class TowerController : MonoBehaviour
{
    [Header("Tower Components")]
    [Tooltip("The part of the tower that rotates left/right (Y-Axis).")]
    [SerializeField] private Transform _turretBase; // Assign the rotating base
    [Tooltip("The part of the tower that rotates up/down (X-Axis).")]
    [SerializeField] private Transform _barrel;     // Assign the barrel
    [Tooltip("The empty GameObject where shots/projectiles originate.")]
    [SerializeField] private Transform _shootPoint; // Assign the muzzle

    [Header("Tuning")]
    [SerializeField] private float _aimSpeed = 10f;
    [SerializeField] private float _aimTolerance = 3f; // How many degrees off-target it can be and still fire

    // --- Private References ---
    private TowerData _towerData;
    private SphereCollider _rangeTrigger;
    private AudioSource _audioSource;
    private LineRenderer _hitscanTracer; // Optional for hitscan

    // --- Target & State ---
    private List<EnemyController> _enemiesInRange = new List<EnemyController>();
    private EnemyController _currentTarget;
    private float _fireCooldown = 0f;

    void Awake()
    {
        _rangeTrigger = GetComponent<SphereCollider>();
        _audioSource = GetComponent<AudioSource>();

        // Configure the trigger
        _rangeTrigger.isTrigger = true;

        // Find optional LineRenderer
        _hitscanTracer = GetComponentInChildren<LineRenderer>();
        if (_hitscanTracer != null)
        {
            _hitscanTracer.enabled = false;
        }
    }

    /// <summary>
    /// This is called by TowerManager after instantiation to give this tower its stats.
    /// </summary>
    public void Initialize(TowerData data)
    {
        _towerData = data;
        // Set the detection radius from the ScriptableObject
        _rangeTrigger.radius = _towerData.range;
    }

    void Update()
    {
        // 1. Cooldown
        if (_fireCooldown > 0)
        {
            _fireCooldown -= Time.deltaTime;
        }

        // 2. Target Acquisition
        if (_currentTarget == null || !_currentTarget.gameObject.activeInHierarchy)
        {
            FindNewTarget();
        }

        // 3. Aim & Fire
        if (_currentTarget != null)
        {
            AimAtTarget();
            TryFire();
        }
    }

    /// <summary>
    /// Finds the closest valid enemy from the list.
    /// </summary>
    private void FindNewTarget()
    {
        // Clean the list of any null (destroyed) enemies
        _enemiesInRange.RemoveAll(enemy => enemy == null || !enemy.gameObject.activeInHierarchy);

        if (_enemiesInRange.Count == 0)
        {
            _currentTarget = null;
            return;
        }

        // Find the closest enemy
        float closestDist = Mathf.Infinity;
        EnemyController closestEnemy = null;

        foreach (EnemyController enemy in _enemiesInRange)
        {
            float dist = Vector3.Distance(transform.position, enemy.transform.position);
            if (dist < closestDist)
            {
                closestDist = dist;
                closestEnemy = enemy;
            }
        }
        _currentTarget = closestEnemy;
    }

    /// <summary>
    /// Rotates the turret and barrel to face the current target.
    /// </summary>
    private void AimAtTarget()
    {
        if (_turretBase == null || _barrel == null) return;

        Vector3 targetDir = _currentTarget.transform.position - _turretBase.position;
        Quaternion lookRotation = Quaternion.LookRotation(targetDir);
        Vector3 euler = Quaternion.Slerp(_turretBase.rotation, lookRotation, Time.deltaTime * _aimSpeed).eulerAngles;

        // Rotate Turret (Y-Axis)
        _turretBase.rotation = Quaternion.Euler(0f, euler.y, 0f);

        // Rotate Barrel (X-Axis)
        // We use the barrel's parent (the turret) to get the correct local rotation
        Vector3 localTargetPos = _turretBase.InverseTransformPoint(_currentTarget.transform.position);
        Quaternion barrelRotation = Quaternion.LookRotation(localTargetPos);
        _barrel.localRotation = Quaternion.Slerp(_barrel.localRotation, barrelRotation, Time.deltaTime * _aimSpeed);
    }

    /// <summary>
    /// Checks if the tower is aimed and the cooldown is ready, then fires.
    /// </summary>
    private void TryFire()
    {
        if (_fireCooldown > 0 || _currentTarget == null)
        {
            return;
        }

        // Check if we are aimed at the target
        Vector3 targetDir = _currentTarget.transform.position - _shootPoint.position;
        if (Vector3.Angle(_shootPoint.forward, targetDir) < _aimTolerance)
        {
            // We are aimed, FIRE!
            Shoot();
            _fireCooldown = _towerData.fireRate; // Reset cooldown
        }
    }

    private void Shoot()
    {
        if (_towerData.shootSound != null)
        {
            _audioSource.PlayOneShot(_towerData.shootSound);
        }

        if (_towerData.attackType == TowerData.AttackType.Hitscan)
        {
            // --- HITSCAN LOGIC (Unchanged) ---
            _currentTarget.TakeDamage(_towerData.damage, this.transform);

            // Show tracer
            if (_hitscanTracer != null)
            {
                StartCoroutine(ShowHitscanTracet());
            }
        }
        else
        {
            // --- PROJECTILE LOGIC (MODIFIED) ---
            if (_towerData.projectilePrefab == null) return;

            GameObject proj = Instantiate(
                _towerData.projectilePrefab,
                _shootPoint.position,
                _shootPoint.rotation // The AimAtTarget() function already aimed the shoot point
            );

            Projectile pScript = proj.GetComponent<Projectile>();
            if (pScript != null)
            {
                // --- MODIFIED ---
                // Pass the tower's data and identity to the projectile
                pScript.towerData = _towerData;
                pScript.attacker = this.transform;
                // -----------------
            }
        }
    }

    private IEnumerator ShowHitscanTracet()
    {
        _hitscanTracer.enabled = true;
        _hitscanTracer.SetPosition(0, _shootPoint.position);
        _hitscanTracer.SetPosition(1, _currentTarget.transform.position + Vector3.up * 0.5f); // Aim at center mass

        yield return new WaitForSeconds(0.07f);

        _hitscanTracer.enabled = false;
    }

    // --- Trigger Detection ---

    void OnTriggerEnter(Collider other)
    {
        // Use CompareTag for efficiency
        if (other.CompareTag("Enemy"))
        {
            EnemyController enemy = other.GetComponent<EnemyController>();
            if (enemy != null && !_enemiesInRange.Contains(enemy))
            {
                _enemiesInRange.Add(enemy);
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
                _enemiesInRange.Remove(enemy);

                if (_currentTarget == enemy)
                {
                    _currentTarget = null; // Find a new target next frame
                }
            }
        }
    }
}