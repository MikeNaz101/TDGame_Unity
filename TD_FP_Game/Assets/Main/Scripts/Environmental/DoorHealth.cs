using System;
using UnityEngine;

// Make sure the object is tagged "Door", sits on the Door layer and has a NavMeshObstacle (carving) component.
[RequireComponent(typeof(UnityEngine.AI.NavMeshObstacle))]
public class DoorHealth : MonoBehaviour, IDamageable
{
    [Header("Door Stats")]
    [Tooltip("The total health of the door.")]
    public float maxHealth = 200f;

    [Header("Feedback (optional)")]
    public GameObject destroyedEffect;
    public AudioClip hitSound;
    public AudioClip destroyedSound;

    public float CurrentHealth { get; private set; }
    public bool IsDestroyed { get; private set; }

    public event Action<float, float> OnHealthChanged;
    public event Action<DoorHealth> OnDestroyed;

    void Start()
    {
        CurrentHealth = maxHealth;
    }

    public void TakeDamage(float amount)
    {
        if (IsDestroyed || amount <= 0f) return;

        CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
        if (hitSound != null) AudioSource.PlayClipAtPoint(hitSound, transform.position);
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);

        if (CurrentHealth <= 0f)
        {
            Die();
        }
    }

    private void Die()
    {
        if (IsDestroyed) return;
        IsDestroyed = true;

        if (destroyedEffect != null) Instantiate(destroyedEffect, transform.position, transform.rotation);
        if (destroyedSound != null) AudioSource.PlayClipAtPoint(destroyedSound, transform.position);
        OnDestroyed?.Invoke(this);

        // Destroying the object removes the NavMeshObstacle, which re-opens the path for enemies.
        Destroy(gameObject);
    }
}
