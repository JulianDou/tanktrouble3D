using UnityEngine;
using UnityEngine.InputSystem;

public class AIToggleManager : MonoBehaviour
{
    public GameObject player1;
    public GameObject player2;
    
    private AIController player1AI;
    private AIController player2AI;
    private PlayerMovement player1Controller;
    private PlayerMovement player2Controller;

    void Start()
    {
        // Get or add AI controllers
        player1AI = player1.GetComponent<AIController>();
        if (player1AI == null)
            player1AI = player1.AddComponent<AIController>();
            
        player2AI = player2.GetComponent<AIController>();
        if (player2AI == null)
            player2AI = player2.AddComponent<AIController>();

        // Get existing player controller scripts
        player1Controller = player1.GetComponent<PlayerMovement>();
        player2Controller = player2.GetComponent<PlayerMovement>();
    }

    void Update()
    {
        // Use new Input System
        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        // Toggle Player 1 AI with F1 key
        if (keyboard.f1Key.wasPressedThisFrame)
        {
            TogglePlayer1AI();
        }

        // Toggle Player 2 AI with F2 key
        if (keyboard.f2Key.wasPressedThisFrame)
        {
            TogglePlayer2AI();
        }
    }

    void TogglePlayer1AI()
    {
        bool newState = !player1AI.IsAIControlled();
        player1AI.SetAIControl(newState);
        
        // Disable player controller when AI is active
        if (player1Controller != null)
            player1Controller.enabled = !newState;
            
        Debug.Log($"Player 1 AI: {(newState ? "ENABLED" : "DISABLED")}");
    }

    void TogglePlayer2AI()
    {
        bool newState = !player2AI.IsAIControlled();
        player2AI.SetAIControl(newState);
        
        // Disable player controller when AI is active
        if (player2Controller != null)
            player2Controller.enabled = !newState;
            
        Debug.Log($"Player 2 AI: {(newState ? "ENABLED" : "DISABLED")}");
    }
}
