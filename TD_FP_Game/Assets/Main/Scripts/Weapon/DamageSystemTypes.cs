using UnityEngine;

public enum DamageType
{
    Physical,   // Standard bullets (Pistol, SMG, Standard Tower)
    Explosive,  // Rockets, Grenades
    Energy,     // Railgun, Laser
    Fire,       // Flamethrower
    Ice,        // Ice Tower (Slows)
    Electric    // Lightning Gun
}

public enum ArmorType
{
    Unarmored,  // Basic Grunts, Fast Enemies
    Light,      // Hunters
    Heavy,      // Tanks, Bosses
    Shielded    // Special shielded enemies
}

public static class DamageMultiplier
{
    public static float GetMultiplier(DamageType damage, ArmorType armor)
    {
        switch (armor)
        {
            case ArmorType.Unarmored:
                if (damage == DamageType.Fire) return 1.5f;      // Fire burns flesh
                if (damage == DamageType.Explosive) return 1.2f; // Explosions hurt
                return 1.0f;

            case ArmorType.Light:
                if (damage == DamageType.Physical) return 0.8f;  // Bullets slightly resisted
                if (damage == DamageType.Fire) return 0.5f;      // Armor resists fire
                if (damage == DamageType.Energy) return 1.2f;    // Energy cuts through light armor
                return 1.0f;

            case ArmorType.Heavy:
                if (damage == DamageType.Physical) return 0.25f; // Bullets barely scratch
                if (damage == DamageType.Fire) return 0.1f;      // Fire does nothing
                if (damage == DamageType.Explosive) return 1.5f; // Explosions break armor
                if (damage == DamageType.Energy) return 1.0f;    // Energy is neutral
                return 0.5f;                                     // Everything else resisted

            case ArmorType.Shielded:
                if (damage == DamageType.Energy) return 2.0f;    // Energy overloads shields
                if (damage == DamageType.Physical) return 0.0f;  // Shields block bullets completely
                if (damage == DamageType.Electric) return 1.5f;  // Electric shorts shields
                return 0.5f;                                     // Resistant to others

            default:
                return 1.0f;
        }
    }
}