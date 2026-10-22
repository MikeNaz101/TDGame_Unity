using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class PatrolRoute : MonoBehaviour
{
    public enum PatrolType
    {
        Loop = 0,
        PingPong = 1
    }

    [SerializeField] private Color _patrolRouteColor = Color.green;
    public PatrolType patrolType;
    public List<Transform> route = new List<Transform>();

    [ContextMenu("Add Patrol Point")]
    private void AddPatrolPoint()
    {
        GameObject thisPoint = new GameObject("Point" + (route.Count + 1));
        thisPoint.transform.position = transform.position;
        thisPoint.transform.parent = transform;

#if UNITY_EDITOR
        Undo.RegisterCreatedObjectUndo(thisPoint, "Add Patrol Point");
#endif
        route.Add(thisPoint.transform);
    }

    [ContextMenu("Reverse Patrol Route")]
    private void ReversePatrolRoute()
    {
        route.Reverse();
    }

    private void OnDrawGizmos()
    {
#if UNITY_EDITOR
        Handles.Label(transform.position, gameObject.name);
#endif
        if (route == null || route.Count == 0) return;

        Gizmos.color = _patrolRouteColor;
        for (int i = 0; i < route.Count - 1; i++)
        {
            if (route[i] == null || route[i + 1] == null) continue;
            Gizmos.DrawLine(route[i].position, route[i + 1].position);
        }

        if (patrolType == PatrolType.Loop && route.Count > 1 && route[0] != null && route[route.Count - 1] != null)
        {
            Gizmos.DrawLine(route[route.Count - 1].position, route[0].position);
        }
    }
}
