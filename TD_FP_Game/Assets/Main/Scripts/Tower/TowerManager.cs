using System;
using UnityEngine;

public class TowerManager : MonoBehaviour
{
    [Header("Tower Catalog")]
    [Tooltip("List of all available TowerData Scriptable Objects.")]
    public TowerData[] availableTowers;

    [Header("Placement")]
    [Tooltip("Layer newly built towers are placed on (used by the BuildManager proximity check). Leave empty to keep the prefab's layer.")]
    public string towerLayerName = "Tower";
    [Tooltip("Optional effect spawned where a tower is built.")]
    public GameObject buildEffect;
    public AudioClip buildSound;

    /// <summary>Fired after a tower is placed (data, instance).</summary>
    public event Action<TowerData, GameObject> OnTowerBuilt;

    public int TowerCount => TowerController.ActiveTowers.Count;

    private int _towerLayer = -1;

    void Awake()
    {
        if (!string.IsNullOrEmpty(towerLayerName))
        {
            _towerLayer = LayerMask.NameToLayer(towerLayerName);
        }
    }

    public bool IsValidIndex(int index)
    {
        return availableTowers != null && index >= 0 && index < availableTowers.Length && availableTowers[index] != null;
    }

    public TowerData GetTowerData(int index)
    {
        return IsValidIndex(index) ? availableTowers[index] : null;
    }

    // Returns the scrap cost for a tower based on its index in the availableTowers list.
    public int GetTowerCost(int index)
    {
        if (!IsValidIndex(index))
        {
            Debug.LogError($"Invalid tower index: {index}. Catalog size: {(availableTowers != null ? availableTowers.Length : 0)}");
            return int.MaxValue; // Prevent accidental build
        }
        return availableTowers[index].scrapCost;
    }

    /// <summary>Instantiates the selected tower at the world position and wires up its controller. Returns null on failure.</summary>
    public GameObject BuildTower(int index, Vector3 position)
    {
        if (!IsValidIndex(index))
        {
            Debug.LogError($"Invalid tower index: {index}. Instantiation failed.");
            return null;
        }

        TowerData towerData = availableTowers[index];
        if (towerData.towerPrefab == null)
        {
            Debug.LogError($"TowerData '{towerData.towerName}' has no prefab assigned.", towerData);
            return null;
        }

        GameObject newTower = Instantiate(towerData.towerPrefab, position, Quaternion.identity);
        newTower.name = towerData.towerName;

        if (_towerLayer >= 0 && newTower.layer != _towerLayer)
        {
            SetLayerRecursively(newTower.transform, _towerLayer);
        }

        // Existing prefabs have no scripts; add the controller so the tower actually shoots.
        TowerController controller = newTower.GetComponent<TowerController>();
        if (controller == null) controller = newTower.AddComponent<TowerController>();
        controller.Initialize(towerData);

        if (buildEffect != null) Destroy(Instantiate(buildEffect, position, Quaternion.identity), 3f);
        if (buildSound != null) AudioSource.PlayClipAtPoint(buildSound, position);

        OnTowerBuilt?.Invoke(towerData, newTower);
        return newTower;
    }

    private static void SetLayerRecursively(Transform t, int layer)
    {
        t.gameObject.layer = layer;
        for (int i = 0; i < t.childCount; i++)
        {
            SetLayerRecursively(t.GetChild(i), layer);
        }
    }
}
