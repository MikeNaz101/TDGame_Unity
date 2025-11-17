using UnityEngine;

/// <summary>
/// A utility tower that heals the player and grants scrap
/// when the player is within its range.
/// </summary>
public class Tower_Dispenser : TowerController
{
    private float _scrapTimer = 0f;

    protected override void Update()
    {
        // This tower does not aim or attack.
        // It only checks for the player.
        
        if (_playerTarget == null || _playerStats == null || _towerData == null)
        {
            // Failsafe in case player isn't found
            return;
        }
        
        // 1. Check if player is in range
        float distanceToPlayer = Vector3.Distance(transform.position, _playerTarget.position);
        
        if (distanceToPlayer <= _towerData.range)
        {
            // 2. Player is in range. Apply healing.
            if (_towerData.healthPerSecond > 0)
            {
                _playerStats.Heal(_towerData.healthPerSecond * Time.deltaTime);
            }
            
            // 3. Apply scrap generation (1 per second)
            if (_towerData.scrapPerSecond > 0)
            {
                _scrapTimer += Time.deltaTime;
                if (_scrapTimer >= 1f)
                {
                    _playerStats.AddScrap(_towerData.scrapPerSecond);
                    _scrapTimer = 0f; // Reset timer
                }
            }
            
            // Optional: Play a looping "healing" sound
            if (!_audioSource.isPlaying && _towerData.shootSound != null)
            {
                _audioSource.clip = _towerData.shootSound;
                _audioSource.loop = true;
                _audioSource.Play();
            }
        }
        else
        {
            // Player is not in range, stop sound
            if (_audioSource.isPlaying)
            {
                _audioSource.Stop();
            }
        }
    }

    // --- Override these to do nothing ---
    protected override void TryFire() { }
    protected override void Shoot() { }
}