using UnityEngine;
using TMPro;

/// <summary>
/// Simple HUD that displays score and bullets, listening to GameManager events.
/// Attach to a Canvas with TextMeshPro components assigned.
/// </summary>
public class GameHUD : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private TextMeshProUGUI bulletsText;
    [SerializeField] private TextMeshProUGUI escapedBirdsText; // escaped birds counter
    [SerializeField] private TextMeshProUGUI waveText; // wave info display
    [SerializeField] private GameObject gameOverPanel; // optional, will be enabled on game over
    
    [Header("Display Format")]
    [SerializeField] private string scoreFormat = "Score: {0}";
    [SerializeField] private string bulletsFormat = "Bullets: {0}";
    [SerializeField] private string escapedBirdsFormat = "Escaped: {0}/{1}"; // current/max
    [SerializeField] private string waveFormat = "Wave {0}"; // format for wave display
    [SerializeField] private string waveBreakFormat = "Wave Break: {0:F0}s"; // format during break
    [SerializeField] private Color lowAmmoColor = Color.red;
    [SerializeField] private int lowAmmoThreshold = 3;
    [SerializeField] private Color dangerEscapeColor = Color.red;
    [SerializeField] private float dangerEscapeThreshold = 0.7f; // show red when 70% escaped
    
    private Color originalBulletsColor;
    private Color originalEscapedColor;
    
    void Start()
    {
        // Store original bullet text color
        if (bulletsText)
            originalBulletsColor = bulletsText.color;
        
        if (escapedBirdsText)
            originalEscapedColor = escapedBirdsText.color;
        
        // Subscribe to GameManager events
        GameManager.ScoreChanged += UpdateScore;
        GameManager.BulletsChanged += UpdateBullets;
        GameManager.EscapedBirdsChanged += UpdateEscapedBirds;
        GameManager.GameOver += OnGameOver;
        GameManager.GameRestarted += OnGameRestarted;
        
        // Initialize display with current values
        UpdateScore(GameManager.Score);
        UpdateBullets(GameManager.Bullets);
        UpdateEscapedBirds(GameManager.EscapedBirds);
        
        // Hide game over panel initially
        if (gameOverPanel)
            gameOverPanel.SetActive(false);
    }
    
    void OnDestroy()
    {
        // Unsubscribe from events to prevent memory leaks
        GameManager.ScoreChanged -= UpdateScore;
        GameManager.BulletsChanged -= UpdateBullets;
        GameManager.EscapedBirdsChanged -= UpdateEscapedBirds;
        GameManager.GameOver -= OnGameOver;
        GameManager.GameRestarted -= OnGameRestarted;
    }
    
    void Update()
    {
        // Update wave display if present
        UpdateWaveDisplay();
    }
    
    private void UpdateScore(int newScore)
    {
        if (scoreText)
            scoreText.text = string.Format(scoreFormat, newScore);
    }
    
    private void UpdateBullets(int newBullets)
    {
        if (bulletsText)
        {
            bulletsText.text = string.Format(bulletsFormat, newBullets);
            
            // Change color if low on ammo
            bulletsText.color = newBullets <= lowAmmoThreshold ? lowAmmoColor : originalBulletsColor;
        }
    }
    
    private void UpdateEscapedBirds(int escapedCount)
    {
        if (escapedBirdsText)
        {
            int maxEscaped = GameManager.MaxEscapedBirds;
            
            escapedBirdsText.text = string.Format(escapedBirdsFormat, escapedCount, maxEscaped);
            
            // Change color if approaching limit
            float ratio = maxEscaped > 0 ? (float)escapedCount / maxEscaped : 0f;
            escapedBirdsText.color = ratio >= dangerEscapeThreshold ? dangerEscapeColor : originalEscapedColor;
        }
    }
    
    private void OnGameOver()
    {
        if (gameOverPanel)
            gameOverPanel.SetActive(true);
    }
    
    private void OnGameRestarted()
    {
        // Hide game over panel on restart
        if (gameOverPanel)
            gameOverPanel.SetActive(false);
    }
    
    private void UpdateWaveDisplay()
    {
        if (!waveText) return;
        
        // Find active bird spawner to get wave info
    #if UNITY_2023_1_OR_NEWER
        var spawner = FindFirstObjectByType<BirdSpawner>();
    #else
        var spawner = FindObjectOfType<BirdSpawner>();
    #endif
        
        if (spawner)
        {
            if (spawner.IsInWaveBreak)
            {
                waveText.text = string.Format(waveBreakFormat, spawner.WaveBreakTimeRemaining);
            }
            else
            {
                waveText.text = string.Format(waveFormat, spawner.CurrentWave);
            }
        }
    }
}