using UnityEngine;

public class TowerPreview : MonoBehaviour
{

    public bool IsPlacable = true;
    private int overlapCount = 0;

    public float placementRadius = 2f;
    public LayerMask blockingMask;

    /*
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Tower") || other.CompareTag("NoBuildZone"))
        {
            overlapCount++;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Tower") || other.CompareTag("NoBuildZone"))
        {
            overlapCount--;
        }
    }
    */

    private void Update()
    {
        Collider[] nearbyObjects = Physics.OverlapSphere(transform.position, placementRadius, blockingMask);

        IsPlacable = nearbyObjects.Length <= 0;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, placementRadius);
    }

}
