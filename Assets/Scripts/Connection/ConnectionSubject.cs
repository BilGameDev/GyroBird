using System;
using UnityEngine;

/// <summary>
/// Static connection state manager using pure observer pattern.
/// No singleton - monitors connection state and notifies subscribers via events.
/// </summary>
public static class ConnectionSubject
{
    private static float timeoutSeconds = 5f;
    private static float lastPacketTime = -1f;
    
    public static bool IsConnected { get; private set; }
    public static string LastRemoteIP { get; private set; } = "";
    public static float GetLastPacketAge() => lastPacketTime < 0f ? float.PositiveInfinity : Time.time - lastPacketTime;

    // Observer pattern events
    public static event Action OnConnected;
    public static event Action<string> OnConnectedInfo;
    public static event Action OnDisconnected;

    /// <summary>
    /// Initialize with timeout value. Call from ConnectionMonitor component.
    /// </summary>
    public static void Initialize(float timeout = 5f)
    {
        timeoutSeconds = timeout;
        IsConnected = false;
        lastPacketTime = -1f;
        LastRemoteIP = "";
    }

    /// <summary>
    /// Update timeout check. Call from ConnectionMonitor Update.
    /// </summary>
    public static void Update()
    {
        if (IsConnected && timeoutSeconds > 0f && lastPacketTime >= 0f)
        {
            if (Time.time - lastPacketTime > timeoutSeconds)
            {
                Debug.Log("[ConnectionSubject] Disconnected (timeout)");
                SetDisconnected();
            }
        }
    }

    /// <summary>
    /// Notify that a packet was received.
    /// </summary>
    public static void NotifyPacketReceived(string remoteIp = null)
    {
        lastPacketTime = Time.time;
        if (!string.IsNullOrEmpty(remoteIp))
            LastRemoteIP = remoteIp;
        if (!IsConnected)
        {
            IsConnected = true;
            Debug.Log("[ConnectionSubject] Connected" + (string.IsNullOrEmpty(LastRemoteIP) ? string.Empty : " from " + LastRemoteIP));
            OnConnected?.Invoke();
            OnConnectedInfo?.Invoke(LastRemoteIP);
        }
    }

    /// <summary>
    /// Force disconnect.
    /// </summary>
    public static void ForceDisconnect()
    {
        if (IsConnected)
        {
            Debug.Log("[ConnectionSubject] Forced disconnect (last IP=" + LastRemoteIP + ")");
            SetDisconnected();
        }
        lastPacketTime = -1f;
        LastRemoteIP = string.Empty;
    }

    private static void SetDisconnected()
    {
        IsConnected = false;
        OnDisconnected?.Invoke();
    }
}
