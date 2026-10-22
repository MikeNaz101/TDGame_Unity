using System;
using UnityEngine;
using System.Collections;
using StarterAssets;

[RequireComponent(typeof(AudioSource))]
public class WeaponControllerHS : MonoBehaviour
{
    [Header("Setup")]
    [Tooltip("The player camera's shake script for recoil effects.")]
    public CameraShake cameraShake;
    [Tooltip("The main player camera that will be zoomed.")]
    public Camera mainCamera;
    [Tooltip("How much 'range' affects zoom. Higher number = LESS zoom.")]
    public float rangeToZoomRatio = 20f;
    [Tooltip("The object that marks the ADS position.")]
    public Transform adsTarget;
    [Tooltip("The layer mask used for hit scanning (Enemies & Environment).")]
    public LayerMask hitScanLayer;
    [Tooltip("The script that holds the current state of player inputs.")]
    public StarterAssetsInputs inputScript;
    [Tooltip("The dedicated script that handles all building FSM logic.")]
    public BuildManager buildManager;

    [Header("UI")]
    [Tooltip("The simple, static crosshair for hip fire.")]
    public GameObject hipCrosshairUI;
    [Tooltip("The parent RectTransform on your Canvas where ADS reticles will be spawned.")]
    public Transform adsReticleParent;

    [Header("Audio")]
    [Tooltip("The sound to play when the player tries to fire an empty clip.")]
    public AudioClip emptyClipSound;

    [Header("Weapon Data")]
    [Tooltip("List of all weapons the player can switch between.")]
    public WeaponData[] inventory;

    // --- PUBLIC STATE (for Adaptive AI) ---
    [HideInInspector] public int shotsFired = 0;
    [HideInInspector] public int shotsHit = 0;

    // --- EVENTS (HUD) ---
    /// <summary>current clip, reserve</summary>
    public event Action<int, int> OnAmmoChanged;
    public event Action<WeaponData> OnWeaponChanged;
    /// <summary>true = killing blow</summary>
    public event Action<bool> OnEnemyHit;
    public event Action<bool> OnReloadStateChanged;

    // --- PUBLIC PROPERTIES ---
    public bool IsReloading => isReloading;
    public Transform ShootPoint => shootPoint;
    public WeaponData CurrentWeapon => currentWeapon;
    public int CurrentAmmo => currentAmmo;
    public int CurrentReserveAmmo => currentReserveAmmo;
    public bool IsAiming => _isAiming;

    // --- PRIVATE STATE ---
    private WeaponData currentWeapon;
    private GameObject[] _gunInstances;      // one per inventory slot, created once
    private GameObject[] _reticleInstances;  // one per inventory slot, created once
    private GameObject currentGunInstance;
    private GameObject currentAdsReticleInstance;
    private float nextFireTime;
    private int currentAmmo;
    private int currentReserveAmmo;
    private bool isReloading = false;
    private bool wasFiringLastFrame = false;
    private int currentWeaponIndex = 0;
    private Vector3 hipPosition;
    private bool isShootingEnabled = true;
    private float originalFOV;
    private bool _isAiming;
    private bool _lastAimingState;
    private float _tracerTimer;
    private Coroutine _reloadRoutine;
    private int[] _reserveAmmoPerWeapon; // reserve ammo persists across switches

    private Transform gunHolder;
    private Transform shootPoint;
    private LineRenderer currentGunTracer;
    private PlayerStats playerStats;
    private AudioSource _audioSource;

    // --- UNITY LIFECYCLE ---

    void Start()
    {
        playerStats = GetComponentInParent<PlayerStats>();
        gunHolder = transform;
        _audioSource = GetComponent<AudioSource>();
        hipPosition = transform.localPosition;

        if (mainCamera == null && cameraShake != null) mainCamera = cameraShake.GetComponentInParent<Camera>();
        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera != null) originalFOV = mainCamera.fieldOfView;
        else Debug.LogError("Main Camera reference is missing on WeaponControllerHS!", this);

        if (inputScript == null) inputScript = GetComponentInParent<StarterAssetsInputs>();

        BuildWeaponInstances();

        if (inventory != null && inventory.Length > 0)
        {
            EquipWeapon(currentWeaponIndex);
        }

        if (hipCrosshairUI != null) hipCrosshairUI.SetActive(true);
        _lastAimingState = false;
    }

    /// <summary>Instantiates every weapon model and reticle once; switching just toggles them.</summary>
    private void BuildWeaponInstances()
    {
        int count = inventory != null ? inventory.Length : 0;
        _gunInstances = new GameObject[count];
        _reticleInstances = new GameObject[count];
        _reserveAmmoPerWeapon = new int[count];

        for (int i = 0; i < count; i++)
        {
            WeaponData data = inventory[i];
            if (data == null)
            {
                Debug.LogError($"Weapon inventory slot {i} is empty!", this);
                continue;
            }
            _reserveAmmoPerWeapon[i] = data.reserveAmmo;

            if (data.weaponPrefab != null)
            {
                GameObject gun = Instantiate(data.weaponPrefab, gunHolder);
                gun.SetActive(false);
                _gunInstances[i] = gun;
            }
            else
            {
                Debug.LogError($"Weapon '{data.weaponName}' has no weaponPrefab!", data);
            }

            if (data.adsReticlePrefab != null && adsReticleParent != null)
            {
                GameObject reticle = Instantiate(data.adsReticlePrefab, adsReticleParent);
                reticle.SetActive(false);
                _reticleInstances[i] = reticle;
            }
        }

        if (adsReticleParent == null) Debug.LogWarning("AdsReticleParent is not assigned on WeaponControllerHS!", this);
    }

    void Update()
    {
        if (currentWeapon == null || inputScript == null) return;

        if (nextFireTime > 0f) nextFireTime -= Time.deltaTime;

        if (_tracerTimer > 0f)
        {
            _tracerTimer -= Time.deltaTime;
            if (_tracerTimer <= 0f && currentGunTracer != null) currentGunTracer.enabled = false;
        }

        bool gameOver = GameManager.Instance != null && GameManager.Instance.IsGameOver;

        if (isShootingEnabled && !gameOver)
        {
            HandleDirectSwitching();
            HandleADS(true);
            HandleFiringInput();

            if (inputScript.reload && !isReloading && currentAmmo < currentWeapon.maxAmmo && currentReserveAmmo > 0)
            {
                inputScript.reload = false;
                _reloadRoutine = StartCoroutine(Reload());
            }
        }
        else
        {
            // Make sure we don't stay zoomed in while building / interacting.
            SetAiming(false);
            HandleADS(false);
        }

        wasFiringLastFrame = inputScript.fire;
    }

    // --- INPUT HANDLERS (Combat) ---

    void HandleFiringInput()
    {
        bool shotReady = nextFireTime <= 0f;
        bool firePressedThisFrame = inputScript.fire && !wasFiringLastFrame;

        if (!isReloading && shotReady && currentAmmo > 0)
        {
            if (currentWeapon.isAutomatic)
            {
                if (inputScript.fire) Shoot();
            }
            else if (firePressedThisFrame)
            {
                Shoot();
            }
        }
        else if (firePressedThisFrame && currentAmmo <= 0 && !isReloading)
        {
            if (emptyClipSound != null) _audioSource.PlayOneShot(emptyClipSound);
            // Auto reload on an empty click, CoD style.
            if (currentReserveAmmo > 0) _reloadRoutine = StartCoroutine(Reload());
        }
    }

    void HandleDirectSwitching()
    {
        if (inventory == null || inventory.Length <= 1) return;

        int direction = 0;
        if (inputScript.weapon3)
        {
            direction = 1;
            inputScript.weapon3 = false;
        }
        else if (inputScript.weapon2)
        {
            direction = -1;
            inputScript.weapon2 = false;
        }
        if (direction == 0) return;

        int newIndex = (currentWeaponIndex + direction + inventory.Length) % inventory.Length;
        if (newIndex != currentWeaponIndex)
        {
            EquipWeapon(newIndex);
        }
    }

    // --- COMBAT & MOVEMENT HANDLERS ---

    private void SetAiming(bool aiming)
    {
        _isAiming = aiming;
    }

    void HandleADS(bool allowAiming)
    {
        if (allowAiming) SetAiming(inputScript.aim && !isReloading);

        Vector3 targetLocalPosition = hipPosition;
        float targetFOV = originalFOV;

        if (_isAiming)
        {
            if (adsTarget != null) targetLocalPosition = adsTarget.localPosition;
            float zoomAmount = currentWeapon.range / rangeToZoomRatio;
            targetFOV = Mathf.Clamp(originalFOV - zoomAmount, 10f, originalFOV);
        }

        // Only touch the UI when the state actually changes (SetActive every frame is not free).
        if (_isAiming != _lastAimingState)
        {
            _lastAimingState = _isAiming;
            if (hipCrosshairUI != null) hipCrosshairUI.SetActive(!_isAiming);
            if (currentAdsReticleInstance != null) currentAdsReticleInstance.SetActive(_isAiming);
        }

        float lerpSpeed = currentWeapon != null ? currentWeapon.adsSpeed : 10f;
        float t = 1f - Mathf.Exp(-lerpSpeed * Time.deltaTime); // frame-rate independent smoothing
        transform.localPosition = Vector3.Lerp(transform.localPosition, targetLocalPosition, t);
        if (mainCamera != null)
        {
            mainCamera.fieldOfView = Mathf.Lerp(mainCamera.fieldOfView, targetFOV, t);
        }
    }

    void Shoot()
    {
        if (currentAmmo <= 0 || mainCamera == null) return;

        currentAmmo--;
        nextFireTime = currentWeapon.fireRate;
        shotsFired++;
        OnAmmoChanged?.Invoke(currentAmmo, currentReserveAmmo);

        if (currentWeapon.shootSound != null) _audioSource.PlayOneShot(currentWeapon.shootSound);

        Vector3 muzzlePos = shootPoint != null ? shootPoint.position : mainCamera.transform.position;
        EnemyAIAudioEvents.ReportGunshot(muzzlePos);

        // Single parallax-fixed raycast from the camera; reused for damage AND the tracer.
        Ray ray = new Ray(mainCamera.transform.position, mainCamera.transform.forward);
        Vector3 targetPoint;

        if (Physics.Raycast(ray, out RaycastHit hit, currentWeapon.range, hitScanLayer, QueryTriggerInteraction.Ignore))
        {
            targetPoint = hit.point;

            EnemyController enemy = hit.collider.GetComponentInParent<EnemyController>();
            if (enemy != null && !enemy.IsDead)
            {
                float damage = currentWeapon.damage;
                if (currentWeapon.headshotMultiplier > 1f && IsHeadCollider(hit.collider))
                {
                    damage *= currentWeapon.headshotMultiplier;
                }

                enemy.TakeDamage(damage, transform.root);
                shotsHit++;
                OnEnemyHit?.Invoke(enemy.IsDead);

                if (currentWeapon.enemyImpactEffect != null)
                {
                    Destroy(Instantiate(currentWeapon.enemyImpactEffect, hit.point, Quaternion.LookRotation(hit.normal)), 2f);
                }
            }
            else if (currentWeapon.environmentImpactEffect != null)
            {
                Destroy(Instantiate(currentWeapon.environmentImpactEffect, hit.point, Quaternion.LookRotation(hit.normal)), 2f);
            }
        }
        else
        {
            targetPoint = ray.GetPoint(currentWeapon.range);
        }

        if (cameraShake != null) cameraShake.Shake(currentWeapon.recoilKickback);

        ShowTracer(muzzlePos, targetPoint);
        // NOTE: we intentionally do NOT clear inputScript.fire here. The Fire action only reports
        // press/release, so clearing it made automatic weapons fire once per click.
    }

    private static bool IsHeadCollider(Collider c)
    {
        return c.name.IndexOf("head", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private void ShowTracer(Vector3 from, Vector3 to)
    {
        if (currentGunTracer == null) return;
        currentGunTracer.SetPosition(0, from);
        currentGunTracer.SetPosition(1, to);
        currentGunTracer.enabled = true;
        _tracerTimer = Mathf.Max(0.01f, currentWeapon.tracerDuration);
    }

    // --- COROUTINES ---

    IEnumerator Reload()
    {
        if (isReloading) yield break;
        isReloading = true;
        OnReloadStateChanged?.Invoke(true);

        if (currentWeapon.reloadSound != null) _audioSource.PlayOneShot(currentWeapon.reloadSound);

        yield return new WaitForSeconds(currentWeapon.reloadTime);

        int ammoNeeded = currentWeapon.maxAmmo - currentAmmo;
        int ammoToUse = Mathf.Min(ammoNeeded, currentReserveAmmo);
        currentAmmo += ammoToUse;
        currentReserveAmmo -= ammoToUse;
        isReloading = false;
        _reloadRoutine = null;
        OnReloadStateChanged?.Invoke(false);
        OnAmmoChanged?.Invoke(currentAmmo, currentReserveAmmo);
    }

    private void CancelReload()
    {
        if (_reloadRoutine != null)
        {
            StopCoroutine(_reloadRoutine);
            _reloadRoutine = null;
        }
        if (isReloading)
        {
            isReloading = false;
            OnReloadStateChanged?.Invoke(false);
        }
    }

    // --- PUBLIC METHODS ---

    public float GetAccuracy()
    {
        if (shotsFired == 0) return 0f;
        return (float)shotsHit / shotsFired;
    }

    /// <summary>Allows BuildManager / PC interaction to temporarily halt weapon operation.</summary>
    public void ToggleShootingEnabled(bool state)
    {
        isShootingEnabled = state;
        if (!state) CancelReload();
    }

    /// <summary>Adds reserve ammo to the current weapon (ammo pickups, wave rewards).</summary>
    public void AddReserveAmmo(int amount)
    {
        if (currentWeapon == null || amount <= 0) return;
        currentReserveAmmo += amount;
        OnAmmoChanged?.Invoke(currentAmmo, currentReserveAmmo);
    }

    // --- WEAPON EQUIP METHOD ---
    void EquipWeapon(int weaponIndex)
    {
        if (inventory == null || weaponIndex < 0 || weaponIndex >= inventory.Length || inventory[weaponIndex] == null) return;

        CancelReload();

        // Remember reserve ammo of the weapon we're putting away.
        if (currentWeapon != null && _reserveAmmoPerWeapon != null && currentWeaponIndex < _reserveAmmoPerWeapon.Length)
        {
            _reserveAmmoPerWeapon[currentWeaponIndex] = currentReserveAmmo;
        }

        if (currentGunInstance != null) currentGunInstance.SetActive(false);
        if (currentAdsReticleInstance != null) currentAdsReticleInstance.SetActive(false);

        currentWeaponIndex = weaponIndex;
        currentWeapon = inventory[currentWeaponIndex];

        if (currentWeapon.equipSound != null) _audioSource.PlayOneShot(currentWeapon.equipSound);

        currentGunInstance = _gunInstances[currentWeaponIndex];
        currentAdsReticleInstance = _reticleInstances[currentWeaponIndex];

        if (currentGunInstance != null)
        {
            currentGunInstance.SetActive(true);
            currentGunTracer = currentGunInstance.GetComponentInChildren<LineRenderer>(true);
            shootPoint = currentGunInstance.transform.Find("ShootPoint");
            if (shootPoint == null)
            {
                // Fallback: search deeper for a ShootPoint or Muzzle-tagged child.
                foreach (Transform t in currentGunInstance.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == "ShootPoint" || t.CompareTag("Muzzle")) { shootPoint = t; break; }
                }
            }
            if (currentGunTracer != null) currentGunTracer.enabled = false;
        }
        else
        {
            currentGunTracer = null;
            shootPoint = null;
        }

        if (currentAdsReticleInstance != null) currentAdsReticleInstance.SetActive(_isAiming);

        currentAmmo = currentWeapon.maxAmmo;
        currentReserveAmmo = _reserveAmmoPerWeapon[currentWeaponIndex];
        nextFireTime = 0f;
        _tracerTimer = 0f;

        if (currentGunTracer == null) Debug.LogWarning($"Weapon '{currentWeapon.weaponName}' prefab has no LineRenderer; no tracer will be shown.", this);
        if (shootPoint == null) Debug.LogWarning($"Weapon '{currentWeapon.weaponName}' prefab is missing a 'ShootPoint' object.", this);

        OnWeaponChanged?.Invoke(currentWeapon);
        OnAmmoChanged?.Invoke(currentAmmo, currentReserveAmmo);
    }
}
