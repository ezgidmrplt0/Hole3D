using UnityEngine;
using System.Collections.Generic;

public class SpawnManager : MonoBehaviour
{
    [Header("Prefabs")]
    [Tooltip("List of Human prefabs to spawn.")]
    public List<GameObject> humanPrefabs;
    [Tooltip("List of Zombie prefabs to spawn.")]
    public List<GameObject> zombiePrefabs;



    [Header("Spawn Points")]
    [Tooltip("Drag empty GameObjects here to define where Humans spawn.")]
    public List<Transform> humanSpawnPoints;
    [Tooltip("Drag empty GameObjects here to define where Zombies spawn.")]
    public List<Transform> zombieSpawnPoints;

    [Header("Spawn Settings")]
    [Tooltip("Number of humans to spawn.")]
    public int humanCount = 10;
    [Tooltip("Number of zombies to spawn.")]
    public int zombieCount = 20;
    [Tooltip("Radius around the spawn point to place characters.")]
    public float spawnRadius = 6f;

    [Header("Raycast & Ground")]
    [Tooltip("Y offset for the raycast start position.")]
    public float raycastHeight = 10f;
    [Tooltip("Layer mask to detect ground.")]
    public LayerMask groundLayer;
    [Tooltip("Offset to add to the ground height when spawning.")]
    public float spawnHeightOffset = 0f;

    [Header("Collision Check")]
    [Tooltip("Layer mask for obstacles to avoid spawning inside.")]
    public LayerMask obstacleLayer;
    [Tooltip("Radius to check for existing objects around spawn point.")]
    public float collisionCheckRadius = 1f;
    [Tooltip("Minimum distance between spawned characters.")]
    public float minSpawnDistance = 1.5f;
    [Tooltip("Maximum attempts to find a valid position per character.")]
    public int maxSpawnAttempts = 30;

    private List<Vector3> spawnedPositions = new List<Vector3>();

    // Public method called by LevelManager
    public void ClearScene()
    {
        // Find all existing characters and destroy them
        var humans = GameObject.FindGameObjectsWithTag("Human");
        foreach (var h in humans) Destroy(h);

        var zombies = GameObject.FindGameObjectsWithTag("Zombie");
        foreach (var z in zombies) Destroy(z);
        
        spawnedPositions.Clear();
    }

    public void UpdateSpawnPoints(Transform mapRoot)
    {
        if (mapRoot == null) return;

        // 1. Try to find explicit containers
        Transform humanContainer = mapRoot.Find("SpawnPoints/Humans");
        Transform zombieContainer = mapRoot.Find("SpawnPoints/Zombies");

        // Clear previous references
        if (humanSpawnPoints == null) humanSpawnPoints = new List<Transform>();
        else humanSpawnPoints.Clear();

        if (zombieSpawnPoints == null) zombieSpawnPoints = new List<Transform>();
        else zombieSpawnPoints.Clear();

        // 2. Populate if found
        if (humanContainer != null)
        {
            foreach (Transform t in humanContainer) humanSpawnPoints.Add(t);
        }
        
        if (zombieContainer != null)
        {
            foreach (Transform t in zombieContainer) zombieSpawnPoints.Add(t);
        }

        // 3. Fallback: Use Map Bounds (Floor) if list is empty
        if (humanSpawnPoints.Count == 0 || zombieSpawnPoints.Count == 0)
        {
            Debug.Log("SpawnManager: Explicit spawn points not found. Generating dynamic points from Map Bounds...");
            GenerateDynamicSpawnPoints(mapRoot);
        }
        
        Debug.Log($"SpawnManager: Initialized with {humanSpawnPoints.Count} Human points and {zombieSpawnPoints.Count} Zombie points.");
    }

    private void GenerateDynamicSpawnPoints(Transform mapRoot)
    {
        // Try to find "Floor" or "Ground"
        Transform floor = mapRoot.Find("Floor");
        if (floor == null) floor = mapRoot.Find("Ground");
        
        Bounds bounds = new Bounds(Vector3.zero, new Vector3(10, 1, 10)); // Default fallback
        if (floor != null)
        {
            Renderer r = floor.GetComponent<Renderer>();
            if (r != null) bounds = r.bounds;
            else 
            {
                 Collider c = floor.GetComponent<Collider>();
                 if (c != null) bounds = c.bounds;
            }
        }
        else
        {
             // Try to infer from MapRoot children
             Renderer[] renderers = mapRoot.GetComponentsInChildren<Renderer>();
             if (renderers.Length > 0)
             {
                 bounds = renderers[0].bounds;
                 for (int i=1; i<renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
             }
        }

        // Create temporary spawn points
        GameObject dynamicRoot = new GameObject("DynamicSpawnPoints_Temp");
        dynamicRoot.transform.SetParent(mapRoot);
        
        // Generate X points
        int pointsToGenerate = 10;
        
        for (int i = 0; i < pointsToGenerate; i++)
        {
            // Human Point
            GameObject hInfo = new GameObject($"HumanSpawn_{i}");
            hInfo.transform.SetParent(dynamicRoot.transform);
            hInfo.transform.position = GetRandomPosInBounds(bounds);
            humanSpawnPoints.Add(hInfo.transform);

            // Zombie Point
            GameObject zInfo = new GameObject($"ZombieSpawn_{i}");
            zInfo.transform.SetParent(dynamicRoot.transform);
            zInfo.transform.position = GetRandomPosInBounds(bounds);
            zombieSpawnPoints.Add(zInfo.transform);
        }
    }

    private Vector3 GetRandomPosInBounds(Bounds b)
    {
        // Shrink bounds slightly to avoid edge items
        float x = Random.Range(b.min.x * 0.8f, b.max.x * 0.8f);
        float z = Random.Range(b.min.z * 0.8f, b.max.z * 0.8f);
        return new Vector3(x, b.center.y + 0.5f, z);
    }

    public void SpawnLevel(int humans, int zombies, bool isHordeMode = false)
    {
        humanCount = humans;
        zombieCount = zombies;
        
        // Spawn Humans (Always normal)
        for (int i = 0; i < humanCount; i++)
        {
            SpawnRandomPrefab(humanPrefabs, humanSpawnPoints, "Human");
        }

        // Spawn Zombies
        if (isHordeMode)
        {
            SpawnZombiesClustered();
        }
        else
        {
            // Normal Spawn
            for (int i = 0; i < zombieCount; i++)
            {
                SpawnRandomPrefab(zombiePrefabs, zombieSpawnPoints, "Zombie");
            }
        }
    }

    private void SpawnZombiesClustered()
    {
        if (zombiePrefabs == null || zombiePrefabs.Count == 0) return;
        if (zombieSpawnPoints == null || zombieSpawnPoints.Count == 0) return;

        // 1. Pick a SINGLE random center point for the horde
        Transform centerPoint = zombieSpawnPoints[Random.Range(0, zombieSpawnPoints.Count)];
        if (centerPoint == null) return;

        float hordeRadius = 2.5f; // Tight cluster
        float hordeMinDistance = 0.6f; // Very close to each other
        int attemptsPerZombie = 20;

        for (int i = 0; i < zombieCount; i++)
        {
            GameObject selectedPrefab = zombiePrefabs[Random.Range(0, zombiePrefabs.Count)];
            
            bool spawned = false;
            for (int attempt = 0; attempt < attemptsPerZombie; attempt++)
            {
                // Get point in small circle around center
                Vector3 candidatePos = GetPositionAroundPoint(centerPoint.position, hordeRadius);

                if (CheckValid(candidatePos))
                {
                    // Custom tight overlap check
                    if (IsPositionClear(candidatePos, hordeMinDistance))
                    {
                        Quaternion randomRotation = Quaternion.Euler(0, Random.Range(0, 360f), 0);
                        Instantiate(selectedPrefab, candidatePos, randomRotation);
                        spawnedPositions.Add(candidatePos);
                        spawned = true;
                        break;
                    }
                }
            }
            if (!spawned) Debug.LogWarning("SpawnManager: Could not squeeze zombie into horde!");
        }
        
        Debug.Log($"SpawnManager: Spawning {zombieCount} zombies in HORDE MODE at {centerPoint.name}");
    }

    // Helper for custom distance check
    private bool IsPositionClear(Vector3 pos, float minDist)
    {
         // 1. Obstacle Check
         Vector3 checkPos = pos + Vector3.up * 0.5f;
         if (obstacleLayer.value != 0 && Physics.CheckSphere(checkPos, collisionCheckRadius * 0.5f, obstacleLayer)) // Reduced radius
         {
             return false;
         }

         // 2. Distance Check
         foreach (Vector3 spawnedPos in spawnedPositions)
         {
             if (Vector3.Distance(pos, spawnedPos) < minDist) return false;
         }
         return true;
    }

    /* REMOVED OLD SpawnCharacters to avoid duplication, logic moved to SpawnLevel */

    private void SpawnRandomPrefab(List<GameObject> prefabs, List<Transform> spawnPoints, string debugName)
    {
        if (prefabs == null || prefabs.Count == 0)
        {
            Debug.LogWarning($"SpawnManager: No prefabs assigned for {debugName}!");
            return;
        }

        if (spawnPoints == null || spawnPoints.Count == 0)
        {
             Debug.LogWarning($"SpawnManager: No spawn points assigned for {debugName}! Please assign them in the Inspector.");
             return;
        }

        GameObject selectedPrefab = prefabs[Random.Range(0, prefabs.Count)];
        
        // Try finding a valid position multiple times
        for (int attempt = 0; attempt < maxSpawnAttempts; attempt++)
        {
            // Listeden rastgele bir nokta seç
            Transform randomPoint = spawnPoints[Random.Range(0, spawnPoints.Count)];
            
            if (randomPoint == null) continue;

            // O noktanın etrafında (spawnRadius kadar) rastgele bir yer bul
            // Böylece hepsi üst üste binmez
            Vector3 candidatePos = GetPositionAroundPoint(randomPoint.position, spawnRadius);

            if (CheckValid(candidatePos))
            {
                if (IsValidPosition(candidatePos))
                {
                    Quaternion randomRotation = Quaternion.Euler(0, Random.Range(0, 360f), 0);
                    Instantiate(selectedPrefab, candidatePos, randomRotation);
                    spawnedPositions.Add(candidatePos); // Kaydet
                    return; // Spawn successful, exit method
                }
            }
        }

        Debug.LogWarning($"SpawnManager: Could not find valid position for {debugName} after {maxSpawnAttempts} attempts.");
    }

    private bool IsValidPosition(Vector3 position)
    {
        if (!CheckValid(position)) return false;

        // 1. Engel Kontrolü (Obstacle Layer)
        Vector3 checkPos = position + Vector3.up * (collisionCheckRadius + 0.2f);
        
        if (obstacleLayer.value != 0 && Physics.CheckSphere(checkPos, collisionCheckRadius, obstacleLayer))
        {
            return false;
        }

        // 2. Diğer karakterlere mesafe kontrolü
        foreach (Vector3 spawnedPos in spawnedPositions)
        {
            if (Vector3.Distance(position, spawnedPos) < minSpawnDistance)
            {
                return false;
            }
        }

        return true;
    }

    private Vector3 GetPositionAroundPoint(Vector3 centerPoint, float radius)
    {
        // Safety check for centerPoint
        if (!CheckValid(centerPoint))
        {
            Debug.LogError("SpawnManager: CenterPoint is invalid (Infinity/NaN)! Skipping.");
            return Vector3.negativeInfinity;
        }

        // Rastgele bir ofset al (Daire içinde)
        Vector2 randomCircle = Random.insideUnitCircle * radius;
        Vector3 targetPos = centerPoint + new Vector3(randomCircle.x, 0, randomCircle.y);

        // Raycast ile zemine oturt
        // Yüksekten aşağıya bak
        Vector3 rayStart = new Vector3(targetPos.x, centerPoint.y + raycastHeight, targetPos.z);
        
        if (groundLayer.value == 0)
        {
             // Ground layer yoksa referans aldığı transformun Y'sini kullan
             Vector3 result = new Vector3(targetPos.x, centerPoint.y + spawnHeightOffset, targetPos.z);
             return CheckValid(result) ? result : Vector3.negativeInfinity;
        }

        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, raycastHeight * 2f, groundLayer))
        {
            // Yere gömülmeyi önlemek için +0.5f ekliyoruz
            Vector3 result = hit.point + Vector3.up * (spawnHeightOffset + 0.5f);
             return CheckValid(result) ? result : Vector3.negativeInfinity;
        }
        
        return Vector3.negativeInfinity;
    }

    private bool CheckValid(Vector3 v)
    {
        if (float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z) || 
            float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z))
        {
            return false;
        }
        return true;
    }

    private void OnDrawGizmosSelected()
    {
        // Spawn noktalarını çiz
        if (humanSpawnPoints != null)
        {
            Gizmos.color = Color.green;
            foreach (var p in humanSpawnPoints)
            {
                if(p != null) Gizmos.DrawWireSphere(p.position, 3f);
            }
        }

        if (zombieSpawnPoints != null)
        {
            Gizmos.color = Color.red;
            foreach (var p in zombieSpawnPoints)
            {
               if(p != null) Gizmos.DrawWireSphere(p.position, 3f);
            }
        }
    }
}
