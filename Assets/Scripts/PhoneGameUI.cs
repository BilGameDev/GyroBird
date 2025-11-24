using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Phone-side in-game UI displaying score and bullets during gameplay.
/// Updates in real-time based on GameManager events.
/// </summary>
public class PhoneGameUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Text scoreText;
    [SerializeField] private Text bulletsText;
    [SerializeField] private Text escapedBirdsText;
    
    [Header("Display Formatting")]
    [SerializeField] private string scoreFormat = "Score: {0}";
    [SerializeField] private string bulletsFormat = "Bullets: {0}";
    [SerializeField] private string escapedBirdsFormat = "Escaped: {0}/{1}"; // use dynamic max
    
    void Start()
    {
        // Subscribe to game events
        GameManager.ScoreChanged += OnScoreChanged;
        GameManager.BulletsChanged += OnBulletsChanged;
        GameManager.EscapedBirdsChanged += OnEscapedBirdsChanged;
        
        // Initialize display
        UpdateScoreDisplay();
        UpdateBulletsDisplay();
        UpdateEscapedBirdsDisplay();
    }
    
    void OnEnable()
    {
        // Refresh display when panel becomes active
        UpdateScoreDisplay();
        UpdateBulletsDisplay();
        UpdateEscapedBirdsDisplay();
    }
    
    private void OnScoreChanged(int newScore)
    {
        UpdateScoreDisplay();
    }
    
    private void OnBulletsChanged(int newBullets)
    {
        UpdateBulletsDisplay();
    }
    
    private void OnEscapedBirdsChanged(int newEscapedCount)
    {
        UpdateEscapedBirdsDisplay();
    }
    
    private void UpdateScoreDisplay()
    {
        if (scoreText)
        {
            scoreText.text = string.Format(scoreFormat, GameManager.Score);
        }
    }
    
    private void UpdateBulletsDisplay()
    {
        if (bulletsText)
        {
            bulletsText.text = string.Format(bulletsFormat, GameManager.Bullets);
        }
    }
    
    private void UpdateEscapedBirdsDisplay()
    {
        if (escapedBirdsText)
        {
            int maxEscaped = GameManager.MaxEscapedBirds;
            escapedBirdsText.text = string.Format(escapedBirdsFormat, GameManager.EscapedBirds, maxEscaped);
        }
    }
    
    void OnDestroy()
    {
        GameManager.ScoreChanged -= OnScoreChanged;
        GameManager.BulletsChanged -= OnBulletsChanged;
        GameManager.EscapedBirdsChanged -= OnEscapedBirdsChanged;
    }
}
