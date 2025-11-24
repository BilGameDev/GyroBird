using UnityEngine;

/// <summary>
/// MonoBehaviour that updates the static ConnectionSubject.
/// Handles timeout checking in Update loop.
/// </summary>
public class ConnectionMonitor : MonoBehaviour
{
    [Tooltip("Seconds without packets before declaring disconnected")]
    [SerializeField] private float timeoutSeconds = 5f;

    void Awake()
    {
        ConnectionSubject.Initialize(timeoutSeconds);
    }

    void Update()
    {
        ConnectionSubject.Update();
    }
}
