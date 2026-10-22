using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class FieldOfView : MonoBehaviour
{
    [Header("View Settings")]
    [SerializeField] private Color _gizmoColor = Color.red;
    [SerializeField] private float _viewRadius = 15f;
    [SerializeField] private float _viewAngle = 45f; // Half of the total cone angle (45 = 90-degree cone)

    [Header("Detection")]
    [Tooltip("The 'head' or 'eye' position to raycast from.")]
    [SerializeField] private Transform _headTransform;
    [Tooltip("Layers that will block the AI's line of sight (e.g., 'Default', 'Environment').")]
    [SerializeField] private LayerMask _blockingLayers;
    [Tooltip("Vertical offset added to the target's root position when checking line of sight (roughly chest height).")]
    [SerializeField] private float _targetHeightOffset = 1.2f;

    public float ViewRadius => _viewRadius;

    void Awake()
    {
        if (_headTransform == null) _headTransform = transform;
    }

    /// <summary>
    /// Checks if a target is within the FOV cone and has a clear line of sight. Allocation free.
    /// </summary>
    public bool IsTargetVisible(Transform target)
    {
        if (target == null) return false;

        Vector3 headPos = _headTransform.position;
        Vector3 targetPos = target.position + Vector3.up * _targetHeightOffset;
        Vector3 toTarget = targetPos - headPos;
        float sqrDist = toTarget.sqrMagnitude;

        // 1. Radius
        if (sqrDist > _viewRadius * _viewRadius) return false;

        // 2. Angle
        if (Vector3.Angle(transform.forward, toTarget) > _viewAngle) return false;

        // 3. Line of sight
        float dist = Mathf.Sqrt(sqrDist);
        if (dist > 0.001f && Physics.Raycast(headPos, toTarget / dist, dist, _blockingLayers, QueryTriggerInteraction.Ignore))
        {
            return false;
        }

#if UNITY_EDITOR
        Debug.DrawLine(headPos, targetPos, Color.green);
#endif
        return true;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Transform head = _headTransform != null ? _headTransform : transform;

        Gizmos.color = _gizmoColor;
        Handles.color = _gizmoColor;

        Handles.DrawWireArc(head.position, head.up, head.forward, _viewAngle, _viewRadius);
        Handles.DrawWireArc(head.position, head.up, head.forward, -_viewAngle, _viewRadius);

        Vector3 lineA = Quaternion.AngleAxis(_viewAngle, head.up) * head.forward;
        Vector3 lineB = Quaternion.AngleAxis(-_viewAngle, head.up) * head.forward;
        Handles.DrawLine(head.position, head.position + (lineA * _viewRadius));
        Handles.DrawLine(head.position, head.position + (lineB * _viewRadius));
    }
#endif
}
