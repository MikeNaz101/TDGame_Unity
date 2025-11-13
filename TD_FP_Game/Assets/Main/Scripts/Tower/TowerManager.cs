using UnityEngine;
using System.Collections.Generic;

public class TowerManager : MonoBehaviour
{
    [Header("Tower Catalog")]
    [Tooltip("List of all available TowerData Scriptable Objects.")]
    public TowerData[] availableTowers;
    
    // Returns the scrap cost for a tower based on its index in the availableTowers list.
    public int GetTowerCost(int index)
    {
        if (availableTowers == null || index < 0 || index >= availableTowers.Length)
        {
            Debug.LogError($"Invalid tower index: {index}. Catalog size: {availableTowers.Length}");
            return int.MaxValue; // Return maximum value to prevent accidental build
        }
        
        return availableTowers[index].scrapCost;
    }

    // Instantiates the selected tower prefab at the specified world position.
    public void BuildTower(int index, Vector3 position)
    {
        if (availableTowers == null || index < 0 || index >= availableTowers.Length)
        {
            Debug.LogError($"Invalid tower index: {index}. Instantiation failed.");
            return;
        }

        TowerData towerData = availableTowers[index];

        GameObject newTower = Instantiate(
            towerData.towerPrefab,
            position,
            Quaternion.identity
        );

        // --- THIS IS THE NEW, CRITICAL LINE ---
        // Get the controller and give it its stats
        newTower.GetComponent<TowerController>()?.Initialize(towerData);
        // ----------------------------------------

        Debug.Log($"Successfully instantiated {towerData.towerName} at {position}");
    }
}