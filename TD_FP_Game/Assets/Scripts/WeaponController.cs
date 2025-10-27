using UnityEngine;
using System.Collections;
using StarterAssets; // Required to access the public input booleans

public class WeaponController : MonoBehaviour
{
    // --- SETUP REFERENCES ---
    [Header("Setup")]
    [Tooltip("The script that holds the current state of player inputs.")]
    public StarterAssetsInputs inputScript; // Assigned in Inspector (The Player object)
    [Tooltip("The point where projectiles spawn.")]
    public Transform shootPoint; // Assigned in Inspector (The Muzzle)
    [Tooltip("The player camera's shake script for recoil effects.")]
    public CameraShake cameraShake; // Assigned in Inspector (The Main Camera)
    [Tooltip("The target transform the gun moves to when Aiming Down Sights.")]
    public Transform adsTarget; // Assigned in Inspector

    // --- WEAPON DATA ---
    [Header("Weapon Inventory & Data")]
    public WeaponData[] inventory; // Drag your Scriptable Objects here
    private WeaponData currentWeapon;
    private int currentWeaponIndex = 0;

    // --- AMMO & RELOAD STATE ---
    [Header("Ammo & State")]
    public int currentAmmo;
    public int currentReserveAmmo;
    private bool isReloading = false;
    private float timeUntilNextShot = 0f;
    private bool wasFiringLastFrame = false; // Tracks semi-auto clicks
    private bool adsModeActive = false;
    
    // --- ANIMATION & RECOIL ---
    private Vector3 initialPosition; // For ADS and Recoil
    private Vector3 hipPosition = Vector3.zero; // Local position in hip-fire mode

    // --- LIFE CYCLE METHODS ---

    void Start()
    {
        // 1. Get initial resting position
        initialPosition = transform.localPosition;
        hipPosition = initialPosition;

        // 2. Equip the first weapon on start
        if (inventory.Length > 0)
        {
            EquipWeapon(currentWeaponIndex);
        }
    }

    // This method now handles input polling, ADS, and firing checks every frame
    void Update()
    {
        if (currentWeapon == null) return;

        // 1. Update Cooldown
        if (timeUntilNextShot > 0f)
        {
            timeUntilNextShot -= Time.deltaTime;
        }

        // 2. Handle Aim Down Sights (ADS)
        HandleADS();

        // 3. Handle Firing Input Logic
        // We only allow shooting if not reloading AND the shot is off cooldown.
        bool shotReady = timeUntilNextShot <= 0f;

        if (!isReloading && shotReady)
        {
            if (currentWeapon.isAutomatic)
            {
                // Automatic: Fire continuously while the button is held
                if (inputScript.fire)
                {
                    Shoot();
                }
            }
            else // Semi-Automatic (Current forced behavior)
            {
                // Semi-Auto: Fire only on the frame the button is pressed down (input is TRUE AND it was FALSE last frame)
                if (inputScript.fire && !wasFiringLastFrame)
                {
                    Shoot();
                }
            }
        }
        
        // 4. Handle Manual Reload Input
        if (inputScript.reload && !isReloading && currentAmmo < currentWeapon.maxAmmo)
        {
            StartCoroutine(Reload());
            // Consume the reload input immediately to prevent continuous attempts
            inputScript.reload = false; 
        }

        // 5. Update Input State for next frame
        wasFiringLastFrame = inputScript.fire;
    }

    // --- CORE GAMEPLAY METHODS ---

    void Shoot()
    {
        // 1. Check for Ammunition and Auto-Reload
        if (currentAmmo <= 0)
        {
            Debug.Log("Out of Ammo! Reloading automatically...");
            if (currentReserveAmmo > 0)
            {
                // Only start reload if we are not already reloading and have reserve ammo
                if (!isReloading)
                {
                    StartCoroutine(Reload());
                }
            }
            // Return regardless of whether reload started or not (stops shot attempt)
            return; 
        }

        // 2. Decrement Ammo & Set Cooldown
        currentAmmo--;
        timeUntilNextShot = currentWeapon.fireRate;

        // 3. CORE MECHANIC: Spawn Projectile
        if (currentWeapon.bulletPrefab != null)
        {
            // Instantiating and passing damage/speed data to the Projectile script
            GameObject projectileObject = Instantiate(currentWeapon.bulletPrefab, shootPoint.position, shootPoint.rotation);
            Projectile projectileScript = projectileObject.GetComponent<Projectile>(); 

            if (projectileScript != null)
            {
                projectileScript.damageAmount = currentWeapon.damage;
            }
        }

        // 4. Recoil and Visual Feedback
        if (cameraShake != null)
        {
            cameraShake.Shake(currentWeapon.recoilKickback);
        }

        // Optional: Implement muzzle flash and sound effects here
        
        Debug.Log("Fired! Ammo Remaining: " + currentAmmo);
    }

    IEnumerator Reload()
    {
        Debug.Log("Reloading...");
        isReloading = true;

        // Wait for the specified reload time
        yield return new WaitForSeconds(currentWeapon.reloadTime);

        // Calculate ammo to transfer from reserve to clip
        int neededAmmo = currentWeapon.maxAmmo - currentAmmo;
        int ammoToTransfer = Mathf.Min(neededAmmo, currentReserveAmmo);

        // Update counts
        currentAmmo += ammoToTransfer;
        currentReserveAmmo -= ammoToTransfer;

        isReloading = false;
        Debug.Log("Reload Complete. Ammo: " + currentAmmo + " / Reserve: " + currentReserveAmmo);
    }

    public void EquipWeapon(int index)
    {
        // Bounds check
        if (index < 0 || index >= inventory.Length) return;

        // 1. DEACTIVATE the currently equipped weapon (if there is one)
        if (currentWeapon != null && currentWeapon.weaponPrefab != null)
        {
            currentWeapon.weaponPrefab.SetActive(false);
        }

        // 2. Set the new weapon data
        currentWeaponIndex = index;
        currentWeapon = inventory[index];

        // 3. ACTIVATE the new weapon model
        if (currentWeapon.weaponPrefab != null)
        {
            currentWeapon.weaponPrefab.SetActive(true);
        }
        
        // 4. Initialize ammo
        currentAmmo = currentWeapon.maxAmmo;
        currentReserveAmmo = currentWeapon.reserveAmmo;
        isReloading = false;
        
        Debug.Log("Equipped: " + currentWeapon.weaponName);
    }

    // --- VISUAL & MOVEMENT ---

    void HandleADS()
    {
        Vector3 targetLocalPosition = hipPosition;
        
        // Check if ADS input is active and we are not currently reloading
        if (inputScript.aim && !isReloading)
        {
            // Set the target position to the ADS position from the data asset
            if (adsTarget != null)
            {
                // We use the local position of the adsTarget marker as the target position
                targetLocalPosition = adsTarget.localPosition;
            }
            adsModeActive = true;
        }
        else
        {
            // Otherwise, target the hip position
            adsModeActive = false;
        }

        // Smoothly interpolate between the current position and the target position
        float lerpSpeed = currentWeapon != null ? currentWeapon.adsSpeed : 10f;
        transform.localPosition = Vector3.Lerp(transform.localPosition, targetLocalPosition, Time.deltaTime * lerpSpeed);
    }
}
