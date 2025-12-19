using UnityEngine;

public class TileScript : MonoBehaviour
{
    public GameObject tileWallNorth;
    public GameObject tileWallSouth;
    public GameObject tileWallEast;
    public GameObject tileWallWest;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // If you haven't assigned the walls in the inspector, find them automatically
        if (tileWallNorth == null)
        {
            tileWallNorth = transform.Find("TileWallNorth")?.gameObject;
            tileWallSouth = transform.Find("TileWallSouth")?.gameObject;
            tileWallEast = transform.Find("TileWallEast")?.gameObject;
            tileWallWest = transform.Find("TileWallWest")?.gameObject;
        }
        
        // Don't randomize here - let MapScript control wall generation
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    // Add method to remove a wall in a specific direction
    public void RemoveWall(Direction direction)
    {
        switch (direction)
        {
            case Direction.North: if (tileWallNorth != null) tileWallNorth.SetActive(false); break;
            case Direction.South: if (tileWallSouth != null) tileWallSouth.SetActive(false); break;
            case Direction.East: if (tileWallEast != null) tileWallEast.SetActive(false); break;
            case Direction.West: if (tileWallWest != null) tileWallWest.SetActive(false); break;
        }
    }

    public enum Direction { North, South, East, West }
}
