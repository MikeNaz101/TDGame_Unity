using System;
using UnityEngine;

public class CoreHealth : MonoBehaviour, IDamageable
{
    public static CoreHealth Instance { get; private set; }

    [Header("Core Stats")]
    [Tooltip("The total health of the Core.")]
    public float maxHealth = 1000f;

    [Header("Feedback (optional)")]
    public AudioSource audioSource;
    public AudioClip damagedSound;
    public GameObject destroyedEffect;

    public float CurrentHealth { get; private set; }
    public bool IsDestroyed { get; private set; }

    /// <summary>current, max</summary>
    public event Action<float, float> OnHealthChanged;
    public event Action OnDestroyed;

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        CurrentHealth = maxHealth;
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
    }

    public void TakeDamage(float amount)
    {
        if (IsDestroyed || amount <= 0f) return;

        CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
        if (audioSource != null && damagedSound != null) audioSource.PlayOneShot(damagedSound);
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);

        if (CurrentHealth <= 0f)
        {
            Die();
        }
    }

    public void Repair(float amount)
    {
        if (IsDestroyed || amount <= 0f) return;
        CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + amount);
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
    }

    private void Die()
    {
        if (IsDestroyed) return;
        IsDestroyed = true;

        if (destroyedEffect != null) Instantiate(destroyedEffect, transform.position, Quaternion.identity);
        OnDestroyed?.Invoke();

        if (GameManager.Instance != null)
        {
            GameManager.Instance.GameOver("The Core was destroyed.");
        }
        else
        {
            Debug.LogError("GAME OVER: The Core has been destroyed!");
            Time.timeScale = 0f;
        }
    }
}
