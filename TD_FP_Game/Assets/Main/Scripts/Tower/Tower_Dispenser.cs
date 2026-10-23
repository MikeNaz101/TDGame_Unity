using UnityEngine;

/// <summary>
/// A utility tower that heals the player and grants scrap
/// when the player is within its range.
/// </summary>
public class Tower_Dispenser : TowerController
{
    [Header("Dispenser Visuals")]
    [Tooltip("Optional part that bobs and spins while the player is being serviced.")]
    [SerializeField] private Transform activeIndicator;
    [SerializeField] private float indicatorSpinSpeed = 120f;
    [SerializeField] private float indicatorBobHeight = 0.1f;

    private float _scrapTimer = 0f;
    private float _outputMult = 1f;
    private Vector3 _indicatorRestPos;
    private float _activeBlend;

    protected override void Awake()
    {
        base.Awake();
        if (activeIndicator != null) _indicatorRestPos = activeIndicator.localPosition;
    }

    protected override void ApplySpecificUpgrade(TowerUpgradeData upgrade)
    {
        if (upgrade.effectType == TowerUpgradeType.Dispenser_Output) _outputMult += upgrade.effectValue;
    }

    protected override void Update()
    {
        if (_playerTarget == null || _playerStats == null || _towerData == null) return;

        float range = CurrentRange;
        bool playerInRange = (_playerTarget.position - transform.position).sqrMagnitude <= range * range;

        if (playerInRange)
        {
            if (_towerData.healthPerSecond > 0)
            {
                _playerStats.Heal(_towerData.healthPerSecond * _outputMult * Time.deltaTime);
            }
            
            if (_towerData.scrapPerSecond > 0)
            {
                _scrapTimer += Time.deltaTime;
                if (_scrapTimer >= 1f)
                {
                    _playerStats.AddScrap(Mathf.RoundToInt(_towerData.scrapPerSecond * _outputMult));
                    _scrapTimer -= 1f;
                }
            }
            
            if (!_audioSource.isPlaying && _towerData.shootSound != null)
            {
                _audioSource.clip = _towerData.shootSound;
                _audioSource.loop = true;
                _audioSource.Play();
            }
        }
        else if (_audioSource.isPlaying)
        {
            _audioSource.Stop();
        }

        AnimateIndicator(playerInRange);
    }

    private void AnimateIndicator(bool active)
    {
        if (activeIndicator == null) return;

        // Blend smoothly in and out of the "working" animation instead of snapping.
        _activeBlend = Mathf.MoveTowards(_activeBlend, active ? 1f : 0f, Time.deltaTime * 3f);
        activeIndicator.Rotate(0f, indicatorSpinSpeed * (0.25f + _activeBlend) * Time.deltaTime, 0f, Space.Self);
        float bob = Mathf.Sin(Time.time * 4f) * indicatorBobHeight * _activeBlend;
        activeIndicator.localPosition = _indicatorRestPos + Vector3.up * bob;
    }

    // --- Override these to do nothing ---
    protected override void TryFire() { }
    protected override void Shoot() { }
}
