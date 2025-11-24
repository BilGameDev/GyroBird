using UnityEngine;
using System;

/// <summary>
/// Static game state manager using observer pattern for state changes.
/// No singleton - pure static with events for decoupled communication.
/// </summary>
public static class GameManager
{
    // Configuration (set via GameManagerConfig component)
    private static int startingBullets = 10;
    private static int killsForBonus = 2;
    private static int bonusBullets = 1;
    private static bool endGameWhenOutOfBullets = true;
    private static int maxEscapedBirds = 10;
    private static bool endGameOnEscapeLimit = true;

    // State
    public static int Score { get; private set; }
    public static int Bullets { get; private set; }
    public static int EscapedBirds { get; private set; }
    public static int MaxEscapedBirds => maxEscapedBirds;
    public static bool IsGameOver { get; private set; }

    // Observer pattern events
    public static event Action<int> ScoreChanged;
    public static event Action<int> BulletsChanged;
    public static event Action<int> EscapedBirdsChanged;
    public static event Action GameOver;
    public static event Action GameRestarted;

    private static int killsSinceBonus;

    /// <summary>
    /// Initialize game state. Call this from a GameManagerConfig component on scene start.
    /// </summary>
    public static void Initialize(int bullets = 10, int maxEscapes = 10, int killBonus = 2, int bonusAmmo = 1, bool endOnBullets = true, bool endOnEscapes = true)
    {
        startingBullets = bullets;
        maxEscapedBirds = maxEscapes;
        killsForBonus = killBonus;
        bonusBullets = bonusAmmo;
        endGameWhenOutOfBullets = endOnBullets;
        endGameOnEscapeLimit = endOnEscapes;

        Score = 0;
        Bullets = Mathf.Max(0, startingBullets);
        EscapedBirds = 0;
        killsSinceBonus = 0;
        IsGameOver = false;

        BulletsChanged?.Invoke(Bullets);
        ScoreChanged?.Invoke(Score);
        EscapedBirdsChanged?.Invoke(EscapedBirds);
    }

    public static bool TryConsumeBullet()
    {
        if (IsGameOver) return false;
        if (Bullets <= 0)
        {
            if (endGameWhenOutOfBullets)
                EndGame();
            return false;
        }

        Bullets--;
        BulletsChanged?.Invoke(Bullets);
        return true;
    }

    public static void RegisterKill(int pointsOverride = 1)
    {
        if (IsGameOver) return;
        Score += pointsOverride;
        ScoreChanged?.Invoke(Score);

        killsSinceBonus++;
        if (killsSinceBonus >= Mathf.Max(1, killsForBonus))
        {
            killsSinceBonus = 0;
            if (bonusBullets > 0)
            {
                Bullets += bonusBullets;
                BulletsChanged?.Invoke(Bullets);
            }
        }
    }
    
    public static void RegisterEscape()
    {
        if (IsGameOver) return;
        
        EscapedBirds++;
        EscapedBirdsChanged?.Invoke(EscapedBirds);
        
        Debug.Log($"[GameManager] Bird escaped! Total: {EscapedBirds}/{maxEscapedBirds}");
        
        if (endGameOnEscapeLimit && EscapedBirds >= maxEscapedBirds)
        {
            EndGame();
        }
    }

    public static void EndGame()
    {
        if (IsGameOver) return;
        IsGameOver = true;

        // Stop spawners and input
#if UNITY_2023_1_OR_NEWER
        var spawners = UnityEngine.Object.FindObjectsByType<BirdSpawner>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
#else
        var spawners = UnityEngine.Object.FindObjectsOfType<BirdSpawner>();
#endif
        foreach (var s in spawners) s.StopSpawning();

#if UNITY_2023_1_OR_NEWER
        var shooters = UnityEngine.Object.FindObjectsByType<MouseShooter>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
#else
        var shooters = UnityEngine.Object.FindObjectsOfType<MouseShooter>();
#endif
        foreach (var m in shooters)
            m.enabled = false;

        GameOver?.Invoke();
        Debug.Log("[GameManager] Game Over");
    }

    public static void RestartGame()
    {
        Time.timeScale = 1f;
        
        // Reset state
        IsGameOver = false;
        Score = 0;
        Bullets = startingBullets;
        EscapedBirds = 0;
        killsSinceBonus = 0;
        
        // Notify observers
        ScoreChanged?.Invoke(Score);
        BulletsChanged?.Invoke(Bullets);
        EscapedBirdsChanged?.Invoke(EscapedBirds);
        GameRestarted?.Invoke();
        
        // Return birds to pool
        BirdPool.ReturnAll();
        
        // Re-enable shooters
#if UNITY_2023_1_OR_NEWER
        var shooters = UnityEngine.Object.FindObjectsByType<MouseShooter>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
#else
        var shooters = UnityEngine.Object.FindObjectsOfType<MouseShooter>();
#endif
        foreach (var m in shooters)
            m.enabled = true;
        
        // Restart spawners
#if UNITY_2023_1_OR_NEWER
        var spawners = UnityEngine.Object.FindObjectsByType<BirdSpawner>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
#else
        var spawners = UnityEngine.Object.FindObjectsOfType<BirdSpawner>();
#endif
        foreach (var s in spawners)
            s.ResetDifficulty();
        
        Debug.Log("[GameManager] Game restarted from wave 1");
    }
}
