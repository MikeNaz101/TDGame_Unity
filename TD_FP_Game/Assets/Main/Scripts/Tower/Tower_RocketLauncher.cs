using UnityEngine;
using System.Collections;

public class Tower_RocketLauncher : TowerController
{
    [Header("Rocket Settings")]
    [Tooltip("How long the rocket sits visible on the launcher before firing.")]
    public float prepareTime = 1.0f;

    private bool _isReloading = false;

    protected override void TryFire()
    {
        // If we are already in the middle of the firing sequence or cooldown, do nothing
        if (_isReloading || _fireCooldown > 0 || _currentTarget == null) return;

        // Start the unique firing sequence
        StartCoroutine(FireSequence());
    }

    private IEnumerator FireSequence()
    {
        _isReloading = true;

        // 1. Instantiate the rocket VISUALLY at the shoot point
        // We parent it to the shootPoint so it rotates with the turret while aiming
        if (_towerData.projectilePrefab != null)
        {
            GameObject rocketObj = Instantiate(_towerData.projectilePrefab, _shootPoint.position, _shootPoint.rotation, _shootPoint);
            SlowHomingRocket rocketScript = rocketObj.GetComponent<SlowHomingRocket>();

            if (rocketScript != null)
            {
                // Initialize data but DO NOT launch yet
                rocketScript.Initialize(_towerData, _currentTarget, transform); // transform is attacker
                
                // 2. Wait for the "Prepare" time (1 second)
                yield return new WaitForSeconds(prepareTime);

                // 3. Check if rocket still exists (wasn't destroyed by something else)
                if (rocketObj != null)
                {
                    // Unparent so it flies freely
                    rocketObj.transform.SetParent(null);
                    
                    // Launch!
                    rocketScript.Launch();
                    
                    // Play Sound
                    if (_towerData.shootSound != null)
                    {
                        _audioSource.PlayOneShot(_towerData.shootSound);
                    }
                }
            }
        }
        
        // 4. Handle Cooldown
        // We subtract the prepare time so the FireRate in TowerData represents the total cycle time
        // e.g., if FireRate is 3s and Prepare is 1s, we wait 2 more seconds.
        float remainingCooldown = Mathf.Max(0f, _towerData.fireRate - prepareTime);
        _fireCooldown = remainingCooldown;
        
        // Wait for the cooldown to finish before allowing another shot
        yield return new WaitForSeconds(remainingCooldown);

        _isReloading = false;
    }

    // Override Shoot to do nothing, as we handle it in the Coroutine
    protected override void Shoot() { }
}