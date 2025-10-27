using UnityEngine;

// This allows us to create new Weapon Data assets inside the Unity Editor
[CreateAssetMenu(fileName = "New Weapon", menuName = "Aegis/Weapon Data")]
public class WeaponData : ScriptableObject
{
    [Header("Weapon Identity")]
    public string weaponName = "Assault Rifle";
    public GameObject weaponPrefab; // The 3D model shown in the player's hands
    
    [Header("Projectile Type")]
    // This variable allows the WeaponController to spawn the correct projectile
    public GameObject bulletPrefab; 

    [Header("Core Stats")]
    public float damage = 10f;
    public float fireRate = 0.1f; // Time between shots
    public bool isAutomatic = true;

    [Header("Ammo & Reload")]
    public int maxAmmo = 30;
    public float reloadTime = 2.0f;
    public int reserveAmmo = 90; // Total ammo carried

    [Header("Recoil & Responsiveness")]
    // Camera shake strength (Higher for bigger guns like shotguns)
    public float recoilKickback = 0.5f; 
    // How far the sights move when aiming (for ADS)
    public Vector3 aimDownSightsPosition; 
    public float adsSpeed = 8f; // Speed of ADS transition
}