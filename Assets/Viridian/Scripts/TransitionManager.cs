// MIT License (c) 2025 Viridian Games
// Simple scene transitions (fade / circle wipe) with static API.

using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace ViridianTransitions
{
    public enum TransitionType { None, Fade, Circle }
    public enum LoadMode { Single, Additive }
    public enum EaseType { Linear, InOutSine, InOutQuad, OutCubic }

    public sealed class TransitionManager : MonoBehaviour
    {
        // ---------- Singleton ----------
        private static TransitionManager _inst;
        private static TransitionManager Inst
        {
            get
            {
                if (_inst != null) return _inst;
                var go = new GameObject("TransitionManager");
                DontDestroyOnLoad(go);
                _inst = go.AddComponent<TransitionManager>();
                _inst.BuildOverlay();
                return _inst;
            }
        }

        // ---------- Overlay / UI ----------
        Canvas _canvas;
        CanvasGroup _fadeGroup;     // for fade
        Image _image;               // for circle wipe
        Material _circleMat;        // material for circle wipe
        bool _busy;

        // Shader property cache
        static readonly int _Cutoff = Shader.PropertyToID("_Cutoff");
        static readonly int _Soft = Shader.PropertyToID("_Softness");

        // ---------- Public API (static) ----------

        // Simple overload: fade
        public static void LoadScene(string sceneName, float duration = 0.6f, EaseType ease = EaseType.InOutSine, LoadMode mode = LoadMode.Single)
            => Inst.StartCoroutine(Inst.RunTransition(TransitionType.Fade, sceneName, duration, ease, mode));

        // Circle wipe
        public static void LoadSceneCircle(string sceneName, float duration = 0.8f, EaseType ease = EaseType.OutCubic, float softness = 0.02f, Color? overlayColor = null, LoadMode mode = LoadMode.Single)
            => Inst.StartCoroutine(Inst.RunTransition(TransitionType.Circle, sceneName, duration, ease, mode, softness, overlayColor ?? Color.black));

        // If you want to await from other coroutines:
        public static IEnumerator LoadRoutine(string sceneName, TransitionType type, float duration = 0.7f, EaseType ease = EaseType.InOutSine, LoadMode mode = LoadMode.Single, float softness = 0.02f)
            => Inst.RunTransition(type, sceneName, duration, ease, mode, softness);

        // ---------- Core ----------

        IEnumerator RunTransition(TransitionType type, string sceneName, float duration, EaseType ease, LoadMode mode,
                          float softness = 0.02f, Color overlayColor = default)
        {
            if (_busy) yield break;
            _busy = true;
            EnsureOverlay();

            if (type == TransitionType.Circle)
            {
                _image.color = overlayColor == default ? Color.black : overlayColor;
                if (_circleMat == null) _circleMat = BuildCircleMaterial();
                _image.material = _circleMat;
                _circleMat.SetFloat(_Soft, Mathf.Clamp01(softness));
            }

            // -------- Phase A: Out --------
            switch (type)
            {
                case TransitionType.None:
                    break;

                case TransitionType.Fade:
                    yield return Fade(0f, 1f, duration * 0.5f, ease, true);
                    break;

                case TransitionType.Circle:
                    // 1 -> 0 : hole shrinks to fully covered
                    yield return Circle(1f, 0f, duration * 0.5f, ease, softness, closing: true);
                    break;
            }

            // Load scene
            AsyncOperation op = mode == LoadMode.Single
                ? UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(sceneName, UnityEngine.SceneManagement.LoadSceneMode.Single)
                : UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(sceneName, UnityEngine.SceneManagement.LoadSceneMode.Additive);

            while (!op.isDone) yield return null;
            yield return null; // settle a frame

            // -------- Phase B: In --------
            switch (type)
            {
                case TransitionType.Fade:
                    yield return Fade(1f, 0f, duration, ease, false);
                    break;

                case TransitionType.Circle:
                    // 0 -> 1 : hole expands to reveal new scene
                    yield return Circle(0f, 1f, duration, ease, softness, closing: false);
                    break;
            }

            _busy = false;
        }

        // ---------- Effects ----------

        IEnumerator Fade(float from, float to, float duration, EaseType ease, bool showOverlay)
        {
            _image.enabled = false;
            _fadeGroup.gameObject.SetActive(true);
            _fadeGroup.blocksRaycasts = true;                 // block clicks during fade
            _fadeGroup.alpha = from;

            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(t / Mathf.Max(0.0001f, duration));
                _fadeGroup.alpha = Mathf.Lerp(from, to, EaseEval(ease, p));
                yield return null;
            }
            _fadeGroup.alpha = to;

            if (!showOverlay)
            {
                _fadeGroup.blocksRaycasts = false;
                _fadeGroup.gameObject.SetActive(false);
            }
        }

        IEnumerator Circle(float from, float to, float duration, EaseType ease, float softness, bool closing)
        {
            _fadeGroup.gameObject.SetActive(false);
            _image.enabled = true;
            _image.raycastTarget = true;

            if (_circleMat == null) _circleMat = BuildCircleMaterial();
            _image.material = _circleMat;
            _circleMat.SetFloat(_Soft, Mathf.Clamp01(softness));
            _circleMat.SetFloat(_Cutoff, from);

            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(t / Mathf.Max(0.0001f, duration));
                float v = Mathf.Lerp(from, to, EaseEval(ease, p));
                _circleMat.SetFloat(_Cutoff, v);
                yield return null;
            }
            _circleMat.SetFloat(_Cutoff, to);

            if (!closing)
            {
                _image.raycastTarget = false;
                _image.enabled = false;
            }
        }

        // ==== NEW: serial staggered UI reveal/hide ====

        public Coroutine RevealCanvasGroups(CanvasGroup[] groups,
                                            float fadeDuration = 0.35f,
                                            float perItemDelay = 0.08f,
                                            EaseType ease = EaseType.InOutSine,
                                            bool useUnscaled = true,
                                            bool enableInteractAfter = true)
        {
            return StartCoroutine(RevealCanvasGroupsCo(groups, fadeDuration, perItemDelay, ease, useUnscaled, enableInteractAfter));
        }

        public Coroutine HideCanvasGroups(CanvasGroup[] groups,
                                          float fadeDuration = 0.25f,
                                          float perItemDelay = 0.06f,
                                          EaseType ease = EaseType.InOutSine,
                                          bool useUnscaled = true)
        {
            return StartCoroutine(HideCanvasGroupsCo(groups, fadeDuration, perItemDelay, ease, useUnscaled));
        }

        IEnumerator RevealCanvasGroupsCo(CanvasGroup[] groups, float dur, float gap, EaseType ease, bool unscaled, bool enableInteract)
        {
            if (groups == null) yield break;

            for (int i = 0; i < groups.Length; i++)
            {
                var cg = groups[i];
                if (!cg) continue;

                cg.alpha = Mathf.Min(cg.alpha, 1f);
                cg.blocksRaycasts = false;
                cg.interactable = false;

                // tween via your existing Fade(..)
                yield return FadeCanvasGroupTo(cg, 1f, dur, ease, unscaled);

                if (i < groups.Length - 1)
                    yield return unscaled ? new WaitForSecondsRealtime(gap) : new WaitForSeconds(gap);
            }

            if (enableInteract && groups.Length > 0 && groups[^1])
            {
                var last = groups[^1];
                last.blocksRaycasts = true;
                last.interactable = true;
            }
        }

        IEnumerator HideCanvasGroupsCo(CanvasGroup[] groups, float dur, float gap, EaseType ease, bool unscaled)
        {
            if (groups == null) yield break;

            for (int i = 0; i < groups.Length; i++)
            {
                var cg = groups[i];
                if (!cg) continue;

                cg.blocksRaycasts = false;
                cg.interactable = false;

                yield return FadeCanvasGroupTo(cg, 0f, dur, ease, unscaled);

                if (i < groups.Length - 1)
                    yield return unscaled ? new WaitForSecondsRealtime(gap) : new WaitForSeconds(gap);
            }
        }

        // ==== NEW: fade out these UI groups, then do the scene transition ====
        public static void LoadSceneAfterUIFade(string sceneName,
                                                CanvasGroup[] uiToFade,
                                                float uiFadeDuration = 0.35f,
                                                TransitionType type = TransitionType.None,
                                                float transDuration = 0.6f,
                                                EaseType ease = EaseType.InOutSine,
                                                LoadMode mode = LoadMode.Single,
                                                float circleSoftness = 0.02f,
                                                Color? circleColor = null)
        {
            Inst.StartCoroutine(Inst.LoadSceneAfterUIFadeCo(sceneName, uiToFade, uiFadeDuration, type,
                                                            transDuration, ease, mode, circleSoftness,
                                                            circleColor ?? Color.black));
        }

        IEnumerator LoadSceneAfterUIFadeCo(string sceneName,
                                           CanvasGroup[] uiToFade,
                                           float uiFadeDur,
                                           TransitionType type,
                                           float transDur,
                                           EaseType ease,
                                           LoadMode mode,
                                           float softness,
                                           Color circColor)
        {
            // 1) Fade out requested UI
            yield return HideCanvasGroupsCo(uiToFade, uiFadeDur, 0.06f, ease, true);

            // 2) Run your existing scene transition (fade or circle)
            yield return RunTransition(type, sceneName, transDur, ease, mode, softness, circColor);

            // Optionally: nothing to auto-fade back in; new scene controls its own UI intro.
        }


        // Small helper that reuses your easing and unscaled time
        IEnumerator FadeCanvasGroupTo(CanvasGroup cg, float target, float duration, EaseType ease, bool unscaled)
        {
            float start = cg.alpha;
            float t = 0f;
            while (t < duration)
            {
                t += unscaled ? Time.unscaledDeltaTime : Time.deltaTime;
                float p = Mathf.Clamp01(t / Mathf.Max(0.0001f, duration));
                cg.alpha = Mathf.Lerp(start, target, EaseEval(ease, p));
                yield return null;
            }
            cg.alpha = target;
        }

        // ---------- Overlay builder ----------

        void BuildOverlay()
        {
            // Canvas
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = short.MaxValue; // always on top

            // CanvasScaler (optional – keep defaults)
            gameObject.AddComponent<CanvasScaler>();
            gameObject.AddComponent<GraphicRaycaster>();

            // Fade CG
            var goFade = new GameObject("Fade");
            goFade.transform.SetParent(_canvas.transform, false);
            var rtFade = goFade.AddComponent<RectTransform>();
            rtFade.anchorMin = Vector2.zero;
            rtFade.anchorMax = Vector2.one;
            rtFade.offsetMin = Vector2.zero;
            rtFade.offsetMax = Vector2.zero;

            _fadeGroup = goFade.AddComponent<CanvasGroup>();
            _fadeGroup.alpha = 0f;
            _fadeGroup.interactable = false;
            _fadeGroup.blocksRaycasts = false;

            // Backing image for fade (black full-screen)
            var fadeImg = goFade.AddComponent<Image>();
            fadeImg.color = Color.black;

            // Circle wipe image (uses material to “cut out” a circle)
            var goCircle = new GameObject("Circle");
            goCircle.transform.SetParent(_canvas.transform, false);
            var rtCircle = goCircle.AddComponent<RectTransform>();
            rtCircle.anchorMin = Vector2.zero;
            rtCircle.anchorMax = Vector2.one;
            rtCircle.offsetMin = Vector2.zero;
            rtCircle.offsetMax = Vector2.zero;

            _image = goCircle.AddComponent<Image>();
            _image.color = Color.black;      // black fill, shader reveals the scene
            _image.enabled = false;
            _image.raycastTarget = false;
        }

        void EnsureOverlay()
        {
            if (_canvas == null) BuildOverlay();
        }

        // ---------- Ease helper ----------
        static float EaseEval(EaseType e, float t)
        {
            t = Mathf.Clamp01(t);
            switch (e)
            {
                case EaseType.InOutSine: return -(Mathf.Cos(Mathf.PI * t) - 1f) * 0.5f;
                case EaseType.InOutQuad: return t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) * 0.5f;
                case EaseType.OutCubic: return 1f - Mathf.Pow(1f - t, 3f);
                default: return t;
            }
        }

        // ---------- Circle material (UI shader) ----------
        Material BuildCircleMaterial()
        {
            var mat = Resources.Load<Material>("CircleWipeMat");
            mat.SetFloat(_Cutoff, 1f);
            mat.SetFloat(_Soft, 0.02f);
            return mat;
        }
    }

    // ---------- SceneManager-like helpers ----------
    public static class SceneManagerX
    {
        public static void LoadWithFade(string scene, float duration = 0.6f, EaseType ease = EaseType.InOutSine, LoadMode mode = LoadMode.Single)
            => TransitionManager.LoadScene(scene, duration, ease, mode);

        public static void LoadWithCircle(string scene, float duration = 0.8f, EaseType ease = EaseType.OutCubic, float softness = 0.02f, LoadMode mode = LoadMode.Single)
            => TransitionManager.LoadSceneCircle(scene, duration, ease, softness);

        public static void LoadAfterUIFade(string scene, CanvasGroup[] fadeThese, float uiFade = 0.35f,
                                       TransitionType type = TransitionType.None, float trans = 0.6f,
                                       EaseType ease = EaseType.InOutSine, LoadMode mode = LoadMode.Single)
        => TransitionManager.LoadSceneAfterUIFade(scene, fadeThese, uiFade, type, trans, ease, mode);
    }
}
