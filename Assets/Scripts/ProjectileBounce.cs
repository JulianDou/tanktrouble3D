using UnityEngine;

public class ProjectileBounce : MonoBehaviour
{
    private Rigidbody rb;
    private ParticleSystem collisionParticles;
    
    void Start()
    {
        rb = GetComponent<Rigidbody>();
        gameObject.tag = "Projectile";
        
        // Get and stop the collision particles so they don't play on spawn
        Transform particlesChild = transform.Find("Collision Particles");
        if (particlesChild != null)
        {
            collisionParticles = particlesChild.GetComponent<ParticleSystem>();
            if (collisionParticles != null)
            {
                collisionParticles.Stop();
            }
        }
    }
    
    void OnCollisionEnter(Collision collision)
    {
        Debug.Log("Projectile collided with: " + collision.gameObject.name + " (Tag: " + collision.gameObject.tag + ")");
        
        // Check if we hit the player
        if (collision.gameObject.CompareTag("Player"))
        {
            // Get the PlayerMovement component and call Die
            PlayerMovement player = collision.gameObject.GetComponent<PlayerMovement>();
            if (player != null)
            {
                player.Die();
            }
            
            // Destroy the projectile
            Destroy(gameObject);
        }
        
        // Check if we hit a wall
        if (collision.gameObject.CompareTag("Wall"))
        {
            Debug.Log("Wall collision detected! Particles null? " + (collisionParticles == null));
            // Play the collision particles animation
            if (collisionParticles != null)
            {
                collisionParticles.Play();
                Debug.Log("Playing particles!");
            }
        }
    }
}
