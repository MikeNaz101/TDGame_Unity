using UnityEngine;

public enum TowerUpgradeType
{
    // General Stats
    Damage,
    Range,
    FireRate,
    
    // Specific Mechanics
    Sniper_DoubleShot,
    Flame_Width,
    Flame_BurnDot,
    Rocket_Speed,
    Rocket_BlastRadius,

    // Added later: keep new entries at the END so existing upgrade assets keep their values.
    Ice_SlowStrength,          // effectValue = extra slow, e.g. 0.1 = enemies move 10% slower
    Lightning_ParalyzeChance,  // effectValue = added chance, e.g. 0.15 = +15%
    Radar_BuffStrength,        // effectValue = added buff, e.g. 0.1 = +10% fire rate and damage
    Dispenser_Output,          // effectValue = percent more healing and scrap, e.g. 0.5 = +50%
}
