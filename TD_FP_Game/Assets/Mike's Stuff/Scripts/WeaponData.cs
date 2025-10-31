// WeaponData.cs
using UnityEngine;

[CreateAssetMenu(fileName = "NewWeaponData", menuName = "Aegis/Weapon Data")]
public class WeaponData : ScriptableObject
{
    [Header("Weapon Identity")]
    public string weaponName = "Default Rifle";
    [Tooltip("The 3D model prefab that the player holds.")]
    public GameObject weaponPrefab;

    // --- ADD THIS NEW FIELD ---
    [Header("UI")]
    [Tooltip("The UI prefab for this weapon's specific ADS reticle.")]
    public GameObject adsReticlePrefab;
    // --- END NEW FIELD ---

    [Header("Hitscan & Damage")]
    [Tooltip("Damage dealt per shot.")]
    public float damage = 10f;
    [Tooltip("Max range of the hitscan raycast.")]
    public float range = 100f;
    
    // ... (rest of your script is the same)
    [Header("Fire Rate & Type")]
    public float fireRate = 0.1f;
    public bool isAutomatic = true;

    [Header("Ammo & Reload")]
    public int maxAmmo = 30;
    public int reserveAmmo = 90;
    public float reloadTime = 2.0f;

    [Header("Recoil & ADS")]
    [Range(0.1f, 2.0f)] public float recoilKickback = 0.5f;
    public float adsSpeed = 12f;
    public Vector3 aimDownSightsPosition = new Vector3(0, -0.1f, 0.1f);
}