using UnityEngine;
using System.Collections;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    
    public GameObject powerupPrefab; // Powerup prefab to spawn
    public float powerupSpawnInterval = 15f; // Spawn a powerup every 15 seconds
    
    private MapScript mapScript;
    private PlayerMovement[] players;
    private Vector3[] spawnPositions = new Vector3[] 
    {
        new Vector3(-80, 0, 80),  // Player 1 spawn
        new Vector3(80, 0, -80)   // Player 2 spawn
    };
    
    private float powerupTimer = 0f;
    
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
        mapScript = FindFirstObjectByType<MapScript>();
        FindPlayers();
        powerupTimer = powerupSpawnInterval; // Start timer
    }
    
    void Update()
    {
        // Handle powerup spawning timer
        powerupTimer -= Time.deltaTime;
        
        if (powerupTimer <= 0f)
        {
            SpawnPowerup();
            powerupTimer = powerupSpawnInterval; // Reset timer
        }
    }
    
    void FindPlayers()
    {
        players = FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None);
        Debug.Log($"Found {players.Length} players");
    }
    
    public void OnPlayerDeath()
    {
        Debug.Log("Player died - starting respawn sequence");
        StartCoroutine(RespawnSequence());
    }
    
    IEnumerator RespawnSequence()
    {
        // Wait 1 second
        yield return new WaitForSeconds(1f);
        
        // Regenerate the map
        if (mapScript != null)
        {
            mapScript.RegenerateMap();
        }

        // Wait for map regeneration to complete (including NavMesh baking)
        yield return new WaitForSeconds(0.5f);

        // Destroy all projectiles
        GameObject[] projectiles = GameObject.FindGameObjectsWithTag("Projectile");
        foreach (GameObject projectile in projectiles)
        {
          Destroy(projectile);
        }
        
        // Destroy all powerups
        GameObject[] powerups = GameObject.FindGameObjectsWithTag("Powerup");
        foreach (GameObject powerup in powerups)
        {
            Destroy(powerup);
        }
        Debug.Log($"Destroyed {powerups.Length} powerups during map regeneration");
        
        // Respawn all players
        RespawnPlayers();
    }
    
    void RespawnPlayers()
    {
        // Find all players again in case references were lost
        FindPlayers();
        
        for (int i = 0; i < players.Length && i < spawnPositions.Length; i++)
        {
            if (players[i] != null)
            {
                // Check if player has AI controller
                AIController aiController = players[i].GetComponent<AIController>();
                
                if (aiController != null && aiController.IsAIControlled())
                {
                    // Use AIController's respawn method which handles both AI and player movement
                    aiController.Respawn(spawnPositions[i]);
                }
                else
                {
                    // Normal player respawn
                    players[i].Respawn(spawnPositions[i]);
                }
            }
        }
    }
    
    void SpawnPowerup()
    {
        if (powerupPrefab == null)
        {
            Debug.LogWarning("Powerup prefab not assigned in GameManager!");
            return;
        }
        
        if (mapScript == null)
        {
            Debug.LogWarning("MapScript reference is null!");
            return;
        }
        
        // Get a random tile position from the map
        Vector3 spawnPosition = mapScript.GetRandomTilePosition();
        
        // Spawn the powerup at the tile position (slightly elevated)
        Vector3 powerupPosition = new Vector3(spawnPosition.x, 1f, spawnPosition.z);
        GameObject powerup = Instantiate(powerupPrefab, powerupPosition, Quaternion.identity);
        
        Debug.Log($"Spawned powerup at position: {powerupPosition}");
    }
    
    // Called by PowerupScript when a player collects a powerup
    public void OnPowerupCollected(GameObject player, GameObject powerup)
    {
        Debug.Log($"Powerup collected by {player.name} at position {powerup.transform.position}");
        
        PlayerMovement playerMovement = player.GetComponent<PlayerMovement>();
        
        if (playerMovement != null)
        {
            // Check if player already has a powerup weapon
            if (playerMovement.currentWeapon != PlayerMovement.WeaponType.Default)
            {
                Debug.Log($"{player.name} already has a weapon. Powerup wasted.");
                Destroy(powerup);
                return;
            }
            
            // Pick a random weapon (exclude Default)
            PlayerMovement.WeaponType[] availableWeapons = new PlayerMovement.WeaponType[]
            {
                PlayerMovement.WeaponType.Minigun,
                PlayerMovement.WeaponType.Shotgun,
                PlayerMovement.WeaponType.Sniper
            };
            
            int randomIndex = Random.Range(0, availableWeapons.Length);
            PlayerMovement.WeaponType selectedWeapon = availableWeapons[randomIndex];
            
            // Apply the weapon to the player
            playerMovement.SetWeapon(selectedWeapon);
            
            Debug.Log($"{player.name} received weapon: {selectedWeapon}");
        }
        else
        {
            Debug.LogWarning($"PlayerMovement component not found on {player.name}");
        }
        
        // Destroy the powerup
        Destroy(powerup);
    }
}
