public static class InputQuantizer
{
    private const float MAX_SHORT = 32767f;

    // Convert Unity Float -> Subliminal Short
    public static short Quantize(float value)
    {
        // Clamp creates a hard boundary so a broken controller sending 1.5f 
        // doesn't crash the logic.
        float clamped = UnityEngine.Mathf.Clamp(value, -1f, 1f);
        return (short)(clamped * MAX_SHORT);
    }

    // Convert Subliminal Short -> Unity Float (for gameplay)
    public static float Dequantize(short value)
    {
        return value / MAX_SHORT;
    }
}