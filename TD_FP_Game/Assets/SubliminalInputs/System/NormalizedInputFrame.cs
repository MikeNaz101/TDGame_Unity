using System.Runtime.InteropServices;

namespace SubliminalSarcasm.InputSystem
{
    // PRO TIP: StructLayout(LayoutKind.Sequential) ensures the data is packed 
    // exactly how we expect in memory, which is crucial for sending over network streams.
    [System.Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct NormalizedInputFrame
    {
        // 1. TIMING (4 Bytes)
        // The specific simulation frame this input belongs to. 
        // Critical for "Rollback" functionality.
        public uint FrameID;

        // 2. BUTTONS (2 Bytes)
        // A Bitmask. Each bit represents a button (A, B, X, Y, Triggers, etc.).
        // 0000 0000 0000 0001 = Button A is pressed.
        public ushort ButtonMask;

        // 3. AXES (8 Bytes total)
        // We use 'short' (16-bit integer). 
        // Range: -32,768 to +32,767.
        // This gives us roughly 65,000 steps of precision. 
        // This is HIGHER precision than most physical controller sticks can mechanically detect,
        // so we lose nothing, but we gain perfect mathematical consistency.
        public short MoveX;
        public short MoveY;
        public short LookX;
        public short LookY;
    }
}