using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

/// <summary>
/// Game Over panel with final score display and restart functionality.
/// Attach to a panel GameObject that should be shown when the game ends.
/// </summary>
public class GameOverPanel : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI finalScoreText;
    [SerializeField] private TextMeshProUGUI gameOverMessage;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button quitButton; // optional
    
    [Header("Display Settings")]
    [SerializeField] private string finalScoreFormat = "Final Score: {0}";
    [SerializeField] private string gameOverText = "Game Over!";
    
    void Start()
    {
        // Wire up buttons
        if (restartButton)
            restartButton.onClick.AddListener(RestartGame);
        
        if (quitButton)
            quitButton.onClick.AddListener(QuitGame);
        
        // Set initial text
        if (gameOverMessage)
            gameOverMessage.text = gameOverText;
    }
    
    void OnEnable()
    {
        // Update final score when panel becomes active
        if (finalScoreText)
        {
            finalScoreText.text = string.Format(finalScoreFormat, GameManager.Score);
        }
        
        // Pause time for dramatic effect (optional)
        Time.timeScale = 0f;
    }
    
    void OnDisable()
    {
        // Resume time when panel is hidden
        Time.timeScale = 1f;
    }
    
    private void RestartGame()
    {
        // Resume time before restarting
        Time.timeScale = 1f;
        
        // Use GameManager's restart method
        GameManager.RestartGame();
    }
    
    private void QuitGame()
    {
        // Resume time before quitting
        Time.timeScale = 1f;
        
        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
        #else
            Application.Quit();
        #endif
    }
}