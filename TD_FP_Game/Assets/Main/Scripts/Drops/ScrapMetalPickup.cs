using UnityEngine;
using System.Collections.Generic;

public class ScrapMetalPickup : MonoBehaviour
{
    public int value = 10;

    [Header("Magnet")]
    [Tooltip("Pickup flies to the player once they are this close. 0 disables.")]
    public float magnetRadius = 4f;
    public float magnetSpeed = 10f;
    [Tooltip("Distance at which the pickup is collected by the player.")]
    public float collectRadius = 0.9f;

    [Header("Juice")]
    public float spinSpeed = 90f;
    public float bobHeight = 0.15f;
    public float bobSpeed = 2f;

    [Header("Lifetime")]
    [Tooltip("Seconds before uncollected scrap disappears. 0 = never.")]
    public float lifetime = 60f;

    // Global list used by the ScrapCollectorBot.
    public static readonly List<Transform> AvailableScrap = new List<Transform>();

    private Vector3 _basePosition;
    private float _bobOffset;
    private float _lifeTimer;
    private bool _collected;
    private bool _magnetised;

    void Start()
    {
        if (!AvailableScrap.Contains(transform)) AvailableScrap.Add(transform);
        _basePosition = transform.position;
        _bobOffset = Random.value * Mathf.PI * 2f;
    }

    void OnDestroy()
    {
        AvailableScrap.Remove(transform);
    }

    void Update()
    {
        if (_collected) return;

        float dt = Time.deltaTime;
        if (lifetime > 0f)
        {
            _lifeTimer += dt;
            if (_lifeTimer >= lifetime)
            {
                Destroy(gameObject);
                return;
            }
        }

        PlayerStats player = PlayerStats.Instance;
        if (player != null && !player.IsDead)
        {
            Vector3 toPlayer = player.transform.position + Vector3.up * 0.5f - transform.position;
            float sqrDist = toPlayer.sqrMagnitude;

            if (sqrDist <= collectRadius * collectRadius)
            {
                Collect(player);
                return;
            }

            if (magnetRadius > 0f && (_magnetised || sqrDist <= magnetRadius * magnetRadius))
            {
                _magnetised = true;
                transform.position += toPlayer.normalized * (magnetSpeed * dt);
                transform.Rotate(0f, spinSpeed * 3f * dt, 0f, Space.World);
                return;
            }
        }

        // Idle juice: spin + bob
        if (spinSpeed != 0f) transform.Rotate(0f, spinSpeed * dt, 0f, Space.World);
        if (bobHeight > 0f)
        {
            float y = Mathf.Sin((Time.time + _bobOffset) * bobSpeed) * bobHeight;
            transform.position = _basePosition + Vector3.up * y;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_collected) return;

        PlayerStats player = other.GetComponentInParent<PlayerStats>();
        if (player != null)
        {
            Collect(player);
            return;
        }

        ScrapCollectorBot bot = other.GetComponentInParent<ScrapCollectorBot>();
        if (bot != null)
        {
            bot.NotifyCollection(this);
        }
    }

    private void Collect(PlayerStats player)
    {
        if (_collected) return;
        _collected = true;
        player.AddScrap(value);
        Destroy(gameObject);
    }

    /// <summary>Used by the collector bot so the pickup can't be double-counted.</summary>
    public bool TryTake()
    {
        if (_collected) return false;
        _collected = true;
        Destroy(gameObject);
        return true;
    }
}
