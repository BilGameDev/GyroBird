using UnityEngine;

public readonly struct VirtualPointerState
{
    public readonly Vector2 NormalizedAim;
    public readonly Vector2 ScreenPosition;
    public readonly float PitchDegrees;
    public readonly float YawDegrees;
    public readonly float Confidence;
    public readonly float PacketAge;
    public readonly ushort Sequence;

    public VirtualPointerState(
        Vector2 normalizedAim,
        Vector2 screenPosition,
        float pitchDegrees,
        float yawDegrees,
        float confidence,
        float packetAge,
        ushort sequence)
    {
        NormalizedAim = normalizedAim;
        ScreenPosition = screenPosition;
        PitchDegrees = pitchDegrees;
        YawDegrees = yawDegrees;
        Confidence = confidence;
        PacketAge = packetAge;
        Sequence = sequence;
    }

    public static VirtualPointerState Center =>
        new VirtualPointerState(Vector2.zero, new Vector2(Screen.width * 0.5f, Screen.height * 0.5f), 0f, 0f, 0f, float.PositiveInfinity, 0);
}
