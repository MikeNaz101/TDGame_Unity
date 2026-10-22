using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class ScrapCollectorBot : MonoBehaviour
{
    private enum BotState { Idle, Fetching, Returning }

    [Header("References")]
    [Tooltip("The player's stat script, to know who to give scrap to. Will try to auto-find if null.")]
    public PlayerStats playerStats;

    [Header("Tuning")]
    [Tooltip("How close the bot needs to get to scrap to pick it up.")]
    public float collectionDistance = 1.5f;
    [Tooltip("How far the bot will scan for scrap from its current position.")]
    public float detectionRange = 30f;
    [Tooltip("How often (seconds) the bot re-scans for scrap while idle.")]
    public float scanInterval = 0.5f;
    [Tooltip("Give up on a piece of scrap if it takes longer than this to reach.")]
    public float fetchTimeout = 15f;

    [Header("Feedback (optional)")]
    public AudioSource audioSource;
    public AudioClip collectSound;

    public int Collected { get; private set; }

    private NavMeshAgent agent;
    private Vector3 homePosition;
    private BotState state = BotState.Idle;
    private Transform currentScrapTarget;
    private float _scanTimer;
    private float _fetchTimer;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        homePosition = transform.position;

        if (playerStats == null) playerStats = PlayerStats.Instance != null ? PlayerStats.Instance : FindFirstObjectByType<PlayerStats>();
        if (playerStats == null) Debug.LogError("Scrap Bot can't find PlayerStats! Scrap will not be collected.", this);
    }

    void Update()
    {
        if (!agent.enabled || !agent.isOnNavMesh) return;

        switch (state)
        {
            case BotState.Idle:      UpdateIdle(); break;
            case BotState.Fetching:  UpdateFetching(); break;
            case BotState.Returning: UpdateReturning(); break;
        }
    }

    void UpdateIdle()
    {
        _scanTimer -= Time.deltaTime;
        if (_scanTimer > 0f) return;
        _scanTimer = scanInterval;

        Transform nearestScrap = FindNearestScrap();
        if (nearestScrap != null)
        {
            currentScrapTarget = nearestScrap;
            agent.isStopped = false;
            agent.SetDestination(currentScrapTarget.position);
            _fetchTimer = 0f;
            state = BotState.Fetching;
        }
    }

    void UpdateFetching()
    {
        if (currentScrapTarget == null)
        {
            // Picked up by the player (or expired) before we got there.
            state = BotState.Returning;
            return;
        }

        _fetchTimer += Time.deltaTime;
        if (_fetchTimer > fetchTimeout)
        {
            currentScrapTarget = null;
            state = BotState.Returning;
            return;
        }

        // Scrap can be pulled around by the player magnet; keep the destination fresh.
        if ((agent.destination - currentScrapTarget.position).sqrMagnitude > 1f)
        {
            agent.SetDestination(currentScrapTarget.position);
        }

        if ((transform.position - currentScrapTarget.position).sqrMagnitude <= collectionDistance * collectionDistance)
        {
            CollectScrap(currentScrapTarget.GetComponent<ScrapMetalPickup>());
        }
    }

    void UpdateReturning()
    {
        if (!agent.hasPath || (agent.destination - homePosition).sqrMagnitude > 0.25f)
        {
            agent.isStopped = false;
            agent.SetDestination(homePosition);
        }

        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.1f)
        {
            agent.ResetPath();
            state = BotState.Idle;
            _scanTimer = 0f;
        }
    }

    void CollectScrap(ScrapMetalPickup scrap)
    {
        if (scrap != null && scrap.TryTake())
        {
            if (playerStats != null) playerStats.AddScrap(scrap.value);
            Collected++;
            if (audioSource != null && collectSound != null) audioSource.PlayOneShot(collectSound);
        }

        currentScrapTarget = null;
        state = BotState.Returning;
    }

    Transform FindNearestScrap()
    {
        var list = ScrapMetalPickup.AvailableScrap;
        list.RemoveAll(item => item == null);

        Transform nearest = null;
        float bestSqr = detectionRange * detectionRange;
        Vector3 pos = transform.position;

        for (int i = 0; i < list.Count; i++)
        {
            float sqr = (list[i].position - pos).sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                nearest = list[i];
            }
        }
        return nearest;
    }

    /// <summary>Called by ScrapMetalPickup if the bot's trigger touches a pickup.</summary>
    public void NotifyCollection(ScrapMetalPickup scrap)
    {
        if (scrap != null) CollectScrap(scrap);
    }
}
