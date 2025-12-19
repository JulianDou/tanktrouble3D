using UnityEngine;
using UnityEngine.AI;

public class AIController : MonoBehaviour
{
    private Rigidbody rb;
    private PlayerMovement playerMovement;
    private NavMeshAgent navAgent;
    private Transform enemyPlayer;
    private Animator animator;
    private float targetUpdateTimer;
    private float targetUpdateInterval = 0.5f; // Update target every 0.5 seconds
    private Vector3 targetPosition;
    private bool hasLineOfSight = false;
    private Transform projectileSource;
    private float shootTimer;
    private float shootCooldown = 0.5f; // Time between shots
    private int shotsToFire = 3; // Number of shots to fire when has line of sight
    private int shotsFired = 0;
    
    // Stuck detection
    private Vector3 lastPosition;
    private float stuckTimer;
    private float stuckCheckInterval = 2f;
    private float minMovementDistance = 1f; // Minimum distance to move in check interval
    private int stuckCount = 0;
    
    // Projectile avoidance
    private bool isDodging = false;
    private float dodgeTimer = 0f;
    private float dodgeDuration = 1f;
    private Vector3 dodgeDirection;
    
    public bool isAIControlled = false;
    
    // Match PlayerMovement stats
    [SerializeField] private float moveSpeed = 15f;
    [SerializeField] private float rotationSpeed = 100f;
    [SerializeField] private float arrivalThreshold = 2f; // Distance to consider "arrived"
    [SerializeField] private float sightRange = 100f; // Maximum sight distance
    [SerializeField] private float sightAngle = 60f; // Field of view angle
    [SerializeField] private float waypointDistance = 5f; // Distance to next waypoint to aim for
    [SerializeField] private float projectileDetectionRange = 15f; // How far to detect projectiles
    [SerializeField] private float projectileDangerAngle = 30f; // Angle cone to consider projectile dangerous
    [SerializeField] private float powerupDetectionRange = 30f; // How far to detect powerups
    [SerializeField] private float enemySafetyDistance = 15f; // Minimum distance enemy should be for safe powerup collection

    void Start()
    {
        // Get PlayerMovement reference
        playerMovement = GetComponent<PlayerMovement>();
        
        // Get Rigidbody
        rb = GetComponent<Rigidbody>();
        
        // Get Animator
        animator = GetComponentInChildren<Animator>();
        
        // Get projectile source
        projectileSource = transform.Find("ProjectileSource");
        
        // Initialize stuck detection
        lastPosition = transform.position;
        
        // Get or create NavMeshAgent for pathfinding
        navAgent = GetComponent<NavMeshAgent>();
        if (navAgent == null)
        {
            navAgent = gameObject.AddComponent<NavMeshAgent>();
        }
        
        // Configure NavMeshAgent - it will only be used for pathfinding, not actual movement
        navAgent.speed = moveSpeed;
        navAgent.acceleration = 20f;
        navAgent.angularSpeed = rotationSpeed;
        navAgent.stoppingDistance = 0.5f;
        navAgent.autoBraking = false;
        navAgent.updatePosition = false; // Don't let NavAgent move the object
        navAgent.updateRotation = false; // Don't let NavAgent rotate the object
        
        // Set initial target to current position
        targetPosition = transform.position;
        
        // Find the enemy player
        FindEnemyPlayer();
    }

    void Update()
    {
        if (!isAIControlled)
        {
            return;
        }

        // PRIORITY 1: Projectile avoidance - check first!
        GameObject incomingProjectile = DetectIncomingProjectile();
        if (incomingProjectile != null && !isDodging)
        {
            StartDodge(incomingProjectile);
            
            // Interrupt shooting burst to dodge
            shotsFired = 0;
            shootTimer = 0;
        }
        
        // Handle active dodge
        if (isDodging)
        {
            dodgeTimer -= Time.deltaTime;
            if (dodgeTimer <= 0f)
            {
                isDodging = false;
            }
            else
            {
                // Execute dodge movement and skip normal behavior
                ExecuteDodge();
                return;
            }
        }

        // Update target periodically
        targetUpdateTimer += Time.deltaTime;
        
        if (targetUpdateTimer >= targetUpdateInterval)
        {
            UpdateTarget();
            targetUpdateTimer = 0;
        }
        
        // Shoot if we have line of sight
        if (hasLineOfSight && enemyPlayer != null)
        {
            ShootAtEnemy();
        }
        else
        {
            // Reset shot counter when we lose sight
            shotsFired = 0;
        }
        
        // Check if stuck
        stuckTimer += Time.deltaTime;
        if (stuckTimer >= stuckCheckInterval)
        {
            CheckIfStuck();
            stuckTimer = 0;
        }
        
        // Calculate movement inputs like a player would
        MoveTowardsTarget();
        
        // Sync NavMesh position for pathfinding
        if (navAgent != null && navAgent.enabled)
        {
            navAgent.nextPosition = transform.position;
        }
    }
    
    void ShootAtEnemy()
    {
        if (playerMovement == null || projectileSource == null)
            return;
        
        // Aim at the enemy
        Vector3 directionToEnemy = (enemyPlayer.position - transform.position).normalized;
        float angleToEnemy = Vector3.SignedAngle(transform.forward, directionToEnemy, Vector3.up);
        
        // Only shoot if roughly aimed at enemy (within 15 degrees)
        if (Mathf.Abs(angleToEnemy) > 15f)
            return;
        
        // Check if projectile path is clear (accounting for projectile radius)
        if (!IsProjectilePathClear(directionToEnemy))
            return;
        
        // Handle shooting timer and shot count
        shootTimer += Time.deltaTime;
        
        if (shootTimer >= shootCooldown && shotsFired < shotsToFire)
        {
            // Use PlayerMovement's OnAttack method through reflection
            var onAttackMethod = playerMovement.GetType().GetMethod("OnAttack", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (onAttackMethod != null)
            {
                onAttackMethod.Invoke(playerMovement, new object[] { });
                shotsFired++;
                shootTimer = 0;
            }
        }
        
        // Reset after firing all shots and wait a bit before next burst
        if (shotsFired >= shotsToFire)
        {
            shootTimer += Time.deltaTime;
            if (shootTimer >= shootCooldown * 3) // Wait 3x cooldown before next burst
            {
                shotsFired = 0;
                shootTimer = 0;
            }
        }
    }
    
    GameObject DetectIncomingProjectile()
    {
        GameObject[] projectiles = GameObject.FindGameObjectsWithTag("Projectile");
        GameObject nearestThreat = null;
        float nearestDistance = projectileDetectionRange;
        
        foreach (GameObject projectile in projectiles)
        {
            if (projectile == null)
                continue;
            
            Vector3 directionToProjectile = (projectile.transform.position - transform.position);
            float distance = directionToProjectile.magnitude;
            
            // Check if projectile is within detection range
            if (distance > projectileDetectionRange)
                continue;
            
            // Get projectile velocity to predict if it's heading towards us
            Rigidbody projRb = projectile.GetComponent<Rigidbody>();
            if (projRb == null)
                continue;
            
            Vector3 projectileVelocity = projRb.linearVelocity;
            if (projectileVelocity.magnitude < 1f)
                continue;
            
            // Calculate if projectile is moving towards us
            Vector3 toUs = (transform.position - projectile.transform.position).normalized;
            float velocityAlignment = Vector3.Dot(projectileVelocity.normalized, toUs);
            
            // If projectile is moving towards us (dot product > 0.5 means angle < 60 degrees)
            if (velocityAlignment > 0.5f)
            {
                // Check if we're in the projectile's path
                float angleFromProjectilePath = Vector3.Angle(-projectileVelocity, directionToProjectile);
                
                if (angleFromProjectilePath < projectileDangerAngle && distance < nearestDistance)
                {
                    nearestThreat = projectile;
                    nearestDistance = distance;
                }
            }
        }
        
        return nearestThreat;
    }
    
    void StartDodge(GameObject projectile)
    {
        isDodging = true;
        dodgeTimer = dodgeDuration;
        
        // Calculate dodge direction perpendicular to projectile velocity
        Rigidbody projRb = projectile.GetComponent<Rigidbody>();
        if (projRb != null)
        {
            Vector3 projectileDirection = projRb.linearVelocity.normalized;
            projectileDirection.y = 0;
            
            // Get perpendicular direction (choose left or right)
            Vector3 perpendicular = Vector3.Cross(projectileDirection, Vector3.up).normalized;
            
            // Choose the perpendicular direction that's closer to our current forward
            Vector3 rightPerp = perpendicular;
            Vector3 leftPerp = -perpendicular;
            
            // Pick the side that requires less rotation
            float rightDot = Vector3.Dot(transform.forward, rightPerp);
            float leftDot = Vector3.Dot(transform.forward, leftPerp);
            
            dodgeDirection = (rightDot > leftDot) ? rightPerp : leftPerp;
            
            // Set dodge target position - move 10 units in that direction
            Vector3 currentPos = transform.position;
            Vector3 dodgeTargetPos = currentPos + dodgeDirection * 10f;
            
            // Use NavMesh to find valid dodge position
            if (navAgent != null && navAgent.enabled)
            {
                UnityEngine.AI.NavMeshHit navHit;
                if (UnityEngine.AI.NavMesh.SamplePosition(dodgeTargetPos, out navHit, 10f, UnityEngine.AI.NavMesh.AllAreas))
                {
                    targetPosition = navHit.position;
                }
                else
                {
                    // Fallback: just move perpendicular
                    targetPosition = dodgeTargetPos;
                }
            }
            else
            {
                targetPosition = dodgeTargetPos;
            }
        }
        else
        {
            // Fallback: dodge to the side
            dodgeDirection = transform.right * (Random.value > 0.5f ? 1f : -1f);
            targetPosition = transform.position + dodgeDirection * 10f;
        }
    }
    
    void ExecuteDodge()
    {
        // Use the same player-like movement to reach dodge position
        // Turn toward dodge target and move forward
        Vector3 directionToTarget = (targetPosition - transform.position).normalized;
        directionToTarget.y = 0;
        
        if (directionToTarget.magnitude < 0.01f)
        {
            // Reached dodge position, end dodge early
            isDodging = false;
            return;
        }
        
        // Calculate angle to dodge target
        float angleToTarget = Vector3.SignedAngle(transform.forward, directionToTarget, Vector3.up);
        
        // Rotate toward target
        float rotationInput = 0f;
        if (Mathf.Abs(angleToTarget) > 5f)
        {
            rotationInput = Mathf.Clamp(angleToTarget / 45f, -1f, 1f);
        }
        
        transform.Rotate(Vector3.up * rotationInput * rotationSpeed * Time.deltaTime);
        
        // Move forward if facing the right direction
        float forwardInput = 0f;
        if (Mathf.Abs(angleToTarget) < 90f)
        {
            forwardInput = 1.2f; // Move 20% faster while dodging
        }
        
        // Apply movement
        if (rb != null)
        {
            rb.linearVelocity = transform.forward * forwardInput * moveSpeed + new Vector3(0, rb.linearVelocity.y, 0);
        }
        
        // Update animator for dodge movement
        if (animator != null)
        {
            animator.SetBool("Forwards", forwardInput > 0.1f);
            animator.SetBool("Backwards", forwardInput < -0.1f);
            animator.SetBool("Left", rotationInput < -0.1f);
            animator.SetBool("Right", rotationInput > 0.1f);
        }
    }
    
    void CheckIfStuck()
    {
        float distanceMoved = Vector3.Distance(transform.position, lastPosition);
        
        if (distanceMoved < minMovementDistance && !hasLineOfSight)
        {
            stuckCount++;
            
            // If stuck multiple times, try to get unstuck
            if (stuckCount >= 2)
            {
                // Move back slightly and rotate
                if (rb != null)
                {
                    rb.linearVelocity = -transform.forward * moveSpeed * 0.5f + new Vector3(0, rb.linearVelocity.y, 0);
                }
                transform.Rotate(Vector3.up * Random.Range(90f, 180f));
                
                // Reset path
                if (navAgent != null && navAgent.enabled)
                {
                    navAgent.ResetPath();
                }
                
                stuckCount = 0;
            }
        }
        else
        {
            stuckCount = 0;
        }
        
        lastPosition = transform.position;
    }
    
    void FindEnemyPlayer()
    {
        // Find all PlayerMovement components
        PlayerMovement[] allPlayers = FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None);
        
        // Find the player that is NOT this one
        foreach (PlayerMovement player in allPlayers)
        {
            if (player.gameObject != gameObject)
            {
                enemyPlayer = player.transform;
                break;
            }
        }
        
        if (enemyPlayer == null)
        {
            Debug.LogWarning($"{gameObject.name} - Could not find enemy player!");
        }
    }
    
    bool HasPowerupEquipped()
    {
        if (playerMovement == null)
            return false;
        
        // Check if current weapon is not Default (meaning a powerup is equipped)
        return playerMovement.currentWeapon != PlayerMovement.WeaponType.Default;
    }
    
    bool IsEnemyFarEnough()
    {
        if (enemyPlayer == null)
            return false;
        
        float distanceToEnemy = Vector3.Distance(transform.position, enemyPlayer.position);
        return distanceToEnemy >= enemySafetyDistance;
    }
    
    GameObject FindNearestPowerup()
    {
        GameObject[] powerups = GameObject.FindGameObjectsWithTag("Powerup");
        GameObject nearest = null;
        float nearestDistance = powerupDetectionRange;
        
        foreach (GameObject powerup in powerups)
        {
            if (powerup == null)
                continue;
            
            float distance = Vector3.Distance(transform.position, powerup.transform.position);
            
            if (distance < nearestDistance)
            {
                nearest = powerup;
                nearestDistance = distance;
            }
        }
        
        return nearest;
    }
    
    float GetCurrentProjectileRadius()
    {
        if (playerMovement == null)
            return 0.5f; // Default radius
        
        // Get weapon stats using reflection to access the private method
        var getWeaponStatsMethod = playerMovement.GetType().GetMethod("GetWeaponStats",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        if (getWeaponStatsMethod != null)
        {
            object stats = getWeaponStatsMethod.Invoke(playerMovement, new object[] { playerMovement.currentWeapon });
            
            // Get the size field from the WeaponStats struct
            var sizeField = stats.GetType().GetField("size");
            if (sizeField != null)
            {
                return (float)sizeField.GetValue(stats);
            }
        }
        
        // Fallback to default
        return 0.5f;
    }
    
    bool IsProjectilePathClear(Vector3 direction)
    {
        if (enemyPlayer == null)
            return false;
        
        Vector3 rayOrigin = projectileSource != null ? projectileSource.position : transform.position + Vector3.up * 0.5f;
        float distanceToEnemy = Vector3.Distance(transform.position, enemyPlayer.position);
        float projectileRadius = GetCurrentProjectileRadius();
        
        RaycastHit hit;
        if (Physics.SphereCast(rayOrigin, projectileRadius, direction, out hit, distanceToEnemy))
        {
            // Check if we hit the enemy player
            if (hit.transform == enemyPlayer || hit.transform.IsChildOf(enemyPlayer))
            {
                return true; // Path is clear to enemy
            }
            
            // Hit an obstacle (wall corner, etc.)
            return false;
        }
        
        // Nothing in the way
        return true;
    }
    
    void UpdateTarget()
    {
        if (enemyPlayer == null)
        {
            FindEnemyPlayer();
            if (enemyPlayer == null)
                return;
        }
        
        // Check for line of sight
        hasLineOfSight = CheckLineOfSight();
        
        // PRIORITY: Check for safe powerup collection opportunity
        // Only if we don't have a powerup equipped and enemy is far enough
        if (!HasPowerupEquipped() && IsEnemyFarEnough())
        {
            GameObject nearestPowerup = FindNearestPowerup();
            if (nearestPowerup != null)
            {
                // Path to the powerup instead of the enemy
                if (navAgent != null && navAgent.enabled)
                {
                    navAgent.SetDestination(nearestPowerup.transform.position);
                    
                    // Get the next waypoint from the path
                    if (navAgent.hasPath && navAgent.path.corners.Length > 1)
                    {
                        targetPosition = navAgent.path.corners[1];
                    }
                    else if (navAgent.hasPath && navAgent.path.corners.Length > 0)
                    {
                        targetPosition = navAgent.path.corners[0];
                    }
                    else
                    {
                        targetPosition = nearestPowerup.transform.position;
                    }
                }
                else
                {
                    targetPosition = nearestPowerup.transform.position;
                }
                return; // Skip normal enemy tracking behavior
            }
        }
        
        // If we don't have line of sight, use NavMesh pathfinding
        if (!hasLineOfSight)
        {
            // Update NavMesh path to enemy
            if (navAgent != null && navAgent.enabled)
            {
                navAgent.SetDestination(enemyPlayer.position);
                
                // Get the next waypoint from the path
                if (navAgent.hasPath && navAgent.path.corners.Length > 1)
                {
                    // Use the next corner in the path as our immediate target
                    targetPosition = navAgent.path.corners[1];
                }
                else if (navAgent.hasPath && navAgent.path.corners.Length > 0)
                {
                    targetPosition = navAgent.path.corners[0];
                }
                else
                {
                    // No path available, aim directly at enemy
                    targetPosition = enemyPlayer.position;
                }
            }
            else
            {
                targetPosition = enemyPlayer.position;
            }
        }
        else
        {
            // We have line of sight - maintain current target (stop moving closer)
            // The AI can now focus on aiming/shooting
            targetPosition = transform.position;
        }
    }
    
    bool CheckLineOfSight()
    {
        if (enemyPlayer == null)
            return false;
        
        Vector3 directionToEnemy = (enemyPlayer.position - transform.position);
        float distanceToEnemy = directionToEnemy.magnitude;
        directionToEnemy.Normalize();
        
        // Check if enemy is within sight range
        if (distanceToEnemy > sightRange)
            return false;
        
        // Check if enemy is within field of view
        float angleToEnemy = Vector3.Angle(transform.forward, directionToEnemy);
        if (angleToEnemy > sightAngle)
            return false;
        
        // Use SphereCast to check for obstacles, accounting for projectile radius
        RaycastHit hit;
        Vector3 rayOrigin = transform.position + Vector3.up * 0.5f;
        float projectileRadius = GetCurrentProjectileRadius();
        
        if (Physics.SphereCast(rayOrigin, projectileRadius, directionToEnemy, out hit, distanceToEnemy))
        {
            // Check if we hit the enemy player directly
            if (hit.transform == enemyPlayer || hit.transform.IsChildOf(enemyPlayer))
            {
                return true;
            }
            
            // Hit something else (wall, obstacle, etc.) - no line of sight
            return false;
        }
        
        // SphereCast didn't hit anything - should not happen in a normal scene, but assume no line of sight to be safe
        return false;
    }
    
    void MoveTowardsTarget()
    {
        // Don't move if we have line of sight and are facing the enemy
        if (hasLineOfSight && enemyPlayer != null)
        {
            Vector3 directionToEnemy = (enemyPlayer.position - transform.position).normalized;
            float angleToEnemy = Vector3.SignedAngle(transform.forward, directionToEnemy, Vector3.up);
            
            // Just rotate to face enemy, don't move forward
            float rotationInput = 0f;
            if (Mathf.Abs(angleToEnemy) > 2f)
            {
                rotationInput = Mathf.Clamp(angleToEnemy / 45f, -1f, 1f);
                transform.Rotate(Vector3.up * rotationInput * rotationSpeed * Time.deltaTime);
            }
            
            // Update animator - rotating in place
            if (animator != null)
            {
                animator.SetBool("Forwards", false);
                animator.SetBool("Backwards", false);
                animator.SetBool("Left", rotationInput < -0.1f);
                animator.SetBool("Right", rotationInput > 0.1f);
            }
            
            // Stop moving
            if (rb != null)
            {
                rb.linearVelocity = new Vector3(0, rb.linearVelocity.y, 0);
            }
            return;
        }
        
        // Calculate direction to target
        Vector3 directionToTarget = (targetPosition - transform.position).normalized;
        directionToTarget.y = 0; // Keep on horizontal plane
        
        if (directionToTarget.magnitude < 0.01f)
            return;
        
        // Calculate the angle difference between forward direction and target
        float angleToTarget = Vector3.SignedAngle(transform.forward, directionToTarget, Vector3.up);
        
        // Determine rotation input (like pressing left/right)
        float targetRotationInput = 0f;
        if (Mathf.Abs(angleToTarget) > 5f) // Deadzone to prevent jittering
        {
            targetRotationInput = Mathf.Clamp(angleToTarget / 45f, -1f, 1f); // Normalize to -1 to 1
        }
        
        // Apply rotation like player's horizontal input
        transform.Rotate(Vector3.up * targetRotationInput * rotationSpeed * Time.deltaTime);
        
        // Determine forward/backward movement
        // If roughly facing the target, move forward; if backwards, move backward
        float forwardInput = 0f;
        if (Mathf.Abs(angleToTarget) < 90f)
        {
            // Target is in front, move forward
            forwardInput = 1f;
        }
        else
        {
            // Target is behind, move backward (optional - or just turn)
            forwardInput = 0f; // AI will only turn when target is behind
        }
        
        // Apply forward movement like player
        if (rb != null)
        {
            rb.linearVelocity = transform.forward * forwardInput * moveSpeed + new Vector3(0, rb.linearVelocity.y, 0);
        }
        
        // Update animator parameters based on AI movement
        if (animator != null)
        {
            animator.SetBool("Forwards", forwardInput > 0.1f);
            animator.SetBool("Backwards", forwardInput < -0.1f);
            animator.SetBool("Left", targetRotationInput < -0.1f);
            animator.SetBool("Right", targetRotationInput > 0.1f);
        }
    }

    public void SetAIControl(bool enabled)
    {
        isAIControlled = enabled;
        
        if (!enabled)
        {
            // Stop movement when disabling AI
            if (rb != null)
            {
                rb.linearVelocity = new Vector3(0, rb.linearVelocity.y, 0);
            }
            if (navAgent != null)
            {
                navAgent.ResetPath();
            }
            
            // Reset all animations to idle
            if (animator != null)
            {
                animator.SetBool("Forwards", false);
                animator.SetBool("Backwards", false);
                animator.SetBool("Left", false);
                animator.SetBool("Right", false);
            }
        }
        else
        {
            // Reset target when enabling AI
            targetPosition = transform.position;
            FindEnemyPlayer();
            if (navAgent != null)
            {
                navAgent.enabled = true;
            }
        }
    }

    public bool IsAIControlled()
    {
        return isAIControlled;
    }

    // Handle respawn for AI-controlled players
    public void Respawn(Vector3 position)
    {
        // Call PlayerMovement respawn if available
        if (playerMovement != null)
        {
            // Temporarily enable PlayerMovement to allow respawn logic to execute
            bool wasEnabled = playerMovement.enabled;
            playerMovement.enabled = true;
            
            playerMovement.Respawn(position);
            
            // Restore original state (disabled if AI is active)
            playerMovement.enabled = wasEnabled;
        }

        // Reset AI target position and NavMesh
        if (isAIControlled)
        {
            targetPosition = position;
            targetUpdateTimer = targetUpdateInterval; // Trigger immediate target update
            FindEnemyPlayer();
            
            // Reset stuck detection
            lastPosition = position;
            stuckTimer = 0;
            stuckCount = 0;
            shotsFired = 0;
            shootTimer = 0;
            isDodging = false;
            dodgeTimer = 0f;
            
            if (navAgent != null)
            {
                navAgent.enabled = false;
                navAgent.Warp(position);
                navAgent.enabled = true;
                navAgent.ResetPath();
            }
        }
    }
}
