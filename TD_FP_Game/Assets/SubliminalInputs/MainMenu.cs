using UnityEngine;
using TMPro; // Or UnityEngine.UI if using Legacy
using SubliminalSarcasm.Core;

public class MainMenu : MonoBehaviour
{
    public TMP_InputField IpInput;
    public UdpSender SenderScript;
    public GameObject UiPanel;

    public void OnConnectButton()
    {
        string ip = IpInput.text;
        if (!string.IsNullOrEmpty(ip))
        {
            SenderScript.SetServerIP(ip);
            UiPanel.SetActive(false); // Hide the menu
        }
    }
}