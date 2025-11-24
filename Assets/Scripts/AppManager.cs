using UnityEngine;

/// <summary>
/// Phone-side UI state manager using observer pattern.
/// Handles panel switching between Scanner, GameUI, and GameOver based on connection and game events.
/// </summary>
public class AppManager : MonoBehaviour
{
    [Header("Panel References")]
    [SerializeField] private GameObject scannerPanel;
    [SerializeField] private GameObject gameUIPanel;
    [SerializeField] private GameObject gameOverPanel;
    
    [Header("Component References")]
    [SerializeField] private GyroUdpSender gyroSender;
    
    [Header("Diagnostics")]
    [SerializeField] private bool verboseLogging = true;
    
    private enum AppState
    {
        Scanner,    // QR scanning, waiting for connection
        InGame,     // Connected and playing
        GameOver    // Game ended, showing score
    }
    
    private AppState currentState = AppState.Scanner;
    
    void Awake()
    {
        DontDestroyOnLoad(gameObject);
    }
    
    void Start()
    {
        // Subscribe to connection events (static)
        ConnectionSubject.OnConnected += HandleConnected;
        ConnectionSubject.OnDisconnected += HandleDisconnected;
        
        // Subscribe to game events (static)
        GameManager.GameOver += HandleGameOver;
        GameManager.GameRestarted += HandleGameRestarted;
        
        // Start in scanner state
        SetState(AppState.Scanner);
        
        if (verboseLogging)
            Debug.Log("[AppManager] Initialized in Scanner state");
    }
    
    private void SetState(AppState newState)
    {
        if (currentState == newState)
            return;
            
        if (verboseLogging)
            Debug.Log($"[AppManager] State transition: {currentState} -> {newState}");
            
        currentState = newState;
        
        // Disable all panels
        if (scannerPanel) scannerPanel.SetActive(false);
        if (gameUIPanel) gameUIPanel.SetActive(false);
        if (gameOverPanel) gameOverPanel.SetActive(false);
        
        // Enable appropriate panel
        switch (currentState)
        {
            case AppState.Scanner:
                if (scannerPanel) scannerPanel.SetActive(true);
                break;
                
            case AppState.InGame:
                if (gameUIPanel) gameUIPanel.SetActive(true);
                break;
                
            case AppState.GameOver:
                if (gameOverPanel) gameOverPanel.SetActive(true);
                break;
        }
    }
    
    private void HandleConnected()
    {
        if (verboseLogging)
            Debug.Log("[AppManager] Connection established, transitioning to InGame");
            
        SetState(AppState.InGame);
    }
    
    private void HandleDisconnected()
    {
        if (verboseLogging)
            Debug.Log("[AppManager] Connection lost, returning to Scanner");
            
        SetState(AppState.Scanner);
    }
    
    private void HandleGameOver()
    {
        if (verboseLogging)
            Debug.Log("[AppManager] Game over, transitioning to GameOver panel");
            
        SetState(AppState.GameOver);
    }
    
    private void HandleGameRestarted()
    {
        if (verboseLogging)
            Debug.Log("[AppManager] Game restarted, returning to InGame");
            
        SetState(AppState.InGame);
    }
    
    /// <summary>
    /// Called by phone GameOver panel's restart button.
    /// Sends restart command to PC receiver.
    /// </summary>
    public void RequestRestart()
    {
        if (verboseLogging)
            Debug.Log("[AppManager] Restart requested, sending command to PC");
            
        if (gyroSender)
        {
            gyroSender.SendRestart();
        }
        else
        {
            Debug.LogError("[AppManager] GyroUdpSender reference missing!");
        }
    }
    
    void OnDestroy()
    {
        // Unsubscribe from events
        ConnectionSubject.OnConnected -= HandleConnected;
        ConnectionSubject.OnDisconnected -= HandleDisconnected;
        
        GameManager.GameOver -= HandleGameOver;
        GameManager.GameRestarted -= HandleGameRestarted;
    }
}
