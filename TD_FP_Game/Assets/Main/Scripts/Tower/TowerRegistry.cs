using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// A global, static registry that holds a list of all active towers in the scene.
/// </summary>
public class TowerRegistry : MonoBehaviour
{
    // The master list of all towers
    public static List<TowerController> ActiveTowers = new List<TowerController>();

    // A counter to give each tower a unique ID
    private static int _nextTowerID = 1;

    /// <summary>
    /// Called by a TowerController when it's created to get a unique ID.
    /// </summary>
    public static int RegisterTower(TowerController tower)
    {
        if (tower != null && !ActiveTowers.Contains(tower))
        {
            ActiveTowers.Add(tower);
        }
        return _nextTowerID++; // Give the tower its ID and increment for the next one
    }

    /// <summary>
    /// Called by a TowerController when it's destroyed.
    /// </summary>
    public static void UnregisterTower(TowerController tower)
    {
        if (tower != null && ActiveTowers.Contains(tower))
        {
            ActiveTowers.Remove(tower);
        }
    }
}