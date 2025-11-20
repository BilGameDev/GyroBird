using System;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

public class GyroUdpSender : MonoBehaviour
{
    [Header("Server")]
    public string serverIp = "192.168.0.10";  // will be overridden by discovery
    public int serverPort = 7777;

    private UdpClient udp;
    private IPEndPoint remoteEndPoint;

    private bool gyroSupported;
    private Quaternion calibration = Quaternion.identity;

    void Start()
    {
        udp = new UdpClient();
        UpdateRemoteEndPoint();

        gyroSupported = SystemInfo.supportsGyroscope;
        if (gyroSupported)
        {
            Input.gyro.enabled = true;
            Input.gyro.updateInterval = 0.005f; // 200Hz for better accuracy
        }
        else
        {
            Debug.LogWarning("[GyroUdpSender] Gyroscope not supported on this device!");
        }
    }

    private void UpdateRemoteEndPoint()
    {
        if (!string.IsNullOrEmpty(serverIp))
        {
            remoteEndPoint = new IPEndPoint(IPAddress.Parse(serverIp), serverPort);
            Debug.Log($"[GyroUdpSender] Target set to {serverIp}:{serverPort}");
        }
        else
        {
            remoteEndPoint = null;
        }
    }

    void Update()
    {
        if (!gyroSupported || remoteEndPoint == null)
            return;

        Quaternion raw = Input.gyro.attitude;

        // Convert from device to Unity coordinate space
        Quaternion unityAttitude = new Quaternion(raw.x, raw.y, -raw.z, -raw.w);

        // Rotate 90 degrees so phone screen points forward (not top of phone)
        Quaternion phoneForward = Quaternion.Euler(90, 0, 0) * unityAttitude;

        // Apply calibration (so current orientation becomes "zero")
        Quaternion relative = Quaternion.Inverse(calibration) * phoneForward;

        // Serialize quaternion (16 bytes)
        byte[] data = new byte[16];
        Buffer.BlockCopy(BitConverter.GetBytes(relative.x), 0, data, 0, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(relative.y), 0, data, 4, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(relative.z), 0, data, 8, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(relative.w), 0, data, 12, 4);

        udp.Send(data, data.Length, remoteEndPoint);
    }

    public void Calibrate()
    {
        if (!gyroSupported) return;

        Quaternion raw = Input.gyro.attitude;
        Quaternion unityAttitude = new Quaternion(raw.x, raw.y, -raw.z, -raw.w);
        // Apply phone orientation remapping for calibration
        calibration = Quaternion.Euler(90, 0, 0) * unityAttitude;

        Debug.Log("[GyroUdpSender] Gyro calibrated.");
    }

    public void SetServer(string ip, int port)
    {
        serverIp = ip;
        serverPort = port;
        UpdateRemoteEndPoint();
    }

    void OnDestroy()
    {
        udp?.Close();
    }
}
