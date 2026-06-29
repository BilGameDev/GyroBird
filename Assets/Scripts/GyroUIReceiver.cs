using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

public class GyroUIReceiver : MonoBehaviour
{
    [Header("Network")]
    public int listenPort = 7777;

    [Header("UI Crosshair")]
    public RectTransform crosshair;
    public Canvas canvas;

    [Header("Shooting Integration")]
    [SerializeField] private MouseShooter mouseShooter;

    [Header("Pointer Feel")]
    [Tooltip("Smoothing for position. Higher values feel more responsive.")]
    public float smoothPos = 18f;

    [Tooltip("Sensitivity multiplier after angle normalization.")]
    public float sensitivity = 1.35f;

    [Tooltip("Vertical range as a percentage of canvas height.")]
    public float verticalRange = 0.9f;

    [Tooltip("Horizontal range as a percentage of canvas width.")]
    public float horizontalRange = 0.9f;

    [Tooltip("Flip left/right aim if the phone feels mirrored.")]
    [SerializeField] private bool invertHorizontal = false;

    [Tooltip("Ignore tiny aiming changes below this angle.")]
    public float deadZone = 0.35f;

    [Tooltip("Phone angle that maps to the edge of the aimable area.")]
    public float maxTiltAngle = 28f;

    [Tooltip("Use a curve that gives extra precision near screen center.")]
    public bool useExponentialCurve = true;

    [Tooltip("Higher values give more precision near center and faster edge travel.")]
    public float curvePower = 1.55f;

    [Tooltip("Predict a little ahead using packet-to-packet motion to reduce perceived UDP latency.")]
    [SerializeField] private float latencyCompensation = 0.03f;

    [Tooltip("Maximum predicted screen movement per frame, in anchored canvas units.")]
    [SerializeField] private float maxPredictionStep = 90f;

    [Header("Diagnostics")]
    [SerializeField] private bool verboseLogging = false;
    [SerializeField] private int logEveryNPackets = 90;

    public event Action<VirtualPointerState> PointerUpdated;
    public event Action ShootRequested;
    public event Action CalibrateRequested;
    public event Action RestartRequested;

    public bool IsListening => isListening;
    public bool IsProcessing => isProcessing;
    public VirtualPointerState CurrentPointer { get; private set; } = VirtualPointerState.Center;

    private readonly object networkLock = new object();
    private readonly VirtualPointerAimModel aimModel = new VirtualPointerAimModel();

    private UdpClient udp;
    private IPEndPoint anyIP;
    private RectTransform canvasRect;
    private Vector2 currentVelocity;
    private Vector2 targetAnchoredPos;

    private Quaternion latestRotation = Quaternion.identity;
    private ushort latestAimSequence;
    private double latestPacketTime;
    private int packetCounter;

    private int pendingLegacyShootCount;
    private int pendingLegacyCalibrateCount;
    private int pendingLegacyRestartCount;
    private ushort pendingShootSequence;
    private ushort pendingCalibrateSequence;
    private ushort pendingRestartSequence;
    private ushort lastProcessedShootSequence;
    private ushort lastProcessedCalibrateSequence;
    private ushort lastProcessedRestartSequence;
    private bool pendingShoot;
    private bool pendingCalibrate;
    private bool pendingRestart;
    private bool pendingConnectionNotify;
    private string pendingRemoteIp;
    private bool isListening = true;
    private bool isProcessing = true;

    private static readonly double StopwatchFrequency = System.Diagnostics.Stopwatch.Frequency;

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

    void Update()
    {
        ProcessPendingCommands();

        if (!canvasRect || !isProcessing)
            return;

        Quaternion rotationSnapshot;
        ushort sequenceSnapshot;
        double packetTimeSnapshot;

        lock (networkLock)
        {
            rotationSnapshot = latestRotation;
            sequenceSnapshot = latestAimSequence;
            packetTimeSnapshot = latestPacketTime;
        }

        float packetAge = packetTimeSnapshot > 0 ? (float)Math.Max(0.0, NowSeconds() - packetTimeSnapshot) : float.PositiveInfinity;
        CurrentPointer = aimModel.Evaluate(
            rotationSnapshot,
            canvasRect.rect.size,
            sensitivity,
            horizontalRange,
            verticalRange,
            deadZone,
            maxTiltAngle,
            useExponentialCurve,
            curvePower,
            invertHorizontal,
            latencyCompensation,
            maxPredictionStep,
            packetAge,
            sequenceSnapshot);

        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, CurrentPointer.ScreenPosition, canvas ? canvas.worldCamera : null, out localPoint);
        targetAnchoredPos = localPoint;

        if (crosshair)
        {
            float smoothTime = 1f / Mathf.Max(1f, smoothPos);
            crosshair.anchoredPosition = Vector2.SmoothDamp(crosshair.anchoredPosition, targetAnchoredPos, ref currentVelocity, smoothTime);
        }

        PointerUpdated?.Invoke(CurrentPointer);

        if (verboseLogging && packetCounter > 0 && packetCounter % Mathf.Max(1, logEveryNPackets) == 0)
        {
            Debug.Log($"[GyroUIReceiver] aim=({CurrentPointer.NormalizedAim.x:F2},{CurrentPointer.NormalizedAim.y:F2}) pitch={CurrentPointer.PitchDegrees:F1} yaw={CurrentPointer.YawDegrees:F1} confidence={CurrentPointer.Confidence:F2}");
        }
    }

    public Vector3 GetAimScreenPosition()
    {
        if (crosshair)
            return crosshair.position;

        return new Vector3(CurrentPointer.ScreenPosition.x, CurrentPointer.ScreenPosition.y, 0f);
    }

    public void StopListening()
    {
        isListening = false;
        Debug.Log("[GyroUIReceiver] Stopped listening for controller data");
        ConnectionSubject.ForceDisconnect();
    }

    public void StartListening()
    {
        if (isListening)
            return;

        isListening = true;
        udp?.BeginReceive(ReceiveCallback, null);
        Debug.Log("[GyroUIReceiver] Started listening for controller data");
    }

    public void StopProcessing()
    {
        isProcessing = false;
        Debug.Log("[GyroUIReceiver] Stopped processing controller data");
    }

    public void StartProcessing()
    {
        isProcessing = true;
        Debug.Log("[GyroUIReceiver] Started processing controller data");
    }

    public void Recalibrate()
    {
        Quaternion rotationSnapshot;
        lock (networkLock)
        {
            rotationSnapshot = latestRotation;
        }

        aimModel.Calibrate(rotationSnapshot);
        currentVelocity = Vector2.zero;
        Debug.Log("[GyroUIReceiver] Recalibrated virtual pointer");
    }

    public void Disconnect()
    {
        StopListening();
        StopProcessing();

        lock (networkLock)
        {
            latestRotation = Quaternion.identity;
            latestAimSequence = 0;
            latestPacketTime = 0;
        }

        aimModel.Reset();
        currentVelocity = Vector2.zero;
        targetAnchoredPos = Vector2.zero;

        if (crosshair)
            crosshair.anchoredPosition = Vector2.zero;

        Debug.Log("[GyroUIReceiver] Disconnected");
        ConnectionSubject.ForceDisconnect();
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
        catch (ObjectDisposedException)
        {
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
        if (!ControllerPacket.TryParse(data, out ControllerPacket packet))
        {
            if (verboseLogging)
                Debug.LogWarning($"[GyroUIReceiver] Ignored malformed packet. Length={data?.Length ?? 0}");
            return;
        }

        if (packet.Type == ControllerPacketType.Aim || packet.Type == ControllerPacketType.LegacyAim)
        {
            lock (networkLock)
            {
                latestRotation = packet.Rotation;
                latestAimSequence = packet.Sequence;
                latestPacketTime = NowSeconds();
                pendingRemoteIp = anyIP?.Address.ToString();
                pendingConnectionNotify = true;
            }

            Interlocked.Increment(ref packetCounter);
            return;
        }

        QueueCommand(packet);
    }

    private void QueueCommand(ControllerPacket packet)
    {
        lock (networkLock)
        {
            pendingRemoteIp = anyIP?.Address.ToString();
            pendingConnectionNotify = true;
        }

        switch (packet.Type)
        {
            case ControllerPacketType.LegacyShoot:
                Interlocked.Increment(ref pendingLegacyShootCount);
                break;
            case ControllerPacketType.LegacyCalibrate:
                Interlocked.Increment(ref pendingLegacyCalibrateCount);
                break;
            case ControllerPacketType.LegacyRestart:
                Interlocked.Increment(ref pendingLegacyRestartCount);
                break;
            case ControllerPacketType.Shoot:
                lock (networkLock)
                {
                    pendingShootSequence = packet.Sequence;
                    pendingShoot = true;
                }
                break;
            case ControllerPacketType.Calibrate:
                lock (networkLock)
                {
                    pendingCalibrateSequence = packet.Sequence;
                    pendingCalibrate = true;
                }
                break;
            case ControllerPacketType.Restart:
                lock (networkLock)
                {
                    pendingRestartSequence = packet.Sequence;
                    pendingRestart = true;
                }
                break;
        }
    }

    private void ProcessPendingCommands()
    {
        bool shouldNotifyConnection = false;
        string remoteIp = null;
        bool shouldShoot = false;
        bool shouldCalibrate = false;
        bool shouldRestart = false;

        int legacyShoots = Interlocked.Exchange(ref pendingLegacyShootCount, 0);
        int legacyCalibrates = Interlocked.Exchange(ref pendingLegacyCalibrateCount, 0);
        int legacyRestarts = Interlocked.Exchange(ref pendingLegacyRestartCount, 0);

        lock (networkLock)
        {
            if (pendingConnectionNotify)
            {
                pendingConnectionNotify = false;
                shouldNotifyConnection = true;
                remoteIp = pendingRemoteIp;
                pendingRemoteIp = null;
            }

            if (pendingShoot && pendingShootSequence != lastProcessedShootSequence)
            {
                pendingShoot = false;
                lastProcessedShootSequence = pendingShootSequence;
                shouldShoot = true;
            }

            if (pendingCalibrate && pendingCalibrateSequence != lastProcessedCalibrateSequence)
            {
                pendingCalibrate = false;
                lastProcessedCalibrateSequence = pendingCalibrateSequence;
                shouldCalibrate = true;
            }

            if (pendingRestart && pendingRestartSequence != lastProcessedRestartSequence)
            {
                pendingRestart = false;
                lastProcessedRestartSequence = pendingRestartSequence;
                shouldRestart = true;
            }
        }

        if (shouldNotifyConnection)
            ConnectionSubject.NotifyPacketReceived(remoteIp);

        for (int i = 0; i < legacyShoots; i++)
            HandleShootCommand();

        if (legacyCalibrates > 0 || shouldCalibrate)
            HandleNetworkCalibrate();

        if (legacyRestarts > 0 || shouldRestart)
            HandleRestartCommand();

        if (shouldShoot)
            HandleShootCommand();
    }

    private void HandleShootCommand()
    {
        ShootRequested?.Invoke();
        if (mouseShooter)
            mouseShooter.GyroShoot();
    }

    private void HandleNetworkCalibrate()
    {
        CalibrateRequested?.Invoke();
        Recalibrate();
    }

    private void HandleRestartCommand()
    {
        RestartRequested?.Invoke();
        GameManager.RestartGame();
    }

    private static double NowSeconds()
    {
        return System.Diagnostics.Stopwatch.GetTimestamp() / StopwatchFrequency;
    }

    void OnDestroy()
    {
        isListening = false;
        isProcessing = false;
        ConnectionSubject.ForceDisconnect();
        udp?.Close();
    }
}
