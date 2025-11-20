using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

public class GyroDiscoveryClient : MonoBehaviour
{
    public int discoveryPort = 7778;
    public GyroUdpSender gyroSender;   // drag the sender here in Inspector

    private UdpClient udp;
    private IPEndPoint broadcastEndPoint;

    void Start()
    {
        udp = new UdpClient();
        udp.EnableBroadcast = true;
        broadcastEndPoint = new IPEndPoint(IPAddress.Broadcast, discoveryPort);

        // Start listening for responses
        udp.BeginReceive(ReceiveCallback, null);
    }

    public void SendDiscovery()
    {
        byte[] data = Encoding.UTF8.GetBytes("DISCOVER_GYRO_SERVER");
        udp.Send(data, data.Length, broadcastEndPoint);
        Debug.Log("[GyroDiscoveryClient] Sent discovery broadcast");
    }

    private void ReceiveCallback(IAsyncResult ar)
    {
        IPEndPoint serverEP = new IPEndPoint(IPAddress.Any, 0);
        byte[] data;

        try
        {
            data = udp.EndReceive(ar, ref serverEP);
        }
        catch (ObjectDisposedException)
        {
            return;
        }

        string msg = Encoding.UTF8.GetString(data);
        if (msg.StartsWith("GYRO_SERVER|"))
        {
            string[] parts = msg.Split('|');
            if (parts.Length >= 2 && int.TryParse(parts[1], out int port))
            {
                string ip = serverEP.Address.ToString();
                Debug.Log($"[GyroDiscoveryClient] Discovered server {ip}:{port}");

                if (gyroSender != null)
                {
                    gyroSender.SetServer(ip, port);
                }
            }
        }

        // Keep listening
        udp.BeginReceive(ReceiveCallback, null);
    }

    void OnDestroy()
    {
        udp?.Close();
    }
}
