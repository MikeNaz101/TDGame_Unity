using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Linq;

[RequireComponent(typeof(NavMeshAgent))]
public class ScrapCollectorBot : MonoBehaviour
{
    // Simple state machine for the bot's behavior
    private enum BotState { Idle, Fetching, Returning }

    [Header("References")]
    [Tooltip("The player's stat script, to know who to give scrap to. Will try to auto-find if null.")]
    public PlayerStats playerStats;

    [Header("Tuning")]
    [Tooltip("How close the bot needs to get to scrap to pick it up.")]
    public float collectionDistance = 1.5f;
    [Tooltip("How far the bot will scan for scrap from its home position.")]
    public float detectionRange = 30f;

    private NavMeshAgent agent;
    private Vector3 homePosition; // The spot it returns to
    private BotState state = BotState.Idle;
    private Transform currentScrapTarget;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        homePosition = transform.position;

        // Auto-find PlayerStats if it wasn't assigned in the Inspector
        if (playerStats == null)
        {
            playerStats = FindObjectOfType<PlayerStats>();
        }

        if (playerStats == null)
        {
            Debug.LogError("Scrap Bot can't find PlayerStats! Scrap will not be collected.");
        }
    }

    void Update()
    {
        // Run the logic for the bot's current state
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
        }
    }

    void UpdateIdle()
    {
        // Make sure the bot is stopped
        if (agent.hasPath)
        {
            agent.ResetPath();
        }

        // Look for the nearest piece of scrap
        Transform nearestScrap = FindNearestScrap();
        if (nearestScrap != null)
        {
            // Found scrap! Go get it.
            currentScrapTarget = nearestScrap;
            agent.SetDestination(currentScrapTarget.position);
            state = BotState.Fetching;
        }
    }

    void UpdateFetching()
    {
        if (currentScrapTarget == null)
        {
            // The scrap must have been picked up by the player first. Go home.
            state = BotState.Returning;
            return;
        }

        // Check if we've reached the scrap
        if (Vector3.Distance(transform.position, currentScrapTarget.position) <= collectionDistance)
        {
            // We're close enough, collect it.
            CollectScrap(currentScrapTarget.gameObject);
        }
    }

    void UpdateReturning()
    {
        // Set destination to home, if we aren't already heading there
        if (!agent.hasPath || agent.destination != homePosition)
        {
            agent.SetDestination(homePosition);
        }

        // Check if we've arrived home
        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
        {
            state = BotState.Idle; // We're home. Go back to idle.
        }
    }

    void CollectScrap(GameObject scrapObject)
    {
        if (scrapObject == null) return;

        ScrapMetalPickup scrap = scrapObject.GetComponent<ScrapMetalPickup>();
        
        // Add scrap to the player
        if (scrap != null && playerStats != null)
        {
            playerStats.AddScrap(scrap.value);
        }

        // Destroy the scrap object
        Destroy(scrapObject); // This triggers OnDestroy()

        // Go home
        currentScrapTarget = null;
        state = BotState.Returning;
    }

    Transform FindNearestScrap()
    {
        // Clean the list of any scrap that might have been destroyed by other means
        ScrapMetalPickup.AvailableScrap.RemoveAll(item => item == null);

        Transform nearest = null;
        float minDistance = Mathf.Infinity;

        // Loop through all available scrap
        foreach (Transform scrap in ScrapMetalPickup.AvailableScrap)
        {
            float distance = Vector3.Distance(transform.position, scrap.position);
            
            // Check if it's in range and the closest one we've found so far
            if (distance <= detectionRange && distance < minDistance)
            {
                minDistance = distance;
                nearest = scrap;
            }
        }
        return nearest;
    }

    // Public method called by ScrapMetalPickup.cs if the bot's trigger hits it
    public void NotifyCollection(ScrapMetalPickup scrap)
    {
        if (scrap != null)
        {
             CollectScrap(scrap.gameObject);
        }
    }
}
