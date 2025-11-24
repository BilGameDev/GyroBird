using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Static object pool for bird instances using observer pattern.
/// No singleton - configuration set via BirdPoolConfig component.
/// </summary>
public static class BirdPool
{
    private static GameObject birdPrefab;
    private static Transform poolParent;
    private static int maxPoolSize = 50;
    private static bool verboseLogging = false;
    
    private static Queue<GameObject> availableBirds = new Queue<GameObject>();
    private static HashSet<GameObject> activeBirds = new HashSet<GameObject>();
    
    public static int AvailableCount => availableBirds.Count;
    public static int ActiveCount => activeBirds.Count;
    public static int TotalPooled => availableBirds.Count + activeBirds.Count;

    /// <summary>
    /// Initialize the pool. Call from BirdPoolConfig component.
    /// </summary>
    public static void Initialize(GameObject prefab, Transform parent, int initialSize, int maxSize, bool verbose = false)
    {
        birdPrefab = prefab;
        poolParent = parent;
        maxPoolSize = maxSize;
        verboseLogging = verbose;
        
        // Clear existing pool
        availableBirds.Clear();
        activeBirds.Clear();
        
        if (!birdPrefab)
        {
            Debug.LogError("[BirdPool] No bird prefab assigned!");
            return;
        }
        
        // Pre-instantiate initial pool
        for (int i = 0; i < initialSize; i++)
        {
            CreateNewBird();
        }
        
        if (verboseLogging)
            Debug.Log($"[BirdPool] Initialized with {initialSize} birds");
    }
    
    private static GameObject CreateNewBird()
    {
        if (!birdPrefab)
        {
            Debug.LogError("[BirdPool] Cannot create bird - no prefab set!");
            return null;
        }
        
        GameObject bird = UnityEngine.Object.Instantiate(birdPrefab, poolParent);
        bird.SetActive(false);
        availableBirds.Enqueue(bird);
        return bird;
    }
    
    /// <summary>
    /// Get a bird from the pool.
    /// </summary>
    public static GameObject Get(Vector3 position, Quaternion rotation)
    {
        GameObject bird;
        
        if (availableBirds.Count > 0)
        {
            bird = availableBirds.Dequeue();
        }
        else if (activeBirds.Count < maxPoolSize)
        {
            bird = CreateNewBird();
            if (bird && verboseLogging)
                Debug.Log($"[BirdPool] Expanded pool (now {TotalPooled} total)");
        }
        else
        {
            Debug.LogWarning($"[BirdPool] Pool exhausted at max size {maxPoolSize}!");
            return null;
        }
        
        if (!bird) return null;
        
        // Reset bird state
        bird.transform.position = position;
        bird.transform.rotation = rotation;
        bird.SetActive(true);
        
        activeBirds.Add(bird);
        
        if (verboseLogging)
            Debug.Log($"[BirdPool] Get bird (active: {ActiveCount}, available: {AvailableCount})");
        
        return bird;
    }
    
    /// <summary>
    /// Return a bird to the pool.
    /// </summary>
    public static void Return(GameObject bird)
    {
        if (bird == null)
        {
            Debug.LogWarning("[BirdPool] Attempted to return null bird");
            return;
        }
        
        if (!activeBirds.Contains(bird))
        {
            Debug.LogWarning($"[BirdPool] Attempted to return bird not from this pool: {bird.name}");
            return;
        }
        
        activeBirds.Remove(bird);
        bird.SetActive(false);
        if (poolParent) bird.transform.SetParent(poolParent);
        availableBirds.Enqueue(bird);
        
        if (verboseLogging)
            Debug.Log($"[BirdPool] Return bird (active: {ActiveCount}, available: {AvailableCount})");
    }
    
    /// <summary>
    /// Return all active birds to the pool.
    /// </summary>
    public static void ReturnAll()
    {
        // Copy to list to avoid modifying collection during iteration
        List<GameObject> toReturn = new List<GameObject>(activeBirds);
        
        foreach (GameObject bird in toReturn)
        {
            Return(bird);
        }
        
        if (verboseLogging)
            Debug.Log($"[BirdPool] Returned all birds to pool");
    }
}
