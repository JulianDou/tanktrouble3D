using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Users;
using System.Collections.Generic;

public class PlayerMovement : MonoBehaviour
{
    Rigidbody rb;
    [SerializeField] int moveSpeed = 15;
    [SerializeField] int projectileSpeed = 50;
    [SerializeField] float rotationSpeed = 100f;
    [SerializeField] int maxProjectiles = 5;
    
    Vector2 current_movement;
    float horizontalInput;
    Transform playerCamera;
    Transform projectileSource;
    public GameObject Projectile;
    
    private List<GameObject> activeProjectiles = new List<GameObject>();
    private List<GameObject> powerupProjectiles = new List<GameObject>(); // Separate list for powerup projectiles
    private PlayerInput playerInput;
    private Animator animator;
    private ParticleSystem explosionParticles;
    
    // Minigun burst firing
    private bool minigunFiring = false;
    private float minigunFireTimer = 0f;
    private float minigunBurstDuration = 1f; // Fire for 1 second
    private float minigunFireInterval = 0.05f; // 20 shots per second = 0.05s between shots

    // Weapon powerup properties
    public enum WeaponType
    {
        Default,
        Minigun,
        Shotgun,
        Sniper
    }
    
    [Header("Weapon System")]
    public WeaponType currentWeapon = WeaponType.Default;
    public GameObject defaultWeaponContainer;
    public GameObject minigunWeaponContainer;
    public GameObject shotgunWeaponContainer;
    public GameObject sniperWeaponContainer;
    
    // Weapon-specific properties
    private struct WeaponStats
    {
        public float speed;
        public float size;
        public float accuracyCone; // Cone angle in degrees (0 = perfect accuracy)
        public float lifetime;
        public float fireRate; // Shots per second
        public int pelletsPerShot; // For shotgun
    }
    
    private WeaponStats GetWeaponStats(WeaponType weapon)
    {
        switch (weapon)
        {
            case WeaponType.Shotgun:
                return new WeaponStats
                {
                    speed = 70f,
                    size = 0.3f,
                    accuracyCone = 10f,
                    lifetime = 2f,
                    fireRate = 1f,
                    pelletsPerShot = 12
                };
            case WeaponType.Minigun:
                return new WeaponStats
                {
                    speed = 60f,
                    size = 0.3f,
                    accuracyCone = 5f,
                    lifetime = 5f,
                    fireRate = 20f, // 20 shots per second (handled by interval)
                    pelletsPerShot = 1
                };
            case WeaponType.Sniper:
                return new WeaponStats
                {
                    speed = 120f,
                    size = 0.8f,
                    accuracyCone = 0f,
                    lifetime = 12f,
                    fireRate = 0.5f,
                    pelletsPerShot = 1
                };
            default: // Default weapon
                return new WeaponStats
                {
                    speed = 50f,
                    size = 1f,
                    accuracyCone = 0f,
                    lifetime = 10f,
                    fireRate = 2f,
                    pelletsPerShot = 1
                };
        }
    }

    void Awake()
    {        
        playerInput = GetComponent<PlayerInput>();
        
        // Initialize transforms in Awake to prevent null reference
        projectileSource = transform.Find("ProjectileSource");
        
        // Get and hide explosion particles
        explosionParticles = transform.Find("Explosion Particles")?.GetComponent<ParticleSystem>();
        if (explosionParticles != null)
        {
            explosionParticles.gameObject.SetActive(false);
        }
        
        // Force pairing with keyboard and mouse for both players
        var keyboard = Keyboard.current;
        var mouse = Mouse.current;
        
        if (keyboard != null && mouse != null)
        {
            InputUser user = playerInput.user;
            InputUser.PerformPairingWithDevice(keyboard, user);
            InputUser.PerformPairingWithDevice(mouse, user);
        }
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        rb = GetComponent<Rigidbody>();
        animator = GetComponentInChildren<Animator>();
        
        UpdateWeaponVisuals();
    }

    // Update is called once per frame
    void Update()
    {
        // Forward movement only uses forward/backward input
        rb.linearVelocity = transform.forward * current_movement.y + new Vector3(0, rb.linearVelocity.y, 0);
      
        // Rotate player based on horizontal input (left/right)
        transform.Rotate(Vector3.up * horizontalInput * rotationSpeed * Time.deltaTime);
        
        // Handle minigun continuous fire
        if (minigunFiring)
        {
            minigunFireTimer += Time.deltaTime;
            
            // Check if burst is complete
            if (minigunFireTimer >= minigunBurstDuration)
            {
                minigunFiring = false;
                minigunFireTimer = 0f;
                Debug.Log($"{gameObject.name} minigun burst complete - reverting to default weapon");
                SetWeapon(WeaponType.Default);
            }
            else
            {
                // Fire at regular intervals
                if (Time.frameCount % Mathf.RoundToInt(minigunFireInterval / Time.fixedDeltaTime) == 0)
                {
                    WeaponStats stats = GetWeaponStats(WeaponType.Minigun);
                    FireProjectile(stats, true);
                }
            }
        }
        
        // Clean up destroyed powerup projectiles
        powerupProjectiles.RemoveAll(proj => proj == null);
    }

    void OnMove(InputValue value)
    {
        Vector2 input = value.Get<Vector2>();
        current_movement = new Vector2(0, input.y) * moveSpeed; // Only use Y for forward/backward
        horizontalInput = input.x; // Store X for rotation
        
        // Update animator parameters based on movement
        if (animator != null)
        {
            // Set boolean parameters based on input direction
            animator.SetBool("Forwards", input.y > 0.1f);
            animator.SetBool("Backwards", input.y < -0.1f);
            animator.SetBool("Left", input.x < -0.1f);
            animator.SetBool("Right", input.x > 0.1f);
        }
    }

    void OnAttack()
    {
        // Safety checks
        if (Projectile == null)
        {
            Debug.LogError($"{gameObject.name} - Projectile prefab is not assigned!");
            return;
        }

        if (projectileSource == null)
        {
            Debug.LogWarning($"{gameObject.name} - Missing projectile source!");
            return;
        }
        
        // Remove null references (destroyed projectiles)
        activeProjectiles.RemoveAll(proj => proj == null);
        
        bool isPowerupWeapon = currentWeapon != WeaponType.Default;
        
        // Check projectile limit (only for default weapon)
        if (!isPowerupWeapon && activeProjectiles.Count >= maxProjectiles)
        {
            Debug.Log($"{gameObject.name} - Cannot shoot! {activeProjectiles.Count}/{maxProjectiles} projectiles active");
            return;
        }
        
        // Handle minigun special case - start continuous fire
        if (currentWeapon == WeaponType.Minigun && !minigunFiring)
        {
            minigunFiring = true;
            minigunFireTimer = 0f;
            Debug.Log($"{gameObject.name} - Starting minigun burst");
            return; // Actual firing happens in Update()
        }
        
        // Don't allow firing if minigun is already firing
        if (minigunFiring)
        {
            return;
        }
        
        // Get weapon stats
        WeaponStats stats = GetWeaponStats(currentWeapon);
        
        // Fire the weapon based on type
        for (int i = 0; i < stats.pelletsPerShot; i++)
        {
            FireProjectile(stats, isPowerupWeapon);
        }
        
        Debug.Log($"{gameObject.name} - Fired {currentWeapon} ({(isPowerupWeapon ? "powerup" : $"{activeProjectiles.Count}/{maxProjectiles}")})");
        
        // Expire one-shot powerup weapons immediately (Shotgun, Sniper)
        if (isPowerupWeapon && currentWeapon != WeaponType.Minigun)
        {
            Debug.Log($"{gameObject.name} - {currentWeapon} expired after one shot");
            SetWeapon(WeaponType.Default);
        };
    }
    
    void FireProjectile(WeaponStats stats, bool isPowerupWeapon)
    {
        Vector3 spawnPosition = projectileSource.position;
        Quaternion spawnRotation = projectileSource.rotation;
        
        // Apply accuracy cone (random spread)
        if (stats.accuracyCone > 0f)
        {
            float randomYaw = Random.Range(-stats.accuracyCone / 2f, stats.accuracyCone / 2f);
            float randomPitch = Random.Range(-stats.accuracyCone / 2f, stats.accuracyCone / 2f);
            spawnRotation *= Quaternion.Euler(randomPitch, randomYaw, 0);
        }
        
        GameObject projectile = Instantiate(Projectile, spawnPosition, spawnRotation);
        
        // Scale projectile based on weapon
        projectile.transform.localScale = Vector3.one * stats.size;
        
        // Set velocity
        Rigidbody projRb = projectile.GetComponent<Rigidbody>();
        if (projRb != null)
        {
            projRb.linearVelocity = projectile.transform.forward * stats.speed;
        }
        
        // Track projectile appropriately
        if (isPowerupWeapon)
        {
            powerupProjectiles.Add(projectile);
        }
        else
        {
            activeProjectiles.Add(projectile);
        }
        
        // Destroy after lifetime
        Destroy(projectile, stats.lifetime);
    }

    bool isVoid()
    {
         LayerMask voidMask = LayerMask.GetMask("void");
        return Physics.Raycast(transform.position, -transform.up.normalized, 1.1f, voidMask);
    }

    public void Die()
    {
        // Show and play explosion particles
        if (explosionParticles != null)
        {
            explosionParticles.gameObject.SetActive(true);
            explosionParticles.Play();
        }
        
        // Disable movement
        enabled = false;
        
        // Hide the player (or destroy it)
        gameObject.transform.Find("TankModel").gameObject.SetActive(false);

        // Disable all box colliders
        BoxCollider[] boxColliders = GetComponentsInChildren<BoxCollider>();
        foreach (BoxCollider collider in boxColliders)
        {
          collider.enabled = false;
        }

        // Lock all rigidbody positions
        if (rb != null)
        {
          rb.constraints = RigidbodyConstraints.FreezePosition;
        }
        
        // Notify ScoreManager that this player died
        if (ScoreManager.Instance != null)
        {
            ScoreManager.Instance.OnPlayerKilled(gameObject);
        }
        
        // Notify GameManager
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnPlayerDeath();
        }
    }
    
    public void SetWeapon(WeaponType newWeapon)
    {
        // Don't change weapon if player already has a powerup weapon
        if (currentWeapon != WeaponType.Default && newWeapon != WeaponType.Default)
        {
            Debug.Log($"{gameObject.name} already has a weapon powerup: {currentWeapon}");
            return;
        }
        
        // Reset minigun state when changing weapons
        minigunFiring = false;
        minigunFireTimer = 0f;
        
        currentWeapon = newWeapon;
        UpdateWeaponVisuals();
        
        Debug.Log($"{gameObject.name} equipped weapon: {currentWeapon}");
    }
    
    void UpdateWeaponVisuals()
    {
        // Deactivate all weapon containers
        if (defaultWeaponContainer != null) defaultWeaponContainer.SetActive(false);
        if (minigunWeaponContainer != null) minigunWeaponContainer.SetActive(false);
        if (shotgunWeaponContainer != null) shotgunWeaponContainer.SetActive(false);
        if (sniperWeaponContainer != null) sniperWeaponContainer.SetActive(false);
        
        // Activate the current weapon container
        switch (currentWeapon)
        {
            case WeaponType.Default:
                if (defaultWeaponContainer != null) defaultWeaponContainer.SetActive(true);
                break;
            case WeaponType.Minigun:
                if (minigunWeaponContainer != null) minigunWeaponContainer.SetActive(true);
                break;
            case WeaponType.Shotgun:
                if (shotgunWeaponContainer != null) shotgunWeaponContainer.SetActive(true);
                break;
            case WeaponType.Sniper:
                if (sniperWeaponContainer != null) sniperWeaponContainer.SetActive(true);
                break;
        }
    }
    
    public void Respawn(Vector3 position)
    {
        // Reset position
        transform.position = position;
        // Set rotation based on player number
        if (gameObject.name.Contains("Player1"))
        {
            transform.rotation = Quaternion.Euler(0, 90, 0);
        }
        else if (gameObject.name.Contains("Player2"))
        {
            transform.rotation = Quaternion.Euler(0, -90, 0);
        }
        
        // Show the tank model
        Transform tankModel = gameObject.transform.Find("TankModel");
        if (tankModel != null)
        {
            tankModel.gameObject.SetActive(true);
        }
        
        // Re-enable all box colliders
        BoxCollider[] boxColliders = GetComponentsInChildren<BoxCollider>();
        foreach (BoxCollider collider in boxColliders)
        {
            collider.enabled = true;
        }
        
        // Unfreeze rigidbody and lock Y position
        if (rb != null)
        {
            rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        
        // Hide and stop explosion particles
        if (explosionParticles != null)
        {
            explosionParticles.Stop();
            explosionParticles.gameObject.SetActive(false);
        }
        
        // Clear movement
        current_movement = Vector2.zero;
        horizontalInput = 0;
        
        // Re-enable movement script
        enabled = true;
        
        // Reset weapon to default on respawn
        SetWeapon(WeaponType.Default);
    }
}
