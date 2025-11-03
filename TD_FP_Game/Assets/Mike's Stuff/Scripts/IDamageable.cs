// Interface for any object that can be damaged by an enemy (Player, Core, Door, etc.).
public interface IDamageable
{
    UnityEngine.Transform transform { get; }
    void TakeDamage(float amount);
}