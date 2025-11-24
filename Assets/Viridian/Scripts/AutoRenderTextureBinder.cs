using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public class AutoRenderTextureBinder : MonoBehaviour
{
    [Header("Camera Output")]
    [SerializeField] bool cameraChild;
    [SerializeField] private Camera targetCamera;

    [Header("Optional target RawImage (auto if on same GO)")]
    [SerializeField] private RawImage targetRawImage;

    [Header("RenderTexture Settings")]
    [SerializeField] private RenderTextureFormat format = RenderTextureFormat.ARGB32;
    [SerializeField] private int depthBufferBits = 16;
    [SerializeField] private FilterMode filterMode = FilterMode.Bilinear;
    [SerializeField] private int antiAliasing = 1;   // 1,2,4,8 (runtime only)

    [Header("Behavior")]
    [Tooltip("Rebuild the RT whenever this RectTransform changes size (or Canvas scale changes).")]
    [SerializeField] private bool autoResize = true;

    RenderTexture rt;
    RectTransform rtTransform;
    Canvas canvas;
    Vector2Int lastPixelSize;
    float lastScaleFactor;

    void Awake()
    {
        rtTransform = GetComponent<RectTransform>();
        canvas = GetComponentInParent<Canvas>();
        targetRawImage = GetComponent<RawImage>();
        targetCamera = cameraChild ? Camera.main.transform.GetChild(0).GetComponent<Camera>() : Camera.main;
        if (!targetRawImage) targetRawImage = GetComponent<RawImage>();
        RebuildRenderTexture();

        ApplyBindings();
    }


    void OnDestroy()
    {
        ReleaseRT();

        // Detach camera output to avoid writing into a discarded RT
        if (targetCamera && targetCamera.targetTexture == rt) targetCamera.targetTexture = null;
    }

    void OnRectTransformDimensionsChange()
    {
        if (!autoResize) return;
        TryResizeIfNeeded();
    }

    void Update()
    {
#if UNITY_EDITOR
        // In Edit mode or when Canvas scale changes, keep size correct
        if (autoResize) TryResizeIfNeeded();
#endif
    }

    // --- public API ---
    public void RebuildRenderTexture()
    {
        ReleaseRT();

        var size = GetPixelSize();
        if (size.x <= 0 || size.y <= 0) return;

        rt = new RenderTexture(size.x, size.y, depthBufferBits, format)
        {
            antiAliasing = Mathf.Max(1, antiAliasing),
            filterMode = filterMode,
            useMipMap = false,
            autoGenerateMips = false,
            wrapMode = TextureWrapMode.Clamp,
            name = $"AutoRT_{gameObject.name}_{size.x}x{size.y}"
        };
        rt.Create();

        lastPixelSize = size;
        lastScaleFactor = canvas ? canvas.scaleFactor : 1f;

        ApplyBindings();
    }

    // --- internals ---
    void ApplyBindings()
    {
        if (targetRawImage) targetRawImage.texture = rt;
        if (targetCamera)
        {
            targetCamera.targetTexture = rt;
            if (rt && rt.height != 0) targetCamera.aspect = (float)rt.width / rt.height;

            targetCamera.clearFlags = CameraClearFlags.Depth; // or CameraClearFlags.Nothing
            targetCamera.backgroundColor = Color.clear;
        }
    }

    void ReleaseRT()
    {
        if (rt)
        {
            if (targetRawImage && targetRawImage.texture == rt) targetRawImage.texture = null;
            if (targetCamera && targetCamera.targetTexture == rt) targetCamera.targetTexture = null;

#if UNITY_EDITOR
            if (!Application.isPlaying) DestroyImmediate(rt);
            else Destroy(rt);
#else
            Destroy(rt);
#endif
            rt = null;
        }
    }

    void TryResizeIfNeeded()
    {
        var size = GetPixelSize();
        var scale = canvas ? canvas.scaleFactor : 1f;

        if (size != lastPixelSize || !Mathf.Approximately(scale, lastScaleFactor))
        {
            RebuildRenderTexture();
        }
    }

    Vector2Int GetPixelSize()
    {
        // For UI, convert RectTransform size (in units) to pixels using Canvas scaleFactor.
        var rect = rtTransform.rect;
        float scale = canvas ? canvas.scaleFactor : 1f;

        int w = Mathf.Max(0, Mathf.RoundToInt(rect.width * scale));
        int h = Mathf.Max(0, Mathf.RoundToInt(rect.height * scale));

        // Fallback: if attached to a non-UI object,
        // use world-space bounds projected to screen as a rough size.
        if ((w == 0 || h == 0) && targetCamera != null)
        {
            var corners = new Vector3[4];
            rtTransform.GetWorldCorners(corners);
            var a = RectTransformUtility.WorldToScreenPoint(targetCamera, corners[0]);
            var b = RectTransformUtility.WorldToScreenPoint(targetCamera, corners[2]);
            w = Mathf.Max(w, Mathf.RoundToInt(Mathf.Abs(b.x - a.x)));
            h = Mathf.Max(h, Mathf.RoundToInt(Mathf.Abs(b.y - a.y)));
        }

        // Ensure at least 1×1 to avoid invalid RTs.
        w = Mathf.Max(1, w);
        h = Mathf.Max(1, h);

        return new Vector2Int(w, h);
    }
}
