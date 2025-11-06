using System;
using System.Collections;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor; // Keep this for the gizmo
#endif
using UnityEngine;

public class FieldOfView : MonoBehaviour
{
    // --- Serialized Fields (Set in Inspector) ---
    [Header("View Settings")]
    [SerializeField] private Color _gizmoColor = Color.red;
    [SerializeField] private float _viewRadius = 15f;
    [SerializeField] private float _viewAngle = 45f; // This is half of the total angle (e.g., 45 = 90-degree cone)

    [Header("Detection")]
    [Tooltip("The 'head' or 'eye' position to raycast from.")]
    [SerializeField] private Transform _headTransform;
    [Tooltip("Layers that will block the AI's line of sight (e.g., 'Default', 'Environment').")]
    [SerializeField] private LayerMask _blockingLayers;
    
    // --- Private References ---
    private EnemyController _controller; // Reference to its own controller

    void Start()
    {
        _controller = GetComponentInParent<EnemyController>();
        if (_headTransform == null)
        {
            // Fallback: If no head is assigned, just use this object's transform
            _headTransform = transform;
        }
    }
    
    /// <summary>
    /// This is the main function your EnemyController will call.
    /// It checks if a specific target is within the FOV and has a clear line of sight.
    /// </summary>
    /// <param name="target">The target to check (e.g., the player).</param>
    /// <returns>True if the target is visible, false otherwise.</returns>
    public bool IsTargetVisible(Transform target)
    {
        if (target == null)
        {
            return false;
        }

        // --- Check 1: Is target within the view radius? ---
        Vector3 directionToTarget = (target.position - _headTransform.position);
        float distanceToTarget = directionToTarget.magnitude;

        if (distanceToTarget > _viewRadius)
        {
            // Target is too far away.
            return false;
        }

        // --- Check 2: Is target within the view angle? ---
        if (Vector3.Angle(transform.forward, directionToTarget.normalized) > _viewAngle)
        {
            // Target is outside the view cone.
            return false;
        }

        // --- Check 3: Is there a clear line of sight? ---
        // We do a raycast from our head to the target's position.
        // If it hits anything on the _blockingLayers, the target is obstructed.
        
        // Try to get the target's "head" (like a PlayerStats component) for a better check
        Transform targetHead = target; // Default to the target's root
        PlayerStats player = target.GetComponent<PlayerStats>();
        if (player != null)
        {
            // Aim for the player's camera position (a good approximation for their head)
            // This assumes the PlayerStats is on the root and the camera is a child.
            // A simpler way is to just aim for their root + an offset.
            targetHead = player.transform; // Or a specific 'head' transform on the player
        }

        Vector3 headPos = _headTransform.position;
        Vector3 targetPos = targetHead.position; // Use the more accurate position if found
        Vector3 dirToTargetHead = (targetPos - headPos).normalized;
        
        if (Physics.Raycast(headPos, dirToTargetHead, distanceToTarget, _blockingLayers))
        {
            // Raycast hit a wall or obstacle.
            return false;
        }

        // If all checks pass, the target is visible.
        #if UNITY_EDITOR
        // Draw a green line in the Scene view to show successful detection
        Debug.DrawLine(headPos, targetPos, Color.green);
        #endif
        return true;
    }

    // --- Gizmo Drawing (No changes needed, works with new fields) ---
    #if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (_headTransform == null)
        {
            _headTransform = transform;
        }
        
        Gizmos.color = _gizmoColor;
        Handles.color = _gizmoColor;
        
        Handles.DrawWireArc(_headTransform.position, _headTransform.up, _headTransform.forward, _viewAngle, _viewRadius);
        Handles.DrawWireArc(_headTransform.position, _headTransform.up, _headTransform.forward, -_viewAngle, _viewRadius);

        Vector3 lineA = Quaternion.AngleAxis(_viewAngle, _headTransform.up) * _headTransform.forward;
        Vector3 lineB = Quaternion.AngleAxis(-_viewAngle, _headTransform.up) * _headTransform.forward;
        Handles.DrawLine(_headTransform.position, _headTransform.position + (lineA * _viewRadius));
        Handles.DrawLine(_headTransform.position, _headTransform.position + (lineB * _viewRadius));
    }
    #endif
}
