using UnityEngine;
using Unity.AI.Navigation; // Add this for NavMeshSurface

public class MapScript : MonoBehaviour
{
    public GameObject tilePrefab;
    public Material wallMaterial; // Material for boundary walls
    public int mapWidth = 9;
    public int mapHeight = 9;
    public float tileSize = 20f;
    public float wallHeight = 4f;
    [Range(0f, 1f)]
    public float additionalWallRemovalChance = 0.4f; // Chance to remove extra walls for more open spaces
    [Range(0f, 0.3f)]
    public float wallRemovalVariation = 0.1f; // Random variation added to removal chance each generation

    private TileScript[,] tiles;
    private NavMeshSurface navMeshSurface; // Reference to NavMeshSurface component

    void Start()
    {
        // Get or add NavMeshSurface component
        navMeshSurface = GetComponent<NavMeshSurface>();
        if (navMeshSurface == null)
        {
            navMeshSurface = gameObject.AddComponent<NavMeshSurface>();
            Debug.Log("Added NavMeshSurface component to MapScript");
        }

        StartCoroutine(InitialMapGeneration());
    }
    
    System.Collections.IEnumerator InitialMapGeneration()
    {
        GenerateMap();
        GenerateBoundaryWalls();
        
        // Wait for physics to initialize colliders
        yield return new WaitForEndOfFrame();
        
        BakeNavMesh();
    }

    // Public method that can be called from other scripts
    public void RegenerateMap()
    {
        StartCoroutine(RegenerateMapCoroutine());
    }

    System.Collections.IEnumerator RegenerateMapCoroutine()
    {
        // Clear all existing tiles and boundary walls
        foreach (Transform child in transform)
        {
            Destroy(child.gameObject);
        }

        // Wait for end of frame to ensure objects are destroyed
        yield return new WaitForEndOfFrame();

        // Generate new map
        GenerateMap();
        GenerateBoundaryWalls();
        
        // Wait another frame before baking NavMesh
        yield return new WaitForEndOfFrame();
        
        BakeNavMesh();
    }

    void BakeNavMesh()
    {
        if (navMeshSurface != null)
        {
            Debug.Log("Baking NavMesh...");
            navMeshSurface.BuildNavMesh();
            Debug.Log("NavMesh baked successfully!");
        }
        else
        {
            Debug.LogError("NavMeshSurface component not found!");
        }
    }

    void GenerateMap()
    {
        // Generate the grid of tiles
        GenerateTileGrid();

        Debug.Log("Starting maze generation...");

        // Initialize all walls to active
        int wallCount = 0;
        foreach (TileScript tile in tiles)
        {
            if (tile != null)
            {
                if (tile.tileWallNorth != null) { tile.tileWallNorth.SetActive(true); wallCount++; }
                if (tile.tileWallSouth != null) { tile.tileWallSouth.SetActive(true); wallCount++; }
                if (tile.tileWallEast != null) { tile.tileWallEast.SetActive(true); wallCount++; }
                if (tile.tileWallWest != null) { tile.tileWallWest.SetActive(true); wallCount++; }
            }
        }
        Debug.Log($"Initialized {wallCount} walls");

        // Generate maze using recursive backtracking
        GenerateMaze();
        
        // Count remaining walls after maze generation
        int remainingWalls = 0;
        foreach (TileScript tile in tiles)
        {
            if (tile != null)
            {
                if (tile.tileWallNorth != null && tile.tileWallNorth.activeSelf) remainingWalls++;
                if (tile.tileWallSouth != null && tile.tileWallSouth.activeSelf) remainingWalls++;
                if (tile.tileWallEast != null && tile.tileWallEast.activeSelf) remainingWalls++;
                if (tile.tileWallWest != null && tile.tileWallWest.activeSelf) remainingWalls++;
            }
        }
        Debug.Log($"Walls after maze generation: {remainingWalls}");

        // Remove additional walls to create more open spaces
        RemoveAdditionalWalls();

        // Count final walls
        int finalWalls = 0;
        foreach (TileScript tile in tiles)
        {
            if (tile != null)
            {
                if (tile.tileWallNorth != null && tile.tileWallNorth.activeSelf) finalWalls++;
                if (tile.tileWallSouth != null && tile.tileWallSouth.activeSelf) finalWalls++;
                if (tile.tileWallEast != null && tile.tileWallEast.activeSelf) finalWalls++;
                if (tile.tileWallWest != null && tile.tileWallWest.activeSelf) finalWalls++;
            }
        }
        Debug.Log($"Final walls after removal: {finalWalls}");
        Debug.Log($"Map generated: {mapWidth}x{mapHeight} tiles");
    }

    void GenerateMaze()
    {
        bool[,] visited = new bool[mapWidth, mapHeight];
        System.Random random = new System.Random();

        // Start from random position
        int startX = random.Next(0, mapWidth);
        int startZ = random.Next(0, mapHeight);

        CarvePath(startX, startZ, visited, random);
    }

    void CarvePath(int x, int z, bool[,] visited, System.Random random)
    {
        visited[x, z] = true;

        // Get random order of directions
        int[] directions = { 0, 1, 2, 3 };
        Shuffle(directions, random);

        foreach (int dir in directions)
        {
            int nextX = x;
            int nextZ = z;

            // Calculate neighbor coordinates
            switch (dir)
            {
                case 0: nextZ++; break; // North
                case 1: nextZ--; break; // South
                case 2: nextX++; break; // East
                case 3: nextX--; break; // West
            }

            // Check if neighbor is valid and unvisited
            if (IsValidCell(nextX, nextZ) && !visited[nextX, nextZ])
            {
                // Remove wall between current and next cell
                RemoveWallBetween(x, z, nextX, nextZ);

                // Recursively carve from next cell
                CarvePath(nextX, nextZ, visited, random);
            }
        }
    }

    bool IsValidCell(int x, int z)
    {
        return x >= 0 && x < mapWidth && z >= 0 && z < mapHeight;
    }

    void RemoveWallBetween(int x1, int z1, int x2, int z2)
    {
        TileScript tile1 = tiles[x1, z1];
        TileScript tile2 = tiles[x2, z2];

        if (tile1 == null || tile2 == null)
        {
            Debug.LogWarning($"Null tile found at ({x1},{z1}) or ({x2},{z2})");
            return;
        }

        // Determine direction and remove corresponding walls
        if (x2 > x1) // Moving East
        {
            if (tile1.tileWallEast != null) tile1.tileWallEast.SetActive(false);
            if (tile2.tileWallWest != null) tile2.tileWallWest.SetActive(false);
        }
        else if (x2 < x1) // Moving West
        {
            if (tile1.tileWallWest != null) tile1.tileWallWest.SetActive(false);
            if (tile2.tileWallEast != null) tile2.tileWallEast.SetActive(false);
        }
        else if (z2 > z1) // Moving North
        {
            if (tile1.tileWallNorth != null) tile1.tileWallNorth.SetActive(false);
            if (tile2.tileWallSouth != null) tile2.tileWallSouth.SetActive(false);
        }
        else if (z2 < z1) // Moving South
        {
            if (tile1.tileWallSouth != null) tile1.tileWallSouth.SetActive(false);
            if (tile2.tileWallNorth != null) tile2.tileWallNorth.SetActive(false);
        }
    }

    void Shuffle(int[] array, System.Random random)
    {
        for (int i = array.Length - 1; i > 0; i--)
        {
            int j = random.Next(0, i + 1);
            int temp = array[i];
            array[i] = array[j];
            array[j] = temp;
        }
    }

    void RemoveAdditionalWalls()
    {
        System.Random random = new System.Random();
        
        // Add random variation to the removal chance
        float variation = (float)(random.NextDouble() * 2 - 1) * wallRemovalVariation; // Range: -variation to +variation
        float actualRemovalChance = Mathf.Clamp01(additionalWallRemovalChance + variation);
        Debug.Log($"Wall removal chance this generation: {actualRemovalChance:F2} (base: {additionalWallRemovalChance:F2}, variation: {variation:F2})");
        
        for (int x = 0; x < mapWidth; x++)
        {
            for (int z = 0; z < mapHeight; z++)
            {
                TileScript tile = tiles[x, z];
                if (tile == null) continue;
                
                // Try to remove north wall (if there's a neighbor)
                if (z < mapHeight - 1 && random.NextDouble() < actualRemovalChance)
                {
                    if (tile.tileWallNorth != null && tile.tileWallNorth.activeSelf)
                    {
                        tile.tileWallNorth.SetActive(false);
                        if (tiles[x, z + 1].tileWallSouth != null)
                            tiles[x, z + 1].tileWallSouth.SetActive(false);
                    }
                }
                
                // Try to remove east wall (if there's a neighbor)
                if (x < mapWidth - 1 && random.NextDouble() < actualRemovalChance)
                {
                    if (tile.tileWallEast != null && tile.tileWallEast.activeSelf)
                    {
                        tile.tileWallEast.SetActive(false);
                        if (tiles[x + 1, z].tileWallWest != null)
                            tiles[x + 1, z].tileWallWest.SetActive(false);
                    }
                }
            }
        }
    }

    void GenerateTileGrid()
    {
        tiles = new TileScript[mapWidth, mapHeight];

        for (int x = 0; x < mapWidth; x++)
        {
            for (int z = 0; z < mapHeight; z++)
            {
                // Calculate position - centered around origin
                float xPos = (x - mapWidth / 2) * tileSize;
                float zPos = (z - mapHeight / 2) * tileSize;
                Vector3 position = new Vector3(xPos, 0, zPos);

                // Instantiate tile
                GameObject tileObj = Instantiate(tilePrefab, position, Quaternion.identity, transform);
                tileObj.name = $"Tile_{x}_{z}";

                // Store reference to tile script
                TileScript tileScript = tileObj.GetComponent<TileScript>();
                tiles[x, z] = tileScript;

                // Manually find and assign walls immediately
                if (tileScript != null)
                {
                    tileScript.tileWallNorth = tileObj.transform.Find("TileWallNorth")?.gameObject;
                    tileScript.tileWallSouth = tileObj.transform.Find("TileWallSouth")?.gameObject;
                    tileScript.tileWallEast = tileObj.transform.Find("TileWallEast")?.gameObject;
                    tileScript.tileWallWest = tileObj.transform.Find("TileWallWest")?.gameObject;

                    if (tileScript.tileWallNorth == null)
                        Debug.LogWarning($"Could not find TileWallNorth in {tileObj.name}");
                }
            }
        }

        Debug.Log($"Generated {mapWidth}x{mapHeight} tile grid");
    }

    void ClearTiles()
    {
        if (tiles != null)
        {
            foreach (TileScript tile in tiles)
            {
                if (tile != null)
                {
                    Destroy(tile.gameObject);
                }
            }
        }
        tiles = null;
    }

    void ClearAllWalls()
    {
        foreach (TileScript tile in tiles)
        {
            if (tile != null)
            {
                if (tile.tileWallNorth != null) tile.tileWallNorth.SetActive(false);
                if (tile.tileWallSouth != null) tile.tileWallSouth.SetActive(false);
                if (tile.tileWallEast != null) tile.tileWallEast.SetActive(false);
                if (tile.tileWallWest != null) tile.tileWallWest.SetActive(false);
            }
        }
    }

    void GenerateBoundaryWalls()
    {
        float halfWidth = (mapWidth * tileSize) / 2f;
        float halfHeight = (mapHeight * tileSize) / 2f;

        // North wall (along top edge)
        CreateWall(
            new Vector3(0, wallHeight / 2f, halfHeight),
            new Vector3(mapWidth * tileSize + 0.4f, wallHeight, 1.2f)
        );

        // South wall (along bottom edge)
        CreateWall(
            new Vector3(0, wallHeight / 2f, -halfHeight),
            new Vector3(mapWidth * tileSize + 0.4f, wallHeight, 1.2f)
        );

        // East wall (along right edge)
        CreateWall(
            new Vector3(halfWidth, wallHeight / 2f, 0),
            new Vector3(1.2f, wallHeight, mapHeight * tileSize + 0.4f)
        );

        // West wall (along left edge)
        CreateWall(
            new Vector3(-halfWidth, wallHeight / 2f, 0),
            new Vector3(1.2f, wallHeight, mapHeight * tileSize + 0.4f)
        );
    }

    void CreateWall(Vector3 position, Vector3 scale)
    {
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.transform.position = position;
        wall.transform.localScale = scale;
        wall.transform.parent = transform;
        wall.name = "BoundaryWall";
        wall.tag = "Wall";
        wall.layer = LayerMask.NameToLayer("Walls");
        
        // Apply material if one is assigned
        if (wallMaterial != null)
        {
            Renderer renderer = wall.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material = wallMaterial;
            }
        }
        else
        {
            Debug.LogWarning("No wall material assigned to MapScript. Boundary walls may not render properly in builds.");
        }
    }

    void Update()
    {

    }
    
    // Public method to get a random tile position for spawning powerups
    public Vector3 GetRandomTilePosition()
    {
        if (tiles == null || tiles.Length == 0)
        {
            Debug.LogWarning("No tiles available in map!");
            return Vector3.zero;
        }
        
        System.Random random = new System.Random();
        int randomX = random.Next(0, mapWidth);
        int randomZ = random.Next(0, mapHeight);
        
        TileScript tile = tiles[randomX, randomZ];
        if (tile != null)
        {
            return tile.transform.position;
        }
        
        Debug.LogWarning("Selected tile was null, returning origin");
        return Vector3.zero;
    }
}
