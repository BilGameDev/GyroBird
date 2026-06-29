using System;
using System.Net;
using System.Net.Sockets;
using UnityEngine;
using UnityEngine.InputSystem;

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
    
    private readonly byte[] gyroData = new byte[ControllerPacket.AimPacketSize];
    private readonly byte[] messageData = new byte[ControllerPacket.CommandPacketSize];
    private ushort sequence;

    void Start()
    {
        Application.targetFrameRate = 60;
        udp = new UdpClient();
        
        gyroSupported = AttitudeSensor.current != null;
        if (gyroSupported)
        {
            InputSystem.EnableDevice(AttitudeSensor.current);
            AttitudeSensor.current.samplingFrequency = 120;
        }
        else
        {
            Debug.LogWarning("[GyroUdpSender] Device attitude sensor not supported on this device!");
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

        Quaternion raw = AttitudeSensor.current.attitude.ReadValue();

        // Convert from device to Unity coordinate space
        Quaternion unityAttitude = new Quaternion(raw.x, raw.y, -raw.z, -raw.w);

        // Rotate 90 degrees so phone screen points forward (not top of phone)
        Quaternion phoneForward = Quaternion.Euler(90, 0, 0) * unityAttitude;

        // Apply calibration (so current orientation becomes "zero")
        Quaternion relative = Quaternion.Inverse(calibration) * phoneForward;

        int length = ControllerPacket.WriteAim(gyroData, NextSequence(), Time.realtimeSinceStartup, relative);
        udp.Send(gyroData, length, remoteEndPoint);
    }

    public void Calibrate()
    {
        if (!gyroSupported) return;

        Quaternion raw = AttitudeSensor.current.attitude.ReadValue();
        Quaternion unityAttitude = new Quaternion(raw.x, raw.y, -raw.z, -raw.w);
        // Apply phone orientation remapping for calibration
        calibration = Quaternion.Euler(90, 0, 0) * unityAttitude;

        SendCommand(ControllerPacketType.Calibrate);

        Debug.Log("[GyroUdpSender] Gyro calibrated.");
    }
    
    public void Shoot()
    {
        SendCommand(ControllerPacketType.Shoot);
        Debug.Log("[GyroUdpSender] Shoot command sent");
    }
    
    public void SendRestart()
    {
        SendCommand(ControllerPacketType.Restart);
        Debug.Log("[GyroUdpSender] Restart command sent");
    }

    public void SetServer(string ip, int port)
    {
        serverIp = ip;
        serverPort = port;
        UpdateRemoteEndPoint();
    }

    private void SendCommand(ControllerPacketType type)
    {
        if (remoteEndPoint == null || !isSending)
            return;

        ushort commandSequence = NextSequence();
        int length = ControllerPacket.WriteCommand(messageData, type, commandSequence, Time.realtimeSinceStartup);

        for (int i = 0; i < 3; i++)
        {
            udp.Send(messageData, length, remoteEndPoint);
        }
    }

    private ushort NextSequence()
    {
        sequence++;
        if (sequence == 0)
            sequence = 1;

        return sequence;
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
