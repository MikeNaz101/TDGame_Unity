using UnityEngine;
using System.Net.Sockets;
using System.Net;
using SubliminalSarcasm.NetworkData; // Using your namespace
using SubliminalSarcasm.InputSystem;

namespace SubliminalSarcasm.Core
{
    public class UdpReceiver : MonoBehaviour
    {
        public InputConsumer TargetConsumer;
        public int Port = 8080;

        [Header("Status")]
        public bool IsConnected = false;
        public string ConnectedClientIP = "";
        
        // VISUAL DEBUG: Drag a UI Text or GameObject here to toggle it
        public GameObject ConnectedIndicator; 

        private UdpClient _udpListener;
        private bool _isRunning = true;

        void Start()
        {
            _udpListener = new UdpClient(Port);
            
            if(ConnectedIndicator != null) 
                ConnectedIndicator.SetActive(false); // Start turned off

            ReceiveLoop();
        }

        private async void ReceiveLoop()
        {
            while (_isRunning && _udpListener != null)
            {
                try
                {
                    // 1. Wait for data
                    // ReceiveAsync returns a "Result" object that contains the Sender's IP!
                    UdpReceiveResult result = await _udpListener.ReceiveAsync();
                    byte[] receivedBytes = result.Buffer;

                    // 2. CHECK FOR NEW CONNECTION
                    if (!IsConnected)
                    {
                        // We just got our first packet!
                        IsConnected = true;
                        ConnectedClientIP = result.RemoteEndPoint.Address.ToString();
                        
                        // We must run UI updates on the main thread
                        // (Unity doesn't let background threads touch GameObjects)
                        MainThreadDispatcher.Enqueue(() => {
                            Debug.Log($"🟢 CLIENT CONNECTED from: {ConnectedClientIP}");
                            if(ConnectedIndicator != null) ConnectedIndicator.SetActive(true);
                        });
                    }

                    // 3. Process Data
                    NormalizedInputFrame frame = PacketSerializer.Deserialize(receivedBytes);
                    
                    // Note: We need a thread dispatcher for this too ideally, 
                    // but for this MVP assigning variables is usually safe.
                    TargetConsumer.InjectRemoteFrame(frame);
                }
                catch
                {
                    // Socket error or closed
                }
            }
        }

        void OnDestroy()
        {
            _isRunning = false;
            _udpListener?.Close();
        }
    }
}