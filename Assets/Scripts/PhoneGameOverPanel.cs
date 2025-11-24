using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Phone-side game over panel displaying final score with restart button.
/// Communicates with PC via AppManager/GyroUdpSender.
/// </summary>
public class PhoneGameOverPanel : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Text scoreText;
    [SerializeField] private Button restartButton;
    
    [Header("Display Formatting")]
    [SerializeField] private string scoreFormat = "Final Score: {0}";
    
    void Start()
    {
        if (restartButton)
        {
            restartButton.onClick.AddListener(OnRestartClicked);
        }
        
        // Subscribe to game events to update score display
        GameManager.GameOver += OnGameOver;
    }
    
    void OnEnable()
    {
        // Update score display when panel becomes active
        UpdateScoreDisplay();
    }
    
    private void OnGameOver()
    {
        UpdateScoreDisplay();
    }
    
    private void UpdateScoreDisplay()
    {
        if (scoreText)
        {
            scoreText.text = string.Format(scoreFormat, GameManager.Score);
        }
    }
    
    private void OnRestartClicked()
    {
        Debug.Log("[PhoneGameOverPanel] Restart button clicked");
        
        var appManager = UnityEngine.Object.FindObjectOfType<AppManager>();
        if (appManager)
        {
            appManager.RequestRestart();
        }
        else
        {
            Debug.LogError("[PhoneGameOverPanel] AppManager instance not found!");
        }
    }
    
    void OnDestroy()
    {
        if (restartButton)
        {
            restartButton.onClick.RemoveListener(OnRestartClicked);
        }
        
        GameManager.GameOver -= OnGameOver;
    }
}
