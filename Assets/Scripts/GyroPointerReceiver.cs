using System;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

public class GyroPointerReceiver : MonoBehaviour
{
    [Header("Network")]
    public int listenPort = 7777;

    [Header("Pointer")]
    public Transform laserSphere;
    public Camera targetCamera;
    public float planeDistance = 10f;
    
    [Header("Movement Settings")]
    [Tooltip("Smoothing for position (higher = faster response)")]
    public float smoothPos = 20f;
    
    [Tooltip("Sensitivity multiplier - higher = more sensitive")]
    public float sensitivity = 2f;
    
    [Tooltip("Vertical range of movement (screen height percentage)")]
    public float verticalRange = 0.9f;
    
    [Tooltip("Horizontal range of movement (screen width percentage)")]
    public float horizontalRange = 0.9f;
    
    [Tooltip("Dead zone - ignore small movements below this angle")]
    public float deadZone = 1f;
    
    [Tooltip("Max tilt angle for full range of motion")]
    public float maxTiltAngle = 25f;

    private UdpClient udp;
    private IPEndPoint anyIP;
    private Quaternion latestRotation = Quaternion.identity;

    void Start()
    {
        if (!targetCamera)
            targetCamera = Camera.main;

        anyIP = new IPEndPoint(IPAddress.Any, 0);
        udp = new UdpClient(listenPort);
        udp.BeginReceive(ReceiveCallback, null);

        Debug.Log($"[GyroPointerReceiver] Listening on UDP port {listenPort}");
    }

    private void ReceiveCallback(IAsyncResult ar)
    {
        try
        {
            byte[] data = udp.EndReceive(ar, ref anyIP);

            if (data.Length >= 16)
            {
                float x = BitConverter.ToSingle(data, 0);
                float y = BitConverter.ToSingle(data, 4);
                float z = BitConverter.ToSingle(data, 8);
                float w = BitConverter.ToSingle(data, 12);

                latestRotation = new Quaternion(x, y, z, w);
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[GyroPointerReceiver] UDP receive error: " + e.Message);
        }
        finally
        {
            udp.BeginReceive(ReceiveCallback, null);
        }
    }

    void Update()
    {
        if (!laserSphere || !targetCamera)
            return;

        // Extract pitch (up/down) and yaw (left/right nose rotation) from phone rotation
        // After 90-degree rotation in sender: X=pitch, Z=yaw (nose left/right)
        Vector3 euler = latestRotation.eulerAngles;
        float pitch = euler.x;
        float yaw = euler.z;  // Z-axis is yaw after rotation
        
        // Normalize to -180 to 180 range
        if (pitch > 180f) pitch -= 360f;
        if (yaw > 180f) yaw -= 360f;
        
        // Apply dead zone
        if (Mathf.Abs(pitch) < deadZone) pitch = 0f;
        if (Mathf.Abs(yaw) < deadZone) yaw = 0f;
        
        // Clamp to max tilt angle and normalize to -1 to 1
        pitch = Mathf.Clamp(pitch, -maxTiltAngle, maxTiltAngle);
        yaw = Mathf.Clamp(yaw, -maxTiltAngle, maxTiltAngle);
        
        float normalizedPitch = (pitch / maxTiltAngle) * sensitivity;
        float normalizedYaw = (yaw / maxTiltAngle) * sensitivity;
        
        // Clamp final values to prevent going off screen
        normalizedPitch = Mathf.Clamp(normalizedPitch, -1f, 1f);
        normalizedYaw = Mathf.Clamp(normalizedYaw, -1f, 1f);
        
        // Calculate screen bounds in world space
        Transform cam = targetCamera.transform;
        Vector3 planeCenter = cam.position + cam.forward * planeDistance;
        
        // Get screen dimensions at the plane distance
        float halfHeight = Mathf.Tan(targetCamera.fieldOfView * 0.5f * Mathf.Deg2Rad) * planeDistance;
        float halfWidth = halfHeight * targetCamera.aspect;
        
        // Calculate vertical position based on pitch (nose up/down)
        float verticalOffset = -normalizedPitch * halfHeight * verticalRange;
        
        // Calculate horizontal position based on yaw (nose left/right rotation)
        // Inverted so nose left = sphere left
        float horizontalOffset = -normalizedYaw * halfWidth * horizontalRange;
        
        // Calculate target world position
        Vector3 targetPos = planeCenter 
            + cam.up * verticalOffset 
            + cam.right * horizontalOffset;
        
        // Smoothly move the sphere
        laserSphere.position = Vector3.Lerp(
            laserSphere.position,
            targetPos,
            Time.deltaTime * smoothPos
        );
        
        // Debug visualization - uncomment to see values
        if (Time.frameCount % 30 == 0)
        {
           // Debug.Log($"Pitch: {pitch:F1}°, Yaw: {yaw:F1}°, H: {horizontalOffset:F2}, V: {verticalOffset:F2}");
        }
    }

    void OnDestroy()
    {
        udp?.Close();
    }
}
