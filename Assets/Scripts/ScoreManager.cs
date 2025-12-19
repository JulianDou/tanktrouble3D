using UnityEngine;
using TMPro;

public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance { get; private set; }
    
    public TextMeshProUGUI scoreText; // Reference to the Score Text (TMP) UI element
    
    private int player1Score = 0;
    private int player2Score = 0;
    
    void Awake()
    {
        // Singleton pattern
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    void Start()
    {
        UpdateScoreDisplay();
    }
    
    // Called when a player dies to award a point to the other player
    public void OnPlayerKilled(GameObject deadPlayer)
    {
        // Determine which player died and award point to the other
        if (deadPlayer.name.Contains("Player1"))
        {
            player2Score++;
            Debug.Log($"Player 2 scored! New score: {player1Score} | {player2Score}");
        }
        else if (deadPlayer.name.Contains("Player2"))
        {
            player1Score++;
            Debug.Log($"Player 1 scored! New score: {player1Score} | {player2Score}");
        }
        
        UpdateScoreDisplay();
    }
    
    void UpdateScoreDisplay()
    {
        if (scoreText != null)
        {
            scoreText.text = $"{player1Score} | {player2Score}";
        }
        else
        {
            Debug.LogWarning("Score Text reference not set in ScoreManager!");
        }
    }
    
    // Optional: Method to reset scores
    public void ResetScores()
    {
        player1Score = 0;
        player2Score = 0;
        UpdateScoreDisplay();
    }
}
