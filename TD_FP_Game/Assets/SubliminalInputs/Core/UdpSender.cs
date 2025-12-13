using UnityEngine;
using System.Net.Sockets;
using System.Net;
using SubliminalSarcasm.InputSystem;
using SubliminalSarcasm.NetworkData;

namespace SubliminalSarcasm.Core
{
    public class UdpSender : MonoBehaviour
    {
        public InputInterceptor InputSource;
        
        [Header("Network Settings")]
        public string ServerIP = "127.0.0.1"; // "Localhost"
        public int Port = 8080;

        private UdpClient _udpClient;
        private IPEndPoint _remoteEndPoint;

        void Start()
        {
            _udpClient = new UdpClient();
            _remoteEndPoint = new IPEndPoint(IPAddress.Parse(ServerIP), Port);
        }

        void FixedUpdate()
        {
            // 1. Get Data
            NormalizedInputFrame frame = InputSource.CurrentInput;

            // 2. Crush to Bytes
            byte[] data = PacketSerializer.Serialize(frame);

            // 3. Fire into the Internet
            // This now leaves your computer and travels over Wi-Fi!
            try
            {
                _udpClient.Send(data, data.Length, _remoteEndPoint);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Socket Error: {e.Message}");
            }
        }

        void OnDestroy()
        {
            _udpClient?.Close();
        }
        
        public void SetServerIP(string newIP)
        {
            ServerIP = newIP;
            // Re-initialize the endpoint with the new address
            _remoteEndPoint = new IPEndPoint(IPAddress.Parse(ServerIP), Port);
            Debug.Log($"Target IP changed to: {ServerIP}");
        }
    }
}