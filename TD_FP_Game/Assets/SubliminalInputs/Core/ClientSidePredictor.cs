using UnityEngine;
using System.Collections.Generic;
using SubliminalSarcasm.InputSystem; 

namespace SubliminalSarcasm.Core
{
    [RequireComponent(typeof(CharacterController))]
    public class ClientSidePredictor : MonoBehaviour
    {
        [Header("Dependencies")]
        public InputInterceptor LocalInputSource; 
        public InputConsumer ServerStateSource; // Reference to the Red Capsule script

        [Header("Settings")]
        public float Speed = 5.0f;
        public float SnapThreshold = 0.05f; // How much drift is allowed?

        private CharacterController _controller;
        
        // NEW: We need to store more than just inputs. We need the STATE.
        private struct PredictionState
        {
            public NormalizedInputFrame Inputs;
            public Vector3 PredictedPosition;
        }
        
        // Using a List is easier than a Queue for Replay loops
        private List<PredictionState> _history = new List<PredictionState>();

        void Start()
        {
            _controller = GetComponent<CharacterController>();
            
            // Listen to the Red Capsule directly
            if(ServerStateSource != null)
            {
                ServerStateSource.OnServerFrameProcessed += HandleServerUpdate;
            }
        }

        // 1. THE PREDICTION LOOP (Runs every frame)
        void FixedUpdate()
        {
            NormalizedInputFrame currentFrame = LocalInputSource.CurrentInput;
            
            // Move
            ProcessMovement(currentFrame);

            // Record History
            _history.Add(new PredictionState 
            { 
                Inputs = currentFrame, 
                PredictedPosition = transform.position 
            });
        }

        // 2. THE RECONCILIATION LOOP (Runs when Server reports back)
        private void HandleServerUpdate(uint serverFrameID, Vector3 serverPos)
        {
            // Find the history entry that matches this server frame
            int historyIndex = _history.FindIndex(x => x.Inputs.FrameID == serverFrameID);

            if (historyIndex == -1) return; // We don't have history for this (too old)

            PredictionState recordedState = _history[historyIndex];

            // A. COMPARE
            // Did where we THOUGHT we were match where the Server says we are?
            float drift = Vector3.Distance(recordedState.PredictedPosition, serverPos);

            if (drift > SnapThreshold)
            {
                // B. SNAP (The Correction)
                Debug.LogWarning($"Drift Detected ({drift}m). Reconciling...");

                // Disable CharacterController temporarily so we can teleport
                _controller.enabled = false;
                transform.position = serverPos;
                _controller.enabled = true;

                // C. REPLAY (The Time Machine)
                // We are now at the server's past position. 
                // We must re-apply every input that happened SINCE then to catch up to now.
                for (int i = historyIndex + 1; i < _history.Count; i++)
                {
                    PredictionState historicalState = _history[i];
                    ProcessMovement(historicalState.Inputs);
                    
                    // Update the history with the NEW corrected position
                    historicalState.PredictedPosition = transform.position;
                    _history[i] = historicalState;
                }
            }

            // D. CLEANUP
            // Remove old history that the server has already confirmed.
            _history.RemoveRange(0, historyIndex + 1);
        }

        private void ProcessMovement(NormalizedInputFrame frame)
        {
            float moveX = InputQuantizer.Dequantize(frame.MoveX);
            float moveZ = InputQuantizer.Dequantize(frame.MoveY);
            Vector3 move = transform.right * moveX + transform.forward * moveZ;
            _controller.Move(move * Speed * Time.fixedDeltaTime);
        }

        void OnDestroy()
        {
             if(ServerStateSource != null) ServerStateSource.OnServerFrameProcessed -= HandleServerUpdate;
        }
    }
}