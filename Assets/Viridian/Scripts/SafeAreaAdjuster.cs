using UnityEngine;

/// <summary>
/// Adds padding to a RectTransform to respect the device safe area,
/// without changing anchors. Great for top bars / headers.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class SafeAreaPadding : MonoBehaviour
{
    [Header("Apply which insets?")]
    public bool padTop = true;
    public bool padBottom = false;   // keep false so bottom won't rise
    public bool padLeft = true;
    public bool padRight = true;

    [Tooltip("Extra padding multiplier (1 = exact safe area).")]
    public float multiplier = 1f;

    RectTransform rt;
    Rect lastSafeArea;
    Vector2Int lastScreenSize;

    void Awake()
    {
        rt = GetComponent<RectTransform>();
        Apply();
    }

    void OnEnable() => Apply();

    void Update()
    {
        // Re-apply on orientation / notch changes
        if (Screen.safeArea != lastSafeArea ||
            lastScreenSize.x != Screen.width || lastScreenSize.y != Screen.height)
        {
            Apply();
        }
    }

    void Apply()
    {
        var sa = Screen.safeArea;
        lastSafeArea = sa;
        lastScreenSize = new Vector2Int(Screen.width, Screen.height);

        // Compute insets (pixels) relative to the full screen
        float left   = sa.x;
        float bottom = sa.y;
        float right  = Screen.width  - (sa.x + sa.width);
        float top    = Screen.height - (sa.y + sa.height);

        left   *= multiplier;
        right  *= multiplier;
        top    *= multiplier;
        bottom *= multiplier;

        // Offsets are pixel paddings relative to anchors:
        //  offsetMin.x = left  padding    (positive)
        //  offsetMin.y = bottom padding   (positive)
        //  offsetMax.x = -right padding   (negative)
        //  offsetMax.y = -top padding     (negative)

        var offMin = rt.offsetMin;
        var offMax = rt.offsetMax;

        if (padLeft)   offMin.x = left;
        if (padBottom) offMin.y = bottom;      // false by default -> bottom stays put
        if (padRight)  offMax.x = -right;
        if (padTop)    offMax.y = -top;        // moves top down if needed

        rt.offsetMin = offMin;
        rt.offsetMax = offMax;
    }
}
