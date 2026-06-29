using UnityEngine;

public sealed class VirtualPointerAimModel
{
    private Quaternion calibrationOffset = Quaternion.identity;
    private Vector2 previousTarget;
    private float previousTargetTime;

    public void Calibrate(Quaternion rotation)
    {
        calibrationOffset = rotation;
        previousTarget = Vector2.zero;
        previousTargetTime = 0f;
    }

    public void Reset()
    {
        calibrationOffset = Quaternion.identity;
        previousTarget = Vector2.zero;
        previousTargetTime = 0f;
    }

    public VirtualPointerState Evaluate(
        Quaternion rotation,
        Vector2 canvasSize,
        float sensitivity,
        float horizontalRange,
        float verticalRange,
        float deadZoneDegrees,
        float maxAimAngle,
        bool useCurve,
        float curvePower,
        bool invertHorizontal,
        float latencyCompensation,
        float maxPredictionStep,
        float packetAge,
        ushort sequence)
    {
        Quaternion calibratedRotation = Quaternion.Inverse(calibrationOffset) * rotation;

        Vector3 screenNormal = calibratedRotation * Vector3.forward;
        Vector3 phoneNose = calibratedRotation * Vector3.up;
        float pitch = -Mathf.Asin(Mathf.Clamp(screenNormal.y, -1f, 1f)) * Mathf.Rad2Deg;
        float yaw = Mathf.Asin(Mathf.Clamp(phoneNose.x, -1f, 1f)) * Mathf.Rad2Deg;

        if (invertHorizontal)
            yaw = -yaw;

        if (Mathf.Abs(pitch) < deadZoneDegrees) pitch = 0f;
        if (Mathf.Abs(yaw) < deadZoneDegrees) yaw = 0f;

        maxAimAngle = Mathf.Max(1f, maxAimAngle);
        pitch = Mathf.Clamp(pitch, -maxAimAngle, maxAimAngle);
        yaw = Mathf.Clamp(yaw, -maxAimAngle, maxAimAngle);

        float normalizedPitch = pitch / maxAimAngle;
        float normalizedYaw = yaw / maxAimAngle;

        if (useCurve)
        {
            float power = Mathf.Max(1f, curvePower);
            normalizedPitch = Mathf.Sign(normalizedPitch) * Mathf.Pow(Mathf.Abs(normalizedPitch), power);
            normalizedYaw = Mathf.Sign(normalizedYaw) * Mathf.Pow(Mathf.Abs(normalizedYaw), power);
        }

        normalizedPitch = Mathf.Clamp(normalizedPitch * sensitivity, -1f, 1f);
        normalizedYaw = Mathf.Clamp(normalizedYaw * sensitivity, -1f, 1f);

        float halfWidth = canvasSize.x * 0.5f;
        float halfHeight = canvasSize.y * 0.5f;
        Vector2 target = new Vector2(
            normalizedYaw * halfWidth * horizontalRange,
            -normalizedPitch * halfHeight * verticalRange);

        target = ApplyLatencyCompensation(target, latencyCompensation, maxPredictionStep);

        Vector2 screenPosition = new Vector2(
            Screen.width * 0.5f + target.x,
            Screen.height * 0.5f + target.y);

        float confidence = Mathf.Clamp01(1f - (packetAge / 0.25f));
        return new VirtualPointerState(
            new Vector2(normalizedYaw, normalizedPitch),
            screenPosition,
            pitch,
            yaw,
            confidence,
            packetAge,
            sequence);
    }

    private Vector2 ApplyLatencyCompensation(Vector2 rawTarget, float latencyCompensation, float maxPredictionStep)
    {
        if (latencyCompensation <= 0f)
        {
            previousTarget = rawTarget;
            previousTargetTime = Time.unscaledTime;
            return rawTarget;
        }

        float now = Time.unscaledTime;
        float deltaTime = previousTargetTime > 0f ? now - previousTargetTime : 0f;
        Vector2 predictedTarget = rawTarget;

        if (deltaTime > Mathf.Epsilon)
        {
            Vector2 velocity = (rawTarget - previousTarget) / deltaTime;
            Vector2 prediction = Vector2.ClampMagnitude(velocity * latencyCompensation, maxPredictionStep);
            predictedTarget += prediction;
        }

        previousTarget = rawTarget;
        previousTargetTime = now;
        return predictedTarget;
    }

}
