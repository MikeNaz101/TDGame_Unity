using UnityEngine;

// Make sure the object is tagged "Door" and has a NavMeshObstacle component.
[RequireComponent(typeof(UnityEngine.AI.NavMeshObstacle))]
public class DoorHealth : MonoBehaviour, IDamageable
{
    [Header("Door Stats")]
    [Tooltip("The total health of the door.")]
    public float maxHealth = 200f;

    private float currentHealth;
    public Transform transform => base.transform;

    void Start()
    {
        currentHealth = maxHealth;
    }

    public void TakeDamage(float amount)
    {
        currentHealth -= amount;
        Debug.Log($"Door at {transform.position} took {amount} damage, {currentHealth} health remaining.");

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        // When the door is destroyed, it unblocks the NavMesh path.
        Debug.Log("Door destroyed!");
        Destroy(gameObject);
    }
}