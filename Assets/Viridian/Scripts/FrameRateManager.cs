// Assets/Scripts/FrameRateManager.cs
using UnityEngine;
using System.Collections;

public class FrameRateManager : MonoBehaviour
{
    public enum Mode { Max, Cap60, MaxVariableInput }

    const string PlayerPrefsKey = "FR_MODE";

    static FrameRateManager _instance;
    public static FrameRateManager Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject(nameof(FrameRateManager));
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<FrameRateManager>();
            }
            return _instance;
        }
    }

    void Start()
    {
        ApplySavedMode();
    }

    Mode currentMode = Mode.Max;
    bool isIdle = false;
    float lastActivityTime;
    const float idleThreshold = 5f; // seconds of no input before dropping to idle FPS
    const int idleFPS = 30;
    const int activeFPS = 120;

    public void ApplySavedMode()
    {
        currentMode = (Mode)PlayerPrefs.GetInt(PlayerPrefsKey, (int)Mode.MaxVariableInput);
        lastActivityTime = Time.unscaledTime;
        StopAllCoroutines();
        StartCoroutine(ApplyModeRoutine(currentMode));
        StartCoroutine(MonitorActivityRoutine());
    }

    void Update()
    {
        // Detect any input activity (only for MaxVariableInput mode)
        if (currentMode == Mode.MaxVariableInput)
        {
            if (Input.anyKey || Input.touchCount > 0 || Input.GetMouseButton(0))
            {
                if (isIdle)
                {
                    // Wake up to high FPS
                    SetTargetFPS(activeFPS);
                    isIdle = false;
                }
                lastActivityTime = Time.unscaledTime;
            }
        }
    }

    IEnumerator MonitorActivityRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(0.5f);

            if (currentMode == Mode.MaxVariableInput)
            {
                float timeSinceActivity = Time.unscaledTime - lastActivityTime;
                
                if (!isIdle && timeSinceActivity >= idleThreshold)
                {
                    // Drop to idle FPS
                    SetTargetFPS(idleFPS);
                    isIdle = true;
                }
            }
        }
    }

    public void SetMax()  => SetMode(Mode.Max);
    public void Set60()   => SetMode(Mode.Cap60);
    public void SetVariableInput() => SetMode(Mode.MaxVariableInput);
    public void Toggle()  => SetMode(currentMode == Mode.Max ? Mode.Cap60 : Mode.Max);

    public void SetMode(Mode mode)
    {
        currentMode = mode;
        PlayerPrefs.SetInt(PlayerPrefsKey, (int)mode);
        PlayerPrefs.Save();
        lastActivityTime = Time.unscaledTime;
        isIdle = false;
        StopAllCoroutines();
        StartCoroutine(ApplyModeRoutine(mode));
        StartCoroutine(MonitorActivityRoutine());
    }

    void SetTargetFPS(int fps)
    {
        int w = Screen.currentResolution.width;
        int h = Screen.currentResolution.height;

#if UNITY_2021_2_OR_NEWER
        var rr = new RefreshRate { numerator = (uint)fps, denominator = 1 };
        Screen.SetResolution(w, h, FullScreenMode.FullScreenWindow, rr);
#endif
        Application.targetFrameRate = fps;
    }

    IEnumerator ApplyModeRoutine(Mode mode)
    {
        // vSync must be 0 or targetFrameRate is ignored
        QualitySettings.vSyncCount = 0;

        // Give Android a frame to init display metrics
        yield return null;

        int targetFps = (mode == Mode.Max || mode == Mode.MaxVariableInput) ? activeFPS : 60;
        SetTargetFPS(targetFps);

        // Wait a couple frames for the new mode to settle
        yield return null;
        yield return null;

        // Re-apply to ensure it sticks
        SetTargetFPS(targetFps);
    }
}
