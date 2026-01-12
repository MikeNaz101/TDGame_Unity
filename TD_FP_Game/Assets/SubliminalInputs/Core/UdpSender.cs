using UnityEngine;
using System.Net.Sockets;
using System.Net;
using SubliminalSarcasm.NetworkData;
using SubliminalSarcasm.InputSystem;

namespace SubliminalSarcasm.Core
{
    public class UdpSender : MonoBehaviour
    {
        public InputInterceptor InputSource;
        
        [Header("Network Settings")]
        public string ServerIP = ""; // Default to empty
        public int Port = 8080;
        
        // NEW: Safety switch
        public bool IsReadyToSend = false; 

        private UdpClient _udpClient;
        private IPEndPoint _remoteEndPoint;

        void Start()
        {
            _udpClient = new UdpClient();
            // Don't setup the endpoint yet. Wait for the UI.
        }

        // Call this via the UI Button
        public void SetServerIP(string newIP)
        {
            if(string.IsNullOrEmpty(newIP)) return;

            ServerIP = newIP;
            try {
                _remoteEndPoint = new IPEndPoint(IPAddress.Parse(ServerIP), Port);
                IsReadyToSend = true; // NOW we are allowed to send
                Debug.Log($"Target Locked: {ServerIP}. Sending Data...");
            }
            catch {
                Debug.LogError("Invalid IP Address!");
                IsReadyToSend = false;
            }
        }

        void FixedUpdate()
        {
            // GATE: Stop if we aren't ready
            if (!IsReadyToSend || _remoteEndPoint == null) return;

            NormalizedInputFrame frame = InputSource.CurrentInput;
            byte[] data = PacketSerializer.Serialize(frame);

            try
            {
                _udpClient.Send(data, data.Length, _remoteEndPoint);
            }
            catch (System.Exception e)
            {
                // Silently fail or log sparingly
            }
        }

        void OnDestroy()
        {
            _udpClient?.Close();
        }
    }
}