using UnityEngine;
using System;

// A static event manager to broadcast audio events (like gunshots) to any AI that needs to "hear" them.
public static class EnemyAIAudioEvents
{
    // The event that Roamer enemies will subscribe to.
    public static event Action<Vector3> OnGunshotReported;
    
    //Call this from any script that makes a loud noise (e.g., WeaponController)
    public static void ReportGunshot(Vector3 position)
    {
        OnGunshotReported?.Invoke(position);
    }
}