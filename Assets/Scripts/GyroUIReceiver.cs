using System;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

public class GyroUIReceiver : MonoBehaviour
{
    [Header("Network")]
    public int listenPort = 7777;

    [Header("UI Crosshair")]
    public RectTransform crosshair;
    public Canvas canvas;
    
    [Header("Shooting Integration")]
    [SerializeField] private MouseShooter mouseShooter; // for network shooting
    
    [Header("Movement Settings")]
    [Tooltip("Smoothing for position (higher = faster response)")]
    public float smoothPos = 15f;
    
    [Tooltip("Sensitivity multiplier - higher = more sensitive")]
    public float sensitivity = 1.5f;
    
    [Tooltip("Vertical range of movement (screen height percentage)")]
    public float verticalRange = 0.85f;
    
    [Tooltip("Horizontal range of movement (screen width percentage)")]
    public float horizontalRange = 0.85f;
    
    [Tooltip("Dead zone - ignore small movements below this angle")]
    public float deadZone = 0.5f;
    
    [Tooltip("Max tilt angle for full range of motion")]
    public float maxTiltAngle = 30f;
    
    [Tooltip("Use exponential response curve for more precision at center")]
    public bool useExponentialCurve = true;
    
    [Tooltip("Exponential curve power (higher = more precision at center)")]
    public float curvePower = 2f;

    private UdpClient udp;
    private IPEndPoint anyIP;
    private Quaternion latestRotation = Quaternion.identity;
    private bool isListening = true;
    private bool isProcessing = true;

    private RectTransform canvasRect;
    private Vector2 targetAnchoredPos;
    private Vector2 currentVelocity;
    private Quaternion calibrationOffset = Quaternion.identity;
    
    // Message types (must match GyroUdpSender)
    private const byte MSG_GYRO_DATA = 0;
    private const byte MSG_CALIBRATE = 1;
    private const byte MSG_SHOOT = 2;
    private const byte MSG_RESTART = 3;
    
    // Command flags for main thread processing
    private bool pendingShoot = false;
    private bool pendingCalibrate = false;
    private bool pendingRestart = false;
    private bool pendingConnectionNotify = false; // main-thread connection notification
    private string pendingRemoteIp = null;
    
    [Header("Diagnostics")]
    [SerializeField] private bool verboseLogging = true;
    [SerializeField] private int logEveryNPackets = 60; // log pitch/yaw every N packets
    private int packetCounter = 0;

    public bool IsListening => isListening;
    public bool IsProcessing => isProcessing;

    void Start()
    {
        if (!canvas)
            canvas = GetComponentInParent<Canvas>();

        if (canvas)
            canvasRect = canvas.GetComponent<RectTransform>();

        if (crosshair)
            targetAnchoredPos = crosshair.anchoredPosition;

        anyIP = new IPEndPoint(IPAddress.Any, 0);
        udp = new UdpClient(listenPort);
        udp.BeginReceive(ReceiveCallback, null);

        Debug.Log($"[GyroUIReceiver] Listening on UDP port {listenPort}");
    }

    private void ReceiveCallback(IAsyncResult ar)
    {
        if (!isListening)
            return;

        try
        {
            byte[] data = udp.EndReceive(ar, ref anyIP);
            ProcessNetworkData(data);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[GyroUIReceiver] UDP receive error: " + e.Message);
        }
        finally
        {
            if (isListening)
                udp.BeginReceive(ReceiveCallback, null);
        }
    }
    
    private void ProcessNetworkData(byte[] data)
    {
        if (data == null || data.Length == 0) return;
        int len = data.Length;
        // Verbose packet length logging (can be toggled off later)
        if (len != 16 && len != 17 && len != 1)
        {
            Debug.LogWarning($"[GyroUIReceiver] Unexpected packet length {len}. Raw bytes: {BitConverter.ToString(data)}");
        }
        
        // Handle legacy format (16 bytes, no message type)
        if (data.Length == 16)
        {
            ProcessLegacyGyroData(data);
            return;
        }
        
        // Handle new format with message types
        if (data.Length >= 1)
        {
            byte messageType = data[0];
            
            switch (messageType)
            {
                case MSG_GYRO_DATA:
                    // Require exact 17 bytes (1 type + 16 quaternion) for new format
                    if (data.Length == 17)
                    {
                        try { ProcessNewGyroData(data); }
                        catch (Exception ex)
                        {
                            Debug.LogError($"[GyroUIReceiver] Error processing gyro data (len={data.Length}): {ex.Message}");
                        }
                    }
                    else if (data.Length == 16)
                    {
                        // Some senders may omit type byte (legacy). Treat as legacy.
                        try { ProcessLegacyGyroData(data); }
                        catch (Exception ex)
                        {
                            Debug.LogError($"[GyroUIReceiver] Error processing legacy gyro data (len={data.Length}): {ex.Message}");
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"[GyroUIReceiver] Gyro data packet unexpected length {data.Length}, ignoring.");
                    }
                    break;
                    
                case MSG_CALIBRATE:
                    pendingCalibrate = true;
                    break;
                    
                case MSG_SHOOT:
                    pendingShoot = true;
                    break;
                    
                case MSG_RESTART:
                    pendingRestart = true;
                    break;
            }
        }
    }
    
    private void ProcessLegacyGyroData(byte[] data)
    {
        float x = BitConverter.ToSingle(data, 0);
        float y = BitConverter.ToSingle(data, 4);
        float z = BitConverter.ToSingle(data, 8);
        float w = BitConverter.ToSingle(data, 12);
        latestRotation = new Quaternion(x, y, z, w);
        // Flag connection notify for main thread (avoid Time.time off-thread)
        pendingRemoteIp = anyIP?.Address.ToString();
        pendingConnectionNotify = true;
        packetCounter++;
        if (verboseLogging && packetCounter % logEveryNPackets == 0)
        {
            Debug.Log($"[GyroUIReceiver] Legacy packet #{packetCounter} quat=({x:F3},{y:F3},{z:F3},{w:F3})");
        }
    }
    
    private void ProcessNewGyroData(byte[] data)
    {
        float x = BitConverter.ToSingle(data, 1);
        float y = BitConverter.ToSingle(data, 5);
        float z = BitConverter.ToSingle(data, 9);
        float w = BitConverter.ToSingle(data, 13);
        latestRotation = new Quaternion(x, y, z, w);
        // Flag connection notify for main thread
        pendingRemoteIp = anyIP?.Address.ToString();
        pendingConnectionNotify = true;
        packetCounter++;
        if (verboseLogging && packetCounter % logEveryNPackets == 0)
        {
            Debug.Log($"[GyroUIReceiver] New packet #{packetCounter} quat=({x:F3},{y:F3},{z:F3},{w:F3})");
        }
    }

    void Update()
    {
        // Process network commands on main thread
        if (pendingShoot)
        {
            pendingShoot = false;
            HandleShootCommand();
        }
        
        if (pendingCalibrate)
        {
            pendingCalibrate = false;
            HandleNetworkCalibrate();
        }
        
        if (pendingRestart)
        {
            pendingRestart = false;
            HandleRestartCommand();
        }

        if (pendingConnectionNotify)
        {
            pendingConnectionNotify = false;
            ConnectionSubject.NotifyPacketReceived(pendingRemoteIp);
            pendingRemoteIp = null;
        }
        
        if (!crosshair || !canvasRect || !isProcessing)
            return;

        // Apply calibration offset to counteract drift
        Quaternion calibratedRotation = Quaternion.Inverse(calibrationOffset) * latestRotation;

        // Extract pitch (up/down) and yaw (left/right nose rotation) from phone rotation
        Vector3 euler = calibratedRotation.eulerAngles;
        float pitch = euler.x;
        float yaw = euler.z;
        
        // Normalize to -180 to 180 range
        if (pitch > 180f) pitch -= 360f;
        if (yaw > 180f) yaw -= 360f;
        
        // Apply dead zone
        if (Mathf.Abs(pitch) < deadZone) pitch = 0f;
        if (Mathf.Abs(yaw) < deadZone) yaw = 0f;
        
        // Clamp to max tilt angle and normalize to -1 to 1
        pitch = Mathf.Clamp(pitch, -maxTiltAngle, maxTiltAngle);
        yaw = Mathf.Clamp(yaw, -maxTiltAngle, maxTiltAngle);
        
        float normalizedPitch = pitch / maxTiltAngle;
        float normalizedYaw = yaw / maxTiltAngle;
        
        // Apply exponential curve for better precision at center
        if (useExponentialCurve)
        {
            normalizedPitch = Mathf.Sign(normalizedPitch) * Mathf.Pow(Mathf.Abs(normalizedPitch), curvePower);
            normalizedYaw = Mathf.Sign(normalizedYaw) * Mathf.Pow(Mathf.Abs(normalizedYaw), curvePower);
        }
        
        // Apply sensitivity
        normalizedPitch *= sensitivity;
        normalizedYaw *= sensitivity;
        
        // Clamp final values
        normalizedPitch = Mathf.Clamp(normalizedPitch, -1f, 1f);
        normalizedYaw = Mathf.Clamp(normalizedYaw, -1f, 1f);
        
        // Calculate canvas dimensions
        Vector2 canvasSize = canvasRect.sizeDelta;
        float halfWidth = canvasSize.x * 0.5f;
        float halfHeight = canvasSize.y * 0.5f;
        
        // Calculate position offsets
        // Inverted pitch so nose up = crosshair up
        float verticalOffset = -normalizedPitch * halfHeight * verticalRange;
        
        // Inverted yaw so nose left = crosshair left
        float horizontalOffset = -normalizedYaw * halfWidth * horizontalRange;
        
        // Update target position
        targetAnchoredPos = new Vector2(horizontalOffset, verticalOffset);
        
        // Use SmoothDamp for more responsive and natural movement
        crosshair.anchoredPosition = Vector2.SmoothDamp(
            crosshair.anchoredPosition,
            targetAnchoredPos,
            ref currentVelocity,
            1f / smoothPos
        );

        if (verboseLogging && packetCounter % logEveryNPackets == 0)
        {
            Debug.Log($"[GyroUIReceiver] Pos update pitch={pitch:F1} yaw={yaw:F1} norm=({normalizedPitch:F2},{normalizedYaw:F2}) crosshair={crosshair.anchoredPosition}");
        }
    }

    public void StopListening()
    {
        isListening = false;
        Debug.Log("[GyroUIReceiver] Stopped listening for gyro data");
        ConnectionSubject.ForceDisconnect();
    }

    public void StartListening()
    {
        if (!isListening)
        {
            isListening = true;
            udp.BeginReceive(ReceiveCallback, null);
            Debug.Log("[GyroUIReceiver] Started listening for gyro data");
        }
    }

    public void StopProcessing()
    {
        isProcessing = false;
        Debug.Log("[GyroUIReceiver] Stopped processing gyro data");
    }

    public void StartProcessing()
    {
        isProcessing = true;
        Debug.Log("[GyroUIReceiver] Started processing gyro data");
    }

    public void Recalibrate()
    {
        calibrationOffset = latestRotation;
        currentVelocity = Vector2.zero;
        Debug.Log("[GyroUIReceiver] Recalibrated to current rotation");
    }

    public void Disconnect()
    {
        StopListening();
        StopProcessing();
        latestRotation = Quaternion.identity;
        calibrationOffset = Quaternion.identity;
        currentVelocity = Vector2.zero;
        if (crosshair)
            crosshair.anchoredPosition = Vector2.zero;
        Debug.Log("[GyroUIReceiver] Disconnected");
        ConnectionSubject.ForceDisconnect();
    }

    private void HandleShootCommand()
    {
        Debug.Log("[GyroUIReceiver] Shoot command received");
        
        if (mouseShooter)
        {
            mouseShooter.GyroShoot();
        }
    }
    
    private void HandleNetworkCalibrate()
    {
        Debug.Log("[GyroUIReceiver] Network calibrate command received");
        Recalibrate();
    }
    
    private void HandleRestartCommand()
    {
        Debug.Log("[GyroUIReceiver] Restart command received from phone");
        
        GameManager.RestartGame();
    }
    
    void OnDestroy()
    {
        Disconnect();
        udp?.Close();
    }
}
