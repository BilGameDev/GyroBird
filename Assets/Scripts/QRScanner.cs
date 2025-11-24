using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;
using TMPro;


#if UNITY_ANDROID || UNITY_IOS
using ZXing;
#endif

/// <summary>
/// QR Code scanner that uses device camera to scan QR codes.
/// Works with existing QRDisplay component for QR generation.
/// Integrates with existing network components for connection setup.
/// </summary>
public class QRScanner : MonoBehaviour
{
    [Header("UI Components")]
    [SerializeField] private RawImage cameraDisplay;
    [SerializeField] private Button startScanButton;
    [SerializeField] private GameObject scannerPanel;
    [SerializeField] private GameObject gamePanel;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text resultText;
    
    [Header("Camera Settings")]
    [SerializeField] private int requestedWidth = 512;
    [SerializeField] private int requestedHeight = 512;
    [SerializeField] private int requestedFPS = 30;
    [SerializeField] private bool preferFrontCamera = false;
    
    [Header("Scan Settings")]
    [SerializeField] private float scanInterval = 0.5f; // seconds between scans
    [SerializeField] private bool autoApplyConnection = true;
    [SerializeField] private bool closeScannerOnSuccess = true;
    [SerializeField] private bool autoFixRotation = true; // auto correct camera feed orientation
    
    [Header("Network Integration")]
    [SerializeField] private GyroUdpSender gyroSender;
    
    private WebCamTexture webCamTexture;
    private bool isScanning = false;
    private Coroutine scanCoroutine;
    
#if UNITY_ANDROID || UNITY_IOS
    private IBarcodeReader barcodeReader;
#endif
    
    public event Action<string> QRCodeScanned;
    
    void Start()
    {
        InitializeUI();
        
#if UNITY_ANDROID || UNITY_IOS
        // Initialize ZXing barcode reader
        barcodeReader = new BarcodeReader();
#endif
    }
    
    private void InitializeUI()
    {
        if (startScanButton)
            startScanButton.onClick.AddListener(StartScanning);
            
        UpdateStatusText("Ready to scan");
    }
    
    public void StartScanning()
    {
        if (isScanning) return;
        
#if UNITY_EDITOR
        UpdateStatusText("QR scanning not supported in editor");
        return;
#else
        if (WebCamTexture.devices.Length == 0)
        {
            UpdateStatusText("No camera devices found");
            return;
        }
        
        // Find appropriate camera
        WebCamDevice selectedDevice = WebCamTexture.devices[0];
        for (int i = 0; i < WebCamTexture.devices.Length; i++)
        {
            var device = WebCamTexture.devices[i];
            if (device.isFrontFacing == preferFrontCamera)
            {
                selectedDevice = device;
                break;
            }
        }
        
        // Start camera
        webCamTexture = new WebCamTexture(selectedDevice.name, requestedWidth, requestedHeight, requestedFPS);
        if (cameraDisplay)
        {
            cameraDisplay.texture = webCamTexture;
            cameraDisplay.material.mainTexture = webCamTexture;
        }
        
        webCamTexture.Play();
        isScanning = true;
        
        // Show scanner UI
        if (scannerPanel)
            scannerPanel.SetActive(true);
            
        // Start scanning coroutine
        scanCoroutine = StartCoroutine(ScanCoroutine());
        
        UpdateStatusText("Scanning for QR codes...");
        UpdateButtons();
#endif
    }
    
    public void StopScanning()
    {
        if (!isScanning) return;
        
        isScanning = false;
        
        if (scanCoroutine != null)
        {
            StopCoroutine(scanCoroutine);
            scanCoroutine = null;
        }
        
        if (webCamTexture != null)
        {
            webCamTexture.Stop();
            webCamTexture = null;
        }
        
        if (cameraDisplay)
            cameraDisplay.texture = null;
            
        // Hide scanner UI
            
        UpdateStatusText("Scanning stopped");
        UpdateButtons();
    }
    
    private IEnumerator ScanCoroutine()
    {
        while (isScanning)
        {
            if (webCamTexture != null && webCamTexture.isPlaying)
            {
#if UNITY_ANDROID || UNITY_IOS
                try
                {
                    // Get camera pixels
                    Color32[] pixels = webCamTexture.GetPixels32();
                    
                    // Decode QR code
                    var result = barcodeReader.Decode(pixels, webCamTexture.width, webCamTexture.height);
                    
                    if (result != null && !string.IsNullOrEmpty(result.Text))
                    {
                        OnQRCodeDetected(result.Text);
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[QRScanner] Scan error: {e.Message}");
                }
#endif
            }
            
            yield return new WaitForSeconds(scanInterval);
        }
    }
    
    private void OnQRCodeDetected(string qrData)
    {
        Debug.Log($"[QRScanner] QR Code detected: {qrData}");
        
        UpdateStatusText("QR Code found!");
        if (resultText)
            resultText.text = $"Found: {qrData}";
            
        // Notify listeners
        QRCodeScanned?.Invoke(qrData);
        
        // Auto-apply connection if enabled
        if (autoApplyConnection)
        {
            ApplyQRConnection(qrData);
        }
        
        // Close scanner if configured
        if (closeScannerOnSuccess)
        {
            StopScanning();
        }
    }
    
    private void ApplyQRConnection(string qrData)
    {
        try
        {
            // Try to parse as JSON first
            if (qrData.StartsWith("{"))
            {
                var connection = JsonUtility.FromJson<QRConnectionData>(qrData);
                if (!string.IsNullOrEmpty(connection.ip) && connection.port > 0)
                {
                    ApplyConnection(connection.ip, connection.port);
                    UpdateStatusText($"Connected to {connection.ip}:{connection.port}");
                    return;
                }
            }
            
            // Try simple IP:PORT format
            string[] parts = qrData.Split(':');
            if (parts.Length == 2)
            {
                string ip = parts[0].Trim();
                if (int.TryParse(parts[1].Trim(), out int port))
                {
                    ApplyConnection(ip, port);
                    UpdateStatusText($"Connected to {ip}:{port}");
                    return;
                }
            }
            
            // Try URL format
            if (qrData.StartsWith("gyro://"))
            {
                Uri uri = new Uri(qrData);
                string ip = uri.Host;
                int port = uri.Port > 0 ? uri.Port : 7777;
                ApplyConnection(ip, port);
                UpdateStatusText($"Connected to {ip}:{port}");
                return;
            }
            
            UpdateStatusText("Invalid QR format");
        }
        catch (Exception e)
        {
            Debug.LogError($"[QRScanner] Error parsing QR data: {e.Message}");
            UpdateStatusText("Failed to parse QR code");
        }
    }
    
    private void ApplyConnection(string ip, int port)
    {
        // Configure gyro sender
        scannerPanel.SetActive(false);
            gamePanel.SetActive(true);

        if (gyroSender)
        {
            gyroSender.SetServer(ip, port);
            gyroSender.StartSending();
        }
    }
    
    private void UpdateStatusText(string status)
    {
        if (statusText)
            statusText.text = status;
        Debug.Log($"[QRScanner] {status}");
    }

    void LateUpdate()
    {
#if !UNITY_EDITOR && (UNITY_ANDROID || UNITY_IOS)
        if (!autoFixRotation) return;
        if (webCamTexture != null && webCamTexture.isPlaying && cameraDisplay)
        {
            // Apply rotation (WebCamTexture gives clockwise degrees, RawImage rotates opposite)
            int rot = webCamTexture.videoRotationAngle;
            cameraDisplay.rectTransform.localEulerAngles = new Vector3(0, 0, -rot);

            // Handle vertical mirroring
            bool mirror = webCamTexture.videoVerticallyMirrored;
            var scale = cameraDisplay.rectTransform.localScale;
            scale.y = mirror ? -Mathf.Abs(scale.y) : Mathf.Abs(scale.y);
            cameraDisplay.rectTransform.localScale = scale;

            // Optional aspect correction (swap width/height if 90/270)
            if (rot == 90 || rot == 270)
            {
                // Maintain aspect by adjusting UV rect rather than layout if needed
                // (RawImage rotates content, so usually fine; this is a placeholder for future tweaks.)
            }
        }
#endif
    }
    
    private void UpdateButtons()
    {
        if (startScanButton)
            startScanButton.interactable = !isScanning;     
    }
    
    void OnDestroy()
    {
        StopScanning();
    }
    
    void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
            StopScanning();
    }
}

[System.Serializable]
public class QRConnectionData
{
    public string ip;
    public int port;
    public string name;
}