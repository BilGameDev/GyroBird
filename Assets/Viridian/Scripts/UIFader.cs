using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(CanvasGroup))]
public class UIFader : MonoBehaviour
{
    [Tooltip("Default fade duration in seconds.")]
    public float defaultDuration = 0.3f;

    [Tooltip("Disable raycasts when invisible.")]
    public bool blockRaycastsWhenVisible = true;

    [Tooltip("Ignore user input when faded out.")]
    public bool interactableWhenVisible = true;

    [Header("Delay")]
    public bool useDelay;
    public float delayValue;

    CanvasGroup canvasGroup;
    Coroutine currentRoutine;
    
    [Space]
    [SerializeField] UnityEvent OnClick;

    void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        ApplyInteractableState(canvasGroup.alpha > 0f);
    }

    public void FadeIn()
    {
        StartFade(1f, defaultDuration);
    }

    public void FadeOut()
    {
        StartFade(0f, defaultDuration);
    }

    void StartFade(float targetAlpha, float duration)
    {
        if (currentRoutine != null)
            StopCoroutine(currentRoutine);
        currentRoutine = StartCoroutine(FadeRoutine(targetAlpha, duration));
    }

    IEnumerator FadeRoutine(float targetAlpha, float duration)
    {
        if (useDelay)
            yield return new WaitForSeconds(delayValue);
            
        float startAlpha = canvasGroup.alpha;
        float time = 0f;

        while (time < duration)
        {
            time += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(time / duration);
            canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
            yield return null;
        }

        canvasGroup.alpha = targetAlpha;
        ApplyInteractableState(targetAlpha > 0.99f);

        currentRoutine = null;

        OnClick?.Invoke();
    }

    void ApplyInteractableState(bool visible)
    {
        canvasGroup.interactable = visible && interactableWhenVisible;
        canvasGroup.blocksRaycasts = visible && blockRaycastsWhenVisible;
    }
}
