using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(AudioSource))]
public class PC_Voice_Manager : MonoBehaviour
{
    [Header("References")]
    public Transform playerTransform;
    public PlayerStats playerStats;
    
    [Header("Settings")]
    public float detectionRadius = 8.0f; // How close player must be to hear
    public float minTimeBetweenLines = 15.0f; // Don't speak too often
    
    [Header("Thresholds")]
    public float lowHealthThreshold = 40f; // Trigger if health is below this
    public int richScrapThreshold = 300;   // Trigger if scrap is above this

    [Header("Audio Clips")]
    [Tooltip("General sarcasm and lore.")]
    public List<AudioClip> idleQuips;
    [Tooltip("Helpful gameplay tips.")]
    public List<AudioClip> gameplayHints;
    [Tooltip("Specific insults when player is hurt.")]
    public List<AudioClip> lowHealthLines;
    [Tooltip("Specific comments when player has money.")]
    public List<AudioClip> richPlayerLines;

    private AudioSource _audioSource;
    private float _cooldownTimer;
    private bool _hasPlayedLowHealthLine = false; // Prevent spamming the same line
    private bool _hasPlayedRichLine = false;

    void Start()
    {
        _audioSource = GetComponent<AudioSource>();
        
        // Auto-find player if not assigned
        if (playerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerTransform = player.transform;
                playerStats = player.GetComponent<PlayerStats>();
            }
        }
        
        _cooldownTimer = minTimeBetweenLines;
    }

    void Update()
    {
        if (playerTransform == null || playerStats == null) return;

        // Count down cooldown
        if (_cooldownTimer > 0) _cooldownTimer -= Time.deltaTime;

        // Check Distance
        float distance = Vector3.Distance(transform.position, playerTransform.position);
        
        if (distance <= detectionRadius)
        {
            // Only try to speak if cooldown is ready and we aren't currently talking
            if (_cooldownTimer <= 0 && !_audioSource.isPlaying)
            {
                TryTriggerVoiceLine();
            }
            
            // Reset "One-time" flags if conditions change (e.g. player healed)
            if (playerStats.currentHealth > lowHealthThreshold) _hasPlayedLowHealthLine = false;
            if (playerStats.scrapMetal < richScrapThreshold) _hasPlayedRichLine = false;
        }
    }

    void TryTriggerVoiceLine()
    {
        // PRIORITY 1: Low Health (If haven't commented on it yet)
        if (playerStats.currentHealth < lowHealthThreshold && !_hasPlayedLowHealthLine)
        {
            if (PlayRandomClip(lowHealthLines))
            {
                _hasPlayedLowHealthLine = true;
                _cooldownTimer = minTimeBetweenLines;
                return;
            }
        }

        // PRIORITY 2: Rich Player (If haven't commented on it yet)
        if (playerStats.scrapMetal >= richScrapThreshold && !_hasPlayedRichLine)
        {
            if (PlayRandomClip(richPlayerLines))
            {
                _hasPlayedRichLine = true;
                _cooldownTimer = minTimeBetweenLines;
                return;
            }
        }

        // PRIORITY 3: Random Idle / Hint (50/50 chance)
        // We only do this occasionally (random chance per frame is bad, so we rely on the cooldown timer loop)
        // Since TryTriggerVoiceLine is only called when cooldown is 0, we are guaranteed to try something.
        
        if (Random.value > 0.5f)
        {
            PlayRandomClip(idleQuips);
        }
        else
        {
            PlayRandomClip(gameplayHints);
        }

        // Reset cooldown
        _cooldownTimer = minTimeBetweenLines + Random.Range(0, 10f); // Add variation
    }

    bool PlayRandomClip(List<AudioClip> clips)
    {
        if (clips == null || clips.Count == 0) return false;

        AudioClip clip = clips[Random.Range(0, clips.Count)];
        _audioSource.PlayOneShot(clip);
        return true;
    }

    // Visualize the radius
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
    }
}