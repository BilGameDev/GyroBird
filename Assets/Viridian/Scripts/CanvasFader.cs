// MIT License (c) 2025 Viridian Games
// CanvasFader (DOTween version, channels: Fade | Scale)

using System.Collections.Generic;
#if DOTWEEN
using DG.Tweening;
#endif
using UnityEngine;
using UnityEngine.UI;

[System.Flags]
public enum PlayChannels
{
    None  = 0,
    Fade  = 1 << 0,
    Scale = 1 << 1,
    Both  = Fade | Scale
}

public class CanvasFader : MonoBehaviour
{
    
    [Header("Targets")]
    [Tooltip("CanvasGroups to tween in order. If empty, will search in children (including inactive).")]
    [SerializeField] private List<CanvasGroup> targets = new List<CanvasGroup>();

    [Header("Channels")]
    [SerializeField] private PlayChannels channelsIn  = PlayChannels.Both;
    [SerializeField] private PlayChannels channelsOut = PlayChannels.Both;

    [Header("Timing")]
    [SerializeField] private float startDelay   = 0f;
    [SerializeField] private float perItemDelay = 0.15f;
    [SerializeField] private float inDuration   = 0.5f;
    [SerializeField] private float outDuration  = 0.35f;
    [SerializeField] private bool  useUnscaled  = true;

#if DOTWEEN
    [Header("Easing")]
    [SerializeField] private Ease fadeInEase  = Ease.OutCubic;
    [SerializeField] private Ease fadeOutEase = Ease.InCubic;
    [SerializeField] private Ease scaleInEase  = Ease.OutBack;
    [SerializeField] private Ease scaleOutEase = Ease.InCubic;
#endif
    [Header("Scale")]
    [Tooltip("Scale at the beginning of PlayIn (tweens to 1). Used only if Scale channel is active.")]
    [SerializeField] private float inStartScale = 0.92f;
    [Tooltip("Scale at the end of PlayOut (tweens from 1). Used only if Scale channel is active.")]
    [SerializeField] private float outEndScale  = 0.92f;

    [Header("Behavior")]
    [SerializeField] private bool runOnStart = true;
    [SerializeField] private bool startHidden = true;           // apply initial hidden state on Awake
    [SerializeField] private bool enableInteractAfter = true;   // enable raycasts/interactable after PlayIn
    [SerializeField] private bool disableGroupElementAfter = false; // deactivate GO after PlayOut completes

    static readonly List<CanvasGroup> _buffer = new List<CanvasGroup>(64);
#if DOTWEEN
    readonly List<Sequence> _running = new List<Sequence>(64);
#endif

    void Awake()
    {
        // Auto-collect
        if (targets.Count == 0)
        {
            GetComponentsInChildren(true, _buffer);
            targets.AddRange(_buffer);
            _buffer.Clear();
        }

        // Ensure they are CanvasGroups (if user dragged plain GameObjects)
        for (int i = 0; i < targets.Count; i++)
        {
            var cg = targets[i];
            if (!cg) continue;
            var real = cg.GetComponent<CanvasGroup>() ?? cg.gameObject.AddComponent<CanvasGroup>();
            targets[i] = real;
        }

        if (startHidden) HideImmediate();
    }

    void Start()
    {
        if (runOnStart) PlayIn();
    }

    void OnDisable()
    {
#if DOTWEEN
        // Kill only tweens created by this component
        for (int i = 0; i < _running.Count; i++)
            _running[i]?.Kill();
        _running.Clear();
        DOTween.Kill(this); // extra safety if any tween used SetId(this)
#endif
    }

    // ----- Immediate states -----

    public void HideImmediate()
    {
        foreach (var cg in targets)
        {
            if (!cg) continue;

            // Fade channel
            if ((channelsIn & PlayChannels.Fade) != 0 || (channelsOut & PlayChannels.Fade) != 0)
            {
                cg.alpha = 0f;
                cg.blocksRaycasts = false;
                cg.interactable = false;
            }

            // Scale channel
            if ((channelsIn & PlayChannels.Scale) != 0 || (channelsOut & PlayChannels.Scale) != 0)
            {
                if (cg.transform is RectTransform rt)
                    rt.localScale = Vector3.one * inStartScale;
            }
        }
    }

    public void ShowImmediate()
    {
        foreach (var cg in targets)
        {
            if (!cg) continue;

            if ((channelsIn & PlayChannels.Fade) != 0 || (channelsOut & PlayChannels.Fade) != 0)
                cg.alpha = 1f;

            if ((channelsIn & PlayChannels.Scale) != 0 || (channelsOut & PlayChannels.Scale) != 0)
            {
                if (cg.transform is RectTransform rt)
                    rt.localScale = Vector3.one;
            }

            cg.blocksRaycasts = enableInteractAfter;
            cg.interactable   = enableInteractAfter;
        }
    }

    // ----- Play In -----

    public void PlayIn()
    {
        StopAll();

        for (int i = 0; i < targets.Count; i++)
        {
            var cg = targets[i];
            if (!cg) continue;

            if (!cg.gameObject.activeSelf) cg.gameObject.SetActive(true);

            // Start state
            cg.blocksRaycasts = false;
            cg.interactable   = false;

            if ((channelsIn & PlayChannels.Fade) != 0)
                cg.alpha = Mathf.Clamp01(cg.alpha); // respect existing if already 0/partial; otherwise fine

            if ((channelsIn & PlayChannels.Scale) != 0 && cg.transform is RectTransform rtIn)
                rtIn.localScale = Vector3.one * inStartScale;

#if DOTWEEN
            float delay = startDelay + i * perItemDelay;

            var seq = DOTween.Sequence().SetId(this).SetUpdate(useUnscaled).SetDelay(delay);

            if ((channelsIn & PlayChannels.Fade) != 0)
                seq.Join(cg.DOFade(1f, inDuration).SetEase(fadeInEase));

            if ((channelsIn & PlayChannels.Scale) != 0 && cg.transform is RectTransform rtIn2)
                seq.Join(rtIn2.DOScale(1f, inDuration).SetEase(scaleInEase));

            seq.OnComplete(() =>
            {
                if (!cg) return;
                cg.blocksRaycasts = enableInteractAfter;
                cg.interactable   = enableInteractAfter;
            });

            _running.Add(seq);
#else
            // Immediate fallback
            if ((channelsIn & PlayChannels.Fade) != 0)
                cg.alpha = 1f;
            if ((channelsIn & PlayChannels.Scale) != 0 && cg.transform is RectTransform rtIn2)
                rtIn2.localScale = Vector3.one;
            cg.blocksRaycasts = enableInteractAfter;
            cg.interactable   = enableInteractAfter;
#endif
        }
    }

    // ----- Play Out -----

    public void PlayOut()
    {
        StopAll();

        for (int i = 0; i < targets.Count; i++)
        {
            var cg = targets[i];
            if (!cg) continue;

            cg.blocksRaycasts = false;
            cg.interactable   = false;

#if DOTWEEN
            float delay = startDelay + i * perItemDelay;

            var seq = DOTween.Sequence().SetId(this).SetUpdate(useUnscaled).SetDelay(delay);

            if ((channelsOut & PlayChannels.Fade) != 0)
                seq.Join(cg.DOFade(0f, outDuration).SetEase(fadeOutEase));

            if ((channelsOut & PlayChannels.Scale) != 0 && cg.transform is RectTransform rtOut)
                seq.Join(rtOut.DOScale(outEndScale, outDuration).SetEase(scaleOutEase));

            if (disableGroupElementAfter)
                seq.OnComplete(() => { if (cg) cg.gameObject.SetActive(false); });

            _running.Add(seq);
#else
            // Immediate fallback
            if ((channelsOut & PlayChannels.Fade) != 0)
                cg.alpha = 0f;
            if ((channelsOut & PlayChannels.Scale) != 0 && cg.transform is RectTransform rtOut)
                rtOut.localScale = Vector3.one * outEndScale;
            if (disableGroupElementAfter)
                cg.gameObject.SetActive(false);
#endif
        }
    }

    // ----- Controls -----

    public void StopAll()
    {
#if DOTWEEN
        for (int i = 0; i < _running.Count; i++)
            _running[i]?.Kill();
        _running.Clear();
        DOTween.Kill(this);
#endif
    }

    // Convenience setters if you want to change channels at runtime
    public void SetInChannels(PlayChannels ch)  => channelsIn = ch;
    public void SetOutChannels(PlayChannels ch) => channelsOut = ch;
}
