using UnityEngine;
// We'll use the legacy Input for this prototype for simplicity, 
// but this structure works perfectly with the New Input System too.
using SubliminalSarcasm.InputSystem; 

namespace SubliminalSarcasm.Core
{
    public class InputInterceptor : MonoBehaviour
    {
        // A simple circular buffer or list to store history (for future rollback)
        // For now, we just hold the 'current' frame.
        public NormalizedInputFrame CurrentInput;

        private uint _tickCounter = 0;

        // "FixedUpdate" is crucial here. 
        // Networked games usually run on a fixed "Tick Rate" (e.g. 60hz).
        // Variable framerates (Update) cause desync.
        void FixedUpdate()
        {
            CaptureFrame();
            _tickCounter++;
        }

        private void CaptureFrame()
        {
            // 1. Create a new frame
            NormalizedInputFrame frame = new NormalizedInputFrame();
            frame.FrameID = _tickCounter;

            // 2. Handle Buttons (Bitmasking)
            // This is the "Backend Engineering" flex.
            // instead of storing 4 bools (4 bytes), we store bits in an integer.
            ushort buttons = 0;
            
            if (Input.GetKey(KeyCode.Space)) buttons |= (ushort)InputButtons.Jump;
            if (Input.GetKey(KeyCode.LeftShift)) buttons |= (ushort)InputButtons.Dash;
            if (Input.GetMouseButton(0)) buttons |= (ushort)InputButtons.Fire;
            
            frame.ButtonMask = buttons;

            // 3. Handle Axes (Quantization)
            // We take the raw Unity float and turn it into our standard Short.
            float rawMoveX = Input.GetAxisRaw("Horizontal");
            float rawMoveY = Input.GetAxisRaw("Vertical");
            
            // Assume we had mouse look set up
            float rawLookX = Input.GetAxisRaw("Mouse X"); 
            float rawLookY = Input.GetAxisRaw("Mouse Y");

            frame.MoveX = InputQuantizer.Quantize(rawMoveX);
            frame.MoveY = InputQuantizer.Quantize(rawMoveY);
            frame.LookX = InputQuantizer.Quantize(rawLookX);
            frame.LookY = InputQuantizer.Quantize(rawLookY);

            // 4. Publish
            CurrentInput = frame;
            
            // Debug log to prove it's working (Optional)
            // Debug.Log($"Tick: {frame.FrameID} | Packed X: {frame.MoveX}");
        }
    }

    // Helper to keep our bits organized
    // Powers of 2: 1, 2, 4, 8, 16, 32...
    public enum InputButtons : ushort
    {
        None = 0,
        Jump = 1,      // Binary 0001
        Dash = 2,      // Binary 0010
        Fire = 4,      // Binary 0100
        Reload = 8     // Binary 1000
    }
}