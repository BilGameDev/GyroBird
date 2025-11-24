// MIT License (c) 2025 Viridian Games
// Simple component to rotate a UI element or any Transform (DOTween version).

#if DOTWEEN
using DG.Tweening;
#endif
using UnityEngine;

[DisallowMultipleComponent]
public class UIRotator : MonoBehaviour
{
    [Header("Rotation Settings")]
    [Tooltip("Degrees per second (positive = clockwise).")]
    [SerializeField] private float speed = 90f;

    [Tooltip("If true, rotates back and forth instead of continuous spin.")]
    [SerializeField] private bool pingPong = false;

    [Tooltip("PingPong rotation angle (only used if pingPong = true).")]
    [SerializeField] private float swingAngle = 30f;

    [Header("Tween Settings")]
    [SerializeField] private float tweenDuration = 1.2f;     // used for ping-pong legs
#if DOTWEEN
    [SerializeField] private Ease tweenEase = Ease.InOutSine; // used for ping-pong legs
#endif
    [SerializeField] private bool useUnscaledTime = true;

    Quaternion baseLocalRot;

    void OnEnable()
    {
        baseLocalRot = transform.localRotation;
        StartRotation();
    }

    void OnDisable()
    {
#if DOTWEEN
        DOTween.Kill(this); // kill tweens started by this component
#endif
    }

    void StartRotation()
    {
#if DOTWEEN
        DOTween.Kill(this);

        if (!pingPong)
        {
            // Continuous spin:
            // Unity positive Z is CCW; we want positive speed = clockwise => rotate -360 per cycle when speed > 0
            float dir = speed >= 0f ? -1f : 1f;
            float spd = Mathf.Max(0.0001f, Mathf.Abs(speed)); // deg/sec
            float loopDuration = 360f / spd;

            // Use local rotation (better for UI / RectTransform)
            transform.DOLocalRotate(
                    new Vector3(0f, 0f, dir * 360f),
                    loopDuration,
                    RotateMode.FastBeyond360
                )
                .SetRelative(true)
                .SetEase(Ease.Linear)
                .SetLoops(-1, LoopType.Restart)
                .SetUpdate(useUnscaledTime)
                .SetId(this);
        }
        else
        {
            // Ping-pong around the current local rotation
            var left  = baseLocalRot * Quaternion.Euler(0f, 0f, -swingAngle);
            var right = baseLocalRot * Quaternion.Euler(0f, 0f,  swingAngle);

            // We’ll go: left -> right (double duration) -> base
            var seq = DOTween.Sequence().SetId(this).SetUpdate(useUnscaledTime);

            seq.Append(transform.DOLocalRotateQuaternion(left, tweenDuration)
                         .SetEase(tweenEase));

            seq.Append(transform.DOLocalRotateQuaternion(right, tweenDuration * 2f)
                         .SetEase(tweenEase));

            seq.Append(transform.DOLocalRotateQuaternion(baseLocalRot, tweenDuration)
                         .SetEase(tweenEase));

            seq.SetLoops(-1, LoopType.Restart);
        }
#endif
    }

    // Optional helpers if you want to switch mode at runtime:
    public void SetPingPong(bool on)
    {
        if (pingPong == on) return;
        pingPong = on;
        baseLocalRot = transform.localRotation;
        StartRotation();
    }

    public void SetSpeed(float degPerSec)
    {
        speed = degPerSec;
        if (!pingPong && isActiveAndEnabled) StartRotation();
    }
}
