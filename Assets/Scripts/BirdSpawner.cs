using System.Collections;
using UnityEngine;

/// <summary>
/// Progressive difficulty bird spawner that increases challenge over time.
/// Follows Single Responsibility: manages spawn timing and difficulty progression only.
/// </summary>
public class BirdSpawner : MonoBehaviour
{
    [Header("Bird Setup")]
    [SerializeField] private Transform[] spawnPoints; // optional spawn locations
    [SerializeField] private HitEffectPool hitEffectPool; // for BirdTarget components

    [Header("Difficulty Progression")]
    [SerializeField] private float startSpawnInterval = 3f;
    [SerializeField] private float minSpawnInterval = 0.5f;
    [SerializeField] private float difficultyIncreaseRate = 0.1f; // how fast difficulty ramps
    [SerializeField] private int maxSimultaneousBirds = 8;

    [Header("Wave System")]
    [SerializeField] private bool useWaveSystem = true;
    [SerializeField] private float waveDuration = 30f; // seconds per wave
    [SerializeField] private float waveBreakDuration = 5f;
    [SerializeField] private int birdsPerWaveBase = 5;
    [SerializeField] private int birdsPerWaveIncrease = 2;

    [Header("Spawn Behavior")]
    [SerializeField] private bool spawnFromEdges = true;
    [SerializeField] private float edgeSpawnDistance = 2f;
    [SerializeField] private Vector2 spawnHeightRange = new Vector2(0.2f, 0.8f); // screen percentage

    [Header("Bird Variety")]
    [SerializeField] private Vector2 speedRange = new Vector2(2f, 4f);
    [SerializeField] private Vector2 speedIncreasePerWave = new Vector2(0.2f, 0.3f);

    private Camera mainCamera;
    private Vector2 screenBounds;
    private float currentSpawnInterval;
    private int activeBirdCount;
    private int currentWave = 1;
    private float waveTimer;
    private bool inWaveBreak;
    private int birdsSpawnedThisWave;
    private int targetBirdsThisWave;
    private Coroutine spawnCoroutine;

    public int CurrentWave => currentWave;
    public int ActiveBirds => activeBirdCount;
    public bool IsActive { get; private set; }
    public bool IsInWaveBreak => inWaveBreak;
    public float WaveBreakTimeRemaining => inWaveBreak ? waveTimer : 0f;

    void Start()
    {
        mainCamera = Camera.main;
        if (!mainCamera) mainCamera = FindFirstObjectByType<Camera>();

        CalculateScreenBounds();
        InitializeDifficulty();
        
        // Do not start spawning immediately; wait for connection event.
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
        // Start spawning only if not already active
        if (!IsActive)
        {
            Debug.Log("[BirdSpawner] HandleConnected received - starting spawning.");
            StartSpawning();
        }
    }

    private void HandleDisconnected()
    {
        // Stop spawning when connection lost
        if (IsActive)
            StopSpawning();
    }

    void Update()
    {
        // Fallback poll: if connection established but event missed, start spawning
        // Only trigger if we're not in a wave break
        if (!IsActive && ConnectionSubject.IsConnected && !inWaveBreak)
        {
            Debug.Log("[BirdSpawner] Fallback poll detected connection - starting spawning.");
            StartSpawning();
        }
        if (useWaveSystem)
            UpdateWaveSystem();
        else
            UpdateContinuousProgression();
    }

    private void CalculateScreenBounds()
    {
        if (mainCamera)
        {
            float camHeight = mainCamera.orthographicSize;
            float camWidth = camHeight * mainCamera.aspect;
            screenBounds = new Vector2(camWidth, camHeight);
        }
        else
        {
            screenBounds = new Vector2(10f, 6f);
        }
    }

    private void InitializeDifficulty()
    {
        currentSpawnInterval = startSpawnInterval;
        targetBirdsThisWave = birdsPerWaveBase;
        waveTimer = waveDuration;
        inWaveBreak = false;
    }

    private void UpdateWaveSystem()
    {
        if (inWaveBreak)
        {
            waveTimer -= Time.deltaTime;
            if (waveTimer <= 0)
            {
                StartNextWave();
            }
        }
        else
        {
            waveTimer -= Time.deltaTime;
            bool waveComplete = birdsSpawnedThisWave >= targetBirdsThisWave && activeBirdCount == 0;
            bool timeUp = waveTimer <= 0;

            if (waveComplete || timeUp)
            {
                StartWaveBreak();
            }
        }
    }

    private void UpdateContinuousProgression()
    {
        // Gradually decrease spawn interval over time
        float difficultyFactor = Time.time * difficultyIncreaseRate;
        currentSpawnInterval = Mathf.Lerp(startSpawnInterval, minSpawnInterval, difficultyFactor);
    }

    private void StartNextWave()
    {
        currentWave++;
        inWaveBreak = false;
        birdsSpawnedThisWave = 0;
        targetBirdsThisWave = birdsPerWaveBase + (currentWave - 1) * birdsPerWaveIncrease;
        waveTimer = waveDuration;

        // Increase difficulty
        currentSpawnInterval = Mathf.Max(minSpawnInterval,
            startSpawnInterval - (currentWave - 1) * difficultyIncreaseRate);

        Debug.Log($"[BirdSpawner] Starting Wave {currentWave} - Target birds: {targetBirdsThisWave}, Interval: {currentSpawnInterval}");
        StartSpawning();
    }

    private void StartWaveBreak()
    {
        inWaveBreak = true;
        waveTimer = waveBreakDuration;
        StopSpawning();
    }

    public void StartSpawning()
    {
        if (spawnCoroutine != null)
            StopCoroutine(spawnCoroutine);

        IsActive = true;
        spawnCoroutine = StartCoroutine(SpawnRoutine());
    }

    public void StopSpawning()
    {
        if (spawnCoroutine != null)
        {
            StopCoroutine(spawnCoroutine);
            spawnCoroutine = null;
        }
        IsActive = false;
    }

    private IEnumerator SpawnRoutine()
    {
        Debug.Log($"[BirdSpawner] SpawnRoutine started - Wave {currentWave}");
        while (IsActive)
        {
            bool canSpawn = activeBirdCount < maxSimultaneousBirds;
            bool shouldSpawn = !useWaveSystem || (!inWaveBreak && birdsSpawnedThisWave < targetBirdsThisWave);

            Debug.Log($"[BirdSpawner] Spawn check - CanSpawn:{canSpawn} ShouldSpawn:{shouldSpawn} ActiveBirds:{activeBirdCount} SpawnedThisWave:{birdsSpawnedThisWave}/{targetBirdsThisWave} InBreak:{inWaveBreak}");

            if (canSpawn && shouldSpawn)
            {
                SpawnBird();
                if (useWaveSystem)
                    birdsSpawnedThisWave++;
            }

            yield return new WaitForSeconds(currentSpawnInterval);
        }
        Debug.Log("[BirdSpawner] SpawnRoutine ended");
    }

    private void SpawnBird()
    {
        Vector3 spawnPos = GetSpawnPosition();

        GameObject bird = BirdPool.Get(spawnPos, Quaternion.identity);
        
        if (!bird)
        {
            Debug.LogWarning("[BirdSpawner] Failed to get bird from pool!");
            return;
        }

        SetupBirdDifficulty(bird);
        SetupBirdTarget(bird);

        activeBirdCount++;

        // Subscribe to bird destruction to update count
        var target = bird.GetComponent<BirdTarget>();
        if (target)
        {
            StartCoroutine(TrackBirdLifetime(bird));
        }
        else
        {
            Debug.LogWarning("[DifficultyBirdSpawner] Bird has no BirdTarget component!");
        }
    }

    private Vector3 GetSpawnPosition()
    {
        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            return spawnPoints[Random.Range(0, spawnPoints.Length)].position;
        }

        if (spawnFromEdges)
        {
            // Spawn from screen edges at random height
            bool fromLeft = Random.value > 0.5f;
            float x = fromLeft ? -screenBounds.x - edgeSpawnDistance : screenBounds.x + edgeSpawnDistance;

            float minY = -screenBounds.y + (screenBounds.y * 2 * spawnHeightRange.x);
            float maxY = -screenBounds.y + (screenBounds.y * 2 * spawnHeightRange.y);
            float y = Random.Range(minY, maxY);

            return new Vector3(x, y, 0);
        }

        // Random position within bounds
        return new Vector3(
            Random.Range(-screenBounds.x, screenBounds.x),
            Random.Range(-screenBounds.y * spawnHeightRange.x, screenBounds.y * spawnHeightRange.y),
            0
        );
    }

    private void SetupBirdDifficulty(GameObject bird)
    {
        // Setup speed and behavior based on current wave
        var controller = bird.GetComponent<BirdController>();

        Vector2 currentSpeedRange = speedRange + (currentWave - 1) * speedIncreasePerWave;

        if (controller)
        {
            // Increase speed range for higher waves
            controller.minSpeed = currentSpeedRange.x;
            controller.maxSpeed = currentSpeedRange.y;

            // Reduce stay duration (birds escape faster on higher waves)
            controller.stayDuration = Mathf.Max(2f, controller.stayDuration - (currentWave - 1) * 0.3f);

            // Increase escape speed
            controller.escapeSpeed = controller.escapeSpeed + (currentWave - 1) * 0.5f;

            // Make direction changes more frequent on higher waves
            controller.directionChangeInterval = Mathf.Max(0.3f, controller.directionChangeInterval - (currentWave - 1) * 0.1f);

            // Increase wiggle strength for more erratic movement
            controller.wiggleStrength = controller.wiggleStrength + (currentWave - 1) * 0.2f;
        }
    }


    private void SetupBirdTarget(GameObject bird)
    {
        var target = bird.GetComponent<BirdTarget>();
        if (!target)
        {
            target = bird.AddComponent<BirdTarget>();
        }

        // Assign hit effect factory via BirdTarget API
        if (hitEffectPool)
        {
            target.Setup(hitEffectPool);
        }
    }

    private IEnumerator TrackBirdLifetime(GameObject bird)
    {
        // Wait until bird is deactivated (returned to pool) or destroyed
        yield return new WaitUntil(() => bird == null || !bird.activeInHierarchy);
        activeBirdCount--;
    }

    public void ResetDifficulty()
    {
        StopSpawning();
        currentWave = 1;
        InitializeDifficulty();
        StartSpawning();
    }

    public void SkipToWave(int wave)
    {
        if (wave > 0)
        {
            StopSpawning();
            currentWave = wave;
            InitializeDifficulty();
            StartSpawning();
        }
    }

    void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying) return;

        // Draw screen bounds
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(screenBounds.x * 2, screenBounds.y * 2, 0));

        // Draw spawn areas
        if (spawnFromEdges)
        {
            Gizmos.color = Color.green;
            float minY = -screenBounds.y + (screenBounds.y * 2 * spawnHeightRange.x);
            float maxY = -screenBounds.y + (screenBounds.y * 2 * spawnHeightRange.y);
            float height = maxY - minY;

            // Left spawn area
            Vector3 leftCenter = new Vector3(-screenBounds.x - edgeSpawnDistance, (minY + maxY) * 0.5f, 0);
            Gizmos.DrawWireCube(leftCenter, new Vector3(1f, height, 0));

            // Right spawn area
            Vector3 rightCenter = new Vector3(screenBounds.x + edgeSpawnDistance, (minY + maxY) * 0.5f, 0);
            Gizmos.DrawWireCube(rightCenter, new Vector3(1f, height, 0));
        }

        // Draw custom spawn points
        if (spawnPoints != null)
        {
            Gizmos.color = Color.yellow;
            foreach (var point in spawnPoints)
            {
                if (point) Gizmos.DrawWireSphere(point.position, 0.5f);
            }
        }
    }
}