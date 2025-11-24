using UnityEngine;

/// <summary>
/// Configuration component that initializes the static BirdPool.
/// Attach to a GameObject in the scene to configure pool settings.
/// </summary>
public class BirdPoolConfig : MonoBehaviour
{
    [Header("Pool Configuration")]
    [SerializeField] private GameObject birdPrefab;
    [SerializeField] private int initialPoolSize = 20;
    [SerializeField] private int maxPoolSize = 50;
    
    [Header("Diagnostics")]
    [SerializeField] private bool verboseLogging = false;
    
    private Transform poolParent;

    void Awake()
    {
        poolParent = new GameObject("BirdPool_Parent").transform;
        poolParent.SetParent(transform);
        
        BirdPool.Initialize(
            prefab: birdPrefab,
            parent: poolParent,
            initialSize: initialPoolSize,
            maxSize: maxPoolSize,
            verbose: verboseLogging
        );
    }
}
