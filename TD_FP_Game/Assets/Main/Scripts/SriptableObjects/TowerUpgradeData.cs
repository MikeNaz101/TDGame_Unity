using UnityEngine;


[CreateAssetMenu(fileName = "NewTowerUpgrade", menuName = "Aegis/Tower Upgrade Node")]
public class TowerUpgradeData : ScriptableObject
{
    [Header("UI Info")]
    public string upgradeName;
    [TextArea] public string description;
    public Sprite icon;
    public int cost;

    [Header("Mechanics")]
    public TowerUpgradeType effectType;
    [Tooltip("For percentages, use 0.25 for 25%. For booleans, use 1.")]
    public float effectValue;
}