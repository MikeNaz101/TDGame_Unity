using UnityEngine;
using System.Collections.Generic;
using SubliminalSarcasm.InputSystem;
using System;

namespace SubliminalSarcasm.Core
{
    public class NetworkEmulator : MonoBehaviour
    {
        [Header("Settings")]
        public InputInterceptor InputSource; // Where we get real data
        public InputConsumer TargetConsumer; // Who wants the data
        
        [Tooltip("Simulated Latency in Milliseconds")]
        public float LatencyMS = 200f; // 200ms is a "laggy" connection
        
        public event Action<NormalizedInputFrame> OnServerFrameReceived;

        // A queue to hold our packets while they "travel" through the fake internet
        private Queue<DelayedFrame> _packetQueue = new Queue<DelayedFrame>();

        // Wrapper struct to track when a packet should be released
        private struct DelayedFrame
        {
            public NormalizedInputFrame Frame;
            public float DeliveryTime;
        }

        void FixedUpdate()
        {
            // 1. INGEST
            // Grab the fresh frame from the Interceptor
            NormalizedInputFrame currentFrame = InputSource.CurrentInput;

            // 2. DELAY
            // Instead of sending it immediately, we calculate when it *should* arrive
            DelayedFrame packet = new DelayedFrame
            {
                Frame = currentFrame,
                DeliveryTime = Time.time + (LatencyMS / 1000f)
            };
            _packetQueue.Enqueue(packet);

            // 3. DELIVER
            // Check if the oldest packet in the queue is ready to be released
            if (_packetQueue.Count > 0)
            {
                DelayedFrame oldest = _packetQueue.Peek();
                if (Time.time >= oldest.DeliveryTime)
                {
                    DelayedFrame frameToSend = _packetQueue.Dequeue();
                    TargetConsumer.InjectRemoteFrame(frameToSend.Frame); // Keep this for the visual ghost
                    OnServerFrameReceived?.Invoke(frameToSend.Frame);    // NEW: Notify the predictor
                }
            }
        }
    }
}