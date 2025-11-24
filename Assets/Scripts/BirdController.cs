using UnityEngine;

public class BirdController : MonoBehaviour
{
    [Header("Movement")]
    public float minSpeed = 2f;
    public float maxSpeed = 4f;
    public float directionChangeInterval = 1f;
    public float verticalMinY = 1.5f;
    public float wiggleStrength = 1.2f;

    [Header("Lifetime")]
    public float stayDuration = 6f;
    public float escapeSpeed = 6f;

    [Header("Sprite Facing")]
    public bool rotateSprite = true;   // rotates sprite fully (diagonal, angle-based)
    public bool flipX = true;          // flips left/right for classic retro style

    [Header("Entry Behavior")]
    public float enterDuration = 0.8f; // time spent smoothly entering scene
    public float enterHorizontalBoost = 1.2f; // multiplier for horizontal entry speed
    public float flipCooldown = 0.15f; // minimum time between horizontal flips

    private Vector2 moveDir;
    private float moveSpeed;
    private float lifetime;
    private Camera cam;
    private SpriteRenderer sr;

    private bool entering = true;
    private float enterTimer;
    private float lastFlipTime;

    void Start()
    {
        cam = Camera.main;
        sr = GetComponent<SpriteRenderer>();

        moveSpeed = Random.Range(minSpeed, maxSpeed);
        moveDir = Random.insideUnitCircle.normalized;

        // Ensure initial direction is upward-ish
        if (moveDir.y < 0.2f)
            moveDir.y = 0.2f;

        lifetime = stayDuration;
        enterTimer = enterDuration;

        InvokeRepeating(nameof(ChangeDirection), 0.7f, directionChangeInterval);
    }

    void Update()
    {
        lifetime -= Time.deltaTime;
        if (entering)
            HandleEntering();
        else if (lifetime > 0)
            MoveNormal();
        else
            Escape();

        UpdateSpriteFacing();
    }

    void HandleEntering()
    {
        if (!cam)
        {
            entering = false;
            return;
        }

        enterTimer -= Time.deltaTime;

        // Determine horizontal direction toward center of screen
        float viewportX = cam.WorldToViewportPoint(transform.position).x;
        float targetDirX = viewportX < 0.5f ? 1f : -1f;
        float verticalComponent = Random.Range(0.05f, 0.35f);
        moveDir = new Vector2(targetDirX, verticalComponent).normalized;

        // Move with boosted horizontal speed for smooth entry
        transform.position += (Vector3)moveDir * moveSpeed * enterHorizontalBoost * Time.deltaTime;

        // Switch to normal movement once sufficiently inside viewport or timer elapsed
        if (enterTimer <= 0f || (viewportX > 0.07f && viewportX < 0.93f))
        {
            entering = false;
            // Give a slight random nudge for variety after entry
            moveDir += Random.insideUnitCircle * 0.3f;
        }
    }

    void MoveNormal()
    {
        Vector3 pos = transform.position;

        if (pos.y < verticalMinY)
            moveDir.y = Mathf.Abs(moveDir.y) + 0.3f;

        Vector3 view = cam.WorldToViewportPoint(pos);
        float now = Time.time;

        // Horizontal bounds handling with flip cooldown to avoid flicker
        if ((view.x < 0.02f || view.x > 0.98f) && now - lastFlipTime > flipCooldown)
        {
            moveDir.x *= -1f;
            lastFlipTime = now;
            // Clamp position slightly inside to prevent repeated flipping
            float clampLeft = cam.ViewportToWorldPoint(new Vector3(0.025f, view.y, 0)).x;
            float clampRight = cam.ViewportToWorldPoint(new Vector3(0.975f, view.y, 0)).x;
            pos.x = Mathf.Clamp(pos.x, clampLeft, clampRight);
        }

        // Vertical bounds (less strict) with mild reflection
        if (view.y < 0.08f || view.y > 0.92f)
        {
            moveDir.y *= -1f;
            // Slight damping to reduce jitter
            moveDir.y *= 0.9f;
        }

        pos += (Vector3)moveDir.normalized * moveSpeed * Time.deltaTime;
        transform.position = pos;
    }

    void ChangeDirection()
    {
        if (lifetime <= 0) return;

        moveDir += Random.insideUnitCircle * wiggleStrength;
        moveDir.Normalize();

        if (moveDir.y < 0.1f)
            moveDir.y = Random.Range(0.2f, 0.8f);
    }

    void Escape()
    {
        Vector2 escapeDir = new Vector2(moveDir.x > 0 ? 1 : -1, 1).normalized;
        
        // Update moveDir so sprite faces the escape direction
        moveDir = escapeDir;

        transform.position += (Vector3)escapeDir * escapeSpeed * Time.deltaTime;

        Vector3 view = cam.WorldToViewportPoint(transform.position);
        if (view.y > 1.2f)
        {
            // Notify GameManager that bird escaped
            GameManager.RegisterEscape();
            
            // Return to pool instead of destroying
            BirdPool.Return(gameObject);
        }
    }

    void UpdateSpriteFacing()
    {
        if (sr == null) return;

        // Flip horizontally depending on direction (only once inside if leaving entry phase)
        if (flipX)
        {
            if (!entering)
                sr.flipX = moveDir.x < 0;
            else
                sr.flipX = false; // keep consistent during entry to avoid flicker
        }

        // Rotate sprite toward movement direction
        if (rotateSprite)
        {
            float angle = Mathf.Atan2(moveDir.y, moveDir.x) * Mathf.Rad2Deg;

            // If flipping is active, compensate rotation so it doesn't invert
            if (flipX && sr.flipX)
                angle += 180f;
            // During entry keep rotation subtle for stability
            if (entering)
                transform.rotation = Quaternion.Euler(0, 0, Mathf.LerpAngle(0f, angle, 0.3f));
            else
                transform.rotation = Quaternion.Euler(0, 0, angle);
        }
    }
}
