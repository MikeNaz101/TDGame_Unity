using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Makes a placed tower actually fight: picks a target from EnemyController.ActiveEnemies
/// (no physics queries), rotates an optional turret head, and deals hitscan damage.
/// TowerManager adds this component at runtime if the tower prefab doesn't already have one,
/// so existing prefabs work without edits. Add it to the prefab to assign a head / fire point.
/// </summary>
public class TowerController : MonoBehaviour
{
    public static readonly List<TowerController> ActiveTowers = new List<TowerController>();

    [Header("Setup")]
    [Tooltip("Stats for this tower. Assigned by TowerManager when built.")]
    public TowerData towerData;
    [Tooltip("Optional: the part of the tower that rotates to face enemies (yaw only).")]
    public Transform turretHead;
    [Tooltip("Optional: where shots originate. Defaults to the head (or 1m above the base).")]
    public Transform firePoint;
    [Tooltip("Optional: tracer line renderer. One is created automatically if missing.")]
    public LineRenderer tracer;
    [Tooltip("Optional: audio source for shooting sounds. One is added automatically if a shoot sound is set.")]
    public AudioSource audioSource;

    public EnemyController CurrentTarget { get; private set; }
    public int Kills { get; private set; }
    public float DamageDealt { get; private set; }
    public bool IsInitialized { get; private set; }

    private float _fireTimer;
    private float _scanTimer;
    private float _tracerTimer;
    private float _rangeSqr;

    private Material _ownedTracerMaterial;

    private const float SCAN_INTERVAL = 0.15f;

    void OnEnable()
    {
        if (!ActiveTowers.Contains(this)) ActiveTowers.Add(this);
    }

    void OnDisable()
    {
        ActiveTowers.Remove(this);
    }

    void OnDestroy()
    {
        if (_ownedTracerMaterial != null) Destroy(_ownedTracerMaterial);
    }

    void Start()
    {
        if (!IsInitialized && towerData != null) Initialize(towerData);
    }

    public void Initialize(TowerData data)
    {
        towerData = data;
        if (towerData == null)
        {
            Debug.LogError("TowerController has no TowerData, it will not fire.", this);
            enabled = false;
            return;
        }

        _rangeSqr = towerData.range * towerData.range;
        _fireTimer = 0f;

        if (firePoint == null) firePoint = turretHead != null ? turretHead : null;

        if (tracer == null) tracer = GetComponentInChildren<LineRenderer>();
        if (tracer == null) tracer = CreateTracer();
        if (tracer != null)
        {
            tracer.positionCount = 2;
            tracer.startWidth = towerData.tracerWidth;
            tracer.endWidth = towerData.tracerWidth;
            tracer.startColor = towerData.tracerColor;
            tracer.endColor = towerData.tracerColor;
            tracer.enabled = false;
        }

        if (audioSource == null && towerData.shootSound != null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 1f;
                audioSource.minDistance = 3f;
                audioSource.maxDistance = 40f;
            }
        }

        IsInitialized = true;
        enabled = true;
    }

    void Update()
    {
        if (!IsInitialized) return;

        float dt = Time.deltaTime;
        if (_fireTimer > 0f) _fireTimer -= dt;

        if (_tracerTimer > 0f)
        {
            _tracerTimer -= dt;
            if (_tracerTimer <= 0f && tracer != null) tracer.enabled = false;
        }

        // Validate / refresh target on a timer, not every frame.
        _scanTimer -= dt;
        if (_scanTimer <= 0f || !IsValidTarget(CurrentTarget))
        {
            _scanTimer = SCAN_INTERVAL;
            CurrentTarget = AcquireTarget();
        }

        if (CurrentTarget == null) return;

        bool aimed = AimAt(CurrentTarget.transform.position, dt);
        if (aimed && _fireTimer <= 0f)
        {
            if (!towerData.requireLineOfSight || HasLineOfSight(CurrentTarget))
            {
                Fire(CurrentTarget);
                _fireTimer = Mathf.Max(0.02f, towerData.fireRate);
            }
        }
    }

    private bool IsValidTarget(EnemyController enemy)
    {
        if (enemy == null || enemy.IsDead) return false;
        return (enemy.transform.position - transform.position).sqrMagnitude <= _rangeSqr;
    }

    private EnemyController AcquireTarget()
    {
        EnemyController best = null;
        float bestScore = 0f;
        Vector3 origin = transform.position;
        var enemies = EnemyController.ActiveEnemies;

        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyController enemy = enemies[i];
            if (enemy == null || enemy.IsDead) continue;

            float sqrDist = (enemy.transform.position - origin).sqrMagnitude;
            if (sqrDist > _rangeSqr) continue;

            float score;
            switch (towerData.targeting)
            {
                case TowerData.TargetingMode.First:    score = -enemy.SpawnTime; break;      // oldest wins
                case TowerData.TargetingMode.Strongest: score = enemy.CurrentHealth; break;
                case TowerData.TargetingMode.Weakest:   score = -enemy.CurrentHealth; break;
                case TowerData.TargetingMode.Closest:
                default:                                score = -sqrDist; break;
            }

            if (best == null || score > bestScore)
            {
                best = enemy;
                bestScore = score;
            }
        }
        return best;
    }

    /// <summary>Rotates the head toward the target. Returns true when we're allowed to fire.</summary>
    private bool AimAt(Vector3 targetPos, float dt)
    {
        if (turretHead == null) return true;

        Vector3 dir = targetPos - turretHead.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return true;

        Quaternion desired = Quaternion.LookRotation(dir);
        if (towerData.turnSpeed <= 0f)
        {
            turretHead.rotation = desired;
            return true;
        }

        turretHead.rotation = Quaternion.RotateTowards(turretHead.rotation, desired, towerData.turnSpeed * dt);
        return Quaternion.Angle(turretHead.rotation, desired) <= towerData.fireAngleTolerance;
    }

    private bool HasLineOfSight(EnemyController enemy)
    {
        Vector3 from = GetFirePosition();
        Vector3 to = enemy.transform.position + Vector3.up * 1f;
        Vector3 delta = to - from;
        float dist = delta.magnitude;
        if (dist < 0.01f) return true;
        return !Physics.Raycast(from, delta / dist, dist, towerData.lineOfSightBlockers, QueryTriggerInteraction.Ignore);
    }

    private void Fire(EnemyController enemy)
    {
        Vector3 from = GetFirePosition();
        Vector3 hitPoint = enemy.transform.position + Vector3.up * 1f;

        enemy.TakeDamage(towerData.damage, null);
        DamageDealt += towerData.damage;
        if (enemy.IsDead) Kills++;

        if (tracer != null)
        {
            tracer.SetPosition(0, from);
            tracer.SetPosition(1, hitPoint);
            tracer.enabled = true;
            _tracerTimer = towerData.tracerDuration;
        }

        if (audioSource != null && towerData.shootSound != null)
        {
            audioSource.PlayOneShot(towerData.shootSound);
        }

        if (towerData.muzzleEffect != null)
        {
            Destroy(Instantiate(towerData.muzzleEffect, from, Quaternion.LookRotation(hitPoint - from)), 2f);
        }
        if (towerData.impactEffect != null)
        {
            Destroy(Instantiate(towerData.impactEffect, hitPoint, Quaternion.LookRotation(from - hitPoint)), 2f);
        }
    }

    private Vector3 GetFirePosition()
    {
        if (firePoint != null) return firePoint.position;
        return transform.position + Vector3.up * 1f;
    }

    private LineRenderer CreateTracer()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null) return null;

        GameObject go = new GameObject("Tracer");
        go.transform.SetParent(transform, false);
        LineRenderer lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        _ownedTracerMaterial = new Material(shader);
        lr.material = _ownedTracerMaterial;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        return lr;
    }

    /// <summary>Sells the tower, refunding part of its cost to the player.</summary>
    public void Sell()
    {
        if (towerData != null && PlayerStats.Instance != null)
        {
            PlayerStats.Instance.AddScrap(Mathf.RoundToInt(towerData.scrapCost * towerData.sellRefund));
        }
        Destroy(gameObject);
    }

    void OnDrawGizmosSelected()
    {
        float range = towerData != null ? towerData.range : 15f;
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, range);
        if (CurrentTarget != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(GetFirePosition(), CurrentTarget.transform.position);
        }
    }
}
