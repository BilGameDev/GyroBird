using UnityEngine;

/// <summary>
/// Adapter that makes a BirdController shootable without modifying its flight logic.
/// Single Responsibility: hit processing & death sequence.
/// </summary>
[RequireComponent(typeof(BirdController))]
public class BirdTarget : MonoBehaviour, IShootable
{
    [Header("Hit Settings")]
    [SerializeField] private bool destroyOnHit = true;
    [SerializeField] private float destroyDelay = 0.6f;
    [SerializeField] private LayerMask hitLayer = 0; // optional override
    [SerializeField] private bool disableMovementOnHit = true;
    [SerializeField] private bool disableColliderOnHit = true;
    [SerializeField] private int scoreValue = 100;

    private BirdController controller;
    private Collider2D col;
    private IHitEffectFactory effectFactory;
    private bool isAlive = true;
    private bool warnedNoFactory = false;

    public bool IsAlive => isAlive;

    void Awake()
    {
        controller = GetComponent<BirdController>();
        col = GetComponent<Collider2D>();
        if (!col)
            col = gameObject.AddComponent<CircleCollider2D>();
    }

    public void Setup(MonoBehaviour hitEffectFactoryProvider)
    {
        if (hitEffectFactoryProvider is IHitEffectFactory factory)
            effectFactory = factory;
    }

    public void OnShot(Vector2 hitPoint)
    {
        if (!isAlive) return;
        isAlive = false;

        // Spawn effect
        if (effectFactory != null)
        {
            effectFactory.SpawnHitEffect(hitPoint);
        }
        else if (!warnedNoFactory)
        {
            warnedNoFactory = true;
            Debug.LogWarning("[BirdTarget] No hit effect factory assigned; no effect will play.");
        }

        // Report kill for scoring and ammo bonuses
        GameManager.RegisterKill(scoreValue);

        // Disable movement
        if (disableMovementOnHit && controller)
            enabled = false; // stop Update in this component; controller keeps position unless paused

        // Optionally disable collider to prevent double hits
        if (disableColliderOnHit && col)
            col.enabled = false;

        if (destroyOnHit)
        {
            // Return to pool after delay
            StartCoroutine(ReturnToPoolAfterDelay(destroyDelay));
        }
    }
    
    private System.Collections.IEnumerator ReturnToPoolAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        
        // Re-enable components before returning to pool
        if (col)
            col.enabled = true;
        
        isAlive = true;
        enabled = true;
        
        BirdPool.Return(gameObject);
    }
}
