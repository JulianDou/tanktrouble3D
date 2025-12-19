using UnityEngine;

public class PowerupScript : MonoBehaviour
{
    // Optional: Add rotation or floating animation
    public float rotationSpeed = 50f;
    public float floatAmplitude = 0.5f;
    public float floatSpeed = 2f;
    
    private Vector3 startPosition;
    
    void Start()
    {
        startPosition = transform.position + Vector3.up;
    }

    void Update()
    {
        // Rotate the powerup for visual effect
        transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);
        
        float newY = startPosition.y + Mathf.Sin(Time.time * floatSpeed) * floatAmplitude;
        transform.position = new Vector3(transform.position.x, newY, transform.position.z);
    }
    
    void OnTriggerEnter(Collider other)
    {
        // Check if the object that touched the powerup is a player
        if (other.CompareTag("Player"))
        {
            // Notify GameManager about the collection
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnPowerupCollected(other.gameObject, gameObject);
            }
            else
            {
                Debug.LogWarning("GameManager instance not found! Destroying powerup anyway.");
                Destroy(gameObject);
            }
        }
    }
}
