using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>
/// A door that guards the Core. Enemies attack it when their path to the Core is blocked.
/// While intact it carves the NavMesh, so nothing driven by a NavMeshAgent (bugs, robot dogs,
/// anything) can path or get pushed through it. When its health hits zero it breaks apart
/// into debris and the hole in the NavMesh closes up so enemies can pour in.
/// </summary>
[RequireComponent(typeof(NavMeshObstacle))]
public class DoorHealth : MonoBehaviour, IDamageable
{
    // --- GLOBAL TRACKING ---
    public static int DestroyedDoorCount = 0;
    public static readonly List<DoorHealth> ActiveDoors = new List<DoorHealth>();

    /// <summary>Fired whenever any door takes damage (door, current health, max health).</summary>
    public static event Action<DoorHealth, float, float> OnAnyDoorDamaged;
    /// <summary>Fired when any door breaks.</summary>
    public static event Action<DoorHealth> OnAnyDoorDestroyed;
    // -----------------------

    [Header("Door Stats")]
    public float maxHealth = 200f;

    [Header("Blocking")]
    [Tooltip("Size the NavMeshObstacle to match the door's BoxCollider so the carved hole covers the whole door.")]
    public bool fitObstacleToCollider = true;

    [Header("Damage Feedback")]
    [Tooltip("Tint the door shifts toward as it loses health.")]
    public Color damagedTint = new Color(0.35f, 0.08f, 0.05f);
    [Tooltip("Color the door flashes when hit.")]
    public Color hitFlashColor = new Color(1f, 0.45f, 0.2f);
    public float hitFlashDuration = 0.08f;
    [Tooltip("How far the door jolts when hit.")]
    public float hitShakeAmount = 0.04f;
    public float hitShakeDuration = 0.12f;
    public AudioClip[] hitSounds;
    [Range(0f, 1f)] public float hitVolume = 0.6f;
    [Tooltip("Optional particle effect spawned where the door gets hit.")]
    public GameObject hitEffectPrefab;
    [Tooltip("Minimum seconds between hit effects so a swarm doesn't spam particles/sounds.")]
    public float hitEffectInterval = 0.15f;

    [Header("Break")]
    [Tooltip("Optional effect (explosion, dust) spawned when the door breaks.")]
    public GameObject breakEffectPrefab;
    public AudioClip breakSound;
    [Range(0f, 1f)] public float breakVolume = 1f;
    [Tooltip("Debris chunks along each axis (x = width, y = height). Set either to 0 to skip debris.")]
    public Vector2Int debrisGrid = new Vector2Int(4, 4);
    public float debrisForce = 6f;
    public float debrisLifetime = 4f;

    private float currentHealth;
    private NavMeshObstacle obstacle;
    private Collider doorCollider;
    private Renderer[] renderers;
    private MaterialPropertyBlock propertyBlock;
    private Color[] baseColors;
    private Vector3 restLocalPosition;
    private float flashTimer;
    private float shakeTimer;
    private float lastHitEffectTime = -10f;
    private bool isBroken;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    public new Transform transform => base.transform;
    public float CurrentHealth => currentHealth;
    public float HealthPercent => maxHealth > 0f ? Mathf.Clamp01(currentHealth / maxHealth) : 0f;
    public bool IsBroken => isBroken;

    // Static counters survive scene reloads, so clear them whenever a new scene loads.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        DestroyedDoorCount = 0;
        ActiveDoors.Clear();
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Single) DestroyedDoorCount = 0;
    }

    void Awake()
    {
        obstacle = GetComponent<NavMeshObstacle>();
        doorCollider = GetComponent<Collider>();
        renderers = GetComponentsInChildren<Renderer>();
        propertyBlock = new MaterialPropertyBlock();

        ConfigureBlocking();
        CacheBaseColors();
    }

    void OnEnable()
    {
        if (!ActiveDoors.Contains(this)) ActiveDoors.Add(this);
    }

    void OnDisable()
    {
        ActiveDoors.Remove(this);
    }

    void Start()
    {
        currentHealth = maxHealth;
        restLocalPosition = transform.localPosition;
    }

    void Update()
    {
        if (flashTimer <= 0f && shakeTimer <= 0f) return;

        if (flashTimer > 0f)
        {
            flashTimer -= Time.deltaTime;
            ApplyTint(flashTimer > 0f);
        }

        if (shakeTimer > 0f)
        {
            shakeTimer -= Time.deltaTime;
            if (shakeTimer > 0f)
            {
                float strength = hitShakeAmount * (shakeTimer / hitShakeDuration);
                transform.localPosition = restLocalPosition + UnityEngine.Random.insideUnitSphere * strength;
            }
            else
            {
                transform.localPosition = restLocalPosition;
            }
        }
    }

    // The door must carve the NavMesh. A non-carving obstacle only nudges agents through local
    // avoidance, so their paths still go straight through and small agents (like the dogs)
    // squeeze past. Carving cuts a real hole, so paths to the Core come back partial and the
    // enemies switch to attacking a door instead.
    private void ConfigureBlocking()
    {
        obstacle.carving = true;
        obstacle.carveOnlyStationary = false;
        obstacle.shape = NavMeshObstacleShape.Box;

        if (fitObstacleToCollider && doorCollider is BoxCollider box)
        {
            obstacle.center = box.center;
            obstacle.size = box.size;
        }

        if (doorCollider != null) doorCollider.isTrigger = false;
    }

    private void CacheBaseColors()
    {
        baseColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            Material mat = renderers[i].sharedMaterial;
            if (mat == null) baseColors[i] = Color.white;
            else if (mat.HasProperty(BaseColorId)) baseColors[i] = mat.GetColor(BaseColorId);
            else if (mat.HasProperty(ColorId)) baseColors[i] = mat.GetColor(ColorId);
            else baseColors[i] = Color.white;
        }
    }

    public void TakeDamage(float amount)
    {
        if (isBroken || amount <= 0f) return;

        currentHealth -= amount;
        OnAnyDoorDamaged?.Invoke(this, Mathf.Max(currentHealth, 0f), maxHealth);

        if (currentHealth <= 0f)
        {
            Die();
            return;
        }

        PlayHitFeedback();
    }

    /// <summary>
    /// The spot an attacker at <paramref name="from"/> should stand on to hit this door:
    /// the nearest point on the door's face, pushed out to the walkable NavMesh on the attacker's side.
    /// Spreads a swarm along the whole width of the door instead of piling them on the center.
    /// </summary>
    public Vector3 GetAttackPosition(Vector3 from, float standOff)
    {
        Vector3 surface = ClosestPoint(from);
        Vector3 outward = from - surface;
        outward.y = 0f;
        if (outward.sqrMagnitude < 0.0001f) outward = transform.forward;
        Vector3 desired = surface + outward.normalized * standOff;

        if (NavMesh.SamplePosition(desired, out NavMeshHit hit, standOff + 1.5f, NavMesh.AllAreas)) return hit.position;
        return desired;
    }

    /// <summary>Closest point on the door's collider (or its position if it has none).</summary>
    public Vector3 ClosestPoint(Vector3 from)
    {
        if (doorCollider != null && doorCollider.enabled) return doorCollider.ClosestPoint(from);
        return transform.position;
    }

    private void PlayHitFeedback()
    {
        flashTimer = hitFlashDuration;
        shakeTimer = hitShakeDuration;
        ApplyTint(true);

        if (Time.time - lastHitEffectTime < hitEffectInterval) return;
        lastHitEffectTime = Time.time;

        if (hitSounds != null && hitSounds.Length > 0)
        {
            AudioClip clip = hitSounds[UnityEngine.Random.Range(0, hitSounds.Length)];
            if (clip != null) AudioSource.PlayClipAtPoint(clip, transform.position, hitVolume);
        }

        if (hitEffectPrefab != null)
        {
            GameObject fx = Instantiate(hitEffectPrefab, transform.position, transform.rotation);
            Destroy(fx, 3f);
        }
    }

    // Darkens the door toward damagedTint as health drops, with a short bright flash on hit.
    private void ApplyTint(bool flashing)
    {
        float damage = 1f - HealthPercent;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;
            Color c = Color.Lerp(baseColors[i], damagedTint, damage * 0.75f);
            if (flashing) c = Color.Lerp(c, hitFlashColor, 0.6f);

            renderers[i].GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(BaseColorId, c);
            propertyBlock.SetColor(ColorId, c);
            renderers[i].SetPropertyBlock(propertyBlock);
        }
    }

    private void Die()
    {
        if (isBroken) return;
        isBroken = true;

        DestroyedDoorCount++;
        Debug.Log($"Door Destroyed! Total Destroyed: {DestroyedDoorCount}");

        // Open the NavMesh right away so enemies re-path this frame.
        obstacle.carving = false;
        obstacle.enabled = false;
        if (doorCollider != null) doorCollider.enabled = false;
        ActiveDoors.Remove(this);
        transform.localPosition = restLocalPosition;

        if (breakEffectPrefab != null)
        {
            GameObject fx = Instantiate(breakEffectPrefab, transform.position, transform.rotation);
            Destroy(fx, 5f);
        }
        if (breakSound != null) AudioSource.PlayClipAtPoint(breakSound, transform.position, breakVolume);

        SpawnDebris();
        OnAnyDoorDestroyed?.Invoke(this);

        Destroy(gameObject);
    }

    // Breaks the door into a grid of chunks that use the door's own material, then fling them inward.
    private void SpawnDebris()
    {
        if (debrisGrid.x <= 0 || debrisGrid.y <= 0) return;

        Renderer mainRenderer = GetComponent<Renderer>();
        BoxCollider box = doorCollider as BoxCollider;
        Vector3 localSize = box != null ? box.size : Vector3.one;
        Vector3 localCenter = box != null ? box.center : Vector3.zero;
        Vector3 worldSize = Vector3.Scale(localSize, transform.lossyScale);
        Vector3 chunkSize = new Vector3(worldSize.x / debrisGrid.x, worldSize.y / debrisGrid.y, Mathf.Abs(worldSize.z));

        int debrisLayer = LayerMask.NameToLayer("Ignore Raycast");
        Material material = mainRenderer != null ? mainRenderer.sharedMaterial : null;
        Color tint = Color.Lerp(baseColors.Length > 0 ? baseColors[0] : Color.white, damagedTint, 0.75f);

        for (int x = 0; x < debrisGrid.x; x++)
        {
            for (int y = 0; y < debrisGrid.y; y++)
            {
                Vector3 local = localCenter + new Vector3(
                    ((x + 0.5f) / debrisGrid.x - 0.5f) * localSize.x,
                    ((y + 0.5f) / debrisGrid.y - 0.5f) * localSize.y,
                    0f);
                Vector3 worldPos = transform.TransformPoint(local);

                GameObject chunk = GameObject.CreatePrimitive(PrimitiveType.Cube);
                chunk.name = "DoorDebris";
                if (debrisLayer >= 0) chunk.layer = debrisLayer;
                chunk.transform.SetPositionAndRotation(worldPos, transform.rotation);
                chunk.transform.localScale = chunkSize * UnityEngine.Random.Range(0.75f, 0.95f);

                Renderer r = chunk.GetComponent<Renderer>();
                if (material != null) r.sharedMaterial = material;
                MaterialPropertyBlock block = new MaterialPropertyBlock();
                block.SetColor(BaseColorId, tint);
                block.SetColor(ColorId, tint);
                r.SetPropertyBlock(block);

                Rigidbody rb = chunk.AddComponent<Rigidbody>();
                rb.mass = 0.5f;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                Vector3 push = (UnityEngine.Random.value > 0.5f ? transform.forward : -transform.forward) + UnityEngine.Random.insideUnitSphere * 0.6f + Vector3.up * 0.3f;
                rb.AddForce(push.normalized * debrisForce * UnityEngine.Random.Range(0.6f, 1.2f), ForceMode.Impulse);
                rb.AddTorque(UnityEngine.Random.insideUnitSphere * debrisForce, ForceMode.Impulse);

                DebrisShrink shrink = chunk.AddComponent<DebrisShrink>();
                shrink.lifetime = debrisLifetime * UnityEngine.Random.Range(0.8f, 1.2f);
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.35f);
        Gizmos.matrix = transform.localToWorldMatrix;
        BoxCollider box = GetComponent<BoxCollider>();
        if (box != null) Gizmos.DrawCube(box.center, box.size);
    }
}
