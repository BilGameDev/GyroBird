using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace NetUtils
{
    /// <summary>
    /// Lightweight internet connectivity checker for Unity.
    /// - Periodically probes captive-portal endpoints that return 204.
    /// - Detects likely captive portals (status 200/redirect with HTML from an unexpected host).
    /// - Exposes status changes via C# event and UnityEvent.
    /// - Uses UnityWebRequest with short timeouts and exponential backoff.
    ///
    /// Drop this on a GameObject in your bootstrap scene and subscribe to OnStatusChanged or UnityEvents.
    /// </summary>
    public class InternetConnectivityChecker : MonoBehaviour
    {
        public enum InternetStatus
        {
            Unknown,
            Online,
            CaptivePortal,
            Offline
        }

        [Header("Check Settings")]
        [Tooltip("Seconds between successful checks.")]
        public float checkIntervalSeconds = 10f;

        [Tooltip("Initial retry delay after a failure (exponential backoff up to maxRetryDelaySeconds).")]
        public float initialRetryDelaySeconds = 2f;

        [Tooltip("Maximum delay between retries when failing repeatedly.")]
        public float maxRetryDelaySeconds = 30f;

        [Tooltip("Per-request timeout in seconds.")]
        public int requestTimeoutSeconds = 4;

        [Tooltip("User-Agent header to send (some portals behave differently based on UA).")]
        public string userAgent = "WordSeek/ConnectivityChecker (+unity)";

        [Header("Probe Endpoints (tried in order)")]
        [Tooltip("URLs expected to return 204 No Content (or a specific small success body). Add your own domain if you host a health endpoint.")]
        public List<string> probeUrls = new List<string>
        {
            // 204 generators commonly used by OS connectivity checks
            "https://www.gstatic.com/generate_204",
            "https://connectivitycheck.gstatic.com/generate_204",
            "https://cp.cloudflare.com/generate_204",
            // Apple returns a tiny success page with body 'Success.'
            "https://www.apple.com/library/test/success.html"
        };

        public InternetStatus Current { get; private set; } = InternetStatus.Unknown;
        Coroutine _loop;
        float _retryDelay;

        void OnEnable()
        {
            _retryDelay = initialRetryDelaySeconds;
            if (_loop == null) _loop = StartCoroutine(CheckLoop());
        }

        void OnDisable()
        {
            if (_loop != null) StopCoroutine(_loop);
            _loop = null;
        }

        IEnumerator CheckLoop()
        {
            while (true)
            {
                yield return CheckOnce();

                // Schedule next run based on whether we are online
                float delay = (Current == InternetStatus.Online) ? checkIntervalSeconds : _retryDelay;
                _retryDelay = Mathf.Min(_retryDelay * 2f, maxRetryDelaySeconds);
                yield return new WaitForSecondsRealtime(delay);
            }
        }

        IEnumerator CheckOnce()
        {
            // Quick pre-check: if device reports no reachability at all, fast-fail (not definitive, but cheap)
            if (Application.internetReachability == NetworkReachability.NotReachable)
            {
                SetStatus(InternetStatus.Offline);
                yield break;
            }

            foreach (var url in probeUrls)
            {
                using (var req = UnityWebRequest.Get(url))
                {
                    req.timeout = Mathf.Max(1, requestTimeoutSeconds);
                    req.redirectLimit = 2; // redirects can indicate captive portal
                    req.SetRequestHeader("User-Agent", userAgent);

                    var op = req.SendWebRequest();
                    while (!op.isDone) yield return null;

                    // Network or HTTP error? try next endpoint
#if UNITY_2020_2_OR_NEWER
                    bool netErr = req.result == UnityWebRequest.Result.ConnectionError || req.result == UnityWebRequest.Result.ProtocolError;
#else
                    bool netErr = req.isNetworkError || req.isHttpError;
#endif
                    if (netErr)
                    {
                        // If we got a redirect, that may be a captive portal; Unity reports 3xx as neither error nor success depending on version.
                        if (IsCaptiveByHeuristic(req))
                        {
                            SetStatus(InternetStatus.CaptivePortal);
                            _retryDelay = initialRetryDelaySeconds; // keep trying frequently; user may sign in
                            yield break;
                        }
                        continue; // try next URL
                    }

                    // Success path: evaluate response
                    if (req.responseCode == 204)
                    {
                        SetStatus(InternetStatus.Online);
                        _retryDelay = initialRetryDelaySeconds;
                        yield break;
                    }

                    if (IsAppleSuccess(url, req))
                    {
                        SetStatus(InternetStatus.Online);
                        _retryDelay = initialRetryDelaySeconds;
                        yield break;
                    }

                    if (IsCaptiveByHeuristic(req))
                    {
                        SetStatus(InternetStatus.CaptivePortal);
                        _retryDelay = initialRetryDelaySeconds;
                        yield break;
                    }
                    // Otherwise, try next endpoint
                }
            }

            // All endpoints failed
            SetStatus(InternetStatus.Offline);
        }

        bool IsAppleSuccess(string url, UnityWebRequest req)
        {
            if (!url.Contains("apple.com")) return false;
            var text = req.downloadHandler != null ? req.downloadHandler.text : null;
            if (req.responseCode == 200 && !string.IsNullOrEmpty(text))
            {
                // Apple's page usually contains the word Success
                if (text.IndexOf("Success", StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }

        bool IsCaptiveByHeuristic(UnityWebRequest req)
        {
            // If we requested a 204 endpoint but got 200 + HTML, or got redirected to another host, it's likely a captive portal
            bool htmlLike = false;
            string contentType = req.GetResponseHeader("Content-Type");
            if (!string.IsNullOrEmpty(contentType))
                htmlLike = contentType.IndexOf("text/html", StringComparison.OrdinalIgnoreCase) >= 0;

            // UnityWebRequest doesn't expose final URL change directly across versions, but downloadHandler.text often contains HTML login
            bool hasHtmlMarkers = false;
            var body = req.downloadHandler != null ? req.downloadHandler.text : null;
            if (!string.IsNullOrEmpty(body))
            {
                // cheap markers seen on captive portals
                if (body.IndexOf("<html", StringComparison.OrdinalIgnoreCase) >= 0) hasHtmlMarkers = true;
                if (body.IndexOf("login", StringComparison.OrdinalIgnoreCase) >= 0) hasHtmlMarkers = true;
                if (body.IndexOf("captive", StringComparison.OrdinalIgnoreCase) >= 0) hasHtmlMarkers = true;
            }
            // If server responded with 200 when we expected 204, and content looks like HTML, treat as captive
            if ((req.responseCode == 200 || (req.responseCode >= 300 && req.responseCode < 400)) && (htmlLike || hasHtmlMarkers))
                return true;

            return false;
        }

        void SetStatus(InternetStatus next)
        {
            if (next == Current) return;
            Current = next;
            NetworkEventHandler.ChangeInternetStatus(Current);
        }

        // Optional: manual trigger
        public void ForceRecheck()
        {
            _retryDelay = initialRetryDelaySeconds;
            if (_loop != null) StopCoroutine(_loop);
            _loop = StartCoroutine(CheckLoop());
        }
    }
}