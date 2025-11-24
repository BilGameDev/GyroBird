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
    private bool isConnected = false;
    private bool isSending = true;

    public bool IsConnected => isConnected;
    public bool IsSending => isSending;
    
    // 1 byte message type + 16 bytes quaternion
    private byte[] gyroData = new byte[17];
    private byte[] messageData = new byte[1];
    
    // Message types
    private const byte MSG_GYRO_DATA = 0;
    private const byte MSG_CALIBRATE = 1;
    private const byte MSG_SHOOT = 2;
    private const byte MSG_RESTART = 3;

    void Start()
    {
        udp = new UdpClient();
        
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
            isConnected = true;
            Debug.Log($"[GyroUdpSender] Target set to {serverIp}:{serverPort}");
        }
        else
        {
            remoteEndPoint = null;
            isConnected = false;
        }
    }

    void Update()
    {
        if (!gyroSupported || remoteEndPoint == null || !isSending)
            return;

        Quaternion raw = Input.gyro.attitude;

        // Convert from device to Unity coordinate space
        Quaternion unityAttitude = new Quaternion(raw.x, raw.y, -raw.z, -raw.w);

        // Rotate 90 degrees so phone screen points forward (not top of phone)
        Quaternion phoneForward = Quaternion.Euler(90, 0, 0) * unityAttitude;

        // Apply calibration (so current orientation becomes "zero")
        Quaternion relative = Quaternion.Inverse(calibration) * phoneForward;

        // Serialize packet: [type][x][y][z][w]
        gyroData[0] = MSG_GYRO_DATA; // single byte
        Buffer.BlockCopy(BitConverter.GetBytes(relative.x), 0, gyroData, 1, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(relative.y), 0, gyroData, 5, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(relative.z), 0, gyroData, 9, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(relative.w), 0, gyroData, 13, 4);

        udp.Send(gyroData, gyroData.Length, remoteEndPoint);
    }

    public void Calibrate()
    {
        if (!gyroSupported) return;

        Quaternion raw = Input.gyro.attitude;
        Quaternion unityAttitude = new Quaternion(raw.x, raw.y, -raw.z, -raw.w);
        // Apply phone orientation remapping for calibration
        calibration = Quaternion.Euler(90, 0, 0) * unityAttitude;

        // Send calibration message
        if (remoteEndPoint != null && isSending)
        {
            messageData[0] = MSG_CALIBRATE;
            udp.Send(messageData, 1, remoteEndPoint);
        }

        Debug.Log("[GyroUdpSender] Gyro calibrated.");
    }
    
    public void Shoot()
    {
        if (remoteEndPoint != null && isSending)
        {
            messageData[0] = MSG_SHOOT;
            udp.Send(messageData, 1, remoteEndPoint);
            Debug.Log("[GyroUdpSender] Shoot command sent");
        }
    }
    
    public void SendRestart()
    {
        if (remoteEndPoint != null && isSending)
        {
            messageData[0] = MSG_RESTART;
            udp.Send(messageData, 1, remoteEndPoint);
            Debug.Log("[GyroUdpSender] Restart command sent");
        }
    }

    public void SetServer(string ip, int port)
    {
        serverIp = ip;
        serverPort = port;
        UpdateRemoteEndPoint();
    }

    public void StartSending()
    {
        isSending = true;
        Debug.Log("[GyroUdpSender] Sending started");
    }

    public void StopSending()
    {
        isSending = false;
        Debug.Log("[GyroUdpSender] Sending stopped");
    }

    public void Disconnect()
    {
        isSending = false;
        isConnected = false;
        remoteEndPoint = null;
        serverIp = string.Empty;
        Debug.Log("[GyroUdpSender] Disconnected from server");
    }

    void OnDestroy()
    {
        Disconnect();
        udp?.Close();
    }
}
