using System.Collections;
#if DOTWEEN
using DG.Tweening;
#endif
using UnityEngine;
using ViridianTransitions;

public class ViridianManager : MonoBehaviour
{
    [Header("Logo (UI)")]
    [SerializeField] RectTransform logo;
    [SerializeField] CanvasGroup canvasGroup; // auto-add if missing

    [Header("Timing")]
    [SerializeField] float playDelay = 2f;
    [SerializeField] float fadeDuration = 0.6f;
    [SerializeField] float seqEndDelay = 3f;

    void Awake()
    {
        Application.targetFrameRate = 60;

        if (!canvasGroup && logo)
            canvasGroup = logo.GetComponent<CanvasGroup>() ?? logo.gameObject.AddComponent<CanvasGroup>();

        // initial states
        if (canvasGroup) canvasGroup.alpha = 0f;
    }

    void Start() => StartCoroutine(PlayAnimation());

    IEnumerator PlayAnimation()
    {
        yield return new WaitForSeconds(playDelay);

#if DOTWEEN
         // fade in logo group
        canvasGroup.DOFade(1f, fadeDuration)
                   .SetEase(Ease.InOutSine);

        yield return new WaitForSeconds(seqEndDelay);

        canvasGroup.DOFade(0f, fadeDuration)
                    .SetEase(Ease.InOutSine)
                    .OnComplete(() =>
                    {
                        SceneManagerX.LoadAfterUIFade("Main", new CanvasGroup[] { canvasGroup }, type: TransitionType.Fade);
                    });
#else
        // Immediate fallback
        canvasGroup.alpha = 1f;
        yield return new WaitForSeconds(seqEndDelay);
        canvasGroup.alpha = 0f;
        SceneManagerX.LoadAfterUIFade("Main", new CanvasGroup[] { canvasGroup }, type: TransitionType.Fade);
#endif

    }
}
