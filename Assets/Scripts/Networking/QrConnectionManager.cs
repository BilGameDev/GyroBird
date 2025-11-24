using UnityEngine;

/// <summary>
/// Applies QR scanned payload to configure network connection (e.g., GyroUdpSender).
/// Payload formats supported:
///   ip:port
///   gyro://ip:port
///   {"ip":"x.x.x.x","port":1234}
/// Single Responsibility: parsing + applying settings.
/// Open for extension: add more parsers without modifying existing code via strategy list.
/// </summary>
public class QrConnectionManager : MonoBehaviour
{
    [SerializeField] private GyroUdpSender gyroSender;

    public void OnQrScanned(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            Debug.LogWarning("[QrConnectionManager] Empty payload");
            return;
        }

        if (TryParseSimple(payload, out string ip, out int port) ||
            TryParseScheme(payload, out ip, out port) ||
            TryParseJson(payload, out ip, out port))
        {
            Apply(ip, port);
        }
        else
        {
            Debug.LogWarning($"[QrConnectionManager] Unrecognized payload: {payload}");
        }
    }

    private void Apply(string ip, int port)
    {
        if (!gyroSender)
        {
            Debug.LogWarning("[QrConnectionManager] GyroUdpSender not assigned");
            return;
        }
        gyroSender.SetServer(ip, port);
        Debug.Log($"[QrConnectionManager] Applied server {ip}:{port}");
    }

    private bool TryParseSimple(string payload, out string ip, out int port)
    {
        ip = null; port = 0;
        var parts = payload.Split(':');
        if (parts.Length == 2 && int.TryParse(parts[1], out int p))
        {
            ip = parts[0]; port = p; return true;
        }
        return false;
    }
    private bool TryParseScheme(string payload, out string ip, out int port)
    {
        ip = null; port = 0;
        if (payload.StartsWith("gyro://"))
        {
            var stripped = payload.Substring("gyro://".Length);
            return TryParseSimple(stripped, out ip, out port);
        }
        return false;
    }
    private bool TryParseJson(string payload, out string ip, out int port)
    {
        ip = null; port = 0;
        if (payload.StartsWith("{") && payload.EndsWith("}"))
        {
            // Very naive JSON parsing to avoid dependency
            // Expect: {"ip":"x.x.x.x","port":1234}
            try
            {
                var cleaned = payload.Replace(" ", "");
                int ipIndex = cleaned.IndexOf("\"ip\":\"");
                int portIndex = cleaned.IndexOf("\"port\":");
                if (ipIndex >= 0 && portIndex >= 0)
                {
                    int ipStart = ipIndex + 6; // "ip":"
                    int ipEnd = cleaned.IndexOf("\"", ipStart);
                    ip = cleaned.Substring(ipStart, ipEnd - ipStart);
                    int portStart = portIndex + 7;
                    int portEnd = cleaned.IndexOf("}", portStart);
                    string portStr = cleaned.Substring(portStart, portEnd - portStart).Trim(',', '"');
                    if (int.TryParse(portStr, out int p))
                    {
                        port = p; return true;
                    }
                }
            }
            catch { }
        }
        return false;
    }
}
