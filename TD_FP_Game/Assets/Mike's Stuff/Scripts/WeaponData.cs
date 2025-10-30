using UnityEngine;

// This attribute allows you to create new instances of this data file 
// directly in the Unity Editor via the Assets/Create menu.
[CreateAssetMenu(fileName = "NewWeaponData", menuName = "Aegis/Weapon Data")]
public class WeaponData : ScriptableObject
{
    [Header("Weapon Identity")]
    public string weaponName = "Default Rifle";
    [Tooltip("The 3D model prefab that the player holds.")]
    public GameObject weaponPrefab;

    [Header("Hitscan & Damage")]
    [Tooltip("Damage dealt per shot.")]
    public float damage = 10f;
    [Tooltip("Max range of the hitscan raycast.")]
    public float range = 100f;
    
    [Header("Fire Rate & Type")]
    [Tooltip("Time between shots (lower is faster).")]
    public float fireRate = 0.1f;
    [Tooltip("If true, holding the fire button fires continuously.")]
    public bool isAutomatic = true;

    [Header("Ammo & Reload")]
    [Tooltip("Maximum bullets in the clip.")]
    public int maxAmmo = 30;
    [Tooltip("Total reserve ammo carried.")]
    public int reserveAmmo = 90;
    [Tooltip("Time it takes to complete a reload.")]
    public float reloadTime = 2.0f;

    [Header("Recoil & ADS")]
    [Tooltip("Intensity of the camera shake upon firing (Responsiveness).")]
    [Range(0.1f, 2.0f)] public float recoilKickback = 0.5f;
    [Tooltip("Speed at which the gun moves during ADS (Lerp speed).")]
    public float adsSpeed = 12f;
    [Tooltip("Local position the gun moves to when aiming down sights.")]
    public Vector3 aimDownSightsPosition = new Vector3(0, -0.1f, 0.1f);
}