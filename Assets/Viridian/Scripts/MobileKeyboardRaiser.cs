using System.Collections.Generic;
using UnityEngine;
#if UNITY_IOS || UNITY_ANDROID
using UnityEngine.EventSystems;
#endif
using TMPro;
using UnityEngine.UI;
using UnityEngine.UIElements;

#if UNITY_IOS || UNITY_ANDROID
public class MobileKeyboardRaiser : MonoBehaviour
{
    [Header("General")]
    [Tooltip("Extra space (in px) above the keyboard to keep the caret area comfortably visible.")]
    public float topMargin = 96f;

    [Tooltip("If the platform doesn't report a keyboard area, use this fraction of screen height as a fallback (Android on some devices).")]
    [Range(0.25f, 0.6f)]
    public float fallbackKeyboardPct = 0.4f;

    [Tooltip("Enable debug logging to diagnose keyboard positioning issues.")]
    public bool debugLog = false;

    [Header("UGUI / TMP Settings")]
    [Tooltip("The RectTransform that should move (often your whole panel or a container under the Canvas).")]
    public RectTransform moveTarget;

    [Tooltip("Leave empty to auto-discover TMP_InputField in the scene (under this object).")]
    public List<TMP_InputField> tmpInputs = new List<TMP_InputField>();

    [Tooltip("Smoothness of movement when raising/lowering the UI.")]
    public float moveLerp = 12f;

    [Header("UI Toolkit Settings")]
    [Tooltip("UIDocument hosting your runtime UI Toolkit hierarchy.")]
    public UIDocument uiDocument;

    [Tooltip("Optional: If your inputs live in a ScrollView, assign it so we can ScrollTo the focused field.")]
    public ScrollView uiScrollView;

    [Tooltip("Extra bottom padding (px) to add under UI Toolkit root while keyboard is open.")]
    public float uiToolkitExtraBottom = 12f;

    // --- Internals ---
    Vector2 _originalAnchoredPos;
    bool _hasOriginalPos;
    bool _keyboardWasVisible;
    VisualElement _root;
    bool _uiToolkitActive;

    TMP_InputField _focusedTMP;
    VisualElement _focusedVE;

    void Awake()
    {
        // Cache original position for UGUI target
        if (moveTarget != null)
        {
            _originalAnchoredPos = moveTarget.anchoredPosition;
            _hasOriginalPos = true;
        }

        // Auto-find TMP inputs if not assigned
        if (tmpInputs == null || tmpInputs.Count == 0)
        {
            tmpInputs = new List<TMP_InputField>(GetComponentsInChildren<TMP_InputField>(true));
        }

        // Hook TMP events
        foreach (var input in tmpInputs)
        {
            if (input == null) continue;
            input.onSelect.AddListener(_ => OnTMPSelected(input));
            input.onDeselect.AddListener(_ => OnTMPDeselected(input));
        }

        // UI Toolkit setup
        if (uiDocument != null)
        {
            _root = uiDocument.rootVisualElement;
            _uiToolkitActive = _root != null;

            if (_uiToolkitActive)
            {
                // Register for focus changes on any TextInputBaseField<string> (TextField/TextArea etc.)
                RegisterUIElementsFocusCallbacks(_root);
            }
        }
    }

    void OnDestroy()
    {
        // Clean up listeners (TMP)
        if (tmpInputs != null)
        {
            foreach (var input in tmpInputs)
            {
                if (input == null) continue;
                input.onSelect.RemoveAllListeners();
                input.onDeselect.RemoveAllListeners();
            }
        }

        // (UI Toolkit listeners are GC'd with the panel)
    }

    void RegisterUIElementsFocusCallbacks(VisualElement root)
    {
        // TextField
        root.Query<TextField>().ForEach(tf =>
        {
            tf.RegisterCallback<FocusInEvent>(OnVEFocusIn);
            tf.RegisterCallback<FocusOutEvent>(OnVEFocusOut);
        });

        // TextArea (if you use it)
        root.Query<TextField>().Where(tf => tf.multiline).ForEach(tf =>
        {
            tf.RegisterCallback<FocusInEvent>(OnVEFocusIn);
            tf.RegisterCallback<FocusOutEvent>(OnVEFocusOut);
        });
    }

    void OnTMPSelected(TMP_InputField field)
    {
        _focusedTMP = field;
    }

    void OnTMPDeselected(TMP_InputField field)
    {
        if (_focusedTMP == field)
            _focusedTMP = null;

        // If keyboard is gone, reset UGUI
        if (!TouchScreenKeyboardVisible())
            ResetUGUIPosition();
    }

    void OnVEFocusIn(FocusInEvent evt)
    {
        _focusedVE = evt.target as VisualElement;

        // If inside a ScrollView, ensure we scroll to it when keyboard shows
        if (uiScrollView != null && _focusedVE != null)
            uiScrollView.ScrollTo(_focusedVE);
    }

    void OnVEFocusOut(FocusOutEvent evt)
    {
        if (_focusedVE == evt.target)
            _focusedVE = null;

        // When keyboard hides, padding is reset in Update
    }

    void Update()
    {
        bool visible = TouchScreenKeyboardVisible();
        float kbHeight = GetKeyboardHeightPixels();
        float bottomInset = Screen.safeArea.y; // pixels eaten by notches/home-gesture
        float effectiveKb = Mathf.Max(0, kbHeight - bottomInset);

        if (debugLog && visible)
        {
            Debug.Log($"[KeyboardRaiser] Keyboard visible: {visible}, height: {kbHeight}px, effective: {effectiveKb}px, " +
                     $"bottomInset: {bottomInset}px, Screen: {Screen.width}x{Screen.height}");
        }

        // UGUI behavior
        if (moveTarget != null)
        {
            if (visible && (_focusedTMP != null))
            {
                float requiredShiftPx = ComputeUGUIRequiredShift(_focusedTMP.textComponent.rectTransform, effectiveKb, topMargin);
                float requiredShiftUnits = ScreenPixelsToAnchoredUnits(requiredShiftPx);
                
                if (debugLog)
                {
                    var canvas = moveTarget.GetComponentInParent<Canvas>();
                    Debug.Log($"[KeyboardRaiser] Shift: {requiredShiftPx}px -> {requiredShiftUnits} units " +
                             $"(scale: {canvas?.rootCanvas.scaleFactor ?? 1f}, " +
                             $"anchor: {moveTarget.anchorMin}-{moveTarget.anchorMax}, " +
                             $"pivot: {moveTarget.pivot}, " +
                             $"anchoredPos: {moveTarget.anchoredPosition})");
                }
                
                MoveUGUI(requiredShiftUnits);
            }
            else
            {
                // Smoothly return
                MoveUGUI(0f);
            }
        }

        // UI Toolkit behavior (padding the root so layout reflows above keyboard)
        if (_uiToolkitActive && _root != null)
        {
            if (visible)
            {
                float padding = Mathf.Max(0f, effectiveKb + uiToolkitExtraBottom);
                var len = new Length(padding, LengthUnit.Pixel);
                if (_root.resolvedStyle.paddingBottom != padding)
                {
                    _root.style.paddingBottom = len;
                }

                // Helpful scroll into view on every frame while open
                if (uiScrollView != null && _focusedVE != null)
                    uiScrollView.ScrollTo(_focusedVE);
            }
            else
            {
                // reset padding
                if (_root.style.paddingBottom != StyleKeyword.None)
                    _root.style.paddingBottom = 0;
            }
        }

        _keyboardWasVisible = visible;
    }

    void MoveUGUI(float shift)
    {
        if (!_hasOriginalPos) return;

        Vector2 target = _originalAnchoredPos + Vector2.up * shift;
        moveTarget.anchoredPosition = Vector2.Lerp(moveTarget.anchoredPosition, target, Time.unscaledDeltaTime * moveLerp);
    }

    void ResetUGUIPosition()
    {
        if (!_hasOriginalPos || moveTarget == null) return;
        moveTarget.anchoredPosition = _originalAnchoredPos;
    }

    float ComputeUGUIRequiredShift(RectTransform fieldRect, float keyboardPx, float marginPx)
    {
        if (fieldRect == null) return 0f;

        // Screen-space rect of the input field
        Vector3[] corners = new Vector3[4];
        fieldRect.GetWorldCorners(corners);

        // Convert to screen pixels
        Vector2 bottomLeft = RectTransformUtility.WorldToScreenPoint(null, corners[0]);
        Vector2 topRight   = RectTransformUtility.WorldToScreenPoint(null, corners[2]);

        float fieldBottom = bottomLeft.y; // px from bottom of screen
        float fieldTop = topRight.y;
        float keyboardTop = keyboardPx > 0 ? Screen.height - keyboardPx : Screen.height;

        if (debugLog)
        {
            Debug.Log($"[KeyboardRaiser] Screen.height: {Screen.height}px, keyboardPx: {keyboardPx}px, " +
                     $"keyboardTop: {keyboardTop}px, fieldBottom: {fieldBottom}px, fieldTop: {fieldTop}px");
        }

        // Calculate how far above the keyboard we want the field to be
        // We want the BOTTOM of the field to be at least (keyboardTop + margin)
        float desiredFieldBottom = keyboardTop + marginPx;
        
        // But we're moving moveTarget, not the field directly
        // So we need to know: if we move moveTarget by X, how much does the field move?
        // For most cases they move together 1:1, but let's be explicit
        
        // Get moveTarget's current screen position
        Vector3[] targetCorners = new Vector3[4];
        moveTarget.GetWorldCorners(targetCorners);
        Vector2 targetBottomLeft = RectTransformUtility.WorldToScreenPoint(null, targetCorners[0]);
        Vector2 targetTopRight = RectTransformUtility.WorldToScreenPoint(null, targetCorners[2]);
        
        // Calculate offset: how far is the field from the moveTarget's bottom
        float fieldOffsetFromTarget = fieldBottom - targetBottomLeft.y;
        
        // Now compute: where should moveTarget's bottom be so that field is at desiredFieldBottom?
        float desiredTargetBottom = desiredFieldBottom - fieldOffsetFromTarget;
        
        // Delta: how much to shift moveTarget's bottom
        float targetDelta = desiredTargetBottom - targetBottomLeft.y;
        
        if (debugLog)
        {
            Debug.Log($"[KeyboardRaiser] Target bottom: {targetBottomLeft.y}px, top: {targetTopRight.y}px, " +
                     $"fieldOffsetFromTarget: {fieldOffsetFromTarget}px, desiredTargetBottom: {desiredTargetBottom}px, " +
                     $"targetDelta: {targetDelta}px, margin: {marginPx}px");
        }
        
        return Mathf.Max(0f, targetDelta);
    }

    // Convert an amount in device screen pixels to anchoredPosition units
    // taking Canvas scale into account. This makes movement correct even when
    // the root RectTransform does not fill the whole screen or when a CanvasScaler
    // is set to Scale With Screen Size.
    float ScreenPixelsToAnchoredUnits(float screenPixels)
    {
        if (moveTarget == null) return screenPixels;
        var canvas = moveTarget.GetComponentInParent<Canvas>();
        if (canvas == null) return screenPixels;
        float scale = Mathf.Max(0.0001f, canvas.rootCanvas.scaleFactor);
        return screenPixels / scale;
    }

    bool TouchScreenKeyboardVisible()
    {
#if UNITY_EDITOR
        // In Editor, pretend it's not visible
        return false;
#else
        return TouchScreenKeyboard.visible;
#endif
    }

    float GetKeyboardHeightPixels()
    {
#if UNITY_EDITOR
        return 0f;
#else
        // Prefer actual area if available
        Rect area = TouchScreenKeyboard.area;
        float h = area.height;

        if (h <= 0f && TouchScreenKeyboard.visible)
        {
            // Some Android devices report 0; fallback heuristic
            h = Screen.height * fallbackKeyboardPct;
        }
        return h;
#endif
    }
}
#else
// Stub so the script compiles on non-mobile platforms
public class MobileKeyboardRaiser : MonoBehaviour { }
#endif
