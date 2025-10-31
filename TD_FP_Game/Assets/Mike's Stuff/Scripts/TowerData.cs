using UnityEngine;

[CreateAssetMenu(fileName = "NewTowerData", menuName = "Aegis/Tower Data")]
public class TowerData : ScriptableObject
{
    [Header("Tower Identity")]
    public string towerName = "New Tower";
    public string description = "A basic defensive unit.";

    // --- NEW FIELD ADDED ---
    [Header("UI")]
    [Tooltip("The 2D icon to display in the build menu.")]
    public Sprite towerIcon; 
    // --- END NEW FIELD ---
    
    [Header("Building")]
    [Tooltip("The actual GameObject prefab to be instantiated.")]
    public GameObject towerPrefab;
    public int scrapCost = 100;

    [Header("Combat Stats")]
    public float damage = 10f;
    public float fireRate = 0.5f;
    public float range = 15f;
}