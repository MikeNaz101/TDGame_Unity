using UnityEngine;

[CreateAssetMenu(fileName = "NewTowerData", menuName = "Aegis/Tower Data")]
public class TowerData : ScriptableObject
{
    public enum TargetingMode
    {
        Closest,   // Nearest enemy to the tower
        First,     // Enemy that has been alive the longest (closest to leaking, BTD "First")
        Strongest, // Highest current health
        Weakest    // Lowest current health
    }

    [Header("Tower Identity")]
    public string towerName = "New Tower";
    public string description = "A basic defensive unit.";

    [Header("UI")]
    [Tooltip("The 2D icon to display in the build menu.")]
    public Sprite towerIcon;

    [Header("Building")]
    [Tooltip("The actual GameObject prefab to be instantiated.")]
    public GameObject towerPrefab;
    public int scrapCost = 100;
    [Tooltip("Fraction of the cost refunded when the tower is sold.")]
    [Range(0f, 1f)] public float sellRefund = 0.5f;

    [Header("Combat Stats")]
    public float damage = 10f;
    [Tooltip("Seconds between shots.")]
    public float fireRate = 0.5f;
    public float range = 15f;
    public TargetingMode targeting = TargetingMode.Closest;
    [Tooltip("How fast the turret head turns (degrees per second). 0 = snap instantly.")]
    public float turnSpeed = 360f;
    [Tooltip("Only fire when the turret is pointing roughly at the target (degrees). Ignored when turnSpeed is 0.")]
    public float fireAngleTolerance = 10f;
    [Tooltip("Require a clear line of sight to the target before firing.")]
    public bool requireLineOfSight = false;
    [Tooltip("Layers that block line of sight (walls, doors).")]
    public LayerMask lineOfSightBlockers;

    [Header("Effects")]
    public AudioClip shootSound;
    [Tooltip("Seconds the tracer line stays visible per shot.")]
    public float tracerDuration = 0.05f;
    public Color tracerColor = new Color(1f, 0.8f, 0.2f, 1f);
    public float tracerWidth = 0.05f;
    [Tooltip("Optional effect spawned at the hit point.")]
    public GameObject impactEffect;
    [Tooltip("Optional effect spawned at the muzzle.")]
    public GameObject muzzleEffect;
}
