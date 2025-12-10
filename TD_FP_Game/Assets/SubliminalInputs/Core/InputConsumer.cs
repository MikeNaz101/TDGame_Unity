using UnityEngine;
using System;
using SubliminalSarcasm.InputSystem; 

namespace SubliminalSarcasm.Core
{
    [RequireComponent(typeof(CharacterController))]
    public class InputConsumer : MonoBehaviour
    {
        private CharacterController _controller;
        public float Speed = 5.0f;

        // NEW: This holds the packet sent by the Network Emulator
        private NormalizedInputFrame _frameToProcess;
        private bool _hasNewFrame = false;
        
        // Sends: (Frame ID, The Position Result)
        public event Action<uint, Vector3> OnServerFrameProcessed;

        void Start()
        {
            _controller = GetComponent<CharacterController>();
        }

        // --- THE MISSING METHOD ---
        // This is the "mailbox" that allows the NetworkEmulator 
        // to push a delayed frame into this script.
        public void InjectRemoteFrame(NormalizedInputFrame frame)
        {
            _frameToProcess = frame;
            _hasNewFrame = true;
        }

        void FixedUpdate()
        {
            // If we haven't received a frame this tick (due to lag/packet loss), do nothing.
            if (!_hasNewFrame) return;

            ProcessMovement(_frameToProcess);
            
            // Mark the frame as "consumed" so we don't process it twice
            _hasNewFrame = false; 
        }

        private void ProcessMovement(NormalizedInputFrame frame)
        {
            // 1. DEQUANTIZE (Convert Short -> Float)
            float moveX = InputQuantizer.Dequantize(frame.MoveX);
            float moveZ = InputQuantizer.Dequantize(frame.MoveY);

            // 2. MOVE
            Vector3 move = transform.right * moveX + transform.forward * moveZ;
            _controller.Move(move * Speed * Time.fixedDeltaTime);

            // 3. DEBUG JUMP
            if ((frame.ButtonMask & (ushort)InputButtons.Jump) != 0)
            {
                Debug.DrawRay(transform.position, Vector3.up * 2, Color.green, 0.1f);
            }
            
            OnServerFrameProcessed?.Invoke(frame.FrameID, transform.position);
        }
    }
}