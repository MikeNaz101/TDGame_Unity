using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(SphereCollider))]
[RequireComponent(typeof(AudioSource))]
public class TowerController : MonoBehaviour
{
    public enum TargetingPriority
    {
        First,      // Closest to the core (furthest along the path)
        Last,       // Furthest from the core
        Weakest,
        Strongest
    }

    [Header("Tower Components")]
    [SerializeField] private Transform _turretBase;
    [SerializeField] private Transform _barrel;
    [SerializeField] public Transform _shootPoint;

    // Auto-found on the ShootPoint (or its children) in Awake.
    protected ParticleSystem _muzzleFlash;

    // --- VISUALS ---
    [Header("Visuals")]
    [SerializeField] private Renderer _towerRenderer;
    [SerializeField] private int _materialIndex = 0;
    [SerializeField] private Color _emissionColor = Color.cyan;
    [SerializeField] private float _maxEmissionIntensity = 2.0f;

    [Header("Animation")]
    [Tooltip("Scale the tower in with a little bounce when it is built.")]
    [SerializeField] private bool _playBuildAnimation = true;
    [SerializeField] private float _buildAnimDuration = 0.4f;
    [Tooltip("How far the barrel kicks back when firing (local units). 0 = off.")]
    [SerializeField] private float _recoilDistance = 0.1f;
    [SerializeField] private float _recoilRecoverSpeed = 10f;
    [Tooltip("Scale punch played when an upgrade is bought.")]
    [SerializeField] private float _upgradePunchScale = 0.15f;

    [Header("Targeting")]
    [SerializeField] private TargetingPriority _targetingPriority = TargetingPriority.First;
    [Tooltip("How often (seconds) the tower re-evaluates the best target while it already has one.")]
    [SerializeField] private float _retargetInterval = 0.25f;
    [Tooltip("Aim this far above the enemy's pivot (scaled by enemy size) so shots hit the body, not the feet.")]
    [SerializeField] private float _aimHeightOffset = 1.0f;

    [Header("Tuning")]
    [SerializeField] private float _aimSpeed = 10f;
    [SerializeField] private float _aimTolerance = 3f;

    [Header("Idle Behavior")]
    [SerializeField] private float _idleScanSpeed = 1.0f;
    [SerializeField] private float _idleScanAngle = 45f;
    [SerializeField] private bool _resetToForward = true;

    private Quaternion _initialBaseRotation;
    private Quaternion _initialBarrelRotation;
    private Vector3 _barrelRestPosition;
    private float _recoilOffset;
    private float _randomOffset;
    private Vector3 _baseScale;
    private Coroutine _scaleRoutine;
    private float _retargetTimer;
    private float _lastEmission = -1f;

    private static readonly int EmissionColorID = Shader.PropertyToID("_EmissionColor");
    private static readonly WaitForSeconds TraceWait = new WaitForSeconds(0.07f);
    private static Transform _coreTransform;

    // --- Public Properties ---
    public string TowerName { get; private set; }
    public int TowerID { get; private set; }
    public int Level { get; private set; } = 1;
    public int[] PathProgress { get; set; }
    public TowerData Data => _towerData;

    /// <summary>Final range after upgrades and global bonuses.</summary>
    public float CurrentRange => BaseRange * _rangeMultiplier * _globalRangeMult;
    /// <summary>Combined damage multiplier: path upgrades, level, global upgrades and radar buffs.</summary>
    public float DamageMultiplier => _damageMultiplier * _upgradeMultiplier * _globalDamageMult * _buffDamageMult;
    /// <summary>Combined fire rate multiplier (higher = faster): path upgrades and radar buffs.</summary>
    public float FireRateMultiplier => _fireRateMultiplier * _buffFireRateMult;
    public float CurrentDamage => _towerData != null ? _towerData.damage * DamageMultiplier : 0f;
    /// <summary>Seconds between shots after all multipliers.</summary>
    public float CurrentFireInterval => _towerData != null ? _towerData.fireRate / Mathf.Max(0.01f, FireRateMultiplier) : 1f;

    // --- Protected References ---
    protected TowerData _towerData;
    protected AudioSource _audioSource;
    protected LineRenderer _hitscanTracer;
    protected PlayerStats _playerStats;
    protected Transform _playerTarget;
    protected Material _targetMaterial;

    // --- Target & State ---
    protected List<EnemyController> _enemiesInRange = new List<EnemyController>();
    protected EnemyController _currentTarget;
    protected float _fireCooldown = 0f;

    // Upgrade multipliers (from upgrade paths). Buffs live in separate fields so a
    // Radar tower leaving can never wipe out upgrades the player paid for.
    protected float _fireRateMultiplier = 1f;
    protected float _damageMultiplier = 1f;
    protected float _rangeMultiplier = 1f;
    protected float _upgradeMultiplier = 1f;
    protected float _globalDamageMult = 1f;
    protected float _globalRangeMult = 1f;
    protected float _buffFireRateMult = 1f;
    protected float _buffDamageMult = 1f;

    private readonly Dictionary<object, Vector2> _buffs = new Dictionary<object, Vector2>();
    private int _scrapInvested;
    private float _prefabRange;

    private SphereCollider _rangeTrigger;

    // Some tower assets leave range at 0 and rely on the prefab's collider radius.
    private float BaseRange => (_towerData != null && _towerData.range > 0f) ? _towerData.range : _prefabRange;

    protected virtual void Awake()
    {
        _rangeTrigger = GetComponent<SphereCollider>();
        _audioSource = GetComponent<AudioSource>();
        _rangeTrigger.isTrigger = true;
        _prefabRange = _rangeTrigger.radius;

        _hitscanTracer = GetComponentInChildren<LineRenderer>();
        if (_hitscanTracer != null) _hitscanTracer.enabled = false;

        TowerID = TowerRegistry.RegisterTower(this);

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            _playerTarget = playerObj.transform;
            _playerStats = playerObj.GetComponent<PlayerStats>();
        }

        if (_coreTransform == null)
        {
            GameObject coreObj = GameObject.FindGameObjectWithTag("Core");
            if (coreObj != null) _coreTransform = coreObj.transform;
        }

        if (_turretBase != null) _initialBaseRotation = _turretBase.rotation;
        if (_barrel != null)
        {
            _initialBarrelRotation = _barrel.localRotation;
            _barrelRestPosition = _barrel.localPosition;
        }

        _randomOffset = Random.Range(0f, 100f);
        _baseScale = transform.localScale;

        if (_towerRenderer != null)
        {
            _targetMaterial = _towerRenderer.materials[_materialIndex];
            _targetMaterial.EnableKeyword("_EMISSION");
        }

        if (_shootPoint != null)
        {
            _muzzleFlash = _shootPoint.GetComponentInChildren<ParticleSystem>();
            if (_muzzleFlash != null) _muzzleFlash.Stop();
        }
    }

    protected virtual void Start()
    {
        RefreshGlobalStats();
        if (_playBuildAnimation && _buildAnimDuration > 0f) _scaleRoutine = StartCoroutine(BuildPopIn());
    }

    public void RefreshGlobalStats()
    {
        if (UpgradeManager.Instance != null)
        {
            _globalDamageMult = UpgradeManager.Instance.GlobalTowerDamageMult;
            _globalRangeMult = UpgradeManager.Instance.GlobalTowerRangeMult;
        }
        RecalculateStats();
    }

    public void Initialize(TowerData data)
    {
        _towerData = data;
        TowerName = _towerData.towerName;
        Level = 1;
        _scrapInvested = _towerData.scrapCost;
        if (_towerData.upgradePaths != null) PathProgress = new int[_towerData.upgradePaths.Count];
        RecalculateStats();
    }

    protected virtual void OnDestroy() { TowerRegistry.UnregisterTower(this); }

    protected void RecalculateStats()
    {
        if (_rangeTrigger != null && _towerData != null)
        {
            _rangeTrigger.radius = CurrentRange;
        }
    }

    public void ApplyUpgrade(TowerUpgradeData upgrade)
    {
        if (upgrade == null) return;

        switch (upgrade.effectType)
        {
            case TowerUpgradeType.Damage: _damageMultiplier += upgrade.effectValue; break;
            case TowerUpgradeType.FireRate: _fireRateMultiplier += upgrade.effectValue; break;
            case TowerUpgradeType.Range: _rangeMultiplier += upgrade.effectValue; break;
            default: ApplySpecificUpgrade(upgrade); break;
        }
        _scrapInvested += upgrade.cost;
        RecalculateStats();
        PlayUpgradeFeedback();
    }

    protected virtual void ApplySpecificUpgrade(TowerUpgradeData upgrade) { }

    #region --- Update / Aiming ---

    protected virtual void Update()
    {
        if (_fireCooldown > 0) _fireCooldown -= Time.deltaTime;

        UpdateEmission();
        UpdateRecoil();

        if (!IsValidTarget(_currentTarget))
        {
            UpdateTarget();
        }
        else
        {
            // Periodically re-pick so Weakest/Strongest/First follow the action.
            _retargetTimer -= Time.deltaTime;
            if (_retargetTimer <= 0f) UpdateTarget();
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

        float time = Time.time + _randomOffset;

        // Gentle pan left/right and nod up/down. Slower than combat aiming so it reads as "searching".
        float angleY = Mathf.Sin(time * _idleScanSpeed) * _idleScanAngle;
        Quaternion targetBaseRot = _initialBaseRotation * Quaternion.Euler(0f, angleY, 0f);
        _turretBase.rotation = SmoothRotate(_turretBase.rotation, targetBaseRot, _aimSpeed * 5f);

        float angleX = Mathf.Cos(time * _idleScanSpeed * 0.7f) * 15f;
        Quaternion targetBarrelRot = _initialBarrelRotation * Quaternion.Euler(angleX, 0f, 0f);
        _barrel.localRotation = SmoothRotate(_barrel.localRotation, targetBarrelRot, _aimSpeed * 5f);
    }

    /// <summary>
    /// Ease-out rotation: fast when far from the target, slowing as it settles, capped at maxDegreesPerSecond.
    /// Looks far less robotic than a constant-speed RotateTowards.
    /// </summary>
    protected static Quaternion SmoothRotate(Quaternion from, Quaternion to, float maxDegreesPerSecond)
    {
        float angle = Quaternion.Angle(from, to);
        if (angle < 0.01f) return to;
        float step = Mathf.Min(Mathf.Max(angle * 10f, 30f), maxDegreesPerSecond) * Time.deltaTime;
        return Quaternion.RotateTowards(from, to, step);
    }

    protected void UpdateEmission()
    {
        if (_targetMaterial == null || _towerData == null) return;

        float chargePercent = 1.0f - Mathf.Clamp01(_fireCooldown / CurrentFireInterval);
        float intensity = chargePercent * _maxEmissionIntensity;
        if (Mathf.Abs(intensity - _lastEmission) < 0.01f) return;
        _lastEmission = intensity;
        _targetMaterial.SetColor(EmissionColorID, _emissionColor * intensity);
    }

    /// <summary>Point the tower should aim at on an enemy (body height, not feet).</summary>
    protected Vector3 GetAimPoint(EnemyController enemy)
    {
        Transform t = enemy.transform;
        return t.position + Vector3.up * (_aimHeightOffset * t.lossyScale.y);
    }

    protected static bool IsValidTarget(EnemyController enemy)
    {
        return enemy != null && !enemy._isDead && enemy.gameObject.activeInHierarchy;
    }

    /// <summary>Removes destroyed, disabled and dead enemies from the in-range list without allocating.</summary>
    protected void CleanEnemyList()
    {
        for (int i = _enemiesInRange.Count - 1; i >= 0; i--)
        {
            if (!IsValidTarget(_enemiesInRange[i]))
            {
                OnEnemyExited(_enemiesInRange[i]);
                _enemiesInRange.RemoveAt(i);
            }
        }
    }

    protected void UpdateTarget()
    {
        _retargetTimer = _retargetInterval;
        CleanEnemyList();

        EnemyController best = null;
        float bestScore = float.MaxValue;
        bool hasCore = _coreTransform != null;

        for (int i = 0; i < _enemiesInRange.Count; i++)
        {
            EnemyController e = _enemiesInRange[i];
            float score;
            switch (_targetingPriority)
            {
                case TargetingPriority.Weakest: score = e.CurrentHealth; break;
                case TargetingPriority.Strongest: score = -e.CurrentHealth; break;
                case TargetingPriority.Last:
                    score = hasCore ? -(e.transform.position - _coreTransform.position).sqrMagnitude : -i;
                    break;
                default: // First
                    score = hasCore ? (e.transform.position - _coreTransform.position).sqrMagnitude : i;
                    break;
            }
            if (score < bestScore) { bestScore = score; best = e; }
        }

        _currentTarget = best;
    }

    protected virtual void AimAtTarget()
    {
        if (_turretBase == null || _barrel == null || _currentTarget == null) return;

        Vector3 targetPosition = GetAimPoint(_currentTarget);
        float maxSpeed = _aimSpeed * 50f;

        // 1. Turret base (yaw)
        Vector3 flatDir = targetPosition - _turretBase.position;
        flatDir.y = 0f;
        if (flatDir.sqrMagnitude > 0.0001f)
        {
            _turretBase.rotation = SmoothRotate(_turretBase.rotation, Quaternion.LookRotation(flatDir), maxSpeed);
        }

        // 2. Barrel (pitch)
        Vector3 dirToTarget = targetPosition - _barrel.position;
        if (dirToTarget.sqrMagnitude < 0.0001f) return;
        Quaternion lookRotBarrel = Quaternion.LookRotation(dirToTarget);
        Quaternion localTargetRot = Quaternion.Inverse(_barrel.parent.rotation) * lookRotBarrel;

        float xAngle = localTargetRot.eulerAngles.x;
        if (xAngle > 180) xAngle -= 360;
        xAngle = Mathf.Clamp(xAngle, -45f, 45f);

        _barrel.localRotation = SmoothRotate(_barrel.localRotation, Quaternion.Euler(xAngle, 0, 0), maxSpeed);
    }

    #endregion

    #region --- Firing ---

    protected virtual void TryFire()
    {
        if (_fireCooldown > 0 || _currentTarget == null || _towerData == null) return;

        Vector3 targetDir = GetAimPoint(_currentTarget) - _shootPoint.position;
        if (Vector3.Angle(_shootPoint.forward, targetDir) < _aimTolerance)
        {
            Shoot();
            _fireCooldown = CurrentFireInterval;
        }
    }

    protected virtual void Shoot()
    {
        PlayFireFeedback();
        if (_towerData.shootSound != null) _audioSource.PlayOneShot(_towerData.shootSound);

        float totalDmgMult = DamageMultiplier;

        if (_towerData.attackType == TowerData.AttackType.Hitscan)
        {
            _currentTarget.TakeDamage(_towerData.damage * totalDmgMult, transform, _towerData.damageType);
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
                pScript.attacker = transform;
                pScript.damageMultiplier = totalDmgMult;
            }

            var homing = proj.GetComponent<HomingProjectile>();
            if (homing)
            {
                homing.damageMultiplier = totalDmgMult;
                homing.Initialize(_towerData, transform, _currentTarget);
            }

            var slowRocket = proj.GetComponent<SlowHomingRocket>();
            if (slowRocket)
            {
                slowRocket.Initialize(_towerData, _currentTarget, transform);
                slowRocket.damageMultiplier = totalDmgMult;
                slowRocket.Launch();
            }
        }
    }

    /// <summary>Muzzle flash + barrel kick. Subclasses with custom Shoot() call this too.</summary>
    protected void PlayFireFeedback(bool playMuzzleFlash = true)
    {
        if (playMuzzleFlash && _muzzleFlash != null) _muzzleFlash.Play();
        _recoilOffset = _recoilDistance;
    }

    protected void UpdateRecoil()
    {
        if (_barrel == null || _recoilDistance <= 0f) return;
        if (_recoilOffset <= 0.0001f)
        {
            if (_recoilOffset != 0f) { _recoilOffset = 0f; _barrel.localPosition = _barrelRestPosition; }
            return;
        }

        // Kick back along the barrel's own forward axis, then ease back into place.
        Vector3 back = _barrel.localRotation * Vector3.forward;
        _barrel.localPosition = _barrelRestPosition - back * _recoilOffset;
        _recoilOffset = Mathf.Lerp(_recoilOffset, 0f, 1f - Mathf.Exp(-_recoilRecoverSpeed * Time.deltaTime));
    }

    protected IEnumerator ShowHitscanTrace()
    {
        if (_currentTarget == null) yield break;
        _hitscanTracer.enabled = true;
        _hitscanTracer.SetPosition(0, _shootPoint.position);
        _hitscanTracer.SetPosition(1, GetAimPoint(_currentTarget));
        yield return TraceWait;
        _hitscanTracer.enabled = false;
    }

    #endregion

    #region --- Animation ---

    private IEnumerator BuildPopIn()
    {
        float t = 0f;
        while (t < _buildAnimDuration)
        {
            t += Time.deltaTime;
            transform.localScale = _baseScale * EaseOutBack(Mathf.Clamp01(t / _buildAnimDuration));
            yield return null;
        }
        transform.localScale = _baseScale;
        _scaleRoutine = null;
    }

    private void PlayUpgradeFeedback()
    {
        if (_upgradePunchScale <= 0f || !isActiveAndEnabled) return;
        if (_scaleRoutine != null) StopCoroutine(_scaleRoutine);
        _scaleRoutine = StartCoroutine(ScalePunch());
    }

    private IEnumerator ScalePunch()
    {
        const float duration = 0.3f;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float n = Mathf.Clamp01(t / duration);
            // Quick swell then a damped wobble back to rest.
            float punch = Mathf.Sin(n * Mathf.PI * 2f) * (1f - n) * _upgradePunchScale;
            transform.localScale = _baseScale * (1f + punch);
            _lastEmission = -1f;
            if (_targetMaterial != null) _targetMaterial.SetColor(EmissionColorID, _emissionColor * (_maxEmissionIntensity * 2f * (1f - n)));
            yield return null;
        }
        transform.localScale = _baseScale;
        _scaleRoutine = null;
    }

    private static float EaseOutBack(float x)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
    }

    #endregion

    #region --- Range Tracking ---

    protected virtual void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Enemy")) return;
        EnemyController enemy = other.GetComponent<EnemyController>();
        if (enemy != null && !_enemiesInRange.Contains(enemy))
        {
            _enemiesInRange.Add(enemy);
            OnEnemyEntered(enemy);
        }
    }

    protected virtual void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Enemy")) return;
        EnemyController enemy = other.GetComponent<EnemyController>();
        if (enemy != null && _enemiesInRange.Remove(enemy))
        {
            OnEnemyExited(enemy);
            if (_currentTarget == enemy) _currentTarget = null;
        }
    }

    /// <summary>Hook for aura towers (Ice, Radar) to apply effects once instead of every frame.</summary>
    protected virtual void OnEnemyEntered(EnemyController enemy) { }
    /// <summary>Called when an enemy leaves range, dies or is destroyed. May receive a destroyed (null) enemy.</summary>
    protected virtual void OnEnemyExited(EnemyController enemy) { }

    #endregion

    // --- HELPERS ---
    public void SetTargetingPriority(int priorityIndex) { _targetingPriority = (TargetingPriority)priorityIndex; UpdateTarget(); }
    public string GetPriorityName() { return _targetingPriority.ToString(); }
    public void CyclePriority() { int current = (int)_targetingPriority; SetTargetingPriority((current + 1) % 4); }
    public virtual void UpgradeTower() { Level++; _upgradeMultiplier += 0.25f; _scrapInvested += GetUpgradeCost(); PlayUpgradeFeedback(); }
    public void SellTower() { Destroy(gameObject); }
    public int GetUpgradeCost() { if (_towerData == null) return 0; return Mathf.RoundToInt(_towerData.scrapCost * 0.75f * Level); }
    /// <summary>Half the base cost plus half of everything spent on upgrades.</summary>
    public int GetSellValue() { if (_towerData == null) return 0; return Mathf.RoundToInt(_towerData.scrapCost * 0.5f + (_scrapInvested - _towerData.scrapCost) * 0.5f); }

    /// <summary>Legacy single-source buff. Radar towers call the sourced overload so multiple radars don't fight.</summary>
    public void ApplyBuff(float fireRateBuff, float damageBuff) { ApplyBuff(this, fireRateBuff, damageBuff); }
    public void RemoveBuff() { RemoveBuff(this); }

    public void ApplyBuff(object source, float fireRateBuff, float damageBuff)
    {
        _buffs[source] = new Vector2(fireRateBuff, damageBuff);
        RecalculateBuffs();
    }

    public void RemoveBuff(object source)
    {
        if (_buffs.Remove(source)) RecalculateBuffs();
    }

    private void RecalculateBuffs()
    {
        // Strongest buff wins on each stat; buffs never stack multiplicatively.
        _buffFireRateMult = 1f;
        _buffDamageMult = 1f;
        foreach (var kvp in _buffs)
        {
            if (kvp.Value.x > _buffFireRateMult) _buffFireRateMult = kvp.Value.x;
            if (kvp.Value.y > _buffDamageMult) _buffDamageMult = kvp.Value.y;
        }
    }
}
