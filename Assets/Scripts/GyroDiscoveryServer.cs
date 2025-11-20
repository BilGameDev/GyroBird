using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

public class GyroDiscoveryServer : MonoBehaviour
{
    public int discoveryPort = 7778;
    public int gamePort = 7777;

    private UdpClient udp;

    void Start()
    {
        udp = new UdpClient(discoveryPort);
        udp.BeginReceive(ReceiveCallback, null);
        Debug.Log($"[GyroDiscoveryServer] Listening for discovery on port {discoveryPort}");
    }

    private void ReceiveCallback(IAsyncResult ar)
    {
        IPEndPoint sender = new IPEndPoint(IPAddress.Any, 0);
        byte[] data;

        try
        {
            data = udp.EndReceive(ar, ref sender);
        }
        catch (ObjectDisposedException)
        {
            return; // socket closed
        }

        string msg = Encoding.UTF8.GetString(data);
        // Simple protocol: client sends "DISCOVER_GYRO_SERVER"
        if (msg == "DISCOVER_GYRO_SERVER")
        {
            string response = $"GYRO_SERVER|{gamePort}";
            byte[] respBytes = Encoding.UTF8.GetBytes(response);
            udp.Send(respBytes, respBytes.Length, sender);
            Debug.Log($"[GyroDiscoveryServer] Responded to discovery from {sender}");
        }

        // Keep listening
        udp.BeginReceive(ReceiveCallback, null);
    }

    void OnDestroy()
    {
        udp?.Close();
    }
}
