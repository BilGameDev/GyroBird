using UnityEngine;
using UnityEngine.UI;
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
    [SerializeField] private KeyCode shootKey = KeyCode.Mouse0;
    [SerializeField] private int maxHitPerShot = 1;

    [Header("Input Mode")]
    [SerializeField] private InputMode inputMode = InputMode.Mouse;
    [SerializeField] private RectTransform gyroCrosshair; // UI crosshair driven by gyro for aiming

    [Header("Reticle (Optional)")]
    [SerializeField] private RectTransform uiReticle;

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
    private bool allowShooting; // gated by connection

    void Awake()
    {
        if (!targetCamera)
            targetCamera = Camera.main;

        // Setup audio source if not assigned
        if (enableShootSound && !audioSource)
            audioSource = GetComponent<AudioSource>();
        if (enableShootSound && !audioSource)
            audioSource = gameObject.AddComponent<AudioSource>();
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

        bool shouldShoot = false;

        // Check input based on current mode
        if (inputMode == InputMode.Mouse && Input.GetKeyDown(shootKey))
        {
            shouldShoot = true;
        }
        else if (inputMode == InputMode.Gyro)
        {
            // Gyro shooting is handled via network messages, but we can also check for fallback
            if (Input.GetKeyDown(shootKey)) // fallback for testing
                shouldShoot = true;
        }

        if (shouldShoot)
        {
            // Consume a bullet before firing; end game when out (per GameManager config)
            if (GameManager.TryConsumeBullet())
            {
                TryShoot();
                PlayShootEffects();
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

            var mp = Input.mousePosition;
            uiReticle.position = mp;
        }
    }

    private void TryShoot()
    {
        if (!targetCamera) return;

        Vector3 shootPoint;
        Ray ray;

        if (inputMode == InputMode.Mouse)
        {
            Vector3 mousePos = Input.mousePosition;
            ray = targetCamera.ScreenPointToRay(mousePos);
            shootPoint = mousePos;
        }
        else // Gyro mode
        {
            // Use gyro crosshair position if assigned, else screen center fallback
            if (gyroCrosshair)
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
            lastWorldPoint = world;
            HandlePointShoot(world);
        }
        else
        {
            if (Physics.Raycast(ray, out var hit, maxDistance, targetLayers))
            {
                lastWorldPoint = hit.point;
                HandleColliderHit(hit.collider, hit.point);
            }
        }
    }

    private void HandlePointShoot(Vector3 world)
    {
        // 2D overlap
        Collider2D[] hits = Physics2D.OverlapPointAll(world, targetLayers);
        int count = 0;
        for (int i = 0; i < hits.Length; i++)
        {
            if (count >= maxHitPerShot) break;
            HandleColliderHit(hits[i], world);
            count++;
        }
    }

    private void HandleColliderHit(Collider hitCol, Vector3 point)
    {
        if (!hitCol) return;
        if (hitCol.TryGetComponent<IShootable>(out var shootable))
        {
            if (shootable.IsAlive)
                shootable.OnShot(point);
        }
    }
    private void HandleColliderHit(Collider2D hitCol, Vector3 point)
    {
        if (!hitCol) return;
        if (hitCol.TryGetComponent<IShootable>(out var shootable))
        {
            if (shootable.IsAlive)
                shootable.OnShot(point);
        }
    }

    private void PlayShootEffects()
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
                TryShoot();
                PlayShootEffects();
            }
            else
            {
                PlayEmptyClickEffect();
            }
        }
    }

    void OnEnable()
    {
        ConnectionSubject.OnConnected += HandleConnected;
        ConnectionSubject.OnDisconnected += HandleDisconnected;

    }

    void OnDisable()
    {
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
