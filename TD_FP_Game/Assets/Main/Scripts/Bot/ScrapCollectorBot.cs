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
    public float collectionDistance = 2.5f;
    public float detectionRange = 50f;
    public float baseSpeed = 3.5f;

    [Header("Upgradable Stats")]
    public int carryCapacity = 1;       // Upgrade 1 increases this
    public bool canFly = false;         // Upgrade 2
    public bool canHeal = false;        // Upgrade 3
    public float healAmount = 10f;
    public float healCooldown = 5f;

    // --- Internal State ---
    private NavMeshAgent agent;
    private Vector3 homePosition;
    private BotState state = BotState.Idle;
    private Transform currentTarget;
    
    // Inventory
    private int currentScrapCount = 0;
    private int currentScrapValueStored = 0;
    
    // Healing
    private float lastHealTime = 0f;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        homePosition = transform.position;
        agent.speed = baseSpeed;

        if (playerStats == null)
        {
            playerStats = FindObjectOfType<PlayerStats>();
        }
    }

    // --- PUBLIC UPGRADE METHODS (Called by UI) ---
    public void UpgradeCapacity(int newCapacity)
    {
        carryCapacity = newCapacity;
        Debug.Log("Bot Upgrade: Capacity increased to " + carryCapacity);
    }

    public void UpgradeFlight()
    {
        canFly = true;
        agent.baseOffset = 1.5f; // Hover effect
        agent.speed = baseSpeed * 2.0f; // Fly faster
        agent.acceleration = 20f;
        Debug.Log("Bot Upgrade: Flight Systems Online.");
    }

    public void UpgradeHealing()
    {
        canHeal = true;
        Debug.Log("Bot Upgrade: Medkit Installed.");
    }

    void Update()
    {
        switch (state)
        {
            case BotState.Idle:
                UpdateIdle();
                break;
            case BotState.Fetching:
                UpdateFetching();
                break;
            case BotState.Returning:
                UpdateReturning();
                break;
            case BotState.Healing:
                UpdateHealing();
                break;
        }
    }

    void UpdateIdle()
    {
        // If we are holding scrap, drop it off first
        if (currentScrapCount > 0)
        {
            state = BotState.Returning;
            return;
        }

        // Priority 1: Heal Player (if unlocked and player needs it)
        if (canHeal && playerStats.currentHealth < playerStats.maxHealth && Time.time > lastHealTime + healCooldown)
        {
            state = BotState.Healing;
            return;
        }

        // Priority 2: Get Scrap
        // Only look for scrap if we have room
        if (currentScrapCount < carryCapacity)
        {
            Transform nearestScrap = FindNearestScrap();
            if (nearestScrap != null)
            {
                currentTarget = nearestScrap;
                agent.SetDestination(currentTarget.position);
                state = BotState.Fetching;
            }
            else
            {
                // No scrap found, return to home/follow player loosely
                if(Vector3.Distance(transform.position, homePosition) > 5f)
                {
                    agent.SetDestination(homePosition);
                }
            }
        }
        else
        {
            // Full capacity
            state = BotState.Returning;
        }
    }

    void UpdateFetching()
    {
        // Target validation
        if (currentTarget == null)
        {
            state = BotState.Idle;
            return;
        }

        // Go to scrap
        if (!agent.pathPending && agent.remainingDistance <= collectionDistance)
        {
            CollectScrap(currentTarget.gameObject);
        }
    }

    void UpdateReturning()
    {
        // Go home
        agent.SetDestination(homePosition);

        if (!agent.pathPending && agent.remainingDistance <= collectionDistance)
        {
            // Deposit all scrap
            if (playerStats != null && currentScrapValueStored > 0)
            {
                playerStats.AddScrap(currentScrapValueStored);
                Debug.Log($"Bot deposited {currentScrapValueStored} scrap.");
            }

            // Reset inventory
            currentScrapCount = 0;
            currentScrapValueStored = 0;

            state = BotState.Idle;
        }
    }

    void UpdateHealing()
    {
        if (playerStats == null) return;

        // Approach Player
        agent.SetDestination(playerStats.transform.position);

        if (!agent.pathPending && agent.remainingDistance <= collectionDistance)
        {
            // Heal
            playerStats.Heal(healAmount);
            lastHealTime = Time.time;
            
            // Go back to work
            state = BotState.Idle;
        }
    }

    void CollectScrap(GameObject scrapObject)
    {
        ScrapMetalPickup scrap = scrapObject.GetComponent<ScrapMetalPickup>();
        
        if (scrap != null)
        {
            currentScrapValueStored += scrap.value;
            currentScrapCount++;
        }

        Destroy(scrapObject);

        // Decide next move:
        // If we have space, look for more scrap nearby immediately
        if (currentScrapCount < carryCapacity)
        {
            Transform nextScrap = FindNearestScrap();
            if (nextScrap != null)
            {
                currentTarget = nextScrap;
                agent.SetDestination(currentTarget.position);
                state = BotState.Fetching;
                return;
            }
        }

        // If full or no more scrap, go home
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
            float distance = Vector3.Distance(transform.position, scrap.position);
            
            if (distance <= detectionRange && distance < minDistance)
            {
                minDistance = distance;
                nearest = scrap;
            }
        }
        return nearest;
    }

    // Triggered if the player runs over scrap while bot is nearby? 
    // (Keeping legacy support, though bot logic handles itself mostly now)
    public void NotifyCollection(ScrapMetalPickup scrap)
    {
        if (state != BotState.Returning && currentScrapCount < carryCapacity)
        {
            CollectScrap(scrap.gameObject);
        }
    }
}