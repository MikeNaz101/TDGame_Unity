using UnityEngine;
using System.Collections;

public class Tower_Sniper : TowerController
{
    [Header("Sniper Stats")]
    [Tooltip("If true, fires a second shot right after the first.")]
    public bool doubleShot = false;
    [Tooltip("Delay between the two Double Shot rounds, so both tracers and sounds read clearly.")]
    [SerializeField] private float doubleShotDelay = 0.12f;

    private WaitForSeconds _doubleShotWait;

    protected override void Awake()
    {
        base.Awake();
        _doubleShotWait = new WaitForSeconds(doubleShotDelay);
    }

    protected override void ApplySpecificUpgrade(TowerUpgradeData upgrade)
    {
        switch (upgrade.effectType)
        {
            case TowerUpgradeType.Sniper_DoubleShot:
                doubleShot = true;
                break;
            // Damage/Speed handled by base class (TowerUpgradeType.Damage, TowerUpgradeType.FireRate)
        }
    }

    protected override void Shoot()
    {
        FireSingleShot(_currentTarget);
        if (doubleShot) StartCoroutine(SecondShot(_currentTarget));
    }

    private IEnumerator SecondShot(EnemyController firstTarget)
    {
        yield return _doubleShotWait;

        // The second round goes to the original target if it survived,
        // otherwise to whoever the tower would pick next. No wasted shots on corpses.
        EnemyController target = IsValidTarget(firstTarget) ? firstTarget : null;
        if (target == null)
        {
            UpdateTarget();
            target = _currentTarget;
        }
        FireSingleShot(target);
    }

    private void FireSingleShot(EnemyController target)
    {
        if (!IsValidTarget(target)) return;

        PlayFireFeedback();
        if (_towerData.shootSound != null) _audioSource.PlayOneShot(_towerData.shootSound);

        target.TakeDamage(CurrentDamage, transform, _towerData.damageType);

        if (_hitscanTracer != null) StartCoroutine(ShowHitscanTrace());
    }
}
