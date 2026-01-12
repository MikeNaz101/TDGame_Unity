using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(NavMeshAgent))]
public class ScrapCollectorBot : MonoBehaviour
{
    private enum BotState { Idle, Fetching, Returning, Healing }

    [Header("References")]
    public PlayerStats playerStats;

    [Header("Tuning")]
    public float collectionDistance = 1.5f;
    public float detectionRange = 50f;
    public float baseSpeed = 5f;
    
    [Header("Safety Protocols")]
    public float fallThreshold = -20f; 
    public float stuckCheckInterval = 3.0f;
    public float minMoveDistance = 0.5f;

    [Header("Upgradable Stats")]
    public int carryCapacity = 1;       
    public bool canFly = false;         
    public bool canHeal = false;        
    public float healAmount = 10f;
    public float healCooldown = 5f;

    private NavMeshAgent agent;
    private Vector3 homePosition; // This is set in Start()
    private BotState state = BotState.Idle;
    private Transform currentTarget;
    
    private int currentScrapCount = 0;
    private int currentScrapValueStored = 0;
    private float lastHealTime = 0f;

    private float _stuckTimer = 0f;
    private Vector3 _lastPosition;
    
    // Stores scrap that we tried to get but failed (stuck/unreachable)
    private List<Transform> _blacklistedScrap = new List<Transform>();

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        homePosition = transform.position; // <--- This is the point you want
        agent.speed = baseSpeed;
        _lastPosition = transform.position;

        if (playerStats == null) playerStats = FindObjectOfType<PlayerStats>();
    }
    
    // --- NEW PUBLIC METHOD FOR ENEMIES ---
    public Vector3 GetHomePosition()
    {
        return homePosition;
    }
    // -------------------------------------

    public void UpgradeCapacity(int newCapacity) { carryCapacity = newCapacity; }

    public void UpgradeFlight()
    {
        canFly = true;
        agent.baseOffset = 3.5f; 
        agent.speed = baseSpeed * 2.5f; 
        agent.acceleration = 20f;
    }

    public void UpgradeHealing() { canHeal = true; }

    void Update()
    {
        MonitorSafety();
        switch (state)
        {
            case BotState.Idle: UpdateIdle(); break;
            case BotState.Fetching: UpdateFetching(); break;
            case BotState.Returning: UpdateReturning(); break;
            case BotState.Healing: UpdateHealing(); break;
        }
    }

    void MonitorSafety()
    {
        if (transform.position.y < fallThreshold) { ForceResetHome(); return; }

        if (state != BotState.Idle)
        {
            _stuckTimer += Time.deltaTime;
            if (_stuckTimer >= stuckCheckInterval)
            {
                if (Vector3.Distance(transform.position, _lastPosition) < minMoveDistance) ForceResetHome();
                _stuckTimer = 0f;
                _lastPosition = transform.position;
            }
        }
        else { _stuckTimer = 0f; _lastPosition = transform.position; }
    }

    void ForceResetHome()
    {
        // --- NEW: Add the problematic target to blacklist ---
        if (state == BotState.Fetching && currentTarget != null)
        {
            if (!_blacklistedScrap.Contains(currentTarget))
            {
                _blacklistedScrap.Add(currentTarget);
            }
        }
        agent.Warp(homePosition);
        state = BotState.Idle;
        currentTarget = null;
        _stuckTimer = 0f;
        _lastPosition = homePosition;
    }

    void UpdateIdle()
    {
        //if (currentScrapCount > 0) { state = BotState.Returning; return; }

        if (canHeal && playerStats.currentHealth < playerStats.maxHealth && Time.time > lastHealTime + healCooldown)
        {
            state = BotState.Healing; return;
        }

        if (currentScrapCount < carryCapacity)
        {
            Transform nearestScrap = FindNearestScrap();
            if (nearestScrap != null)
            {
                currentTarget = nearestScrap;
                agent.SetDestination(currentTarget.position);
                state = BotState.Fetching;
            }
            else if(Vector3.Distance(transform.position, homePosition) > 5f) agent.SetDestination(homePosition);
        }
        else { state = BotState.Returning; }
    }

    void UpdateFetching()
    {
        if (currentTarget == null) { state = BotState.Idle; return; }
        if (!agent.pathPending && agent.remainingDistance <= collectionDistance) CollectScrap(currentTarget.gameObject);
    }

    void UpdateReturning()
    {
        agent.SetDestination(homePosition);
        if (!agent.pathPending && agent.remainingDistance <= collectionDistance)
        {
            if (playerStats != null && currentScrapValueStored > 0) playerStats.AddScrap(currentScrapValueStored);
            currentScrapCount = 0;
            currentScrapValueStored = 0;
            state = BotState.Idle;
        }
    }

    void UpdateHealing()
    {
        if (playerStats == null) return;
        agent.SetDestination(playerStats.transform.position);
        if (!agent.pathPending && agent.remainingDistance <= collectionDistance)
        {
            playerStats.Heal(healAmount);
            lastHealTime = Time.time;
            state = BotState.Idle;
        }
    }

    void CollectScrap(GameObject scrapObject)
    {
        ScrapMetalPickup scrap = scrapObject.GetComponent<ScrapMetalPickup>();
        if (scrap != null) { currentScrapValueStored += scrap.value; currentScrapCount++; }
        Destroy(scrapObject);
        
        // --- NEW: SUCCESS! CLEAR BLACKLIST ---
        // Since we successfully did something, maybe the path is clear now.
        // We clear the list so we can try those items again later.
        _blacklistedScrap.Clear();

        if (currentScrapCount < carryCapacity)
        {
            Transform nextScrap = FindNearestScrap();
            if (nextScrap != null) { currentTarget = nextScrap; agent.SetDestination(currentTarget.position); state = BotState.Fetching; return; }
        }
        currentTarget = null;
        state = BotState.Returning;
    }

    Transform FindNearestScrap()
    {
        ScrapMetalPickup.AvailableScrap.RemoveAll(item => item == null);
        Transform nearest = null;
        float minDistance = Mathf.Infinity;
        foreach (Transform scrap in ScrapMetalPickup.AvailableScrap)
        {
            // --- NEW: Skip Blacklisted Items ---
            if (_blacklistedScrap.Contains(scrap)) continue;
            
            float distance = Vector3.Distance(transform.position, scrap.position);
            if (distance <= detectionRange && distance < minDistance) { minDistance = distance; nearest = scrap; }
        }
        return nearest;
    }

    public void NotifyCollection(ScrapMetalPickup scrap)
    {
        if (state != BotState.Returning && currentScrapCount < carryCapacity) CollectScrap(scrap.gameObject);
    }
}