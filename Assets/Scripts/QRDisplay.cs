using UnityEngine;
using UnityEngine.UI;
using System.Net;
using System.Net.Sockets;

#if UNITY_EDITOR || UNITY_STANDALONE
using ZXing;
using ZXing.QrCode;
#endif

public class QRDisplay : MonoBehaviour
{
    [SerializeField] private RawImage targetImage;
    [SerializeField] private int qrWidth = 256;
    [SerializeField] private int qrHeight = 256;
    [SerializeField] private int margin = 2;

    [Header("Network Integration")]
    [SerializeField] private bool autoGenerateConnection = false;
    [SerializeField] private int networkPort = 7777;
    [SerializeField] private string connectionName = "GyroGame";
    [SerializeField] private GyroUIReceiver gyroReceiver; // to get port automatically

    public enum IPSelectionStrategy { AutoFirst, PreferWireless, PreferEthernet, ManualOverride }
    [Header("IP Selection")]
    [SerializeField] private IPSelectionStrategy ipStrategy = IPSelectionStrategy.AutoFirst;
    [SerializeField] private string manualIpOverride = ""; // used if ManualOverride
    [SerializeField] private bool listCandidateIPsInLog = true;
    private string chosenIp = ""; // cached after selection

    [Header("Connection Behavior")]
    [Tooltip("Automatically hide this QR display once a remote device connects.")]
    [SerializeField] private bool hideOnConnect = true;
    [Tooltip("Optional: Reactivate if disconnected (requires external re-enable if GameObject disabled).")]
    [SerializeField] private bool showOnDisconnect = false;
    [Header("Diagnostics")]
    [SerializeField] private bool verboseLogging = true; // enable detailed logs for troubleshooting


#if UNITY_EDITOR || UNITY_STANDALONE
    private BarcodeWriter barcodeWriter;
#endif
    private Texture2D qrTexture;

    void Start()
    {
#if UNITY_EDITOR || UNITY_STANDALONE
        // Initialize ZXing barcode writer
        barcodeWriter = new BarcodeWriter
        {
            Format = BarcodeFormat.QR_CODE,
            Options = new ZXing.QrCode.QrCodeEncodingOptions
            {
                Height = qrHeight,
                Width = qrWidth,
                Margin = margin
            }
        };
#endif

        if (autoGenerateConnection)
        {
            GenerateConnectionQR();
        }
        else
        {
            // Example usage
            SetQrText("https://example.com");
        }

        ConnectionSubject.OnConnected += HandleConnected;
        ConnectionSubject.OnConnectedInfo += HandleConnectedInfo;
        ConnectionSubject.OnDisconnected += HandleDisconnected;

        // If already connected (scene reloaded late), apply state
        if (ConnectionSubject.IsConnected)
            HandleConnected();

        if (verboseLogging)
            Debug.Log("[QRDisplay] Subscribed to ConnectionSubject events (OnEnable). Connected=" + ConnectionSubject.IsConnected);

    }

    public void SetQrText(string text)
    {
        if (targetImage == null)
        {
            Debug.LogError("[QRDisplay] targetImage not set.");
            return;
        }

#if UNITY_EDITOR || UNITY_STANDALONE
        try
        {
            // Generate QR code texture using ZXing
            Color32[] pixels = barcodeWriter.Write(text);

            // Create or reuse texture
            if (qrTexture == null || qrTexture.width != qrWidth || qrTexture.height != qrHeight)
            {
                if (qrTexture != null)
                    DestroyImmediate(qrTexture);

                qrTexture = new Texture2D(qrWidth, qrHeight, TextureFormat.RGB24, false)
                {
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp
                };
            }

            qrTexture.SetPixels32(pixels);
            qrTexture.Apply();

            // Assign to UI
            targetImage.texture = qrTexture;
            targetImage.SetNativeSize(); // optional

            Debug.Log($"[QRDisplay] Generated QR code: {text}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[QRDisplay] Error generating QR code: {e.Message}");
        }
#else
        Debug.LogWarning("[QRDisplay] QR generation only supported on PC/Editor platforms");
        // For mobile, you could show a placeholder or message
        if (targetImage)
        {
            // Create simple placeholder texture
            var placeholderTex = new Texture2D(qrWidth, qrHeight, TextureFormat.RGB24, false);
            Color[] colors = new Color[qrWidth * qrHeight];
            for (int i = 0; i < colors.Length; i++)
                colors[i] = Color.gray;
            placeholderTex.SetPixels(colors);
            placeholderTex.Apply();
            targetImage.texture = placeholderTex;
        }
#endif
    }

    public void GenerateConnectionQR()
    {
        string connectionData = GenerateConnectionData();
        SetQrText(connectionData);
        Debug.Log($"[QRDisplay] Generated connection QR: {connectionData}");
    }

    private string GenerateConnectionData()
    {
        string ip = GetLocalIPAddress();
        int port = GetNetworkPort();

        // Generate JSON format for compatibility with QRScanner
        var connectionData = new QRConnectionData
        {
            ip = ip,
            port = port,
            name = connectionName
        };

        return JsonUtility.ToJson(connectionData);
    }

    private int GetNetworkPort()
    {
        if (gyroReceiver)
            return gyroReceiver.listenPort;
        return networkPort;
    }

    private string GetLocalIPAddress()
    {
        if (!string.IsNullOrEmpty(chosenIp)) return chosenIp; // already resolved this session

        // Manual override pathway
        if (ipStrategy == IPSelectionStrategy.ManualOverride && !string.IsNullOrEmpty(manualIpOverride))
        {
            chosenIp = manualIpOverride;
            if (verboseLogging) Debug.Log("[QRDisplay] Using manual IP override: " + chosenIp);
            return chosenIp;
        }

        var candidates = new System.Collections.Generic.List<(string ip, System.Net.NetworkInformation.NetworkInterfaceType type)>();
        try
        {
            foreach (var ni in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up) continue;
                if (ni.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Loopback) continue;
                var props = ni.GetIPProperties();
                foreach (var ua in props.UnicastAddresses)
                {
                    if (ua.Address.AddressFamily == AddressFamily.InterNetwork)
                    {
                        string ip = ua.Address.ToString();
                        candidates.Add((ip, ni.NetworkInterfaceType));
                    }
                }
            }
        }
        catch (System.Exception ex)
        {
            if (verboseLogging) Debug.LogWarning("[QRDisplay] NetworkInterface enumeration failed: " + ex.Message);
        }

        if (listCandidateIPsInLog && verboseLogging)
        {
            if (candidates.Count == 0)
                Debug.Log("[QRDisplay] No IPv4 candidates found; will fallback.");
            else
            {
                Debug.Log("[QRDisplay] IPv4 candidates: " + string.Join(", ", candidates.ConvertAll(c => c.ip + "(" + c.type + ")")));
            }
        }

        // Strategy selection
        if (candidates.Count > 0)
        {
            System.Func<System.Net.NetworkInformation.NetworkInterfaceType, bool> isWireless = t => t == System.Net.NetworkInformation.NetworkInterfaceType.Wireless80211;
            System.Func<System.Net.NetworkInformation.NetworkInterfaceType, bool> isEthernet = t => t == System.Net.NetworkInformation.NetworkInterfaceType.Ethernet || t == System.Net.NetworkInformation.NetworkInterfaceType.GigabitEthernet || t == System.Net.NetworkInformation.NetworkInterfaceType.FastEthernetFx || t == System.Net.NetworkInformation.NetworkInterfaceType.FastEthernetT;
            string selected = null;

            switch (ipStrategy)
            {
                case IPSelectionStrategy.PreferWireless:
                    selected = candidates.Find(c => isWireless(c.type)).ip;
                    if (selected == null) selected = candidates[0].ip;
                    break;
                case IPSelectionStrategy.PreferEthernet:
                    selected = candidates.Find(c => isEthernet(c.type)).ip;
                    if (selected == null) selected = candidates[0].ip;
                    break;
                case IPSelectionStrategy.AutoFirst:
                default:
                    selected = candidates[0].ip;
                    break;
            }

            chosenIp = selected;
            if (verboseLogging) Debug.Log("[QRDisplay] Selected IP (" + ipStrategy + "): " + chosenIp);
            return chosenIp;
        }

        // Fallback method (existing approach) if no candidates
        try
        {
            using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
            {
                socket.Connect("8.8.8.8", 65530);
                var endPoint = socket.LocalEndPoint as IPEndPoint;
                chosenIp = endPoint?.Address.ToString() ?? "127.0.0.1";
                if (verboseLogging) Debug.Log("[QRDisplay] Fallback IP: " + chosenIp);
                return chosenIp;
            }
        }
        catch
        {
            chosenIp = "127.0.0.1";
            if (verboseLogging) Debug.Log("[QRDisplay] Fallback to loopback IP: " + chosenIp);
            return chosenIp;
        }
    }

    // Allow runtime switching (e.g., developer debug UI)
    public void SetIPStrategy(IPSelectionStrategy strategy, string manualIp = null)
    {
        ipStrategy = strategy;
        if (strategy == IPSelectionStrategy.ManualOverride && !string.IsNullOrEmpty(manualIp))
            manualIpOverride = manualIp;
        chosenIp = string.Empty; // force recompute
        if (verboseLogging) Debug.Log("[QRDisplay] IP strategy changed to " + ipStrategy + (manualIpOverride != null ? " manual=" + manualIpOverride : ""));
        RefreshConnectionQR();
    }

    public void RefreshConnectionQR()
    {
        if (autoGenerateConnection || targetImage.texture != null)
        {
            GenerateConnectionQR();
        }
    }

    void OnDestroy()
    {
        if (qrTexture)
            DestroyImmediate(qrTexture);

        ConnectionSubject.OnConnected -= HandleConnected;
        ConnectionSubject.OnConnectedInfo -= HandleConnectedInfo;
        ConnectionSubject.OnDisconnected -= HandleDisconnected;
    }

    private void HandleConnected()
    {
        if (hideOnConnect)
        {
            // Disable the entire GameObject (simplest "close" behavior)
            if (verboseLogging)
                Debug.Log("[QRDisplay] HandleConnected invoked. Hiding QRDisplay GameObject.");

            gameObject.SetActive(false); // will also trigger OnDisable
        }
        else if (verboseLogging)
        {
            Debug.Log("[QRDisplay] HandleConnected invoked but hideOnConnect is false.");
        }
    }

    private void HandleConnectedInfo(string ip)
    {
        if (verboseLogging)
        {
            Debug.Log("[QRDisplay] HandleConnectedInfo remote IP=" + ip);
        }
    }

    private void HandleDisconnected()
    {
        if (showOnDisconnect && hideOnConnect)
        {
            // Re-show QR to allow reconnection (only works if something else re-enables first)
            if (!gameObject.activeSelf)
            {
                // If this component is disabled we cannot run this; assume external manager may re-enable
                // Log for clarity.
                Debug.Log("[QRDisplay] Disconnected while hidden. Re-enable GameObject externally to show QR again.");
            }
            else
            {
                GenerateConnectionQR();
                if (targetImage) targetImage.gameObject.SetActive(true);
            }
        }
        else if (verboseLogging)
        {
            Debug.Log("[QRDisplay] HandleDisconnected invoked. showOnDisconnect=" + showOnDisconnect);
        }
    }
}
