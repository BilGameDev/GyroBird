using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;

public enum InputMode
{
    Mouse,
    Gyro
}

/// <summary>
/// Handles mouse and gyro input to shoot IShootable targets. Can switch between input modes.
/// </summary>
public class MouseShooter : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private Camera targetCamera;
    [SerializeField] private LayerMask targetLayers; // layers that contain shootable colliders
    [SerializeField] private float maxDistance = 100f;
    [SerializeField] private bool useOverlapPoint = true; // 2D style shooting
    [SerializeField] private int maxHitPerShot = 1;

    [Header("New Input System")]
    [SerializeField] private InputActionReference shootAction;
    [SerializeField] private InputActionReference pointAction;

    [Header("Input Mode")]
    [SerializeField] private InputMode inputMode = InputMode.Mouse;
    [SerializeField] private RectTransform gyroCrosshair; // UI crosshair driven by gyro for aiming
    [SerializeField] private GyroUIReceiver pointerReceiver;

    [Header("Reticle (Optional)")]
    [SerializeField] private RectTransform uiReticle;

    [Header("Aim Assist")]
    [SerializeField] private bool enableAimAssist = true;
    [SerializeField] private float assistRadiusPixels = 70f;
    [SerializeField] private float assistStrength = 0.55f;
    [SerializeField] private float minimumPointerConfidence = 0.15f;

    [Header("Reticle Feel")]
    [SerializeField] private float reticlePunchScale = 1.18f;
    [SerializeField] private float reticlePunchDuration = 0.08f;

    [Header("Effects (Optional)")]
    [SerializeField] private bool enableScreenFlash = true;
    [SerializeField] private UnityEngine.UI.Image screenFlashOverlay; // full-screen white image
    [SerializeField] private float flashDuration = 0.1f;
    [SerializeField] private Color flashColor = Color.white;
    [SerializeField] private bool enableShootSound = true;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip shootSound;
    [SerializeField] private AudioClip emptyClickSound; // when out of ammo

    private Vector3 lastWorldPoint;
    private Coroutine flashCoroutine;
    private Coroutine reticlePunchCoroutine;
    private bool allowShooting; // gated by connection
    private Vector3 baseReticleScale = Vector3.one;

    void Awake()
    {
        if (!targetCamera)
            targetCamera = Camera.main;

        // Setup audio source if not assigned
        if (enableShootSound && !audioSource)
            audioSource = GetComponent<AudioSource>();
        if (enableShootSound && !audioSource)
            audioSource = gameObject.AddComponent<AudioSource>();

        if (!pointerReceiver)
            pointerReceiver = FindAnyObjectByType<GyroUIReceiver>();

        if (uiReticle)
            baseReticleScale = uiReticle.localScale;
    }

    void Start()
    {
        // If already connected (e.g., late enable) reflect state
        if (ConnectionSubject.IsConnected)
            HandleConnected();
    }

    void Update()
    {
        if (!allowShooting) return; // gate all shooting logic until connected
        UpdateReticle();

        if (WasLocalShootPressed())
        {
            // Consume a bullet before firing; end game when out (per GameManager config)
            if (GameManager.TryConsumeBullet())
            {
                bool didHit = TryShoot();
                PlayShootEffects(didHit);
            }
            else
            {
                // Play empty click sound when out of ammo
                PlayEmptyClickEffect();
            }
        }
    }

    private void UpdateReticle()
    {
        if (!uiReticle || !targetCamera) return;
        if (inputMode == InputMode.Mouse)
        {
            uiReticle.position = GetPointerScreenPosition();
        }
    }

    private bool TryShoot()
    {
        if (!targetCamera) return false;

        Vector3 shootPoint;
        Ray ray;

        if (inputMode == InputMode.Mouse)
        {
            Vector3 mousePos = GetPointerScreenPosition();
            ray = targetCamera.ScreenPointToRay(mousePos);
            shootPoint = mousePos;
        }
        else // Gyro mode
        {
            // Use gyro crosshair position if assigned, else screen center fallback
            if (pointerReceiver)
            {
                shootPoint = pointerReceiver.GetAimScreenPosition();
            }
            else if (gyroCrosshair)
            {
                shootPoint = gyroCrosshair.position;
            }
            else if (uiReticle)
            {
                shootPoint = uiReticle.position;
            }
            else
            {
                shootPoint = new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0);
            }
            ray = targetCamera.ScreenPointToRay(shootPoint);
        }

        if (useOverlapPoint)
        {
            // Convert to world at z=0 plane
            Vector3 world = targetCamera.ScreenToWorldPoint(new Vector3(shootPoint.x, shootPoint.y, Mathf.Abs(targetCamera.transform.position.z)));
            world = ResolveAssistedWorldPoint(shootPoint, world);
            lastWorldPoint = world;
            return HandlePointShoot(world);
        }
        else
        {
            if (Physics.Raycast(ray, out var hit, maxDistance, targetLayers))
            {
                lastWorldPoint = hit.point;
                return HandleColliderHit(hit.collider, hit.point);
            }
        }

        return false;
    }

    private bool WasLocalShootPressed()
    {
        if (shootAction && shootAction.action != null)
        {
            return shootAction.action.WasPressedThisFrame();
        }

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            return true;

        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
            return true;

        if (Keyboard.current != null &&
            (Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.enterKey.wasPressedThisFrame))
            return true;

        return false;
    }

    private Vector3 GetPointerScreenPosition()
    {
        if (pointAction && pointAction.action != null)
        {
            Vector2 point = pointAction.action.ReadValue<Vector2>();
            return new Vector3(point.x, point.y, 0f);
        }

        if (Mouse.current != null)
        {
            Vector2 point = Mouse.current.position.ReadValue();
            return new Vector3(point.x, point.y, 0f);
        }

        if (Touchscreen.current != null)
        {
            Vector2 point = Touchscreen.current.primaryTouch.position.ReadValue();
            return new Vector3(point.x, point.y, 0f);
        }

        return new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f);
    }

    private Vector3 ResolveAssistedWorldPoint(Vector3 screenPoint, Vector3 rawWorld)
    {
        if (!enableAimAssist || inputMode != InputMode.Gyro)
            return rawWorld;

        if (pointerReceiver && pointerReceiver.CurrentPointer.Confidence < minimumPointerConfidence)
            return rawWorld;

        Vector3 radiusWorldPoint = targetCamera.ScreenToWorldPoint(new Vector3(screenPoint.x + assistRadiusPixels, screenPoint.y, Mathf.Abs(targetCamera.transform.position.z)));
        float worldRadius = Mathf.Abs(radiusWorldPoint.x - rawWorld.x);
        Collider2D[] candidates = Physics2D.OverlapCircleAll(rawWorld, worldRadius, targetLayers);

        Collider2D bestCollider = null;
        Vector3 bestWorld = rawWorld;
        float bestDistance = float.PositiveInfinity;

        for (int i = 0; i < candidates.Length; i++)
        {
            Collider2D candidate = candidates[i];
            if (!candidate || !candidate.TryGetComponent<IShootable>(out var shootable) || !shootable.IsAlive)
                continue;

            Vector3 candidateWorld = candidate.bounds.center;
            Vector3 candidateScreen = targetCamera.WorldToScreenPoint(candidateWorld);
            float distance = Vector2.Distance(screenPoint, candidateScreen);

            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestCollider = candidate;
                bestWorld = candidateWorld;
            }
        }

        if (!bestCollider)
            return rawWorld;

        float normalizedDistance = Mathf.Clamp01(bestDistance / Mathf.Max(1f, assistRadiusPixels));
        float dynamicStrength = assistStrength * (1f - normalizedDistance);
        return Vector3.Lerp(rawWorld, bestWorld, dynamicStrength);
    }

    private bool HandlePointShoot(Vector3 world)
    {
        // 2D overlap
        Collider2D[] hits = Physics2D.OverlapPointAll(world, targetLayers);
        int count = 0;
        bool didHit = false;
        for (int i = 0; i < hits.Length; i++)
        {
            if (count >= maxHitPerShot) break;
            if (HandleColliderHit(hits[i], world))
            {
                didHit = true;
                count++;
            }
        }

        return didHit;
    }

    private bool HandleColliderHit(Collider hitCol, Vector3 point)
    {
        if (!hitCol) return false;
        if (hitCol.TryGetComponent<IShootable>(out var shootable))
        {
            if (shootable.IsAlive)
            {
                shootable.OnShot(point);
                return true;
            }
        }

        return false;
    }

    private bool HandleColliderHit(Collider2D hitCol, Vector3 point)
    {
        if (!hitCol) return false;
        if (hitCol.TryGetComponent<IShootable>(out var shootable))
        {
            if (shootable.IsAlive)
            {
                shootable.OnShot(point);
                return true;
            }
        }

        return false;
    }

    private void PlayShootEffects(bool didHit)
    {
        // Screen flash
        if (enableScreenFlash && screenFlashOverlay)
        {
            if (flashCoroutine != null)
                StopCoroutine(flashCoroutine);
            flashCoroutine = StartCoroutine(ScreenFlashCoroutine());
        }

        // Shoot sound
        if (enableShootSound && audioSource && shootSound)
        {
            audioSource.PlayOneShot(shootSound);
        }

        PlayReticlePunch(didHit ? reticlePunchScale * 1.08f : reticlePunchScale);
    }

    private void PlayEmptyClickEffect()
    {
        // Play empty click sound
        if (enableShootSound && audioSource && emptyClickSound)
        {
            audioSource.PlayOneShot(emptyClickSound);
        }
    }

    private IEnumerator ScreenFlashCoroutine()
    {
        if (!screenFlashOverlay) yield break;

        // Set initial flash color with full alpha
        Color startColor = flashColor;
        startColor.a = 0.8f; // Bright but not completely blinding
        screenFlashOverlay.color = startColor;
        screenFlashOverlay.gameObject.SetActive(true);

        // Fade out over flash duration
        float elapsed = 0f;
        while (elapsed < flashDuration)
        {
            elapsed += Time.unscaledDeltaTime; // Use unscaled time in case game is paused
            float alpha = Mathf.Lerp(startColor.a, 0f, elapsed / flashDuration);
            Color currentColor = startColor;
            currentColor.a = alpha;
            screenFlashOverlay.color = currentColor;
            yield return null;
        }

        // Ensure flash is completely hidden
        screenFlashOverlay.gameObject.SetActive(false);
        flashCoroutine = null;
    }

    private void PlayReticlePunch(float targetScale)
    {
        RectTransform reticle = inputMode == InputMode.Gyro && gyroCrosshair ? gyroCrosshair : uiReticle;
        if (!reticle)
            return;

        if (reticlePunchCoroutine != null)
            StopCoroutine(reticlePunchCoroutine);

        reticlePunchCoroutine = StartCoroutine(ReticlePunchCoroutine(reticle, targetScale));
    }

    private IEnumerator ReticlePunchCoroutine(RectTransform reticle, float targetScale)
    {
        Vector3 startScale = reticle.localScale;
        Vector3 peakScale = baseReticleScale * targetScale;
        float halfDuration = Mathf.Max(0.01f, reticlePunchDuration * 0.5f);

        float elapsed = 0f;
        while (elapsed < halfDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            reticle.localScale = Vector3.Lerp(startScale, peakScale, elapsed / halfDuration);
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < halfDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            reticle.localScale = Vector3.Lerp(peakScale, baseReticleScale, elapsed / halfDuration);
            yield return null;
        }

        reticle.localScale = baseReticleScale;
        reticlePunchCoroutine = null;
    }

    // Public methods for external control
    public void SetInputMode(InputMode mode)
    {
        inputMode = mode;
        Debug.Log($"[MouseShooter] Input mode set to {mode}");
    }

    public void GyroShoot()
    {
        // Called by network receiver when gyro shoot command is received
        if (inputMode == InputMode.Gyro && allowShooting)
        {
            // Consume a bullet before firing
            if (GameManager.TryConsumeBullet())
            {
                bool didHit = TryShoot();
                PlayShootEffects(didHit);
            }
            else
            {
                PlayEmptyClickEffect();
            }
        }
    }

    void OnEnable()
    {
        if (shootAction && shootAction.action != null)
            shootAction.action.Enable();
        if (pointAction && pointAction.action != null)
            pointAction.action.Enable();

        ConnectionSubject.OnConnected += HandleConnected;
        ConnectionSubject.OnDisconnected += HandleDisconnected;

    }

    void OnDisable()
    {
        if (shootAction && shootAction.action != null)
            shootAction.action.Disable();
        if (pointAction && pointAction.action != null)
            pointAction.action.Disable();

        ConnectionSubject.OnConnected -= HandleConnected;
        ConnectionSubject.OnDisconnected -= HandleDisconnected;
    }

    private void HandleConnected()
    {
        allowShooting = true;
        // Auto-switch to gyro mode on connection if a gyro crosshair exists
        if (gyroCrosshair)
        {
            SetInputMode(InputMode.Gyro);
        }
    }

    private void HandleDisconnected()
    {
        allowShooting = false;
    }
}
