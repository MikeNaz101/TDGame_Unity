using UnityEngine;

[CreateAssetMenu(fileName = "NewWeaponData", menuName = "Aegis/Weapon Data")]
public class WeaponData : ScriptableObject
{
    [Header("Weapon Identity")]
    public string weaponName = "Default Rifle";
    [Tooltip("The 3D model prefab that the player holds.")]
    public GameObject weaponPrefab;

    [Header("UI")]
    [Tooltip("The UI prefab for this weapon's specific ADS reticle.")]
    public GameObject adsReticlePrefab;

    [Header("Hitscan & Damage")]
    public float damage = 10f;
    public float range = 100f;
    public DamageType damageType = DamageType.Physical;
    
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

    // --- NEW AUDIO FIELDS ---
    [Header("Audio")]
    [Tooltip("The sound played when the weapon is fired.")]
    public AudioClip shootSound;
    [Tooltip("The sound played when the weapon is reloaded.")]
    public AudioClip reloadSound;
    [Tooltip("The sound played when the weapon is equipped.")]
    public AudioClip equipSound;
    
    [Header("Shotgun Settings")]
    [Tooltip("Number of rays per shot. Set to 1 for rifles/pistols.")]
    public int pellets = 1; 
    [Tooltip("Spread angle in degrees.")]
    public float spreadAngle = 0f;
    [Tooltip("Multiplier applied if ALL pellets hit the same target.")]
    public float fullHitBonus = 1.5f;

    [Header("Projectile Settings")]
    [Tooltip("The projectile to spawn (e.g., Rocket). Leave null for hitscan.")]
    public GameObject projectilePrefab; 
    [Tooltip("Force applied to the projectile on launch.")]
    public float launchForce = 20f;
}