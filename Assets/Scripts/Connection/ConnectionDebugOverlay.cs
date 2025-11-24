using UnityEngine;
using UnityEngine.UI;

public class ConnectionDebugOverlay : MonoBehaviour
{
    [SerializeField] private Text statusText;
    [SerializeField] private bool autoHideOnConnect = false;
    [SerializeField] private float hideDelay = 1f;
    private float connectedTime = -1f;

    void Update()
    {
        if (statusText)
        {
            statusText.text = ConnectionSubject.IsConnected ?
                ($"Connected IP: {ConnectionSubject.LastRemoteIP}\nLastPacket: {Mathf.Round(Time.time - ConnectionSubject.GetLastPacketAge())}s ago") :
                "Waiting for packets...";
        }
        if (autoHideOnConnect && ConnectionSubject.IsConnected)
        {
            if (connectedTime < 0f) connectedTime = Time.time;
            if (Time.time - connectedTime > hideDelay)
                gameObject.SetActive(false);
        }
    }
}
